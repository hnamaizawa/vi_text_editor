# vi_text_editor

Windows向けの軽量テキストエディターです。EmEditor／サクラエディタに近いGUIとファイル操作を持ち、編集操作にはvi系キーバインドを採用します。

## 開発方針

1. 人が自然言語で要件を伝える
2. AIが必要なソースコードを最小差分で修正する
3. AIがハーネス／ガードレールに従って回帰テストする
4. AIがGitHubへPull Requestを作成する
5. 人がPull Requestを確認する
6. 問題なければ人がAIへマージを依頼する
7. AIがPull Requestをマージする
8. AIがその版のWindows x64自己完結ZIPをダウンロード可能にする

## v0.1.17 の主な変更

- JSON整形を強化
  - 改行なしのcompact JSONを回帰テスト対象に追加
  - 標準JSONは従来どおり厳密パースを優先
  - 厳密パース失敗時のみ、文字列外の先頭BOM、Unicode空白、数値符号と数字の間の空白、全角／Unicode符号を安全に正規化して再解析
  - quoted string内の文字は変更しない
  - 改行コード維持、Undo可能、参照モード中は整形を拒否する既存仕様を維持
- Vim 9.2 `motion.txt` を基準にカーソル移動を拡張
  - `%` で `()[]{}` の対応括弧へ移動
  - `N%` でファイル内のN%位置へ移動
  - `f/F/t/T` と `;` / `,` による行内文字移動
  - `ge/gE` で直前のword / WORD末尾へ移動
  - `(`/`)` で文単位移動
  - `{`/`}` で段落単位移動
  - `H/M/L` で画面上部／中央／下部へ移動
  - `+/-/_/|`、`Ctrl+E/Ctrl+Y` を追加
  - `5j`、`3w`、`10G`、`3|`、`2gg`、`50%` など数値プレフィックスに対応
  - `f/F/t/T` の次の文字は編集コマンドより先にmotion targetとして処理

## v0.1.16 の主な変更

- 右下の `X=桁  Y=行` 座標表示をToolStripレイアウトから分離
- StatusStrip右端に座標用の専用領域を確保し、通常のWinForms `Label` を重ねて表示
- `Spring` / `Alignment` / overflowに依存しない構造へ変更
- カーソル移動、マウス操作、vi操作、フォームリサイズ、StatusStripサイズ変更時に座標表示を再配置
- 補助タイマーでも位置を再確認し、常時表示を安定化

## v0.1.15 の主な変更

- StatusStripの座標表示レイアウトを改善
- ファイル拡張子に応じたシンタックス強調を追加
  - Markdown: `.md` / `.markdown`
  - JSON: `.json` / `.jsonl`
  - XML: `.xml` / `.xaml` / `.svg`
  - C#: `.cs` / `.csx`
  - Python: `.py` / `.pyw`
  - JavaScript: `.js` / `.jsx` / `.mjs` / `.cjs`
  - TypeScript: `.ts` / `.tsx`
  - YAML: `.yaml` / `.yml`
- Markdownの `#` 見出しをレベルに応じて太字・サイズ差で強調
- 保存先の拡張子変更時にもシンタックス強調を再判定
- 強調処理は文書本文自体を変更しない

## v0.1.14 の主な変更

- タブを閉じた直後に破棄済みScintillaへアクセスして `ObjectDisposedException` が発生する問題を修正
- `:q` / `:q!` / `:wq` / `:x` など、COMMAND入力のKeyDown処理中に即座にフォームを破棄しないよう終了処理を次のUIメッセージへ遅延
- 破棄途中のStatus更新にも安全ガードを追加
- 右下に `X=桁  Y=行` の1始まり座標表示を追加

## v0.1.13 の主な変更

- 大容量ファイル対応
  - 64MiB以上のファイルは通常の全文読込を避け、Large Fileモードへ自動切替
  - `RandomAccess.Read` と疎な行インデックスで必要な部分だけをオンデマンド読込
  - Large Fileモードは読み取り専用で、巨大ファイルを全文メモリへ展開しない
  - `j/k`、`Ctrl+F/B`、`Ctrl+D/U`、`gg/G`、`/ ? n N`、`:set ic/noic` を利用可能
  - 通常ローダーにも64MiBガードを追加し、`:e` 等から誤って巨大ファイルを全文読込しない
