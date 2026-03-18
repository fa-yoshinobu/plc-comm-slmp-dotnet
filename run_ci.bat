@echo off
set PUBLISH_DIR=.\publish

echo ===================================================
echo [CI] Starting .NET Build, Format and Single-File Publish...
echo ===================================================

echo [1/3] Building Project...
dotnet build
if %errorlevel% neq 0 (
    echo [ERROR] Build failed.
    pause & exit /b %errorlevel%
)

echo [2/3] Checking Format...
dotnet format --verify-no-changes
if %errorlevel% neq 0 (
    echo [ERROR] Code format violations found. Run 'dotnet format' to fix.
    pause & exit /b %errorlevel%
)

echo [3/3] Publishing Single-File Executable to %PUBLISH_DIR%...
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:PublishTrimmed=false -o "%PUBLISH_DIR%"
if %errorlevel% neq 0 (
    echo [ERROR] Publish failed.
    pause & exit /b %errorlevel%
)

echo ===================================================
echo [SUCCESS] CI passed and Single-File published to:
echo %cd%\publish
echo ===================================================
pause

