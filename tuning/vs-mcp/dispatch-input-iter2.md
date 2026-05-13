# vs-mcp Iter 2 dispatch input

You are an executor evaluating vs-mcp's tool catalog with a blank slate. **DRY-RUN only — do not invoke any MCP tools.**

## Background

vs-mcp is a Visual Studio automation MCP server (115 tools). Two surfaces co-exist: Windows UI Automation (the desktop app being debugged in VS) and the browser DOM (Chrome/Edge/Firefox via CDP/RDP).

## Tool catalog (alphabetical, mixed tiers)

Tier 1 entries show the full description and parameters; Tier 2 entries show only `name: gist`. Both are part of the server.

**breakpoint_enable**: Enable or disable a breakpoint at a specific file and line.

**breakpoint_list**: List all breakpoints in the current solution.

**breakpoint_remove**: Remove a breakpoint at a specific file and line.

**breakpoint_set**: Set a breakpoint at a file+line or by function name.

**build_configuration**: Get or set the active solution build configuration and platform.

**build_project**
Description: Build a specific project.
Parameters:
- `name` (string, required)

**build_solution**
Description: Build the entire solution in Visual Studio. Use this instead of running MSBuild.exe from the command line.
Parameters: (none)

**clean**: Clean the solution build output.

**code_find_references**
Description: Find all references of a symbol at the specified position. Triggers VS Find All References window.
Parameters: `path`, `line`, `column` (all required)

**code_goto_definition**
Description: Navigate to the definition of a symbol at the specified position. Opens the file and returns the definition location.
Parameters: `path`, `line`, `column` (all required)

**code_goto_implementation**: Navigate to the implementation of an interface or abstract member.

**console_get_info**: Get console info (buffer size, cursor position) of a debugged console app.

**console_read**
Description: Read the console output buffer of a debugged console application. The output is read from the console window (conhost.exe/Windows Terminal), not the VS Output pane.
Parameters: `tail`, `processId`

**console_send**: Send input/keys to the console of a debugged console application.

**debug_attach**
Description: Attach the debugger to a running process by name or PID.
Parameters: `processName`, `processId`

**debug_break**
Description: Break (pause) the debugger at the current execution point.
Parameters: (none)

**debug_continue**
Description: Continue (resume) execution after a breakpoint or break.
Parameters: (none)

**debug_evaluate**
Description: Read-only evaluate an expression in the current debug context — no side effects (no assignments, no method calls that mutate state). Must be in break mode. Use this to inspect variable values. For expressions WITH side effects (assignments, mutating method calls) use immediate_execute. To persist an expression that re-evaluates across breaks, use watch_add.
Parameters: `expression` (string, required)

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
Parameters: `pendingId` (string, required)

**edit_list_pending**: List all pending edit previews with their status.

**edit_preview**
Description: Show a diff preview of proposed changes in VS and create a pending edit for approval. Use oldText+newText for partial replacement (like file_edit), or content for full replacement (like file_write).
Parameters: `path` (required), `oldText`, `newText`, `content`

**edit_reject**
Description: Reject a pending edit and discard the changes.
Parameters: `pendingId` (string, required)

**error_list_get**
Description: Get all items currently shown in the Visual Studio Error List window — includes errors/warnings/messages from any source: build, IntelliSense, analyzers, XAML, etc. Use this when the user wants 'everything in the Error List'. For build-only errors collected after a build, prefer get_build_errors.
Parameters: `severity` (string)

**exception_settings_get**: Get exception break settings.

**exception_settings_set**: Configure when to break on a specific exception type.

**execute_command**
Description: FALLBACK: execute an arbitrary Visual Studio command by name (e.g. 'Edit.FormatDocument', 'Build.BuildSolution'). Prefer a dedicated tool when one exists — for builds use build_solution/build_project, for debugging use debug_start/debug_stop, for files use file_open, etc. Only use execute_command when no dedicated tool covers the action the user wants.
Parameters: `command` (string, required), `args` (string)

**file_close**: Close a file in the editor.

**file_edit**
Description: Edit a file by replacing a specific text occurrence with new text.
Parameters: `path`, `oldText`, `newText` (all required)

**file_open**
Description: Open a file in the Visual Studio editor.
Parameters: `path` (required), `line`

**file_read**
Description: Read the contents of a file at a given path (does not need to be open in the editor). Can optionally read specific line ranges. For information about the document currently focused in the VS editor (without specifying a path), use get_active_document.
Parameters: `path` (required), `startLine`, `endLine`

**file_write**
Description: DESTRUCTIVE: overwrite a file's entire contents (no diff, no approval). For partial replacement use file_edit; to show the user a diff preview that requires explicit approval before applying, use edit_preview with the 'content' parameter.
Parameters: `path`, `content` (both required)

