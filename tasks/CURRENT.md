# CURRENT - v0.1.28

## 目的

日本語やMarkdown書式が混在する文書でも、URLの青色・下線付き装飾を `http://` / `https://` の先頭から末尾だけへ正確に適用する。

## v0.1.28 スコープ

- [x] URLの.NET UTF-16文字位置をScintilla自身の文書位置へ変換
- [x] 日本語やサロゲートペアがURLより前にある場合の表示ずれを防止
- [x] Markdown箇条書きの空白、番号、記号、表示名をURL装飾から除外
- [x] `[test](https://test.com)` の末尾 `)` をURL装飾から除外
- [x] Markdownリンク表示名とリンク先にあるURLを別々に検出
- [x] 文書全体の装飾解除にScintillaのネイティブ文書長を使用
- [x] 箇条書き、生URL、Markdownリンクの厳密な範囲テストを追加
- [x] README.md変更履歴・操作説明を更新
- [x] ハーネスをv0.1.28へ更新

## 対象外

- Markdown記法や本文そのものは変更しない
- `ftp://`、`file://`、メールアドレスはリンク表示しない
- URL内で対応している丸括弧は従来どおりURLの一部として扱う

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
