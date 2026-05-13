# Iter 0 — vs-mcp 整合性テーブル

`snapshot-iter0-pre.md` を基に collision / gap / orphan / cluster-boundary を洗い出した結果。
Iter 0 の reconciliation edit に進む対象に ✅ を付ける。

---

## 1. Cluster-boundary collisions (UIA × Browser DOM)

vs-mcp は 2 つの異なる surface — Windows UI Automation（デバッグ対象の **デスクトップアプリ**）と Chrome/Edge/Firefox の **Browser DOM**（CDP/RDP 経由）— に対する操作群を持つ。エージェントは「Click ...」「Find element ...」のような汎用句で両方を選びうる。

| tool A (UIA) | tool B (Browser) | 同じ utterance で混乱しうるか | 対応 |
|---|---|---|---|
| `ui_click` | `web_element_click` | ✅ "Click the button" | ✅ surface tag |
| `ui_set_value` | `web_element_set_value` | ✅ "Set the text field to 'foo'" | ✅ surface tag |
| `ui_find_elements` | `web_dom_query` | ✅ "Find the OK button / element with class X" | ✅ surface tag |
| `ui_get_tree` / `ui_snapshot` | `web_dom_get` | ✅ "Show me the UI tree" | ✅ surface tag |
| `ui_capture_window` / `ui_capture_region` | `web_screenshot` | ✅ "Take a screenshot" | ✅ surface tag |
| `ui_send_keys` | `web_js_execute` (KeyboardEvent) | ⚠️ "Press Enter" | ✅ surface tag |
| `ui_wait_for_element` | （該当なし） | — | ✅ surface tag（UIA 側のみ） |

**判定**: 単一のクラスタ境界問題。**symmetric pair editing 1 回** で対応 — 全 ui_* に `[Windows UIA — desktop app being debugged]` 接頭辞、全 web_* に `[Browser DOM — connected via web_connect]` 接頭辞を front-load する。Failure ledger の "Cluster-tag missing" パターン。

---

## 2. Same-verb prefix collisions（critical 含む）

| collision | 関係 | severity | 対応 |
|---|---|---|---|
| `debug_start` vs `debug_start_without_debugging` | F5 vs Ctrl+F5 — デバッガ attach の有無で挙動が決定的に異なる | **[critical]** | ✅ "do NOT use" 句、Ctrl+F5 を前置 |
| `debug_stop` vs `process_detach` vs `process_terminate` | プロセスを終わらせる方法が 3 系統。terminate は debuggee を kill | **[critical]** | ✅ 各々に "use X instead when ..." |
| `error_list_get` vs `get_build_errors` | 前者は VS Error List 全件（IntelliSense 警告含む）、後者はビルドが出した分のみ | medium | ✅ 差分を description に明記 |
| `file_read` vs `get_active_document` | "show what's open" で両方候補化 | medium | ✅ "currently open in editor" vs "any file" |
| `file_write` vs `file_edit` vs `edit_preview` | 全体置換 / 部分置換 / プレビュー付き | low（既に区別記述あり） | ✅ edit_preview の "for approval" を強調 |
| `debug_evaluate` vs `immediate_execute` vs `watch_add` | 副作用 vs 純粋評価 vs 永続化 | medium | ✅ side-effect の有無を front-load |
| `debug_get_callstack` vs `thread_get_callstack` vs `parallel_stacks` | アクティブスレッド / 特定 ID / 全スレッド | low | ✅ scope を front-load |
| `build_solution` vs `build_project` vs `rebuild` | 全体 / 単一 / clean+build | low（差分は既に明確） | — |
| `output_read` vs `console_read` | VS Output pane vs debuggee の本物のコンソール | medium（既に console_read 側のみ記述） | ✅ output_read 側にも対称的に追記 |

---

## 3. Negative space gaps（「使わない時」が明示されていない）

| tool | missing negative clause | 影響 |
|---|---|---|
| `execute_command` | "汎用フォールバック。専用ツールがあるならそちらを優先" がない | 高（広義すぎて他ツールの scenario を吸収しうる） |
| `ui_get_tree` | "Prefer ui_snapshot unless you specifically need raw tree" がない | medium（ui_snapshot 側にはある — asymmetric） |
| `output_read` | "console アプリの stdout は console_read を使う" がない | medium |
| `file_write` | "destructive — use edit_preview if user needs to review" がない | medium |
| `process_terminate` | "use debug_stop for normal flow, this kills" がない | **[critical]** に近い |
| `breakpoint_set` | "to disable without removing, use breakpoint_enable" がない | low |
| `nuget_install` | "use nuget_update when already installed" がない | low |

