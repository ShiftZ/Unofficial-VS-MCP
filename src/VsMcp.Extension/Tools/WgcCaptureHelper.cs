using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

namespace VsMcp.Extension.Tools
{
    /// <summary>
    /// Captures windows using the Windows.Graphics.Capture (WGC) API.
    /// Produces correct screenshots regardless of whether the target window
    /// is covered by other windows.
    /// Requires Windows 10 version 1903 (build 18362) or later.
    /// </summary>
    internal static class WgcCaptureHelper
    {
        #region P/Invoke

        [DllImport("d3d11.dll", PreserveSig = true)]
        private static extern int D3D11CreateDevice(
            IntPtr pAdapter,
            int driverType,
            IntPtr software,
            uint flags,
            IntPtr pFeatureLevels,
            uint featureLevels,
            uint sdkVersion,
            out IntPtr ppDevice,
            out int pFeatureLevel,
            out IntPtr ppImmediateContext);

        [DllImport("d3d11.dll", PreserveSig = true)]
        private static extern int CreateDirect3D11DeviceFromDXGIDevice(
            IntPtr dxgiDevice,
            out IntPtr graphicsDevice);

        [DllImport("combase.dll", PreserveSig = false)]
        private static extern void RoGetActivationFactory(
            [MarshalAs(UnmanagedType.HString)] string activatableClassId,
            [In] ref Guid iid,
            out IntPtr factory);

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, int count);

        private const int D3D_DRIVER_TYPE_HARDWARE = 1;
        private const int D3D_DRIVER_TYPE_WARP = 5;
        private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
        private const uint D3D11_SDK_VERSION = 7;

        #endregion

        #region COM Interfaces

