# CURRENT - v0.1.20

## 目的

Markdown Viewerを通常エディタ／Large File Viewer／バイナリViewerと同じvi中心の操作体系へさらに揃え、Viewerがフォーカスを持っていてもワークスペースのタブ移動を安定して実行できるようにする。

## v0.1.20 スコープ

- [x] Markdown Viewerで `:` COMMAND入力を利用可能にする
- [x] Markdown Viewerで `:set ic` / `:set noic` / `:set ic?` を利用可能にする
- [x] Markdown Viewerの `:` `/` `?` 開始判定をOEMキーコードではなく実際の入力文字で行う
- [x] JIS／USキーボード配列差でCOMMAND入力が失われない構造へ変更
- [x] COMMAND入力中は下部入力欄と `COMMAND` ステータスを表示
- [x] `Ctrl+PageDown` / `Ctrl+PageUp` をワークスペース最優先のタブ切替として追加
- [x] `Ctrl+Tab` / `Ctrl+Shift+Tab` の既存タブ切替を維持
- [x] Markdown Viewerがスクロール後もタブ切替キーを奪わないよう優先順位を明示
- [x] キーボードでタブ切替後、選択されたEditor / Markdown / Large File本文へフォーカスを移す
- [x] MarkdownのCOMMAND/検索入力中に別タブへ移る場合は一時入力をキャンセル
- [x] README.mdへv0.1.20変更履歴を追記
- [x] ハーネスをv0.1.20へ更新

## 実装方針

Markdown Viewer内の既存 `ViOptions` / 検索ロジックは維持し、COMMAND開始方法だけをWinFormsの `WM_CHAR` ベースへ変更する。これにより、JISキーボード上で `:` がどのOEMキーに割り当たるかを推測しない。

タブ移動は埋め込みViewerより上位の `EditorWorkspaceForm.ProcessCmdKey` で処理し、`Ctrl+PageUp/PageDown` と `Ctrl+Tab/Ctrl+Shift+Tab` をViewerへ渡す前に消費する。選択後は新しいタブの主コントロールへ明示的にフォーカスする。

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
