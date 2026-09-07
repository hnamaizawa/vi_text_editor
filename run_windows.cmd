@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] .NET SDK が見つかりません。.NET 10 SDK をインストールしてください。
  exit /b 1
)

echo [vi_text_editor] restoring packages...
dotnet restore src\ViTextEditor\ViTextEditor.csproj
if errorlevel 1 exit /b 1

echo [vi_text_editor] starting...
dotnet run --project src\ViTextEditor\ViTextEditor.csproj
endlocal
