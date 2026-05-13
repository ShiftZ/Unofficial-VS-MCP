# vs-mcp tool catalog — snapshot-iter0-pre

抽出日: 2026-05-13
ソース: `src/VsMcp.Extension/Tools/*.cs` の `new McpToolDefinition(...)` + `SchemaBuilder` 呼び出し
ツール数: 115（get_help と一致）

各エントリは `name | description | parameters` のフラット形式。Iter 0 reconciliation 前の凍結状態。

---

## General

- **execute_command** — Execute a Visual Studio command by name (e.g. 'Edit.FormatDocument', 'Build.BuildSolution')
  - `command` (string, required): The VS command name to execute
  - `args` (string): Optional arguments for the command
- **get_status** — Get the current Visual Studio status including solution state, active document, and debugger mode. Use this instead of curl or other HTTP requests to check VS state.
- **get_help** — Get a categorized list of all available vs-mcp tools with descriptions. Call this first to understand what tools are available.

## Solution

- **solution_open** — Open a solution or project file in Visual Studio
  - `path` (string, required): Full path to the .sln or project file
- **solution_close** — Close the current solution
  - `save` (boolean): Save changes before closing (default: true)
- **solution_info** — Get information about the currently open solution

## Project

- **project_list** — List all projects in the current solution
- **project_info** — Get detailed information about a specific project
  - `name` (string, required): Project name

## Build

- **build_solution** — Build the entire solution in Visual Studio. Use this instead of running MSBuild.exe from the command line.
- **build_project** — Build a specific project
  - `name` (string, required): Project name to build
- **clean** — Clean the solution build output
- **rebuild** — Clean and rebuild the entire solution
- **build_configuration** — Get or set the active solution build configuration and platform. Call without parameters to list all available configurations.
  - `configuration` (string): Configuration name to set (e.g. 'Debug', 'Release')
  - `platform` (string): Platform name to set (e.g. 'Any CPU', 'x64', 'x86')
- **get_build_errors** — Get the list of build errors and warnings from the Visual Studio Error List. Call this after build_solution to check results.

## Editor

- **file_open** — Open a file in the Visual Studio editor
  - `path` (string, required): Full path to the file to open
  - `line` (integer): Optional line number to navigate to
- **file_close** — Close a file in the editor. If no path is specified, closes the active document.
  - `path` (string): Path to the file to close (optional, closes active document if omitted)
  - `save` (boolean): Save before closing (default: true)
- **file_read** — Read the contents of a file. Can optionally read specific line ranges.
  - `path` (string, required): Full path to the file to read
  - `startLine` (integer): Start line number (1-based, optional)
  - `endLine` (integer): End line number (1-based, optional)
- **file_write** — Write content to a file, replacing its entire contents
  - `path` (string, required): Full path to the file to write
  - `content` (string, required): The content to write to the file
- **file_edit** — Edit a file by replacing a specific text occurrence with new text
  - `path` (string, required): Full path to the file to edit
  - `oldText` (string, required): The exact text to find and replace
  - `newText` (string, required): The replacement text
- **get_active_document** — Get information about the currently active document in the editor
- **find_in_files** — Search for text in files within the solution. Searches the file system directly (fast, does not block VS UI). Skips bin/obj/.vs/packages/node_modules directories.
  - `query` (string, required): The search text or pattern
  - `filePattern` (string): File pattern to filter (e.g. '*.cs', '*.xaml')
  - `matchCase` (boolean): Whether to match case (default: false)
  - `useRegex` (boolean): Whether to use regular expressions (default: false)
  - `maxResults` (integer): Maximum number of results to return (default: 100)

## EditPreview

- **edit_preview** — Show a diff preview of proposed changes in VS and create a pending edit for approval. Use oldText+newText for partial replacement (like file_edit), or content for full replacement (like file_write).
  - `path` (string, required), `oldText`, `newText`, `content` (string)
- **edit_approve** — Approve a pending edit and apply the changes to the file
  - `pendingId` (string, required)
- **edit_reject** — Reject a pending edit and discard the changes
  - `pendingId` (string, required)
- **edit_list_pending** — List all pending edit previews with their status

## Debugger

- **debug_start** — Start debugging the startup project (equivalent to F5). Use this instead of trying to launch Visual Studio or press F5 manually.
- **debug_start_without_debugging** — Start the startup project without the debugger attached (equivalent to Ctrl+F5)
- **debug_stop** — Stop debugging the current session
- **debug_restart** — Restart debugging the current session
- **debug_attach** — Attach the debugger to a running process by name or PID
  - `processName` (string): Name of the process to attach to (e.g. 'myapp')
  - `processId` (integer): PID of the process to attach to
