# CURRENT - v0.1.6

## 目的

EmEditor／サクラエディタに近いWindows GUIを維持しつつ、viの行内移動、COMMANDモード、Ex形式のyank/put、キー入力応答性を改善する。

## v0.1.6 スコープ

- [x] `^` を行の最初の非空白文字への移動として確実に処理
- [x] 日本語/JISキーボードでも `^` / `:` / `/` / `?` をKeyPressで補完
- [x] `:` 入力中はステータスを `COMMAND` と表示
- [x] `:行番号` / `:$` のジャンプを維持
- [x] `:[range]y[ank] [register]` を追加
- [x] `:[line]pu[t] [register]` を追加
- [x] `:5y a`, `:5,10y a`, `:%y a`, `:pu a`, `:20pu a`, `:0pu a` をサポート
- [x] 大文字名前付きレジスタへのyankは小文字レジスタへ追記
- [x] 参照モードではyank/移動を許可しputを抑止
- [x] 行・桁表示をScintillaネイティブAPIへ変更し、キーごとの全文走査を廃止
- [x] 不要なKeyUp後のステータス再計算を廃止
- [x] `^` / Ex yank / put / named register のCore単体テストを追加
- [x] 参照モード、Undo下限保護、ファイルI/Oを維持

## 次候補

1. `e` / `E`, `ge` / `gE` のword/WORD末尾移動
2. 数値プレフィックス (`3w`, `5j`, `2Ctrl+F` など)
3. operator + motion (`dw`, `dW`, `cw`, `c$` など)
4. `"a yy` / `"a p` などNORMALモードの名前付きレジスタ
5. Vim互換に近い検索正規表現と検索ハイライト
6. Visualモード
7. タブ編集
