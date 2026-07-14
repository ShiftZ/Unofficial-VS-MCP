using System;
using System.Collections.Generic;
using System.Linq;

namespace VsMcp.Shared
{
    public enum ToolCategory
    {
        General,
        Solution,
        Project,
        Build,
        Editor,
        EditPreview,
        Debugger,
        Breakpoint,
        Watch,
        Thread,
        Process,
        Immediate,
        Module,
        Register,
        Exception,
        Memory,
        Parallel,
        Diagnostics,
        Output,
        Console,
        Web,
        UI,
        Test,
        NuGet,
        Navigation,
        CppCode,
        CSharpCode,
        SolutionExplorer,
        Other
    }

    public static class ToolCategoryMap
    {
        /// <summary>
        /// Maps tool names to their category.
        /// This is the authoritative source for get_help and StdioProxy tool filtering.
        /// </summary>
        public static readonly Dictionary<string, ToolCategory> ToolToCategory = new Dictionary<string, ToolCategory>
        {
            // General
            { "execute_command", ToolCategory.General },
            { "get_status", ToolCategory.General },
            { "get_help", ToolCategory.General },
            { "vs_inproc_invoke", ToolCategory.General },
            { "vs_inproc_handles", ToolCategory.General },
            { "vs_inproc_release", ToolCategory.General },
            // Solution
            { "solution_open", ToolCategory.Solution },
            { "solution_close", ToolCategory.Solution },
            { "solution_info", ToolCategory.Solution },
            // Project
            { "project_list", ToolCategory.Project },
            { "project_info", ToolCategory.Project },
            // Build
            { "build_solution", ToolCategory.Build },
            { "build_project", ToolCategory.Build },
            { "clean", ToolCategory.Build },
            { "rebuild", ToolCategory.Build },
            { "get_build_errors", ToolCategory.Build },
            { "build_configuration", ToolCategory.Build },
            // Editor
            { "file_open", ToolCategory.Editor },
            { "file_close", ToolCategory.Editor },
            { "file_read", ToolCategory.Editor },
            { "file_write", ToolCategory.Editor },
            { "file_edit", ToolCategory.Editor },
            { "get_active_document", ToolCategory.Editor },
            { "get_selected_text", ToolCategory.Editor },
            { "find_in_files", ToolCategory.Editor },
            // Debugger
            { "debug_start", ToolCategory.Debugger },
            { "debug_start_in_break", ToolCategory.Debugger },
            { "debug_wait_break", ToolCategory.Debugger },
            { "debug_start_wait_break", ToolCategory.Debugger },
            { "debug_continue_wait_break", ToolCategory.Debugger },
            { "debug_start_without_debugging", ToolCategory.Debugger },
            { "debug_stop", ToolCategory.Debugger },
            { "debug_restart", ToolCategory.Debugger },
            { "debug_attach", ToolCategory.Debugger },
            { "debug_break", ToolCategory.Debugger },
            { "debug_continue", ToolCategory.Debugger },
            { "debug_step", ToolCategory.Debugger },
            { "debug_get_callstack", ToolCategory.Debugger },
            { "debug_switch_frame", ToolCategory.Debugger },
            { "debug_switch_process", ToolCategory.Debugger },
            { "debug_get_locals", ToolCategory.Debugger },
            { "debug_get_threads", ToolCategory.Debugger },
            { "debug_get_mode", ToolCategory.Debugger },
            { "debug_evaluate", ToolCategory.Debugger },
            // Breakpoint
            { "breakpoint_set", ToolCategory.Breakpoint },
            { "breakpoint_remove", ToolCategory.Breakpoint },
            { "breakpoint_list", ToolCategory.Breakpoint },
            { "breakpoint_enable", ToolCategory.Breakpoint },
            // Watch
            { "watch_add", ToolCategory.Watch },
            { "watch_remove", ToolCategory.Watch },
            { "watch_list", ToolCategory.Watch },
            // Thread
            { "thread_switch", ToolCategory.Thread },
            { "thread_set_frozen", ToolCategory.Thread },
            { "thread_get_callstack", ToolCategory.Thread },
            // Process
            { "process_list_debugged", ToolCategory.Process },
            { "process_list_local", ToolCategory.Process },
            { "process_detach", ToolCategory.Process },
            { "process_terminate", ToolCategory.Process },
            // Immediate Window
            { "immediate_execute", ToolCategory.Immediate },
            // Module
            { "module_list", ToolCategory.Module },
            // CPU Register
            { "register_list", ToolCategory.Register },
            { "register_get", ToolCategory.Register },
            // Exception Settings
            { "exception_settings_get", ToolCategory.Exception },
            { "exception_settings_set", ToolCategory.Exception },
            // Memory
            { "memory_read", ToolCategory.Memory },
            // Parallel Debug
            { "parallel_stacks", ToolCategory.Parallel },
            { "parallel_watch", ToolCategory.Parallel },
            { "parallel_tasks_list", ToolCategory.Parallel },
            // Diagnostics
            { "diagnostics_binding_errors", ToolCategory.Diagnostics },
            // Output
            { "output_write", ToolCategory.Output },
            { "output_read", ToolCategory.Output },
            { "error_list_get", ToolCategory.Output },
            { "output_clear", ToolCategory.Output },
            // UI Automation
            { "ui_capture_window", ToolCategory.UI },
            { "ui_capture_region", ToolCategory.UI },
            { "ui_snapshot", ToolCategory.UI },
            { "ui_get_tree", ToolCategory.UI },
            { "ui_find_elements", ToolCategory.UI },
            { "ui_wait_for_element", ToolCategory.UI },
            { "ui_wait_idle", ToolCategory.UI },
            { "ui_get_element", ToolCategory.UI },
            { "ui_click", ToolCategory.UI },
            { "ui_double_click", ToolCategory.UI },
            { "ui_right_click", ToolCategory.UI },
            { "ui_drag", ToolCategory.UI },
            { "ui_mouse_wheel", ToolCategory.UI },
            { "ui_set_value", ToolCategory.UI },
            { "ui_invoke", ToolCategory.UI },
            { "ui_send_keys", ToolCategory.UI },
            // Console
            { "console_read", ToolCategory.Console },
            { "console_send", ToolCategory.Console },
            { "console_get_info", ToolCategory.Console },
            // Web (CDP)
            { "web_connect", ToolCategory.Web },
            { "web_disconnect", ToolCategory.Web },
            { "web_status", ToolCategory.Web },
            { "web_navigate", ToolCategory.Web },
            { "web_screenshot", ToolCategory.Web },
            { "web_dom_get", ToolCategory.Web },
            { "web_dom_query", ToolCategory.Web },
            { "web_console", ToolCategory.Web },
            { "web_js_execute", ToolCategory.Web },
            { "web_network", ToolCategory.Web },
            { "web_element_click", ToolCategory.Web },
            { "web_element_set_value", ToolCategory.Web },
            // Test
            { "test_discover", ToolCategory.Test },
            { "test_run", ToolCategory.Test },
            { "test_results", ToolCategory.Test },
            // NuGet
            { "nuget_list", ToolCategory.NuGet },
            { "nuget_search", ToolCategory.NuGet },
            { "nuget_install", ToolCategory.NuGet },
            { "nuget_update", ToolCategory.NuGet },
            { "nuget_uninstall", ToolCategory.NuGet },
            // Navigation
            { "code_goto_definition", ToolCategory.Navigation },
            { "code_find_references", ToolCategory.Navigation },
            { "code_goto_implementation", ToolCategory.Navigation },
            // C++ code intelligence
            { "cpp_find_symbol", ToolCategory.CppCode },
            // C# code intelligence
            { "csharp_find_symbol", ToolCategory.CSharpCode },
            // SolutionExplorer
            { "solution_add_project", ToolCategory.SolutionExplorer },
            { "solution_remove_project", ToolCategory.SolutionExplorer },
            { "project_add_file", ToolCategory.SolutionExplorer },
            { "project_remove_file", ToolCategory.SolutionExplorer },
            { "project_add_reference", ToolCategory.SolutionExplorer },
            { "project_remove_reference", ToolCategory.SolutionExplorer },
            // EditPreview
            { "edit_preview", ToolCategory.EditPreview },
            { "edit_approve", ToolCategory.EditPreview },
            { "edit_reject", ToolCategory.EditPreview },
            { "edit_list_pending", ToolCategory.EditPreview },
        };

