# CURRENT - v0.1.11

## 目的

COMMANDモードとNORMALモードのレジスタを統合して `:yank` → `p/P` を正しく連携させ、バイナリモードでもviらしいキー移動と検索を利用できるようにする。

## v0.1.11 スコープ

- [x] `ViRegisterStore` をCOMMAND/NORMAL共通のレジスタ基盤にする
- [x] `:y3` / `:5y a` 等でyankした内容をNORMALの `p` / `P` でpaste可能にする
- [x] NORMALの `yy` をCOMMANDの `:put` から利用可能にする
- [x] NORMALの `dd` / `dw` / `x` / change系で無名レジスタを共通更新する
- [x] 参照モードでは従来どおり `p/P` をブロックし、yankは許可する
- [x] バイナリモードで `j` / `k`（J/Kも可）による上下移動
- [x] バイナリモードで `Ctrl+F` / `Ctrl+B` の1画面移動
- [x] バイナリモードで `Ctrl+D` / `Ctrl+U` の半画面移動
- [x] バイナリモードで `gg` / `G` による先頭/末尾移動
- [x] バイナリモードで `/` / `?` 検索と `n` / `N` 繰り返し
- [x] バイナリ検索は選択中のUTF-8 / Shift_JIS / UTF-16 / ASCII文字コードを使用
- [x] `hex:4D 5A 90` 形式のRAW HEX検索を追加
- [x] 検索は64KBチャンク単位で `RandomAccess.Read` し、ファイル全体をメモリへ展開しない
- [x] バージョン表示をv0.1.11へ統一

## 次候補

1. 数値プレフィックス (`3yy`, `3w`, `5j`, `2dw` など)
2. `yw` / `yW` / `ye` / `y$` のoperator + motion
3. `cc` / `C` / `ciw` / `caw` などchange系拡張
4. `"a yy` / `"a p` などNORMALモードの名前付きレジスタ
5. バイナリ検索結果のバイト単位ハイライト、オフセット直接ジャンプ、選択範囲コピー
6. Vim互換に近い検索正規表現と検索ハイライト
7. Visualモード
8. タブ編集
