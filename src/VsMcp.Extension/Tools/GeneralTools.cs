using System;
using System.Collections.Generic;
using System.Linq;
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
    public static class GeneralTools
    {
        public static void Register(McpToolRegistry registry, VsServiceAccessor accessor)
        {
            registry.Register(
                new McpToolDefinition(
                    "execute_command",
                    "FALLBACK: execute an arbitrary Visual Studio command by name (e.g. 'Edit.FormatDocument', 'Build.BuildSolution'). Prefer a dedicated tool when one exists — for builds use build_solution/build_project, for debugging use debug_start/debug_stop, for files use file_open, etc. Only use execute_command when no dedicated tool covers the action the user wants.",
                    SchemaBuilder.Create()
                        .AddString("command", "The VS command name to execute", required: true)
                        .AddString("args", "Optional arguments for the command")
                        .Build()),
                args => ExecuteCommandAsync(accessor, args));

            registry.Register(
                new McpToolDefinition(
                    "get_status",
                    "Get the current Visual Studio status — bundles solution state, active document, and debugger mode in one call. Use this instead of curl or other HTTP requests. For a single facet, prefer the dedicated tool: solution_info (solution only), get_active_document (active document only), debug_get_mode (debugger mode only).",
                    SchemaBuilder.Empty()),
                args => GetStatusAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "get_help",
                    "Get available vs-mcp tools with descriptions. Omit category or use category='All' for every category, or pass one category to list only tools under that category.",
                    SchemaBuilder.Create()
                        .AddEnum("category", "Tool category to list. Defaults to All.", ToolCategoryMap.GetCategoryNames(includeAll: true), defaultValue: ToolCategory.All.ToString())
                        .Build()),
                args => GetHelpAsync(registry, args));
        }

        private static Task<McpToolResult> GetHelpAsync(McpToolRegistry registry, JObject args)
        {
            var requestedCategory = ToolCategory.All;
            var categoryArg = args.Value<string>("category");
            if (!string.IsNullOrWhiteSpace(categoryArg))
            {
                if (!ToolCategoryMap.TryParseCategory(categoryArg, out requestedCategory))
                    return Task.FromResult(McpToolResult.Error($"Unknown category '{categoryArg}'. Valid categories: {string.Join(", ", ToolCategoryMap.GetCategoryNames(includeAll: true))}"));
            }

            var allTools = registry.GetAllDefinitions();
            var categorized = new Dictionary<ToolCategory, List<object>>();

            foreach (var tool in allTools)
            {
                var category = ToolCategoryMap.ToolToCategory.TryGetValue(tool.Name, out var cat) ? cat : ToolCategory.Other;
                if (requestedCategory != ToolCategory.All && category != requestedCategory)
                    continue;

                if (!categorized.ContainsKey(category))
                    categorized[category] = new List<object>();

                categorized[category].Add(new
                {
                    name = tool.Name,
                    description = tool.Description
                });
            }

            var ordered = new List<object>();
            foreach (var cat in ToolCategoryMap.CategoryOrder)
            {
                if (categorized.TryGetValue(cat, out var tools))
                {
                    ordered.Add(new { category = cat.ToString(), tools });
                }
            }

            return Task.FromResult(McpToolResult.Success(new
            {
                category = requestedCategory.ToString(),
                availableCategories = ToolCategoryMap.GetCategoryNames(includeAll: true),
                totalTools = categorized.Values.Sum(tools => tools.Count),
                categories = ordered,
                guidelines = new
                {
                    ui_automation = "DPI SCALING: Screenshot pixel coordinates from ui_capture_window do NOT match screen coordinates used by ui_click/ui_drag due to DPI scaling. "
                        + "NEVER estimate coordinates from screenshots. Always use ui_find_elements to get element bounds in screen coordinates, then calculate click/drag positions from those bounds. "
                        + "POPUPS OUTSIDE WINDOW: WPF popups (context menu submenus, tooltips, etc.) may render outside the main window bounds. "
                        + "ui_click/ui_drag reject coordinates outside the window bounds. "
                        + "For elements outside the window, use ui_find_elements to locate the element by name, then use ui_click with the name parameter or ui_invoke with AutomationId instead of coordinates. "
                        + "DRAG AND HIT-TESTING: ui_drag sends Win32 mouse events, so WPF visual hit-testing applies. "
                        + "If a visual element overlaps the drag start position, the event goes to that element instead of the intended target. "
                        + "When drag does not work as expected, use ui_get_tree or ui_find_elements to check what element is at the start position. "
                        + "KEYBOARD INPUT: Use ui_send_keys to send keyboard shortcuts (e.g. 'ctrl+f', 'ctrl+s', 'alt+f4') or type text. "
                        + "The tool brings the debugged app's window to the foreground before sending keys via Win32 SendInput.",
                    web_debugging = "Use web_connect to connect to Chrome/Edge (via CDP) or Firefox (via RDP). "
                        + "Chrome/Edge: start with --remote-debugging-port (e.g. chrome --remote-debugging-port=9222). Auto-detection scans ports 9222-9229. "
                        + "Firefox: start with -start-debugger-server (e.g. firefox -start-debugger-server 6000). Requires devtools.debugger.remote-enabled=true in about:config. "
                        + "Use web_connect with browser='auto' (default) to auto-detect, or browser='chrome'/'firefox' to specify. "
                        + "Call web_console/web_network with action='enable' to start monitoring before navigating. "
                        + "Use web_js_execute for JavaScript evaluation, web_dom_query for CSS selectors, web_screenshot for page captures."
                }
            }));
        }

        private static async Task<McpToolResult> ExecuteCommandAsync(VsServiceAccessor accessor, JObject args)
        {
            var command = args.Value<string>("command");
            if (string.IsNullOrEmpty(command))
                return McpToolResult.Error("Parameter 'command' is required");

            var commandArgs = args.Value<string>("args") ?? "";

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());
                dte.ExecuteCommand(command, commandArgs);
                return McpToolResult.Success($"Command '{command}' executed successfully");
            });
        }

        private static async Task<McpToolResult> GetStatusAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                var solutionName = "";
                var solutionPath = "";
                var isOpen = false;

                try
                {
                    if (dte.Solution != null && !string.IsNullOrEmpty(dte.Solution.FullName))
                    {
                        isOpen = true;
                        solutionPath = dte.Solution.FullName;
                        solutionName = System.IO.Path.GetFileNameWithoutExtension(solutionPath);
                    }
                }
                catch { }

                var activeDoc = "";
                try
                {
                    if (dte.ActiveDocument != null)
                        activeDoc = dte.ActiveDocument.FullName;
                }
                catch { }

                var debugMode = "Design";
                try
                {
                    switch (dte.Debugger.CurrentMode)
                    {
                        case dbgDebugMode.dbgRunMode:
                            debugMode = "Running";
                            break;
                        case dbgDebugMode.dbgBreakMode:
                            debugMode = "Break";
                            break;
                        case dbgDebugMode.dbgDesignMode:
                            debugMode = "Design";
                            break;
                    }
                }
                catch { }

                return McpToolResult.Success(new
                {
                    solution = new { name = solutionName, path = solutionPath, isOpen },
                    solutionState = VsMcpPackage.SolutionState,
                    activeDocument = activeDoc,
                    debuggerMode = debugMode,
                    vsVersion = dte.Version,
                    vsEdition = dte.Edition
                });
            });
        }
    }
}
