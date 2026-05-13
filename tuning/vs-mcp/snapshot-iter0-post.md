# vs-mcp tool catalog — snapshot-iter0-post

Iter 0 の reconciliation edit を C# ソースに適用した後の凍結状態。Iter 1 の dispatch contract はこの内容をそのまま渡す。

差分対象: 27 ツールに編集を適用（UI 15 + Web 12 + 11 個の per-tool disambiguation。重複ありで合計編集箇所は **30 件**）。

---

## General

- **execute_command** — FALLBACK: execute an arbitrary Visual Studio command by name (e.g. 'Edit.FormatDocument', 'Build.BuildSolution'). Prefer a dedicated tool when one exists — for builds use build_solution/build_project, for debugging use debug_start/debug_stop, for files use file_open, etc. Only use execute_command when no dedicated tool covers the action the user wants.
  - `command` (string, required), `args` (string)
- **get_status** — Get the current Visual Studio status including solution state, active document, and debugger mode. Use this instead of curl or other HTTP requests to check VS state.
- **get_help** — Get a categorized list of all available vs-mcp tools with descriptions. Call this first to understand what tools are available.

## Solution

- **solution_open** — Open a solution or project file in Visual Studio
  - `path` (required)
- **solution_close** — Close the current solution
  - `save`
- **solution_info** — Get information about the currently open solution

## Project

- **project_list** — List all projects in the current solution
- **project_info** — Get detailed information about a specific project
  - `name` (required)

## Build

- **build_solution** — Build the entire solution in Visual Studio. Use this instead of running MSBuild.exe from the command line.
- **build_project** — Build a specific project
  - `name` (required)
- **clean** — Clean the solution build output
- **rebuild** — Clean and rebuild the entire solution
- **build_configuration** — Get or set the active solution build configuration and platform. Call without parameters to list all available configurations.
- **get_build_errors** — Get build-produced errors and warnings (MSBuild/compiler output) from the Visual Studio Error List. Call this right after build_solution or build_project to check the build result. For the full Error List including IntelliSense and analyzer items, use error_list_get instead.

## Editor

- **file_open** — Open a file in the Visual Studio editor
  - `path` (required), `line`
- **file_close** — Close a file in the editor. If no path is specified, closes the active document.
- **file_read** — Read the contents of a file at a given path (does not need to be open in the editor). Can optionally read specific line ranges. For information about the document currently focused in the VS editor (without specifying a path), use get_active_document.
  - `path` (required), `startLine`, `endLine`
- **file_write** — DESTRUCTIVE: overwrite a file's entire contents (no diff, no approval). For partial replacement use file_edit; to show the user a diff preview that requires explicit approval before applying, use edit_preview with the 'content' parameter.
  - `path` (required), `content` (required)
- **file_edit** — Edit a file by replacing a specific text occurrence with new text
  - `path`, `oldText`, `newText` (all required)
- **get_active_document** — Get information about the document currently focused in the VS editor (path, language, dirty state, caret position). Use this to answer 'what file is the user looking at right now'. To read the contents of a specific file by path, use file_read.
- **find_in_files** — Text search across files in the solution directory (literal text or regex). Searches the file system directly (fast, does not block VS UI). Skips bin/obj/.vs/packages/node_modules. Not symbol-aware — for finding references of a symbol use code_find_references; for navigating to a definition use code_goto_definition.

## EditPreview

- **edit_preview** — Show a diff preview of proposed changes in VS and create a pending edit for approval. Use oldText+newText for partial replacement (like file_edit), or content for full replacement (like file_write).
- **edit_approve** — Approve a pending edit and apply the changes to the file
- **edit_reject** — Reject a pending edit and discard the changes
- **edit_list_pending** — List all pending edit previews with their status

## Debugger

