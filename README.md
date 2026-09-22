# vi_text_editor

Windows向けの軽量テキストエディターです。EmEditor／サクラエディタに近いGUIとファイル操作を持ち、基本操作はvi/Vim系キーバインドで行います。

現在のアプリ版は **v0.1.25** です。

## 主な特徴

- .NET 10 + WinForms + Scintilla5.NET
- Windows x64自己完結版に加え、別PCへEXE 1個だけコピーできるsingle-file版を配布
- GitHub ReleasesのLatestからversion付きSingle-file EXE / Portable ZIPを誰でもダウンロード可能
- Windowsの「既定のアプリ」「プログラムから開く」から渡されるファイルを起動時に開く
- 既にvi_text_editorが起動中なら、別ファイルの起動要求を既存ウィンドウの新規タブへ転送
- Explorerから起動中ウィンドウへファイルをドラッグ＆ドロップして新規タブで開く
- 通常利用時は.NETの別途インストール不要
- NORMAL / INSERT / COMMANDを中心としたvi操作
- 日本語IME、UTF-8 / UTF-8 BOM / Shift_JIS / UTF-16、CRLF / LF対応
- 複数タブ、最近使ったファイル、Large Fileモード
- JSON整形、Markdownプレビュー、拡張子別シンタックス強調
- バイナリビューア、文字コード別TEXT表示、vi風移動・検索
- 右下に1始まりの `X=桁 / Y=行` 座標表示
- 既定は編集モード。必要な場合だけ参照モードを明示的にON

## 開発方針

1. 人が自然言語で要件を伝える
2. AIが必要なソースコードを最小差分で修正する
3. AIがハーネス／ガードレールに従って回帰テストする
4. **バージョン変更を含むPRでは、AIがREADME.mdの変更履歴へその版の変更点を追記する**
5. AIがGitHubへPull Requestを作成する
6. 人がPull Requestを確認する
7. 問題なければ人がAIへマージを依頼する
8. AIがPull Requestをマージする
9. versioned merge後、Release workflowが `vX.Y.Z` タグとGitHub Releaseを自動作成しLatestとして公開する
10. AIがLatest ReleaseにSingle-file EXE / Portable ZIPが公開されたことを確認する

## 変更履歴

### v0.1.25

- 同一Windowsユーザー／セッションではvi_text_editorを単一インスタンスとして動作させるよう変更
- 既に起動中の状態で `.txt` 等をダブルクリックした場合、2つ目のプロセスはファイルパスを既存プロセスへ転送して終了
- 既存プロセスは受信したファイルを既存の `OpenPath` 経路で新規タブとして開く
- 同一ファイルが既に開いている場合は重複タブを作らず既存タブを選択
- WindowsのNamed Mutex + Named Pipeを利用し、ユーザー／セッション単位でファイルオープン要求を転送
- 起動中のvi_text_editorへExplorerからファイルをドラッグ＆ドロップして新規タブで開く機能を追加
- 動的に追加されるEditor / Viewer配下のControlにもドロップ受付を再帰的に設定
- 通常版／Single-file版の双方で、一次プロセス起動→二次プロセスへファイル指定→一次側で受信・オープン、までCIスモークテスト
- Release workflowでもsingle-instance forwardingを配布前に再検証

### v0.1.24

- Windows Explorer / 「プログラムから開く」/ 既定アプリから渡されるファイルパスを起動時に開くよう対応
- スペースを含むパスに対応し、複数ファイルが渡された場合は複数タブで開く
- 起動対象ファイルがある場合は余分な空タブを作成しない
- `--startup-open-smoke-test` を追加し、実際のファイルパスを通常版／Single-file版EXEへ渡すCI検証を追加
- `.github/workflows/release.yml` を追加
- versioned PRがmainへマージされた後、`ViTextEditor.csproj` のVersionから `vX.Y.Z` タグを自動作成
- Release前にCore tests、Windows build、通常版／Single-file版publish、実起動、Windows Shell相当のファイルオープンを再検証
- `vi_text_editor-vX.Y.Z-win-x64.exe` と `vi_text_editor-vX.Y.Z-win-x64.zip` をGitHub Release Assetsへ自動登録
- 新しいReleaseをLatestとして公開し、同一versionのReleaseが既に存在する場合は重複公開しない
- 今後「マージしてください」の後はRelease workflow完了とLatest Release Assetsまで確認する運用へ変更