- タブ機能
  - 各タブが独立したScintillaバッファとUndo履歴を保持
  - `Ctrl+T` で新規タブ、`Ctrl+W` で閉じる、`Ctrl+Tab` / `Ctrl+Shift+Tab` で前後タブへ移動
- 「最近使ったファイル」をファイルメニューへ追加
  - 最大15件
  - `%APPDATA%\vi_text_editor\recent-files.json` へ永続化
- JSON整形を追加
  - `ツール` → `JSONを整形`
  - `Ctrl+Shift+J`
  - 現在のCRLF/LFを維持
- Markdownプレビューを追加
  - `ツール` → `Markdownプレビュー`
  - `Ctrl+Shift+M`
  - Markdigを使用し、元のMarkdownを変更せず別タブでHTML表示

## v0.1.12 の主な変更

- `:set ic` / `:set ignorecase` で大文字・小文字を区別しない検索
- `:set noic` / `:set noignorecase` で大文字・小文字を区別する既定動作へ戻す
- `:set ic?` で現在値を確認
- 通常テキスト検索とバイナリTEXT検索の両方で同じ `ignorecase` を共有
- 通常テキスト検索では `\c` / `\C` による検索単位のcase指定にも対応
- バイナリモードでも `:` COMMAND入力を利用可能。`:set ic` / `:set noic` を実行可能
- `:q` / `:q!` / `:wq` / `:x` / `:e ファイル` / `:e! ファイル` / `:saveas ファイル` / `:w!` を追加
- `:d` / `:delete`、`:s` / `:substitute` の基本形を追加
- COMMAND履歴と検索履歴を分離し、入力中の `↑` / `↓` で再利用可能
- 既存の `:y3`、共有レジスタ、バイナリvi移動・検索を維持

## 検索と `ignorecase`

既定ではVimと同じく大文字・小文字を区別します。

```text
:set ic
```

で `ignorecase` をONにすると、たとえば `/hello` で `Hello` / `HELLO` / `hello` を検索できます。

```text
:set noic
:set ic?
```

でOFFまたは現在値の確認ができます。通常テキスト検索では検索パターンの `\c` でその検索だけcase-insensitive、`\C` でcase-sensitiveにできます。

バイナリモードでも `:` を押して `:set ic` / `:set noic` を指定できます。TEXT検索は現在選択中の文字コードでバイト列化し、ASCII英字の大文字・小文字を無視して検索します。`hex:4D 5A` のRAW HEX検索は常に完全一致で、`ignorecase` の対象外です。

## `:` COMMANDモード

NORMALモードで `:` を押すと画面下部のCOMMAND入力欄へ移ります。v0.1.12では、単一バッファのGUIエディタとして意味のある日常的なExコマンドをVimの挙動へ近づけています。

| コマンド | 動作 |
|---|---|
| `:set ic` / `:set ignorecase` | 検索時に大文字・小文字を区別しない |
| `:set noic` / `:set noignorecase` | 大文字・小文字を区別する |
| `:set ic?` | `ignorecase` の現在値を表示 |
| `:q` | 未保存変更がなければ終了。未保存なら終了しない |
| `:q!` | 未保存変更を破棄して終了 |
| `:w` / `:w!` | 現在ファイルを保存 |
| `:w ファイル名` | 指定ファイルへコピーを書き出す。現在バッファ名は変更しない |
| `:w! ファイル名` | 既存ファイルでも強制的にコピーを書き出す |
| `:saveas ファイル名` / `:saveas! ファイル名` | 保存後、現在バッファのファイル名も変更 |
| `:wq` / `:wq!` | 保存して終了 |
| `:x` / `:xit` | 変更がある場合だけ保存して終了 |
| `:e ファイル名` | 指定ファイルを開く。未保存変更がある場合は拒否 |
| `:e! ファイル名` | 未保存変更を破棄して指定ファイルを開く |
| `:e!` | 現在ファイルを強制再読込 |
| `:e#` / `:e #` | alternate fileを開く |
| `:120` / `:$` | 指定行 / 最終行へ移動 |
| `:y3` | カーソル行から3行を無名レジスタへyank |
| `:y a 3` | カーソル行から3行をレジスタ `a` へyank |
| `:5y a` / `:5,10y a` / `:%y a` | 指定行または範囲をyank |
| `:pu a` / `:20pu a` / `:0pu a` | レジスタ内容をput |
| `:d` / `:2,5delete` | 現在行 / 範囲を削除し、削除行をレジスタへ保存 |
| `:s/foo/bar/` | 現在行の最初の一致を置換 |
| `:%s/foo/bar/g` | 全行で全一致を置換 |
| `:%s/foo/bar/gi` | 全行・全一致をcase-insensitiveで置換 |
| `:%s/foo/bar/gI` | `ignorecase` がONでもcase-sensitiveで置換 |
| `:&` | 直前のsubstituteを繰り返す |

