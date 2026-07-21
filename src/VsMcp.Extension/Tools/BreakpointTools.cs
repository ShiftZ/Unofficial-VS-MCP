using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json.Linq;
using VsMcp.Extension.McpServer;
using VsMcp.Extension.Services;
using VsMcp.Shared;
using VsMcp.Shared.Protocol;

namespace VsMcp.Extension.Tools
{
    public static class BreakpointTools
    {
        private static readonly TimeSpan BreakpointBindingTimeout = TimeSpan.FromSeconds(5);

        public static void Register(McpToolRegistry registry, VsServiceAccessor accessor)
        {
            registry.Register(
                new McpToolDefinition(
                    "breakpoint_set",
                    "Set a breakpoint. Use file+line for location breakpoints, or functionName for function breakpoints. When possible, provide the function name with its signature (for example, 'Foo::Init(const String&)'). Optionally add condition or hitCount. Conditions must be simple, side-effect-free Boolean expressions over in-scope values (for example, 'count == 10'); do not invoke methods or functions. More about condition expressions: https://learn.microsoft.com/en-us/visualstudio/debugger/expressions-in-the-debugger?view=visualstudio. List of supported intrinsics: https://learn.microsoft.com/en-us/visualstudio/debugger/expressions-in-the-debugger?view=visualstudio#BKMK_Using_debugger_intrinisic_functions_to_maintain_state. Note: setting a breakpoint at design time makes sense only inside the body of a non-lambda function. There is a high chance that the breakpoint will fail to bind at runtime. Lambda functions tend to fail binding even when the location is inside their body. A more reliable workflow is to start the session with debug_start_in_break, set the desired breakpoints, and then call debug_continue_wait_break.",
                    SchemaBuilder.Create()
                        .AddString("file", "Full path to the source file (required for location breakpoints)")
                        .AddInteger("line", "Line number to set the breakpoint (required for location breakpoints)")
                        .AddString("functionName", "Fully qualified function name for function breakpoints. When possible, include its signature (e.g. 'Foo::Init(const String&)')")
                        .AddString("condition", "Optional simple, side-effect-free Boolean condition over in-scope values; do not invoke methods or functions")
                        .AddInteger("hitCount", "Optional hit count target value")
                        .AddEnum("hitCountType", "When to break relative to the hit count",
                            new[] { "equal", "greaterOrEqual", "multiple" })
                        .Build()),
                args => BreakpointSetUnifiedAsync(accessor, args));

            registry.Register(
                new McpToolDefinition(
                    "breakpoint_remove",
                    "Remove a breakpoint by its id, or at a specific file and line",
                    SchemaBuilder.Create()
                        .AddString("id", "Breakpoint id returned by breakpoint_set")
                        .AddString("file", "Full path to the source file (required when id is omitted)")
                        .AddInteger("line", "Line number of the breakpoint (required when id is omitted)")
                        .Build()),
                args => BreakpointRemoveAsync(accessor, args));

            registry.Register(
                new McpToolDefinition(
                    "breakpoint_list",
                    "List all breakpoints in the current solution",
                    SchemaBuilder.Empty()),
                args => BreakpointListAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "breakpoint_enable",
                    "Enable or disable a breakpoint by its id, or at a specific file and line",
                    SchemaBuilder.Create()
                        .AddString("id", "Breakpoint id returned by breakpoint_set")
                        .AddString("file", "Full path to the source file (required when id is omitted)")
                        .AddInteger("line", "Line number of the breakpoint (required when id is omitted)")
                        .AddBoolean("enabled", "true to enable, false to disable", required: true)
                        .Build()),
                args => BreakpointEnableAsync(accessor, args));
        }