### v0.1.23

- v0.1.22のsingle-file EXEが一部環境で起動直後に終了し、GUIが表示されない問題への対策を追加
- .NET single-fileが自己展開した `Scintilla.dll` / `Lexilla.dll` の実配置先を起動時に探索
- Scintillaコントロール生成前に `ScintillaNativeLibrary.SatelliteDirectory` を設定
- `NATIVE_DLL_SEARCH_DIRECTORIES` を優先し、通常のアプリ配置先と `%TEMP%\.net\...` もフォールバック探索
- `--startup-smoke-test` を追加し、ワークスペースとScintilla初期化まで実際に行う起動検証経路を追加
- GitHub Actionsでフォルダー版／single-file版の両方を実際に起動し、startup smoke test成功を必須化
- Single-file EXEが存在するだけではなく、実際に起動可能であることを配布前に検証する運用へ強化

### v0.1.22

- Windows x64のsingle-file self-contained配布を追加
- `vi_text_editor.exe` 1ファイルだけを別PCへコピーして実行可能
- Scintilla等のネイティブライブラリをEXEへ同梱し、必要時にWindowsの一時領域へ自己展開
- CIでsingle-file出力が `vi_text_editor.exe` 1ファイルだけであることを検証
- GitHub Actionsへ `vi_text_editor-win-x64-single-file` Artifactを追加
- ローカル生成用 `publish_windows_single_file.cmd` を追加
- 従来のフォルダー型 `vi_text_editor-win-x64` 配布も継続

### v0.1.21

- エラー／警告ダイアログ表示時のWindows標準効果音を抑止
- `MessageBoxIcon.Error` / `MessageBoxIcon.Warning` をアプリ共通ラッパーで無音化
- エラー／警告のメッセージ本文とボタン構成は従来どおり維持
- Information / Questionなど通常の情報表示・確認ダイアログの意味は変更しない

### v0.1.20

- Markdown Viewerで `:` COMMAND入力を正式対応
- `:set ic` / `:set noic` / `:set ic?` をMarkdown Viewerから実行可能
- Markdown Viewerの `:` `/` `?` 開始判定をOEMキーコードではなく実際に入力された文字で処理し、JIS／USキーボード配列差へ対応
- COMMAND入力中は画面下部の入力欄と `COMMAND` ステータスを表示
- `Ctrl+PageDown` / `Ctrl+PageUp` を次／前タブへの切替として追加
- `Ctrl+PageDown/PageUp` と既存の `Ctrl+Tab/Ctrl+Shift+Tab` を埋め込みViewerより優先して処理
- キーボードでタブを切り替えた後、選択されたEditor / Markdown Viewer / Large File Viewer本文へ明示的にフォーカス
- Markdown Viewerのスクロール後でもタブ切替キーを安定して利用できるよう改善

### v0.1.19

- 起動時の既定を参照モードから**編集モード**へ変更
- `Ctrl+Shift+J` / `Ctrl+Shift+M` をワークスペース側で一元処理し、現在選択中タブだけを対象化
- JSON整形で日本語を `\uXXXX` 化せず、そのまま読みやすく保持
- Markdown Viewerへ `j/k`, `Ctrl+F/B`, `Ctrl+D/U`, `gg/G` を追加
- Markdown Viewerへ `/`, `?`, `n`, `N` 検索と `:set ic` / `:set noic` を追加
- Markdown Viewerではブラウザ標準ショートカットよりvi操作を優先
- `Ctrl+マウスホイール` のズームをデバウンスし、連続再描画による波打ちを抑制
- ズーム時にテキスト編集画面の表示先頭行、Markdown Viewerのスクロール比率を可能な限り維持