`substitute` は.NET正規表現を用いたVim互換の基本サブセットです。Vim固有の正規表現構文を完全に再現するものではありません。

### COMMAND / 検索履歴

`:`, `/`, `?` の入力中に `↑` / `↓` を押すと履歴を呼び出せます。COMMAND履歴と検索履歴は別々に保持します。

**v0.1.12での重要な互換性変更:** v0.1.11以前の `:w ファイル名` はSave As相当でしたが、Vimに合わせて「指定ファイルへ書き出すだけで現在のバッファ名は変えない」動作へ変更しました。現在のファイル名も変更したい場合は `:saveas ファイル名` を使います。

## 大容量ファイル（Large Fileモード）

64MiB以上のテキストファイルは、通常の全文読込ではなくLarge Fileモードで開きます。ファイル全体を巨大な文字列として保持せず、必要な行だけを `RandomAccess.Read` で読み込みます。

Large Fileモードは安全性と省メモリを優先した**読み取り専用**です。検索やvi移動は利用できますが、巨大ファイルを直接編集する用途ではありません。

## タブと最近使ったファイル

- `Ctrl+T`: 新規タブ
- `Ctrl+W`: 現在タブを閉じる
- `Ctrl+Tab`: 次のタブ
- `Ctrl+Shift+Tab`: 前のタブ
- ファイルメニューの「最近使ったファイル」から過去に開いたファイルを再度開けます
- 履歴は最大15件で、アプリ再起動後も保持されます

## JSON整形

`ツール` → `JSONを整形`、または `Ctrl+Shift+J` で現在のJSONを整形します。

標準JSONは厳密に解析します。厳密解析に失敗した場合のみ、機械生成JSONに見られる軽微な表記揺れを文字列外に限って補正し、再解析します。整形後も現在の改行コードを維持します。

## Markdownプレビュー

`ツール` → `Markdownプレビュー`、または `Ctrl+Shift+M` でMarkdownをHTML表示します。元の文書は変更せず、別タブにプレビューを表示します。

## シンタックス強調

拡張子に応じてMarkdown、JSON、XML、C#、Python、JavaScript、TypeScript、YAMLを自動で強調表示します。Markdownでは `#` から始まる見出しをレベルに応じて太字・サイズ差で表示します。

## バイナリモード

`表示` → `バイナリモード` で現在の保存済みファイルを生バイト列として閲覧できます。`ファイル` → `バイナリとして開く...` では、通常のテキストデコードを経由せずRAWバイトとして直接開きます。既存のテキスト編集バッファは保持されます。

1行16バイトで `OFFSET / HEX / TEXT` の3列を表示します。TEXT欄の文字コードは `自動判定 / UTF-8 / Shift_JIS / UTF-16 LE / UTF-16 BE / ASCII` から選択できます。日本語表示用にBIZ UDGothic / MS Gothic等の日本語対応等幅フォントを優先します。

自動判定ではBOMを優先し、BOMがなければUTF-8として妥当か確認し、妥当でなければShift_JISを候補にします。ファイル全体を巨大なHEX文字列へ変換せず、DataGridViewのVirtualModeと `RandomAccess.Read` で必要な行だけ読み込みます。

### バイナリモードのvi操作

| キー | 動作 |
|---|---|
| `j` / `k` | 1行下 / 上へ移動。J/Kも同じ動作 |
| `Ctrl+F` / `Ctrl+B` | 約1画面下 / 上へ移動 |
| `Ctrl+D` / `Ctrl+U` | 約半画面下 / 上へ移動 |
| `gg` / `G` | ファイル先頭 / 末尾へ移動 |
| `/文字列` / `?文字列` | 前方 / 後方TEXT検索 |
| `n` / `N` | 同方向 / 逆方向に検索を繰り返す |
| `/hex:4D 5A 90` | RAWバイト列検索 |
| `:` | COMMAND入力。`:set ic` / `:set noic` 等を利用可能 |

