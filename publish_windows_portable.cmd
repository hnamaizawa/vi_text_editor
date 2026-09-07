@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] 配布版をビルドするには .NET 10 SDK が必要です。
  exit /b 1
)

set "OUTDIR=%~dp0dist\vi_text_editor-win-x64"
if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"
mkdir "%OUTDIR%"

 echo [vi_text_editor] publishing Windows x64 self-contained build...
dotnet publish src\ViTextEditor\ViTextEditor.csproj ^
  --configuration Release ^
  --runtime win-x64 ^
  --self-contained true ^
  --output "%OUTDIR%"
if errorlevel 1 exit /b 1

echo.
echo [SUCCESS] Portable build created:
echo %OUTDIR%\vi_text_editor.exe
endlocal
