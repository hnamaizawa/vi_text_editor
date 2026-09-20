# CURRENT - v0.1.9

## 目的

Vimの `.` による直前変更の繰り返し、Scintilla表示性能の改善、巨大ファイルにも耐えやすい仮想化バイナリビューアを追加する。

## v0.1.9 スコープ

- [x] `.` で直前の変更を繰り返す
- [x] `p` / `P` のputを `.` で繰り返す
- [x] `x`, `dd`, `dw`, `dW`, `de`, `dE`, `d$`, `D` を `.` で繰り返す
- [x] `cw`, `cW`, `ce`, `cE`, `c$` とINSERT入力内容を `.` で繰り返す仕組みを追加
- [x] INSERT中の挿入・削除イベントを相対位置で記録し、IME確定文字列やBackspaceを再現可能にする
- [x] yankや移動だけでは `.` の対象を上書きしない
- [x] viの通常移動・削除処理で全文 `Text` 取得を避け、Scintillaネイティブ位置APIを利用
- [x] Win32でScintillaの追加BufferedDrawをOFF
- [x] 表示ページのLayoutCacheを有効化
- [x] バイナリモードを追加
- [x] バイナリ表示は OFFSET / HEX / ASCII の3列構成
- [x] バイナリ表示をDataGridView VirtualMode + RandomAccessで実装し、全ファイルのHEX文字列化を回避
- [x] 既存のExファイル操作、Undo/save point、参照モード、IME、文字コード/改行維持を継続

## 次候補

1. 数値プレフィックス (`3yy`, `3w`, `5j`, `2dw` など)
2. `yw` / `yW` / `ye` / `y$` のoperator + motion
3. `cc` / `C` / `ciw` / `caw` などchange系拡張
4. `"a yy` / `"a p` などNORMALモードの名前付きレジスタ
5. バイナリモードの検索・位置ジャンプ・選択範囲コピー
6. Vim互換に近い検索正規表現と検索ハイライト
7. Visualモード
8. タブ編集