### v0.1.18

- v0.1.17で発生した `:` COMMANDモード入力回帰を修正
- JISキーボードで `:` が `;` motionとして誤認され得るOEMキー依存処理を撤廃
- `:` `/` `?` は従来のCOMMAND/検索入力を必ず優先
- JSON整形の参照モード判定を `Scintilla.ReadOnly` ではなく実際の参照モード状態に修正
- 参照モードOFFなのにReadOnlyが残った場合は編集可能状態へ復旧して整形

### v0.1.17

- compactな1行JSONの整形を回帰テスト化
- 厳密JSON解析失敗時のみ、文字列外のBOM、Unicode空白、数値符号直後の空白、Unicode符号を安全に正規化
- quoted stringの内容は補正対象外
- Vim 9.2 `motion.txt` を基準に主要カーソル移動を拡張
- `%` で `()[]{}` の対応括弧へ移動、`N%` でファイル内割合位置へ移動
- `f/F/t/T`, `;/,`, `ge/gE`, `(`/`)`, `{`/`}`, `H/M/L`, `+/-/_/|`, `Ctrl+E/Y` を追加
- `5j`, `3w`, `10G`, `50%`, `3|`, `2gg` など数値プレフィックス対応

### v0.1.16

- X/Y座標表示をToolStripStatusLabelから分離
- StatusStrip右端へ通常のWinForms Labelとして重ね、ToolStrip overflowに依存しない構造へ変更
- リサイズやカーソル移動でも右下の `X=... Y=...` を維持

### v0.1.15

- StatusStripのスペーサーを整理し、座標表示を右端へ固定
- 拡張子別シンタックス強調を追加
- Markdown、JSON、XML、C#、Python、JavaScript、TypeScript、YAMLに対応
- Markdownの `#`〜`######` 見出しを太字・サイズ差で強調
- Save Asで拡張子が変わった場合もハイライトを再判定

### v0.1.14

- COMMAND入力中の `:q` / `:q!` / `:wq` / `:x` 等で埋め込みタブを閉じた際の `ObjectDisposedException` を修正
- 埋め込みMainFormのCloseを次のWinForms UIメッセージへ遅延
- 右下座標表示を `X=桁  Y=行` の1始まりへ統一

### v0.1.13

- 64MiB以上のファイルを自動的にLarge Fileモードへ切替
- `RandomAccess.Read` と疎な行チェックポイントにより、巨大ファイルを全文メモリ展開せず閲覧
- Large Fileモードへ `j/k`, `Ctrl+F/B`, `Ctrl+D/U`, `gg/G`, `/ ? n N`, `:set ic/noic` を追加
- 複数タブを追加。各タブでテキストバッファ、Undo履歴、vi状態を分離
- `Ctrl+T`, `Ctrl+W`, `Ctrl+Tab`, `Ctrl+Shift+Tab` を追加
- 最近使ったファイルを `%APPDATA%\vi_text_editor\recent-files.json` に最大15件保存
- `Ctrl+Shift+J` でJSON整形
- `Ctrl+Shift+M` でMarkdownプレビューを別タブ表示

### v0.1.12

- `:set ic` / `:set ignorecase`、`:set noic`、`:set ic?` を追加
- 通常検索とバイナリTEXT検索で共通のignorecaseを利用
- 通常検索の `\c` / `\C` override対応
- `:q`, `:q!`, `:w`, `:w!`, `:wq`, `:x`, `:e`, `:e!`, `:e#`, `:saveas` を追加
- `:d` / `:delete`、`:s` / `:substitute`、`:&` を追加
- COMMAND履歴と検索履歴を分離し、`↑/↓` で再利用
- `:w ファイル名` をVimに合わせ「コピー書き出し」に変更し、現在バッファ名を変える場合は `:saveas` を使用

### v0.1.11

