# Project Notes

## Visual Studio SDK Documentation Map

This MCP server is a classic in-process Visual Studio extension. For MCP tools that automate Visual Studio, start with the VSSDK, `EnvDTE`, and Roslyn workspace docs:

- [Choose the right Visual Studio extensibility model](https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/extensibility-models?view=visualstudio) - confirms when to use classic VSSDK versus the newer out-of-process `VisualStudio.Extensibility` model. This project uses VSSDK.
- [Visual Studio SDK reference](https://learn.microsoft.com/en-us/visualstudio/extensibility/visual-studio-sdk-reference?view=vs-2022) and [Inside the Visual Studio SDK](https://learn.microsoft.com/en-us/visualstudio/extensibility/internals/inside-the-visual-studio-sdk?view=visualstudio) - top-level maps for shell, editor, project, debugger, and service APIs.
- [Use AsyncPackage to load VSPackages in the background](https://learn.microsoft.com/en-us/visualstudio/extensibility/how-to-use-asyncpackage-to-load-vspackages-in-the-background?view=vs-2022) - package loading, `PackageRegistration`, `ProvideAutoLoad`, `AllowsBackgroundLoading`, and `GetServiceAsync`.
- [List of available Visual Studio services](https://learn.microsoft.com/en-us/visualstudio/extensibility/internals/list-of-available-services?view=visualstudio) - service IDs and primary interfaces such as `SDTE`, `SVsSolution`, `SVsOutputWindow`, and other shell services.
- [EnvDTE namespace](https://learn.microsoft.com/en-us/dotnet/api/envdte?view=visualstudiosdk-2022) and [DTE2 interface](https://learn.microsoft.com/en-us/dotnet/api/envdte80.dte2?view=visualstudiosdk-2022) - the main automation object model used by this server for solutions, projects, editor commands, builds, and debugging.
- Debugging APIs: [Debugger](https://learn.microsoft.com/en-us/dotnet/api/envdte.debugger?view=visualstudiosdk-2022), [SolutionBuild.Debug](https://learn.microsoft.com/en-us/dotnet/api/envdte.solutionbuild.debug?view=visualstudiosdk-2022), [Debugger.GetExpression](https://learn.microsoft.com/en-us/dotnet/api/envdte.debugger.getexpression?view=visualstudiosdk-2022), [Debugger.CurrentThread](https://learn.microsoft.com/en-us/dotnet/api/envdte.debugger.currentthread?view=visualstudiosdk-2022), [Thread.StackFrames](https://learn.microsoft.com/en-us/dotnet/api/envdte.thread.stackframes?view=visualstudiosdk-2022), and [StackFrame](https://learn.microsoft.com/en-us/dotnet/api/envdte.stackframe?view=visualstudiosdk-2022). These cover starting/debugging, expression evaluation, current thread, call stack, locals, and frame metadata.
- Breakpoint APIs: [Breakpoints](https://learn.microsoft.com/en-us/dotnet/api/envdte.breakpoints?view=visualstudiosdk-2022), [Breakpoints.Add](https://learn.microsoft.com/en-us/dotnet/api/envdte.breakpoints.add?view=visualstudiosdk-2022), and [Breakpoint](https://learn.microsoft.com/en-us/dotnet/api/envdte.breakpoint?view=visualstudiosdk-2022). Use these for file, function, conditional, hit-count, data, and address breakpoints.
- Editor/navigation automation: [DTE.ExecuteCommand](https://learn.microsoft.com/en-us/dotnet/api/envdte.dte.executecommand?view=visualstudiosdk-2022), [ItemOperations.OpenFile](https://learn.microsoft.com/en-us/dotnet/api/envdte.itemoperations.openfile?view=visualstudiosdk-2022), [TextSelection.MoveToLineAndOffset](https://learn.microsoft.com/en-us/dotnet/api/envdte.textselection.movetolineandoffset?view=visualstudiosdk-2022), and [Find references in your code](https://learn.microsoft.com/en-us/visualstudio/ide/finding-references?view=vs-2022). This is the route used for command-driven features like `Edit.GoToDefinition`, `Edit.GoToImplementation`, and `Edit.FindAllReferences`.
- Programmatic symbol/reference analysis: [Workspace.CurrentSolution](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.workspace.currentsolution?view=roslyn-dotnet-4.14.0), [Solution](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.solution?view=roslyn-dotnet-4.14.0), and [SymbolFinder.FindReferencesAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.findsymbols.symbolfinder.findreferencesasync?view=roslyn-dotnet-4.14.0). Prefer Roslyn when an MCP tool needs structured reference results instead of only opening the Visual Studio Find All References window.

## Notes on Building the MCP Server

To build the MCP server without touching the C++ sample test projects, build the server projects directly instead of building `Unoffcial-VS-MCP.sln`:

```powershell
dotnet build .\src\VsMcp.Extension\VsMcp.Extension.csproj -c Release
dotnet build .\src\VsMcp.StdioProxy\VsMcp.StdioProxy.csproj -c Release
```

The extension project builds `VsMcp.Shared` as needed. Avoid `dotnet build .\Unoffcial-VS-MCP.sln` for this purpose because the solution includes C++ sample projects under `tests\cpp\`, and .NET MSBuild does not build those `.vcxproj` projects correctly.

The `VsMcp.Extension.VS2019` project is the separate VS2019 compatibility VSIX. Its VSSDK packages intentionally stay on the VS2019-era 16.x line, including `Microsoft.VSSDK.BuildTools` 16.11.71. Do not "fix" its `CodeTaskFactory` failure by upgrading to 17.x casually; that can undermine VS2019 compatibility. `dotnet build` uses .NET Core MSBuild and fails during VSIX packaging because VSSDK BuildTools 16.x uses `CodeTaskFactory`, which .NET Core MSBuild does not support. Build the VS2019 package with Visual Studio/.NET Framework MSBuild instead, after restore:

```powershell
dotnet restore .\src\VsMcp.Extension.VS2019\VsMcp.Extension.VS2019.csproj
MSBuild.exe .\src\VsMcp.Extension.VS2019\VsMcp.Extension.VS2019.csproj /p:Configuration=Release
```

If using the current VS2026 MSBuild install on this machine, set the SDK path first and do not use `/restore` on the MSBuild invocation:

```powershell
$env:MSBuildSDKsPath = 'C:\Program Files\dotnet\sdk\10.0.300\Sdks'
$env:MSBuildEnableWorkloadResolver = 'false'
MSBuild.exe .\src\VsMcp.Extension.VS2019\VsMcp.Extension.VS2019.csproj /p:Configuration=Release
```

## Notes on local Deployment

`VsMcpPackage.DeployStdioProxy()` always copies the packaged StdioProxy files into `%LOCALAPPDATA%\VsMcp\bin\` when the extension loads.
If Visual Studio is already running and you need the proxy updated immediately after a build, copy the Release output manually:

```powershell
Copy-Item 'src\VsMcp.StdioProxy\bin\Release\net8.0\VsMcp.StdioProxy.dll',
          'src\VsMcp.StdioProxy\bin\Release\net8.0\VsMcp.StdioProxy.exe',
          'src\VsMcp.StdioProxy\bin\Release\net8.0\VsMcp.StdioProxy.deps.json',
          'src\VsMcp.StdioProxy\bin\Release\net8.0\VsMcp.StdioProxy.runtimeconfig.json',
          'src\VsMcp.StdioProxy\bin\Release\net8.0\VsMcp.Shared.dll',
          'src\VsMcp.StdioProxy\bin\Release\net8.0\Newtonsoft.Json.dll' `
    -Destination "$env:LOCALAPPDATA\VsMcp\bin\" -Force
```

When installing a rebuilt VSIX with `VSIXInstaller.exe`, do not use `/norestart`.
`VSIXInstaller.exe /?` does not print help to stdout; it opens a message box instead.
For CLI-readable diagnostics, check the `%TEMP%\dd_VSIXInstaller_*.log` file after install/uninstall/help attempts;
The exit code of the tool is not reliable.

Observed usage shape:

```text
VSIXInstaller.exe [/quiet] [/norepair] [/admin] [/prerequisitesRequired] [/force] [/shutdownprocesses] [/noextensionpack] [/instanceIds:instanceIds] [/appIdInstallPath:path] [/appIdName:name] [/skuName:name /skuVersion:version] [/logFile:filename] </uninstall:vsixID | /downgrade:vsixID | vsix_path>
```

For local redeploys, uninstall the existing extension first, then install the rebuilt VSIX into the specific Visual Studio instance:

```powershell
VSIXInstaller.exe /quiet /shutdownprocesses /instanceIds:<instanceId> /logFile:<uninstallLogPath> /uninstall:<extensionId>
VSIXInstaller.exe /quiet /force /shutdownprocesses /instanceIds:<instanceId> /logFile:<installLogPath> <vsixPath>
```

## StdioProxy Offline Response Architecture

When VS is not running, StdioProxy does not exit immediately. Instead, it returns local responses:

```
When VS is running:     Codex -> StdioProxy -> VS Extension HTTP -> forwards all requests
When VS is not running: Codex -> StdioProxy -> local response (initialize/tools/list/ping)
                                            -> tools/call returns a "VS is not running" error
```

### Related Files
- `src/VsMcp.Shared/Protocol/McpConstants.cs` - shared `GetInstructions()` method
- `src/VsMcp.Shared/ToolDefinitionCache.cs` - tool definition cache (`%LOCALAPPDATA%\VsMcp\tools-cache.json`)
- `src/VsMcp.Extension/McpServer/McpRequestRouter.cs` - `instructions` uses `McpConstants.GetInstructions()`
- `src/VsMcp.Extension/VsMcpPackage.cs` - writes the cache with `ToolDefinitionCache.Write()` after `RegisterTools()`
- `src/VsMcp.StdioProxy/Program.cs` - manages connection state through the static `_baseUrl` field and includes reconnection logic

### Routing by Method (Program.cs)
| Method | When VS is connected | When VS is not connected |
|---|---|---|
| `initialize` | Local response | Local response |
| `notifications/initialized` | No response | No response |
| `ping` | Local response | Local response |
| `tools/list` | HTTP forwarding | Response from cache |
| `tools/call` | HTTP forwarding | Error response |

## Notes When Updating Versions

When bumping the version, update the following 7 locations:

| File | Format |
|---|---|
| `src/VsMcp.Extension/source.extension.vsixmanifest` | `Identity Version="x.y.z"` |
| `src/VsMcp.Extension/extension.vsixmanifest` | Same as above (.gitignore target, generated by build) |
| `src/VsMcp.Extension/VsMcp.Extension.csproj` | `<Version>x.y.z</Version>` |
| `src/VsMcp.Extension.VS2019/source.extension.vsixmanifest` | `Identity Version="x.y.z"` |
| `src/VsMcp.Extension.VS2019/VsMcp.Extension.VS2019.csproj` | `<Version>x.y.z</Version>` |
| `src/VsMcp.Shared/VsMcp.Shared.csproj` | Same as above |
| `src/VsMcp.StdioProxy/VsMcp.StdioProxy.csproj` | Same as above |

**Important:** The VSSDK `DetokenizeVsixManifestFile` target caches `obj\Debug\net48\extension.vsixmanifest`, so after changing the version you must manually delete the `obj` directories and then rebuild. Otherwise, the change will not be reflected in the VSIX package:

```powershell
Remove-Item -Recurse -Force src\VsMcp.Extension\obj, src\VsMcp.Extension\bin, src\VsMcp.Extension.VS2019\obj, src\VsMcp.Extension.VS2019\bin
```

The `VsMcp.Extension.VS2019` paths in that cleanup command are for the separate VS2019 compatibility VSIX package. They are not used by the VS2022+/VS2026 package, but keep them in version-bump cleanup when updating both packages so the VS2019 VSIX does not retain a stale generated manifest.