- **debug_break** — Break (pause) the debugger at the current execution point
- **debug_continue** — Continue (resume) execution after a breakpoint or break
- **debug_step** — Step through code. direction: over (F10), into (F11), or out (Shift+F11)
  - `direction` (enum, required): over | into | out
- **debug_get_callstack** — Get the current call stack of the active thread
- **debug_get_locals** — Get the local variables in the current stack frame
- **debug_get_threads** — Get all threads in the current debug session
- **debug_get_mode** — Get the current debugger mode (Design, Running, or Break)
- **debug_evaluate** — Evaluate an expression in the current debug context (only works in break mode)
  - `expression` (string, required): The expression to evaluate

## Breakpoint

- **breakpoint_set** — Set a breakpoint. Use file+line for location breakpoints, or functionName for function breakpoints. Optionally add condition or hitCount.
  - `file`, `line`, `functionName`, `condition`, `hitCount`, `hitCountType`
- **breakpoint_remove** — Remove a breakpoint at a specific file and line
  - `file` (required), `line` (required)
- **breakpoint_list** — List all breakpoints in the current solution
- **breakpoint_enable** — Enable or disable a breakpoint at a specific file and line
  - `file`, `line`, `enabled` (all required)

## Watch

- **watch_add** — Add a watch expression and return its current value (only works in break mode)
  - `expression` (string, required)
- **watch_remove** — Remove a watch expression by value or index
  - `expression` (string), `index` (integer)
- **watch_list** — List all watch expressions with their current values (values are only available in break mode)

## Thread

- **thread_switch** — Switch the active (current) thread by thread ID
  - `threadId` (integer, required)
- **thread_set_frozen** — Freeze or thaw a thread. frozen=true freezes the thread, frozen=false thaws it.
  - `threadId`, `frozen` (both required)
- **thread_get_callstack** — Get the call stack of a specific thread by ID
  - `threadId` (integer, required)

## Process

- **process_list_debugged** — List all processes currently being debugged
- **process_list_local** — List local processes available for attaching the debugger
  - `filter` (string): Optional name filter (case-insensitive substring match)
- **process_detach** — Detach the debugger from a specific process
  - `processId` (integer, required)
- **process_terminate** — Terminate a process being debugged
  - `processId` (integer, required)

## Immediate

- **immediate_execute** — Execute an expression with side effects in the debugger context (like the Immediate Window). Can assign variables, call methods with side effects, etc. Only works in break mode.
  - `expression` (string, required), `timeout` (integer)

## Module

- **module_list** — List all loaded modules (DLLs/assemblies) in the current debug session. Collected from stack frames across all threads.

## Register

- **register_list** — Get values of common CPU registers (works best in native or mixed-mode debugging, must be in break mode)
  - `architecture` (enum): x64 | x86
- **register_get** — Get the value of a specific CPU register by name (e.g. 'rax', 'eip'). Must be in break mode.
  - `name` (string, required)

## Exception

- **exception_settings_get** — Get exception break settings. Lists exception groups and their configured exceptions.
  - `group` (string): Exception group name to filter
- **exception_settings_set** — Configure when to break on a specific exception type. Uses Debug.SetBreakOnException VS command.
  - `exceptionName` (string, required), `breakWhenThrown` (boolean, required)

## Memory

- **memory_read** — Read memory bytes. Provide 'address' for raw memory read, or 'variable' to get a variable's address and byte representation. Must be in break mode.
  - `address`, `variable`, `count`

## Parallel

- **parallel_stacks** — Get all threads' call stacks in a tree view, grouping threads that share common stack frames (like Parallel Stacks window). Must be in break mode.
- **parallel_watch** — Evaluate the same expression on all threads and compare results. Temporarily switches threads and restores the original. Must be in break mode.
  - `expression` (string, required)
- **parallel_tasks_list** — Attempt to list TPL (Task Parallel Library) task information by evaluating internal task state. Best-effort; results depend on runtime version. Must be in break mode.

## Diagnostics

- **diagnostics_binding_errors** — Extract XAML/WPF binding errors from the Debug output pane. Filters for 'BindingExpression' and 'binding' error patterns.
  - `tail` (integer)

## Output

- **output_write** — Write text to a Visual Studio Output window pane
  - `text` (string, required), `pane` (string)
- **output_read** — Read the content of a Visual Studio Output window pane. Supports localized pane names (e.g. 'Build', 'Debug'). Call without pane parameter to list available panes. Returns the last 'tail' lines by default (200). Use tail=0 to read all content. Use 'pattern' to filter lines by regex.
  - `pane`, `tail`, `pattern`
