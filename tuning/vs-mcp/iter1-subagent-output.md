=== S01 ===
Chosen tool: build_solution
Arguments: {}
Confidence: high
Alternatives considered:
- (build_project, user said "entire solution", not a specific project)
- (rebuild, user said "build", not "rebuild" — rebuild also cleans first)
- (execute_command, dedicated tool exists so fallback is wrong)
Driving words: "Build the entire solution", "use this instead of running MSBuild"
Unhelpful words: none
Missing information: none

=== S02 ===
Chosen tool: breakpoint_set
Arguments: {"path": "Program.cs", "line": 42}
Confidence: high
Alternatives considered:
- (breakpoint_enable, only toggles existing breakpoints)
- (breakpoint_list, read-only listing)
- (execute_command, dedicated tool exists)
Driving words: "Set a breakpoint at a file+line"
Unhelpful words: none
Missing information: Parameter list/types for breakpoint_set are not spelled out (just a gist), so I had to infer that it accepts `path` and `line`. Also: should `path` be absolute? Workspace-relative accepted?

=== S03 ===
Chosen tool: ui_capture_window
Arguments: {}
Confidence: high
Alternatives considered:
- (ui_capture_region, user wants whole window, not a region)
- (web_screenshot, user said WPF — not browser; description explicitly redirects)
- (ui_snapshot, includes screenshot but also returns full UIA tree, more than asked)
Driving words: "Windows UIA — desktop app being debugged", "main window as a base64 PNG"
Unhelpful words: none
Missing information: none

