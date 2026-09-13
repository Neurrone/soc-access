<#
.SYNOPSIS
    Build the shipped battlefield-description tables from the authoring folder.

.DESCRIPTION
    Turns the per-layout description files under battlefields\descriptions\<language>\ into the
    tables the mod loads at runtime, soc-access\battlefields\<language>.json - one file per
    language, every layout in it, keys sorted.

    The authoring folder holds one file per layout, `{"terrain","attacker","defender"}`, under the
    same <LevelType>\<PathName> path the dumps use, so the key a file builds is its own path with
    the extension taken off. That is the key the game's own MapFormat answers to at runtime
    (Metadata.Type + "/" + Metadata.PathName), so nothing translates between the two and nothing
    can drift out of step.

    Every authored file must name a layout the game actually ships, which is what
    battlefields\<LevelType>\<PathName>.json - the dump BattlefieldDump wrote - is checked for. A
    file with a missing or empty field, or a key the English set has and another language does not,
    fails the build rather than shipping a description the player hears half of.

    A layout the player can reach (contentProfile Demo or Releasable) that has no English
    description is only WARNED about: the English set is still being written.

.PARAMETER Authoring
    The folder holding the authored descriptions. Defaults to battlefields\descriptions beside
    this script.

.PARAMETER Dumps
    The folder holding the dumped layouts. Defaults to battlefields beside this script.

.PARAMETER OutputDirectory
    Where the built tables are written. Defaults to soc-access\battlefields.