- **output_clear** — Clear the content of a Visual Studio Output window pane
  - `pane` (string, required)
- **error_list_get** — Get all items from the Visual Studio Error List window (errors, warnings, and messages)
  - `severity` (string)

## Console

- **console_read** — Read the console output buffer of a debugged console application. The output is read from the console window (conhost.exe/Windows Terminal), not the VS Output pane.
  - `tail`, `processId`
- **console_send** — Send input to the console of a debugged console application. Provide 'text' for text input or 'keys' for special keys (ctrl+c, ctrl+break, ctrl+z, enter, escape, tab, backspace, up, down, left, right).
  - `text`, `keys`, `newline`, `processId`
- **console_get_info** — Get console information (buffer size, cursor position, window rect, title) of a debugged console application
  - `processId` (integer)

## Web

- **web_connect** — Connect to a browser for web debugging. Supports Chrome/Edge (via CDP) and Firefox (via RDP). Auto-detects browser type by default.
  - `browser` (enum: auto | chrome | firefox), `port` (integer)
- **web_disconnect** — Disconnect from the browser connection
- **web_status** — Get the current browser connection status including console/network message counts
- **web_navigate** — Navigate the browser to a URL. Optionally wait for the page load event.
  - `url` (string, required), `waitForLoad` (boolean)
- **web_screenshot** — Capture a screenshot of the current page. Returns the image as base64.
  - `format` (enum), `quality` (integer)
- **web_dom_get** — Get the DOM tree of the current page with configurable depth
  - `depth` (integer)
- **web_dom_query** — Query DOM elements using a CSS selector. returnType: nodes (default, returns node IDs/info), html (returns outerHTML), attributes (returns all attributes)
  - `selector` (string, required), `returnType` (enum)
- **web_console** — Manage browser console messages. action: enable (start collecting), get (retrieve messages), clear (clear buffer)
  - `action` (enum, required: enable | get | clear), `level` (string)
- **web_js_execute** — Execute JavaScript in the browser page context. Supports await for promises.
  - `expression` (string, required), `awaitPromise` (boolean)
- **web_network** — Manage network monitoring. action: enable (start capturing), get (retrieve entries), clear (clear buffer)
  - `action` (enum, required), `urlFilter`, `methodFilter`
- **web_element_click** — Click a DOM element found by CSS selector (uses JavaScript click)
  - `selector` (string, required)
- **web_element_set_value** — Set the value of an input element found by CSS selector. Uses native setter for React compatibility.
  - `selector` (required), `value` (required)

## UI

- **ui_capture_window** — Capture a screenshot of the debugged application's main window as a base64 PNG image
- **ui_capture_region** — Capture a screenshot of a specific region of the debugged application's window
  - `x`, `y`, `width`, `height` (all required, integers)
- **ui_snapshot** — Capture a compact semantic snapshot of the debugged application's main window in a single call. Returns a pruned UI Automation tree (omits invisible/boring nodes, includes actionable patterns, state flags, rect, and focused element) plus an optional screenshot. Optimized for autonomous exploration and LLM-driven UI testing; prefer this over ui_get_tree + ui_capture_window.
  - `depth`, `maxElements`, `includeScreenshot`, `includeOffscreen`, `ancestorAutomationId`
- **ui_get_tree** — Get the UI element tree of the debugged application's main window
  - `depth`, `maxChildren`, `maxElements`
- **ui_find_elements** — Find UI elements matching specified criteria in the debugged application. String fields (name, automationId, className) support match modes: 'exact' (default), 'contains' (case-insensitive substring), and 'regex' (case-insensitive). Use 'hasPattern' to require supported UIA patterns (invoke, toggle, select, setvalue, expand). Use 'ancestorAutomationId' to scope the search to the descendants of a specific element.
  - `name`, `nameMatch`, `automationId`, `automationIdMatch`, `className`, `classNameMatch`, `controlType`, `hasPattern`, `ancestorAutomationId`, `maxResults`
- **ui_wait_for_element** — Wait until a UI element matching the given criteria reaches the specified state. Polls the UI Automation tree at regular intervals and returns as soon as the condition is met or the timeout elapses. States: 'appears' (default), 'disappears', 'enabled', 'focused'. Use this instead of sleeping after triggering a UI action.
  - `name`, `automationId`, `className`, `controlType`, `state`, `timeoutMs`, `pollIntervalMs`
- **ui_wait_idle** — Wait until the UI Automation tree stops changing for a quiet period. Useful after triggering an action that may cause asynchronous UI updates (loading, layout, progressive rendering). Returns as soon as the element count is stable for quietMs, or when the timeout is reached.
  - `quietMs`, `timeoutMs`, `pollIntervalMs`
