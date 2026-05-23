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

## Notes on StdioProxy Deployment

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