- **debug_start** — F5: start the startup project WITH the debugger attached (breakpoints hit, exceptions break into VS). Use this when the user wants to debug. For run-without-debugging (Ctrl+F5), use debug_start_without_debugging.
- **debug_start_without_debugging** — Ctrl+F5: run the startup project WITHOUT the debugger attached (breakpoints are ignored, exceptions do not break into VS). Use this when the user wants to just run the app. For debugging (F5) with breakpoints, use debug_start.
- **debug_stop** — Stop the current debug session normally (Shift+F5). Detaches and terminates the debuggee like clicking the VS Stop button. This is the default 'stop debugging' action. To detach without terminating, use process_detach. To force-kill a specific debugged process, use process_terminate.
- **debug_restart** — Restart debugging the current session
- **debug_attach** — Attach the debugger to a running process by name or PID
- **debug_break** — Break (pause) the debugger at the current execution point
- **debug_continue** — Continue (resume) execution after a breakpoint or break
- **debug_step** — Step through code. direction: over (F10), into (F11), or out (Shift+F11)
- **debug_get_callstack** — Get the current call stack of the active thread
- **debug_get_locals** — Get the local variables in the current stack frame
- **debug_get_threads** — Get all threads in the current debug session
- **debug_get_mode** — Get the current debugger mode (Design, Running, or Break)
- **debug_evaluate** — Read-only evaluate an expression in the current debug context — no side effects (no assignments, no method calls that mutate state). Must be in break mode. Use this to inspect variable values. For expressions WITH side effects (assignments, mutating method calls) use immediate_execute. To persist an expression that re-evaluates across breaks, use watch_add.

## Breakpoint

- **breakpoint_set / _remove / _list / _enable** — (unchanged from pre)

## Watch

- **watch_add** — Add a persistent watch expression to the VS Watch window and return its current value. The expression is remembered across breaks (use watch_list to see all). Only works in break mode. For a one-shot read-only evaluation, use debug_evaluate; for a one-shot expression with side effects, use immediate_execute.
- **watch_remove / _list** — (unchanged)

## Thread / Process

- **thread_switch / _set_frozen / _get_callstack** — (unchanged)
- **process_list_debugged / _list_local** — (unchanged)
- **process_detach** — Detach the debugger from a specific debugged process WITHOUT terminating it — the process keeps running freely. Use this when the user wants to release a debug attachment but keep the app running. For ending the whole debug session normally use debug_stop; to force-kill a process use process_terminate.
- **process_terminate** — DESTRUCTIVE: force-kill a specific debugged process by PID. This is NOT the normal 'stop debugging' action — for that, use debug_stop. Only use process_terminate when the user explicitly wants to kill a specific PID, or when debug_stop is not appropriate (e.g. unresponsive process). To detach the debugger without killing, use process_detach.

## Immediate

- **immediate_execute** — Execute an expression WITH side effects in the debugger context (like the VS Immediate Window) — assignments, mutating method calls, etc. Must be in break mode. For read-only inspection of a value without side effects, prefer debug_evaluate. To persist an expression that re-evaluates each break, use watch_add.

## Module / Register / Exception / Memory / Parallel / Diagnostics

- (unchanged from pre)

## Output

