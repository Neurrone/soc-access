param([int]$Port = 8772)

# Run against the loader before deploying the mod. Fails on the first broken contract.
$ErrorActionPreference = 'Stop'
$baseUrl = "http://127.0.0.1:$Port"
function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
    Write-Output "PASS $message"
}
function Get-Json([string]$path) {
    Invoke-RestMethod "$baseUrl$path" -TimeoutSec 20
}
function Post-Json([string]$path, [string]$body) {
    Invoke-RestMethod "$baseUrl$path" -Method Post -Body $body -ContentType 'text/plain' -TimeoutSec 20
}
function Expect-Status([string]$path, [int]$expected, [string]$method = 'GET') {
    try {
        Invoke-WebRequest "$baseUrl$path" -Method $method -UseBasicParsing -TimeoutSec 20 | Out-Null
        throw "Expected HTTP $expected on $path"
    } catch [System.Net.WebException] {
        Assert ([int]$_.Exception.Response.StatusCode -eq $expected) "$path answers HTTP $expected"
        $reader = New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
}

$status = Get-Json '/loader/status'
Assert (-not $status.modLoaded -and $status.lastReloadError -match 'SongsOfConquest.Access.dll') 'loader survives a missing mod'
$sum = Post-Json '/eval?speech=0' '1+1'
Assert ($sum.ok -and $sum.result -eq '2') 'vendored mcs evaluates 1+1'
$unity = Post-Json '/eval?speech=0' 'UnityEngine.Application.unityVersion'
Assert ($unity.ok -and $unity.result -eq '2022.3.67f2') 'REPL reaches Unity 2022.3.67f2'
$gui = Get-Json '/gui/game?depth=2'
Assert ($gui.nodeCount -gt 0 -and $gui.roots.Count -gt 0) 'raw scene dump contains nodes'
$shot = Invoke-WebRequest "$baseUrl/screenshot" -UseBasicParsing -TimeoutSec 20
Assert ($shot.Headers['Content-Type'] -eq 'image/png' -and [BitConverter]::ToString($shot.Content[0..7]) -eq '89-50-4E-47-0D-0A-1A-0A') 'screenshot returns PNG'
$log = Get-Json '/log'
Assert ($null -ne $log.entries) 'log ring answers'
$timer = [Diagnostics.Stopwatch]::StartNew()
$wait = Post-Json '/wait?timeout=1000' 'false'
Assert ($wait.ok -and -not $wait.satisfied -and $wait.frames -gt 0 -and $timer.ElapsedMilliseconds -ge 900 -and $timer.ElapsedMilliseconds -lt 5000) 'false predicate times out in about one second'
$reloaded = Post-Json '/reload' ''
$deadline = (Get-Date).AddSeconds(10)
do {
    $afterReload = Get-Json '/loader/status'
    if ($afterReload.failedReloadCount -gt $status.failedReloadCount) { break }
    Start-Sleep -Milliseconds 100
} while ((Get-Date) -lt $deadline)
Assert ($afterReload.failedReloadCount -gt $status.failedReloadCount) 'reload request reaches the main thread'
$status = $afterReload
Assert (-not $status.modLoaded -and $status.lastReloadError -match 'SongsOfConquest.Access.dll') 'empty-body reload reports missing mod'
$badQuery = Expect-Status '/loader/status?undeclared=1' 400
Assert ($badQuery -match 'undeclared') 'unknown query error names the parameter'

# Raw TCP preserves the lack of Content-Length; HTTP client APIs may add it themselves.
$client = New-Object Net.Sockets.TcpClient('127.0.0.1', $Port)
try {
    $stream = $client.GetStream()
    $stream.ReadTimeout = 10000
    $request = [Text.Encoding]::ASCII.GetBytes("POST /reload HTTP/1.1`r`nHost: 127.0.0.1:$Port`r`nConnection: close`r`n`r`n")
    $stream.Write($request, 0, $request.Length)
    $reader = New-Object IO.StreamReader($stream)
    $statusLine = $reader.ReadLine()
    Assert ($statusLine -match '^HTTP/1\.[01] 411 ') 'bodyless POST answers HTTP 411'
} finally { $client.Close() }
