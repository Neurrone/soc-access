Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$installerDir = Join-Path $scriptDir "installer"

if (-not (Test-Path (Join-Path $installerDir "Cargo.toml"))) {
    throw "Installer project not found: $installerDir"
}

if ([string]::IsNullOrWhiteSpace($env:LIBCLANG_PATH)) {
    $libclangCandidates = @(
        "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\LLVM\bin"
    )
    foreach ($candidate in $libclangCandidates) {
        if (Test-Path (Join-Path $candidate "libclang.dll")) {
            $env:LIBCLANG_PATH = $candidate
            break
        }
    }
}

Push-Location $installerDir
try {
    cargo test
    if ($LASTEXITCODE -ne 0) {
        throw "Installer tests failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