        [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IGraphicsCaptureItemInterop
        {
            void CreateForWindow(
                IntPtr window,
                [In] ref Guid iid,
                out IntPtr result);
        }

        [ComImport, Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMemoryBufferByteAccess
        {
            void GetBuffer(out IntPtr buffer, out uint capacity);
        }

        #endregion

        private static readonly Guid IDXGIDeviceGuid =
            new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");

        private static readonly Guid IGraphicsCaptureItemGuid =
            new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");

        private static readonly Guid IGraphicsCaptureItemInteropGuid =
            new Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

        /// <summary>
        /// Returns true if Windows.Graphics.Capture is available on this OS.
        /// </summary>
        public static bool IsSupported
        {
            get
            {
                try
                {
                    return GraphicsCaptureSession.IsSupported();
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Captures the specified window using the WGC API.
        /// Returns null if the capture fails or no frame is obtained.
        /// </summary>
        public static async Task<Bitmap> CaptureWindowAsync(IntPtr hwnd)
        {
            IDirect3DDevice device = null;
            Direct3D11CaptureFramePool pool = null;
            GraphicsCaptureSession session = null;

            try
            {
                device = CreateDirect3DDevice();
                var item = CreateCaptureItemForWindow(hwnd);

                pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    1,
                    item.Size);

                session = pool.CreateCaptureSession(item);
                TryConfigureSession(session);
                session.StartCapture();

                // Poll for a frame with timeout (~1 second)
                Direct3D11CaptureFrame frame = null;
                for (int i = 0; i < 20 && frame == null; i++)
                {
                    frame = pool.TryGetNextFrame();
                    if (frame == null)
                        await Task.Delay(50);
                }

                if (frame == null)
                    return null;

                try
                {
                    return await FrameToBitmapAsync(frame);
                }
                finally
                {
                    frame.Dispose();
                }
            }
            finally
            {
                session?.Dispose();
                pool?.Dispose();
                device?.Dispose();
            }
        }

        #region D3D Device

        private static IDirect3DDevice CreateDirect3DDevice()
        {
            int hr = D3D11CreateDevice(
                IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                IntPtr.Zero, 0, D3D11_SDK_VERSION,
                out IntPtr d3dDevicePtr, out _, out IntPtr contextPtr);

            if (hr < 0)
            {
                // Fall back to WARP software driver
                hr = D3D11CreateDevice(
                    IntPtr.Zero, D3D_DRIVER_TYPE_WARP, IntPtr.Zero,
                    D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                    IntPtr.Zero, 0, D3D11_SDK_VERSION,
                    out d3dDevicePtr, out _, out contextPtr);
                Marshal.ThrowExceptionForHR(hr);
            }

            try
            {
                var dxgiGuid = IDXGIDeviceGuid;
                Marshal.ThrowExceptionForHR(
                    Marshal.QueryInterface(d3dDevicePtr, ref dxgiGuid, out IntPtr dxgiPtr));
                try
                {
                    hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiPtr, out IntPtr inspectable);
                    Marshal.ThrowExceptionForHR(hr);
                    try
                    {
                        return (IDirect3DDevice)Marshal.GetObjectForIUnknown(inspectable);
                    }
                    finally
                    {
                        Marshal.Release(inspectable);
                    }
                }
                finally
                {
                    Marshal.Release(dxgiPtr);
                }
            }
            finally
            {
                Marshal.Release(contextPtr);
                Marshal.Release(d3dDevicePtr);
            }
        }

        #endregion

        #region Capture Item

        private static GraphicsCaptureItem CreateCaptureItemForWindow(IntPtr hwnd)
        {
            var interopGuid = IGraphicsCaptureItemInteropGuid;
            RoGetActivationFactory(
                "Windows.Graphics.Capture.GraphicsCaptureItem",
                ref interopGuid,
                out IntPtr factoryPtr);

            try
            {
                var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
                var itemGuid = IGraphicsCaptureItemGuid;
                interop.CreateForWindow(hwnd, ref itemGuid, out IntPtr itemPtr);
                try
                {
                    return (GraphicsCaptureItem)Marshal.GetObjectForIUnknown(itemPtr);
                }
                finally
                {
                    Marshal.Release(itemPtr);
                }
            }
            finally
            {
                Marshal.Release(factoryPtr);
            }
        }

        #endregion

        #region Session Configuration

        private static void TryConfigureSession(GraphicsCaptureSession session)
        {
            // IsBorderRequired: Windows 11 / Windows 10 21H1+
            try { session.IsBorderRequired = false; }
            catch { /* Not available on older Windows */ }

            // IsCursorCaptureEnabled: Windows 10 1903+
            try { session.IsCursorCaptureEnabled = false; }
            catch { /* Not available */ }
        }

        #endregion

        #region Frame → Bitmap Conversion

        private static async Task<Bitmap> FrameToBitmapAsync(Direct3D11CaptureFrame frame)
        {
            var surface = frame.Surface;

            var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(
                surface, BitmapAlphaMode.Premultiplied);

            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
            {
                var converted = SoftwareBitmap.Convert(
                    softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                softwareBitmap.Dispose();
                softwareBitmap = converted;
            }

            try
            {
                return SoftwareBitmapToDrawingBitmap(softwareBitmap);
            }
            finally
            {
                softwareBitmap.Dispose();
            }
        }

        private static Bitmap SoftwareBitmapToDrawingBitmap(SoftwareBitmap softwareBitmap)
        {
            int w = softwareBitmap.PixelWidth;
            int h = softwareBitmap.PixelHeight;

            using (var buffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Read))
            using (var reference = buffer.CreateReference())
            {
                var byteAccess = (IMemoryBufferByteAccess)(object)reference;
                byteAccess.GetBuffer(out IntPtr dataPtr, out uint capacity);

                var plane = buffer.GetPlaneDescription(0);

                var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                var bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, w, h),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);

                try
                {
                    int bytesPerRow = w * 4;
                    for (int y = 0; y < h; y++)
                    {
                        var srcRow = IntPtr.Add(dataPtr, plane.StartIndex + y * plane.Stride);
                        var dstRow = IntPtr.Add(bmpData.Scan0, y * bmpData.Stride);
                        CopyMemory(dstRow, srcRow, bytesPerRow);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bmpData);
                }

                return bitmap;
            }
        }

        #endregion
    }
}