- COMMANDモードとNORMALモードで無名レジスタを共有
- `:y3` 等でyankした内容をNORMALの `p/P` から利用可能
- NORMALの `yy` を `:put` から利用可能
- バイナリモードへ `j/k`, `Ctrl+F/B`, `Ctrl+D/U`, `gg/G`, `/ ? n N` を追加
- バイナリ検索へ文字コード対応TEXT検索と `hex:4D 5A 90` 形式のRAW HEX検索を追加
- 検索をストリーミング化し、ファイル全体をメモリ展開しない方式を維持

### v0.1.10

- バイナリビューアのASCII列をTEXT列へ変更
- `自動判定 / UTF-8 / Shift_JIS / UTF-16 LE / UTF-16 BE / ASCII` を選択可能
- BOM優先、次にUTF-8妥当性、最後にShift_JIS候補という自動判定
- 日本語対応等幅フォントを優先
- `:y3`, `:y 3`, `:yank 3`, `:y a 3` のcount yankを追加

### v0.1.9

- Vimの `.` で直前の変更を繰り返す機能を追加
- `p/P`, `x`, `dd`, delete/change、INSERT入力、IME確定入力、Backspaceをrepeat可能に拡張
- vi移動・削除で全文 `Text` を毎回取得しないようScintillaネイティブ位置APIへ移行
- Scintillaの表示キャッシュを改善
- 仮想化バイナリビューアを追加
- `OFFSET / HEX / ASCII(TEXT)` を1行16バイトで表示し、`RandomAccess.Read` を利用

### v0.1.8

- `dw/dW`, `de/dE`, `d$`, `D` を追加
- deleteした文字列を無名レジスタへ保持
- `:e!`, `:e#`, `:q!`, `:w` などExファイル操作を追加
- alternate file管理を追加
- Windows x64自己完結ZIPをマージ後に提示する運用をハーネスへ追加

### v0.1.7

- `cw/cW/ce/cE/c$` を追加し、削除後INSERTへ入るchange操作を実装
- changeで削除した文字列を無名レジスタへ保持
- ScintillaのSavePointを利用してDirty状態を追跡
- Undoで保存時点へ戻るとタイトルの `*` を消し、Redoで離れると再表示

### v0.1.6

- `^` を行の最初の非空白文字への移動として安定化
- COMMANDモード `:` と `:行番号` / `:$` を整備
- Ex形式の `:[range]y[ank] [register]` と `:[line]pu[t] [register]` を追加
- 名前付きレジスタと、大文字レジスタへの追記を追加
- 参照モードでは移動・検索・yankを許可し、putを抑止
- 行・桁表示の計算をScintillaネイティブAPIへ変更し、応答性を改善
- `Ctrl+D/U`, `/ ? n N` などv0.1.5で進めた機能を正式統合

### v0.1.5（v0.1.6へ統合された中間開発版）

- `Ctrl+D` / `Ctrl+U` の半画面移動
- `Ctrl+F` / `Ctrl+B` でカーソルも画面移動へ追従
- `/` / `?` 検索と `n/N` の繰り返し
- `:行番号` による行ジャンプ
- COMMAND入力欄の基盤を追加

この版の作業は後続のPR #6で **v0.1.6** としてまとめて正式マージされました。

### v0.1.4

- NORMALをブロックカーソル、INSERTを3px幅の縦カーソルへ変更
- `BIZ UDGothic` を優先する日本語等幅フォント選択
- `w/b` をVimのword境界に近づけ、`W/B` を追加
- `Ctrl+F/B` のページ移動を追加

### v0.1.3

- 誤編集防止用の参照モードを追加。当時は既定ON
- 参照モード中はScintillaをReadOnly化し、vi編集・Undo/Redoを抑止
- ファイル読込・新規作成直後にUndoバッファをリセット
- Undoを繰り返してもファイル読込前の空文書へ戻らないよう下限を設定

※ v0.1.19で既定は編集モードへ変更され、参照モードは任意でONにする方式になりました。

### v0.1.2