- **ui_get_element** — Get detailed properties of a specific UI element by its AutomationId
  - `automationId` (string, required)
- **ui_click** — Click a UI element by AutomationId, Name, or screen coordinates. When an element is given, InvokePattern is tried first (no cursor movement). For physical clicks (coordinates or pattern-less elements), the cursor is restored to its previous position by default.
  - `automationId`, `name`, `x`, `y`, `waitMs`, `restoreCursor`, `blockInput`
- **ui_double_click** — Double-click a UI element by AutomationId, Name, or screen coordinates. Always uses physical mouse events; cursor is restored to its previous position by default.
- **ui_right_click** — Right-click a UI element by AutomationId, Name, or screen coordinates to open context menus. Always uses physical mouse events; cursor is restored to its previous position by default.
- **ui_drag** — Perform a drag-and-drop operation from start coordinates to end coordinates. Cursor is restored to its previous position after the drag completes by default.
  - `startX`, `startY`, `endX`, `endY` (all required), `steps`, `delayMs`, `restoreCursor`, `blockInput`
- **ui_mouse_wheel** — Scroll the mouse wheel over a UI element or screen coordinates. Specify the position by AutomationId, Name, or x/y coordinates. Use 'clicks' to control the amount and direction: positive scrolls up (away from user), negative scrolls down (toward user). One click equals one wheel notch (WHEEL_DELTA=120). Set 'horizontal' to true for horizontal wheel scrolling. By default, when an element is given, ScrollPattern is used (no cursor movement, user's mouse is not disturbed). Falls back to physical wheel events otherwise. When physical events are used, the cursor is restored to its previous position.
  - `automationId`, `name`, `x`, `y`, `clicks`, `horizontal`, `usePattern`, `restoreCursor`, `blockInput`, `waitMs`
- **ui_set_value** — Set the value of a UI element (e.g. text input) using ValuePattern
  - `automationId` (required), `value` (required)
- **ui_invoke** — Invoke the default action on a UI element (e.g. click a button) using InvokePattern
  - `automationId` (string, required)
- **ui_send_keys** — Send keyboard input to the debugged application's foreground window. Use 'keys' for key combinations (e.g. 'ctrl+f', 'alt+f4', 'shift+ctrl+s', 'enter', 'f5') or 'text' to type a string of characters. Modifier keys: ctrl, shift, alt, win. Named keys: enter, escape/esc, tab, backspace/bs, delete/del, insert/ins, home, end, pageup/pgup, pagedown/pgdn, up, down, left, right, space, f1-f12. For single characters like 'a', 'A', '1', use keys='a'. Multiple key presses can be separated with spaces: keys='tab tab enter'.
  - `keys`, `text`, `waitMs`

## Test

- **test_discover** — Discover all tests in the solution or a specific project. Returns a list of test names.
  - `project` (string)
- **test_run** — Run tests and get results. Supports filtering by test name/category. Returns passed/failed/skipped counts and failure details.
  - `project`, `filter`, `timeout`
- **test_results** — Get detailed results from the last test run (or a specific TRX file). Shows each test's outcome, duration, and error details.
  - `trxPath` (string)

## NuGet

- **nuget_list** — List installed NuGet packages for a specific project
  - `project` (string, required)
- **nuget_search** — Search for NuGet packages on NuGet.org
  - `query` (string, required), `take` (integer)
- **nuget_install** — Install a NuGet package into a project
  - `project`, `packageId` (both required), `version` (optional)
- **nuget_update** — Update a NuGet package to a specific version
  - `project`, `packageId`, `version` (all required)
- **nuget_uninstall** — Remove a NuGet package from a project
  - `project`, `packageId` (both required)

## Navigation

- **code_goto_definition** — Navigate to the definition of a symbol at the specified position. Opens the file and returns the definition location.
  - `path`, `line`, `column` (all required)
- **code_find_references** — Find all references of a symbol at the specified position. Triggers VS Find All References window.
  - `path`, `line`, `column` (all required)
- **code_goto_implementation** — Navigate to the implementation of an interface or abstract member at the specified position.
  - `path`, `line`, `column` (all required)

## SolutionExplorer

- **solution_add_project** — Add an existing project to the current solution
  - `projectPath` (required)
- **solution_remove_project** — Remove a project from the current solution
  - `name` (required)
- **project_add_file** — Add an existing file to a project
  - `project`, `filePath` (both required)
- **project_remove_file** — Remove a file from a project
  - `project`, `filePath` (both required)
- **project_add_reference** — Add a project-to-project reference
  - `project`, `referencedProject` (both required)
- **project_remove_reference** — Remove a reference from a project
  - `project`, `referenceName` (both required)
