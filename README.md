# vi_text_editor

Windows向けの軽量テキストエディターです。EmEditor／サクラエディタに近い一般的なGUIと使い勝手を持ち、編集操作にはvi系キーバインドを採用します。

## 開発方針

このプロジェクトは、トレーディングシステムやソフトシンセと同様に、次の流れで開発します。

1. 人が自然言語で要件を伝える
2. AIが必要なソースコードを最小差分で修正する
3. AIがハーネス／ガードレールに従って回帰テストする
4. AIがGitHubへPull Requestを作成する
5. 人がPull Requestを確認する
6. 問題なければ人がAIへマージを依頼する
7. AIがPull Requestをマージする
8. AIがその版のWindows x64自己完結ZIPをダウンロード可能にする

## v0.1.9 の構成

- .NET 10 + Windows Forms
- Scintilla5.NET 7.0.0
- viキーバインドの状態機械を `ViTextEditor.Core` に分離
- UTF-8 / UTF-8 BOM / UTF-16 / Shift_JISの読み込み・保存
- CRLF / LF / CRの改行コードを保存時に維持
- Windows x64 自己完結版（self-contained）をGitHub Actionsで生成
- 参照モードを搭載し、既定でON
- Scintilla save pointで未保存状態を追跡
- Vimの `.` に近い「直前の変更を繰り返す」操作
- `cw/cW/ce/cE/c$`、`dw/dW/de/dE/d$/D` などoperator + motion
- `:e!` / `:e#` / `:q!` / `:w [ファイル名]` のファイル操作
- Scintillaネイティブ位置APIを使い、通常のvi移動・削除で全文コピーを抑制
- WindowsではScintillaの追加BufferedDrawをOFF、表示ページのlayout cacheを有効化
- OFFSET / HEX / ASCII の3列で閲覧する仮想化バイナリモード

## `.` による直前変更の繰り返し

NORMALモードで `.` を押すと、直前の「変更」を繰り返します。移動や単純なyankは変更として記録されません。

例:

- `yy` → `p` → `.` : yank内容のputをもう一度実行
- `x` → `.` : 1文字削除をもう一度実行
- `dw` → `.` : word削除をもう一度実行
- `cw` → 文字入力 → `Esc` → `.` : change + INSERTした文字列を別のwordにも適用
- `i` → 文字入力 → `Esc` → `.` : INSERTした変更を現在位置で繰り返す

INSERT中はScintillaの変更通知から確定済みの挿入・削除を相対位置で記録するため、日本語IMEの確定文字列やBackspaceを含む変更も繰り返し対象にできます。

## 表示・キー応答性

v0.1.9では、通常のviカーソル移動やword/delete処理で毎回ドキュメント全文を取得する方法を減らし、Scintillaの位置・文字・範囲APIを利用します。

さらにWindowsではScintillaの追加BufferedDrawを無効化し、表示ページ単位のlayout cacheを有効にしています。カーソル移動やスクロール時の不要なレイアウト・描画処理を減らすことを狙っています。

## バイナリモード

`表示` → `バイナリモード` で、保存済みファイルを生バイト列として閲覧できます。バイナリモードは読み取り専用です。

画面は1行16バイトで次の3列を表示します。

| 列 | 内容 |
|---|---|
| `OFFSET` | ファイル先頭からの16進オフセット |
| `HEX` | `00`〜`FF` の16進バイト列 |
| `ASCII / TEXT` | 対応する印字可能ASCII文字。非表示文字は `.` |

ファイル全体を巨大なHEX文字列へ変換して保持せず、DataGridViewのVirtualModeと `RandomAccess.Read` で表示対象行を必要時に読み込みます。そのため、大きなファイルでもメモリ消費を抑えた閲覧を目指します。

現時点のバイナリモードは閲覧専用です。バイナリ検索、オフセット指定ジャンプ、選択範囲のコピー・編集は今後の拡張候補です。

## 参照モード

起動時およびファイルを開いた直後は、誤編集防止のため参照モードがONです。

