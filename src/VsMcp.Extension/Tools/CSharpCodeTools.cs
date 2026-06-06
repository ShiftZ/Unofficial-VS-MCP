using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Newtonsoft.Json.Linq;
using VsMcp.Extension.McpServer;
using VsMcp.Extension.Services;
using VsMcp.Shared;
using VsMcp.Shared.Protocol;

namespace VsMcp.Extension.Tools
{
    public static class CSharpCodeTools
    {
        public static void Register(McpToolRegistry registry, VsServiceAccessor accessor)
        {
            registry.Register(
                new McpToolDefinition(
                    "csharp_find_symbol",
                    "Find C# source declarations matching a symbol name using Roslyn and return structured locations without changing the active IDE document or selection.",
                    SchemaBuilder.Create()
                        .AddString("symbolName", "C# symbol name to find", required: true)
                        .AddString("projectName", "Optional C# project name to limit the search")
                        .AddBoolean("ignoreCase", "Whether to match symbol names case-insensitively")
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
            var ignoreCase = args.Value<bool?>("ignoreCase") ?? false;
            var maxResults = args.Value<int?>("maxResults") ?? 50;
            if (maxResults <= 0) maxResults = 50;

            var componentModel = (IComponentModel)await accessor.GetServiceAsync(typeof(SComponentModel));
            if (componentModel == null)
                return McpToolResult.Error("Visual Studio component model is unavailable");

            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            if (workspace == null)
                return McpToolResult.Error("VisualStudioWorkspace is unavailable");

            var solution = workspace.CurrentSolution;
            if (solution == null || solution.ProjectIds.Count == 0)
                return McpToolResult.Error("No Roslyn solution is currently available");

            var csharpProjects = solution.Projects
                .Where(project => project.Language == LanguageNames.CSharp)
                .Where(project => string.IsNullOrEmpty(projectName)
                    || string.Equals(project.Name, projectName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!csharpProjects.Any())
                return McpToolResult.Success(new
                {
                    symbolName,
                    language = "C#",
                    matches = Array.Empty<object>(),
                    inspectedProjects = Array.Empty<string>()
                });

            var csharpProjectIds = new HashSet<ProjectId>(csharpProjects.Select(project => project.Id));
            var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
                solution,
                symbolName,
                ignoreCase,
                SymbolFilter.TypeAndMember,
                CancellationToken.None);

            var matches = new List<object>();

            foreach (var symbol in symbols)
            {
                foreach (var location in symbol.Locations)
                {
                    if (!location.IsInSource || location.SourceTree == null) continue;

                    var document = solution.GetDocument(location.SourceTree);
                    if (document == null || !csharpProjectIds.Contains(document.Project.Id))
                        continue;

                    var span = location.GetLineSpan();
                    matches.Add(new
                    {
                        project = document.Project.Name,
                        name = symbol.Name,
                        fullName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        kind = symbol.Kind.ToString(),
                        file = span.Path,
                        line = span.StartLinePosition.Line + 1,
                        column = span.StartLinePosition.Character + 1
                    });

                    if (matches.Count >= maxResults)
                        break;
                }

                if (matches.Count >= maxResults)
                    break;
            }

            return McpToolResult.Success(new
            {
                symbolName,
                language = "C#",
                matches,
                inspectedProjects = csharpProjects.Select(project => project.Name).ToArray()
            });
        }
    }
}
