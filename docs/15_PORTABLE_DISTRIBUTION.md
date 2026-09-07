# Portable Windows Distribution

`vi_text_editor` の通常利用者は .NET SDK / .NET Runtime を事前にインストールする必要はありません。

GitHub Actions の Windows ジョブで次のコマンドを実行し、Windows x64 自己完結版を生成します。

```bat
dotnet publish src\ViTextEditor\ViTextEditor.csproj --configuration Release --runtime win-x64 --self-contained true
```

生成物は `vi_text_editor-win-x64` artifact として保存します。

利用者はartifactを展開し、`vi_text_editor.exe` を起動します。

開発者がローカルで同じ配布物を生成する場合は `publish_windows_portable.cmd` を使用します。
