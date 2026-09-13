<#
.SYNOPSIS
    Dump every battlefield layout the game ships: its JSON and two pictures.

.DESCRIPTION
    A battlefield is a MapFormat the game keeps as a resource; there are about 82 of them and a
    battle picks one by terrain type and seed. The descriptions the mod speaks are authored from
    these dumps (battlefields\PROMPT.md), so this is how the dumps are made again after a game
    update or a change to the dump code. Two modes, because each needs the game in a different
    place:

    -Mode Deployment   From a loaded adventure map. One REPL call writes, per layout,
                       battlefields\<LevelType>\<PathName>.json (terrain, regions, spawn points,
                       the ascii board) and .jpg (the game's own deployment preview: a schematic
                       with the spawn markers and no troops).

    -Mode Combat       From the main menu. Opens the map editor in its Battle context, which draws
                       a layout with the real props and no troops, loads each layout in turn and
                       saves the frame as battlefields\<LevelType>\<PathName>.combat.jpg. The
                       editor's own panels are hidden for the frames and shown again after, and
                       the game is returned to the main menu.

    Both drive the dev server (docs\dev-loop.md), so the game must be running with it on
    (run-game.ps1). The dump code is soc-access\dev\BattlefieldDump.cs and BattlefieldEditor.cs.

.PARAMETER Mode
    Deployment or Combat; see above.

.PARAMETER Out
    The folder to write into. Defaults to battlefields\ beside this script.

.PARAMETER Zoom
    Combat only: the editor camera's zoom, 0 closest to 1 furthest. 0.08 fills a 1920x1080 frame
    with the 13 by 9 board.

.PARAMETER SettleMs
    Combat only: how long to give the editor to rebuild the terrain after each load before the
    frame is taken.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Deployment', 'Combat')]
    [string]$Mode,
    [string]$Out,
    [double]$Zoom = 0.08,
    [int]$SettleMs = 3000,
    [string]$DevUrl = 'http://127.0.0.1:8772'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
if (-not $Out) { $Out = Join-Path $root 'battlefields' }
$Out = [System.IO.Path]::GetFullPath($Out)

$evalBodyPath = Join-Path $env:TEMP 'socaccess-battlefield-eval.txt'

function Invoke-Eval([string]$body, [int]$timeoutSec = 30) {
    # Through a file: PowerShell strips the double quotes a C# string literal needs when it
    # hands an argument to curl.exe.
    [System.IO.File]::WriteAllText($evalBodyPath, $body)
    $answer = curl.exe -s --max-time $timeoutSec -X POST "$DevUrl/eval?settle=0&speech=0" --data-binary "@$evalBodyPath"
    if (-not $answer) { throw "the dev server did not answer: $body" }
    $envelope = $answer | ConvertFrom-Json
    if ($envelope.error) { throw "eval failed: $($envelope.error)`n$body" }
    if (-not $envelope.result) { return $null }
    $result = $envelope.result | ConvertFrom-Json
    if ($result.PSObject.Properties['error']) { throw "the call answered an error: $($result.error)`n$body" }
    return $result
}

function Get-DevStatus {
    $answer = curl.exe -s --max-time 5 "$DevUrl/status"
    if (-not $answer) { throw "no dev server at $DevUrl; start the game with run-game.ps1" }
    return $answer | ConvertFrom-Json
}

function Save-Jpeg([string]$pngPath, [string]$jpgPath, [int]$width, [int]$quality) {
    Add-Type -AssemblyName System.Drawing
    $src = [System.Drawing.Bitmap]::FromFile($pngPath)
    try {
        $height = [int]($src.Height * $width / $src.Width)
        $dst = New-Object System.Drawing.Bitmap $width, $height
        try {
            $g = [System.Drawing.Graphics]::FromImage($dst)
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.DrawImage($src, 0, 0, $width, $height)
            $g.Dispose()
            $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
            $parameters = New-Object System.Drawing.Imaging.EncoderParameters 1
            $parameters.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality, [long]$quality)
            $dst.Save($jpgPath, $codec, $parameters)
        } finally { $dst.Dispose() }
    } finally { $src.Dispose() }
}

$null = Get-DevStatus

