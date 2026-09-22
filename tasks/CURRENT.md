# CURRENT - v0.1.24

## 目的

GitHub Releasesから誰でも最新のSingle-file EXEを取得できる配布フローを自動化し、Windows Explorerからテキストファイルをダブルクリックした際にvi_text_editorで開けるよう、Windows Shellから渡されるファイルパス引数に対応する。

## v0.1.24 スコープ

- [x] Windows Shell / 「プログラムから開く」から渡されるファイルパスを起動時に開く
- [x] スペースを含むファイルパスに対応
- [x] 複数ファイルパスが渡された場合は複数タブで開く
- [x] 起動時にファイルパスがある場合は余分な空タブを作成しない
- [x] `--startup-open-smoke-test` を追加し、実際のファイルパス起動をCIで検証
- [x] 通常版／Single-file版の両方でWindows Shell相当のファイルオープンをスモークテスト
- [x] `.github/workflows/release.yml` を追加
- [x] mainへのversioned merge後にプロジェクトVersionから `vX.Y.Z` タグを自動作成
- [x] Release workflowでCore tests / build / publish / startup smoke / shell-open smokeを再確認
- [x] version付きSingle-file EXEとPortable ZIPをGitHub Release Assetsへ登録
- [x] 新しいReleaseをLatestとして公開
- [x] 既に同一versionのReleaseが存在する場合は重複公開しない
- [x] README.mdへv0.1.24変更履歴、Release取得方法、Windows既定アプリ設定手順を追記
- [x] ハーネスをv0.1.24へ更新

## Windowsでのダブルクリック動作

Windowsは既定アプリまたは「プログラムから開く」で選択されたEXEへ、対象ファイルのパスをコマンドライン引数として渡す。v0.1.23までは `Program.Main(string[] args)` がその引数を利用していなかったため、vi_text_editor自体は起動しても対象ファイルを開かなかった。

v0.1.24では通常引数を起動対象パスとして `EditorWorkspaceForm` へ渡し、既存の `OpenPath` 経路で開く。Windows側で一度 `vi_text_editor.exe` を `.txt` の既定アプリとして選択すれば、その後はダブルクリックで対象ファイルを開ける。

## Release運用

`.github/workflows/release.yml` は `main` へのpushで起動する。`ViTextEditor.csproj` の `<Version>` を読み取り、同じversionのReleaseがまだ存在しない場合のみ、テストと配布物の実起動確認を行った後に `vX.Y.Z` タグを作成し、GitHub ReleaseをLatestとして公開する。

今後ユーザーが「マージしてください」と指示した場合は、PRマージ後にRelease workflowの成功、Latest Release、Single-file EXE / Portable ZIP Assetsを確認する。

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