**find_in_files**
Description: Text search across files in the solution directory (literal text or regex). Searches the file system directly (fast, does not block VS UI). Skips bin/obj/.vs/packages/node_modules. Not symbol-aware — for finding references of a symbol use code_find_references; for navigating to a definition use code_goto_definition.
Parameters: `query` (required), `filePattern`, `matchCase`, `useRegex`, `maxResults`

**get_active_document**
Description: Get information about the document currently focused in the VS editor (path, language, dirty state, caret position). Use this to answer 'what file is the user looking at right now'. To read the contents of a specific file by path, use file_read.
Parameters: (none)

**get_build_errors**
Description: Get build-produced errors and warnings (MSBuild/compiler output) from the Visual Studio Error List. Call this right after build_solution or build_project to check the build result. For the full Error List including IntelliSense and analyzer items, use error_list_get instead.
Parameters: (none)

**get_help**: Get a categorized list of all available vs-mcp tools.

**get_status**
Description: Get the current Visual Studio status — bundles solution state, active document, and debugger mode in one call. Use this instead of curl or other HTTP requests. For a single facet, prefer the dedicated tool: solution_info (solution only), get_active_document (active document only), debug_get_mode (debugger mode only).
Parameters: (none)

**immediate_execute**
Description: Execute an expression WITH side effects in the debugger context (like the VS Immediate Window) — assignments, mutating method calls, etc. Must be in break mode. For read-only inspection of a value without side effects, prefer debug_evaluate. To persist an expression that re-evaluates each break, use watch_add.
Parameters: `expression` (required), `timeout`

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
Parameters: `pane`, `tail`, `pattern`

**output_write**
Description: Write text to a Visual Studio Output window pane.
Parameters: `text` (required), `pane`

**parallel_stacks**: Get all threads' call stacks in a tree view (Parallel Stacks window).

**parallel_tasks_list**: List TPL task information (best-effort, break mode).

**parallel_watch**: Evaluate the same expression on all threads and compare.

**process_detach**
Description: Detach the debugger from a specific debugged process WITHOUT terminating it — the process keeps running freely. Use this when the user wants to release a debug attachment but keep the app running. For ending the whole debug session normally use debug_stop; to force-kill a process use process_terminate.
Parameters: `processId` (required)

**process_list_debugged**
Description: List all processes currently being debugged.
Parameters: (none)

**process_list_local**
Description: List local processes available for attaching the debugger.
Parameters: `filter`

**process_terminate**
Description: DESTRUCTIVE: force-kill a specific debugged process by PID. This is NOT the normal 'stop debugging' action — for that, use debug_stop. Only use process_terminate when the user explicitly wants to kill a specific PID, or when debug_stop is not appropriate (e.g. unresponsive process). To detach the debugger without killing, use process_detach.
Parameters: `processId` (required)

**project_add_file**: Add an existing file to a project.

**project_add_reference**: Add a project-to-project reference.

**project_info**: Get detailed information about a specific project.

**project_list**: List all projects in the current solution.

**project_remove_file**: Remove a file from a project.

**project_remove_reference**: Remove a reference from a project.

**rebuild**
Description: Clean and rebuild the entire solution (equivalent to clean followed by build_solution). Use this only when the user explicitly wants a forced rebuild — for a normal build, prefer build_solution (incremental, faster).
Parameters: (none)

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
Parameters: `x`, `y`, `width`, `height` (all required)

**ui_capture_window**
Description: [Windows UIA — desktop app being debugged] Capture a screenshot of the debugged application's main window as a base64 PNG image. For web pages use web_screenshot instead.
Parameters: (none)

**ui_click**
Description: [Windows UIA — desktop app being debugged] Click a UI element in a Win32/WPF/WinForms application by AutomationId, Name, or screen coordinates. When an element is given, InvokePattern is tried first (no cursor movement). For physical clicks (coordinates or pattern-less elements), the cursor is restored to its previous position by default. For clicking DOM elements in a browser page use web_element_click (CSS selector) instead.
Parameters: `automationId`, `name`, `x`, `y`, `waitMs`, `restoreCursor`, `blockInput`

**ui_double_click**
Description: [Windows UIA — desktop app being debugged] Double-click a UI element in a desktop app by AutomationId, Name, or screen coordinates. Always uses physical mouse events; cursor is restored to its previous position by default. Browser DOM has no double-click helper — use web_js_execute with dispatchEvent('dblclick').
Parameters: `automationId`, `name`, `x`, `y`, `waitMs`, `restoreCursor`, `blockInput`

