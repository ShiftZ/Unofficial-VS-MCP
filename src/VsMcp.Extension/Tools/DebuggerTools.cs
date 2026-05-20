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
using Stopwatch = System.Diagnostics.Stopwatch;

namespace VsMcp.Extension.Tools
{
    public static class DebuggerTools
    {
        private static readonly TimeSpan WaitBreakTimeoutPadding = TimeSpan.FromSeconds(15);

        public static void Register(McpToolRegistry registry, VsServiceAccessor accessor)
        {
            registry.Register(
                new McpToolDefinition(
                    "debug_start",
                    "F5: start the startup project WITH the debugger attached (breakpoints hit, exceptions break into VS). Use this when the user wants to debug. For run-without-debugging (Ctrl+F5), use debug_start_without_debugging.",
                    SchemaBuilder.Empty()),
                args => DebugStartAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "debug_start_without_debugging",
                    "Ctrl+F5: run the startup project WITHOUT the debugger attached (breakpoints are ignored, exceptions do not break into VS). Use this when the user wants to just run the app. For debugging (F5) with breakpoints, use debug_start.",
                    SchemaBuilder.Empty()),
                args => DebugStartWithoutDebuggingAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "debug_stop",
                    "Stop the current debug session normally (Shift+F5). Detaches and terminates the debuggee like clicking the VS Stop button. This is the default 'stop debugging' action. To detach without terminating, use process_detach. To force-kill a specific debugged process, use process_terminate.",
                    SchemaBuilder.Empty()),
                args => DebugStopAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "debug_restart",
                    "Restart debugging the current session",
                    SchemaBuilder.Empty()),
                args => DebugRestartAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "debug_attach",
                    "Attach the debugger to a running process by name or PID",
                    SchemaBuilder.Create()
                        .AddString("processName", "Name of the process to attach to (e.g. 'myapp')")
                        .AddInteger("processId", "PID of the process to attach to")
                        .Build()),
                args => DebugAttachAsync(accessor, args));

            registry.Register(
                new McpToolDefinition(
                    "debug_break",
                    "Break (pause) the debugger at the current execution point",
                    SchemaBuilder.Empty()),
                args => DebugBreakAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "debug_continue",
                    "Continue (resume) execution after a breakpoint or break",
                    SchemaBuilder.Empty()),
                args => DebugContinueAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "debug_step",
                    "Step through code. direction: over (F10), into (F11), or out (Shift+F11)",
                    SchemaBuilder.Create()
                        .AddEnum("direction", "Step direction", new[] { "over", "into", "out" }, required: true)
                        .Build()),
                args => DebugStepAsync(accessor, args));

            registry.Register(
                new McpToolDefinition(
                    "debug_get_callstack",
                    "Get the current call stack of the active thread",
                    SchemaBuilder.Empty()),
                args => DebugGetCallstackAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "debug_get_locals",
                    "Get the local variables in the current stack frame",
                    SchemaBuilder.Empty()),
                args => DebugGetLocalsAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "debug_get_threads",
                    "Get all threads in the current debug session",
                    SchemaBuilder.Empty()),
                args => DebugGetThreadsAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "debug_get_mode",
                    "Get the current debugger mode (Design, Running, or Break)",
                    SchemaBuilder.Empty()),
                args => DebugGetModeAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "debugger_wait_break",
                    "Wait until the debugger stops or the timeout expires. This is event-driven: it listens for VS debugger break/design-mode events, does not continue execution, and returns the stop reason plus current break context when available. The time_out parameter is in milliseconds.",
                    SchemaBuilder.Create()
                        .AddInteger("time_out", "Timeout in milliseconds", required: true)
                        .Build()),
                args => DebuggerWaitBreakAsync(accessor, args),
                GetDebuggerWaitBreakTimeout);

            registry.Register(
                new McpToolDefinition(
                    "debug_evaluate",
                    "Read-only evaluate an expression in the current debug context — no side effects (no assignments, no method calls that mutate state). Must be in break mode. Use this to inspect variable values. For expressions WITH side effects (assignments, mutating method calls) use immediate_execute. To persist an expression that re-evaluates across breaks, use watch_add.",
                    SchemaBuilder.Create()
                        .AddString("expression", "The expression to evaluate", required: true)
                        .Build()),
                args => DebugEvaluateAsync(accessor, args));
        }

        private static async Task<McpToolResult> DebugStartAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                dte.Solution.SolutionBuild.Debug();
                return McpToolResult.Success("Debugging started");
            });
        }

        private static async Task<McpToolResult> DebugStartWithoutDebuggingAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                dte.ExecuteCommand("Debug.StartWithoutDebugging");
                return McpToolResult.Success("Started without debugging");
            });
        }

        private static async Task<McpToolResult> DebugStopAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
                    return McpToolResult.Error("Debugger is not running");

                dte.Debugger.Stop(false);
                return McpToolResult.Success("Debugging stopped");
            });
        }

        private static async Task<McpToolResult> DebugRestartAsync(VsServiceAccessor accessor)
        {
            // Step 1: Stop debugging
            var isRunning = await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
                    return false;

                dte.Debugger.Stop(false);
                return true;
            });

            if (!isRunning)
                return McpToolResult.Error("Debugger is not running");

            // Step 2: Wait for debugger to reach Design mode (up to 15 seconds)
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(500);
                var mode = await accessor.RunOnUIThreadAsync(() =>
                {
                    var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                        .Run(() => accessor.GetDteAsync());
                    return dte.Debugger.CurrentMode;
                });
                if (mode == dbgDebugMode.dbgDesignMode)
                    break;
            }

            // Step 3: Start debugging
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());
                dte.Solution.SolutionBuild.Debug();
                return McpToolResult.Success("Debugging restarted");
            });
        }

        private static async Task<McpToolResult> DebugAttachAsync(VsServiceAccessor accessor, JObject args)
        {
            var processName = args.Value<string>("processName");
            var processId = args.Value<int?>("processId");

            if (string.IsNullOrEmpty(processName) && !processId.HasValue)
                return McpToolResult.Error("Either 'processName' or 'processId' is required");

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                var processes = dte.Debugger.LocalProcesses;
                foreach (Process2 process in processes)
                {
                    try
                    {
                        bool match = false;
                        if (processId.HasValue && process.ProcessID == processId.Value)
                            match = true;
                        else if (!string.IsNullOrEmpty(processName) &&
                                 process.Name.IndexOf(processName, StringComparison.OrdinalIgnoreCase) >= 0)
                            match = true;

                        if (match)
                        {
                            process.Attach();
                            return McpToolResult.Success(new
                            {
                                message = $"Attached to process: {process.Name} (PID: {process.ProcessID})",
                                processName = process.Name,
                                processId = process.ProcessID
                            });
                        }
                    }
                    catch { }
                }

                return McpToolResult.Error($"Process not found: {processName ?? processId?.ToString()}");
            });
        }

        private static async Task<McpToolResult> DebugBreakAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode != dbgDebugMode.dbgRunMode)
                    return McpToolResult.Error("Debugger must be in Running mode to break");

                dte.Debugger.Break(false);
                return McpToolResult.Success("Debugger paused");
            });
        }

        private static async Task<McpToolResult> DebugContinueAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                    return McpToolResult.Error("Debugger must be in Break mode to continue");

                dte.Debugger.Go(false);
                return McpToolResult.Success("Execution continued");
            });
        }

        private static async Task<McpToolResult> DebugStepAsync(VsServiceAccessor accessor, JObject args)
        {
            var direction = args.Value<string>("direction");
            if (string.IsNullOrEmpty(direction))
                return McpToolResult.Error("Parameter 'direction' is required");

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                    return McpToolResult.Error("Debugger must be in Break mode to step");

                switch (direction)
                {
                    case "over":
                        dte.Debugger.StepOver(false);
                        return McpToolResult.Success("Stepped over");
                    case "into":
                        dte.Debugger.StepInto(false);
                        return McpToolResult.Success("Stepped into");
                    case "out":
                        dte.Debugger.StepOut(false);
                        return McpToolResult.Success("Stepped out");
                    default:
                        return McpToolResult.Error($"Unknown direction: '{direction}'. Use 'over', 'into', or 'out'.");
                }
            });
        }

        private static async Task<McpToolResult> DebugGetCallstackAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                    return McpToolResult.Error("Debugger must be in Break mode to get callstack");

                var thread = dte.Debugger.CurrentThread;
                var frames = new List<object>();

                foreach (StackFrame frame in thread.StackFrames)
                {
                    try
                    {
                        frames.Add(new
                        {
                            functionName = frame.FunctionName,
                            module = frame.Module,
                            fileName = DebugHelpers.TryGetFrameFileName(frame),
                            line = DebugHelpers.TryGetFrameLine(frame),
                            language = frame.Language
                        });
                    }
                    catch { }
                }

                return McpToolResult.Success(new
                {
                    threadId = thread.ID,
                    threadName = thread.Name,
                    frameCount = frames.Count,
                    frames
                });
            });
        }


        private static async Task<McpToolResult> DebugGetLocalsAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                    return McpToolResult.Error("Debugger must be in Break mode to get locals");

                var frame = dte.Debugger.CurrentStackFrame;

                // If current frame has no locals (e.g., native code), try to find a managed frame
                if (frame == null || frame.Locals == null || frame.Locals.Count == 0)
                {
                    if (!DebugHelpers.TryNavigateToManagedFrame(dte.Debugger))
                        return McpToolResult.Error("No managed stack frame found. The debugger is stopped in native code.");
                    frame = dte.Debugger.CurrentStackFrame;
                }

                var locals = new List<object>();
                var frameInfo = new { functionName = frame.FunctionName, language = frame.Language };

                foreach (Expression local in frame.Locals)
                {
                    try
                    {
                        var item = new Dictionary<string, object>
                        {
                            ["name"] = local.Name,
                            ["type"] = local.Type,
                            ["value"] = local.Value
                        };

                        // Include child members for complex types (up to 10)
                        if (local.DataMembers != null && local.DataMembers.Count > 0)
                        {
                            var members = new List<object>();
                            var count = 0;
                            foreach (Expression member in local.DataMembers)
                            {
                                if (count++ >= 10) break;
                                try
                                {
                                    members.Add(new
                                    {
                                        name = member.Name,
                                        type = member.Type,
                                        value = member.Value
                                    });
                                }
                                catch { }
                            }
                            item["members"] = members;
                        }

                        locals.Add(item);
                    }
                    catch { }
                }

                return McpToolResult.Success(new { frame = frameInfo, locals });
            });
        }

        private static async Task<McpToolResult> DebugGetThreadsAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
                    return McpToolResult.Error("Debugger is not running");

                var threads = new List<object>();
                foreach (Thread thread in dte.Debugger.CurrentProgram.Threads)
                {
                    try
                    {
                        threads.Add(new
                        {
                            id = thread.ID,
                            name = thread.Name,
                            isFrozen = thread.IsFrozen,
                            isAlive = thread.IsAlive,
                            location = DebugHelpers.TryGetThreadLocation(thread)
                        });
                    }
                    catch { }
                }

                return McpToolResult.Success(new
                {
                    currentThreadId = dte.Debugger.CurrentThread?.ID,
                    threads
                });
            });
        }

        private static async Task<McpToolResult> DebugGetModeAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                string mode;
                switch (dte.Debugger.CurrentMode)
                {
                    case dbgDebugMode.dbgRunMode:
                        mode = "Running";
                        break;
                    case dbgDebugMode.dbgBreakMode:
                        mode = "Break";
                        break;
                    case dbgDebugMode.dbgDesignMode:
                    default:
                        mode = "Design";
                        break;
                }

                return McpToolResult.Success(new { mode });
            });
        }

        private static async Task<McpToolResult> DebuggerWaitBreakAsync(VsServiceAccessor accessor, JObject args)
        {
            var timeoutMs = args.Value<int?>("time_out");
            if (!timeoutMs.HasValue || timeoutMs.Value <= 0)
                return McpToolResult.Error("Parameter 'time_out' is required and must be positive");

            var stopwatch = Stopwatch.StartNew();
            DebuggerWaitRegistration registration = null;

            try
            {
                registration = await accessor.RunOnUIThreadAsync(() =>
                {
                    var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                        .Run(() => accessor.GetDteAsync());

                    var debuggerEvents = dte.Events.DebuggerEvents;
                    var waitRegistration = new DebuggerWaitRegistration(debuggerEvents);
                    waitRegistration.Subscribe();

                    var currentMode = dte.Debugger.CurrentMode;
                    if (currentMode != dbgDebugMode.dbgRunMode)
                        waitRegistration.Complete(currentMode == dbgDebugMode.dbgBreakMode ? "break" : "stopped");

                    return waitRegistration;
                });

                var delayTask = Task.Delay(timeoutMs.Value);
                var completed = await Task.WhenAny(registration.WaitTask, delayTask).ConfigureAwait(false);
                var signal = completed == registration.WaitTask || registration.WaitTask.IsCompleted
                    ? await registration.WaitTask.ConfigureAwait(false)
                    : new WaitBreakSignal("timeout", null, null);

                stopwatch.Stop();

                return await accessor.RunOnUIThreadAsync(() =>
                {
                    var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                        .Run(() => accessor.GetDteAsync());

                    return CreateWaitBreakResult(dte, signal, stopwatch.ElapsedMilliseconds);
                });
            }
            finally
            {
                if (registration != null)
                {
                    try
                    {
                        await accessor.RunOnUIThreadAsync(() => registration.Unsubscribe());
                    }
                    catch { }
                }
            }
        }

        private static async Task<McpToolResult> DebugEvaluateAsync(VsServiceAccessor accessor, JObject args)
        {
            var expression = args.Value<string>("expression");
            if (string.IsNullOrEmpty(expression))
                return McpToolResult.Error("Parameter 'expression' is required");

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                    return McpToolResult.Error("Debugger must be in Break mode to evaluate expressions");

                // Try evaluation, searching across frames and threads for one that works
                var evalResult = DebugHelpers.TryEvaluateExpression(dte.Debugger, expression);
                if (evalResult == null)
                    return McpToolResult.Error("Expression evaluation failed: no suitable managed frame found");

                var resultText = $"[debug_evaluate] {expression} = {evalResult.Value}  ({evalResult.Type})";
                try
                {
                    var outputWindow = dte.ToolWindows.OutputWindow;
                    OutputWindowPane pane = null;
                    foreach (OutputWindowPane p in outputWindow.OutputWindowPanes)
                    {
                        if (p.Name == "VsMcp") { pane = p; break; }
                    }
                    if (pane == null)
                        pane = outputWindow.OutputWindowPanes.Add("VsMcp");
                    pane.OutputString(resultText + Environment.NewLine);
                    pane.Activate();
                }
                catch { /* best-effort */ }

                return McpToolResult.Success(new
                {
                    expression,
                    value = evalResult.Value,
                    type = evalResult.Type,
                    name = evalResult.Name,
                    frame = dte.Debugger.CurrentStackFrame?.FunctionName
                });
            });
        }

        private static McpToolResult CreateWaitBreakResult(DTE2 dte, WaitBreakSignal signal, long elapsedMs)
        {
            var mode = "Unknown";
            dbgDebugMode? currentMode = null;

            try
            {
                currentMode = dte.Debugger.CurrentMode;
                mode = GetDebuggerModeName(currentMode.Value);
            }
            catch { }

            var result = new Dictionary<string, object>
            {
                ["status"] = signal.Status,
                ["mode"] = mode,
                ["reasonCode"] = signal.ReasonCode,
                ["eventName"] = signal.EventName,
                ["timedOut"] = signal.Status == "timeout",
                ["elapsedMs"] = elapsedMs
            };

            if (currentMode != dbgDebugMode.dbgBreakMode)
                return McpToolResult.Success(result);

            try
            {
                var thread = dte.Debugger.CurrentThread;
                if (thread != null)
                {
                    result["threadId"] = thread.ID;
                    result["threadName"] = thread.Name;
                }
            }
            catch { }

            try
            {
                var frame = dte.Debugger.CurrentStackFrame;
                if (frame != null)
                {
                    result["functionName"] = frame.FunctionName;
                    result["module"] = frame.Module;
                    result["fileName"] = DebugHelpers.TryGetFrameFileName(frame);
                    result["line"] = DebugHelpers.TryGetFrameLine(frame);
                    result["language"] = frame.Language;
                }
            }
            catch { }

            return McpToolResult.Success(result);
        }

        private static TimeSpan GetDebuggerWaitBreakTimeout(JObject args)
        {
            var timeoutMs = args.Value<int?>("time_out");
            if (!timeoutMs.HasValue || timeoutMs.Value <= 0)
                return WaitBreakTimeoutPadding;

            var totalMs = Math.Min(
                (long)timeoutMs.Value + (long)WaitBreakTimeoutPadding.TotalMilliseconds,
                int.MaxValue);
            return TimeSpan.FromMilliseconds(totalMs);
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

        private sealed class WaitBreakSignal
        {
            public WaitBreakSignal(string status, string eventName, string reasonCode)
            {
                Status = status;
                EventName = eventName;
                ReasonCode = reasonCode;
            }

            public string Status { get; }
            public string EventName { get; }
            public string ReasonCode { get; }
        }

        private sealed class DebuggerWaitRegistration
        {
            private readonly DebuggerEvents _debuggerEvents;
            private readonly TaskCompletionSource<WaitBreakSignal> _completion =
                new TaskCompletionSource<WaitBreakSignal>(TaskCreationOptions.RunContinuationsAsynchronously);
            private bool _subscribed;

            public DebuggerWaitRegistration(DebuggerEvents debuggerEvents)
            {
                _debuggerEvents = debuggerEvents ?? throw new ArgumentNullException(nameof(debuggerEvents));
            }

            public Task<WaitBreakSignal> WaitTask => _completion.Task;

            public void Subscribe()
            {
                if (_subscribed) return;

                _debuggerEvents.OnEnterBreakMode += OnEnterBreakMode;
                _debuggerEvents.OnEnterDesignMode += OnEnterDesignMode;
                _subscribed = true;
            }

            public void Unsubscribe()
            {
                if (!_subscribed) return;

                _debuggerEvents.OnEnterBreakMode -= OnEnterBreakMode;
                _debuggerEvents.OnEnterDesignMode -= OnEnterDesignMode;
                _subscribed = false;
            }

            public void Complete(string status)
            {
                _completion.TrySetResult(new WaitBreakSignal(status, null, null));
            }

            private void OnEnterBreakMode(dbgEventReason reason, ref dbgExecutionAction executionAction)
            {
                _completion.TrySetResult(new WaitBreakSignal("break", "OnEnterBreakMode", reason.ToString()));
            }

            private void OnEnterDesignMode(dbgEventReason reason)
            {
                _completion.TrySetResult(new WaitBreakSignal("stopped", "OnEnterDesignMode", reason.ToString()));
            }
        }
    }
}
