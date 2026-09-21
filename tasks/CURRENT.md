# CURRENT - v0.1.17

## 目的

改行のないJSONの整形を安定させ、Vim 9.2 `motion.txt` と照合して常用頻度の高い未対応カーソル移動を追加する。

## v0.1.17 スコープ

- [x] 正しい1行compact JSONを整形できることを回帰テスト化
- [x] 厳密JSONパースを最優先し、失敗時のみ機械生成JSONの軽微な表記揺れを安全に補正
- [x] 先頭BOM、文字列外Unicode空白、数値符号直後の空白、Unicode/全角符号を補正
- [x] JSON文字列リテラル内部は補正しない
- [x] `%` で `()[]{}` の対応括弧へ移動
- [x] `N%` でファイルのN%位置へ移動
- [x] `f/F/t/T` と `;/,` を追加
- [x] `(`/`)` の文移動、`{`/`}` の段落移動を追加
- [x] `H/M/L` の画面位置移動を追加
- [x] `+/-/_/|` を追加
- [x] `Ctrl+E/Ctrl+Y` の表示スクロールを追加
- [x] `ge/gE` を追加
- [x] カーソル移動へ数値プレフィックスを追加（`5j`, `3w`, `50%`, `10G`, `3|`, `2gg` 等）
- [x] `f/F/t/T` の対象文字を編集コマンドより先にmotion targetとして処理
- [x] v0.1.16の座標表示、タブ、履歴、シンタックス強調、Large File、Markdown、Ex機能を維持
- [x] ハーネスをv0.1.17へ更新

## 実装方針

カーソル移動ロジックは `ViNavigationProcessor` に集約し、Coreで単体テスト可能な状態を維持する。Windows側では埋め込みMainFormの `KeyPreview` を利用し、motionキーをScintilla/編集コマンドより先に処理する。JSONは標準準拠の厳密パースを最初に試し、明確に安全な表記揺れだけを文字列外で正規化して再試行する。

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
