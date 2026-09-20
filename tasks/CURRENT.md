# CURRENT - v0.1.15

## 目的

v0.1.14で右下のX/Y座標がStatusStripのoverflowへ隠れる問題を修正し、ファイル拡張子に応じた読みやすいシンタックス強調を追加する。

## v0.1.15 スコープ

- [x] StatusStripのSpring領域を専用spacerへ分離
- [x] 右端へ1始まりの `X=桁  Y=行` を固定幅で常時表示
- [x] カーソル移動・マウス操作・vi操作後も座標更新
- [x] `.md/.markdown` をMarkdownとして強調
- [x] Markdownの `#`〜`######` 見出しを太字＋サイズ差で強調
- [x] Markdownのリスト、リンク、コード、引用も視認性を改善
- [x] `.json/.jsonl` をJSONとして強調
- [x] `.xml/.xaml/.svg` をXMLとして強調
- [x] `.cs/.csx` をC#として強調
- [x] `.py/.pyw` をPythonとして強調
- [x] `.js/.jsx/.mjs/.cjs` をJavaScriptとして強調
- [x] `.ts/.tsx` をTypeScriptとして強調
- [x] `.yaml/.yml` をYAMLとして強調
- [x] Save Asなどで拡張子が変わった場合も強調を再適用
- [x] シンタックス強調は表示だけを変更し、文書内容・Undo履歴を変更しない
- [x] v0.1.14までのタブ、Large File、JSON整形、Markdownプレビュー、vi/Ex操作を維持
- [x] ハーネスをv0.1.15へ更新

## 実装方針

Scintilla5.NET 7.0.0 / Lexillaの `LexerName` を拡張子から選択し、スタイルを設定する。Markdownでは見出しを特に強く表示する。C# / JavaScript / TypeScriptはScintilla5.NETが対応づけるLexilla `cpp` lexerを使い、言語別キーワードセットを設定する。

## 次候補

1. ユーザーが配色テーマを選べる機能
2. シンタックス強調ON/OFFと拡張子ごとの手動言語指定
3. タブの前回セッション復元、タブのドラッグ並べ替え、ピン留め
4. Markdownライブプレビュー（編集と同期）
5. JSONツリー表示 / JSONPath検索
6. Large Fileモードのバックグラウンド索引作成と進捗表示
7. 数値プレフィックス (`3yy`, `3w`, `5j`, `2dw` など)
8. Visualモード