**ui_drag**
Description: [Windows UIA — desktop app being debugged] Perform a drag-and-drop operation in a desktop app from start coordinates to end coordinates. Cursor is restored to its previous position after the drag completes by default. For browser DOM drag use web_js_execute with synthetic drag events.
Parameters: `startX`, `startY`, `endX`, `endY` (all required), `steps`, `delayMs`, `restoreCursor`, `blockInput`

**ui_find_elements**
Description: [Windows UIA — desktop app being debugged] Find UI elements matching specified criteria (Name / AutomationId / ClassName / ControlType) in the debugged desktop application. String fields support match modes: 'exact' (default), 'contains', 'regex'. Use 'hasPattern' to require supported UIA patterns. Use 'ancestorAutomationId' to scope. For browser DOM elements use web_dom_query (CSS selectors) instead.
Parameters: `name`, `nameMatch`, `automationId`, `automationIdMatch`, `className`, `classNameMatch`, `controlType`, `hasPattern`, `ancestorAutomationId`, `maxResults`

**ui_get_element**
Description: [Windows UIA — desktop app being debugged] Get detailed properties of a specific UI element by its AutomationId. For browser DOM elements use web_dom_query with returnType='attributes'.
Parameters: `automationId` (required)

**ui_get_tree**
Description: [Windows UIA — desktop app being debugged] Get the raw UI element tree of the debugged application's main window. Prefer ui_snapshot unless you specifically need the unpruned tree. For web pages use web_dom_get instead.
Parameters: `depth`, `maxChildren`, `maxElements`

**ui_invoke**
Description: [Windows UIA — desktop app being debugged] Invoke the default action on a UI element (e.g. click a WPF/WinForms button) using InvokePattern only — requires AutomationId and the element must support InvokePattern. For richer click semantics (Name/coords, physical-click fallback when InvokePattern is unavailable) prefer ui_click. For browser DOM elements use web_element_click instead.
Parameters: `automationId` (required)

**ui_mouse_wheel**
Description: [Windows UIA — desktop app being debugged] Scroll the mouse wheel over a UI element or screen coordinates in a desktop app. Specify the position by AutomationId, Name, or x/y. For scrolling a browser page use web_js_execute with window.scrollBy.
Parameters: `automationId`, `name`, `x`, `y`, `clicks`, `horizontal`, `usePattern`, `restoreCursor`, `blockInput`, `waitMs`

**ui_right_click**
Description: [Windows UIA — desktop app being debugged] Right-click a UI element in a desktop app by AutomationId, Name, or screen coordinates to open context menus. Always uses physical mouse events; cursor is restored to its previous position by default. Browser DOM has no right-click helper — use web_js_execute with dispatchEvent('contextmenu').
Parameters: `automationId`, `name`, `x`, `y`, `waitMs`, `restoreCursor`, `blockInput`

**ui_send_keys**
Description: [Windows UIA — desktop app being debugged] Send keyboard input via Win32 SendInput to the debugged desktop application's foreground window. Use 'keys' for key combinations or 'text' to type characters. For typing into a browser page use web_element_set_value or web_js_execute with KeyboardEvent.
Parameters: `keys`, `text`, `waitMs`

**ui_set_value**
Description: [Windows UIA — desktop app being debugged] Set the value of a UI element (e.g. WPF/WinForms text input) using ValuePattern. For setting an <input> value in a browser page use web_element_set_value (CSS selector) instead.
Parameters: `automationId`, `value` (both required)

**ui_snapshot**
Description: [Windows UIA — desktop app being debugged] Capture a compact semantic snapshot of the debugged application's main window in a single call. Returns a pruned UI Automation tree plus an optional screenshot. Prefer this over ui_get_tree + ui_capture_window. For web pages use web_dom_get + web_screenshot instead.
Parameters: `depth`, `maxElements`, `includeScreenshot`, `includeOffscreen`, `ancestorAutomationId`

**ui_wait_for_element**
Description: [Windows UIA — desktop app being debugged] Wait until a UI element matching the given criteria reaches the specified state. States: 'appears' (default), 'disappears', 'enabled', 'focused'. Use this instead of sleeping after triggering a UI action.
Parameters: `name`, `automationId`, `className`, `controlType`, `state`, `timeoutMs`, `pollIntervalMs`

**ui_wait_idle**
Description: [Windows UIA — desktop app being debugged] Wait until the UI Automation tree stops changing for a quiet period. Useful after triggering an action that may cause asynchronous UI updates.
Parameters: `quietMs`, `timeoutMs`, `pollIntervalMs`

**watch_add**
Description: Add a persistent watch expression to the VS Watch window and return its current value. The expression is remembered across breaks (use watch_list to see all). Only works in break mode. For a one-shot read-only evaluation, use debug_evaluate; for a one-shot expression with side effects, use immediate_execute.
Parameters: `expression` (required)

