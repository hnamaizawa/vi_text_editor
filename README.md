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

## v0.1.1 の構成

- .NET 10 + Windows Forms
- Scintilla5.NET 7.0.0
- viキーバインドの状態機械を `ViTextEditor.Core` に分離
- CoreはGUI非依存のため自動テスト可能
- UTF-8 / UTF-8 BOM / UTF-16 / Shift_JISの読み込み・保存
- CRLF / LF / CRの改行コードを保存時に維持
- 未保存変更の終了確認
- Windows x64 自己完結版（self-contained）をGitHub Actionsで生成
- 配布版の利用時は .NET SDK / .NET Runtime の事前インストール不要

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

Visualモード、`:` コマンド、`/` 検索、数値プレフィックスなどは今後追加します。

## Windowsで通常利用する方法（.NETのインストール不要）

通常利用者は .NET 10 SDK をインストールする必要はありません。

1. GitHub の `Actions` を開く
2. 最新の成功した `CI` 実行を開く
3. `Artifacts` から `vi_text_editor-win-x64` をダウンロードする
4. ZIPを任意のフォルダーへ展開する
5. 展開したフォルダー内の `vi_text_editor.exe` をダブルクリックする

自己完結版には実行に必要な .NET ランタイムが同梱されます。

## ソースコードから開発・起動する方法

こちらは開発者向けです。前提として .NET 10 SDK が必要です。

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

`run_windows.cmd` は `dist\vi_text_editor-win-x64\vi_text_editor.exe` が存在する場合は配布版を優先して起動し、存在しない場合のみ .NET SDK を使った開発モードで起動します。

直接開発モードで起動する場合:

```bat
dotnet restore src\ViTextEditor\ViTextEditor.csproj
dotnet run --project src\ViTextEditor\ViTextEditor.csproj
```

## 自己完結版をローカルで作成する方法

.NET 10 SDK がインストールされた開発PCでは次を実行できます。

```bat
publish_windows_portable.cmd
```

生成先:

```text
dist\vi_text_editor-win-x64\vi_text_editor.exe
```

この `dist\vi_text_editor-win-x64` フォルダー一式を別のWindows x64 PCへコピーすれば、そのPCに .NET がインストールされていなくても実行できます。

## 回帰テスト

```bat
check_harness.cmd
```

ハーネスでは次を確認します。

- Core単体テスト
- WindowsアプリのReleaseビルド
- Windows x64自己完結版のpublish
- `vi_text_editor.exe` が生成されること

Core単体テストのみの場合:

```bat
dotnet test tests\ViTextEditor.Core.Tests\ViTextEditor.Core.Tests.csproj
```
