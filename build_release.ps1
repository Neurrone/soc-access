Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptDir "soc-access\soc-access.csproj"
$templateDir = Join-Path $scriptDir "release-template"
$releaseDir = Join-Path $scriptDir "releases"

[xml]$project = Get-Content $projectPath
$version = $project.Project.PropertyGroup |
    ForEach-Object { $_.Version } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read Version from $projectPath"
}

if (-not (Test-Path $templateDir)) {
    throw "Release template not found: $templateDir"
}

$pluginDll = Join-Path $templateDir "BepInEx\plugins\SongsOfConquest.Access.dll"
$zipPath = Join-Path $releaseDir "SongsOfConquestAccess-v$version.zip"

Push-Location $scriptDir
try {
    New-Item -ItemType Directory -Force $releaseDir | Out-Null

    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    dotnet build $projectPath -c Release -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path $pluginDll)) {
        throw "Release DLL was not copied to template: $pluginDll"
    }

    Compress-Archive -Path (Join-Path $templateDir "*") -DestinationPath $zipPath -Force

    Write-Host "Release zip: $zipPath"
}
finally {
    Pop-Location
}
