# Definition of Done

変更を完了とみなす条件:

1. `harness/app_blueprint.yaml` の non_negotiable_invariants を破壊していない。
2. vi状態機械の変更にはCore単体テストを追加または更新する。
3. `dotnet test tests/ViTextEditor.Core.Tests/ViTextEditor.Core.Tests.csproj --configuration Release` が成功する。
4. `dotnet build src/ViTextEditor/ViTextEditor.csproj --configuration Release` が成功する。
5. Windows x64 自己完結版の `dotnet publish` が成功し、`vi_text_editor.exe` が生成される。
6. 配布版は利用者PCへの .NET ランタイム／SDKの事前インストールを要求しない。
7. unrelated changes を入れない。
8. ユーザー向け操作が変わった場合はREADMEを更新する。
9. GitHub Pull Requestとして提示し、人の確認後にのみマージする。
10. 単一インスタンス変更では、実プロセス2個を使って後続プロセスのファイルが先行プロセスへ届くことを通常版／Single-file版で確認する。
11. URL操作の変更では、日本語混在、末尾句読点、括弧、URL外位置をCore単体テストで確認し、ブラウザ起動を伴わずURL判定を回帰検証できること。
12. URLリンク表示はファイル読込、入力、貼り付け、削除へ動的に追従し、文書内容、変更状態、Undo履歴を変更しないこと。編集中の更新では文書全体をキー入力ごとに走査しないこと。
13. URL装飾範囲は `http://` / `https://` の先頭から有効な末尾までに限定し、Markdownの箇条書き記号、表示名、区切り括弧、行頭空白を含めないこと。日本語混在時はScintillaのUTF-16コード単位変換で表示位置を求めること。
14. vi operator変更では、`d{count}d` の削除行数・行単位ヤンク・`p/P`・`.` と、change系の削除範囲・INSERTモード遷移をCore単体テストで確認すること。
15. Markdown URL表示ではlexerとURLインジケーターの両方を検証し、表示名・区切り記号・後続文をリンク色にしないこと。タブ変更では、未編集の初期無題タブ置換と並べ替えをWindowsスモークテストで確認すること。
