# vi_text_editor

Windows向けの軽量テキストエディターです。EmEditor／サクラエディタに近い一般的なGUIと使い勝手を持ち、編集操作にはvi系キーバインドを採用します。

## 開発方針

1. 人が自然言語で要件を伝える
2. AIが必要なソースコードを最小差分で修正する
3. AIがハーネス／ガードレールに従って回帰テストする
4. AIがGitHubへPull Requestを作成する
5. 人がPull Requestを確認する
6. 問題なければ人がAIへマージを依頼する
7. AIがPull Requestをマージする
8. AIがその版のWindows x64自己完結ZIPをダウンロード可能にする

## v0.1.10 の主な構成

- .NET 10 + Windows Forms / Scintilla5.NET 7.0.0
- UTF-8 / UTF-8 BOM / UTF-16 / Shift_JISの読み込み・保存
- CRLF / LF / CRの改行コード維持
- 参照モードを既定ON
- Scintilla save pointによる未保存状態追跡
- Vimの `.` に近い直前変更の繰り返し
- `cw/cW/ce/cE/c$`、`dw/dW/de/dE/d$/D` などoperator + motion
- `:e!` / `:e#` / `:q!` / `:w [ファイル名]`
- `:y3` / `:y 3` / `:yank 3` による現在行からの複数行yank
- Scintillaネイティブ位置API、layout cache、描画設定による応答性改善
- OFFSET / HEX / TEXT の3列で閲覧する仮想化バイナリモード
- バイナリTEXT欄でUTF-8 / Shift_JIS / UTF-16 LE / UTF-16 BE / ASCIIを選択可能

## `.` による直前変更の繰り返し

NORMALモードで `.` を押すと、直前の「変更」を繰り返します。移動や単純なyankは変更として記録されません。

- `yy` → `p` → `.` : putをもう一度実行
- `x` → `.` : 1文字削除をもう一度実行
- `dw` → `.` : word削除をもう一度実行
- `cw` → 文字入力 → `Esc` → `.` : change + INSERTした文字列を再適用
- `i` → 文字入力 → `Esc` → `.` : INSERTした変更を繰り返す

INSERT中はScintillaの変更通知から確定済みの挿入・削除を記録するため、日本語IMEの確定文字列やBackspaceを含む変更も繰り返し対象にできます。

## バイナリモード

`表示` → `バイナリモード` で現在の保存済みファイルを生バイト列として閲覧できます。`ファイル` → `バイナリとして開く...` では、通常のテキストデコードを経由せずRAWバイトとして直接開きます。既存のテキスト編集バッファは保持されます。

1行16バイトで次の3列を表示します。

| 列 | 内容 |
|---|---|
| `OFFSET` | ファイル先頭からの16進オフセット |
| `HEX` | `00`〜`FF` の16進バイト列 |
| `TEXT` | 選択した文字コードでデコードした文字 |

TEXT欄の文字コードは、`自動判定 / UTF-8 / Shift_JIS / UTF-16 LE / UTF-16 BE / ASCII` から選択できます。日本語表示用にBIZ UDGothic / MS Gothic等の日本語対応等幅フォントを優先します。

自動判定ではBOMを優先し、BOMがなければUTF-8として妥当か確認し、妥当でなければShift_JISを候補にします。バイナリデータは文字コード情報を持たない場合もあるため、自動判定が意図と異なる場合は手動で切り替えてください。

ファイル全体を巨大なHEX文字列へ変換せず、DataGridViewのVirtualModeと `RandomAccess.Read` で表示対象行を必要時に読み込みます。バイナリモードは閲覧専用です。

## 参照モード

起動時およびファイルを開いた直後は参照モードがONです。

- `モード` → `参照モード` のチェックを外すと編集可能
- 参照モード中は通常入力、vi編集コマンド、Undo/Redoによる文書変更を抑止
- 検索、行ジャンプ、カーソル移動、`:yank` は利用可能
- `:put` と `.` は文書変更を伴うため抑止

## Undoと未保存状態

- 編集するとタイトル先頭に `*` が付く
- `u` / Ctrl+Zで読込時点または直近保存時点まで戻ると `*` が消える
- Redoでsave pointを離れると `*` が再び付く
- 保存成功時はその状態が新しいsave pointになる

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
| `cw` / `ce` / `cW` / `cE` / `c$` | changeしてINSERT |
| `dw` / `dW` / `de` / `dE` / `d$` / `D` | delete |
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

NORMALモードで `:` を押すと画面下部のコマンド入力欄へ移ります。

| コマンド | 動作 |
|---|---|
| `:e!` | 現在ファイルを強制再読込。未保存変更は破棄 |
| `:e#` / `:e #` | 副ファイルを開く |
| `:q!` | 未保存変更を無視して終了 |
| `:w` | 現在ファイルへ保存 |
| `:w ファイル名` | 指定パスへ別名保存 |
| `:120` / `:$` | 指定行 / 最終行へ移動 |
| `:y3` | カーソル行から3行を無名レジスタへyank |
| `:y 3` / `:yank 3` | `:y3` と同じ |
| `:y a 3` | カーソル行から3行をレジスタ `a` へyank |
| `:5y a` / `:5,10y a` / `:%y a` | 指定行または範囲を名前付きレジスタへyank |
| `:pu a` / `:20pu a` / `:0pu a` | 名前付きレジスタをput |

`y3` の3行がファイル末尾を越える場合は、存在する最終行までをyankします。

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
