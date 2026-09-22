# CURRENT - v0.1.25

## 目的

Windowsでvi_text_editorを既定アプリとして利用しているとき、既にエディターが起動していても新しいウィンドウを増やさず、対象ファイルを既存ウィンドウの新規タブへ追加する。また、Explorerから起動中のvi_text_editorへファイルをドラッグ＆ドロップした場合も同じタブオープン経路を利用する。

## v0.1.25 スコープ

- [x] 同一Windowsユーザー／セッションでは通常起動を単一インスタンス化
- [x] 2つ目のプロセスから既存プロセスへファイルパスを転送
- [x] Named Mutex + Named Pipeでプロセス間通信
- [x] 既存プロセス側は受信ファイルを `EditorWorkspaceForm.OpenPath` で開く
- [x] 同じファイルが既に開いている場合は重複タブを作らず既存タブを選択
- [x] 起動中ウィンドウへファイルをドラッグ＆ドロップして新規タブで開く
- [x] 複数ファイルのドラッグ＆ドロップに対応
- [x] 動的に追加されるEditor / Viewer配下Controlにもドロップ受付を設定
- [x] 通常版でsingle-instance forwardingスモークテスト
- [x] Single-file版でsingle-instance forwardingスモークテスト
- [x] Release workflowでも同じ転送テストを配布前に再実行
- [x] README.mdへv0.1.25変更履歴と利用方法を追記
- [x] harness/app_blueprint.yamlをv0.1.25へ更新

## 既存アプリへのファイル転送

通常起動ではユーザー名とWindowsセッションIDから安定した識別子を作成し、Named Mutexで一次プロセスを判定する。二次プロセスはGUIを新規作成せず、Named Pipeへ起動引数のファイルパスを送信して終了する。

一次プロセスは受信したファイルパスをWinForms UIスレッドへ渡し、既存の `OpenPath` を利用する。これにより、通常のファイルオープン、Windows既定アプリからの起動、既存インスタンスへの転送、ドラッグ＆ドロップが同じタブ管理ルールを利用する。

## ドラッグ＆ドロップ

Workspace配下のWinForms Controlを再帰的に登録し、後から追加されたタブやEditor / Viewerにも `ControlAdded` 経由でドロップ受付を適用する。FileDrop形式の既存ファイルだけを受け付け、複数ファイルは順に `OpenPath` へ渡す。

GitHub ActionsではOLEの実マウスドラッグ操作そのものは安定して自動化できないため、Windows buildによるイベント配線のコンパイル確認を行う。single-instance転送については一次／二次プロセスを実際に起動して自動検証する。

## Release運用

v0.1.25をmainへマージするとRelease workflowがCore tests、build、通常起動、Windows Shellオープン、single-instance forwardingを通常版／Single-file版の両方で再検証する。成功後 `v0.1.25` タグとLatest GitHub Releaseを作成し、version付きSingle-file EXE / Portable ZIPを公開する。

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト