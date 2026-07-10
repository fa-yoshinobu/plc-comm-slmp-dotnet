param(
    [Parameter(Mandatory = $true)]
    [string] $Tag,
    [string] $AssemblyPath,
    [string] $PackageDirectory
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

[xml] $props = Get-Content -LiteralPath (Join-Path $RepoRoot "Directory.Build.props") -Raw
$version = @($props.Project.PropertyGroup.Version | Where-Object { $_ })[0]
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Directory.Build.props does not define Version."
}
$expectedTag = "v$version"
if ($Tag -cne $expectedTag) {
    throw "Release version mismatch: tag '$Tag' does not match Directory.Build.props version '$version'."
}

if (-not [string]::IsNullOrWhiteSpace($AssemblyPath)) {
    $resolvedAssembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
    $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($resolvedAssembly).Version
    $expectedAssemblyVersion = ($version -split "-", 2)[0]
    $actualAssemblyVersion = "$($assemblyVersion.Major).$($assemblyVersion.Minor).$($assemblyVersion.Build)"
    if ($actualAssemblyVersion -cne $expectedAssemblyVersion) {
        throw "Runtime assembly version mismatch: expected '$expectedAssemblyVersion', got '$actualAssemblyVersion'."
    }
    $productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($resolvedAssembly).ProductVersion
    if ($productVersion -cne $version -and -not $productVersion.StartsWith("$version+", [System.StringComparison]::Ordinal)) {
        throw "Runtime product version mismatch: expected '$version' (optionally with +metadata), got '$productVersion'."
    }
}

if (-not [string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $resolvedPackages = (Resolve-Path -LiteralPath $PackageDirectory).Path
    $expectedNames = @("PlcComm.Slmp.$version.nupkg", "PlcComm.Slmp.$version.snupkg") | Sort-Object
    $actualFiles = @(Get-ChildItem -LiteralPath $resolvedPackages -File | Sort-Object Name)
    $actualNames = @($actualFiles.Name)
    if (Compare-Object -ReferenceObject $expectedNames -DifferenceObject $actualNames) {
        throw "Package artifact names mismatch: expected '$($expectedNames -join ', ')', got '$($actualNames -join ', ')'."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($package in $actualFiles) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName.EndsWith(".nuspec", [System.StringComparison]::OrdinalIgnoreCase) })
            if ($nuspecEntries.Count -ne 1) {
                throw "Package '$($package.Name)' must contain exactly one .nuspec file."
            }
            $reader = [System.IO.StreamReader]::new($nuspecEntries[0].Open())
            try {
                [xml] $nuspec = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
            $packageId = $nuspec.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='id']").InnerText
            $packageVersion = $nuspec.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='version']").InnerText
            if ($packageId -cne "PlcComm.Slmp" -or $packageVersion -cne $version) {
                throw "Package '$($package.Name)' metadata mismatch: expected PlcComm.Slmp $version, got $packageId $packageVersion."
            }
        }
        finally {
            $archive.Dispose()
        }
    }
}

Write-Host "release-version-check-ok version=$version"
