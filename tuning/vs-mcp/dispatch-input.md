# vs-mcp Iter 1 dispatch input

You are an executor evaluating vs-mcp's tool catalog with a blank slate.

You will **NOT invoke any MCP tools**. This is a **dry-run**: for each scenario, declare what you would call, but do not actually call it. The dry-run evaluates whether the descriptions alone are sufficient for correct selection.

## Background

vs-mcp is a Visual Studio automation MCP server (115 tools). Some tools target the Windows UI Automation surface (the desktop app being debugged in VS), others target a browser DOM (Chrome/Edge/Firefox via CDP/RDP). You will see both clusters mixed together.

## Tool catalog

The catalog is shown below in mixed alphabetical order. Tools shown with a **full description and parameters** are in scope for evaluation; tools shown with only `name: one-line gist` are also part of the server but not under evaluation — they exist so refusal pressure is realistic (you should still recognize when the right tool is one of them).

Some descriptions reference other tools by name (e.g. "use X instead when ..."). Treat those as part of the description's signal.

---

### Tools (alphabetical)

**breakpoint_enable**: Enable or disable a breakpoint at a specific file and line.

**breakpoint_list**: List all breakpoints in the current solution.

**breakpoint_remove**: Remove a breakpoint at a specific file and line.

**breakpoint_set**: Set a breakpoint at a file+line or by function name.

**build_configuration**: Get or set the active solution build configuration and platform.

**build_project**
Description: Build a specific project.
Parameters:
- `name` (string, required): Project name to build

**build_solution**
Description: Build the entire solution in Visual Studio. Use this instead of running MSBuild.exe from the command line.
Parameters: (none)

**clean**: Clean the solution build output.

**code_find_references**
Description: Find all references of a symbol at the specified position. Triggers VS Find All References window.
Parameters:
- `path` (string, required): Full path to the source file
- `line` (integer, required): Line number (1-based)
- `column` (integer, required): Column number (1-based)

**code_goto_definition**
Description: Navigate to the definition of a symbol at the specified position. Opens the file and returns the definition location.
Parameters:
- `path` (string, required), `line` (int, required), `column` (int, required)

**code_goto_implementation**: Navigate to the implementation of an interface or abstract member.

**console_get_info**: Get console info (buffer size, cursor position) of a debugged console app.

**console_read**
Description: Read the console output buffer of a debugged console application. The output is read from the console window (conhost.exe/Windows Terminal), not the VS Output pane.
Parameters:
- `tail` (integer): Number of lines to read from the end (default: 200)
- `processId` (integer): PID of the debugged process (default: first)

**console_send**: Send input/keys to the console of a debugged console application.

**debug_attach**
Description: Attach the debugger to a running process by name or PID.
Parameters:
- `processName` (string), `processId` (integer)

**debug_break**
Description: Break (pause) the debugger at the current execution point.
Parameters: (none)

**debug_continue**
Description: Continue (resume) execution after a breakpoint or break.
Parameters: (none)

**debug_evaluate**
Description: Read-only evaluate an expression in the current debug context — no side effects (no assignments, no method calls that mutate state). Must be in break mode. Use this to inspect variable values. For expressions WITH side effects (assignments, mutating method calls) use immediate_execute. To persist an expression that re-evaluates across breaks, use watch_add.
Parameters:
- `expression` (string, required): The expression to evaluate

**debug_get_callstack**: Get the current call stack of the active thread.

**debug_get_locals**: Get the local variables in the current stack frame.

**debug_get_mode**: Get the current debugger mode (Design, Running, or Break).

**debug_get_threads**: Get all threads in the current debug session.

**debug_restart**
Description: Restart debugging the current session.
Parameters: (none)

**debug_start**
Description: F5: start the startup project WITH the debugger attached (breakpoints hit, exceptions break into VS). Use this when the user wants to debug. For run-without-debugging (Ctrl+F5), use debug_start_without_debugging.
Parameters: (none)

**debug_start_without_debugging**
Description: Ctrl+F5: run the startup project WITHOUT the debugger attached (breakpoints are ignored, exceptions do not break into VS). Use this when the user wants to just run the app. For debugging (F5) with breakpoints, use debug_start.
Parameters: (none)

**debug_step**: Step through code. direction: over (F10) / into (F11) / out (Shift+F11).

