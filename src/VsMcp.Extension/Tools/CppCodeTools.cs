using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EnvDTE;
using Newtonsoft.Json.Linq;
using VsMcp.Extension.McpServer;
using VsMcp.Extension.Services;
using VsMcp.Shared;
using VsMcp.Shared.Protocol;

namespace VsMcp.Extension.Tools
{
    public static class CppCodeTools
    {
        public static void Register(McpToolRegistry registry, VsServiceAccessor accessor)
        {
            registry.Register(
                new McpToolDefinition(
                    "cpp_find_symbol",
                    "Find C++ source declarations matching a symbol name using VCCodeModel and return structured locations without changing the active IDE document or selection.",
                    SchemaBuilder.Create()
                        .AddString("symbolName", "C++ symbol name or fully qualified name to find", required: true)
                        .AddString("projectName", "Optional C++ project name to limit the search")
                        .AddInteger("maxResults", "Maximum number of declaration locations to return (default 50)")
                        .Build()),
                args => FindSymbolAsync(accessor, args));
        }

        private static async Task<McpToolResult> FindSymbolAsync(VsServiceAccessor accessor, JObject args)
        {
            var symbolName = args.Value<string>("symbolName");
            if (string.IsNullOrWhiteSpace(symbolName))
                return McpToolResult.Error("Parameter 'symbolName' is required");

            var projectName = args.Value<string>("projectName");
            var maxResults = args.Value<int?>("maxResults") ?? 50;
            if (maxResults <= 0) maxResults = 50;

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Solution == null || string.IsNullOrEmpty(dte.Solution.FullName))
                    return McpToolResult.Error("No solution is currently open");

                var projects = ProjectModelHelpers.EnumerateSolutionProjects(dte.Solution.Projects);

                var matches = new List<object>();
                var inspectedProjects = new List<string>();
                var errors = new List<object>();

                foreach (var project in projects)
                {
                    if (!string.IsNullOrEmpty(projectName)
                        && !string.Equals(project.Name, projectName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!ProjectModelHelpers.IsCppProject(project)) continue;

                    inspectedProjects.Add(project.Name);

                    try
                    {
                        var codeModel = project.CodeModel;
                        if (codeModel == null) continue;

                        var codeElements = GetCodeElementsFromFullName(codeModel, symbolName);
                        if (codeElements == null) continue;

                        var count = 0;
                        try { count = (int)codeElements.Count; }
                        catch { count = 1; }

                        for (var index = 1; index <= count && matches.Count < maxResults; index++)
                        {
                            try
                            {
                                var element = count == 1
                                    ? TryGetSingleElement(codeElements)
                                    : codeElements.Item(index) as CodeElement;

                                if (element == null) continue;

                                var startPoint = element.StartPoint;
                                var file = "";
                                try { file = element.ProjectItem?.FileNames[1] ?? ""; }
                                catch { }

                                matches.Add(new
                                {
                                    project = project.Name,
                                    name = element.Name,
                                    fullName = element.FullName,
                                    kind = element.Kind.ToString(),
                                    file,
                                    line = startPoint.Line,
                                    column = startPoint.LineCharOffset
                                });
                            }
                            catch (COMException ex)
                            {
                                errors.Add(new { project = project.Name, message = ex.Message });
                            }
                        }
                    }
                    catch (COMException ex)
                    {
                        errors.Add(new { project = project.Name, message = ex.Message });
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new { project = project.Name, message = ex.Message });
                    }

                    if (matches.Count >= maxResults) break;
                }

                return McpToolResult.Success(new
                {
                    symbolName,
                    language = "C++",
                    matches,
                    inspectedProjects,
                    errors
                });
            });
        }

        private static CodeElement TryGetSingleElement(CodeElements codeElements)
        {
            try { return codeElements.Item(1) as CodeElement; }
            catch { return null; }
        }

        private static CodeElements GetCodeElementsFromFullName(CodeModel codeModel, string symbolName)
        {
            if (codeModel == null) return null;

            var result = codeModel.GetType().InvokeMember(
                "CodeElementFromFullName",
                BindingFlags.InvokeMethod,
                null,
                codeModel,
                new object[] { symbolName });

            return result as CodeElements;
        }
    }
}