検索は64KB単位で `RandomAccess.Read` し、ファイル全体をメモリへ展開しません。

## レジスタ共有とyank / paste

COMMANDモードの `:yank` とNORMALモードの `yy` / delete / pasteは同じ `ViRegisterStore` を利用します。

```text
:y3
p
```

で、カーソル行から3行をyankした後、NORMALモードの `p` で貼り付けられます。逆にNORMALモードで `yy` した内容を `:put` から利用できます。

参照モード中はyank・検索・`:set` は利用できますが、文書を変更する `p/P`、`:put`、`:delete`、`:substitute`、`.` などは抑止します。

## `.` による直前変更の繰り返し

NORMALモードで `.` を押すと直前の「変更」を繰り返します。移動や単純なyankは変更として記録されません。

- `yy` → `p` → `.` : putをもう一度実行
- `x` → `.` : 1文字削除をもう一度実行
- `dw` → `.` : word削除をもう一度実行
- `cw` → 文字入力 → `Esc` → `.` : change + INSERTした文字列を再適用
- `i` → 文字入力 → `Esc` → `.` : INSERTした変更を繰り返す

## 現在利用できる主なvi操作

| キー | 操作 |
|---|---|
| `Esc` | NORMALモード |
| `i` / `a` | INSERTモード |
| `o` / `O` | 下／上に行を追加してINSERT |
| `h` `j` `k` `l` | カーソル移動 |
| `0` / `^` / `$` | 行頭 / 最初の非空白 / 行末 |
| `w` / `b` / `e`、`W` / `B` / `E` | word / WORD単位移動 |
| `ge` / `gE` | 直前のword / WORD末尾へ移動 |
| `%` | `()[]{}` の対応括弧へ移動 |
| `N%` | ファイル内のN%位置へ移動 |
| `f/F/t/T` | 現在行内の指定文字へ移動 |
| `;` / `,` | 直前の `f/F/t/T` を同方向 / 逆方向へ繰り返す |
| `(` / `)` | 文単位で前 / 後へ移動 |
| `{` / `}` | 段落単位で前 / 後へ移動 |
| `+` / `-` / `_` | 行単位移動と先頭非空白への移動 |
| `|` | 指定桁へ移動（例: `20|`） |
| `H` / `M` / `L` | 画面上部 / 中央 / 下部へ移動 |
| `gg` / `G` | ファイル先頭 / 最終行 |
| `Ctrl+F` / `Ctrl+B` | 約1画面分移動 |
| `Ctrl+D` / `Ctrl+U` | 約半画面分移動 |
| `Ctrl+E` / `Ctrl+Y` | 表示を1行下 / 上へスクロール |
| `5j` / `3w` / `10G` / `2gg` など | 数値プレフィックス付き移動 |
| `/文字列` / `?文字列` / `n` / `N` | 検索と繰り返し |
| `cw` / `ce` / `cW` / `cE` / `c$` | changeしてINSERT |
| `dw` / `dW` / `de` / `dE` / `d$` / `D` / `dd` | delete |
| `x` / `yy` / `p` / `P` | 1文字削除 / yank / paste |
| `.` | 直前の変更を繰り返す |
| `u` / `Ctrl+R` | Undo / Redo |

## Windowsで通常利用する方法（.NETのインストール不要）

GitHub Actionsの成功したCIから `vi_text_editor-win-x64` を取得できます。AIへ「マージしてください」と依頼した場合は、マージ完了後にその版のZIPダウンロードリンクも提示します。

1. ZIPを任意のフォルダーへ展開
2. `vi_text_editor.exe` をダブルクリック

## ソースコードから開発・起動する方法

```bat
git clone https://github.com/hnamaizawa/vi_text_editor.git
cd vi_text_editor
run_windows.cmd
```

既にclone済みの場合:

```bat
cd C:\temp\vi_text_editor
git pull
run_windows.cmd
```

## 回帰テスト

```bat
check_harness.cmd
```

Core単体テストのみ:

```bat
dotnet test tests\ViTextEditor.Core.Tests\ViTextEditor.Core.Tests.csproj
```