        public static readonly ToolCategory[] CategoryOrder =
        {
            ToolCategory.General,
            ToolCategory.Solution,
            ToolCategory.Project,
            ToolCategory.Build,
            ToolCategory.Editor,
            ToolCategory.EditPreview,
            ToolCategory.Debugger,
            ToolCategory.Breakpoint,
            ToolCategory.Watch,
            ToolCategory.Thread,
            ToolCategory.Process,
            ToolCategory.Immediate,
            ToolCategory.Module,
            ToolCategory.Register,
            ToolCategory.Exception,
            ToolCategory.Memory,
            ToolCategory.Parallel,
            ToolCategory.Diagnostics,
            ToolCategory.Output,
            ToolCategory.Console,
            ToolCategory.Web,
            ToolCategory.UI,
            ToolCategory.Test,
            ToolCategory.NuGet,
            ToolCategory.Navigation,
            ToolCategory.CppCode,
            ToolCategory.CSharpCode,
            ToolCategory.SolutionExplorer,
            ToolCategory.Other
        };

        public static readonly Dictionary<ToolCategory, string> CategoryDescriptions = new Dictionary<ToolCategory, string>
        {
            { ToolCategory.General, "Server help, Visual Studio status, and fallback command execution." },
            { ToolCategory.Solution, "Open, close, and inspect the current solution." },
            { ToolCategory.Project, "List projects and inspect project metadata." },
            { ToolCategory.Build, "Build, clean, rebuild, configure, and inspect build errors." },
            { ToolCategory.Editor, "Open, close, read, write, edit, and search files." },
            { ToolCategory.EditPreview, "Preview file edits in Visual Studio before applying them." },
            { ToolCategory.Debugger, "Start, stop, attach, switch processes, step, inspect stack frames, locals, threads, and expressions." },
            { ToolCategory.Breakpoint, "Set, remove, list, enable, and disable breakpoints." },
            { ToolCategory.Watch, "Manage persistent debugger watch expressions." },
            { ToolCategory.Thread, "Switch, freeze, thaw, and inspect debugged threads." },
            { ToolCategory.Process, "List, attach to, detach from, and terminate debugged processes." },
            { ToolCategory.Immediate, "Run side-effecting Immediate Window expressions in break mode." },
            { ToolCategory.Module, "List modules loaded in the current debug session." },
            { ToolCategory.Register, "Read CPU registers in native or mixed-mode debugging." },
            { ToolCategory.Exception, "Read and modify debugger exception break settings." },
            { ToolCategory.Memory, "Read raw memory or variable bytes while debugging." },
            { ToolCategory.Parallel, "Inspect parallel stacks, per-thread values, and task information." },
            { ToolCategory.Diagnostics, "Collect diagnostic signals such as XAML binding errors." },
            { ToolCategory.Output, "Read, write, clear Output panes and inspect the Error List." },
            { ToolCategory.Console, "Read and send input to a debugged console application." },
            { ToolCategory.Web, "Connect to browser debugging, inspect DOM, console, network, and execute JavaScript." },
            { ToolCategory.UI, "Inspect, capture, wait for, and interact with debugged desktop app UI." },
            { ToolCategory.Test, "Discover, run, and inspect tests." },
            { ToolCategory.NuGet, "List, search, install, update, and uninstall NuGet packages." },
            { ToolCategory.Navigation, "Use IDE navigation commands for definitions, implementations, and references." },
            { ToolCategory.CppCode, "Inspect C++ code symbols without navigating the IDE." },
            { ToolCategory.CSharpCode, "Inspect C# code symbols without navigating the IDE." },
            { ToolCategory.SolutionExplorer, "Modify solution and project structure in Solution Explorer." },
            { ToolCategory.Other, "Tools that do not fit a more specific category." }
        };

