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
        private static readonly TimeSpan DebugStartModeChangeTimeout = TimeSpan.FromSeconds(5);

        public static void Register(McpToolRegistry registry, VsServiceAccessor accessor)
        {
            registry.Register(
                new McpToolDefinition(
                    "debug_start",
                    "F5: start the startup project WITH the debugger attached (breakpoints hit, exceptions break into VS). Starts only from Design mode; otherwise leaves the current debug session untouched. Use this when the user wants to debug. For run-without-debugging (Ctrl+F5), use debug_start_without_debugging.",
                    SchemaBuilder.Create()
                        .AddBoolean("wait_mode_change", "If true, wait up to 5 seconds for VS to enter Run mode after starting.")
                        .Build()),
                args => DebugStartAsync(accessor, args));

            registry.Register(
                new McpToolDefinition(
                    "debug_start_in_break",
                    "Start debugging from Design mode and step into the first executable statement. Returns an error if Visual Studio is already in Running or Break mode.",
                    SchemaBuilder.Empty()),
                args => DebugStartInBreakAsync(accessor));

            registry.Register(
                new McpToolDefinition(
                    "debug_wait_break",
                    "Wait until the debugger stops or the timeout expires. This is event-driven: it listens for VS debugger break/design-mode events, does not continue execution, and returns the stop reason plus current break context when available. The time_out parameter is in milliseconds.",
                    SchemaBuilder.Create()
                        .AddInteger("time_out", "Timeout in milliseconds", required: true)
                        .Build()),
                args => DebugWaitBreakAsync(accessor, args),
                GetDebugWaitBreakTimeout);

            registry.Register(
                new McpToolDefinition(
                    "debug_start_wait_break",
                    "F5: start the startup project WITH the debugger attached, then wait until the debugger breaks/stops or time_out expires. Starts only from Design mode; otherwise leaves the current debug session untouched. Equivalent to debug_start followed by debug_wait_break.",
                    SchemaBuilder.Create()
                        .AddInteger("time_out", "Timeout in milliseconds", required: true)
                        .Build()),
                args => DebugStartWaitBreakAsync(accessor, args),
                GetDebugStartWaitBreakTimeout);

            registry.Register(
                new McpToolDefinition(
                    "debug_continue_wait_break",
                    "Continue execution from a debugger break, then wait until the debugger breaks/stops again or time_out expires. Equivalent to debug_continue followed by debug_wait_break.",
                    SchemaBuilder.Create()
                        .AddInteger("time_out", "Timeout in milliseconds", required: true)
                        .Build()),
                args => DebugContinueWaitBreakAsync(accessor, args),
                GetDebugWaitBreakTimeout);

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
                    "debug_switch_frame",
                    "Switch the current stack frame by frame index. Optionally switch to a thread first by thread ID. Use frameIndex values returned by debug_get_callstack or thread_get_callstack.",
                    SchemaBuilder.Create()
                        .AddInteger("frameIndex", "Zero-based frame index returned by debug_get_callstack or thread_get_callstack", required: true)
                        .AddInteger("threadId", "Optional thread ID. When provided, switches to this thread before switching frames.")
                        .Build()),
                args => DebugSwitchFrameAsync(accessor, args));

            registry.Register(
                new McpToolDefinition(
                    "debug_switch_process",
                    "Switch the debugger's active process by PID. Use processId values returned by process_list_debugged. The active process controls debugger data such as current threads, stack frames, locals, and expression evaluation.",
                    SchemaBuilder.Create()
                        .AddInteger("processId", "PID of the debugged process to make active", required: true)
                        .Build()),
                args => DebugSwitchProcessAsync(accessor, args));

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
                    "debug_evaluate",
                    "Read-only evaluate an expression in the current debug context (currently selected thread and stack frame only). No side effects (no assignments, no method calls that mutate state). Must be in break mode. Use this to inspect variable values. For expressions WITH side effects (assignments, mutating method calls) use immediate_execute. To persist an expression that re-evaluates across breaks, use watch_add.",
                    SchemaBuilder.Create()
                        .AddString("expression", "The expression to evaluate", required: true)
                        .Build()),
                args => DebugEvaluateAsync(accessor, args));
        }

        private static Task<McpToolResult> DebugStartAsync(VsServiceAccessor accessor, JObject args)
        {
            var waitModeChange = args.Value<bool?>("wait_mode_change") == true;
            return DebugStartAsync(accessor, waitModeChange);
        }

        private static async Task<McpToolResult> DebugStartInBreakAsync(VsServiceAccessor accessor)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                var currentMode = dte.Debugger.CurrentMode;
                if (currentMode != dbgDebugMode.dbgDesignMode)
                    return McpToolResult.Error(
                        $"Visual Studio is already in {GetDebuggerModeName(currentMode)} mode");

                dte.Debugger.StepInto(false);
                return McpToolResult.Success("Debugging started in Break mode");
            });
        }

        private static async Task<McpToolResult> DebugStartAsync(VsServiceAccessor accessor, bool waitModeChange)
        {
            if (!waitModeChange)
            {
                return await accessor.RunOnUIThreadAsync(() =>
                {
                    var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                        .Run(() => accessor.GetDteAsync());

                    var currentMode = dte.Debugger.CurrentMode;
                    if (currentMode != dbgDebugMode.dbgDesignMode)
                        return McpToolResult.Success($"Debugger is already in {GetDebuggerModeName(currentMode)} mode");

                    dte.Solution.SolutionBuild.Debug();
                    return McpToolResult.Success("Debugging started");
                });
            }

            var startResult = await DebugStartAndWaitRunAsync(accessor);
            return await CreateDebuggerSignalResultAsync(accessor, startResult.Signal, startResult.ElapsedMs);
        }

        private static async Task<McpToolResult> DebugStartWaitBreakAsync(VsServiceAccessor accessor, JObject args)
        {
            var startResult = await DebugStartAndWaitRunAsync(accessor);
            if (startResult.Signal.Status == "timeout")
                return await CreateDebuggerSignalResultAsync(accessor, startResult.Signal, startResult.ElapsedMs);

            return await DebugWaitBreakAsync(accessor, args);
        }

        private static async Task<McpToolResult> DebugContinueWaitBreakAsync(VsServiceAccessor accessor, JObject args)
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

                    if (dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                        return null;

                    var waitRegistration = new DebuggerWaitRegistration(dte.Events.DebuggerEvents);
                    waitRegistration.Subscribe();
                    dte.Debugger.Go(false);
                    return waitRegistration;
                });

                if (registration == null)
                    return McpToolResult.Error("Debugger must be in Break mode to continue");

                var delayTask = Task.Delay(timeoutMs.Value);
                var completed = await Task.WhenAny(registration.WaitTask, delayTask).ConfigureAwait(false);
                var signal = completed == registration.WaitTask || registration.WaitTask.IsCompleted
                    ? await registration.WaitTask.ConfigureAwait(false)
                    : new DebuggerSignal("timeout", null, null);

                stopwatch.Stop();
                return await CreateDebuggerSignalResultAsync(accessor, signal, stopwatch.ElapsedMilliseconds, includeBreakContext: true);
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

        private static async Task<DebuggerWaitResult> DebugStartAndWaitRunAsync(VsServiceAccessor accessor)
        {
            var stopwatch = Stopwatch.StartNew();
            DebuggerRunRegistration registration = null;
            DebuggerSignal currentSignal = null;

            try
            {
                await accessor.RunOnUIThreadAsync(() =>
                {
                    var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                        .Run(() => accessor.GetDteAsync());

                    var currentMode = dte.Debugger.CurrentMode;
                    if (currentMode != dbgDebugMode.dbgDesignMode)
                    {
                        currentSignal = new DebuggerSignal(GetDebuggerStatusName(currentMode), null, null);
                        return;
                    }

                    var debuggerEvents = dte.Events.DebuggerEvents;
                    registration = new DebuggerRunRegistration(debuggerEvents);
                    registration.Subscribe();

                    dte.Solution.SolutionBuild.Debug();

                    switch (dte.Debugger.CurrentMode)
                    {
                        case dbgDebugMode.dbgRunMode:
                            registration.Complete("running");
                            break;
                        case dbgDebugMode.dbgBreakMode:
                            registration.Complete("break");
                            break;
                    }
                });

                if (currentSignal != null)
                {
                    stopwatch.Stop();
                    return new DebuggerWaitResult(currentSignal, stopwatch.ElapsedMilliseconds);
                }

                var delayTask = Task.Delay(DebugStartModeChangeTimeout);
#pragma warning disable VSTHRD003 // Event-backed TaskCompletionSource; no UI-thread work is awaited here.
                var completed = await Task.WhenAny(registration.WaitTask, delayTask);
                var signal = completed == registration.WaitTask || registration.WaitTask.IsCompleted
                    ? await registration.WaitTask
                    : new DebuggerSignal("timeout", null, null);
#pragma warning restore VSTHRD003

                stopwatch.Stop();
                return new DebuggerWaitResult(signal, stopwatch.ElapsedMilliseconds);
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
                var frameIndex = 0;

                foreach (StackFrame frame in thread.StackFrames)
                {
                    try
                    {
                        frames.Add(new
                        {
                            frameIndex,
                            functionName = frame.FunctionName,
                            module = frame.Module,
                            fileName = DebugHelpers.TryGetFrameFileName(frame),
                            line = DebugHelpers.TryGetFrameLine(frame),
                            language = frame.Language
                        });
                    }
                    catch { }

                    frameIndex++;
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

        private static async Task<McpToolResult> DebugSwitchFrameAsync(VsServiceAccessor accessor, JObject args)
        {
            var frameIndex = args.Value<int?>("frameIndex");
            if (!frameIndex.HasValue)
                return McpToolResult.Error("Parameter 'frameIndex' is required");

            if (frameIndex.Value < 0)
                return McpToolResult.Error("Parameter 'frameIndex' must be zero or greater");

            var threadId = args.Value<int?>("threadId");

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                    return McpToolResult.Error("Debugger must be in Break mode to switch frames");

                Thread thread;
                if (threadId.HasValue)
                {
                    thread = DebugHelpers.FindThread(dte.Debugger, threadId.Value);
                    if (thread == null)
                        return McpToolResult.Error($"Thread with ID {threadId.Value} not found");

                    dte.Debugger.CurrentThread = thread;
                }
                else
                {
                    thread = dte.Debugger.CurrentThread;
                    if (thread == null)
                        return McpToolResult.Error("No current thread is available");
                }

                StackFrame targetFrame = null;
                var currentIndex = 0;
                foreach (StackFrame frame in thread.StackFrames)
                {
                    if (currentIndex == frameIndex.Value)
                    {
                        targetFrame = frame;
                        break;
                    }

                    currentIndex++;
                }

                if (targetFrame == null)
                    return McpToolResult.Error($"Frame index {frameIndex.Value} not found on thread {thread.ID}");

                dte.Debugger.CurrentStackFrame = targetFrame;

                return McpToolResult.Success(new
                {
                    message = $"Switched to frame {frameIndex.Value} on thread {thread.ID}",
                    threadId = thread.ID,
                    threadName = thread.Name,
                    frameIndex = frameIndex.Value,
                    functionName = targetFrame.FunctionName,
                    module = targetFrame.Module,
                    fileName = DebugHelpers.TryGetFrameFileName(targetFrame),
                    line = DebugHelpers.TryGetFrameLine(targetFrame),
                    language = targetFrame.Language
                });
            });
        }

        private static async Task<McpToolResult> DebugSwitchProcessAsync(VsServiceAccessor accessor, JObject args)
        {
            var processId = args.Value<int?>("processId");
            if (!processId.HasValue)
                return McpToolResult.Error("Parameter 'processId' is required");

            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
                    return McpToolResult.Error("Debugger is not running");

                Process targetProcess = null;
                foreach (Process process in dte.Debugger.DebuggedProcesses)
                {
                    try
                    {
                        if (process.ProcessID == processId.Value)
                        {
                            targetProcess = process;
                            break;
                        }
                    }
                    catch { }
                }

                if (targetProcess == null)
                    return McpToolResult.Error($"No debugged process found with PID {processId.Value}");

                var previousProcess = dte.Debugger.CurrentProcess;
                dte.Debugger.CurrentProcess = targetProcess;

                var result = new Dictionary<string, object>
                {
                    ["message"] = $"Switched active debug process to {targetProcess.ProcessID} ({targetProcess.Name})",
                    ["processId"] = targetProcess.ProcessID,
                    ["processName"] = targetProcess.Name,
                    ["previousProcessId"] = previousProcess?.ProcessID,
                    ["previousProcessName"] = previousProcess?.Name,
                    ["mode"] = GetDebuggerModeName(dte.Debugger.CurrentMode)
                };

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

                return McpToolResult.Success(result);
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

        private static async Task<McpToolResult> DebugWaitBreakAsync(VsServiceAccessor accessor, JObject args)
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
                    : new DebuggerSignal("timeout", null, null);

                stopwatch.Stop();

                return await CreateDebuggerSignalResultAsync(accessor, signal, stopwatch.ElapsedMilliseconds, includeBreakContext: true);
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

                var evaluation = DebugHelpers.TryEvaluateExpressionDetailed(dte.Debugger, expression);
                if (!evaluation.Succeeded)
                    return McpToolResult.Error(
                        "Expression: " + expression + "\n" + evaluation.GetFailureSummary());

                var evalResult = evaluation.Expression;
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
                    threadId = evaluation.Context?.ThreadId,
                    frame = evaluation.Context?.FunctionName,
                    language = evaluation.Context?.Language
                });
            });
        }

        private static async Task<McpToolResult> CreateDebuggerSignalResultAsync(
            VsServiceAccessor accessor,
            DebuggerSignal signal,
            long elapsedMs,
            bool includeBreakContext = false)
        {
            return await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                return CreateDebuggerSignalResult(dte, signal, elapsedMs, includeBreakContext);
            });
        }

        private static McpToolResult CreateDebuggerSignalResult(DTE2 dte, DebuggerSignal signal, long elapsedMs, bool includeBreakContext)
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

            try
            {
                var process = dte.Debugger.CurrentProcess;
                if (process != null)
                {
                    result["processId"] = process.ProcessID;
                    result["processName"] = process.Name;
                }
            }
            catch { }

            if (!includeBreakContext || currentMode != dbgDebugMode.dbgBreakMode)
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

        private static TimeSpan GetDebugWaitBreakTimeout(JObject args)
        {
            var timeoutMs = args.Value<int?>("time_out");
            if (!timeoutMs.HasValue || timeoutMs.Value <= 0)
                return WaitBreakTimeoutPadding;

            var totalMs = Math.Min(
                (long)timeoutMs.Value + (long)WaitBreakTimeoutPadding.TotalMilliseconds,
                int.MaxValue);
            return TimeSpan.FromMilliseconds(totalMs);
        }

        private static TimeSpan GetDebugStartWaitBreakTimeout(JObject args)
        {
            var waitBreakTimeout = GetDebugWaitBreakTimeout(args);
            var totalMs = Math.Min(
                (long)waitBreakTimeout.TotalMilliseconds + (long)DebugStartModeChangeTimeout.TotalMilliseconds,
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

        private static string GetDebuggerStatusName(dbgDebugMode mode)
        {
            switch (mode)
            {
                case dbgDebugMode.dbgRunMode:
                    return "running";
                case dbgDebugMode.dbgBreakMode:
                    return "break";
                case dbgDebugMode.dbgDesignMode:
                default:
                    return "stopped";
            }
        }

        private sealed class DebuggerWaitResult
        {
            public DebuggerWaitResult(DebuggerSignal signal, long elapsedMs)
            {
                Signal = signal ?? throw new ArgumentNullException(nameof(signal));
                ElapsedMs = elapsedMs;
            }

            public DebuggerSignal Signal { get; }
            public long ElapsedMs { get; }
        }

        private sealed class DebuggerSignal
        {
            public DebuggerSignal(string status, string eventName, string reasonCode)
            {
                Status = status;
                EventName = eventName;
                ReasonCode = reasonCode;
            }

            public string Status { get; }
            public string EventName { get; }
            public string ReasonCode { get; }
        }

        private sealed class DebuggerRunRegistration
        {
            private readonly DebuggerEvents _debuggerEvents;
            private readonly TaskCompletionSource<DebuggerSignal> _completion =
                new TaskCompletionSource<DebuggerSignal>(TaskCreationOptions.RunContinuationsAsynchronously);
            private bool _subscribed;

            public DebuggerRunRegistration(DebuggerEvents debuggerEvents)
            {
                _debuggerEvents = debuggerEvents ?? throw new ArgumentNullException(nameof(debuggerEvents));
            }

            public Task<DebuggerSignal> WaitTask => _completion.Task;

            public void Subscribe()
            {
                if (_subscribed) return;

                _debuggerEvents.OnEnterRunMode += OnEnterRunMode;
                _subscribed = true;
            }

            public void Unsubscribe()
            {
                if (!_subscribed) return;

                _debuggerEvents.OnEnterRunMode -= OnEnterRunMode;
                _subscribed = false;
            }

            public void Complete(string status)
            {
                _completion.TrySetResult(new DebuggerSignal(status, null, null));
            }

            private void OnEnterRunMode(dbgEventReason reason)
            {
                _completion.TrySetResult(new DebuggerSignal("running", "OnEnterRunMode", reason.ToString()));
            }
        }

        private sealed class DebuggerWaitRegistration
        {
            private readonly DebuggerEvents _debuggerEvents;
            private readonly TaskCompletionSource<DebuggerSignal> _completion =
                new TaskCompletionSource<DebuggerSignal>(TaskCreationOptions.RunContinuationsAsynchronously);
            private bool _subscribed;

            public DebuggerWaitRegistration(DebuggerEvents debuggerEvents)
            {
                _debuggerEvents = debuggerEvents ?? throw new ArgumentNullException(nameof(debuggerEvents));
            }

            public Task<DebuggerSignal> WaitTask => _completion.Task;

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
                _completion.TrySetResult(new DebuggerSignal(status, null, null));
            }

            private void OnEnterBreakMode(dbgEventReason reason, ref dbgExecutionAction executionAction)
            {
                _completion.TrySetResult(new DebuggerSignal("break", "OnEnterBreakMode", reason.ToString()));
            }

            private void OnEnterDesignMode(dbgEventReason reason)
            {
                _completion.TrySetResult(new DebuggerSignal("stopped", "OnEnterDesignMode", reason.ToString()));
            }
        }
    }
}
