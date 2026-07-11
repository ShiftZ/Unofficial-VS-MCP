using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Debugger.Evaluation.IL;

namespace VsMcp.Debugger
{
    /// <summary>
    /// Entry point for debugger-engine events consumed by VS MCP features.
    /// Keep callbacks small: Concord invokes them on debugger-owned threads.
    /// </summary>
    public sealed class IdeDebuggerEventListener :
        IDkmRuntimeBreakpointConditionFailedNotification,
        IDkmRuntimeBreakpointHitWithErrorNotification,
        IDkmBreakpointHitWithErrorNotification
    {
        public IdeDebuggerEventListener()
        {
            DebuggerEventLog.Write("component.loaded", "component", "ide");
        }

        public void OnRuntimeBreakpointConditionFailed(
            DkmRuntimeBreakpoint runtimeBreakpoint,
            DkmThread thread,
            string errorMessage,
            DkmILFailureReason errorCode,
            DkmEventDescriptorS eventDescriptor)
        {
            DebuggerEventLog.Write(
                "breakpoint.conditionFailed.notification",
                "runtimeBreakpointId", GetId(runtimeBreakpoint),
                "threadId", thread?.UniqueId.ToString(),
                "errorCode", errorCode.ToString(),
                "message", errorMessage);
        }

        public void OnRuntimeBreakpointHitWithError(
            DkmRuntimeBreakpoint runtimeBreakpoint,
            DkmThread thread,
            bool hasException,
            DkmBreakpointMessageLevel level,
            string errorMessage,
            DkmEventDescriptorS eventDescriptor)
        {
            DebuggerEventLog.Write(
                "breakpoint.runtimeHitWithError.notification",
                "runtimeBreakpointId", GetId(runtimeBreakpoint),
                "threadId", thread?.UniqueId.ToString(),
                "hasException", hasException.ToString(),
                "level", level.ToString(),
                "message", errorMessage);
        }

        public void OnBreakpointHitWithError(
            DkmPendingBreakpoint pendingBreakpoint,
            DkmThread thread,
            bool hasException,
            DkmBreakpointMessageLevel level,
            string errorMessage,
            DkmEventDescriptorS eventDescriptor)
        {
            DebuggerEventLog.Write(
                "breakpoint.hitWithError.notification",
                "pendingBreakpointId", GetId(pendingBreakpoint),
                "threadId", thread?.UniqueId.ToString(),
                "hasException", hasException.ToString(),
                "level", level.ToString(),
                "message", errorMessage);
        }

        private static string GetId(DkmPendingBreakpoint breakpoint)
        {
            return breakpoint?.UniqueId.ToString();
        }

        private static string GetId(DkmRuntimeBreakpoint breakpoint)
        {
            return breakpoint?.UniqueId.ToString();
        }
    }

    public sealed class RuntimeDebuggerEventListener :
        IDkmRuntimeBreakpointConditionFailedReceived,
        IDkmRuntimeBreakpointHitWithErrorReceived,
        IDkmBreakpointHitWithErrorReceived
    {
        public RuntimeDebuggerEventListener()
        {
            DebuggerEventLog.Write("component.loaded", "component", "runtime");
        }

        public void OnRuntimeBreakpointConditionFailedReceived(
            DkmRuntimeBreakpoint runtimeBreakpoint,
            DkmThread thread,
            string errorMessage,
            DkmILFailureReason errorCode,
            DkmEventDescriptorS eventDescriptor)
        {
            DebuggerEventLog.Write(
                "breakpoint.conditionFailed",
                "runtimeBreakpointId", GetId(runtimeBreakpoint),
                "threadId", thread?.UniqueId.ToString(),
                "errorCode", errorCode.ToString(),
                "message", errorMessage);
        }

        public void OnRuntimeBreakpointHitWithErrorReceived(
            DkmRuntimeBreakpoint runtimeBreakpoint,
            DkmThread thread,
            bool hasException,
            DkmBreakpointMessageLevel level,
            string errorMessage,
            DkmEventDescriptorS eventDescriptor)
        {
            DebuggerEventLog.Write(
                "breakpoint.runtimeHitWithError",
                "runtimeBreakpointId", GetId(runtimeBreakpoint),
                "threadId", thread?.UniqueId.ToString(),
                "hasException", hasException.ToString(),
                "level", level.ToString(),
                "message", errorMessage);
        }

        public void OnBreakpointHitWithErrorReceived(
            DkmPendingBreakpoint pendingBreakpoint,
            DkmThread thread,
            bool hasException,
            DkmBreakpointMessageLevel level,
            string errorMessage,
            DkmEventDescriptorS eventDescriptor)
        {
            DebuggerEventLog.Write(
                "breakpoint.hitWithError",
                "pendingBreakpointId", GetId(pendingBreakpoint),
                "threadId", thread?.UniqueId.ToString(),
                "hasException", hasException.ToString(),
                "level", level.ToString(),
                "message", errorMessage);
        }

        private static string GetId(DkmPendingBreakpoint breakpoint)
        {
            return breakpoint?.UniqueId.ToString();
        }

        private static string GetId(DkmRuntimeBreakpoint breakpoint)
        {
            return breakpoint?.UniqueId.ToString();
        }
    }
}