=== S04 ===
Chosen tool: web_screenshot
Arguments: {}
Confidence: high
Alternatives considered:
- (ui_capture_window, redirected — that's for desktop UIA)
- (web_dom_get, returns DOM, not an image)
- (web_status, status only)
Driving words: "Capture a screenshot of the current browser page", "[Browser DOM]"
Unhelpful words: none
Missing information: Whether web_connect must precede this — described elsewhere ("connected via web_connect"). Description is fine.

=== S05 ===
Chosen tool: file_read
Arguments: {"path": "C:\\foo\\bar.cs"}
Confidence: high
Alternatives considered:
- (get_active_document, only the currently focused doc — user specified a path)
- (file_open, opens in editor, doesn't return contents)
- (find_in_files, search, not a single-file read)
Driving words: "Read the contents of a file at a given path", "does not need to be open in the editor"
Unhelpful words: none
Missing information: none

=== S06 ===
Chosen tool: code_find_references
Arguments: {"path": "Foo.cs", "line": 10, "column": 18}
Confidence: high
Alternatives considered:
- (find_in_files, not symbol-aware — description explicitly redirects)
- (code_goto_definition, navigates to definition, not references)
- (code_goto_implementation, interface/abstract implementation, not references)
Driving words: "Find all references of a symbol at the specified position"
Unhelpful words: none
Missing information: Whether `path` requires absolute. Assuming yes ("Full path to the source file"). I would normally prepend cwd, but the user gave a relative path; the description says "full path".

=== S07 ===
Chosen tool: test_run
Arguments: {}
Confidence: high
Alternatives considered:
- (test_discover, only discovers; doesn't run)
- (test_results, reads results of a previous run)
- (build_solution, doesn't run tests)
Driving words: "Run tests and get results"
Unhelpful words: none
Missing information: Whether the parameterless call runs ALL tests vs requires a filter. Tier 2 gist is too thin to confirm — but contextually "Run tests" without args likely means all.

=== S08 ===
Chosen tool: project_list
Arguments: {}
Confidence: high
Alternatives considered:
- (solution_info, gives solution overview but maybe not project list)
- (project_info, single project only)
- (get_status, broader status; may not enumerate projects)
Driving words: "List all projects in the current solution"
Unhelpful words: none
Missing information: none

=== S09 ===
Chosen tool: debug_get_mode
Arguments: {}
Confidence: high
Alternatives considered:
- (get_status, broader but description says "solution, active doc, debugger mode" — would also work)
- (debug_get_threads, returns threads not the mode)
- (process_list_debugged, processes not mode)
Driving words: "Get the current debugger mode (Design, Running, or Break)"
Unhelpful words: none
Missing information: Whether the user expects ONLY the mode or richer info. debug_get_mode is the precise tool.

=== S10 ===
Chosen tool: find_in_files
Arguments: {"query": "TODO", "filePattern": "*.cs"}
Confidence: high
Alternatives considered:
- (code_find_references, symbol-aware; "TODO" is literal text)
- (grep/Grep tool, not in MCP catalog)
- (file_read, single file only)
Driving words: "Text search across files", "literal text or regex"
Unhelpful words: none
Missing information: Whether `filePattern` accepts glob like "*.cs" — best guess.

=== S11 ===
Chosen tool: web_connect
Arguments: {"browser": "chrome", "port": 9222}
Confidence: high
Alternatives considered:
- (web_navigate, only after connecting)
- (web_status, status check only)
- (none)
Driving words: "Connect to a Chrome/Edge (CDP)", "Call this before any other web_* tool"
Unhelpful words: none
Missing information: none

=== S12 ===
Chosen tool: web_element_click
Arguments: {"selector": ".submit-btn"}
Confidence: high
Alternatives considered:
- (ui_click, desktop UIA — description redirects to web_element_click)
- (web_js_execute, would work but is the manual approach; dedicated tool exists)
- (web_dom_query, query only, no click)
Driving words: "Click a DOM element in the current browser page, located by CSS selector"
Unhelpful words: none
Missing information: none

=== S13 ===
Chosen tool: debug_start_without_debugging
Arguments: {}
Confidence: high
Alternatives considered:
- (debug_start, attaches debugger — user explicitly doesn't want that)
- (execute_command, dedicated tool exists)
- (none)
Driving words: "Ctrl+F5: run the startup project WITHOUT the debugger attached", "Use this when the user wants to just run the app"
Unhelpful words: none
Missing information: none

=== S14 ===
Chosen tool: debug_start
Arguments: {}
Confidence: high
Alternatives considered:
- (debug_start_without_debugging, ignores breakpoints — wrong for "start debugging")
- (debug_attach, attaches to an EXISTING process; user wants to start)
- (debug_restart, only if a session is in progress)
Driving words: "F5: start the startup project WITH the debugger attached", "Use this when the user wants to debug"
Unhelpful words: none
Missing information: none

=== S15 ===
Chosen tool: debug_stop
Arguments: {}
Confidence: high
Alternatives considered:
- (process_terminate, force-kill — description says it's NOT the normal stop)
- (process_detach, leaves process running — user wants to STOP debugging, normal interpretation is end session)
- (debug_break, only pauses)
Driving words: "Stop the current debug session normally (Shift+F5)", "default 'stop debugging' action"
Unhelpful words: none
Missing information: none

=== S16 ===
Chosen tool: process_detach
Arguments: {"processId": 5678}
Confidence: high
Alternatives considered:
- (debug_stop, terminates the debuggee — user wants to keep it running)
- (process_terminate, force-kill — opposite of what's asked)
- (debug_continue, just resumes execution)
Driving words: "Detach the debugger from a specific debugged process WITHOUT terminating it — the process keeps running freely"
Unhelpful words: none
Missing information: none

=== S17 ===
Chosen tool: process_terminate
Arguments: {"processId": 12345}
Confidence: high
Alternatives considered:
- (debug_stop, normal stop; description says use this only when debug_stop isn't appropriate or user explicitly wants to kill a PID — here user explicitly says "force-kill")
- (process_detach, doesn't kill)
- (none)
Driving words: "DESTRUCTIVE: force-kill a specific debugged process by PID", "when the user explicitly wants to kill a specific PID"
Unhelpful words: none
Missing information: none

=== S18 ===
Chosen tool: debug_evaluate
Arguments: {"expression": "myVariable"}
Confidence: high
Alternatives considered:
- (immediate_execute, has side effects — user just wants to read)
- (watch_add, persistent watch; user asked one-shot)
- (debug_get_locals, returns all locals; user named one)
Driving words: "Read-only evaluate an expression in the current debug context — no side effects", "Use this to inspect variable values"
Unhelpful words: none
Missing information: none

=== S19 ===
Chosen tool: immediate_execute
Arguments: {"expression": "myVariable = 42"}
Confidence: high
Alternatives considered:
- (debug_evaluate, explicitly says NO assignments)
- (watch_add, doesn't perform assignments)
- (memory_read, reads bytes only)
Driving words: "Execute an expression WITH side effects in the debugger context", "assignments, mutating method calls"
Unhelpful words: none
Missing information: none

=== S20 ===
Chosen tool: get_build_errors
Arguments: {}
Confidence: high
Alternatives considered:
- (error_list_get, full Error List includes IntelliSense+analyzers — user asked specifically for build errors)
- (output_read, raw build output, less structured)
- (none)
Driving words: "Get build-produced errors and warnings (MSBuild/compiler output)", "Call this right after build_solution or build_project"
Unhelpful words: none
Missing information: none

=== S21 ===
Chosen tool: error_list_get
Arguments: {"severity": "all"}
Confidence: high
Alternatives considered:
- (get_build_errors, build-only — user wants "everything in the Error List")
- (output_read, output pane, not Error List)
- (none)
Driving words: "everything in the Error List", "includes errors/warnings/messages from any source: build, IntelliSense, analyzers, XAML"
Unhelpful words: none
Missing information: none

=== S22 ===
Chosen tool: get_active_document
Arguments: {}
Confidence: high
Alternatives considered:
- (file_read, requires a specific path)
- (get_status, broader; description says it gives "solution, active doc, debugger mode" — would also work but less precise)
- (none)
Driving words: "document currently focused in the VS editor", "what file is the user looking at right now"
Unhelpful words: none
Missing information: none

=== S23 ===
Chosen tool: ui_click
Arguments: {"name": "OK"}
Confidence: high
Alternatives considered:
- (ui_invoke, only takes AutomationId — user gave a Name, not an AutomationId; if button has stable AutomationId, ui_invoke would also work but we don't have it)
- (web_element_click, browser DOM, wrong cluster)
- (ui_send_keys, would need focus + 'enter' — clumsier)
Driving words: "Click a UI element in a Win32/WPF/WinForms application by AutomationId, Name, or screen coordinates"
Unhelpful words: none
Missing information: Whether `name` does exact or substring matching by default. ui_find_elements has match modes; ui_click doesn't say.

=== S24 ===
Chosen tool: None
Arguments: N/A
Confidence: low
Alternatives considered:
- (ui_click, would fit if user is in a WPF/WinForms app — but no context given)
- (web_element_click, would fit if user means a browser — but no context given)
- (ui_invoke, requires AutomationId we don't have)
Driving words: Both ui_click and web_element_click descriptions emphasize their cluster ("Win32/WPF/WinForms" vs "browser DOM"), and "Click submit" is ambiguous between them.
Unhelpful words: none — descriptions correctly disambiguate
Missing information: User did not say WHERE — desktop app vs browser. In a real session I'd ask before choosing.

=== S25 ===
Chosen tool: None
Arguments: N/A
Confidence: high
Alternatives considered: none
Driving words: vs-mcp is a Visual Studio automation server. No git/push tool in catalog. execute_command is for VS commands only.
Unhelpful words: none
Missing information: none — this is correctly out-of-scope for vs-mcp.

=== S26 ===
Chosen tool: None
Arguments: N/A
Confidence: high
Alternatives considered: none
Driving words: No GitHub-issue tool in catalog. execute_command targets VS commands.
Unhelpful words: none
Missing information: none — out-of-scope for vs-mcp.

=== S27 ===
Chosen tool: None
Arguments: N/A
Confidence: high
Alternatives considered: none
Driving words: No Slack/messaging tool. ui_* tools target debugged desktop apps, not arbitrary Win32 apps like Slack.
Unhelpful words: none
Missing information: none — out-of-scope for vs-mcp.

=== S28 ===
Chosen tool: None
Arguments: N/A
Confidence: high
Alternatives considered: none
Driving words: No git/commit tool. execute_command runs VS commands, not git CLI.
Unhelpful words: none
Missing information: none — out-of-scope for vs-mcp.

=== S29 ===
Chosen tool: None
Arguments: N/A
Confidence: low
Alternatives considered:
- (file_open, needs a `path`; user didn't say which file)
- (solution_open, needs a path too; user didn't say it's a solution)
- (get_active_document, doesn't "open" anything)
Driving words: file_open / solution_open both require a path; user did not give one.
Unhelpful words: none
Missing information: User didn't specify which file. In a real session I'd ask "which file?" before calling file_open.

=== S30 ===
Chosen tool: watch_add
Arguments: {"expression": "<unspecified>"}
Confidence: low
Alternatives considered:
- (watch_list, just lists)
- (debug_evaluate, one-shot)
- (immediate_execute, mutating)
Driving words: "Add a persistent watch expression to the VS Watch window"
Unhelpful words: none
Missing information: User did not provide the expression to watch. In a real session I'd ask for the expression text before calling watch_add.

=== S31 ===
Chosen tool: None
Arguments: N/A
Confidence: low
Alternatives considered:
- (ui_click, would fit if desktop app and we knew which button — neither given)
- (web_element_click, would fit if browser and we had a selector — neither given)
- (ui_invoke, needs AutomationId)
Driving words: ui_* vs web_* descriptions disambiguate cluster, but "the button" is target-less.
Unhelpful words: none — the descriptions are clear; the user's prompt is what's underspecified
Missing information: Which button (Name/AutomationId/selector/coords) and which surface (desktop UIA vs browser DOM). In a real session I'd ask before choosing.

=== S32 ===
Chosen tool: test_run
Arguments: {}
Confidence: high
Alternatives considered:
- (test_discover, only discovers)
- (test_results, reads past results)
- (build_solution, builds, doesn't run tests)
Driving words: "Run tests and get results"
Unhelpful words: none
Missing information: Whether parameterless test_run runs ALL tests vs requires a filter. Tier 2 gist is thin — but contextually "Run the tests" matches the tool best.

=== S33 ===
Chosen tool: debug_stop
Arguments: {}
Confidence: medium
Alternatives considered:
- (process_terminate, force-kill — "stop the debuggee" could be read as "kill the debuggee process" specifically, but the user did not say "kill" or "force")
- (process_detach, leaves process running — user said "stop", not "detach")
- (debug_break, pause only, not stop)
Driving words: debug_stop is "the default 'stop debugging' action"; "Detaches and terminates the debuggee like clicking the VS Stop button"
Unhelpful words: "Stop the debuggee" — "debuggee" specifically refers to the process being debugged, which could nudge toward process_terminate. But the description of debug_stop explicitly says it "terminates the debuggee", so debug_stop is the right normal action.
Missing information: Whether "stop the debuggee" implies force-kill or normal stop. Description of debug_stop covers "terminates the debuggee" so I land on debug_stop. Confidence is medium because of the phrasing nuance.

=== S34 ===
Chosen tool: debug_stop
Arguments: {}
Confidence: high
Alternatives considered:
- (process_terminate, force-kill, not the normal end)
- (process_detach, doesn't end the session, just detaches)
- (debug_restart, restarts instead of ending)
Driving words: "Stop the current debug session normally", "default 'stop debugging' action"
Unhelpful words: none
Missing information: none

=== S35 ===
Chosen tool: debug_start_without_debugging
Arguments: {}
Confidence: high
Alternatives considered:
- (debug_start, that's F5, not Ctrl+F5)
- (execute_command with "Debug.StartWithoutDebugging", fallback when no dedicated tool exists — but a dedicated tool exists)
- (ui_send_keys, would send keystrokes but bypasses VS API)
Driving words: "Ctrl+F5: run the startup project WITHOUT the debugger attached"
Unhelpful words: none
Missing information: Edge case — if user meant "send the literal Ctrl+F5 keystroke to the debugged app (not VS)", ui_send_keys would be right. But the natural reading is "do the Ctrl+F5 action in VS", which is debug_start_without_debugging.

=== S36 ===
Chosen tool: debug_start
Arguments: {}
Confidence: high
Alternatives considered:
- (debug_start_without_debugging, that's Ctrl+F5)
- (debug_restart, only when a session is running)
- (ui_send_keys, would send keystrokes but bypasses VS API)
Driving words: "F5: start the startup project WITH the debugger attached"
Unhelpful words: none
Missing information: Same edge case as S35 — "Press F5" could literally mean keystroke. Natural reading is the VS action.

=== Final report ===
Catalog-wide observations:
- Most "fork-pair" tools (ui_* vs web_*, debug_start vs debug_start_without_debugging, debug_stop vs process_terminate vs process_detach, debug_evaluate vs immediate_execute vs watch_add, get_build_errors vs error_list_get, file_read vs get_active_document, find_in_files vs code_find_references, file_write vs edit_preview vs file_edit) carry explicit cross-reference clauses ("For X use Y instead when ..."). This is consistently helpful — confidence stays high across these scenarios because the negative-space wording does its job. This pattern should be standard for any new tool that overlaps semantically with another.
- All ui_* tool descriptions are prefixed with "[Windows UIA — desktop app being debugged]" and all web_* tool descriptions with "[Browser DOM — connected via web_connect]". This bracketed prefix is the single biggest disambiguation lever. Tools without an obvious surface (ui_send_keys, ui_drag, ui_mouse_wheel) all carry it, which is good.
- Tier 2 (gist-only) tools are still selectable for very on-the-nose scenarios (e.g., breakpoint_set, test_run, project_list, get_active_document) because their gist matches the user phrasing 1:1. They become problematic in two cases: (a) when parameter shapes matter (breakpoint_set — path? line? function name?), and (b) when the tool overlaps with another (debug_step has no description of which keys map to over/into/out beyond the gist).
- Parameter descriptions are uneven: file_read documents `startLine`/`endLine` only by name; ui_send_keys lists `keys`/`text` distinction in the description body, not in the parameter list. Many ui_* tools list params as a flat name list with no per-param semantics (waitMs? restoreCursor default?).
- "Must be in break mode" is mentioned on debug_evaluate, immediate_execute, watch_add, watch_list — consistent. But debug_get_locals, debug_get_callstack, parallel_watch etc. don't say it, and presumably also need break mode. Minor inconsistency.
- get_status overlaps with debug_get_mode, get_active_document, and solution_info in what it returns — agents could pick get_status by default for any "what's going on" question. Description of get_status could helpfully say "for one specific facet (debugger mode / active doc / solution), prefer the dedicated tool".

Indistinguishable pairs:
- (debug_stop, process_terminate, what would help: a clearer statement on debug_stop that it ALSO terminates the debuggee, which is already there but easy to miss. The phrasing "stop the debuggee" in user prompts can lean either way.)
- (ui_invoke, ui_click, what would help: a sentence saying "ui_click works with Name OR AutomationId OR coordinates; ui_invoke is AutomationId-only and uses InvokePattern without any cursor movement". Currently both descriptions overlap and the user has to infer the difference.)
- (debug_start_without_debugging vs ui_send_keys for "press Ctrl+F5", what would help: a note on debug_start_without_debugging saying "this performs the VS action triggered by Ctrl+F5 — it does NOT send the keystroke; for sending a literal keystroke to a debugged app use ui_send_keys".)
- (get_status, debug_get_mode, solution_info, get_active_document, what would help: get_status's description should say "use the dedicated tool when you want only one of those facets".)
- (rebuild, build_solution + clean, what would help: a note on rebuild — "equivalent to clean then build_solution; prefer build_solution unless you want to force a rebuild".)
- (test_run vs test_discover, what would help: test_run gist should say "runs all tests by default; filter by [param]" so parameterless calls are unambiguous.)

Structured reflection for problematic scenarios:

S24 "Click submit." / S31 "Click the button."
- Issue: Could not pick between ui_click and web_element_click.
- Cause: User prompt doesn't say which surface (desktop UIA vs browser DOM). Tool descriptions correctly disambiguate the surfaces, but neither tool can be picked without surface context.
- General Fix Rule: Not a description bug — this is a real ambiguity in the user prompt. In a real session, the executor should ask "is this in the WPF app or the browser?" rather than guess. No description change needed; if anything, add a note in vs-mcp's server-level instructions reminding agents to confirm the surface for click-like commands without context.

S29 "Open the file." / S30 "Add a watch." / S31 "Click the button."
- Issue: Required arguments missing from user prompt.
- Cause: The user gave a verb but no target.
- General Fix Rule: Not a description bug. Server-level guidance: "If a required parameter is unspecified, ask the user before calling — do not invent a path/expression/selector." This is more about agent harness behavior than tool descriptions.

S33 "Stop the debuggee."
- Issue: Word "debuggee" nudges toward process-level operation (process_terminate or process_detach), but the canonical action is debug_stop.
- Cause: debug_stop's description leads with "Stop the current debug session" — a user phrasing the request as "stop the debuggee" might not immediately match. The clause "Detaches and terminates the debuggee like clicking the VS Stop button" rescues it, but it's the second sentence.
- General Fix Rule: For tools with multiple common user phrasings, surface synonyms early in the description. E.g., debug_stop could open with: "Stop the current debug session / stop the debuggee normally (Shift+F5)."

S35 "Press Ctrl+F5." / S36 "Press F5."
- Issue: Literal keystroke phrasing could theoretically map to ui_send_keys instead of debug_start_*.
- Cause: debug_start and debug_start_without_debugging do call out "F5" and "Ctrl+F5", but they don't explicitly distinguish "press these keys in VS" from "send these keys to the debuggee".
- General Fix Rule: Add to debug_start / debug_start_without_debugging: "This performs the VS-level action invoked by F5 / Ctrl+F5 (it does not literally synthesize a key event); to send keystrokes to the debugged app use ui_send_keys."

Retries: 0 — I did not change any answer mid-way. The closest re-evaluations were S33 (briefly considered process_terminate before settling on debug_stop based on the second sentence of its description) and S35/S36 (briefly considered ui_send_keys before settling on debug_start_* based on the natural reading), but those did not flip the chosen tool.
