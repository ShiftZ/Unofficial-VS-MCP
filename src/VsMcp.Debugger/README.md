# VS MCP debugger integration

This assembly contains Concord/DKM components. It is intentionally separate
from the VSPackage so debugger event handling can evolve without adding DKM
dependencies to the MCP server itself.

`IdeDebuggerEventListener` runs in `devenv.exe` and receives debugger-client
notifications. `RuntimeDebuggerEventListener` is a server-side component for
events that DKM only permits below the client boundary.

The initial event transport is a JSON Lines file:

```text
%LOCALAPPDATA%\VsMcp\Debugger\events.jsonl
```

Each record includes the host process and PID so execution placement is
observable. Callbacks must remain short and must not call the MCP HTTP server
or Visual Studio automation synchronously.

For native C++ condition evaluation failures, the useful client event is
`IDkmBreakpointHitWithErrorNotification`. Its message level is
`ConditionError`, and its message contains the evaluator error. The
`IDkmRuntimeBreakpointConditionFailed*` interfaces describe IL-query failures
and did not fire for the native expression-evaluator failure tested here.
