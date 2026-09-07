# CURRENT - v0.1.1

## 目的

EmEditor／サクラエディタに近いWindows GUIを持ち、編集キーバインドだけをvi方式にしたテキストエディターを、通常利用者のPCへ .NET を事前インストールせずに利用できる形へする。

## v0.1.1 スコープ

- [x] Windows x64 self-contained publish を追加
- [x] GitHub Actions で `vi_text_editor-win-x64` artifact を生成
- [x] `run_windows.cmd` は配布版EXEを優先して起動
- [x] `publish_windows_portable.cmd` を追加
- [x] ハーネスで self-contained publish と `vi_text_editor.exe` 生成を検証
- [x] README に .NET 不要の通常利用手順を追加
- [x] portable build output を `.gitignore` に追加

## 維持する v0.1.0 機能

- [x] .NET 10 + WinFormsの開発構成
- [x] Scintilla5.NET
- [x] NORMAL / INSERT
- [x] `h j k l`, `w b e`, `0 ^ $`, `gg G`
- [x] `x dd yy p P`, `u Ctrl+R`, `i a o O`
- [x] UTF-8 / UTF-16 / Shift_JIS読み込み・保存
- [x] 未保存変更確認
- [x] Core自動テスト

## 次候補

1. 検索 (`/`, `n`, `N`) とWindows検索ダイアログ
2. タブ編集
3. Visualモード
4. 数値プレフィックス
5. operator + motion