- **output_write** — (unchanged)
- **output_read** — Read the content of a Visual Studio Output window pane (Build / Debug / Test / etc. — the panes shown in VS's Output tool window). Supports localized pane names. Call without pane parameter to list available panes. Returns the last 'tail' lines by default (200). Use tail=0 to read all content. Use 'pattern' to filter lines by regex. For the stdout/stderr of a debugged console application (the actual console window, not the VS Output pane), use console_read instead.
- **output_clear** — (unchanged)
- **error_list_get** — Get all items currently shown in the Visual Studio Error List window — includes errors/warnings/messages from any source: build, IntelliSense, analyzers, XAML, etc. Use this when the user wants 'everything in the Error List'. For build-only errors collected after a build, prefer get_build_errors.

## Console

- **console_read / _send / _get_info** — (unchanged — these already had differentiating phrasing)

## Web — [Browser DOM — connected via web_connect]

- **web_connect** — [Browser DOM] Connect to a Chrome/Edge (CDP) or Firefox (RDP) instance for web debugging. Auto-detects browser type by default. Call this before any other web_* tool. Unrelated to ui_* tools which target Win32/WPF desktop apps.
- **web_disconnect / _status / _navigate** — [Browser DOM — connected via web_connect] (prefix added)
- **web_screenshot** — [Browser DOM — connected via web_connect] Capture a screenshot of the current browser page. Returns the image as base64. For screenshots of a debugged desktop app use ui_capture_window instead.
- **web_dom_get** — [Browser DOM — connected via web_connect] Get the DOM tree of the current browser page with configurable depth. For the UI tree of a debugged desktop app use ui_get_tree or ui_snapshot instead.
- **web_dom_query** — [Browser DOM — connected via web_connect] Query DOM elements in the current browser page using a CSS selector. ... For UI elements in a debugged desktop app use ui_find_elements (Name/AutomationId/ClassName/ControlType) instead.
- **web_console** — [Browser DOM — connected via web_connect] Manage browser DevTools console messages (console.log/warn/error from the page). ... For the VS Output window use output_read; for a debugged console application's stdout use console_read.
- **web_js_execute** — [Browser DOM — connected via web_connect] Execute JavaScript in the browser page context. Supports await for promises. For evaluating C#/.NET expressions in a debugged process use debug_evaluate (read-only) or immediate_execute (side effects).
- **web_network** — [Browser DOM — connected via web_connect] Manage browser network monitoring (HTTP requests issued by the page). ...
- **web_element_click** — [Browser DOM — connected via web_connect] Click a DOM element in the current browser page, located by CSS selector (uses JavaScript click). For clicking a UI element in a debugged desktop app use ui_click (AutomationId/Name) instead.
- **web_element_set_value** — [Browser DOM — connected via web_connect] Set the value of an <input>/<textarea> in the current browser page, located by CSS selector. Uses native setter for React compatibility. For setting a value on a UI element in a debugged desktop app use ui_set_value (ValuePattern) instead.

## UI — [Windows UIA — desktop app being debugged]

- **ui_capture_window** — [Windows UIA — desktop app being debugged] Capture a screenshot of the debugged application's main window as a base64 PNG image. For web pages use web_screenshot instead.
- **ui_capture_region** — [Windows UIA — desktop app being debugged] Capture a screenshot of a specific region of the debugged application's window. For web pages use web_screenshot instead.
- **ui_snapshot** — [Windows UIA — desktop app being debugged] Capture a compact semantic snapshot of the debugged application's main window in a single call. ... For web pages use web_dom_get + web_screenshot instead.
- **ui_get_tree** — [Windows UIA — desktop app being debugged] Get the raw UI element tree of the debugged application's main window. Prefer ui_snapshot unless you specifically need the unpruned tree. For web pages use web_dom_get instead.
- **ui_find_elements** — [Windows UIA — desktop app being debugged] Find UI elements matching specified criteria (Name / AutomationId / ClassName / ControlType) in the debugged desktop application. ... For browser DOM elements use web_dom_query (CSS selectors) instead.
- **ui_wait_for_element / ui_wait_idle** — [Windows UIA — desktop app being debugged] (prefix added)
- **ui_get_element** — [Windows UIA — desktop app being debugged] Get detailed properties of a specific UI element by its AutomationId. For browser DOM elements use web_dom_query with returnType='attributes'.
- **ui_click** — [Windows UIA — desktop app being debugged] Click a UI element in a Win32/WPF/WinForms application by AutomationId, Name, or screen coordinates. ... For clicking DOM elements in a browser page use web_element_click (CSS selector) instead.
- **ui_double_click / ui_right_click / ui_drag / ui_mouse_wheel** — [Windows UIA — desktop app being debugged] (prefix + cross-cluster reference added)
- **ui_set_value** — [Windows UIA — desktop app being debugged] Set the value of a UI element (e.g. WPF/WinForms text input) using ValuePattern. For setting an <input> value in a browser page use web_element_set_value (CSS selector) instead.
- **ui_invoke** — [Windows UIA — desktop app being debugged] Invoke the default action on a UI element (e.g. click a WPF/WinForms button) using InvokePattern. For browser DOM elements use web_element_click instead.
- **ui_send_keys** — [Windows UIA — desktop app being debugged] Send keyboard input via Win32 SendInput to the debugged desktop application's foreground window. ... For typing into a browser page use web_element_set_value or web_js_execute with KeyboardEvent.

## Test / NuGet / Navigation / SolutionExplorer

- (unchanged from pre)
