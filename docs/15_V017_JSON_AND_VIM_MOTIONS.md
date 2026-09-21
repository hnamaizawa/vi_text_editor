# v0.1.17 JSON整形 / Vim cursor motion

## JSON整形

`Ctrl+Shift+J` は標準JSONの厳密パースを最初に行う。正しい1行JSON（compact JSON）はそのまま整形できる。

厳密パースが失敗した場合のみ、機械生成データで見られる以下の軽微かつ一意に解釈できる表記揺れを文字列リテラル外で正規化して再試行する。

- 先頭BOM
- JSON標準4種以外のUnicode空白
- `- 12` / `+ 12` のような数値符号と数字の間の空白
- Unicode minus / 全角plus-minus
- `.5` 相当を符号直後に受けた場合の先頭0

quoted string内は一切正規化しない。補正しても解析不能な入力は元データを変更せずエラーにする。

## Vim 9.2 motion.txt と v0.1.17

v0.1.17では通常NORMALモードで日常的に使うcursor motionを拡張する。

- `h j k l`, `0 ^ $`
- `w W b B e E ge gE`
- `gg G`, `N%`
- `%` matching pair: `() [] {}`
- `f F t T`, `; ,`
- `(` `)` sentence
- `{` `}` paragraph
- `H M L`
- `+ - _ |`
- `Ctrl+F Ctrl+B Ctrl+D Ctrl+U Ctrl+E Ctrl+Y`
- 数値プレフィックス（例: `5j`, `3w`, `50%`, `10G`, `3|`, `2gg`）

`f/F/t/T` の次の文字は、`x` 等であっても編集コマンドではなくfind targetとして先に処理する。

## 次段階として分離するVim機能

以下は単純なcursor motionだけではなく、別の状態管理またはoperator統合が必要なためv0.1.17のスコープ外とする。

- operator + motion の完全化: `d%`, `dfx`, `c}`, `y2w`, `3dd` 等
- Visual mode / text object: `iw`, `aw`, `i(`, `a{` 等
- marks / jumplist / changelist: `m{a-z}`, `'a`, `` `a ``, `Ctrl+O`, `Ctrl+I`, `g;`, `g,`
- wrapされたscreen line専用motion: `gj/gk/g0/g^/g$` 等
- section/function motion: `[[`, `]]`, `[]`, `][`

これらは既存編集コマンドとの非退行を保ちながら個別に実装する。
