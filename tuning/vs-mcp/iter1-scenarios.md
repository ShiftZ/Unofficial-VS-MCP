# Iter 1 シナリオセット

Focused subset mode で評価。

## Subset under tuning (Tier 1 — 44 tools)

完全な description+params を dispatch contract に含める対象：

- **UI cluster (15)**: ui_capture_window, ui_capture_region, ui_snapshot, ui_get_tree, ui_find_elements, ui_wait_for_element, ui_wait_idle, ui_get_element, ui_click, ui_double_click, ui_right_click, ui_drag, ui_mouse_wheel, ui_set_value, ui_invoke, ui_send_keys
- **Web cluster (12)**: web_connect, web_disconnect, web_status, web_navigate, web_screenshot, web_dom_get, web_dom_query, web_console, web_js_execute, web_network, web_element_click, web_element_set_value
- **Debug critical (8)**: debug_start, debug_start_without_debugging, debug_stop, debug_restart, debug_break, debug_continue, debug_attach, debug_evaluate
- **Process critical (4)**: process_detach, process_terminate, process_list_debugged, process_list_local
- **Editor + Output disambig (10)**: file_read, file_write, file_edit, file_open, get_active_document, find_in_files, output_read, output_write, error_list_get, immediate_execute
- **Build (3)**: build_solution, build_project, get_build_errors
- **Navigation / fallback (3)**: code_find_references, code_goto_definition, execute_command
- **Edit preview (3)**: edit_preview, edit_approve, edit_reject
- **Watch (1)**: watch_add
- **Console (1)**: console_read

合計: 60 ツール（Tier 1）。残り 55 ツールは Tier 2（name: gist のみ）として混ぜる。

Subset size = 60。Scenario count target = `max(10, ceil(60 × 0.6)) = 36`。Hold-out = `max(2, ceil(36 × 0.2)) = 8`。

---

## Main scenarios（subagent に提示する。順序は最終提示時にシャッフル）

### Median — utterance に対応するツールが明確 (12)

1. "Build the entire solution." → expected: `build_solution`, args: `{}`
2. "Set a breakpoint at line 42 of Program.cs" → expected: `breakpoint_set`, args: `{file: 'Program.cs', line: 42}` — **out-of-subset OK if subagent finds it via Tier 2**
3. "Take a screenshot of the WPF window I'm debugging." → expected: `ui_capture_window` (alt: `ui_snapshot` with screenshot)
4. "Take a screenshot of the page I'm viewing in Chrome." → expected: `web_screenshot`
5. "Read the contents of C:\\foo\\bar.cs" → expected: `file_read`, args: `{path: 'C:\\foo\\bar.cs'}`
6. "Find references to the symbol MyClass.SomeMethod at Foo.cs line 10 column 18." → expected: `code_find_references`, args: `{path: 'Foo.cs', line: 10, column: 18}`
7. "Run all unit tests in the solution." → expected: `test_run` — **out-of-subset; subagent should locate via Tier 2 or refuse with low confidence**
8. "List the projects in this solution." → expected: `project_list` — **out-of-subset**
9. "What's the current debugger mode?" → expected: `debug_get_mode` — **out-of-subset**
10. "Search for the literal string 'TODO' across all .cs files." → expected: `find_in_files`, args: `{query: 'TODO', filePattern: '*.cs'}`
11. "Connect to Chrome on port 9222." → expected: `web_connect`, args: `{browser: 'chrome', port: 9222}`
12. "Click the .submit-btn element on the current page." → expected: `web_element_click`, args: `{selector: '.submit-btn'}`

### Neighbor-collision — disambiguation テスト (12)

