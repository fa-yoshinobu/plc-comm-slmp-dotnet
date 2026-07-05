param(
    [string]$Ref = $env:SLMP_PROFILES_REF,
    [string]$SourceRoot = $env:SLMP_PROFILES_SOURCE_ROOT,
    [switch]$FailIfChanged
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Ref)) {
    $Ref = "v1.2.1"
}

$RawBase = "https://raw.githubusercontent.com/fa-yoshinobu/plc-comm-slmp-profiles/$Ref"
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$Changed = New-Object System.Collections.Generic.List[string]

function Get-CanonicalJson {
    param([string]$Path)
    if (-not [string]::IsNullOrWhiteSpace($SourceRoot)) {
        $sourcePath = Join-Path $SourceRoot $Path
        Write-Host "[profiles] reading $sourcePath"
        $content = [System.IO.File]::ReadAllText($sourcePath)
    } else {
        $uri = "$RawBase/$Path"
        Write-Host "[profiles] downloading $uri"
        $response = Invoke-WebRequest -UseBasicParsing -Uri $uri
        $content = [string]$response.Content
    }
    $null = $content | ConvertFrom-Json
    return $content
}

function Write-IfChanged {
    param(
        [string]$Destination,
        [string]$Content
    )
    $fullPath = Join-Path (Get-Location) $Destination
    $parent = Split-Path -Parent $fullPath
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }
    $normalizedContent = $Content.Replace("`r`n", "`n")
    $current = $null
    if (Test-Path -LiteralPath $fullPath) {
        $current = [System.IO.File]::ReadAllText($fullPath).Replace("`r`n", "`n")
    }
    if ($current -ne $normalizedContent) {
        [System.IO.File]::WriteAllText($fullPath, $normalizedContent, $Utf8NoBom)
        $Changed.Add($Destination) | Out-Null
        Write-Host "[profiles] updated $Destination"
    } else {
        Write-Host "[profiles] unchanged $Destination"
    }
}

$capability = Get-CanonicalJson "capability/slmp_builtin_ethernet_profiles.json"
$deviceRanges = Get-CanonicalJson "device-ranges/slmp_device_range_rules.json"

Write-IfChanged "tests/PlcComm.Slmp.Tests/fixtures/slmp_builtin_ethernet_profiles.json" $capability
Write-IfChanged "tests/PlcComm.Slmp.Tests/fixtures/slmp_device_range_rules.json" $deviceRanges

if ($Changed.Count -gt 0) {
    Write-Host "[profiles] changed files:"
    foreach ($path in $Changed) {
        Write-Host "  $path"
    }
    if ($FailIfChanged) {
        Write-Error "Canonical SLMP profile JSON changed. Commit the updated files before release."
    }
}
