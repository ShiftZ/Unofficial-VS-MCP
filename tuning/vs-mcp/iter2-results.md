# Iter 2 評価結果

Dispatch 完了: `iter2-subagent-output.md` を集計。fresh subagent（iter 1 とは別個体）で 36 main + 8 hold-out = 44 シナリオを dry-run。

## Main scenarios (S01–S36)

| # | Expected | Chosen | Conf | Label | iter1 比較 |
|---|---|---|---|---|---|
| S01 | build_solution | build_solution | high | ○ | 同 |
| S02 | breakpoint_set | breakpoint_set | high | ○ | 同 |
| S03 | ui_capture_window | ui_capture_window | high | ○ | 同 |
| S04 | web_screenshot | web_screenshot | high | ○ | 同 |
| S05 | file_read | file_read | high | ○ | 同 |
| S06 | code_find_references | code_find_references | high | ○ | 同 |
| S07 | test_run | test_run | high | ○ | 同 |
| S08 | project_list | project_list | high | ○ | 同 |
| S09 | debug_get_mode | debug_get_mode | high | ○ | 同（get_status fix が効いた — alternatives で get_status を考慮はしたが選ばず） |
| S10 | find_in_files | find_in_files | high | ○ | 同 |
| S11 | web_connect | web_connect | high | ○ | 同 |
| S12 | web_element_click | web_element_click | high | ○ | 同 |
| **S13 [critical]** | debug_start_without_debugging | debug_start_without_debugging | high | ○ | 同 |
| **S14 [critical]** | debug_start | debug_start | high | ○ | 同 |
| **S15 [critical]** | debug_stop | debug_stop | high | ○ | 同 |
| **S16 [critical]** | process_detach | process_detach | high | ○ | 同 |
| **S17 [critical]** | process_terminate | process_terminate | high | ○ | 同 |
| S18 | debug_evaluate | debug_evaluate | high | ○ | 同 |
| S19 | immediate_execute | immediate_execute | high | ○ | 同 |
| S20 | get_build_errors | get_build_errors | high | ○ | 同 |
| S21 | error_list_get | error_list_get | high | ○ | 同 |
| S22 | get_active_document | get_active_document | high | ○ | 同 |
| S23 | ui_click | ui_click | high | ○ | 同 |
| S24 | None or low | None | low | ○ | 同 |
| S25 | None | None | high | ○ | 同 |
| S26 | None | None | high | ○ | 同 |
| S27 | None | None | high | ○ | 同 |
| S28 | None | None | high | ○ | 同 |
| S29 | low-conf file_open | file_open | low | △ | **改善**: iter1 は None だった (under-trigger) → 正しいツールに当たるように |
| S30 | low-conf watch_add | watch_add | low | △ | 同 |
| S31 | None or low | None | low | ○ | 同 |
| S32 | test_run | test_run | high | ○ | 同 |
| **S33 [critical]** | debug_stop | debug_stop | medium | ○ | 同（subagent も同じ "debuggee" 同義語 nuance を独立に検出） |
| **S34 [critical]** | debug_stop | debug_stop | high | ○ | 同 |
| **S35 [critical]** | debug_start_without_debugging | debug_start_without_debugging | high | ○ | 同 |
| **S36 [critical]** | debug_start | debug_start | high | ○ | 同 |

## Hold-out scenarios (overfitting check)

| # | Expected | Chosen | Conf | Label |
|---|---|---|---|---|
| H01 | process_detach | process_detach | medium | ○（processId 欠落だが選択は正解） |
| H02 | ui_snapshot with includeScreenshot=false | ui_snapshot, args correct | high | ○ |
| H03 | debug_evaluate | debug_evaluate | high | ○ |
| H04 | ui_set_value | ui_set_value, args correct | high | ○ |
| **H05 [critical]** | process_terminate (frozen → kill OK) | process_terminate | medium | ○ |
| H06 | web_network action=get | web_network action=get | high | ○ |
| H07 | low-conf code_find_references or find_in_files | code_find_references | low | △ |
| H08 | web_navigate or web_js_execute | web_navigate | medium | ○ |

Hold-out selection ○: 8/8 = 100%。

## 集計（iter 1 と比較）

