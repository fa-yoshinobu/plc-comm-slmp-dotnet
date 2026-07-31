[CmdletBinding()]
param(
    [string]$Treeish = "HEAD",
    [switch]$UseWorktreeAttributes,
    [switch]$SkipValidation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$workRoot = Join-Path $repositoryRoot ("build/source-archive-check-" + [guid]::NewGuid().ToString("N"))
$archivePath = Join-Path $workRoot "source.zip"
$extractRoot = Join-Path $workRoot "extracted"
$stageRoot = Join-Path $workRoot "staged"

try {
    [void](New-Item -ItemType Directory -Path $workRoot -Force)
    & git -C $repositoryRoot rev-parse --verify "$Treeish`^{tree}" *> $null
    if ($LASTEXITCODE -ne 0) { throw "Cannot resolve treeish '$Treeish'." }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $worktreeFiles = @()
    if ($UseWorktreeAttributes) {
        $worktreeFiles = @(& git -C $repositoryRoot ls-files --cached --others --exclude-standard |
            ForEach-Object { $_.Replace("\", "/") } |
            Where-Object {
                (Test-Path -LiteralPath (Join-Path $repositoryRoot $_) -PathType Leaf) -and
                $_ -notin @(".gitattributes", ".gitignore") -and
                $_ -notmatch '^(build|build_win|release-artifacts)/'
            } |
            Sort-Object -Unique)
        if ($LASTEXITCODE -ne 0) { throw "Cannot enumerate current worktree files." }
        [void](New-Item -ItemType Directory -Path $stageRoot)
        foreach ($path in $worktreeFiles) {
            $destination = Join-Path $stageRoot $path
            [void](New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force)
            Copy-Item -LiteralPath (Join-Path $repositoryRoot $path) -Destination $destination -Force
        }
        [System.IO.Compression.ZipFile]::CreateFromDirectory($stageRoot, $archivePath)
    }
    else {
        & git -C $repositoryRoot archive --format=zip --output=$archivePath $Treeish
        if ($LASTEXITCODE -ne 0) { throw "git archive failed for '$Treeish'." }
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $archiveFiles = @($archive.Entries |
            Where-Object { -not $_.FullName.EndsWith("/") } |
            ForEach-Object { $_.FullName.Replace("\", "/") } |
            Sort-Object -Unique)
    }
    finally {
        $archive.Dispose()
    }

    $trackedFiles = if ($UseWorktreeAttributes) { $worktreeFiles } else {
        @(& git -C $repositoryRoot ls-tree -r --name-only $Treeish |
            ForEach-Object { $_.Replace("\", "/") } |
            Sort-Object -Unique)
    }
    if ($LASTEXITCODE -ne 0) { throw "Cannot enumerate source files for '$Treeish'." }

    $requiredRootFiles = @("CHANGELOG.md", "LICENSE", "README.md", "run_ci.bat")
    $missingRootFiles = @($requiredRootFiles | Where-Object { $_ -notin $archiveFiles })
    if ($missingRootFiles.Count -ne 0) {
        throw "Source archive is missing required root files: $($missingRootFiles -join ', ')"
    }

    $manifestCandidates = @("package.json", "pyproject.toml", "Cargo.toml", "library.json", "library.properties", "CMakeLists.txt") +
        @($trackedFiles | Where-Object { $_ -match '\.(sln|csproj)$' })
    if (@($manifestCandidates | Where-Object { $_ -in $archiveFiles }).Count -eq 0) {
        throw "Source archive contains no recognized build manifest."
    }

    $guideRoots = @("docsrc/user", "docs")
    foreach ($guide in @("GETTING_STARTED.md", "USAGE_GUIDE.md", "PROFILES.md", "GOTCHAS.md", "API_REFERENCE.md")) {
        if (@($guideRoots | ForEach-Object { "$_/$guide" } | Where-Object { $_ -in $archiveFiles }).Count -eq 0) {
            throw "Source archive is missing standard user guide '$guide'."
        }
    }

    $requiredTracked = @($trackedFiles | Where-Object {
        $_ -match '^(test|tests|\.github|docsrc/maintainer|internal_docs|scripts|tools)/' -or
        $_ -in @("AGENTS.md", "TODO.md", "release_check.bat", "run_ci.bat")
    })
    $missingTracked = @($requiredTracked | Where-Object { $_ -notin $archiveFiles })
    if ($missingTracked.Count -ne 0) {
        throw "Source archive omits tracked validation or maintainer material: $($missingTracked -join ', ')"
    }
    if (@($archiveFiles | Where-Object { $_ -match '^(test|tests)/' }).Count -eq 0) {
        throw "Source archive contains no repository tests."
    }

    $forbidden = @($archiveFiles | Where-Object {
        $_ -match '^(build|build_win|release-artifacts)/'
    })
    if ($forbidden.Count -ne 0) {
        throw "Source archive contains generated or release-output files: $($forbidden -join ', ')"
    }

    if (-not $SkipValidation) {
        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot
        Push-Location $extractRoot
        try {
            & cmd.exe /d /c run_ci.bat
            if ($LASTEXITCODE -ne 0) {
                throw "run_ci.bat failed from the extracted source archive."
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host "[OK] Source archive contract passed: treeish=$Treeish files=$($archiveFiles.Count) validation=$(-not $SkipValidation)"
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force
    }
}
