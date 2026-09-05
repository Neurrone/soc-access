Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Fail($message) {
    Write-Error $message
    exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$propsPath = Join-Path $scriptDir "soc-access\GamePaths.props"
$decompiledDir = Join-Path $scriptDir "decompiled"

if (-not (Test-Path -LiteralPath $propsPath)) {
    Fail "Game paths file not found: $propsPath"
}

try {
    [xml]$props = Get-Content -LiteralPath $propsPath
}
catch {
    Fail "Could not read game paths file: $propsPath"
}

$gameDir = $props.Project.PropertyGroup |
    ForEach-Object { $_.GameDir } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($gameDir)) {
    Fail "Could not read GameDir from $propsPath"
}

$managedDir = Join-Path $gameDir "SongsOfConquest_Data\Managed"
if (-not (Test-Path -LiteralPath $managedDir)) {
    Fail "Game managed assemblies folder not found: $managedDir"
}

if (-not (Test-Path -LiteralPath $decompiledDir)) {
    Fail "Decompiled folder not found: $decompiledDir"
}

$dlls = @(Get-ChildItem -LiteralPath $decompiledDir -Filter "*.dll" -File)
if ($dlls.Count -eq 0) {
    Fail "No DLL files found in $decompiledDir"
}

$updatedDlls = @()
foreach ($dll in $dlls) {
    $sourcePath = Join-Path $managedDir $dll.Name
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        Fail "Game assembly not found: $sourcePath"
    }

    $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    $targetHash = (Get-FileHash -LiteralPath $dll.FullName -Algorithm SHA256).Hash

    if ($sourceHash -ne $targetHash) {
        Copy-Item -LiteralPath $sourcePath -Destination $dll.FullName -Force
        $updatedDlls += Get-Item -LiteralPath $dll.FullName
        Write-Host "Copied $($dll.Name)"
    }
}

if ($updatedDlls.Count -eq 0) {
    Write-Host "No DLLs changed; nothing to decompile."
    exit 0
}

$illspy = Get-Command "ilspycmd" -ErrorAction SilentlyContinue
if ($null -eq $illspy) {
    Fail "ilspycmd was not found on PATH. Install it or run this script from a shell where ilspycmd is available."
}

$decompiledRoot = [System.IO.Path]::GetFullPath($decompiledDir)
foreach ($dll in $updatedDlls) {
    $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($dll.Name)
    $outputDir = Join-Path $decompiledDir $assemblyName
    $outputFullPath = [System.IO.Path]::GetFullPath($outputDir)

    if (-not $outputFullPath.StartsWith($decompiledRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to remove output folder outside decompiled root: $outputFullPath"
    }

    if (Test-Path -LiteralPath $outputDir) {
        Remove-Item -LiteralPath $outputDir -Recurse -Force
    }

    Write-Host "Decompiling $($dll.Name) -> $outputDir"
    & $illspy.Source -p --nested-directories --disable-updatecheck -o $outputDir $dll.FullName
    if ($LASTEXITCODE -ne 0) {
        Fail "ilspycmd failed for $($dll.Name) with exit code $LASTEXITCODE"
    }
}

Write-Host "Decompiled $($updatedDlls.Count) updated DLL(s)."
