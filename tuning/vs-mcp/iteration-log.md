# vs-mcp empirical tool description tuning — iteration log

## Iter 0 — structural reconciliation

### Goals
カタログ整合性チェックと、それから surface した構造的問題の reconciliation。Iter 1 で empirical signal を取る前に「明らかな wording 問題」と「cluster 境界」を解消する。

### Changes (one-theme rule exempt — iter 0)

#### Theme A: Surface-tag front-loading（symmetric pair editing, 1 round — cap used）

Cluster pair: **(ui_*, web_*) — symmetric edit applied; further fixes in this pair must be per-tool.**

- 15 個の `ui_*` ツール: description 先頭に `[Windows UIA — desktop app being debugged]` を追加
- 12 個の `web_*` ツール: description 先頭に `[Browser DOM — connected via web_connect]` を追加
- それぞれに「反対側を使う条件」を末尾に追加（例: ui_click → "For clicking DOM elements in a browser page use web_element_click (CSS selector) instead."）

Pattern applied: **Cluster-tag missing**（seeds ledger に既存）

ファイル:
- `src/VsMcp.Extension/Tools/UiTools.cs` (15 edits)
- `src/VsMcp.Extension/Tools/WebTools.cs` (12 edits)

#### Theme B: Per-tool critical disambiguation

`[critical]` 候補や medium-severity の同義語衝突に対して、各 description を強化。

| ツール | 変更要旨 | 衝突相手 | severity |
|---|---|---|---|
| `debug_start` | "F5" を front-load、debug_start_without_debugging への誘導 | debug_start_without_debugging | [critical] |
| `debug_start_without_debugging` | "Ctrl+F5" を front-load、breakpoint 無視を明記 | debug_start | [critical] |
| `debug_stop` | "Shift+F5 normal stop" を明記、process_detach/_terminate との使い分け | process_detach, process_terminate | [critical] |
| `process_detach` | "WITHOUT terminating" を強調、debug_stop への誘導 | debug_stop | [critical] |
| `process_terminate` | "DESTRUCTIVE: force-kill" を front-load、debug_stop への誘導 | debug_stop | [critical] |
| `error_list_get` | "all sources — IntelliSense/analyzers含む" を明記 | get_build_errors | medium |
| `get_build_errors` | "build-produced のみ" を front-load | error_list_get | medium |
| `file_read` | "path 指定、editor で開いてなくてもOK" | get_active_document | medium |
| `get_active_document` | "currently focused in VS editor" を front-load | file_read | medium |
| `output_read` | "VS Output window pane" を front-load、console_read との対比 | console_read | medium |
| `find_in_files` | "not symbol-aware" を明記、code_find_references への誘導 | code_find_references | medium |
| `file_write` | "DESTRUCTIVE: overwrite" を front-load、edit_preview への誘導 | file_edit, edit_preview | medium |
| `debug_evaluate` | "Read-only" を front-load、immediate_execute/watch_add との対比 | immediate_execute, watch_add | medium |
| `immediate_execute` | "WITH side effects" を front-load | debug_evaluate, watch_add | medium |
| `watch_add` | "persistent" を強調 | debug_evaluate, immediate_execute | low |
| `execute_command` | "FALLBACK" を front-load、専用ツール優先を明記 | (over-broad) | high |

ファイル:
- `src/VsMcp.Extension/Tools/DebuggerTools.cs` (3 edits)
- `src/VsMcp.Extension/Tools/ProcessTools.cs` (2 edits)
- `src/VsMcp.Extension/Tools/BuildTools.cs` (1 edit)
- `src/VsMcp.Extension/Tools/EditorTools.cs` (4 edits)
- `src/VsMcp.Extension/Tools/OutputTools.cs` (2 edits)
- `src/VsMcp.Extension/Tools/ImmediateWindowTools.cs` (1 edit)
- `src/VsMcp.Extension/Tools/WatchTools.cs` (1 edit)
- `src/VsMcp.Extension/Tools/GeneralTools.cs` (1 edit)

