using System;
using System.Collections.Generic;
using System.IO;
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
    public static class BreakpointTools
    {
        public static void Register(McpToolRegistry registry, VsServiceAccessor accessor)
        {
            registry.Register(
                new McpToolDefinition(
                    "breakpoint_set",
                    "Set a breakpoint. Use file+line for location breakpoints, or functionName for function breakpoints. Optionally add condition or hitCount. Conditions must be simple, side-effect-free Boolean expressions over in-scope values (for example, 'count == 10'); do not invoke methods or functions. For more information see https://learn.microsoft.com/en-us/visualstudio/debugger/expressions-in-the-debugger?view=visualstudio. List of supported intrinsics: https://learn.microsoft.com/en-us/visualstudio/debugger/expressions-in-the-debugger?view=visualstudio#BKMK_Using_debugger_intrinisic_functions_to_maintain_state",
                    SchemaBuilder.Create()
                        .AddString("file", "Full path to the source file (required for location breakpoints)")
                        .AddInteger("line", "Line number to set the breakpoint (required for location breakpoints)")
                        .AddString("functionName", "Fully qualified function name for function breakpoints (e.g. 'MyNamespace.MyClass.MyMethod')")
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
                // Function breakpoint
                return await accessor.RunOnUIThreadAsync(() =>
                {
                    var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                        .Run(() => accessor.GetDteAsync());

                    return RunWithSuppressedUi(dte, () =>
                    {
                        var breakpointId = CreateBreakpointId(functionName);
                        if (!string.IsNullOrEmpty(condition))
                        {
                            var created = dte.Debugger.Breakpoints.Add(Function: functionName,
                                Condition: condition,
                                ConditionType: dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue);
                            var taggingError = TagCreatedBreakpoints(created, breakpointId);
                            if (taggingError != null)
                            {
                                return McpToolResult.Error(
                                    $"Failed to set conditional function breakpoint on '{functionName}': {taggingError}");
                            }

                            var validationError = ValidateCreatedBreakpoint(dte, created, condition, breakpointId);
                            if (validationError != null)
                            {
                                return McpToolResult.Error(
                                    $"Failed to set conditional function breakpoint on '{functionName}': {validationError}");
                            }
                        }
                        else
                        {
                            var created = dte.Debugger.Breakpoints.Add(Function: functionName);
                            var taggingError = TagCreatedBreakpoints(created, breakpointId);
                            if (taggingError != null)
                            {
                                return McpToolResult.Error(
                                    $"Failed to set function breakpoint on '{functionName}': {taggingError}");
                            }

                            var validationError = ValidateCreatedBreakpoint(dte, created, null, breakpointId);
                            if (validationError != null)
                            {
                                return McpToolResult.Error(
                                    $"Failed to set function breakpoint on '{functionName}': {validationError}");
                            }
                        }

                        return McpToolResult.Success(new
                        {
                            message = $"Function breakpoint set on '{functionName}'",
                            functionName,
                            condition = condition ?? "",
                            id = breakpointId
                        });
                    });
                });
            }

            // Location breakpoint
            if (string.IsNullOrEmpty(file))
                return McpToolResult.Error("Either 'functionName' or 'file'+'line' is required");
            if (!line.HasValue || line.Value <= 0)
                return McpToolResult.Error("Parameter 'line' is required and must be positive");

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                return RunWithSuppressedUi(dte, () =>
                {
                    var breakpointId = CreateBreakpointId(Path.GetFileName(file) + ":" + line.Value);
                    if (hitCount.HasValue && hitCount.Value > 0)
                    {
                        // Hit count breakpoint
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

                        var created = dte.Debugger.Breakpoints.Add("", file, line.Value,
                            HitCount: hitCount.Value,
                            HitCountType: hitCountType);
                        var taggingError = TagCreatedBreakpoints(created, breakpointId);
                        if (taggingError != null)
                        {
                            return McpToolResult.Error(
                                $"Failed to set breakpoint with hit count at {file}:{line.Value}: {taggingError}");
                        }

                        var validationError = ValidateCreatedBreakpoint(dte, created, null, breakpointId);
                        if (validationError != null)
                        {
                            return McpToolResult.Error(
                                $"Failed to set breakpoint with hit count at {file}:{line.Value}: {validationError}");
                        }

                        return McpToolResult.Success(new
                        {
                            message = $"Breakpoint with hit count set at {file}:{line.Value}",
                            hitCount = hitCount.Value,
                            hitCountType = hitCountTypeStr,
                            id = breakpointId
                        });
                    }

                    if (!string.IsNullOrEmpty(condition))
                    {
                        // Conditional breakpoint
                        var created = dte.Debugger.Breakpoints.Add("", file, line.Value,
                            Condition: condition,
                            ConditionType: dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue);
                        var taggingError = TagCreatedBreakpoints(created, breakpointId);
                        if (taggingError != null)
                        {
                            return McpToolResult.Error(
                                $"Failed to set conditional breakpoint at {file}:{line.Value}: {taggingError}");
                        }

                        var validationError = ValidateCreatedBreakpoint(dte, created, condition, breakpointId);
                        if (validationError != null)
                        {
                            return McpToolResult.Error(
                                $"Failed to set conditional breakpoint at {file}:{line.Value}: {validationError}");
                        }

                        return McpToolResult.Success(new
                        {
                            message = $"Conditional breakpoint set at {file}:{line.Value} (condition: {condition})",
                            id = breakpointId
                        });
                    }

                    // Simple breakpoint
                    var simpleBreakpoints = dte.Debugger.Breakpoints.Add("", file, line.Value);
                    var simpleTaggingError = TagCreatedBreakpoints(simpleBreakpoints, breakpointId);
                    if (simpleTaggingError != null)
                    {
                        return McpToolResult.Error(
                            $"Failed to set breakpoint at {file}:{line.Value}: {simpleTaggingError}");
                    }

                    var simpleValidationError = ValidateCreatedBreakpoint(
                        dte, simpleBreakpoints, null, breakpointId);
                    if (simpleValidationError != null)
                    {
                        return McpToolResult.Error(
                            $"Failed to set breakpoint at {file}:{line.Value}: {simpleValidationError}");
                    }

                    return McpToolResult.Success(new
                    {
                        message = $"Breakpoint set at {file}:{line.Value}",
                        id = breakpointId
                    });
                });
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

        private static string ValidateCreatedBreakpoint(DTE2 dte, Breakpoints created, string expectedCondition,
            string breakpointId)
        {
            var debuggerMode = dte.Debugger.CurrentMode;
            var validationError = ValidateCreatedBreakpoints(created, expectedCondition,
                requireRuntimeBounds: debuggerMode == dbgDebugMode.dbgRunMode);
            if (validationError == null) return null;

            if (debuggerMode != dbgDebugMode.dbgRunMode)
                return validationError;

            var cleanupError = TryDeleteTaggedBreakpoint(dte, breakpointId);
            if (cleanupError == null) return validationError;

            return validationError + " Cleanup failed: " + cleanupError;
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
