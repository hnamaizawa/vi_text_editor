# CURRENT - v0.1.18

## 目的

v0.1.17で発生したCOMMANDモード入力回帰とJSON整形の参照モード誤判定を緊急修正する。

## v0.1.18 スコープ

- [x] `:` `/` `?` を追加viモーション処理が横取りしないよう入力経路を修正
- [x] JIS/US配列差に依存するOEMキーコードから句読記号モーションを推測しない
- [x] `%`, `;`, `,`, `()`, `{}`, `+`, `-`, `_`, `|` は実際に入力された文字から判定
- [x] `f/F/t/T` の対象文字先取り、数値プレフィックス、`H/M/L`、`Ctrl+E/Y` は維持
- [x] JSON整形の参照モード判定を `Scintilla.ReadOnly` ではなく `_referenceMode` の状態へ変更
- [x] 参照モードOFFなのに `ReadOnly` が残留した場合は編集可能状態へ復旧して整形
- [x] COMMAND/検索プレフィックスと追加モーションが重ならないことをCore回帰テスト化
- [x] v0.1.17のVimモーション、座標、タブ、履歴、Large File、Markdown、シンタックス強調を維持
- [x] ハーネスをv0.1.18へ更新

## 実装方針

COMMAND入力 (`:` `/` `?`) は既存MainFormの入力処理を最優先とする。フォーム全体のKeyPreviewは、MainForm未対応の追加モーションだけを補助し、キーボード配列依存のOEMキーコードから句読記号を推測しない。句読記号系モーションはKeyPressで得られる実文字から判定する。

JSON整形はユーザーが選択した参照モード状態だけを変更可否の根拠にする。ScintillaのReadOnlyは表示・状態遷移でも変化し得る実装詳細なので、参照モード判定には利用しない。

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