        private static async Task<McpToolResult> BreakpointSetUnifiedAsync(VsServiceAccessor accessor, JObject args)
        {
            var functionName = args.Value<string>("functionName");
            var file = args.Value<string>("file");
            var line = args.Value<int?>("line");
            var condition = args.Value<string>("condition");
            var hitCount = args.Value<int?>("hitCount");
            var hitCountTypeStr = args.Value<string>("hitCountType") ?? "equal";

            // Validate: either functionName or file+line must be provided
            if (!string.IsNullOrEmpty(functionName))
            {
                var breakpointId = CreateBreakpointId(functionName);
                var functionSetResult = await CreateAndWaitForBreakpointBindingAsync(
                    accessor, breakpointId, null, null, functionName, condition,
                    dte => string.IsNullOrEmpty(condition)
                        ? dte.Debugger.Breakpoints.Add(Function: functionName)
                        : dte.Debugger.Breakpoints.Add(Function: functionName,
                            Condition: condition,
                            ConditionType: dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue));
                if (functionSetResult.Error != null)
                {
                    var kind = string.IsNullOrEmpty(condition) ? "function" : "conditional function";
                    return McpToolResult.Error(
                        $"Failed to set {kind} breakpoint on '{functionName}': {functionSetResult.Error}");
                }

                return McpToolResult.Success(new
                {
                    message = $"Function breakpoint set on '{functionName}'",
                    functionName,
                    condition = condition ?? "",
                    id = functionSetResult.BreakpointId
                });
            }

            // Location breakpoint
            if (string.IsNullOrEmpty(file))
                return McpToolResult.Error("Either 'functionName' or 'file'+'line' is required");
            if (!line.HasValue || line.Value <= 0)
                return McpToolResult.Error("Parameter 'line' is required and must be positive");

            var locationBreakpointId = CreateBreakpointId(Path.GetFileName(file) + ":" + line.Value);
            if (hitCount.HasValue && hitCount.Value > 0)
            {
                dbgHitCountType hitCountType;
                switch (hitCountTypeStr)
                {
                    case "greaterOrEqual":
                        hitCountType = dbgHitCountType.dbgHitCountTypeGreaterOrEqual;
                        break;
                    case "multiple":
                        hitCountType = dbgHitCountType.dbgHitCountTypeMultiple;
                        break;
                    default:
                        hitCountType = dbgHitCountType.dbgHitCountTypeEqual;
                        break;
                }

                var hitCountSetResult = await CreateAndWaitForBreakpointBindingAsync(
                    accessor, locationBreakpointId, file, line, null, null,
                    dte => dte.Debugger.Breakpoints.Add("", file, line.Value,
                        HitCount: hitCount.Value,
                        HitCountType: hitCountType));
                if (hitCountSetResult.Error != null)
                {
                    return McpToolResult.Error(
                        $"Failed to set breakpoint with hit count at {file}:{line.Value}: {hitCountSetResult.Error}");
                }

                return McpToolResult.Success(new
                {
                    message = $"Breakpoint with hit count set at {file}:{line.Value}",
                    hitCount = hitCount.Value,
                    hitCountType = hitCountTypeStr,
                    id = hitCountSetResult.BreakpointId
                });
            }

            if (!string.IsNullOrEmpty(condition))
            {
                var conditionalSetResult = await CreateAndWaitForBreakpointBindingAsync(
                    accessor, locationBreakpointId, file, line, null, condition,
                    dte => dte.Debugger.Breakpoints.Add("", file, line.Value,
                        Condition: condition,
                        ConditionType: dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue));
                if (conditionalSetResult.Error != null)
                {
                    return McpToolResult.Error(
                        $"Failed to set conditional breakpoint at {file}:{line.Value}: {conditionalSetResult.Error}");
                }

                return McpToolResult.Success(new
                {
                    message = $"Conditional breakpoint set at {file}:{line.Value} (condition: {condition})",
                    id = conditionalSetResult.BreakpointId
                });
            }

            var simpleSetResult = await CreateAndWaitForBreakpointBindingAsync(
                accessor, locationBreakpointId, file, line, null, null,
                dte => dte.Debugger.Breakpoints.Add("", file, line.Value));
            if (simpleSetResult.Error != null)
            {
                return McpToolResult.Error(
                    $"Failed to set breakpoint at {file}:{line.Value}: {simpleSetResult.Error}");
            }

            return McpToolResult.Success(new
            {
                message = $"Breakpoint set at {file}:{line.Value}",
                id = simpleSetResult.BreakpointId
            });
        }

        private static T RunWithSuppressedUi<T>(DTE2 dte, Func<T> action)
        {
            var previousSuppressUi = dte.SuppressUI;
            try
            {
                dte.SuppressUI = true;
                return action();
            }
            finally
            {
                dte.SuppressUI = previousSuppressUi;
            }
        }

        private static string CreateBreakpointId(string selector)
        {
            const long millisecondsPerDay = 24L * 60 * 60 * 1000;
            var millisecondsSinceEpoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return selector + "#" + (millisecondsSinceEpoch % millisecondsPerDay);
        }

        private static string TagCreatedBreakpoints(Breakpoints created, string breakpointId)
        {
            if (created == null) return "Visual Studio returned no breakpoint collection to tag with its id";

            int count;
            try
            {
                count = created.Count;
            }
            catch (Exception ex)
            {
                return $"Could not inspect the created breakpoint collection for id tagging: {ex.Message}";
            }

            if (count <= 0) return "Visual Studio returned no created breakpoint to tag with its id";

            for (var index = 1; index <= count; index++)
            {
                try
                {
                    var breakpoint = created.Item(index);
                    breakpoint.Tag = breakpointId;
                    if (!string.Equals(breakpoint.Tag, breakpointId, StringComparison.Ordinal))
                        return $"Visual Studio did not retain the id on created breakpoint {index}";
                }
                catch (Exception ex)
                {
                    return $"Could not tag created breakpoint {index} with its id: {ex.Message}";
                }
            }

            return null;
        }

