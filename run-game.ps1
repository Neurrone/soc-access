param(
    [switch]$NoBuild,
    [switch]$NoSpeech,
    [switch]$NoDev,
    [switch]$NoWait,
    # Boot straight into a saved game, skipping the main menu. The value is the save's name
    # (not a file path), matched case-insensitively; empty is not accepted here, but POST
    # /loadsave with an empty body takes the most recent save.
    [string]$LoadSave
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$devUrl = 'http://127.0.0.1:8772'
$devPort = 8772
$lockPath = Join-Path $env:TEMP 'socaccess-run-game.lock'

if ($LoadSave -and $NoDev) {
    Write-Error "-LoadSave drives the dev server's POST /loadsave, so it cannot be used with -NoDev."
    exit 1
}

# --- one game at a time -------------------------------------------------------------------
# Two copies of the game fight over one dev port, and the loser wins silently: the second
# game's loader finds 8772 taken, logs it, and carries on without a dev server - so every
# request an agent then makes is answered by the FIRST game, whose state has nothing to do with
# what the test just did. That failure looks like a mod bug and takes an hour to disbelieve. So
# a launch refuses rather than allows it, and never kills anything: a running game may be
# something the developer is in the middle of.
$gameProcessNames = '^SongsOfConquest$'

function Get-LiveGameProcess {
    # Same-session only: a process orphaned into the Services session (session 0) is not a game
    # this script could have started, cannot be killed from here, and would otherwise block every
    # future launch forever.
    $session = (Get-Process -Id $PID).SessionId
    Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -match $gameProcessNames -and $_.SessionId -eq $session }
}

function Test-DevPortBound {
    try {
        $null -ne (Get-NetTCPConnection -LocalPort $devPort -State Listen -ErrorAction Stop)
    } catch {
        $false
    }
}

if (Test-Path $lockPath) {
    $lockedPid = 0
    if ([int]::TryParse((Get-Content $lockPath -Raw -ErrorAction SilentlyContinue).Trim(), [ref]$lockedPid)) {
        $holder = Get-Process -Id $lockedPid -ErrorAction SilentlyContinue
        # A dead pid, or one Windows has since handed to something else, is a lock left behind by
        # a run that crashed. Clearing it is safe precisely because the identity is checked.
        if ($holder -and $holder.ProcessName -match $gameProcessNames) {
            Write-Error "Songs of Conquest is already running as pid $lockedPid (lock: $lockPath). Quit it first - POST $devUrl/quit - or delete the lock if that pid is not the game."
            exit 1
        }
    }
    Remove-Item $lockPath -Force -ErrorAction SilentlyContinue
}

# A game that is on its way out holds the port for a few seconds after POST /quit answers, which
# is the normal case for a test loop relaunching immediately. Waiting is right; killing is not.
$deadline = (Get-Date).AddSeconds(15)
while ((Get-LiveGameProcess) -or (Test-DevPortBound)) {
    if ((Get-Date) -gt $deadline) {
        $running = (Get-LiveGameProcess | ForEach-Object { "$($_.ProcessName) (pid $($_.Id))" }) -join ', '
        $portMsg = if (Test-DevPortBound) { "port $devPort is still listening" } else { 'the port is free' }
        Write-Error "Songs of Conquest has not finished shutting down after 15s: $portMsg; processes: $(if ($running) { $running } else { 'none' }). Nothing was launched and nothing was killed - quit the running game yourself, then retry."
        exit 1
    }
    Write-Host "Waiting for the previous game to exit..."
    Start-Sleep -Seconds 1
}

