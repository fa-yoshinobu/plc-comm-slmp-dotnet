$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Checker = Join-Path $PSScriptRoot "check_release_version.ps1"

[xml] $props = Get-Content -LiteralPath (Join-Path $RepoRoot "Directory.Build.props") -Raw
$version = @($props.Project.PropertyGroup.Version | Where-Object { $_ })[0]

& $Checker -Tag "v$version"

$mismatchRejected = $false
try {
    & $Checker -Tag "v$version.mismatch"
}
catch {
    $mismatchRejected = $true
}
if (-not $mismatchRejected) {
    throw "Release checker accepted a tag that does not match Directory.Build.props."
}

$workflow = Get-Content -LiteralPath (Join-Path $RepoRoot ".github/workflows/release.yml") -Raw
foreach ($requiredText in @(
    'ref: refs/tags/${{ steps.release.outputs.tag }}',
    '--verify-tag',
    'if ($LASTEXITCODE -ne 0)'
)) {
    if (-not $workflow.Contains($requiredText, [System.StringComparison]::Ordinal)) {
        throw "Release workflow is missing required text: $requiredText"
    }
}
if ($workflow.Contains("--target ", [System.StringComparison]::Ordinal)) {
    throw "Release workflow must use the verified remote tag instead of --target."
}

Write-Host "release-version-regression-tests-ok"
