@echo off
setlocal

echo ===================================================
echo [RELEASE] SLMP .NET release check
echo ===================================================

echo [1/3] Checking registry version...
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check_registry_duplicate.ps1 -Registry nuget -Package PlcComm.Slmp -VersionSource csproj -ManifestPath src\PlcComm.Slmp\PlcComm.Slmp.csproj
if %errorlevel% neq 0 (
    echo [ERROR] Release version check failed.
    exit /b %errorlevel%
)

echo [2/3] Running CI...
call run_ci.bat
if %errorlevel% neq 0 (
    echo [ERROR] CI failed.
    exit /b %errorlevel%
)

echo [3/3] Packing NuGet package...
dotnet pack src\PlcComm.Slmp\PlcComm.Slmp.csproj -c Release
if %errorlevel% neq 0 (
    echo [ERROR] Pack failed.
    exit /b %errorlevel%
)

echo ===================================================
echo [SUCCESS] Release check passed.
echo ===================================================
endlocal