**watch_list**: List all watch expressions with current values (break mode only).

**watch_remove**: Remove a watch expression by value or index.

**web_connect**
Description: [Browser DOM] Connect to a Chrome/Edge (CDP) or Firefox (RDP) instance for web debugging. Auto-detects browser type by default. Call this before any other web_* tool. Unrelated to ui_* tools which target Win32/WPF desktop apps.
Parameters: `browser` (enum: auto | chrome | firefox), `port`

**web_console**
Description: [Browser DOM — connected via web_connect] Manage browser DevTools console messages (console.log/warn/error from the page). action: enable / get / clear. For the VS Output window use output_read; for a debugged console application's stdout use console_read.
Parameters: `action` (enum, required), `level`

**web_disconnect**
Description: [Browser DOM — connected via web_connect] Disconnect from the browser connection.
Parameters: (none)

**web_dom_get**
Description: [Browser DOM — connected via web_connect] Get the DOM tree of the current browser page with configurable depth. For the UI tree of a debugged desktop app use ui_get_tree or ui_snapshot instead.
Parameters: `depth`

**web_dom_query**
Description: [Browser DOM — connected via web_connect] Query DOM elements in the current browser page using a CSS selector. returnType: nodes / html / attributes. For UI elements in a debugged desktop app use ui_find_elements (Name/AutomationId/ClassName/ControlType) instead.
Parameters: `selector` (required), `returnType` (enum)

**web_element_click**
Description: [Browser DOM — connected via web_connect] Click a DOM element in the current browser page, located by CSS selector (uses JavaScript click). For clicking a UI element in a debugged desktop app use ui_click (AutomationId/Name) instead.
Parameters: `selector` (required)

**web_element_set_value**
Description: [Browser DOM — connected via web_connect] Set the value of an <input>/<textarea> in the current browser page, located by CSS selector. Uses native setter for React compatibility. For setting a value on a UI element in a debugged desktop app use ui_set_value (ValuePattern) instead.
Parameters: `selector`, `value` (both required)

**web_js_execute**
Description: [Browser DOM — connected via web_connect] Execute JavaScript in the browser page context. Supports await for promises. For evaluating C#/.NET expressions in a debugged process use debug_evaluate (read-only) or immediate_execute (side effects).
Parameters: `expression` (required), `awaitPromise`

**web_navigate**
Description: [Browser DOM — connected via web_connect] Navigate the connected browser to a URL. Optionally wait for the page load event.
Parameters: `url` (required), `waitForLoad`

**web_network**
Description: [Browser DOM — connected via web_connect] Manage browser network monitoring (HTTP requests issued by the page). action: enable / get / clear.
Parameters: `action` (enum, required), `urlFilter`, `methodFilter`

**web_screenshot**
Description: [Browser DOM — connected via web_connect] Capture a screenshot of the current browser page. Returns the image as base64. For screenshots of a debugged desktop app use ui_capture_window instead.
Parameters: `format` (enum), `quality`

**web_status**
Description: [Browser DOM — connected via web_connect] Get the current browser connection status including console/network message counts.
Parameters: (none)

---

## Scenarios

Process each scenario independently. Same 36 main scenarios as iter 1, plus 8 hold-outs (H01–H08) that were previously sealed.

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
H01: "Detach the debugger but keep the program running."
H02: "Take a snapshot of the WPF UI tree without the screenshot."
H03: "Print the value of foo.Bar.Baz in the debugger console without changing anything."
H04: "Type 'hello world' into the AutomationId='UserNameTextBox' field of the desktop app I'm debugging."
H05: "Kill the debugged process — it's frozen."
H06: "List network requests the page has made."
H07: "Show me how many files reference the type Foo."
H08: "Refresh the page."

---

## Per-scenario response structure

For each scenario emit:

```
=== S01 ===
Chosen tool: <name, or `None`>
Arguments: <JSON, or "N/A">
Confidence: high | medium | low
Alternatives considered: <list, or "none">
Driving words: <2-5 phrases from descriptions>
Unhelpful words: <or "none">
Missing information: <or "none">
```

After all scenarios, emit:

```
=== Final report ===
Catalog-wide observations:
Indistinguishable pairs:
Structured reflection for problematic scenarios:
Retries: <count and notes>
```

## Hard rules

- DO NOT call any mcp__vs-mcp__* tool. Dry-run only.
- Treat each scenario independently.
- If no tool fits, return `None`.
- Lower confidence when the gist (Tier 2) is too thin.
- Cross-reference clauses ("For X use Y instead") are intentional — follow them.
