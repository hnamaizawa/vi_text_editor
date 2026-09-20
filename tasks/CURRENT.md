# CURRENT - v0.1.8

## 目的

EmEditor／サクラエディタに近いWindows GUIを維持しつつ、viのdelete operator + motionとExファイルコマンドを実用化する。

## v0.1.8 スコープ

- [x] `dw` でword motion単位の削除
- [x] `dW` でWORD motion単位の削除
- [x] `de` / `dE` でword / WORD末尾まで削除
- [x] `d$` / `D` で行末まで削除
- [x] deleteで削除した文字列を無名レジスタへ保持
- [x] `:e!` で未保存変更を破棄して現在ファイルを再読込
- [x] `:e#` / `:e #` でalternate fileを開く
- [x] `:q!` で未保存変更を無視して強制終了
- [x] `:w` で現在ファイルへ保存
- [x] `:w ファイル名` をユーザー要件どおり別名保存として実装
- [x] GUIファイル切替・別名保存時にalternate fileを更新
- [x] ExファイルコマンドのCoreパーサーと単体テストを追加
- [x] 既存のchange、Undo/save point、参照モード、IME、ファイルI/Oを維持
- [x] マージ完了後にその版のWindows x64自己完結ZIPを提示する運用をハーネスへ追加

## 次候補

1. `yw` / `yW` / `ye` / `y$` のoperator + motion
2. `cc` / `C` / `ciw` / `caw` などchange系拡張
3. 数値プレフィックス (`3w`, `5j`, `2dw` など)
4. `"a yy` / `"a p` などNORMALモードの名前付きレジスタ
5. `:e ファイル名` / `:saveas` / `:w!` などExファイル操作拡張
6. Vim互換に近い検索正規表現と検索ハイライト
7. Visualモード
8. タブ編集