**debug_stop**
Description: Stop the current debug session normally (Shift+F5). Detaches and terminates the debuggee like clicking the VS Stop button. This is the default 'stop debugging' action. To detach without terminating, use process_detach. To force-kill a specific debugged process, use process_terminate.
Parameters: (none)

**diagnostics_binding_errors**: Extract XAML/WPF binding errors from the Debug output pane.

**edit_approve**
Description: Approve a pending edit and apply the changes to the file.
Parameters:
- `pendingId` (string, required)

**edit_list_pending**: List all pending edit previews with their status.

**edit_preview**
Description: Show a diff preview of proposed changes in VS and create a pending edit for approval. Use oldText+newText for partial replacement (like file_edit), or content for full replacement (like file_write).
Parameters:
- `path` (string, required)
- `oldText` (string), `newText` (string), `content` (string)

**edit_reject**
Description: Reject a pending edit and discard the changes.
Parameters:
- `pendingId` (string, required)

**error_list_get**
Description: Get all items currently shown in the Visual Studio Error List window — includes errors/warnings/messages from any source: build, IntelliSense, analyzers, XAML, etc. Use this when the user wants 'everything in the Error List'. For build-only errors collected after a build, prefer get_build_errors.
Parameters:
- `severity` (string): 'error', 'warning', 'message', or 'all' (default: 'all')

**exception_settings_get**: Get exception break settings.

**exception_settings_set**: Configure when to break on a specific exception type.

**execute_command**
Description: FALLBACK: execute an arbitrary Visual Studio command by name (e.g. 'Edit.FormatDocument', 'Build.BuildSolution'). Prefer a dedicated tool when one exists — for builds use build_solution/build_project, for debugging use debug_start/debug_stop, for files use file_open, etc. Only use execute_command when no dedicated tool covers the action the user wants.
Parameters:
- `command` (string, required), `args` (string)

**file_close**: Close a file in the editor.

**file_edit**
Description: Edit a file by replacing a specific text occurrence with new text.
Parameters:
- `path`, `oldText`, `newText` (all string, required)

**file_open**
Description: Open a file in the Visual Studio editor.
Parameters:
- `path` (string, required), `line` (integer, optional)

**file_read**
Description: Read the contents of a file at a given path (does not need to be open in the editor). Can optionally read specific line ranges. For information about the document currently focused in the VS editor (without specifying a path), use get_active_document.
Parameters:
- `path` (string, required), `startLine`, `endLine`

**file_write**
Description: DESTRUCTIVE: overwrite a file's entire contents (no diff, no approval). For partial replacement use file_edit; to show the user a diff preview that requires explicit approval before applying, use edit_preview with the 'content' parameter.
Parameters:
- `path` (string, required), `content` (string, required)

**find_in_files**
Description: Text search across files in the solution directory (literal text or regex). Searches the file system directly (fast, does not block VS UI). Skips bin/obj/.vs/packages/node_modules. Not symbol-aware — for finding references of a symbol use code_find_references; for navigating to a definition use code_goto_definition.
Parameters:
- `query` (string, required), `filePattern`, `matchCase`, `useRegex`, `maxResults`

**get_active_document**
Description: Get information about the document currently focused in the VS editor (path, language, dirty state, caret position). Use this to answer 'what file is the user looking at right now'. To read the contents of a specific file by path, use file_read.
Parameters: (none)

**get_build_errors**
Description: Get build-produced errors and warnings (MSBuild/compiler output) from the Visual Studio Error List. Call this right after build_solution or build_project to check the build result. For the full Error List including IntelliSense and analyzer items, use error_list_get instead.
Parameters: (none)

**get_help**: Get a categorized list of all available vs-mcp tools.

**get_status**: Get the current Visual Studio status (solution, active doc, debugger mode).

**immediate_execute**
Description: Execute an expression WITH side effects in the debugger context (like the VS Immediate Window) — assignments, mutating method calls, etc. Must be in break mode. For read-only inspection of a value without side effects, prefer debug_evaluate. To persist an expression that re-evaluates each break, use watch_add.
Parameters:
- `expression` (string, required), `timeout` (integer)

**memory_read**: Read memory bytes given address or variable.

**module_list**: List all loaded modules in the current debug session.