        public static string GetCategoryDescription(ToolCategory category)
        {
            return CategoryDescriptions.TryGetValue(category, out var description) ? description : "";
        }

        public static string[] GetCategoryNames()
        {
            var names = CategoryOrder.Select(category => category.ToString());
            return names.ToArray();
        }

        public static bool TryParseCategory(string value, out ToolCategory category)
        {
            return Enum.TryParse(value, ignoreCase: true, result: out category);
        }

        /// <summary>
        /// Presets map a preset name to a set of categories.
        /// </summary>
        public static readonly Dictionary<string, HashSet<ToolCategory>> Presets = new Dictionary<string, HashSet<ToolCategory>>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "core", new HashSet<ToolCategory>
                {
                    ToolCategory.General, ToolCategory.Solution, ToolCategory.Project,
                    ToolCategory.Build, ToolCategory.Editor, ToolCategory.EditPreview,
                    ToolCategory.Output, ToolCategory.Navigation, ToolCategory.NuGet,
                    ToolCategory.CppCode, ToolCategory.CSharpCode,
                    ToolCategory.SolutionExplorer, ToolCategory.Test
                }
            },
            {
                "debug", new HashSet<ToolCategory>
                {
                    ToolCategory.Debugger, ToolCategory.Breakpoint, ToolCategory.Watch,
                    ToolCategory.Thread, ToolCategory.Process, ToolCategory.Immediate,
                    ToolCategory.Module, ToolCategory.Register, ToolCategory.Exception,
                    ToolCategory.Memory, ToolCategory.Parallel, ToolCategory.Diagnostics,
                    ToolCategory.Console
                }
            },
            {
                "web", new HashSet<ToolCategory> { ToolCategory.Web }
            },
            {
                "ui", new HashSet<ToolCategory> { ToolCategory.UI }
            },
        };

        /// <summary>
        /// Resolves a --tools argument (e.g. "core,debug") into a set of allowed tool names.
        /// Returns null if all tools should be included (i.e. "all" or null input).
        /// </summary>
        public static HashSet<string> ResolveToolFilter(string toolsArg)
        {
            if (string.IsNullOrWhiteSpace(toolsArg))
                return null; // all tools

            var parts = toolsArg.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToList();

            if (parts.Count == 0 || parts.Any(p => p.Equals("all", StringComparison.OrdinalIgnoreCase)))
                return null; // all tools

            // Collect all categories from presets
            var allowedCategories = new HashSet<ToolCategory>();
            foreach (var part in parts)
            {
                if (Presets.TryGetValue(part, out var cats))
                {
                    foreach (var cat in cats)
                        allowedCategories.Add(cat);
                }
                else if (TryParseCategory(part, out var category))
                {
                    allowedCategories.Add(category);
                }
                else
                {
                    // Unknown category names intentionally match no tools.
                }
            }

            // Resolve to tool names
            var allowedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in ToolToCategory)
            {
                if (allowedCategories.Contains(kvp.Value))
                    allowedTools.Add(kvp.Key);
            }

            return allowedTools;
        }
    }
}