        private static async Task<BreakpointSetResult> CreateAndWaitForBreakpointBindingAsync(VsServiceAccessor accessor,
            string breakpointId, string file, int? line, string functionName, string expectedCondition,
            Func<DTE2, Breakpoints> createBreakpoint)
        {
            var debugger = await accessor.GetVsDebuggerAsync();
            if (debugger == null) return BreakpointSetResult.Failure("Visual Studio debugger service is unavailable");

            var registration = new BreakpointBindingRegistration(debugger, file, line, functionName);
            try
            {
                var subscribeError = await accessor.RunOnUIThreadAsync(registration.Subscribe);
                if (subscribeError != null) return BreakpointSetResult.Failure(subscribeError);

                var creation = await accessor.RunOnUIThreadAsync(() =>
                {
                    var dte = ThreadHelper.JoinableTaskFactory.Run(() => accessor.GetDteAsync());
                    return RunWithSuppressedUi(dte, () =>
                    {
                        try
                        {
                            var created = createBreakpoint(dte);
                            if (!TryGetBreakpointCollectionCount(created, out var createdCount, out var collectionError))
                                return new BreakpointCreationResult(created, collectionError);

                            if (createdCount == 0)
                            {
                                var existing = FindExistingBreakpoint(dte, file, line, functionName);
                                if (existing.Error != null)
                                    return new BreakpointCreationResult(created, existing.Error);

                                if (existing.Found)
                                {
                                    var existingTag = existing.Tag;
                                    if (string.IsNullOrEmpty(existingTag))
                                    {
                                        existingTag = breakpointId;
                                        try
                                        {
                                            existing.Breakpoint.Tag = existingTag;
                                        }
                                        catch (Exception ex)
                                        {
                                            return new BreakpointCreationResult(created,
                                                $"Could not tag the existing breakpoint with its id: {ex.Message}");
                                        }
                                    }

                                    return new BreakpointCreationResult(null, null, existingTag, true);
                                }

                                return new BreakpointCreationResult(created,
                                    "Visual Studio returned no created breakpoint to tag with its id");
                            }

                            var taggingError = TagCreatedBreakpoints(created, breakpointId);
                            if (taggingError == null) registration.Arm();
                            return new BreakpointCreationResult(created, taggingError);
                        }
                        catch (Exception ex)
                        {
                            return new BreakpointCreationResult(null,
                                $"Visual Studio rejected the breakpoint: {ex.Message}");
                        }
                    });
                });
                if (creation.Error != null) return BreakpointSetResult.Failure(creation.Error);
                if (creation.ExistingBreakpointFound)
                    return BreakpointSetResult.Success(creation.ExistingBreakpointTag);

                var basicValidationError = await accessor.RunOnUIThreadAsync(
                    () => ValidateCreatedBreakpoints(creation.Breakpoints, expectedCondition,
                        requireRuntimeBounds: false));
                if (basicValidationError != null)
                {
                    var error = await CleanupFailedBreakpointAsync(accessor, breakpointId, basicValidationError);
                    return BreakpointSetResult.Failure(error);
                }

                var hasRuntimeBounds = await accessor.RunOnUIThreadAsync(
                    () => HasRuntimeBounds(creation.Breakpoints));
                if (hasRuntimeBounds) return BreakpointSetResult.Success(breakpointId);

                var bindingSignal = await registration.WaitAsync(BreakpointBindingTimeout);
                if (bindingSignal == null)
                {
                    var debuggerMode = await GetDebuggerModeAsync(accessor);
                    if (debuggerMode == dbgDebugMode.dbgDesignMode)
                        return BreakpointSetResult.Success(breakpointId);

                    var timeoutError = $"Timed out after {BreakpointBindingTimeout.TotalSeconds:0} seconds " +
                        $"waiting for a breakpoint bound or error callback. Current debugger mode: " +
                        GetDebuggerModeName(debuggerMode) + ". The breakpoint failed to bind to executable code.";
                    var error = await CleanupFailedBreakpointAsync(accessor, breakpointId, timeoutError);
                    return BreakpointSetResult.Failure(error);
                }

                if (bindingSignal.IsBound) return BreakpointSetResult.Success(breakpointId);

                var bindingError = await CleanupFailedBreakpointAsync(accessor, breakpointId, bindingSignal.Message);
                return BreakpointSetResult.Failure(bindingError);
            }
            finally
            {
                await accessor.RunOnUIThreadAsync(registration.Unsubscribe);
            }
        }

