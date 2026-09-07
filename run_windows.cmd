@echo off
setlocal
cd /d "%~dp0"

set "PORTABLE_EXE=%~dp0dist\vi_text_editor-win-x64\vi_text_editor.exe"
if exist "%PORTABLE_EXE%" (
  echo [vi_text_editor] starting portable build...
  start "" "%PORTABLE_EXE%"
  exit /b 0
)

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] 実行可能な配布版が見つかりません。
  echo.
  echo 通常利用では .NET SDK は不要です。
  echo GitHub Actions から vi_text_editor-win-x64 の配布ZIPを取得して展開し、
  echo vi_text_editor.exe を実行してください。
  echo.
  echo 開発者としてソースコードから起動する場合のみ .NET 10 SDK が必要です。
  exit /b 1
)

echo [vi_text_editor] development mode: restoring packages...
dotnet restore src\ViTextEditor\ViTextEditor.csproj
if errorlevel 1 exit /b 1

echo [vi_text_editor] development mode: starting...
dotnet run --project src\ViTextEditor\ViTextEditor.csproj
endlocal