# Built after the old process is gone, so the loader DLL is unlocked and a changed loader can
# deploy; a build failure aborts the launch rather than running a stale mod.
if (-not $NoBuild) {
    dotnet build "$root\soc-access\soc-access.csproj"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$props = [xml](Get-Content "$root\soc-access\GamePaths.props")
$gameDir = ($props.Project.PropertyGroup | Where-Object { $_.GameDir } | Select-Object -First 1).GameDir

# Both switches go through the BepInEx config file, not the environment: the executable hands
# itself over to Steam, which relaunches it, and the relaunched process does not inherit what
# this script exported. The dev server is opt-in (off for players); dev runs opt in here.
$cfgPath = Join-Path $gameDir 'BepInEx\config\songs.of.conquest.access.cfg'
function Set-DevSetting([string]$cfgText, [string]$name, [string]$value) {
    if ($cfgText -match "(?m)^\s*$name\s*=") {
        return $cfgText -replace "(?m)^\s*$name\s*=.*$", "$name = $value"
    }
    if ($cfgText -match '(?m)^\[Dev\]') {
        return $cfgText -replace '(?m)^\[Dev\]\s*$', "[Dev]`r`n$name = $value"
    }
    return $cfgText + "`r`n[Dev]`r`n$name = $value`r`n"
}
$cfgText = if (Test-Path $cfgPath) { Get-Content $cfgPath -Raw } else { '' }
$cfgText = Set-DevSetting $cfgText 'devServer' $(if ($NoDev) { 'false' } else { 'true' })
$cfgText = Set-DevSetting $cfgText 'muteSpeech' $(if ($NoSpeech) { 'true' } else { 'false' })
New-Item -ItemType Directory -Force (Split-Path $cfgPath) | Out-Null
Set-Content $cfgPath $cfgText -Encoding utf8

# The process this starts is not the game that runs. It boots Unity, BepInEx, the loader and
# the mod - far enough that its dev server answers - and then asks Steam to relaunch it and
# exits. So the pid worth tracking is the second process, found by name once it exists; the
# first one is excluded by pid so its short life is never mistaken for the game.
$launcher = Start-Process "$gameDir\SongsOfConquest.exe" -WorkingDirectory $gameDir -PassThru
$proc = $null
$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline) {
    $proc = Get-LiveGameProcess |
        Where-Object { -not $_.HasExited -and $_.Id -ne $launcher.Id } |
        Select-Object -First 1
    if ($proc) { break }
    Start-Sleep -Milliseconds 500
}
if (-not $proc) {
    if (-not $launcher.HasExited) {
        # No relaunch happened (Steam not involved); the process started here is the game.
        $proc = $launcher
    } else {
        Write-Error "Songs of Conquest did not appear within 60s of launching (launcher pid $($launcher.Id) exited). Is Steam running?"
        exit 1
    }
}

function Test-DevPortOwnedBy([int]$processId) {
    try {
        $listener = Get-NetTCPConnection -LocalPort $devPort -State Listen -ErrorAction Stop | Select-Object -First 1
        $null -ne $listener -and $listener.OwningProcess -eq $processId
    } catch {
        $false
    }
}
# Written after the launch so the lock always names a real process. With -NoWait the script
# returns while the game runs on, and the lock is what stops the next launch; it goes stale on
# its own the moment that pid dies.
Set-Content $lockPath $proc.Id -Encoding utf8
if ($NoDev) {
    Write-Host "Songs of Conquest started (pid $($proc.Id)). Dev server disabled."
} else {
    Write-Host "Songs of Conquest started (pid $($proc.Id)). Dev server: $devUrl/"
}

# Drive the load through the dev server once the game answers, so one command goes from a cold
# launch to in-game. Done here, before the optional WaitForExit, because that call blocks until
# the game quits. Two waits, both slow on purpose: booting to the main menu takes a while (curl
# retries the connection refusals and the 503s a busy frame answers with), and the route itself
# reports "[not ready]" until the menu can actually start a load.
if ($LoadSave) {
    Write-Host "Waiting for the dev server, then loading '$LoadSave'..."
    # Polled by hand rather than with curl's --retry: on this machine curl.exe gives up on a
    # refused loopback connection at once, and /status is a 404 (not a transient error) in the
    # moment between the loader's server coming up and the mod finishing its start. The port
    # must belong to the tracked process: the first, short-lived process answers /status too,
    # and a load sent to it is lost with it.
    $status = ''
    $bootDeadline = (Get-Date).AddSeconds(180)
    while ((Get-Date) -lt $bootDeadline) {
        if ($proc.HasExited) { break }
        if (Test-DevPortOwnedBy $proc.Id) {
            $status = curl.exe -s --connect-timeout 2 "$devUrl/status" 2>$null
            if ($status -match '"version"') { break }
        }
        Start-Sleep -Seconds 1
    }
    if ($status -match '"version"') {
        $loaded = $false
        $answer = ''
        for ($i = 0; $i -lt 60; $i++) {
            # --data-raw, not --data-binary: a name beginning with @ would otherwise be read as
            # the name of a file to send.
            $answer = curl.exe -s -X POST --data-raw "$LoadSave" "$devUrl/loadsave"
            if ($answer -match '"result"\s*:\s*"loading') { $loaded = $true; break }
            if ($answer -notmatch '\[not ready\]') { break }
            Start-Sleep -Seconds 1
        }
        if ($loaded) {
            Write-Host $answer -ForegroundColor Green
        } else {
            Write-Warning "loading '$LoadSave' did not happen; last answer: $answer"
        }
    } else {
        Write-Warning "the dev server never answered; skipping the load of '$LoadSave'."
    }
}

if (-not $NoWait) {
    $proc.WaitForExit()
    Remove-Item $lockPath -Force -ErrorAction SilentlyContinue
    Write-Host "Game exited with code $($proc.ExitCode)."
}