if ($Mode -eq 'Deployment') {
    $state = Invoke-Eval 'SongsOfConquestAccess.Dev.DevProbe.State()'
    if ($state.state -ne 'ingame') {
        throw "Deployment needs a loaded adventure map (wait-game.ps1 ingame); the game is at '$($state.state)'"
    }

    New-Item -ItemType Directory -Force $Out | Out-Null
    $before = @(Get-ChildItem -Path $Out -Recurse -Filter '*.json' -File | Where-Object { $_.Name -ne 'current.json' }).Count
    # The call takes about 15 s on the main thread and the eval route gives up waiting at 5 s;
    # the answer is then a timeout while the files keep arriving, so the files are what is watched.
    $body = 'SongsOfConquestAccess.Dev.BattlefieldDump.All(@"' + $Out + '")'
    [System.IO.File]::WriteAllText($evalBodyPath, $body)
    $null = curl.exe -s --max-time 30 -X POST "$DevUrl/eval?settle=0&speech=0" --data-binary "@$evalBodyPath"
    $last = -1
    $stable = 0
    while ($stable -lt 4) {
        Start-Sleep -Seconds 2
        $count = @(Get-ChildItem -Path $Out -Recurse -Filter '*.json' -File | Where-Object { $_.Name -ne 'current.json' }).Count
        if ($count -eq $last) { $stable++ } else { $stable = 0; $last = $count }
    }
    Write-Host "Deployment dump: $last layouts in $Out (was $before)"
    exit 0
}

# ---- Combat: the editor as a renderer ------------------------------------------------------

$keys = @(Get-ChildItem -Path $Out -Recurse -Filter '*.json' -File |
    Where-Object { $_.Directory.Parent.FullName -eq $Out } |  # <Out>\<LevelType>\*.json, not descriptions\
    ForEach-Object { (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json).key })
if ($keys.Count -eq 0) { throw "no layout JSON under $Out; run -Mode Deployment first" }

$editor = Invoke-Eval 'SongsOfConquestAccess.Dev.BattlefieldEditor.State()'
if (-not $editor.ready) {
    if ($editor.scene -notlike 'MainMenu*') {
        throw "Combat needs the main menu (or the editor already open); the game is in scene '$($editor.scene)'"
    }
    $null = Invoke-Eval 'SongsOfConquestAccess.Dev.BattlefieldEditor.Open()'
    $deadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Seconds 2
        $editor = Invoke-Eval 'SongsOfConquestAccess.Dev.BattlefieldEditor.State()'
    } while (-not $editor.ready -and (Get-Date) -lt $deadline)
    if (-not $editor.ready) { throw "the editor did not open in time (scene '$($editor.scene)', state '$($editor.state)')" }
    Start-Sleep -Seconds 3
}
if ($editor.context -ne 'Battle') { throw "the editor is open in its $($editor.context) context; close it and rerun from the main menu" }

$null = Invoke-Eval 'SongsOfConquestAccess.Dev.BattlefieldEditor.Ui(false)'
$shot = Join-Path $env:TEMP 'socaccess-battlefield-shot.png'
$zoomText = $Zoom.ToString([System.Globalization.CultureInfo]::InvariantCulture)
$written = 0
try {
    foreach ($key in $keys) {
        $null = Invoke-Eval ('SongsOfConquestAccess.Dev.BattlefieldEditor.Load("' + $key + '")')
        $null = Invoke-Eval ('SongsOfConquestAccess.Dev.BattlefieldEditor.Zoom(' + $zoomText + 'f)')
        Start-Sleep -Milliseconds $SettleMs
        curl.exe -s --max-time 20 "$DevUrl/screenshot" -o $shot
        if (-not (Test-Path $shot) -or (Get-Item $shot).Length -lt 1024) { throw "no frame for $key" }
        $parts = $key.Split('/')
        $target = Join-Path (Join-Path $Out $parts[0]) ($parts[1] + '.combat.jpg')
        Save-Jpeg $shot $target 1280 90
        $written++
        Write-Host "$key -> $target"
    }
} finally {
    $null = Invoke-Eval 'SongsOfConquestAccess.Dev.BattlefieldEditor.Ui(true)'
    $null = Invoke-Eval 'SongsOfConquestAccess.Dev.BattlefieldEditor.Close()'
    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Seconds 2
        $editor = Invoke-Eval 'SongsOfConquestAccess.Dev.BattlefieldEditor.State()'
    } while ($editor.scene -notlike 'MainMenu*' -and (Get-Date) -lt $deadline)
}
Write-Host "Combat renders: $written of $($keys.Count) layouts in $Out; the game is back at the main menu"
