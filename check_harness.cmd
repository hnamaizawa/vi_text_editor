@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo vi_text_editor harness check
echo ============================================================

echo [1/2] Core regression tests
dotnet test tests\ViTextEditor.Core.Tests\ViTextEditor.Core.Tests.csproj --configuration Release
if errorlevel 1 goto :fail

echo [2/2] Windows application build
dotnet build src\ViTextEditor\ViTextEditor.csproj --configuration Release
if errorlevel 1 goto :fail

echo.
echo [PASS] All harness checks passed.
exit /b 0

:fail
echo.
echo [FAIL] Harness check failed.
exit /b 1
