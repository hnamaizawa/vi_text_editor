# CURRENT - v0.1.13

## 目的

vi_text_editorを日常的に長期利用できるワークスペースへ拡張し、大容量ファイル、複数タブ、最近使ったファイル、JSON整形、Markdownプレビューを安全に追加する。

## v0.1.13 スコープ

- [x] 64MiB以上の通常テキストをLarge Fileモードへ自動切替
- [x] Large Fileモードは `File.ReadAllBytes` / 全文string化を行わない
- [x] `RandomAccess.Read` + 256行単位の疎なラインインデックスで仮想表示
- [x] Large Fileモードで `j/k`, `Ctrl+F/B`, `Ctrl+D/U`, `gg/G`, `/ ? n N`, `:set ic/noic`
- [x] `TabControl` ワークスペースを追加し、各通常テキストタブは独立したMainForm/Scintilla/Undo履歴を保持
- [x] `Ctrl+T`, `Ctrl+W`, `Ctrl+Tab`, `Ctrl+Shift+Tab`
- [x] ファイルメニューへ「最近使ったファイル」を追加
- [x] `%APPDATA%/vi_text_editor/recent-files.json` に最大15件を永続化
- [x] 右下ステータスを1始まりの `X / Y` 座標表示へ変更
- [x] JSON整形 (`Ctrl+Shift+J`) を追加。参照モードでは変更しない
- [x] JSON整形は現在文書の改行コードを維持
- [x] Markdownプレビュー (`Ctrl+Shift+M`) を別タブで表示
- [x] Markdig 1.3.2を利用し、元Markdownテキストは変更しない
- [x] v0.1.12までのvi/Ex/ignorecase/バイナリモードを維持
- [x] ハーネスをv0.1.13へ更新

## 大容量ファイルの方針

通常編集モードはScintillaへ全文を保持するため、64MiB以上では自動的に読み取り専用Large Fileモードを使う。Large Fileモードはファイルを順次走査して行数と疎なチェックポイントのみ作成し、表示対象行を必要時に読み込む。巨大ファイルを編集する機能は今回の対象外とし、閲覧・検索を安全に行うことを優先する。

## 次候補

1. Large Fileモードのバックグラウンド索引作成と進捗表示
2. Large Fileモードで選択範囲コピー、行番号/オフセット直接ジャンプ
3. タブの前回セッション復元、タブのドラッグ並べ替え、ピン留め
4. Markdownライブプレビュー（編集と同期）
5. JSONツリー表示 / JSONPath検索
6. 数値プレフィックス (`3yy`, `3w`, `5j`, `2dw` など)
7. Visualモード
8. Vim互換regexと検索ハイライト拡張