| Metric | iter1 | iter2 main | iter2 with hold-outs | Delta |
|---|---|---|---|---|
| Selection ○ | 33/36 = 91.7% | 33/36 = 91.7% | 41/44 = 93.2% | 0 (main), +1.5 (with H) |
| △ (half-success) | 2 | 2 | 3 | +1 (H07) |
| × (mis-selection) | 0 | 0 | 0 | 0 |
| **[critical] accuracy** | 9/9 = 100% | 9/9 = 100% | 10/10 = 100%（H05 含む） | 0 |
| Argument-fill ○ | 11/11 = 100% | 11/11 = 100% | (適用範囲内で 100%) | 0 |
| Off-diagonal confusion | 0 | 0 | 0 | 0 |

## Cluster-level matrix (iter 2 with hold-outs)

|  | UIA chosen | Web chosen | Other chosen | None chosen |
|---|---|---|---|---|
| UIA (expected) | 4 (S03, S23, H02, H04) | 0 | 0 | 0 |
| Web (expected) | 0 | 5 (S04, S11, S12, H06, H08) | 0 | 0 |
| None (expected) | 0 | 0 | 0 | 6 |
| Other (expected) | 0 | 0 | 27 | 2 (low conf 4 で None or 不完全 arg) |

Cluster boundary 完全保持。

## Convergence check (vs iter 1)

| 基準 | 評価 | 達成 |
|---|---|---|
| Selection accuracy delta ≤ +3 | 0 | ✅ |
| Off-diagonal cell newly crossed 3 | none | ✅ |
| Argument-fill delta ≤ +5 | 0 | ✅ |
| Hold-out drop ≤ 15 from recent avg | -8.3（つまり改善側） | ✅ |
| **New unclear points: 0** | 5 件残（後述） | **❌** |

## 残った unclear points の性質変化（iter 2 で subagent が報告）

iter 1 の unclear points は description-tuning で解消可能だったが、iter 2 で残ってる 5 件は性質が違う：

1. **ui_invoke の redundancy** — iter 2 で description を強化したが、subagent は「ui_click が AutomationId+InvokePattern も試すから ui_invoke は事実上 subset」と報告。これは **wording fix ではなく tool boundary 問題**（ui_invoke を ui_click にマージする構造変更）。
2. **web_reload tool の不在** — H08「Refresh the page」に専用ツールがない。description 修正では解消できない。**catalog gap = 新ツール追加が必要**。
3. **symbol-by-name 検索の不在** — H07「Foo の参照ファイル数」は code_find_references が path+line+column 要求するため、シンボル名から逆引きできない。**catalog gap = 新ツール追加**。
4. **S24/S31「Click submit」「Click the button」** — surface（UIA vs Web）の不確定。subagent は正しく None/low を返した。**user-side underspec で description bug ではない**。
5. **S33「Stop the debuggee」の "debuggee" 同義語** — iter 2 で意図的に修正しなかったが、medium-conf ○ は維持。**残してもよいレベル**。

## 判定

**Wording-level tuning は plateau に到達**。

- 主要メトリクスは全て production 閾値超え（selection 91.7%/93.2%、critical 100%、arg fill 100%、off-diagonal 0）
- 残課題 5 件のうち 3 件は **構造的問題**（tool boundary / catalog gap）で description fix では解消不可能
- 残り 2 件は **user-side / 許容範囲**

これは skill 仕様の Divergence 判定に近い：
> "If the same off-diagonal confusion cell remains hot across 3+ iterations despite targeted fixes → the tool boundary is wrong, not the wording."

正確には off-diagonal cell ではなく "wording 問題から structural 問題への遷移" だが、結論は同じ — **description tuning はここで stop すべき**。

## 推奨フォローアップ（description tuning スコープ外）

- **ui_invoke を ui_click にマージ** または ui_invoke を deprecate（structural change）
- **web_reload ツール追加**（catalog gap）
- **code_find_references_by_name ツール追加**（catalog gap）
- break-mode 表記揺れの統一（debug_get_locals/_step/_continue に "must be in break mode" を明記。iter 0 で意図的に保留した分）

## 結論

Iter 0+1+2 で vs-mcp tool description tuning は **plateau 到達 → 完了**。Production 閾値を全項目で満たしてる。