- ScintillaのIME interactionを `SC_IME_INLINE` に設定
- 日本語IMEの変換中文字列をキャレット位置へ直接描画
- Scintillaのハンドル再生成時にもIME設定を再適用

### v0.1.1

- Windows x64 self-contained publishを追加
- GitHub Actionsで `vi_text_editor-win-x64` Artifactを自動生成
- `run_windows.cmd` が配布版EXEを優先して起動
- `publish_windows_portable.cmd` を追加
- 通常利用では.NET Runtime / SDKを別途インストールせず実行可能

### v0.1.0

- 初期実装
- .NET 10 + WinForms + Scintilla5.NET 7.0.0
- viキー処理を `ViTextEditor.Core` へ分離
- NORMAL / INSERT、`h j k l`, `w b e`, `0 ^ $`, `gg G`
- `x`, `dd`, `yy`, `p/P`, `u`, `Ctrl+R`, `i/a/o/O`
- INSERT中はIME/Scintillaへ通常入力を渡す設計
- UTF-8 / UTF-8 BOM / UTF-16 / Shift_JISの読み込み・保存
- 未保存変更の終了確認
- ハーネス、Definition of Done、Known Issues、GitHub Actionsを導入

## 現在利用できる主なvi操作

| キー | 操作 |
|---|---|
| `Esc` | NORMALモード |
| `i` / `a` | INSERTモード |
| `o` / `O` | 下／上に行を追加してINSERT |
| `h` `j` `k` `l` | カーソル移動 |
| `0` / `^` / `$` | 行頭 / 最初の非空白 / 行末 |
| `w/b/e`, `W/B/E`, `ge/gE` | word / WORD単位移動 |
| `%` | `()[]{}` の対応括弧へ移動。数値付き `N%` はファイル内割合位置 |
| `f/F/t/T` | 行内の指定文字へ移動 |
| `;` / `,` | 直前のf/F/t/Tを同方向 / 逆方向に繰り返す |
| `(` / `)` | 前 / 次の文へ移動 |
| `{` / `}` | 前 / 次の段落へ移動 |
| `H/M/L` | 画面上 / 中央 / 下へ移動 |
| `+/-/_/|` | 行・桁単位の移動 |
| `Ctrl+F/B` | 約1画面下 / 上 |
| `Ctrl+D/U` | 約半画面下 / 上 |
| `Ctrl+E/Y` | 画面を1行スクロール |
| `gg/G` | ファイル先頭 / 最終行 |
| `/文字列` / `?文字列` / `n` / `N` | 検索と繰り返し |
| `cw/cW/ce/cE/c$` | changeしてINSERT |
| `dw/dW/de/dE/d$`, `D`, `dd` | delete |
| `x/yy/p/P` | 1文字削除 / yank / paste |
| `.` | 直前の変更を繰り返す |
| `u` / `Ctrl+R` | Undo / Redo |
| `5j`, `3w`, `10G`, `2gg` 等 | 数値プレフィックス付き移動 |

## `:` COMMANDモード

NORMALモードで `:` を押すと画面下部のCOMMAND入力欄へ移ります。

| コマンド | 動作 |
|---|---|
| `:set ic` / `:set ignorecase` | 検索時に大文字・小文字を区別しない |
| `:set noic` / `:set noignorecase` | 大文字・小文字を区別する |
| `:set ic?` | `ignorecase` の現在値を表示 |
| `:q` / `:q!` | 通常終了 / 未保存変更を破棄して終了 |
| `:w` / `:w!` | 現在ファイルを保存 |
| `:w ファイル名` | 指定ファイルへコピーを書き出し。現在バッファ名は変更しない |
| `:saveas ファイル名` / `:saveas! ファイル名` | 保存後、現在バッファのファイル名も変更 |
| `:wq` / `:wq!` | 保存して終了 |
| `:x` / `:xit` | 変更がある場合だけ保存して終了 |
| `:e ファイル名` | 指定ファイルを開く。未保存変更がある場合は拒否 |
| `:e! ファイル名` / `:e!` | 強制的に開く / 現在ファイルを再読込 |
| `:e#` | alternate fileを開く |
| `:120` / `:$` | 指定行 / 最終行へ移動 |
| `:y3` / `:y a 3` | カーソル行から複数行をyank |
| `:5y a` / `:5,10y a` / `:%y a` | 指定行または範囲をyank |
| `:pu a` / `:20pu a` / `:0pu a` | レジスタ内容をput |
| `:d` / `:2,5delete` | 現在行 / 範囲を削除してレジスタへ保存 |
| `:s/foo/bar/` | 現在行の最初の一致を置換 |
| `:%s/foo/bar/g` | 全行で全一致を置換 |
| `:&` | 直前のsubstituteを繰り返す |