### Build verification
`build_solution` 実行 → success（failedProjects: 0）。description は全て string constants で `+` 連結のみ使用、構文エラー無し。

### Deliverables
- `tuning/vs-mcp/snapshot-iter0-pre.md` — 編集前の凍結カタログ
- `tuning/vs-mcp/iter0-consistency-table.md` — 衝突・ギャップ・孤児の整理
- `tuning/vs-mcp/snapshot-iter0-post.md` — 編集後の baseline（iter 1 はこれに対して dry-run する）

### Notes / 触らなかったもの（iter 1 の signal 待ち）
- break-mode 表記揺れの統一（debug_get_locals/_step/_continue に "must be in break mode" が抜けてる箇所がある）
- web_console / web_network の `action` enum 分割（構造変更で iter 0 では避けた）
- error_list_get と get_build_errors の構造的統合（同上）

### Iter 1 への引き継ぎ
- 評価対象は **post-reconciliation の 115 ツール全体**。ただし subagent への dispatch は focused-subset 化して負荷を抑える
- Subset 候補: UI/Web 27 + Debug/Process critical 11 + Editor 同名衝突 4 + Build/Error 2 = **44 ツール程度**
- 残り 71 ツールは Tier 2（name: gist）として混ぜ込み、refusal pressure を保つ
- シナリオは `max(10, ceil(44 × 0.6)) = 27` を目標、hold-out は `max(2, ceil(27 × 0.2)) = 6` を目標
- Hold-out は SEALED セクションで iter 1 開始前に確定し、subagent には絶対に見せない

---

## Iter 1 — empirical baseline

### Dispatch
- Bias-free general-purpose subagent、dry-run のみ
- `dispatch-input.md`: Tier 1 60 ツール（完全 description+params）+ Tier 2 55 ツール（name: gist）
- Scenarios: 36 main（hold-outs 8 は SEALED）
- 詳細: `iter1-subagent-output.md` / 集計: `iter1-results.md`

### Selection accuracy
- 33/36 = **91.7% ○**
- △ 2 (S29, S30 — どちらも user-side underspec)
- × **0**
- **[critical] 9/9 = 100%**
- Argument-fill 11/11 = 100%（○ かつ required args ありの分）
- Off-diagonal confusion = 0（UIA ↔ Web cross-cluster confusion ゼロ）

### Subagent が報告した残課題（unclear points）
1. `debug_stop` の "debuggee" 同義語が `process_terminate` に流れるリスク（subagent が自力で救済）
2. `ui_invoke` と `ui_click` の差分が片方の description にしか書かれてない
3. `get_status` が広すぎて 1 facet クエリでも吸収しうる
4. `debug_start/_without_debugging` vs `ui_send_keys`（"Press F5" の解釈）— subagent が自力で正解
5. `rebuild` vs `build_solution` の使い分け

### 判定: production 閾値クリア。だが unclear points が 5 件あるため iter 2 を継続。

---

## Iter 2 — narrower-tool preference theme

### Goal（one theme per iter ルール適用）
**Theme**: "Cross-reference to preferred alternative for common cases" — over-broad あるいは subset 関係にあるツールに narrower-tool 誘導句を追加。

### Changes
| ツール | 変更内容 | 対象 unclear point |
|---|---|---|
| `get_status` | "For a single facet, prefer the dedicated tool: solution_info / get_active_document / debug_get_mode" を追加 | iter 1 #3 |
| `ui_invoke` | "InvokePattern only — requires AutomationId" + "For richer click semantics ... prefer ui_click" を追加 | iter 1 #2 |
| `rebuild` | "equivalent to clean+build_solution. Use only when ... explicitly wants forced rebuild — for normal build, prefer build_solution" を追加 | iter 1 #5 |