        private static bool TryGetBreakpointCollectionCount(Breakpoints breakpoints, out int count, out string error)
        {
            count = 0;
            error = null;
            if (breakpoints == null)
            {
                error = "Visual Studio returned no breakpoint collection to tag with its id";
                return false;
            }

            try
            {
                count = breakpoints.Count;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Could not inspect the created breakpoint collection for id tagging: {ex.Message}";
                return false;
            }
        }

        private static ExistingBreakpointMatch FindExistingBreakpoint(DTE2 dte, string file, int? line,
            string functionName)
        {
            try
            {
                foreach (Breakpoint2 breakpoint in dte.Debugger.Breakpoints)
                {
                    try
                    {
                        var matchesFunction = !string.IsNullOrEmpty(functionName) &&
                            string.Equals(breakpoint.FunctionName, functionName, StringComparison.Ordinal);
                        var matchesLocation = string.IsNullOrEmpty(functionName) && line.HasValue &&
                            string.Equals(breakpoint.File, file, StringComparison.OrdinalIgnoreCase) &&
                            breakpoint.FileLine == line.Value;
                        if (!matchesFunction && !matchesLocation) continue;

                        return ExistingBreakpointMatch.CreateFound(breakpoint);
                    }
                    catch (Exception ex)
                    {
                        return ExistingBreakpointMatch.CreateError(
                            $"Could not inspect an existing breakpoint while checking for a duplicate: {ex.Message}");
                    }
                }

                return ExistingBreakpointMatch.CreateNotFound();
            }
            catch (Exception ex)
            {
                return ExistingBreakpointMatch.CreateError(
                    $"Could not list existing breakpoints while checking for a duplicate: {ex.Message}");
            }
        }

