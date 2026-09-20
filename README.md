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

## v0.1.8 の構成

- .NET 10 + Windows Forms
- Scintilla5.NET 7.0.0
- viキーバインドの状態機械を `ViTextEditor.Core` に分離
- UTF-8 / UTF-8 BOM / UTF-16 / Shift_JISの読み込み・保存
- CRLF / LF / CRの改行コードを保存時に維持
- Windows x64 自己完結版（self-contained）をGitHub Actionsで生成
- 配布版の利用時は .NET SDK / .NET Runtime の事前インストール不要
- 参照モードを搭載し、既定でON
- ファイル読込時点をUndoの下限として固定
- Scintilla save pointで未保存状態を追跡
- Vimに近い `w` / `b` / `W` / `B` ナビゲーション
- `cw` / `cW` / `ce` / `cE` / `c$` のchange操作
- `dw` / `dW` / `de` / `dE` / `d$` / `D` のdelete操作
- `/` / `?` 検索と `n` / `N` の検索繰り返し
- `:行番号`、Ex形式の `:yank` / `:put` と名前付きレジスタ
- `:e!` / `:e#` / `:q!` / `:w [ファイル名]` のファイル操作
- current file / alternate file を保持し `:e#` で切替可能

## 参照モード

起動時およびファイルを開いた直後は、誤編集防止のため参照モードがONです。

- `モード` → `参照モード` のチェックを外すと編集可能になります。
- 参照モード中は通常入力、viの編集系コマンド、Undo/Redoによる文書変更を抑止します。
- 検索、行ジャンプ、カーソル移動、`:yank` は参照モードでも利用できます。
- `:put` は文書を変更するため参照モード中は実行しません。
- `:e!` / `:e#` / `:q!` / `:w` はファイル操作として利用できます。

## Undoと未保存状態

ファイルを開いた直後にScintillaのUndo履歴をリセットします。このため `Ctrl+Z` や vi の `u` を繰り返しても、Undoは「最初にファイルを開いた状態」で停止し、読込前の空文書まで戻ることはありません。

- 編集するとタイトル先頭に `*` が付きます。
- `u` / Ctrl+Zで読込時点または直近保存時点のsave pointまで戻ると `*` が自動で消えます。
- Redoでsave pointを離れると `*` が再び付きます。
- 保存成功時にはその状態を新しいsave pointとして設定します。

## 現在利用できるvi操作

| キー | 操作 |
|---|---|
| `Esc` | NORMALモード |
| `i` | INSERTモード |
| `a` | カーソルの次からINSERT |
| `o` / `O` | 下／上に行を追加してINSERT |
| `h` `j` `k` `l` | カーソル移動 |
| `0` | 行の第1文字へ移動 |
| `^` | 行の最初の非空白文字へ移動 |
| `$` | 行末へ移動 |
| `w` / `b` | Vimのword単位で前後移動 |
| `W` / `B` | 空白区切りのWORD単位で前後移動 |
| `e` | word末尾へ移動 |
| `cw` / `ce` | 現在位置からword末尾まで削除しINSERTモードへ入る |
| `cW` / `cE` | 現在位置からWORD末尾まで削除しINSERTモードへ入る |
| `c$` | 現在位置から行末まで削除しINSERTモードへ入る |
| `dw` | word motionで削除。次word直前までの空白も含む |
| `dW` | WORD motionで削除。次WORD直前までの空白も含む |
| `de` / `dE` | word / WORD の末尾まで削除 |
| `d$` / `D` | 現在位置から行末まで削除 |
| `dd` | 1行削除 |
| `Ctrl+F` / `Ctrl+B` | 約1画面分、下／上へ移動。カーソルも追従 |
| `Ctrl+D` / `Ctrl+U` | 約半画面分、下／上へ移動。カーソルも追従 |
| `/文字列` | 前方検索 |
| `?文字列` | 後方検索 |
| `n` | 直前の検索と同じ方向に繰り返す |
| `N` | 直前の検索と逆方向に繰り返す |
| `gg` / `G` | ファイル先頭／最終行 |
| `x` | 1文字削除 |
| `yy` | 1行ヤンク |
| `p` / `P` | ペースト |
| `u` | Undo |
| `Ctrl+R` | Redo |

change/deleteで削除された文字列は既存の無名レジスタに保持されます。

## `:` COMMANDモード

NORMALモードで `:` を押すと画面下部のコマンド入力欄へ移ります。`Esc` でキャンセルできます。

| コマンド | 動作 |
|---|---|
| `:e!` | 現在ファイルをディスクから強制再読込。未保存変更は破棄 |
| `:e#` / `:e #` | 副ファイル（alternate file）を開く。現在ファイルは次のalternateになる |
| `:q!` | 未保存変更を無視して強制終了 |
| `:w` | 現在ファイルへ保存 |
| `:w ファイル名` | 指定パスへ別名保存し、そのファイルをcurrent fileにする |
| `:120` | 120行目へ移動 |
| `:$` | 最終行へ移動 |
| `:5y a` / `:5ya a` / `:5yank a` | 5行目をレジスタ `a` へyank |
| `:5,10y a` | 5〜10行目をレジスタ `a` へyank |
| `:%y a` | 全行をレジスタ `a` へyank |
| `:pu a` / `:put a` | レジスタ `a` を現在行の後へput |
| `:20pu a` | レジスタ `a` を20行目の後へput |
| `:0pu a` | レジスタ `a` を先頭行より前へput |

`e#` のalternate fileは、GUIで別ファイルを開いた場合や `:w ファイル名` で別名保存した場合にも更新されます。

本家Vimでは `:w 新ファイル` は現在バッファの名前を変更せずコピーを書き出す動作ですが、このエディターではユーザー要件に合わせて「別名保存」としてcurrent fileを新しいパスへ切り替えます。

## Windowsで通常利用する方法（.NETのインストール不要）

GitHub Actionsの成功したCIから `vi_text_editor-win-x64` を取得できます。また、AIへ「マージしてください」と依頼した場合は、マージ完了後にその版のZIPダウンロードリンクも提示します。

1. ZIPを任意のフォルダーへ展開する
2. 展開したフォルダー内の `vi_text_editor.exe` をダブルクリックする

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
