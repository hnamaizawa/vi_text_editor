# CURRENT - v0.1.7

## 目的

EmEditor／サクラエディタに近いWindows GUIを維持しつつ、viのchange演算子とUndo時の未保存状態表示を改善する。

## v0.1.7 スコープ

- [x] `cw` で現在位置からword末尾まで変更してINSERTへ入る
- [x] `cW` で現在位置からWORD末尾まで変更してINSERTへ入る
- [x] `ce` をword末尾までのchangeとして追加
- [x] `c$` を行末までのchangeとして追加
- [x] changeで削除した文字列を既存の無名レジスタへ保持
- [x] 改良済み `w` / `W` ナビゲーションより先にpending changeを処理する
- [x] Scintilla save pointをDirty判定に利用
- [x] `u` / Ctrl+Zでsave pointまで戻ったらタイトルの `*` を消す
- [x] Redoでsave pointを離れたら `*` を再表示する
- [x] 保存成功時に新しいsave pointを設定する
- [x] 既存のUndo下限保護、参照モード、IME、ファイルI/Oを維持
- [x] `cw` / `cW` / `ce` / `c$` のCore単体テストを追加

## 次候補

1. `dw` / `dW` / `de` / `d$` のoperator + motion
2. `yw` / `yW` / `ye` / `y$` のoperator + motion
3. `cc` / `C` / `ciw` / `caw` などchange系拡張
4. `e` / `E`, `ge` / `gE` のword/WORD末尾移動
5. 数値プレフィックス (`3w`, `5j`, `2Ctrl+F` など)
6. `"a yy` / `"a p` などNORMALモードの名前付きレジスタ
7. Vim互換に近い検索正規表現と検索ハイライト
8. Visualモード
9. タブ編集
