# CURRENT - v0.1.32

## 目的

vi/Vim互換の行結合操作を追加する。

## v0.1.32 スコープ

- [x] `J` で現在行と次行をvi方式の空白調整付きで結合
- [x] `gJ` で横方向の空白を維持したまま改行だけを除去
- [x] `.` で直前の行結合を繰り返し
- [x] LF／CRLF、空行、最終行、閉じ丸括弧、インデントのCoreテストを追加
- [x] README.md変更履歴・操作説明を更新
- [x] ハーネスとアプリ版をv0.1.32へ更新

## 対象外

- Visualモードの複数行選択結合は追加しない
- `:join` Exコマンドは追加しない
- 既存の小文字 `j` による下移動は変更しない

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
