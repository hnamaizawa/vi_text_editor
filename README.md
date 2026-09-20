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

## v0.1.7 の構成

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
- 日本語の等幅表示を優先するフォント設定
- NORMALはブロック、INSERTは太い縦線、`:` / `/` / `?` 入力中はCOMMAND表示
- Vimに近い `w` / `b` / `W` / `B` ナビゲーション
- `cw` / `cW` / `ce` / `c$` のchange操作
- `Ctrl+F` / `Ctrl+B` は1画面、`Ctrl+D` / `Ctrl+U` は半画面移動
- `/` / `?` 検索と `n` / `N` の検索繰り返し
- `:行番号`、Ex形式の `:yank` / `:put` と名前付きレジスタ
- 行・桁表示をScintillaのネイティブ位置APIで更新し、キー入力時の全文走査を廃止

## 参照モード

起動時およびファイルを開いた直後は、誤編集防止のため参照モードがONです。

- `モード` → `参照モード` のチェックを外すと編集可能になります。
- 参照モード中は通常入力、viの編集系コマンド、Undo/Redoによる文書変更を抑止します。
- 検索、行ジャンプ、カーソル移動、`:yank` は参照モードでも利用できます。
- `:put` は文書を変更するため参照モード中は実行しません。
- ステータスバーに `参照` / `編集` を表示します。

## Undoと未保存状態

ファイルを開いた直後にScintillaのUndo履歴をリセットします。このため `Ctrl+Z` や vi の `u` を繰り返しても、Undoは「最初にファイルを開いた状態」で停止し、読込前の空文書まで戻ることはありません。

v0.1.7ではScintillaのsave pointを未保存状態の基準として使います。

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
| `w` / `b` | Vimのword単位で前後移動。記号の連続もword境界として扱う |
| `W` / `B` | 空白区切りの大きなWORD単位で前後移動 |
| `e` | word末尾へ移動 |
| `cw` / `ce` | 現在位置からword末尾まで削除し、その位置でINSERTモードへ入る |
| `cW` | 現在位置から空白区切りWORD末尾まで削除し、その位置でINSERTモードへ入る |
| `c$` | 現在位置から行末まで削除し、その位置でINSERTモードへ入る |
| `Ctrl+F` / `Ctrl+B` | 約1画面分、下／上へ移動。カーソルも追従 |
| `Ctrl+D` / `Ctrl+U` | 約半画面分、下／上へ移動。カーソルも追従 |
| `/文字列` | 前方検索 |
| `?文字列` | 後方検索 |
| `n` | 直前の検索と同じ方向に繰り返す |
| `N` | 直前の検索と逆方向に繰り返す |
| `:行番号` | 指定した行へジャンプ。例 `:120` |
| `:$` | 最終行へジャンプ |
| `gg` / `G` | ファイル先頭／最終行 |
| `x` | 1文字削除 |
| `dd` | 1行削除 |
| `yy` | 1行ヤンク |
| `p` / `P` | ペースト |
| `u` | Undo |
| `Ctrl+R` | Redo |

`cw` / `cW` / `ce` / `c$` で削除された文字列は既存の無名レジスタに保持されます。

## `:` COMMANDモード

NORMALモードで `:` を押すと画面下部のコマンド入力欄へ移ります。`Esc` でキャンセルできます。

VimのExコマンド形式に合わせ、次をサポートします。

| コマンド | 動作 |
|---|---|
| `:120` | 120行目へ移動 |
| `:$` | 最終行へ移動 |
| `:5y a` / `:5ya a` / `:5yank a` | 5行目をレジスタ `a` へyank |
| `:5,10y a` | 5〜10行目をレジスタ `a` へyank |
| `:%y a` | 全行をレジスタ `a` へyank |
| `:pu a` / `:put a` | レジスタ `a` を現在行の後へput |
| `:20pu a` | レジスタ `a` を20行目の後へput |
| `:0pu a` | レジスタ `a` を先頭行より前へput |

大文字レジスタ（例 `A`）へyankすると、小文字レジスタ `a` の既存内容へ追記します。

検索はv0.1.7時点では文字列一致です。Vim正規表現互換は今後の拡張候補です。

## キー入力の応答性

カーソル移動時の行番号・桁位置はScintillaの位置APIから直接取得し、キー入力ごとの全文走査を行いません。不要な `KeyUp` 後の再計算も行わず、Scintillaの `UpdateUI` 通知へ寄せています。

## エディター表示

- 日本語の全角・半角の幅を揃えやすい `BIZ UDGothic` を優先します。
- 利用できない環境では `MS Gothic`、その後 `Consolas` へフォールバックします。
- NORMALモードはブロックカーソル、INSERTモードは3px幅の縦カーソルです。

## Windowsで通常利用する方法（.NETのインストール不要）

1. GitHub の `Actions` を開く
2. 最新の成功した `CI` 実行を開く
3. `Artifacts` から `vi_text_editor-win-x64` をダウンロードする
4. ZIPを任意のフォルダーへ展開する
5. 展開したフォルダー内の `vi_text_editor.exe` をダブルクリックする

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
