# CURRENT - v0.1.23

## 目的

v0.1.22のWindows x64 single-file EXEが一部PCで起動直後に終了する問題を修正し、今後はCIで実際の起動まで検証する。

## v0.1.23 スコープ

- [x] Single-file起動時に.NETの `NATIVE_DLL_SEARCH_DIRECTORIES` から自己展開済みネイティブDLLの場所を取得
- [x] Scintillaを最初に生成する前に `ScintillaNativeLibrary.SatelliteDirectory` を設定
- [x] 通常のフォルダー型publishでも従来どおりScintillaを読み込めるfallbackを維持
- [x] `%TEMP%\.net\...` へのbundle展開先も防御的fallbackとして探索
- [x] `--startup-smoke-test` を追加し、EditorWorkspaceForm / MainForm / Scintillaの初期化まで実行
- [x] CIで通常版EXEとsingle-file EXEの両方を実際に起動し、終了コード0を必須化
- [x] `publish_windows_single_file.cmd` でも生成後にstartup smoke testを実施
- [x] README.mdへv0.1.23変更履歴を追記
- [x] ハーネスをv0.1.23へ更新

## 原因

Scintilla5.NET 7.0.0は `Scintilla.dll` と `Lexilla.dll` を実ファイルとして検索する。Single-file publishではネイティブDLLは `%TEMP%\.net\...` へ自己展開されるが、Scintilla側の既定検索ではその展開先を直接参照できず、最初のScintilla生成時に失敗してプロセスが終了する場合がある。

## 実装方針

.NETホストが公開する `NATIVE_DLL_SEARCH_DIRECTORIES` を最優先で確認し、`Scintilla.dll` と `Lexilla.dll` が同一ディレクトリに存在する場所を `ScintillaNativeLibrary.SatelliteDirectory` に設定してからWinForms/Scintillaを初期化する。フォルダー型publish用に `AppContext.BaseDirectory` と実行ファイルディレクトリもfallbackとして残す。

CIではファイル生成の確認だけでは不十分なため、配布EXEへ `--startup-smoke-test` を渡し、ワークスペースとScintilla初期化が例外なく完了することを検証する。

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
