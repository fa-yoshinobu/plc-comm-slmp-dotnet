@echo off
setlocal

echo ===================================================
echo [CI] Build, test, and format verification
echo ===================================================

echo [1/3] Build
dotnet build PlcComm.Slmp.sln
if %errorlevel% neq 0 (
    echo [ERROR] Build failed.
    exit /b %errorlevel%
)

echo [2/3] Test
dotnet test PlcComm.Slmp.sln --no-build
if %errorlevel% neq 0 (
    echo [ERROR] Tests failed.
    exit /b %errorlevel%
)

echo [3/3] Format check
dotnet format PlcComm.Slmp.sln --verify-no-changes
if %errorlevel% neq 0 (
    echo [ERROR] Format violations found.
    exit /b %errorlevel%
)

echo ===================================================
echo [SUCCESS] CI checks passed.
echo ===================================================

endlocal
