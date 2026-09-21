# CURRENT - v0.1.22

## 目的

別PCへ持ち運ぶ際に、`vi_text_editor.exe` 1ファイルだけをコピーして実行できるWindows x64 single-file self-contained配布を追加する。

## v0.1.22 スコープ

- [x] 従来のWindows x64自己完結フォルダー版を維持
- [x] Windows x64 single-file self-contained publishを追加
- [x] Scintilla等のネイティブライブラリをEXEへ同梱し、実行時自己展開する設定を追加
- [x] Single-file出力が `vi_text_editor.exe` 1ファイルだけであることをCIで検証
- [x] Single-file EXEをGitHub Actions Artifactとして生成
- [x] ローカル用 `publish_windows_single_file.cmd` を追加
- [x] README.mdへv0.1.22変更履歴と1ファイル版利用方法を追記
- [x] ハーネスをv0.1.22へ更新

## 実装方針

`.NET 10` の `PublishSingleFile=true` と `IncludeNativeLibrariesForSelfExtract=true` を利用する。`EnableCompressionInSingleFile=true` で配布サイズを抑え、`DebugType=None` / `DebugSymbols=false` によりPDBを配布出力から除外する。CIでは出力ディレクトリを再帰確認し、ファイル数が1かつ名前が `vi_text_editor.exe` であることを必須条件とする。

従来の `vi_text_editor-win-x64` Artifactは互換性のため残し、新たに `vi_text_editor-win-x64-single-file` Artifactを追加する。

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
