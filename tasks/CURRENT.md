# CURRENT - v0.1.33

## 目的

vi/Vimのoperator + motionを単語・行頭・yank方向へ拡張する。

## v0.1.33 スコープ

- [x] `d/c` の `w/W/e/E` motionへ回数指定を適用
- [x] `db/dB/cb/cB` を追加
- [x] `d0/d^/c0/c^` を追加
- [x] `yw/yW/ye/yE/yb/yB/y0/y^/y$` と `Y` を追加
- [x] 回数付き変更の`.`繰り返しとレジスタ連携を検証
- [x] README.md変更履歴・操作説明を更新
- [x] ハーネスとアプリ版をv0.1.33へ更新

## 対象外

- 行をまたぐword operatorは追加しない
- Visualモードとtext objectは追加しない
- `f/F/t/T/%/{/}` をoperator motionへはまだ統合しない

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
