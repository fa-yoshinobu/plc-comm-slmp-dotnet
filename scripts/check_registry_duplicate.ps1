param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("pypi", "crates", "nuget", "npm", "platformio")]
    [string] $Registry,

    [Parameter(Mandatory = $true)]
    [string] $Package,

    [Parameter(Mandatory = $true)]
    [ValidateSet("pyproject", "cargo", "package-json", "csproj", "library-properties", "library-json")]
    [string] $VersionSource,

    [string] $ManifestPath = "",
    [string] $CompareSource = "",
    [string] $CompareManifestPath = ""
)

$ErrorActionPreference = "Stop"

function Resolve-LocalPath([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "Manifest path is required."
    }
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }
    return (Join-Path (Get-Location) $Path)
}

function Read-Version([string] $Source, [string] $Path) {
    $fullPath = Resolve-LocalPath $Path
    if (-not (Test-Path $fullPath)) {
        throw "Manifest '$Path' was not found."
    }

    switch ($Source) {
        "pyproject" {
            $text = Get-Content $fullPath -Raw
            $match = [regex]::Match($text, '(?m)^\s*version\s*=\s*"([^"]+)"')
            if (-not $match.Success) {
                throw "Could not find version in '$Path'."
            }
            return $match.Groups[1].Value
        }
        "cargo" {
            $text = Get-Content $fullPath -Raw
            $match = [regex]::Match($text, '(?m)^\s*version\s*=\s*"([^"]+)"')
            if (-not $match.Success) {
                throw "Could not find version in '$Path'."
            }
            return $match.Groups[1].Value
        }
        "package-json" {
            $json = Get-Content $fullPath -Raw | ConvertFrom-Json
            if (-not $json.version) {
                throw "Could not find version in '$Path'."
            }
            return [string] $json.version
        }
        "library-json" {
            $json = Get-Content $fullPath -Raw | ConvertFrom-Json
            if (-not $json.version) {
                throw "Could not find version in '$Path'."
            }
            return [string] $json.version
        }
        "csproj" {
            [xml] $xml = Get-Content $fullPath -Raw
            $version = $xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1
            if (-not $version) {
                throw "Could not find Version in '$Path'."
            }
            return [string] $version
        }
        "library-properties" {
            $line = Get-Content $fullPath | Where-Object { $_ -match '^version=(.+)$' } | Select-Object -First 1
            if (-not $line) {
                throw "Could not find version in '$Path'."
            }
            return ($line -replace '^version=', '').Trim()
        }
    }
}

function Get-HttpResult([string] $Uri, [hashtable] $Headers = @{}) {
    try {
        $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -Headers $Headers -ErrorAction Stop
        return @{
            StatusCode = [int] $response.StatusCode
            Content = [string] $response.Content
        }
    }
    catch {
        if ($_.Exception.Response) {
            return @{
                StatusCode = [int] $_.Exception.Response.StatusCode
                Content = ""
            }
        }
        throw
    }
}

$version = Read-Version $VersionSource $ManifestPath

if (-not [string]::IsNullOrWhiteSpace($CompareSource)) {
    $compareVersion = Read-Version $CompareSource $CompareManifestPath
    if ($compareVersion -ne $version) {
        throw "Version mismatch: '$ManifestPath' has '$version', but '$CompareManifestPath' has '$compareVersion'."
    }
}

switch ($Registry) {
    "pypi" {
        $packagePart = [System.Uri]::EscapeDataString($Package)
        $versionPart = [System.Uri]::EscapeDataString($version)
        $result = Get-HttpResult "https://pypi.org/pypi/$packagePart/$versionPart/json"
        if ($result.StatusCode -eq 200) {
            throw "PyPI package '$Package==$version' is already published. Bump the version before release."
        }
        if ($result.StatusCode -ne 404) {
            throw "Unexpected PyPI response $($result.StatusCode) while checking '$Package==$version'."
        }
    }
    "crates" {
        $headers = @{ "User-Agent" = "plc-comm-release-check" }
        $packagePart = [System.Uri]::EscapeDataString($Package)
        $versionPart = [System.Uri]::EscapeDataString($version)
        $result = Get-HttpResult "https://crates.io/api/v1/crates/$packagePart/$versionPart" $headers
        if ($result.StatusCode -eq 200) {
            throw "crates.io package '$Package@$version' is already published. Bump the version before release."
        }
        if ($result.StatusCode -ne 404) {
            throw "Unexpected crates.io response $($result.StatusCode) while checking '$Package@$version'."
        }
    }
    "nuget" {
        $id = $Package.ToLowerInvariant()
        $result = Get-HttpResult "https://api.nuget.org/v3-flatcontainer/$id/index.json"
        if ($result.StatusCode -eq 404) {
            break
        }
        if ($result.StatusCode -ne 200) {
            throw "Unexpected NuGet response $($result.StatusCode) while checking '$Package@$version'."
        }
        $index = $result.Content | ConvertFrom-Json
        $versions = @($index.versions | ForEach-Object { ([string] $_).ToLowerInvariant() })
        if ($versions -contains $version.ToLowerInvariant()) {
            throw "NuGet package '$Package@$version' is already published. Bump the version before release."
        }
    }
    "npm" {
        $npm = (Get-Command npm -ErrorAction SilentlyContinue).Source
        if (-not $npm) {
            throw "npm was not found; cannot verify registry duplicate version."
        }
        $output = & $npm view "$Package@$version" version --json 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($output)) {
            throw "npm package '$Package@$version' is already published. Bump the version before release."
        }
        if ($LASTEXITCODE -ne 0 -and $output -notmatch 'E404|404|not found') {
            throw "Failed to query npm package '$Package@$version'. $output"
        }
    }
    "platformio" {
        $pio = (Get-Command pio -ErrorAction SilentlyContinue).Source
        if (-not $pio) {
            $candidate = Join-Path $env:USERPROFILE ".platformio\penv\Scripts\pio.exe"
            if (Test-Path $candidate) {
                $pio = $candidate
            }
        }
        if (-not $pio) {
            throw "PlatformIO CLI was not found; cannot verify registry duplicate version."
        }
        $env:PYTHONIOENCODING = "utf-8"
        $env:PYTHONUTF8 = "1"
        $output = & $pio pkg show $Package 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to query PlatformIO package '$Package'. $output"
        }
        $plainOutput = $output -replace "`e\[[0-?]*[ -/]*[@-~]", ""
        $escaped = [regex]::Escape($version)
        if ($plainOutput -match "(?m)^\s*$escaped\s+") {
            throw "PlatformIO package '$Package@$version' is already published. Bump the version before release."
        }
    }
}

Write-Host "[OK] $Package version $version is not already published on $Registry."
