using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Newtonsoft.Json.Linq;
using VsMcp.Extension.McpServer;
using VsMcp.Extension.Services;
using VsMcp.Shared;
using VsMcp.Shared.Protocol;

namespace VsMcp.Extension.Tools
{
    public static class BuildTools
    {
        public static void Register(McpToolRegistry registry, VsServiceAccessor accessor)
        {
            registry.Register(
                new McpToolDefinition(
                    "build_solution",
                    "Build the entire solution in Visual Studio. Use this instead of running MSBuild.exe from the command line.",
                    SchemaBuilder.Empty()),
                args => BuildSolutionAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "build_project",
                    "Build a specific project",
                    SchemaBuilder.Create()
                        .AddString("name", "Project name to build", required: true)
                        .Build()),
                args => BuildProjectAsync(accessor, args));

            registry.Register(
                new McpToolDefinition(
                    "clean",
                    "Clean the solution build output",
                    SchemaBuilder.Empty()),
                args => CleanAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "rebuild",
                    "Clean and rebuild the entire solution",
                    SchemaBuilder.Empty()),
                args => RebuildAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "get_build_errors",
                    "Get the list of build errors and warnings from the Visual Studio Error List. Call this after build_solution to check results.",
                    SchemaBuilder.Empty()),
                args => GetBuildErrorsAsync(accessor));
        }

        private static void StopDebuggingIfActive(DTE2 dte)
        {
            try
            {
                if (dte.Debugger.CurrentMode != dbgDebugMode.dbgDesignMode)
                {
                    dte.Debugger.Stop(true);
                }
            }
            catch { }
        }

        private static void ShowOutputWindow(DTE2 dte)
        {
            try
            {
                var outputWindow = dte.ToolWindows.OutputWindow;
                outputWindow.Parent.Activate();

                // Activate the "Build" pane
                foreach (OutputWindowPane pane in outputWindow.OutputWindowPanes)
                {
                    try
                    {
                        // Match localized "Build" / "ビルド" pane by GUID
                        if (string.Equals(pane.Guid, "{1BD8A850-02D1-11D1-BEE7-00A0C913D1F8}", StringComparison.OrdinalIgnoreCase))
                        {
                            pane.Activate();
                            break;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static async Task<McpToolResult> BuildSolutionAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                StopDebuggingIfActive(dte);
                ShowOutputWindow(dte);

                var sb = (SolutionBuild2)dte.Solution.SolutionBuild;
                sb.Build(true);

                var succeeded = sb.LastBuildInfo == 0;
                return McpToolResult.Success(new
                {
                    success = succeeded,
                    failedProjects = sb.LastBuildInfo,
                    message = succeeded ? "Build succeeded" : $"Build failed with {sb.LastBuildInfo} project(s) having errors"
                });
            });
        }

        private static async Task<McpToolResult> BuildProjectAsync(VsServiceAccessor accessor, JObject args)
        {
            var name = args.Value<string>("name");
            if (string.IsNullOrEmpty(name))
                return McpToolResult.Error("Parameter 'name' is required");

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                StopDebuggingIfActive(dte);
                ShowOutputWindow(dte);

                var sb = (SolutionBuild2)dte.Solution.SolutionBuild;
                var config = sb.ActiveConfiguration?.Name ?? "Debug";

                // Find the project unique name (recursing into solution folders)
                string uniqueName = FindProjectUniqueName(dte.Solution.Projects, name);

                if (uniqueName == null)
                    return McpToolResult.Error($"Project '{name}' not found");

                sb.BuildProject(config, uniqueName, true);

                var succeeded = sb.LastBuildInfo == 0;
                return McpToolResult.Success(new
                {
                    success = succeeded,
                    project = name,
                    message = succeeded ? $"Project '{name}' built successfully" : $"Project '{name}' build failed"
                });
            });
        }

        private static async Task<McpToolResult> CleanAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                StopDebuggingIfActive(dte);
                ShowOutputWindow(dte);

                var sb = (SolutionBuild2)dte.Solution.SolutionBuild;
                sb.Clean(true);

                return McpToolResult.Success("Solution cleaned successfully");
            });
        }

        private static async Task<McpToolResult> RebuildAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                StopDebuggingIfActive(dte);
                ShowOutputWindow(dte);

                var sb = (SolutionBuild2)dte.Solution.SolutionBuild;
                sb.Build(true);

                var succeeded = sb.LastBuildInfo == 0;
                return McpToolResult.Success(new
                {
                    success = succeeded,
                    failedProjects = sb.LastBuildInfo,
                    message = succeeded ? "Rebuild succeeded" : $"Rebuild failed with {sb.LastBuildInfo} project(s) having errors"
                });
            });
        }

        private static async Task<McpToolResult> GetBuildErrorsAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                var errors = new List<object>();
                var errorList = dte.ToolWindows.ErrorList;
                var errorItems = errorList.ErrorItems;

                for (int i = 1; i <= errorItems.Count; i++)
                {
                    try
                    {
                        var item = errorItems.Item(i);
                        errors.Add(new
                        {
                            severity = GetSeverity(item),
                            description = item.Description,
                            file = item.FileName,
                            line = item.Line,
                            column = item.Column,
                            project = item.Project
                        });
                    }
                    catch { }
                }

                return McpToolResult.Success(new
                {
                    totalCount = errors.Count,
                    errors
                });
            });
        }

        private static string FindProjectUniqueName(Projects projects, string name)
        {
            foreach (Project project in projects)
            {
                try
                {
                    var result = FindProjectUniqueNameRecursive(project, name);
                    if (result != null)
                        return result;
                }
                catch { }
            }
            return null;
        }

        private static string FindProjectUniqueNameRecursive(Project project, string name)
        {
            // Solution folder — recurse into sub-projects
            if (project.Kind == "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}")
            {
                if (project.ProjectItems != null)
                {
                    foreach (ProjectItem item in project.ProjectItems)
                    {
                        try
                        {
                            if (item.SubProject != null)
                            {
                                var result = FindProjectUniqueNameRecursive(item.SubProject, name);
                                if (result != null)
                                    return result;
                            }
                        }
                        catch { }
                    }
                }
                return null;
            }

            if (string.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase))
                return project.UniqueName;

            return null;
        }

        private static string GetSeverity(ErrorItem item)
        {
            try
            {
                switch (item.ErrorLevel)
                {
                    case vsBuildErrorLevel.vsBuildErrorLevelHigh:
                        return "error";
                    case vsBuildErrorLevel.vsBuildErrorLevelMedium:
                        return "warning";
                    case vsBuildErrorLevel.vsBuildErrorLevelLow:
                        return "message";
                    default:
                        return "unknown";
                }
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
