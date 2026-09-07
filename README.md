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

## v0.1.0 の構成

- .NET 10
- Windows Forms
- Scintilla5.NET 7.0.0
- viキーバインドの状態機械を `ViTextEditor.Core` に分離
- CoreはGUI非依存のため自動テスト可能
- UTF-8 / UTF-8 BOM / UTF-16 / Shift_JISの読み込み・保存
- CRLF / LF / CRの改行コードを保存時に維持
- 未保存変更の終了確認
- GitHub ActionsでCoreテストとWindowsビルドを実行

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

## Windowsでの起動

前提: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) がインストール済みであること。

```bat
git clone https://github.com/hnamaizawa/vi_text_editor.git
cd vi_text_editor
run_windows.cmd
```

または直接実行します。

```bat
dotnet restore src\ViTextEditor\ViTextEditor.csproj
dotnet run --project src\ViTextEditor\ViTextEditor.csproj
```

## 回帰テスト

```bat
check_harness.cmd
```

Core単体テストのみの場合:

```bat
dotnet test tests\ViTextEditor.Core.Tests\ViTextEditor.Core.Tests.csproj
```
