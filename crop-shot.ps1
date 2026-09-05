param(
    # x,y,w,h of the region to keep, in screen pixels, top-left origin.
    [Parameter(Mandatory = $true)][int[]]$Rect,
    [string]$Out,
    [int]$Margin = 12,
    [string]$DevUrl = 'http://127.0.0.1:8772'
)

# Fetch a screenshot and crop it to one region, so evidence for "this is what is drawn there"
# costs a small image instead of a full frame. Reading full frames into an agent's context was
# measured as the single largest token cost of a verification stage; never do it.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if ($Rect.Count -ne 4) {
    Write-Error "-Rect needs x,y,w,h"
    exit 1
}

$tmp = Join-Path $env:TEMP "socaccess-shot.png"
curl.exe -s --max-time 20 "$DevUrl/screenshot" -o $tmp
if (-not (Test-Path $tmp) -or (Get-Item $tmp).Length -lt 1024) {
    Write-Error "screenshot did not arrive ($tmp)"
    exit 1
}

if (-not $Out) {
    $Out = Join-Path (Get-Location) ("crop-{0}-{1}-{2}x{3}.png" -f $Rect[0], $Rect[1], $Rect[2], $Rect[3])
}

$src = [System.Drawing.Bitmap]::FromFile($tmp)
try {
    $x = [Math]::Max(0, $Rect[0] - $Margin)
    $y = [Math]::Max(0, $Rect[1] - $Margin)
    $w = [Math]::Min($src.Width - $x, $Rect[2] + 2 * $Margin)
    $h = [Math]::Min($src.Height - $y, $Rect[3] + 2 * $Margin)
    $crop = $src.Clone([System.Drawing.Rectangle]::new($x, $y, $w, $h), $src.PixelFormat)
    try {
        $crop.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $crop.Dispose()
    }
} finally {
    $src.Dispose()
}

Write-Host "cropped [$($Rect -join ',')] +$Margin -> $Out"