        private static async Task<dbgDebugMode> GetDebuggerModeAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = ThreadHelper.JoinableTaskFactory.Run(() => accessor.GetDteAsync());
                return dte.Debugger.CurrentMode;
            });
        }

        private static async Task<string> CleanupFailedBreakpointAsync(VsServiceAccessor accessor,
            string breakpointId, string error)
        {
            var cleanupError = await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = ThreadHelper.JoinableTaskFactory.Run(() => accessor.GetDteAsync());
                if (dte.Debugger.CurrentMode == dbgDebugMode.dbgDesignMode) return null;

                return TryDeleteTaggedBreakpoint(dte, breakpointId);
            });
            if (cleanupError == null) return error;

            return error + " Cleanup failed: " + cleanupError;
        }

        private static bool HasRuntimeBounds(Breakpoints created)
        {
            if (created == null) return false;

            try
            {
                if (created.Count <= 0) return false;

                for (var index = 1; index <= created.Count; index++)
                {
                    var breakpoint = created.Item(index);
                    if (breakpoint.Children?.Count <= 0) return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetDebuggerModeName(dbgDebugMode mode)
        {
            switch (mode)
            {
                case dbgDebugMode.dbgRunMode:
                    return "Running";
                case dbgDebugMode.dbgBreakMode:
                    return "Break";
                case dbgDebugMode.dbgDesignMode:
                default:
                    return "Design";
            }
        }

        private static string ValidateCreatedBreakpoints(Breakpoints created, string expectedCondition,
            bool requireRuntimeBounds)
        {
            if (created == null) return "Visual Studio returned no breakpoint collection";

            int count;
            try
            {
                count = created.Count;
            }
            catch (Exception ex)
            {
                return $"Could not inspect the created breakpoints: {ex.Message}";
            }

            if (count <= 0) return "Visual Studio did not create a breakpoint";

            for (var index = 1; index <= count; index++)
            {
                Breakpoint breakpoint;
                try
                {
                    breakpoint = created.Item(index);
                }
                catch (Exception ex)
                {
                    return $"Could not inspect created breakpoint {index}: {ex.Message}";
                }

                string actualCondition;
                try
                {
                    actualCondition = breakpoint.Condition;
                }
                catch (Exception ex)
                {
                    return $"Could not inspect created breakpoint {index}: {ex.Message}";
                }

                if (requireRuntimeBounds)
                {
                    Breakpoints children;
                    int childCount;
                    try
                    {
                        children = breakpoint.Children;
                        childCount = children?.Count ?? 0;
                    }
                    catch (Exception ex)
                    {
                        return $"Could not inspect bound DTE children for created breakpoint {index}: {ex.Message}";
                    }

                    if (childCount <= 0)
                    {
                        return $"The debugger created pending breakpoint {index}, but it has no bound DTE children. " +
                            "It did not resolve to executable code in the active debug session.";
                    }
                }

                if (expectedCondition != null && !string.Equals(actualCondition, expectedCondition, StringComparison.Ordinal))
                {
                    var actual = string.IsNullOrEmpty(actualCondition) ? "(empty)" : $"'{actualCondition}'";
                    return $"Visual Studio rejected the condition. Stored condition: {actual}";
                }
            }

            return null;
        }

        private sealed class BreakpointCreationResult
        {
            public BreakpointCreationResult(Breakpoints breakpoints, string error, string existingBreakpointTag = null,
                bool existingBreakpointFound = false)
            {
                Breakpoints = breakpoints;
                Error = error;
                ExistingBreakpointTag = existingBreakpointTag;
                ExistingBreakpointFound = existingBreakpointFound;
            }

            public Breakpoints Breakpoints { get; }
            public string Error { get; }
            public string ExistingBreakpointTag { get; }
            public bool ExistingBreakpointFound { get; }
        }

        private sealed class BreakpointSetResult
        {
            private BreakpointSetResult(string breakpointId, string error)
            {
                BreakpointId = breakpointId;
                Error = error;
            }

            public string BreakpointId { get; }
            public string Error { get; }

            public static BreakpointSetResult Success(string breakpointId)
            {
                return new BreakpointSetResult(breakpointId, null);
            }

            public static BreakpointSetResult Failure(string error)
            {
                return new BreakpointSetResult(null, error);
            }
        }

        private sealed class ExistingBreakpointMatch
        {
            private ExistingBreakpointMatch(bool found, Breakpoint2 breakpoint, string tag, string error)
            {
                Found = found;
                Breakpoint = breakpoint;
                Tag = tag;
                Error = error;
            }

            public bool Found { get; }
            public Breakpoint2 Breakpoint { get; }
            public string Tag { get; }
            public string Error { get; }

            public static ExistingBreakpointMatch CreateFound(Breakpoint2 breakpoint)
            {
                return new ExistingBreakpointMatch(true, breakpoint, breakpoint.Tag, null);
            }

            public static ExistingBreakpointMatch CreateNotFound()
            {
                return new ExistingBreakpointMatch(false, null, null, null);
            }

            public static ExistingBreakpointMatch CreateError(string error)
            {
                return new ExistingBreakpointMatch(false, null, null, error);
            }
        }

        private sealed class BreakpointBindingSignal
        {
            private BreakpointBindingSignal(bool isBound, string message)
            {
                IsBound = isBound;
                Message = message;
            }

            public bool IsBound { get; }
            public string Message { get; }

            public static BreakpointBindingSignal Bound()
            {
                return new BreakpointBindingSignal(true, null);
            }

            public static BreakpointBindingSignal Error(string message)
            {
                return new BreakpointBindingSignal(false,
                    string.IsNullOrWhiteSpace(message)
                        ? "The debugger reported a breakpoint binding error."
                        : message);
            }
        }

        [ComVisible(true)]
        private sealed class BreakpointBindingRegistration : IDebugEventCallback2, IVsDebuggerEvents
        {
            private readonly IVsDebugger _debugger;
            private readonly string _file;
            private readonly int? _line;
            private readonly string _functionName;
            private readonly TaskCompletionSource<BreakpointBindingSignal> _completion =
                new TaskCompletionSource<BreakpointBindingSignal>(TaskCreationOptions.RunContinuationsAsynchronously);
            private bool _armed;
            private bool _subscribed;

            public BreakpointBindingRegistration(IVsDebugger debugger, string file, int? line, string functionName)
            {
                _debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));
                _file = file;
                _line = line;
                _functionName = functionName;
            }

            public void Arm()
            {
                _armed = true;
            }

            public string Subscribe()
            {
                if (_subscribed) return null;

                try
                {
                    var hresult = _debugger.AdviseDebugEventCallback(this);
                    if (hresult < 0)
                    {
                        return $"Could not subscribe to Visual Studio debugger callbacks (0x{hresult:X8}).";
                    }

                    _subscribed = true;
                    return null;
                }
                catch (Exception ex)
                {
                    return $"Could not subscribe to Visual Studio debugger callbacks: {ex.Message}";
                }
            }

            public void Unsubscribe()
            {
                if (!_subscribed) return;

                try
                {
                    _debugger.UnadviseDebugEventCallback(this);
                }
                catch
                {
                }
                finally
                {
                    _subscribed = false;
                }
            }

            public async Task<BreakpointBindingSignal> WaitAsync(TimeSpan timeout)
            {
                var timeoutTask = Task.Delay(timeout);
#pragma warning disable VSTHRD003 // Event-backed TaskCompletionSource; no UI-thread work is awaited here.
                var completedTask = await Task.WhenAny(_completion.Task, timeoutTask);
                if (completedTask != _completion.Task) return null;

                return await _completion.Task;
#pragma warning restore VSTHRD003
            }

            public int OnModeChange(DBGMODE dbgmode)
            {
                return VSConstants.S_OK;
            }

            public int Event(IDebugEngine2 engine, IDebugProcess2 process, IDebugProgram2 program,
                IDebugThread2 thread, IDebugEvent2 debugEvent, ref Guid eventInterfaceId, uint attributes)
            {
                try
                {
                    if (!_armed) return VSConstants.S_OK;

                    if (eventInterfaceId == typeof(IDebugBreakpointBoundEvent2).GUID)
                    {
                        var boundEvent = debugEvent as IDebugBreakpointBoundEvent2;
                        if (boundEvent != null && Matches(boundEvent))
                            _completion.TrySetResult(BreakpointBindingSignal.Bound());
                    }
                    else if (eventInterfaceId == typeof(IDebugBreakpointErrorEvent2).GUID)
                    {
                        var errorEvent = debugEvent as IDebugBreakpointErrorEvent2;
                        if (errorEvent != null && Matches(errorEvent, out var errorMessage))
                            _completion.TrySetResult(BreakpointBindingSignal.Error(errorMessage));
                    }
                }
                catch (Exception ex)
                {
                    _completion.TrySetResult(BreakpointBindingSignal.Error(
                        $"Could not inspect the debugger breakpoint callback: {ex.Message}"));
                }
                finally
                {
                    ReleaseComObject(debugEvent);
                    ReleaseComObject(thread);
                    ReleaseComObject(program);
                    ReleaseComObject(process);
                    ReleaseComObject(engine);
                }

                return VSConstants.S_OK;
            }

            private bool Matches(IDebugBreakpointBoundEvent2 boundEvent)
            {
                IDebugPendingBreakpoint2 pendingBreakpoint = null;
                try
                {
                    if (boundEvent.GetPendingBreakpoint(out pendingBreakpoint) < 0) return false;

                    return Matches(pendingBreakpoint);
                }
                finally
                {
                    ReleaseComObject(pendingBreakpoint);
                }
            }

            private bool Matches(IDebugBreakpointErrorEvent2 errorEvent, out string errorMessage)
            {
                errorMessage = null;
                IDebugErrorBreakpoint2 errorBreakpoint = null;
                IDebugPendingBreakpoint2 pendingBreakpoint = null;
                try
                {
                    if (errorEvent.GetErrorBreakpoint(out errorBreakpoint) < 0) return false;
                    if (errorBreakpoint.GetPendingBreakpoint(out pendingBreakpoint) < 0) return false;
                    if (!Matches(pendingBreakpoint)) return false;

                    errorMessage = GetErrorMessage(errorBreakpoint);
                    return true;
                }
                finally
                {
                    ReleaseComObject(pendingBreakpoint);
                    ReleaseComObject(errorBreakpoint);
                }
            }

            private bool Matches(IDebugPendingBreakpoint2 pendingBreakpoint)
            {
                if (pendingBreakpoint == null) return false;

                IDebugBreakpointRequest2 request = null;
                try
                {
                    if (pendingBreakpoint.GetBreakpointRequest(out request) < 0 || request == null) return false;

                    var requestInfo = new BP_REQUEST_INFO[1];
                    if (request.GetRequestInfo(enum_BPREQI_FIELDS.BPREQI_BPLOCATION, requestInfo) < 0)
                        return false;

                    var location = requestInfo[0].bpLocation;
                    var locationType = (enum_BP_LOCATION_TYPE)location.bpLocationType;
                    if (!string.IsNullOrEmpty(_file))
                    {
                        return locationType == enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE &&
                            MatchesFileLineLocation(location);
                    }

                    return locationType == enum_BP_LOCATION_TYPE.BPLT_CODE_FUNC_OFFSET &&
                        MatchesFunctionLocation(location);
                }
                finally
                {
                    ReleaseComObject(request);
                }
            }

            private bool MatchesFileLineLocation(BP_LOCATION location)
            {
                if (location.unionmember1 == IntPtr.Zero || !_line.HasValue) return false;

                var fileLine = Marshal.PtrToStructure<BP_LOCATION_CODE_FILE_LINE>(location.unionmember1);
                if (fileLine.pDocPos == null) return false;

                if (fileLine.pDocPos.GetFileName(out var callbackFile) < 0 ||
                    !string.Equals(callbackFile, _file, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var start = new TEXT_POSITION[1];
                var end = new TEXT_POSITION[1];
                if (fileLine.pDocPos.GetRange(start, end) < 0) return false;

                return start[0].dwLine + 1 == _line.Value;
            }

            private bool MatchesFunctionLocation(BP_LOCATION location)
            {
                if (location.unionmember1 == IntPtr.Zero || string.IsNullOrEmpty(_functionName)) return false;

                var functionOffset = Marshal.PtrToStructure<BP_LOCATION_CODE_FUNC_OFFSET>(location.unionmember1);
                if (functionOffset.pFuncPos == null) return false;
                if (functionOffset.pFuncPos.GetFunctionName(out var callbackFunction) < 0) return false;

                return string.Equals(callbackFunction, _functionName, StringComparison.Ordinal);
            }

            private static string GetErrorMessage(IDebugErrorBreakpoint2 errorBreakpoint)
            {
                IDebugErrorBreakpointResolution2 errorResolution = null;
                try
                {
                    if (errorBreakpoint.GetBreakpointResolution(out errorResolution) < 0 || errorResolution == null)
                        return null;

                    var resolutionInfo = new BP_ERROR_RESOLUTION_INFO[1];
                    if (errorResolution.GetResolutionInfo(enum_BPERESI_FIELDS.BPERESI_MESSAGE, resolutionInfo) < 0)
                        return null;

                    return resolutionInfo[0].bstrMessage;
                }
                finally
                {
                    ReleaseComObject(errorResolution);
                }
            }

            private static void ReleaseComObject(object value)
            {
                if (value == null || !Marshal.IsComObject(value)) return;

                try
                {
                    Marshal.ReleaseComObject(value);
                }
                catch
                {
                }
            }
        }

        private static string TryDeleteTaggedBreakpoint(DTE2 dte, string breakpointId)
        {
            Breakpoint2 taggedBreakpoint = null;
            try
            {
                foreach (Breakpoint2 breakpoint in dte.Debugger.Breakpoints)
                {
                    if (!string.Equals(breakpoint.Tag, breakpointId, StringComparison.Ordinal)) continue;

                    taggedBreakpoint = breakpoint;
                    break;
                }
            }
            catch (Exception ex)
            {
                return $"Could not reacquire the breakpoint by id from the global DTE collection: {ex.Message}";
            }

            if (taggedBreakpoint == null)
            {
                return $"No breakpoint with id '{breakpointId}' was present in the global DTE breakpoint collection";
            }

            // Do not call Delete on an item from Breakpoints.Add(). For an unbound native
            // function breakpoint, Visual Studio can access-violate in that automation object.
            // Reacquiring by its id gives us the persistent DTE breakpoint instead.
            try
            {
                taggedBreakpoint.Delete();
                return null;
            }
            catch (Exception ex)
            {
                return $"Could not delete the reacquired tagged breakpoint: {ex.Message}";
            }
        }

        private static async Task<McpToolResult> BreakpointRemoveAsync(VsServiceAccessor accessor, JObject args)
        {
            var breakpointId = args.Value<string>("id");
            if (!string.IsNullOrEmpty(breakpointId))
            {
                return await accessor.RunOnUIThreadAsync(() =>
                {
                    var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                        .Run(() => accessor.GetDteAsync());
                    var deletionError = TryDeleteTaggedBreakpoint(dte, breakpointId);
                    if (deletionError != null) return McpToolResult.Error(deletionError);

                    return McpToolResult.Success($"Breakpoint removed: {breakpointId}");
                });
            }

            var file = args.Value<string>("file");
            if (string.IsNullOrEmpty(file))
                return McpToolResult.Error("Either 'id' or 'file'+'line' is required");

            var line = args.Value<int?>("line");
            if (!line.HasValue || line.Value <= 0)
                return McpToolResult.Error("Parameter 'line' is required and must be positive");

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                var removed = false;
                foreach (Breakpoint2 bp in dte.Debugger.Breakpoints)
                {
                    try
                    {
                        if (string.Equals(bp.File, file, StringComparison.OrdinalIgnoreCase) &&
                            bp.FileLine == line.Value)
                        {
                            bp.Delete();
                            removed = true;
                        }
                    }
                    catch { }
                }

                if (!removed)
                    return McpToolResult.Error($"No breakpoint found at {file}:{line.Value}");

                return McpToolResult.Success($"Breakpoint removed at {file}:{line.Value}");
            });
        }

        private static async Task<McpToolResult> BreakpointListAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());
                var breakpoints = CreateDteBreakpointSummaries(dte);

                return McpToolResult.Success(new
                {
                    source = "dte",
                    note = "DTE provides logical breakpoint settings and runtimeBounds from each pending breakpoint's bound children.",
                    count = breakpoints.Count,
                    breakpoints
                });
            });
        }

        private static List<JObject> CreateDteBreakpointSummaries(DTE2 dte)
        {
            var breakpoints = new List<JObject>();
            foreach (Breakpoint2 bp in dte.Debugger.Breakpoints)
            {
                try
                {
                    breakpoints.Add(CreateDteBreakpointSummary(bp, includeRuntimeBounds: true));
                }
                catch { }
            }

            return breakpoints;
        }

        private static JObject CreateDteBreakpointSummary(Breakpoint breakpoint, bool includeRuntimeBounds)
        {
            var summary = new JObject
            {
                ["file"] = breakpoint.File,
                ["line"] = breakpoint.FileLine,
                ["column"] = breakpoint.FileColumn,
                ["enabled"] = breakpoint.Enabled,
                ["condition"] = TryGetCondition(breakpoint),
                ["hitCount"] = TryGetHitCount(breakpoint),
                ["functionName"] = breakpoint.FunctionName,
                ["id"] = TryGetBreakpointId(breakpoint),
                ["type"] = breakpoint.LocationType.ToString()
            };

            if (includeRuntimeBounds)
                summary["runtimeBounds"] = CreateDteRuntimeBoundSummaries(breakpoint);

            return summary;
        }

        private static JArray CreateDteRuntimeBoundSummaries(Breakpoint breakpoint)
        {
            var runtimeBounds = new JArray();
            Breakpoints children;
            try { children = breakpoint.Children; }
            catch { return runtimeBounds; }

            if (children == null) return runtimeBounds;

            try
            {
                foreach (Breakpoint child in children)
                    runtimeBounds.Add(CreateDteBreakpointSummary(child, includeRuntimeBounds: false));
            }
            catch { }

            return runtimeBounds;
        }

        private static string TryGetCondition(Breakpoint breakpoint)
        {
            try { return breakpoint.Condition; } catch { return ""; }
        }

        private static string TryGetBreakpointId(Breakpoint breakpoint)
        {
            try { return breakpoint.Tag; } catch { return ""; }
        }

        private static int TryGetHitCount(Breakpoint breakpoint)
        {
            try { return breakpoint.CurrentHits; } catch { return 0; }
        }

        private static async Task<McpToolResult> BreakpointEnableAsync(VsServiceAccessor accessor, JObject args)
        {
            var enabled = args.Value<bool?>("enabled");
            if (!enabled.HasValue)
                return McpToolResult.Error("Parameter 'enabled' is required");

            var breakpointId = args.Value<string>("id");
            if (!string.IsNullOrEmpty(breakpointId))
            {
                return await accessor.RunOnUIThreadAsync(() =>
                {
                    var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                        .Run(() => accessor.GetDteAsync());
                    Breakpoint2 taggedBreakpoint = null;
                    try
                    {
                        foreach (Breakpoint2 breakpoint in dte.Debugger.Breakpoints)
                        {
                            if (!string.Equals(breakpoint.Tag, breakpointId, StringComparison.Ordinal)) continue;

                            taggedBreakpoint = breakpoint;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        return McpToolResult.Error(
                            $"Could not reacquire breakpoint '{breakpointId}' from the global DTE collection: {ex.Message}");
                    }

                    if (taggedBreakpoint == null)
                        return McpToolResult.Error($"No breakpoint found with id '{breakpointId}'");

                    try
                    {
                        taggedBreakpoint.Enabled = enabled.Value;
                    }
                    catch (Exception ex)
                    {
                        return McpToolResult.Error(
                            $"Could not {(enabled.Value ? "enable" : "disable")} breakpoint '{breakpointId}': {ex.Message}");
                    }

                    return McpToolResult.Success(
                        $"Breakpoint {breakpointId} {(enabled.Value ? "enabled" : "disabled")}");
                });
            }

            var file = args.Value<string>("file");
            if (string.IsNullOrEmpty(file))
                return McpToolResult.Error("Either 'id' or 'file'+'line' is required");

            var line = args.Value<int?>("line");
            if (!line.HasValue || line.Value <= 0)
                return McpToolResult.Error("Parameter 'line' is required and must be positive");

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                var found = false;
                foreach (Breakpoint2 bp in dte.Debugger.Breakpoints)
                {
                    try
                    {
                        if (string.Equals(bp.File, file, StringComparison.OrdinalIgnoreCase) &&
                            bp.FileLine == line.Value)
                        {
                            bp.Enabled = enabled.Value;
                            found = true;
                        }
                    }
                    catch { }
                }

                if (!found)
                    return McpToolResult.Error($"No breakpoint found at {file}:{line.Value}");

                return McpToolResult.Success($"Breakpoint at {file}:{line.Value} {(enabled.Value ? "enabled" : "disabled")}");
            });
        }

    }
}