`substitute` は.NET正規表現を用いたVim互換の基本サブセットです。Vim固有の正規表現構文を完全に再現するものではありません。

## タブと最近使ったファイル

- `Ctrl+T`: 新しいタブ
- `Ctrl+W`: 現在のタブを閉じる
- `Ctrl+Tab` / `Ctrl+PageDown`: 次のタブ
- `Ctrl+Shift+Tab` / `Ctrl+PageUp`: 前のタブ
- キーボードでタブ切替後は選択されたタブ本文へフォーカス
- 最近使ったファイルは最大15件保存

## Large Fileモード

64MiB以上のファイルは自動的に読み取り専用Large Fileモードで開きます。`RandomAccess.Read` と疎な行インデックスを利用し、ファイル全体を巨大なbyte配列やstringへ展開しません。

利用可能な主な操作:

- `j/k`
- `Ctrl+F/B`
- `Ctrl+D/U`
- `gg/G`
- `/ ? n N`
- `:set ic` / `:set noic`

## JSON整形

`Ctrl+Shift+J` または `ツール > JSONを整形` を使用します。

- compactな1行JSONに対応
- 現在の改行コードを維持
- 日本語は `\uXXXX` にせず読みやすい文字のまま保持
- 参照モードでは変更しない
- 軽微な機械生成JSONの表記揺れは、quoted stringを変更しない範囲で補正して再解析

## Markdownプレビュー

`Ctrl+Shift+M` または `ツール > Markdownプレビュー` で別タブに表示します。元のMarkdown本文は変更しません。

Viewerでも基本操作をviに統一しています。

- `j/k`
- `Ctrl+F/B`
- `Ctrl+D/U`
- `gg/G`
- `/ ? n N`
- `:` でCOMMAND入力
- `:set ic` / `:set noic` / `:set ic?`
- `Ctrl+PageDown/PageUp` または `Ctrl+Tab/Ctrl+Shift+Tab` で他タブへ移動

`:` `/` `?` はキーボード配列依存のOEMキーコードではなく、実際に入力された文字で判定します。

## シンタックス強調

現在は以下を拡張子から自動判定します。

- Markdown: `.md`, `.markdown`
- JSON: `.json`, `.jsonl`
- XML: `.xml`, `.xaml`, `.svg`
- C#: `.cs`, `.csx`
- Python: `.py`, `.pyw`
- JavaScript: `.js`, `.jsx`, `.mjs`, `.cjs`
- TypeScript: `.ts`, `.tsx`
- YAML: `.yaml`, `.yml`

Markdownの見出しは太字・サイズ差で強調します。ハイライト処理は文書本文とUndo履歴を変更しません。

## バイナリモード

`表示 > バイナリモード`、または `ファイル > バイナリとして開く...` でRAWバイトを閲覧できます。

- `OFFSET / HEX / TEXT` の3列
- 1行16バイト
- TEXT文字コード: 自動判定 / UTF-8 / Shift_JIS / UTF-16 LE / UTF-16 BE / ASCII
- DataGridView VirtualMode + `RandomAccess.Read`
- 日本語対応等幅フォントを優先

主なvi操作:

