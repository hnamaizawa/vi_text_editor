# CURRENT - v0.1.30

## 目的

NORMALモードへ行数付き削除とchange operatorを追加し、削除内容を既存レジスタ・貼り付け・繰り返し機能と統合する。

## v0.1.30 スコープ

- [x] `d{count}d` で指定行数を削除
- [x] 削除行を行単位で無名レジスタへ保存し、`p/P`で貼り付け
- [x] `.` で行数を含む削除操作を繰り返し
- [x] `cw` / `cW` / `ce` / `cE` / `c$` / `C` / `cc` に対応
- [x] change対象削除後にINSERTモードへ移行
- [x] Core回帰テストを追加
- [x] README.md変更履歴・操作説明を更新
- [x] ハーネスとアプリ版をv0.1.30へ更新

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
