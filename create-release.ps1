param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$VersionTag
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Fail {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    [Console]::Error.WriteLine($Message)
    exit 1
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        Fail "$Description failed with exit code $LASTEXITCODE."
    }
}

function Get-ChangelogSection {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ChangelogPath,

        [Parameter(Mandatory = $true)]
        [string]$ReleaseTitle
    )

    $lines = Get-Content -LiteralPath $ChangelogPath
    $heading = "## $ReleaseTitle"
    $startIndex = -1

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq $heading) {
            $startIndex = $i
            break
        }
    }

    if ($startIndex -lt 0) {
        Fail "Could not find changelog section '$heading' in $ChangelogPath."
    }

    $endIndex = $lines.Count
    for ($i = $startIndex + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^##\s+') {
            $endIndex = $i
            break
        }
    }

    $sectionLines = @()
    if ($endIndex -gt ($startIndex + 1)) {
        $sectionLines = $lines[($startIndex + 1)..($endIndex - 1)]
    }

    $section = ($sectionLines -join [Environment]::NewLine).Trim()
    if ([string]::IsNullOrWhiteSpace($section)) {
        Fail "Changelog section '$heading' is empty."
    }

    return $section
}

if ($VersionTag -notmatch '^v.+') {
    Fail "Version tag must start with lowercase 'v', for example v0.7.0."
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseDir = Join-Path $scriptDir "releases"
$changelogPath = Join-Path $scriptDir "docs_src\src\changelog.md"
$zipPath = Join-Path $releaseDir "SongsOfConquestAccess-$VersionTag.zip"
$installerPath = Join-Path $releaseDir "SongsOfConquestAccessInstaller.exe"
$releaseTitle = "V$($VersionTag.Substring(1))"

Push-Location $scriptDir
try {
    $null = & git rev-parse --verify --quiet "refs/tags/$VersionTag"
    if ($LASTEXITCODE -ne 0) {
        Fail "Tag '$VersionTag' does not exist in the local repository."
    }

    if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
        Fail "Release zip not found: $zipPath"
    }

    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
        Fail "Installer not found: $installerPath"
    }

    $null = & git ls-remote --exit-code --tags origin "refs/tags/$VersionTag"
    if ($LASTEXITCODE -ne 0) {
        Fail "Tag '$VersionTag' does not exist on remote 'origin'."
    }

    $releaseNotes = Get-ChangelogSection -ChangelogPath $changelogPath -ReleaseTitle $releaseTitle
    $notesFile = Join-Path ([System.IO.Path]::GetTempPath()) "SongsOfConquestAccess-$VersionTag-release-notes.md"
    Set-Content -LiteralPath $notesFile -Value $releaseNotes -Encoding UTF8

    try {
        if ($null -eq (Get-Command gh -ErrorAction SilentlyContinue)) {
            Fail "GitHub CLI executable 'gh' was not found on PATH."
        }

        Invoke-Checked "GitHub release creation" {
            & gh release create $VersionTag $zipPath $installerPath --title $releaseTitle --notes-file $notesFile
        }
    }
    finally {
        if (Test-Path -LiteralPath $notesFile) {
            Remove-Item -LiteralPath $notesFile -Force
        }
    }
}
finally {
    Pop-Location
}