- `j/k`
- `Ctrl+F/B`
- `Ctrl+D/U`
- `gg/G`
- `/文字列`, `?文字列`, `n/N`
- `/hex:4D 5A 90`
- `:set ic` / `:set noic`

## Windowsで通常利用する方法（.NETのインストール不要）

一般利用者にはGitHub Releasesの **Latest** から取得する方法を推奨します。

- Latest Release: `https://github.com/hnamaizawa/vi_text_editor/releases/latest`
- 推奨: `vi_text_editor-vX.Y.Z-win-x64.exe` — 1ファイルだけで動作するSingle-file版
- 代替: `vi_text_editor-vX.Y.Z-win-x64.zip` — 従来のフォルダー型自己完結版
- どちらも別途.NET Runtime / SDKをインストールする必要はありません

GitHub ActionsのArtifactにも開発確認用として `vi_text_editor-win-x64-single-file` / `vi_text_editor-win-x64` を残しますが、一般配布はRelease Assetsを使用します。

### `.txt` をダブルクリックしてvi_text_editorで開く

v0.1.24以降はWindowsから渡されたファイルパスを起動時に開きます。最初にWindows側でvi_text_editorを既定アプリとして選択してください。

1. Latest ReleaseからSingle-file EXEを取得する
2. `C:\Tools\vi_text_editor\vi_text_editor.exe` など、今後も変えない固定パス・固定ファイル名へコピーする
3. `.txt` ファイルを右クリック → **プログラムから開く** → **別のプログラムを選択**
4. **PCでアプリを選択する** から上記 `vi_text_editor.exe` を指定する
5. 「常にこのアプリを使う」に相当する選択肢を有効にする

以後は `.txt` のダブルクリックで対象ファイルがvi_text_editorに渡され、そのファイルを開きます。スペースを含むパスにも対応します。複数ファイルを一度に渡された場合は複数タブで開きます。

**v0.1.25以降は、vi_text_editorが既に起動している場合も新しいウィンドウを増やさず、既存ウィンドウへファイルパスを転送して新規タブで開きます。** 同じファイルが既に開いている場合はそのタブを選択します。

Windowsの既定アプリ設定はEXEのフルパスを保持するため、更新時も `C:\Tools\vi_text_editor\vi_text_editor.exe` を新しいSingle-file EXEで**同じ場所に上書き**する方法を推奨します。

### 起動中のvi_text_editorへファイルをドラッグ＆ドロップ

v0.1.25以降はExplorerからファイルをvi_text_editorのウィンドウへドラッグ＆ドロップすると、そのファイルを新規タブで開きます。複数ファイルの同時ドロップにも対応します。同じファイルが既に開いている場合は重複タブを作らず、そのタブを選択します。

## GitHub Releaseの自動公開

v0.1.24以降、versioned PRが `main` へマージされるとRelease workflowが起動します。同じversionのReleaseがまだ存在しない場合、次の処理を自動で実行します。

1. Core tests / Windows build
2. Portable版 / Single-file版をpublish
3. 両方のEXEで通常起動スモークテスト
4. 両方のEXEへスペースを含む `.txt` パスを渡すWindows Shell相当のオープンスモークテスト
5. 両方のEXEで一次プロセス起動→二次プロセスからファイル転送→一次側でオープン、のsingle-instance forwardingスモークテスト
6. プロジェクトVersionと同じ `vX.Y.Z` タグを作成
7. version付きSingle-file EXE / Portable ZIPをRelease Assetsへ登録
8. ReleaseをLatestとして公開

同じversionのReleaseが既に存在するmain更新では、重複Releaseを作成しません。

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

フォルダー型の自己完結版をローカル生成:

```bat
publish_windows_portable.cmd
```

1ファイル版をローカル生成:

```bat
publish_windows_single_file.cmd
```

## 回帰テスト

```bat
check_harness.cmd
```

Core単体テストのみ:

```bat
dotnet test tests\ViTextEditor.Core.Tests\ViTextEditor.Core.Tests.csproj
```