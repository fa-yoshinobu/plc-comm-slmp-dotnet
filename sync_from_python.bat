@echo off
setlocal

set "PY_REPO=..\plc-comm-slmp-python"
set "SRC_DIR=%PY_REPO%\docs\validation\reports"
set "DST_DIR=docs\validation\reports"

if not exist "%SRC_DIR%\PLC_COMPATIBILITY.md" (
  echo [NG] source not found: %SRC_DIR%\PLC_COMPATIBILITY.md
  exit /b 1
)

if not exist "%DST_DIR%" (
  mkdir "%DST_DIR%"
)

copy /Y "%SRC_DIR%\PLC_COMPATIBILITY.md" "%DST_DIR%\PLC_COMPATIBILITY.md" >nul
if errorlevel 1 (
  echo [NG] copy failed: PLC_COMPATIBILITY.md
  exit /b 1
)

if exist "%SRC_DIR%\compatibility_policy.json" (
  copy /Y "%SRC_DIR%\compatibility_policy.json" "%DST_DIR%\compatibility_policy.json" >nul
  if errorlevel 1 (
    echo [NG] copy failed: compatibility_policy.json
    exit /b 1
  )
)

echo [DONE] synced from %SRC_DIR%
exit /b 0