13. **[critical]** "Just run the app, I don't need to debug." → expected: `debug_start_without_debugging`, args: `{}`
14. **[critical]** "Start debugging the app." → expected: `debug_start`, args: `{}`
15. **[critical]** "Stop debugging." → expected: `debug_stop`, args: `{}`
16. **[critical]** "Detach the debugger from PID 5678 but keep the process running." → expected: `process_detach`, args: `{processId: 5678}`
17. **[critical]** "Force-kill the debugged process with PID 12345." → expected: `process_terminate`, args: `{processId: 12345}`
18. "What's the value of myVariable in the debugger?" → expected: `debug_evaluate`, args: `{expression: 'myVariable'}`
19. "Set myVariable to 42 in the debugger." → expected: `immediate_execute`, args: `{expression: 'myVariable = 42'}`
20. "Show me the build errors from the last build." → expected: `get_build_errors`, args: `{}`
21. "Show me everything in the Visual Studio Error List." → expected: `error_list_get`, args: `{}` (alt: `get_build_errors` if subagent picks it — log as collision)
22. "Tell me what file is currently focused in the editor." → expected: `get_active_document`, args: `{}`
23. "Click the OK button in the desktop WPF app I'm debugging." → expected: `ui_click`, args: `{name: 'OK'}` (alt: `ui_invoke` with AutomationId — both acceptable since user didn't specify)
24. "Click submit." → expected: **None or low-confidence**, surface ambiguous (UIA vs Web). Acceptable: low-confidence pick of either with explicit note about ambiguity.

### Negative — expected = None (4)

25. "Push this branch to origin." → expected: `None`
26. "Create a new GitHub issue for this bug." → expected: `None`
27. "Open Slack and send a message to the team." → expected: `None`
28. "Commit the current changes with message 'fix: foo'." → expected: `None`

### Underspecified — low confidence expected (4)

29. "Open the file." → expected: low confidence on `file_open` (path missing — should ask)
30. "Add a watch." → expected: low confidence on `watch_add` (expression missing)
31. "Click the button." → expected: low confidence; surface ambiguous AND no identifier
32. "Run the tests." → expected: low confidence on `test_run` — out-of-subset, may also refuse

### Critical-confusion targeted (4)

33. **[critical]** "Stop the debuggee." → expected: `debug_stop` (alt: NONE allowed because process_terminate is destructive). Tests whether agent picks safer default.
34. **[critical]** "End the debug session." → expected: `debug_stop`
35. **[critical]** "Press Ctrl+F5." → expected: `debug_start_without_debugging`
36. **[critical]** "Press F5." → expected: `debug_start`

`[critical]` total: 8 (13, 14, 15, 16, 17, 33, 34, 35, 36) — wait that's 9. Let me re-count: 13, 14, 15, 16, 17, 33, 34, 35, 36 = 9 critical scenarios. Good — convergence-check requires ALL `[critical]` to be ○ to declare success.

**Note on [critical] acceptable_alternatives**: per skill rule, `[critical]` scenarios MUST NOT have `acceptable_alternatives`. Verified above — all `[critical]` have exactly one expected_tool (or None).

---

## SEALED — hold-out scenarios（convergence-check time only — DO NOT show to iter 1 subagent）

これらは Iter 1 開始前に確定して、subagent には絶対に見せない。convergence check 時のみ評価する。

H1. "Detach the debugger but keep the program running." → expected: `process_detach`
H2. "Take a snapshot of the WPF UI tree without the screenshot." → expected: `ui_snapshot`, args: `{includeScreenshot: false}`
H3. "Print the value of foo.Bar.Baz in the debugger console without changing anything." → expected: `debug_evaluate`, args: `{expression: 'foo.Bar.Baz'}`
H4. "Type 'hello world' into the AutomationId='UserNameTextBox' field of the desktop app I'm debugging." → expected: `ui_set_value`, args: `{automationId: 'UserNameTextBox', value: 'hello world'}`
H5. **[critical]** "Kill the debugged process — it's frozen." → expected: `process_terminate` (the user explicitly said kill and frozen → process_terminate IS appropriate here)
H6. "List network requests the page has made." → expected: `web_network`, args: `{action: 'get'}`
H7. "Show me how many files reference the type Foo." → expected: low confidence — find_in_files (literal text search OK), or out-of-subset symbol-aware tool. Acceptable: either.
H8. "Refresh the page." → expected: `web_navigate` with current URL OR `web_js_execute` with location.reload() — log which the description leads to.

---

## Answer key補足

- **acceptable_alternatives**: scenario 21（error_list_get / get_build_errors）と 23（ui_click / ui_invoke）のみ。`[critical]` には含めない（前述）。
- **out-of-subset OK**: 2, 7, 8, 9 — Tier 2 から見つけられたら ○、refuse なら △（under-trigger）、別の subset-internal tool を呼んだら × (Other-column)。
- **None 期待時の confidence**: 高/中で None を返したら ○、低で None なら ○ だが confidence-calibration メタとして記録。

---

## Iter 1 dispatch 計画

1. Dispatch contract に Tier 1 (60 tools, 完全な description+params) と Tier 2 (55 tools, name+一行 gist) を alphabetical mix で渡す
2. 36 scenarios を 1, 2, 3... の順で並べ替えず（scenario index は別にして）、シャッフルしてから提示
3. Subagent は dry-run（actual MCP tool は呼ばない）。各 scenario の per-scenario response structure を要求
4. 戻ってきたら confusion matrix（tool-level + cluster-level overlay UIA×Web×Other×None）を構築
5. 結果を `iter1-results.md` に保存
6. 整合性テーブル §1〜§3 の予想と subagent の実測がどれだけ一致したか比較し、次の iter 2 fix theme を決定
