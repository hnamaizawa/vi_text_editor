# CURRENT - v0.1.19

## 目的

常用エディタとしての既定挙動を編集優先へ変更し、JSON整形・Markdown Viewer・ズーム操作の使い勝手を改善する。

## v0.1.19 スコープ

- [x] 起動時・新規タブの既定を編集モードへ変更
- [x] 参照モードは明示的にONにする安全モードとして維持
- [x] `Ctrl+Shift+J` / `Ctrl+Shift+M` をワークスペースで一元処理し、必ず選択中タブだけへ送る
- [x] 非アクティブタブのToolStripショートカット競合を無効化
- [x] JSON整形で日本語を `\uXXXX` へ強制変換せず、そのまま読みやすく出力
- [x] JSONとして必要な引用符・バックスラッシュ・制御文字のエスケープは維持
- [x] Markdown Viewerへ `j/k`, `Ctrl+F/B`, `Ctrl+D/U`, `gg/G` を追加
- [x] Markdown Viewerへ `/ ? n N` 検索を追加
- [x] Markdown Viewerで `:set ic` / `:set noic` を利用可能にし、検索へ反映
- [x] Ctrl+マウスホイール拡大縮小をデバウンスし、連続再描画による波打ちを抑制
- [x] テキストエディタのズーム時は先頭表示行を可能な限り維持
- [x] Markdown Viewerのズーム時はスクロール位置の比率を可能な限り維持
- [x] ハーネスをv0.1.19へ更新

## 実装方針

ワークスペースがアクティブタブを唯一の操作対象としてショートカットをルーティングする。各埋め込みMainFormのメニューにはショートカット表示だけを残し、実際のキー処理はワークスペースで1回だけ行う。

Markdown Viewerはバイナリ／Large File Viewerと同じvi閲覧思想に揃え、WebBrowser上のスクロールと検索をviキーから操作する。Ctrl+ホイールはネイティブ連続ズームを抑止し、短時間の入力をまとめて1回のズームへ反映する。

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
