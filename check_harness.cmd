@echo off
setlocal
cd /d "%~dp0"

set "PUBLISH_DIR=%TEMP%\vi_text_editor_harness_publish"
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"

 echo ============================================================
echo vi_text_editor harness check
echo ============================================================

echo [1/3] Core regression tests
dotnet test tests\ViTextEditor.Core.Tests\ViTextEditor.Core.Tests.csproj --configuration Release
if errorlevel 1 goto :fail

echo [2/3] Windows application build
dotnet build src\ViTextEditor\ViTextEditor.csproj --configuration Release
if errorlevel 1 goto :fail

echo [3/3] Windows x64 self-contained publish
dotnet publish src\ViTextEditor\ViTextEditor.csproj --configuration Release --runtime win-x64 --self-contained true --output "%PUBLISH_DIR%"
if errorlevel 1 goto :fail

if not exist "%PUBLISH_DIR%\vi_text_editor.exe" (
  echo [ERROR] vi_text_editor.exe was not created.
  goto :fail
)

rmdir /s /q "%PUBLISH_DIR%"
echo.
echo [PASS] All harness checks passed.
exit /b 0

:fail
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
echo.
echo [FAIL] Harness check failed.
exit /b 1
