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

$pluginDir = Join-Path $templateDir "BepInEx\plugins\SongsOfConquestAccess"
$pluginDll = Join-Path $pluginDir "SongsOfConquest.Access.dll"
$loaderDll = Join-Path $pluginDir "SongsOfConquest.Access.Loader.dll"
$zipPath = Join-Path $releaseDir "SongsOfConquestAccess-v$version.zip"

Push-Location $scriptDir
try {
    New-Item -ItemType Directory -Force $releaseDir | Out-Null

    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    # A release build must not touch the game folder: the loader there is locked while the
    # game runs, and the development deployment stays a Debug build.
    dotnet build $projectPath -c Release -v:minimal /p:DeployToGame=false
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path $pluginDll)) {
        throw "Release DLL was not copied to template: $pluginDll"
    }
    if (-not (Test-Path $loaderDll)) {
        throw "Loader DLL was not copied to template: $loaderDll"
    }

    # mcs.dll backs POST /eval; the dev server is off unless the config enables it, and the
    # config is not shipped, so this only ever loads for someone who turns it on.
    $vendorMcs = Join-Path $scriptDir "vendor\mcs"
    Copy-Item -LiteralPath (Join-Path $vendorMcs "mcs.dll") -Destination $pluginDir
    Copy-Item -LiteralPath (Join-Path $vendorMcs "NOTICE") -Destination (Join-Path $pluginDir "mcs-NOTICE.txt")

    Compress-Archive -Path (Join-Path $templateDir "*") -DestinationPath $zipPath -Force

    Write-Host "Release zip: $zipPath"
}
finally {
    Pop-Location
}