- `モード` → `参照モード` のチェックを外すと編集可能になります。
- 参照モード中は通常入力、viの編集系コマンド、Undo/Redoによる文書変更を抑止します。
- 検索、行ジャンプ、カーソル移動、`:yank` は参照モードでも利用できます。
- `:put` と `.` は文書を変更し得るため参照モード中は実行しません。

## Undoと未保存状態

ファイルを開いた直後にScintillaのUndo履歴をリセットします。このため `Ctrl+Z` や vi の `u` を繰り返しても、Undoは「最初にファイルを開いた状態」で停止します。

- 編集するとタイトル先頭に `*` が付きます。
- `u` / Ctrl+Zで読込時点または直近保存時点のsave pointまで戻ると `*` が自動で消えます。
- Redoでsave pointを離れると `*` が再び付きます。
- 保存成功時にはその状態を新しいsave pointとして設定します。

## 現在利用できるvi操作

| キー | 操作 |
|---|---|
| `Esc` | NORMALモード |
| `i` / `a` | INSERTモード |
| `o` / `O` | 下／上に行を追加してINSERT |
| `h` `j` `k` `l` | カーソル移動 |
| `0` / `^` / `$` | 行頭 / 最初の非空白 / 行末 |
| `w` / `b` / `e` | word単位の移動 |
| `W` / `B` / `E` | WORD単位の移動 |
| `cw` / `ce` | wordをchangeしてINSERT |
| `cW` / `cE` | WORDをchangeしてINSERT |
| `c$` | 行末までchangeしてINSERT |
| `dw` / `dW` | word / WORD motionで削除 |
| `de` / `dE` | word / WORD末尾まで削除 |
| `d$` / `D` | 行末まで削除 |
| `dd` | 1行削除 |
| `.` | 直前の変更を繰り返す |
| `Ctrl+F` / `Ctrl+B` | 約1画面分移動 |
| `Ctrl+D` / `Ctrl+U` | 約半画面分移動 |
| `/文字列` / `?文字列` | 前方 / 後方検索 |
| `n` / `N` | 検索の同方向 / 逆方向繰り返し |
| `gg` / `G` | ファイル先頭 / 最終行 |
| `x` | 1文字削除 |
| `yy` | 1行ヤンク |
| `p` / `P` | ペースト |
| `u` / `Ctrl+R` | Undo / Redo |

## `:` COMMANDモード

NORMALモードで `:` を押すと画面下部のコマンド入力欄へ移ります。`Esc` でキャンセルできます。

| コマンド | 動作 |
|---|---|
| `:e!` | 現在ファイルをディスクから強制再読込。未保存変更は破棄 |
| `:e#` / `:e #` | 副ファイル（alternate file）を開く |
| `:q!` | 未保存変更を無視して強制終了 |
| `:w` | 現在ファイルへ保存 |
| `:w ファイル名` | 指定パスへ別名保存し、そのファイルをcurrent fileにする |
| `:120` / `:$` | 指定行 / 最終行へ移動 |
| `:5y a` / `:5,10y a` / `:%y a` | 行または範囲を名前付きレジスタへyank |
| `:pu a` / `:20pu a` / `:0pu a` | 名前付きレジスタをput |

本家Vimでは `:w 新ファイル` は現在バッファ名を変更せずコピーを書き出す動作ですが、このエディターではユーザー要件に合わせて「別名保存」としてcurrent fileを新しいパスへ切り替えます。

## Windowsで通常利用する方法（.NETのインストール不要）

GitHub Actionsの成功したCIから `vi_text_editor-win-x64` を取得できます。また、AIへ「マージしてください」と依頼した場合は、マージ完了後にその版のZIPダウンロードリンクも提示します。

1. ZIPを任意のフォルダーへ展開する
2. `vi_text_editor.exe` をダブルクリックする

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

Core単体テストのみの場合:

```bat
dotnet test tests\ViTextEditor.Core.Tests\ViTextEditor.Core.Tests.csproj
```
