# CURRENT - v0.1.21

## 目的

エラー／警告表示時にWindows標準の効果音を鳴らさず、メッセージ内容や操作性は従来どおり維持する。

## v0.1.21 スコープ

- [x] `MessageBoxIcon.Error` / `MessageBoxIcon.Warning` によるWindows標準効果音を抑止
- [x] エラー／警告メッセージ本文とボタン構成は変更しない
- [x] Information / Questionなど通常の情報・確認ダイアログの意味は維持
- [x] 個別箇所ではなくアプリ共通のMessageBoxラッパーで一元管理
- [x] README.mdへv0.1.21変更履歴を追記
- [x] ハーネスをv0.1.21へ更新

## 実装方針

`ViTextEditor.MessageBox` をアプリ内ファサードとして追加し、既存の `MessageBox.Show(...)` 呼び出しをそのまま利用する。Error / Warningの場合だけ `MessageBoxIcon.None` へ変換してからWindows標準MessageBoxへ委譲する。これにより、各エラー処理へ個別修正を入れず、今後追加されるエラー／警告ダイアログも同じルールで無音化する。

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
