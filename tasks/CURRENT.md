# CURRENT - v0.1.16

## 目的

v0.1.15でも一部環境で右下のX/Y座標が見えない問題を解消する。ToolStrip/StatusStripのoverflowレイアウトに依存しない専用表示へ変更し、座標を確実に右下へ表示する。

## v0.1.16 スコープ

- [x] 既存のToolStripStatusLabelによる座標表示をワークスペースでは非表示化
- [x] StatusStrip右端に通常のWinForms `Label` を重ねる専用座標表示へ変更
- [x] StatusStrip右端に座標用190pxを予約し、encoding/EOL表示との重なりを防止
- [x] `X=桁  Y=行` を1始まりで表示
- [x] カーソル移動・マウス操作・vi操作・リサイズ後に座標を更新
- [x] タブ埋め込み時のToolStrip overflowに座標表示を依存させない
- [x] バイナリ表示時は従来のpositionステータス文字列を専用表示へ転記
- [x] v0.1.15のシンタックス強調、タブ、Large File、JSON整形、Markdownプレビュー、vi/Ex操作を維持
- [x] ハーネスをv0.1.16へ更新

## 実装方針

WinFormsのStatusStrip内部アイテムは、埋め込みフォームの幅やToolStripレイアウト計算によってoverflowへ送られる場合がある。v0.1.16では座標をToolStripItemとして扱わず、StatusStripの右端領域に通常のLabelコントロールを重ねる。これによりToolStripのoverflow判定から完全に分離する。

## 次候補

1. UI自動テストを追加し、座標ラベルのVisible/BoundsをWindows CIで検証
2. ユーザーが配色テーマを選べる機能
3. シンタックス強調ON/OFFと拡張子ごとの手動言語指定
4. タブの前回セッション復元、タブのドラッグ並べ替え、ピン留め
5. Markdownライブプレビュー（編集と同期）
6. JSONツリー表示 / JSONPath検索
7. Large Fileモードのバックグラウンド索引作成と進捗表示
8. 数値プレフィックス (`3yy`, `3w`, `5j`, `2dw` など)
