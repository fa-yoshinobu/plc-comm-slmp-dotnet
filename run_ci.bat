@echo off
setlocal

echo ===================================================
echo [CI] Build, test, and format verification
echo ===================================================

echo [1/6] Build
dotnet build PlcComm.Slmp.sln
if %errorlevel% neq 0 (
    echo [ERROR] Build failed.
    exit /b %errorlevel%
)

echo [2/6] Test release version checker
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\test_check_release_version.ps1
if %errorlevel% neq 0 (
    echo [ERROR] Release version checker tests failed.
    exit /b %errorlevel%
)

echo [3/6] Test API reference generator
python scripts\test_generate_api_reference.py
if %errorlevel% neq 0 (
    echo [ERROR] API reference generator tests failed.
    exit /b %errorlevel%
)

echo [4/6] Validate API reference
python scripts\generate_api_reference.py --assembly src\PlcComm.Slmp\bin\Debug\net8.0\PlcComm.Slmp.dll --xml src\PlcComm.Slmp\bin\Debug\net8.0\PlcComm.Slmp.xml --output docsrc\user\API_REFERENCE.md --title "SLMP .NET API Reference" --package PlcComm.Slmp --check
if %errorlevel% neq 0 (
    echo [ERROR] API reference is out of date.
    exit /b %errorlevel%
)

echo [5/6] Test
dotnet test PlcComm.Slmp.sln --no-build
if %errorlevel% neq 0 (
    echo [ERROR] Tests failed.
    exit /b %errorlevel%
)

echo [6/6] Format check
dotnet format PlcComm.Slmp.sln --verify-no-changes
if %errorlevel% neq 0 (
    echo [ERROR] Format violations found.
    exit /b %errorlevel%
)

echo ===================================================
echo [SUCCESS] CI checks passed.
echo ===================================================

endlocal