---

## 4. Implicit prerequisites（break mode 必須）

「Must be in break mode」の表記揺れ — 言及されてるツールと、されてないツールの混在：

- ✅ 言及あり: `debug_evaluate`, `immediate_execute`, `module_list`, `register_list`, `register_get`, `memory_read`, `parallel_stacks`, `parallel_watch`, `parallel_tasks_list`, `watch_add`, `watch_list`
- ❌ 言及なし: `debug_get_locals`（break mode 必須なのに記述なし）, `debug_step`（break mode 必須）, `debug_continue`（break mode 必須）

判定: 表記揺れ統一は iter 0 の "naming inconsistency" 寄り — symmetric edit には含めず iter 1+ で出れば対応。**今回は iter 0 では触らない**。

---

## 5. Verb mismatch / over-broad

- `execute_command` — VS の任意コマンドを実行できる "Build.BuildSolution" まで含む。`build_solution` 専用ツールがある状況で本ツールが吸収しないよう、negative space 追加。
- `web_console` / `web_network` — `action` enum で 3 サブ機能を持つ "Manage" 動詞。意味は明確だが "Show me console output" などで他と曖昧化しうる。Iter 1+ で signal が出れば対応。

---

## 6. Orphans / Cross-cluster

- `find_in_files` (Editor) — `code_find_references` (Navigation) と "find" 動詞でかぶる。前者はテキスト検索、後者は symbol references。
  - 対応: `find_in_files` description に "text search across files (not symbol-aware — use code_find_references for symbols)" を追加 ✅
- `error_list_get` (Output) vs `get_build_errors` (Build) — 上記 §2 と重複。
- `parallel_tasks_list` (Parallel) vs `debug_get_threads` (Debugger) — TPL Task は OS Thread と別概念。description に "Task != Thread" を明記。

---

## 7. Tool boundary suspicion

以下は description fix で済まず、tool 構造の見直しが必要かもしれない候補（iter 1+ で confusion matrix が 3+ iter hot のままならエスカレート）：

- `error_list_get` と `get_build_errors` を 1 本化するか、明確な責務分担を入れるか
- `debug_evaluate` と `immediate_execute` を `expression_evaluate(side_effects: bool)` で 1 本化する案
- `web_console` / `web_network` の `action` パラメータ分割（enable/get/clear を別ツールに）

**今回は触らない**。iter 1 の confusion 結果を見てから判断。

---

## Iter 0 reconciliation 計画

以下を symmetric pair editing **1 回**（cap 適用）として実施：

### Theme A: Surface-tag front-loading（symmetric across UIA / Web clusters）

ui_* 全 15 ツールに `[Windows UIA — desktop app being debugged] ` を、web_* 全 12 ツールに `[Browser DOM — connected via web_connect] ` を description 先頭に追加。

### Theme B: Critical disambiguation（per-tool negative clauses）

- `debug_start` / `debug_start_without_debugging` — お互いの存在を明示し、F5/Ctrl+F5 を front-load
- `debug_stop` / `process_detach` / `process_terminate` — 3 者の使い分けを各 description に
- `error_list_get` / `get_build_errors` — 差分を明記
- `file_read` / `get_active_document` — 使い分けを明記
- `debug_evaluate` / `immediate_execute` — side effects の有無を front-load
- `execute_command` — "fallback for VS commands without a dedicated tool" を front-load
- `find_in_files` — "text search, not symbol-aware" を追記
- `output_read` / `console_read` — output pane vs debuggee console の対称的記述
- `ui_get_tree` — "prefer ui_snapshot" を追記
- `process_terminate` — "kills debuggee — use debug_stop for normal flow" を追記

これは per-tool 編集なので "1 theme" にカウントせず、Theme A と Theme B を iter 0 のバンドルに含める。

### あえて触らないもの（iter 1 の signal 待ち）
- break-mode 表記揺れの統一
- web_console / web_network の action 分割
- error_list_get / get_build_errors の構造的統合
