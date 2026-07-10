param(
    [string]$WorkflowRoot = ".github/workflows"
)

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$RepoPrefix = $RepoRoot.TrimEnd(([char[]]@('\', '/'))) + [IO.Path]::DirectorySeparatorChar
$WorkflowPath = Join-Path $RepoRoot $WorkflowRoot

if (-not (Test-Path $WorkflowPath)) {
    throw "Workflow directory not found: $WorkflowPath"
}

$workflowFiles = Get-ChildItem -Path $WorkflowPath -Recurse -File -Include *.yml, *.yaml
$violations = [System.Collections.Generic.List[string]]::new()
$forbidden = @(
    @{ Name = "twine upload"; Pattern = "(^|[`\s;|&])(?:python\s+-m\s+)?twine\s+upload([`\s;|&]|$)" },
    @{ Name = "cargo publish"; Pattern = "(^|[`\s;|&])cargo\s+publish([`\s;|&]|$)" },
    @{ Name = "dotnet nuget push"; Pattern = "(^|[`\s;|&])dotnet\s+nuget\s+push([`\s;|&]|$)" },
    @{ Name = "npm publish"; Pattern = "(^|[`\s;|&])npm\s+publish([`\s;|&]|$)" }
)

foreach ($file in $workflowFiles) {
    if ($file.FullName.StartsWith($RepoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        $relative = $file.FullName.Substring($RepoPrefix.Length)
    } else {
        $relative = $file.FullName
    }
    $lines = Get-Content -LiteralPath $file.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        foreach ($rule in $forbidden) {
            if ($lines[$i] -match $rule.Pattern) {
                $violations.Add("${relative}:$($i + 1): forbidden registry publish command '$($rule.Name)'")
            }
        }
        if ($lines[$i] -match '(^|[\s;|&])gh\s+release\s+create([\s;|&]|$)') {
            if ($lines[$i] -notmatch '--draft') {
                $violations.Add("${relative}:$($i + 1): GitHub release creation must use --draft")
            }
            if ($lines[$i] -notmatch '--verify-tag') {
                $violations.Add("${relative}:$($i + 1): GitHub release creation must use --verify-tag")
            }
            if ($lines[$i] -match '--target(\s|=|$)') {
                $violations.Add("${relative}:$($i + 1): GitHub release creation must not use --target")
            }
        }
    }

    $content = Get-Content -Raw -LiteralPath $file.FullName
    if ($content -match "platformio\s+pkg\s+publish") {
        if ($content -notmatch "environment:\s*platformio-publish") {
            $violations.Add("${relative}: PlatformIO publish must run in the platformio-publish environment")
        }
        if ($content -notmatch "inputs\.confirm_publish\s*==\s*inputs\.version") {
            $violations.Add("${relative}: PlatformIO publish must require inputs.confirm_publish == inputs.version")
        }
        if ($content -notmatch "inputs\.publish_platformio") {
            $violations.Add("${relative}: PlatformIO publish must require inputs.publish_platformio")
        }
    }
}

if ($violations.Count -gt 0) {
    foreach ($violation in $violations) {
        Write-Error $violation
    }
    exit 1
}

Write-Host "[OK] Workflow files do not contain unguarded registry publish commands."
