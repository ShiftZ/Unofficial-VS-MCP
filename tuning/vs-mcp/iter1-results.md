# Iter 1 評価結果

Dispatch 完了: `iter1-subagent-output.md` を集計。

## Selection accuracy

| # | Expected | Chosen | Conf | Label | Notes |
|---|---|---|---|---|---|
| S01 | build_solution | build_solution | high | ○ | |
| S02 | breakpoint_set | breakpoint_set | high | ○ | Tier 2 だが gist で拾えた |
| S03 | ui_capture_window | ui_capture_window | high | ○ | surface-tag が効いた |
| S04 | web_screenshot | web_screenshot | high | ○ | surface-tag が効いた |
| S05 | file_read | file_read | high | ○ | |
| S06 | code_find_references | code_find_references | high | ○ | |
| S07 | test_run | test_run | high | ○ | Tier 2 で拾えた |
| S08 | project_list | project_list | high | ○ | Tier 2 |
| S09 | debug_get_mode | debug_get_mode | high | ○ | Tier 2 |
| S10 | find_in_files | find_in_files | high | ○ | "not symbol-aware" が効いた |
| S11 | web_connect | web_connect | high | ○ | |
| S12 | web_element_click | web_element_click | high | ○ | |
| **S13 [critical]** | debug_start_without_debugging | debug_start_without_debugging | high | ○ | "Ctrl+F5" front-load が効いた |
| **S14 [critical]** | debug_start | debug_start | high | ○ | "F5" front-load が効いた |
| **S15 [critical]** | debug_stop | debug_stop | high | ○ | |
| **S16 [critical]** | process_detach | process_detach | high | ○ | "WITHOUT terminating" が効いた |
| **S17 [critical]** | process_terminate | process_terminate | high | ○ | "DESTRUCTIVE" front-load が効いた |
| S18 | debug_evaluate | debug_evaluate | high | ○ | "Read-only" 効果 |
| S19 | immediate_execute | immediate_execute | high | ○ | "WITH side effects" 効果 |
| S20 | get_build_errors | get_build_errors | high | ○ | |
| S21 | error_list_get | error_list_get | high | ○ | "everything in the Error List" マッチ |
| S22 | get_active_document | get_active_document | high | ○ | |
| S23 | ui_click | ui_click | high | ○ | Name パラメータ採用 |
| S24 | None or low | None | low | ○ | 正しく refusal（surface 不明） |
| S25 | None | None | high | ○ | |
| S26 | None | None | high | ○ | |
| S27 | None | None | high | ○ | |
| S28 | None | None | high | ○ | |
| S29 | low-conf file_open | None | low | △ | under-trigger — refused when low-conf pick was expected |
| S30 | low-conf watch_add | watch_add | low | △ | hit with low conf (per truth table) |
| S31 | None or low | None | low | ○ | |
| S32 | test_run | test_run | high | ○ | |
| **S33 [critical]** | debug_stop | debug_stop | medium | ○ | "debuggee" 同義語の救済余地あり |
| **S34 [critical]** | debug_stop | debug_stop | high | ○ | |
| **S35 [critical]** | debug_start_without_debugging | debug_start_without_debugging | high | ○ | 「キーイベント vs VS action」リスクは subagent が自力で解消 |
| **S36 [critical]** | debug_start | debug_start | high | ○ | 同上 |

## 集計

| Metric | Value |
|---|---|
| Selection accuracy | **33/36 = 91.7%** (○) |
| △ (half-success) | 2 (S29, S30) |
| × (mis-selection) | **0** |
| [critical] accuracy | **9/9 = 100%** |
| Argument-fill accuracy (○ scenarios w/ required args) | 11/11 = 100% |
| Confusion matrix off-diagonal | **0 cells crossed threshold** |
| Confidence calibration | high-conf × 率 = 0; low-conf ○ 率 = 1/3 ≈ 33%（refusal-as-correct を含む） |

## Cluster-level matrix（UIA × Web × None × Other）

|  | UIA (chosen) | Web (chosen) | Other (chosen) | None (chosen) |
|---|---|---|---|---|
| **UIA (expected)** | 2 (S03, S23) | 0 | 0 | 0 |
| **Web (expected)** | 0 | 3 (S04, S11, S12) | 0 | 0 |
| **None (expected)** | 0 | 0 | 0 | 6 (S24, S25, S26, S27, S28, S31) |
| **Other (expected)** | 0 | 0 | 28 | 2 (S29 △) |

**Cluster boundary は完全に保持されてる**っす。UIA ↔ Web の cross-cluster confusion はゼロ。

## Subagent が報告した残課題（structured reflection 要旨）

1. **S33 "Stop the debuggee"** — `debuggee` という単語が `process_terminate` に流れるリスク。`debug_stop` の description が「Stop the current debug session ... Detaches and terminates the debuggee」と書いてるので 2 文目で救済されたが、1 文目に "stop the debuggee" 同義語を入れると更に堅い。
2. **`ui_invoke` vs `ui_click`** — `ui_invoke` は AutomationId 専用 + InvokePattern only、`ui_click` は Name/AutomationId/coords + 物理クリックフォールバック。差分が片方の description には書かれてない。
3. **`get_status` vs `debug_get_mode` / `solution_info` / `get_active_document`** — `get_status` が広すぎて 1 facet クエリでも吸収しうる。"prefer dedicated tool for one facet" 句が必要。
4. **`debug_start` / `debug_start_without_debugging` vs `ui_send_keys`** — "Press F5" を文字通りキーストロークと解釈する余地。subagent は自力で正解したが、明示しておけば堅い。
5. **`rebuild` vs `build_solution`+`clean`** — `rebuild` description に「clean+build と同等」「force-rebuild 以外なら build_solution 優先」が無い。

## 判定

| 基準 | 現状 | 達成 |
|---|---|---|
| Selection accuracy ≥ 90% | 91.7% | ✅ |
| [critical] accuracy = 100% | 100% | ✅ |
| Argument-fill accuracy ≥ 85% | 100% | ✅ |
| Off-diagonal confusion = 0 (newly crossed) | 0 | ✅ |

production-ready の閾値は超えてる。ただし subagent が報告した 5 つの残課題 (unclear points) があるので、convergence の定義「2 連続で new unclear points = 0」はまだ満たしてない。

## 次の判断

**Option A**: 残課題を 1 theme にまとめて iter 2 を回す。
- Theme 候補: "narrower-tool preference clauses" — `get_status`、`debug_stop`、`rebuild`、`ui_invoke` に対称的に narrower-tool 誘導を追加（symmetric-pair editing ではなく "narrower tool" pattern として一括）。
- 期待: selection accuracy ≤ +3 points 上昇、unclear points 0 で convergence 到達。

**Option B**: iter 1 のスナップショットを「good enough」と判断して止める。残課題はメモして次回 tuning run で扱う。

Option A を推奨っす（convergence の定義に従うと、もう 1 iter で plateau 確認するべき）。