#>
[CmdletBinding()]
param(
    [string]$Authoring,
    [string]$Dumps,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

# Not $PSScriptRoot: it is empty inside a param default, which silently turns the defaults below
# into relative paths off whatever the caller's working directory happens to be.
$Root = Split-Path -Parent $MyInvocation.MyCommand.Definition
if (-not $Dumps) { $Dumps = Join-Path $Root 'battlefields' }
if (-not $Authoring) { $Authoring = Join-Path $Dumps 'descriptions' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $Root 'soc-access\battlefields' }

$Fields = @('terrain', 'attacker', 'defender')

function ConvertTo-JsonString {
    param([string]$Value)

    $out = New-Object System.Text.StringBuilder
    [void]$out.Append('"')
    foreach ($ch in $Value.ToCharArray()) {
        if ($ch -eq '"') { [void]$out.Append('\"') }
        elseif ($ch -eq '\') { [void]$out.Append('\\') }
        elseif ([int]$ch -eq 8) { [void]$out.Append('\b') }
        elseif ([int]$ch -eq 12) { [void]$out.Append('\f') }
        elseif ([int]$ch -eq 10) { [void]$out.Append('\n') }
        elseif ([int]$ch -eq 13) { [void]$out.Append('\r') }
        elseif ([int]$ch -eq 9) { [void]$out.Append('\t') }
        elseif ([int]$ch -lt 0x20) { [void]$out.AppendFormat('\u{0:x4}', [int]$ch) }
        else { [void]$out.Append($ch) }
    }
    [void]$out.Append('"')
    return $out.ToString()
}

function Read-Language {
    param([string]$Directory, [string]$Language)

    $layouts = @{}
    foreach ($file in Get-ChildItem -Path $Directory -Filter *.json -Recurse | Sort-Object FullName) {
        $relative = $file.FullName.Substring($Directory.Length).TrimStart('\', '/')
        $key = ($relative -replace '\.json$', '') -replace '\\', '/'
        if ($key -notmatch '^[^/]+/[^/]+$') {
            throw "$Language`: '$relative' is not <LevelType>\<PathName>.json"
        }

        $dump = Join-Path $Dumps ($key -replace '/', '\')
        if (-not (Test-Path "$dump.json")) {
            throw "$Language`: '$relative' describes '$key', which this install has no dump for ($dump.json)"
        }

        $authored = Get-Content -Raw -Encoding UTF8 $file.FullName | ConvertFrom-Json
        $entry = @{}
        foreach ($field in $Fields) {
            $value = if ($authored.PSObject.Properties.Name -contains $field) { [string]$authored.$field } else { '' }
            if ([string]::IsNullOrWhiteSpace($value)) {
                throw "$Language`: '$relative' has no $field"
            }

            $entry[$field] = $value.Trim()
        }

        $layouts[$key] = $entry
    }

    return $layouts
}

function Write-Table {
    param([string]$Language, [hashtable]$Layouts)

    $out = New-Object System.Text.StringBuilder
    [void]$out.AppendLine('{')
    [void]$out.AppendLine('  "version": 1,')
    [void]$out.AppendLine("  ""language"": $(ConvertTo-JsonString $Language),")
    [void]$out.AppendLine('  "layouts": {')
    $keys = @($Layouts.Keys | Sort-Object)
    for ($i = 0; $i -lt $keys.Count; $i++) {
        $entry = $Layouts[$keys[$i]]
        [void]$out.AppendLine("    $(ConvertTo-JsonString $keys[$i]): {")
        for ($f = 0; $f -lt $Fields.Count; $f++) {
            $tail = if ($f -lt $Fields.Count - 1) { ',' } else { '' }
            [void]$out.AppendLine("      $(ConvertTo-JsonString $Fields[$f]): $(ConvertTo-JsonString $entry[$Fields[$f]])$tail")
        }

        $tail = if ($i -lt $keys.Count - 1) { ',' } else { '' }
        [void]$out.AppendLine("    }$tail")
    }

    [void]$out.AppendLine('  }')
    [void]$out.AppendLine('}')

    $path = Join-Path $OutputDirectory "$Language.json"
    [System.IO.File]::WriteAllText($path, $out.ToString(), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "$Language`: $($keys.Count) layouts -> $path"
}

if (-not (Test-Path $Dumps)) { throw "No dump folder at $Dumps" }
if (-not (Test-Path $Authoring)) { throw "No authoring folder at $Authoring" }

$languages = [ordered]@{}
foreach ($directory in Get-ChildItem -Path $Authoring -Directory | Sort-Object Name) {
    $languages[$directory.Name] = Read-Language -Directory $directory.FullName -Language $directory.Name
}

if ($languages.Count -eq 0) { throw "No language folders under $Authoring" }
if (-not $languages.Contains('en')) { throw "No English descriptions under $Authoring" }

# A layout described in English and not in another language would be silently dropped for that
# language's players, who would hear nothing where an English player hears three lines.
$english = $languages['en']
foreach ($language in $languages.Keys) {
    if ($language -eq 'en') { continue }
    $missing = @($english.Keys | Where-Object { -not $languages[$language].ContainsKey($_) } | Sort-Object)
    if ($missing.Count -gt 0) {
        throw "$language is missing $($missing.Count) layout(s) the English set describes: $($missing -join ', ')"
    }
}

if (-not (Test-Path $OutputDirectory)) { [void](New-Item -ItemType Directory -Path $OutputDirectory) }
foreach ($language in $languages.Keys) {
    Write-Table -Language $language -Layouts $languages[$language]
}

# Not an error yet: the English set is still being written. Only the layouts a player can actually
# reach are counted - the rest are test and editor fixtures.
$undescribed = New-Object System.Collections.Generic.List[string]
foreach ($dump in Get-ChildItem -Path $Dumps -Filter *.json -Recurse -File) {
    if ($dump.FullName.StartsWith($Authoring, [System.StringComparison]::OrdinalIgnoreCase)) { continue }
    $layout = Get-Content -Raw -Encoding UTF8 $dump.FullName | ConvertFrom-Json
    if ($layout.contentProfile -ne 'Demo' -and $layout.contentProfile -ne 'Releasable') { continue }
    if (-not $english.ContainsKey([string]$layout.key)) { $undescribed.Add([string]$layout.key) }
}

if ($undescribed.Count -gt 0) {
    Write-Warning "$($undescribed.Count) reachable layout(s) have no English description:"
    foreach ($key in ($undescribed | Sort-Object)) { Write-Warning "  $key" }
}
