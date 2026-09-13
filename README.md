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

## v0.1.3 の構成

- .NET 10 + Windows Forms
- Scintilla5.NET 7.0.0
- viキーバインドの状態機械を `ViTextEditor.Core` に分離
- UTF-8 / UTF-8 BOM / UTF-16 / Shift_JISの読み込み・保存
- CRLF / LF / CRの改行コードを保存時に維持
- Windows x64 自己完結版（self-contained）をGitHub Actionsで生成
- 配布版の利用時は .NET SDK / .NET Runtime の事前インストール不要
- 参照モードを搭載し、既定でON
- ファイル読込時点をUndoの下限として固定

## 参照モード

起動時およびファイルを開いた直後は、誤編集防止のため参照モードがONです。

- `モード` → `参照モード` のチェックを外すと編集可能になります。
- 参照モード中は通常入力、viの編集系コマンド、Undo/Redoによる文書変更を抑止します。
- ステータスバーに `参照` / `編集` を表示します。

## Undoの安全動作

ファイルを開いた直後にScintillaのUndo履歴をリセットします。このため `Ctrl+Z` や vi の `u` を繰り返しても、Undoは「最初にファイルを開いた状態」で停止し、読込前の空文書まで戻ることはありません。

## 現在利用できるvi操作

| キー | 操作 |
|---|---|
| `Esc` | NORMALモード |
| `i` | INSERTモード |
| `a` | カーソルの次からINSERT |
| `o` / `O` | 下／上に行を追加してINSERT |
| `h` `j` `k` `l` | カーソル移動 |
| `w` `b` `e` | 単語移動 |
| `0` `^` `$` | 行内移動 |
| `gg` / `G` | ファイル先頭／最終行 |
| `x` | 1文字削除 |
| `dd` | 1行削除 |
| `yy` | 1行ヤンク |
| `p` / `P` | ペースト |
| `u` | Undo |
| `Ctrl+R` | Redo |

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