**nuget_install**: Install a NuGet package into a project.

**nuget_list**: List installed NuGet packages for a specific project.

**nuget_search**: Search for NuGet packages on NuGet.org.

**nuget_uninstall**: Remove a NuGet package from a project.

**nuget_update**: Update a NuGet package to a specific version.

**output_clear**: Clear the content of a Visual Studio Output window pane.

**output_read**
Description: Read the content of a Visual Studio Output window pane (Build / Debug / Test / etc. — the panes shown in VS's Output tool window). Supports localized pane names. Call without pane parameter to list available panes. Returns the last 'tail' lines by default (200). Use tail=0 to read all content. Use 'pattern' to filter lines by regex. For the stdout/stderr of a debugged console application (the actual console window, not the VS Output pane), use console_read instead.
Parameters:
- `pane` (string), `tail` (integer), `pattern` (string)

**output_write**
Description: Write text to a Visual Studio Output window pane.
Parameters:
- `text` (string, required), `pane` (string)

**parallel_stacks**: Get all threads' call stacks in a tree view (Parallel Stacks window).

**parallel_tasks_list**: List TPL task information (best-effort, break mode).

**parallel_watch**: Evaluate the same expression on all threads and compare.

**process_detach**
Description: Detach the debugger from a specific debugged process WITHOUT terminating it — the process keeps running freely. Use this when the user wants to release a debug attachment but keep the app running. For ending the whole debug session normally use debug_stop; to force-kill a process use process_terminate.
Parameters:
- `processId` (integer, required): PID of the process to detach from

**process_list_debugged**
Description: List all processes currently being debugged.
Parameters: (none)

**process_list_local**
Description: List local processes available for attaching the debugger.
Parameters:
- `filter` (string): Optional name filter

**process_terminate**
Description: DESTRUCTIVE: force-kill a specific debugged process by PID. This is NOT the normal 'stop debugging' action — for that, use debug_stop. Only use process_terminate when the user explicitly wants to kill a specific PID, or when debug_stop is not appropriate (e.g. unresponsive process). To detach the debugger without killing, use process_detach.
Parameters:
- `processId` (integer, required): PID of the process to terminate

**project_add_file**: Add an existing file to a project.

**project_add_reference**: Add a project-to-project reference.

**project_info**: Get detailed information about a specific project.

**project_list**: List all projects in the current solution.

**project_remove_file**: Remove a file from a project.

**project_remove_reference**: Remove a reference from a project.

**rebuild**: Clean and rebuild the entire solution.

**register_get**: Get the value of a specific CPU register.

**register_list**: Get values of common CPU registers.

**solution_add_project**: Add an existing project to the current solution.

**solution_close**: Close the current solution.

**solution_info**: Get information about the currently open solution.

**solution_open**: Open a solution or project file in VS.

**solution_remove_project**: Remove a project from the current solution.

**test_discover**: Discover all tests in the solution or a specific project.

**test_results**: Get detailed results from the last test run.

**test_run**: Run tests and get results.

**thread_get_callstack**: Get the call stack of a specific thread by ID.

**thread_set_frozen**: Freeze or thaw a thread.

**thread_switch**: Switch the active thread by thread ID.

**ui_capture_region**
Description: [Windows UIA — desktop app being debugged] Capture a screenshot of a specific region of the debugged application's window. For web pages use web_screenshot instead.
Parameters:
- `x`, `y`, `width`, `height` (all int, required)

**ui_capture_window**
Description: [Windows UIA — desktop app being debugged] Capture a screenshot of the debugged application's main window as a base64 PNG image. For web pages use web_screenshot instead.
Parameters: (none)

**ui_click**
Description: [Windows UIA — desktop app being debugged] Click a UI element in a Win32/WPF/WinForms application by AutomationId, Name, or screen coordinates. When an element is given, InvokePattern is tried first (no cursor movement). For physical clicks (coordinates or pattern-less elements), the cursor is restored to its previous position by default. For clicking DOM elements in a browser page use web_element_click (CSS selector) instead.
Parameters:
- `automationId` (string), `name` (string), `x` (int), `y` (int), `waitMs` (int), `restoreCursor` (bool), `blockInput` (bool)

**ui_double_click**
Description: [Windows UIA — desktop app being debugged] Double-click a UI element in a desktop app by AutomationId, Name, or screen coordinates. Always uses physical mouse events; cursor is restored to its previous position by default. Browser DOM has no double-click helper — use web_js_execute with dispatchEvent('dblclick').
Parameters:
- `automationId`, `name`, `x`, `y`, `waitMs`, `restoreCursor`, `blockInput`

**ui_drag**
Description: [Windows UIA — desktop app being debugged] Perform a drag-and-drop operation in a desktop app from start coordinates to end coordinates. Cursor is restored to its previous position after the drag completes by default. For browser DOM drag use web_js_execute with synthetic drag events.
Parameters:
- `startX`, `startY`, `endX`, `endY` (all int, required), `steps`, `delayMs`, `restoreCursor`, `blockInput`

**ui_find_elements**
Description: [Windows UIA — desktop app being debugged] Find UI elements matching specified criteria (Name / AutomationId / ClassName / ControlType) in the debugged desktop application. String fields support match modes: 'exact' (default), 'contains' (case-insensitive substring), and 'regex' (case-insensitive). Use 'hasPattern' to require supported UIA patterns (invoke, toggle, select, setvalue, expand). Use 'ancestorAutomationId' to scope the search to the descendants of a specific element. For browser DOM elements use web_dom_query (CSS selectors) instead.
Parameters:
- `name`, `nameMatch`, `automationId`, `automationIdMatch`, `className`, `classNameMatch`, `controlType`, `hasPattern`, `ancestorAutomationId`, `maxResults`

**ui_get_element**
Description: [Windows UIA — desktop app being debugged] Get detailed properties of a specific UI element by its AutomationId. For browser DOM elements use web_dom_query with returnType='attributes'.
Parameters:
- `automationId` (string, required)

**ui_get_tree**
Description: [Windows UIA — desktop app being debugged] Get the raw UI element tree of the debugged application's main window. Prefer ui_snapshot unless you specifically need the unpruned tree. For web pages use web_dom_get instead.
Parameters:
- `depth`, `maxChildren`, `maxElements`

**ui_invoke**
Description: [Windows UIA — desktop app being debugged] Invoke the default action on a UI element (e.g. click a WPF/WinForms button) using InvokePattern. For browser DOM elements use web_element_click instead.
Parameters:
- `automationId` (string, required)

**ui_mouse_wheel**
Description: [Windows UIA — desktop app being debugged] Scroll the mouse wheel over a UI element or screen coordinates in a desktop app. Specify the position by AutomationId, Name, or x/y coordinates. ... For scrolling a browser page use web_js_execute with window.scrollBy.
Parameters:
- `automationId`, `name`, `x`, `y`, `clicks`, `horizontal`, `usePattern`, `restoreCursor`, `blockInput`, `waitMs`

**ui_right_click**
Description: [Windows UIA — desktop app being debugged] Right-click a UI element in a desktop app by AutomationId, Name, or screen coordinates to open context menus. Always uses physical mouse events; cursor is restored to its previous position by default. Browser DOM has no right-click helper — use web_js_execute with dispatchEvent('contextmenu').
Parameters:
- `automationId`, `name`, `x`, `y`, `waitMs`, `restoreCursor`, `blockInput`

**ui_send_keys**
Description: [Windows UIA — desktop app being debugged] Send keyboard input via Win32 SendInput to the debugged desktop application's foreground window. Use 'keys' for key combinations (e.g. 'ctrl+f', 'alt+f4', 'shift+ctrl+s', 'enter', 'f5') or 'text' to type a string of characters. ... For typing into a browser page use web_element_set_value or web_js_execute with KeyboardEvent.
Parameters:
- `keys` (string), `text` (string), `waitMs` (integer)

**ui_set_value**
Description: [Windows UIA — desktop app being debugged] Set the value of a UI element (e.g. WPF/WinForms text input) using ValuePattern. For setting an <input> value in a browser page use web_element_set_value (CSS selector) instead.
Parameters:
- `automationId` (string, required), `value` (string, required)

**ui_snapshot**
Description: [Windows UIA — desktop app being debugged] Capture a compact semantic snapshot of the debugged application's main window in a single call. Returns a pruned UI Automation tree (omits invisible/boring nodes, includes actionable patterns, state flags, rect, and focused element) plus an optional screenshot. Optimized for autonomous exploration and LLM-driven UI testing; prefer this over ui_get_tree + ui_capture_window. For web pages use web_dom_get + web_screenshot instead.
Parameters:
- `depth`, `maxElements`, `includeScreenshot`, `includeOffscreen`, `ancestorAutomationId`

**ui_wait_for_element**
Description: [Windows UIA — desktop app being debugged] Wait until a UI element matching the given criteria reaches the specified state. ... States: 'appears' (default), 'disappears', 'enabled', 'focused'. Use this instead of sleeping after triggering a UI action. Browser DOM has no equivalent wait helper — use web_js_execute with a polling expression.
Parameters:
- `name`, `automationId`, `className`, `controlType`, `state`, `timeoutMs`, `pollIntervalMs`

**ui_wait_idle**
Description: [Windows UIA — desktop app being debugged] Wait until the UI Automation tree stops changing for a quiet period. Useful after triggering an action that may cause asynchronous UI updates.
Parameters:
- `quietMs`, `timeoutMs`, `pollIntervalMs`

**watch_add**
Description: Add a persistent watch expression to the VS Watch window and return its current value. The expression is remembered across breaks (use watch_list to see all). Only works in break mode. For a one-shot read-only evaluation, use debug_evaluate; for a one-shot expression with side effects, use immediate_execute.
Parameters:
- `expression` (string, required)

**watch_list**: List all watch expressions with current values (break mode only).

**watch_remove**: Remove a watch expression by value or index.

**web_connect**
Description: [Browser DOM] Connect to a Chrome/Edge (CDP) or Firefox (RDP) instance for web debugging. Auto-detects browser type by default. Call this before any other web_* tool. Unrelated to ui_* tools which target Win32/WPF desktop apps.
Parameters:
- `browser` (enum: auto | chrome | firefox), `port` (integer)

**web_console**
Description: [Browser DOM — connected via web_connect] Manage browser DevTools console messages (console.log/warn/error from the page). action: enable / get / clear. For the VS Output window use output_read; for a debugged console application's stdout use console_read.
Parameters:
- `action` (enum, required: enable | get | clear), `level` (string)

**web_disconnect**
Description: [Browser DOM — connected via web_connect] Disconnect from the browser connection.
Parameters: (none)

**web_dom_get**
Description: [Browser DOM — connected via web_connect] Get the DOM tree of the current browser page with configurable depth. For the UI tree of a debugged desktop app use ui_get_tree or ui_snapshot instead.
Parameters:
- `depth` (integer)

**web_dom_query**
Description: [Browser DOM — connected via web_connect] Query DOM elements in the current browser page using a CSS selector. returnType: nodes (default, returns node IDs/info), html (returns outerHTML), attributes (returns all attributes). For UI elements in a debugged desktop app use ui_find_elements (Name/AutomationId/ClassName/ControlType) instead.
Parameters:
- `selector` (string, required), `returnType` (enum: nodes | html | attributes)

**web_element_click**
Description: [Browser DOM — connected via web_connect] Click a DOM element in the current browser page, located by CSS selector (uses JavaScript click). For clicking a UI element in a debugged desktop app use ui_click (AutomationId/Name) instead.
Parameters:
- `selector` (string, required)

**web_element_set_value**
Description: [Browser DOM — connected via web_connect] Set the value of an <input>/<textarea> in the current browser page, located by CSS selector. Uses native setter for React compatibility. For setting a value on a UI element in a debugged desktop app use ui_set_value (ValuePattern) instead.
Parameters:
- `selector` (string, required), `value` (string, required)

**web_js_execute**
Description: [Browser DOM — connected via web_connect] Execute JavaScript in the browser page context. Supports await for promises. For evaluating C#/.NET expressions in a debugged process use debug_evaluate (read-only) or immediate_execute (side effects).
Parameters:
- `expression` (string, required), `awaitPromise` (boolean)

**web_navigate**
Description: [Browser DOM — connected via web_connect] Navigate the connected browser to a URL. Optionally wait for the page load event.
Parameters:
- `url` (string, required), `waitForLoad` (boolean)

**web_network**
Description: [Browser DOM — connected via web_connect] Manage browser network monitoring (HTTP requests issued by the page). action: enable / get / clear.
Parameters:
- `action` (enum, required), `urlFilter` (string), `methodFilter` (string)

**web_screenshot**
Description: [Browser DOM — connected via web_connect] Capture a screenshot of the current browser page. Returns the image as base64. For screenshots of a debugged desktop app use ui_capture_window instead.
Parameters:
- `format` (enum: png | jpeg), `quality` (integer)

**web_status**
Description: [Browser DOM — connected via web_connect] Get the current browser connection status including console/network message counts.
Parameters: (none)

---

## Scenarios

Process each scenario independently — do NOT let your answer to one influence another.

For each scenario, emit the response structure shown at the bottom.

S01: "Build the entire solution."
S02: "Set a breakpoint at line 42 of Program.cs."
S03: "Take a screenshot of the WPF window I'm debugging."
S04: "Take a screenshot of the page I'm viewing in Chrome."
S05: "Read the contents of C:\\foo\\bar.cs."
S06: "Find references to the symbol MyClass.SomeMethod at Foo.cs line 10 column 18."
S07: "Run all unit tests in the solution."
S08: "List the projects in this solution."
S09: "What's the current debugger mode?"
S10: "Search for the literal string 'TODO' across all .cs files."
S11: "Connect to Chrome on port 9222."
S12: "Click the .submit-btn element on the current page."
S13: "Just run the app, I don't need to debug."
S14: "Start debugging the app."
S15: "Stop debugging."
S16: "Detach the debugger from PID 5678 but keep the process running."
S17: "Force-kill the debugged process with PID 12345."
S18: "What's the value of myVariable in the debugger?"
S19: "Set myVariable to 42 in the debugger."
S20: "Show me the build errors from the last build."
S21: "Show me everything in the Visual Studio Error List."
S22: "Tell me what file is currently focused in the editor."
S23: "Click the OK button in the desktop WPF app I'm debugging."
S24: "Click submit."
S25: "Push this branch to origin."
S26: "Create a new GitHub issue for this bug."
S27: "Open Slack and send a message to the team."
S28: "Commit the current changes with message 'fix: foo'."
S29: "Open the file."
S30: "Add a watch."
S31: "Click the button."
S32: "Run the tests."
S33: "Stop the debuggee."
S34: "End the debug session."
S35: "Press Ctrl+F5."
S36: "Press F5."

---

## Per-scenario response structure

For each scenario emit (use clear delimiters):

```
=== S01 ===
Chosen tool: <name, or `None` if no tool in this catalog should be called>
Arguments: <JSON object of arg name → value, or "N/A" if Chosen is None>
Confidence: high | medium | low
  (low means: in a real session I would have asked the user to clarify before calling)
Alternatives considered: <list of (tool_name, why_rejected) pairs, max 3; or "none">
Driving words: <2-5 words/phrases from the chosen tool's description that drove the decision. If you chose None, write what was missing across all tools.>
Unhelpful words: <words in any candidate's description that were ambiguous, redundant, or actively misleading. Or "none">
Missing information: <what you wished the description had said. Or "none">
```

## Final report (after all 36 scenarios)

```
=== Final report ===
Catalog-wide observations: <structural patterns across multiple tools — e.g. "many ui_* and web_* tools have parallel structure", "some tools say 'must be in break mode' inconsistently", "parameter descriptions are mostly type-only for breakpoints">

Indistinguishable pairs: <list of (tool_A, tool_B, what would have helped) tuples — tools you found indistinguishable from at least one neighbor>

Structured reflection for problematic scenarios: <Issue / Cause / General Fix Rule per problematic scenario, one per item>

Retries: <number of scenarios where you changed your answer mid-way and why>
```

## Important rules

- **DO NOT invoke any of these tools as real MCP calls.** This is a dry-run; the harness will not have side effects, but treating it as a real call defeats the evaluation.
- Treat each scenario independently; do not chain assumptions.
- If the right tool is in Tier 2 (only name+gist shown), you can still pick it. Note that Tier 2 descriptions are deliberately terse; if Tier 2 description is too thin to commit at high confidence, lower your confidence.
- If no tool in this catalog fits, return `None` — that is a valid answer for scenarios where the user is asking about something outside vs-mcp's scope.
