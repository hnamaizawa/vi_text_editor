# CURRENT - v0.1.12

## 目的

Vimの `:set ignorecase` を通常テキストとバイナリ検索の両方へ適用し、単一バッファGUIエディタとして日常的に使うEx/COMMAND操作をVim互換へ近づける。

## v0.1.12 スコープ

- [x] `:set ic` / `:set ignorecase` でcase-insensitive検索を有効化
- [x] `:set noic` / `:set noignorecase` でcase-sensitiveへ戻す
- [x] `:set ic?` と `:set ic!` / `:set invic` をサポート
- [x] 通常検索で `\c` / `\C` の検索単位case overrideをサポート
- [x] バイナリモードでも `:` COMMAND入力を有効化し、`:set ic` / `:set noic` を使用可能にする
- [x] バイナリTEXT検索へignorecaseを適用し、RAW `hex:` 検索は完全一致を維持
- [x] `:q` はdirty bufferを拒否し、`:q!` は強制終了するVim動作へ揃える
- [x] `:wq` / `:wq!` / `:x` / `:xit` を追加
- [x] `:e ファイル` / `:e! ファイル` を追加
- [x] `:w ファイル` をVim同様のwrite-copy動作へ変更し、`:saveas` で現在ファイル名を変更
- [x] `:w! ファイル` / `:saveas!` の強制上書きを追加
- [x] `:d` / `:delete` とrangeを追加し、削除行を共通レジスタへ格納
- [x] `:s` / `:substitute` の基本形、range、`g/i/I/e` flag、`:&` repeatを追加
- [x] COMMAND履歴と検索履歴を分離し、入力中 `↑` / `↓` で履歴を再利用
- [x] v0.1.11の共有レジスタ、dot repeat、バイナリ仮想表示・vi移動・検索を維持
- [x] バージョン表示・README・ハーネスをv0.1.12へ更新

## 互換性の範囲

VimのExはスクリプト、複数window/buffer、quickfix、shell/filter、autocommandなど非常に広範囲であるため、v0.1.12ではこの単一ウィンドウ型エディタで意味のある基本操作を優先する。`substitute` の正規表現は.NET Regexによる基本互換で、Vim固有regex構文の完全再現は対象外。

## 次候補

1. 数値プレフィックス (`3yy`, `3w`, `5j`, `2dw` など)
2. `yw` / `yW` / `ye` / `y$` のoperator + motion
3. `cc` / `C` / `ciw` / `caw` などchange系拡張
4. `"a yy` / `"a p` などNORMALモードの名前付きレジスタ
5. Exの `:copy` / `:move` / `:join` / `:print` / `:global` 等の行指向操作
6. バイナリ検索結果のバイト単位ハイライト、オフセット直接ジャンプ、選択範囲コピー
7. Vim互換regexの拡張と検索ハイライト
8. Visualモード
9. タブ編集
