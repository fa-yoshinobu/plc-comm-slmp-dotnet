@echo off
echo [DOCS] Building SLMP .NET Docs with DocFX...
docfx metadata docfx.json
docfx build docfx.json
if %errorlevel% neq 0 (
    echo [ERROR] DocFX build failed.
)
echo [SUCCESS] Documentation built to docs/

