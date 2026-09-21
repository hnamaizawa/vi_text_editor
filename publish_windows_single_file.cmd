@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] 1ファイル版をビルドするには .NET 10 SDK が必要です。
  exit /b 1
)

set "OUTDIR=%~dp0dist\vi_text_editor-win-x64-single-file"
if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"
mkdir "%OUTDIR%"

echo [vi_text_editor] publishing Windows x64 single-file self-contained build...
dotnet publish src\ViTextEditor\ViTextEditor.csproj ^
  --configuration Release ^
  --runtime win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  --output "%OUTDIR%"
if errorlevel 1 exit /b 1

set /a FILECOUNT=0
for /r "%OUTDIR%" %%F in (*) do set /a FILECOUNT+=1
if not "%FILECOUNT%"=="1" (
  echo [ERROR] Single-file publish output contains %FILECOUNT% files. Expected exactly 1.
  dir /s /b "%OUTDIR%"
  exit /b 1
)

if not exist "%OUTDIR%\vi_text_editor.exe" (
  echo [ERROR] vi_text_editor.exe was not created.
  exit /b 1
)

echo.
echo [SUCCESS] Single-file build created:
echo %OUTDIR%\vi_text_editor.exe
endlocal