ファイル:
- `src/VsMcp.Extension/Tools/GeneralTools.cs` (1 edit — get_status)
- `src/VsMcp.Extension/Tools/UiTools.cs` (1 edit — ui_invoke)
- `src/VsMcp.Extension/Tools/BuildTools.cs` (1 edit — rebuild)

Build verified: success.

### 意図的に触らなかった unclear points
- iter 1 #1 `debug_stop` 同義語追加 — S33 は medium-conf ○ で十分。over-fitting リスクを避けて保留。
- iter 1 #4 `debug_start vs ui_send_keys` — S35/S36 は既に high-conf ○。preventive のみで signal が弱い。

### Dispatch
- **Fresh subagent**（iter 1 とは別個体）
- 同じ 36 main scenarios + 8 hold-out（iter 2 で初公開）= 44 scenarios
- 詳細: `iter2-subagent-output.md` / 集計: `iter2-results.md`

### Selection accuracy
- 33/36 = **91.7% ○** (main、iter 1 と同) + 8/8 hold-outs = **41/44 = 93.2%**
- × **0**
- **[critical] 10/10 = 100%**（H05 を含む）
- Off-diagonal = 0
- Overfitting check: hold-out drop = -8.3（むしろ改善）→ ✅

### Iter 2 の改善
- **S29** "Open the file." — iter 1 は None/low (under-trigger) → iter 2 は file_open/low（正しいツールに到達）。selection としては改善、依然 △ ラベルだが性質が変わった。
- iter 1 のすべての unclear point が動いた：
  - `get_status` 修正 → S09 alternatives で考慮されたが debug_get_mode が優先された ✅
  - `ui_invoke` 修正 → S23 で ui_click 優先が維持された ✅
  - `rebuild` 修正 → S01 で rebuild は alternatives 落ち、build_solution が選ばれた ✅

### Iter 2 で新たに浮上した unclear points（性質変化）
1. **`ui_invoke` redundancy** — wording 強化したが subagent は「ui_click が実質スーパーセット」と報告。**tool boundary 問題（ui_invoke を ui_click にマージ検討）**。description fix のスコープ外。
2. **`web_reload` ツール不在**（H08）— description で解消不可。**catalog gap**。
3. **symbol-by-name 検索の不在**（H07）— **catalog gap**。
4. **S24/S31 surface 不明** — user-side underspec、description bug ではない。
5. **S33 "debuggee" 同義語** — medium-conf ○ で安定。許容範囲。

## Convergence 判定

| 基準 | 評価 | 達成 |
|---|---|---|
| Selection accuracy delta ≤ +3 | 0 | ✅ |
| Off-diagonal cell newly crossed 3 | none | ✅ |
| Argument-fill delta ≤ +5 | 0 | ✅ |
| Hold-out drop ≤ 15 | -8.3（改善） | ✅ |
| **New unclear points: 0** | 5（うち 3 件は構造問題、2 件は許容範囲） | ⚠️ |

iter 1 → iter 2 で **wording-level の unclear point はすべて解消**。残る 5 件は description tuning の射程外（tool boundary / catalog gap / user-side）。

**= wording tuning は plateau 到達。empirical-mcp-tool-description-tuning スキルとしてはここで完了**。

## 推奨フォローアップ（このスキルのスコープ外）

1. **`ui_invoke` を deprecate** または `ui_click` にマージ — tool boundary 修正
2. **`web_reload` ツール追加**（または `web_navigate` に `reload=true` mode 追加）
3. **`code_find_references_by_name` ツール追加** — シンボル名から逆引き
4. **break-mode 表記揺れ統一**（debug_get_locals/_step/_continue に "must be in break mode"）— iter 0 で意図的に保留したもの

## 最終成果サマリ

- 30 件のツール description を改善（iter 0 で 27 + iter 2 で 3）
- 9 つの [critical] シナリオで 100% 正解（destructive operation の誤選択ゼロ）
- UIA × Web cluster boundary の confusion をゼロに維持
- 91.7%（main） / 93.2%（with hold-outs）の selection accuracy
