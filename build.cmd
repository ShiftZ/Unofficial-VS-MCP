@echo off
setlocal

set MSBUILD="C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
set VSIX_SRC=%~dp0src\VsMcp.Extension\VsMcp.Extension.vsix
set VSIX_TMP=%~dp0tmp_vsix
set VSIX_OUT=%VSIX_SRC%

echo === Building VSIX ===
echo Using MSBuild: %MSBUILD%
%MSBUILD% "%~dp0src\VsMcp.Extension\VsMcp.Extension.csproj" -p:Configuration=Release -restore -v:normal
if %ERRORLEVEL% neq 0 (
    echo Build failed with exit code %ERRORLEVEL%
    goto :eof
)

echo === Repackaging VSIX for VS 2026 compatibility ===
if exist "%VSIX_TMP%" rmdir /s /q "%VSIX_TMP%"
mkdir "%VSIX_TMP%"
powershell -Command "Expand-Archive -Path '%VSIX_SRC%' -DestinationPath '%VSIX_TMP%' -Force"
del "%VSIX_SRC%"
powershell -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::CreateFromDirectory('%VSIX_TMP%', '%VSIX_SRC%')"
rmdir /s /q "%VSIX_TMP%"

echo === Building VS 2019 VSIX ===
set VSIX_VS2019_SRC=%~dp0src\VsMcp.Extension.VS2019\VsMcp.Extension.VS2019.vsix
set VSIX_VS2019_TMP=%~dp0tmp_vsix_vs2019
set VSIX_VS2019_OUT=%VSIX_VS2019_SRC%

%MSBUILD% "%~dp0src\VsMcp.Extension.VS2019\VsMcp.Extension.VS2019.csproj" -p:Configuration=Release -restore -v:normal
if %ERRORLEVEL% neq 0 (
    echo VS 2019 build failed with exit code %ERRORLEVEL%
    goto :eof
)

echo === Repackaging VS 2019 VSIX ===
if exist "%VSIX_VS2019_TMP%" rmdir /s /q "%VSIX_VS2019_TMP%"
mkdir "%VSIX_VS2019_TMP%"
powershell -Command "Expand-Archive -Path '%VSIX_VS2019_SRC%' -DestinationPath '%VSIX_VS2019_TMP%' -Force"
del "%VSIX_VS2019_SRC%"
powershell -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::CreateFromDirectory('%VSIX_VS2019_TMP%', '%VSIX_VS2019_SRC%')"
rmdir /s /q "%VSIX_VS2019_TMP%"

echo === Done ===
echo VSIX (VS 2022+): %VSIX_OUT%
echo VSIX (VS 2019):  %VSIX_VS2019_OUT%
endlocal
