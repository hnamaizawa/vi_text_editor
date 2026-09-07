# Definition of Done

変更を完了とみなす条件:

1. `harness/app_blueprint.yaml` の non_negotiable_invariants を破壊していない。
2. vi状態機械の変更にはCore単体テストを追加または更新する。
3. `dotnet test tests/ViTextEditor.Core.Tests/ViTextEditor.Core.Tests.csproj --configuration Release` が成功する。
4. `dotnet build src/ViTextEditor/ViTextEditor.csproj --configuration Release` が成功する。
5. unrelated changes を入れない。
6. ユーザー向け操作が変わった場合はREADMEを更新する。
7. GitHub Pull Requestとして提示し、人の確認後にのみマージする。
