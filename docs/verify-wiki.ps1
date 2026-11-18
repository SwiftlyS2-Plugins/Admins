# Verifies the wiki against the source it documents.
#
# Five checks:
#   1. Links      - every internal wiki link resolves to a page that exists
#   2. Anchors    - every "#section" target is a heading that exists on that page
#   3. Keys       - every JSONC key shown in the wiki is declared in the source
#   4. Navigation - README's wiki links resolve, and Home reaches every page
#   5. Defaults   - every Default cell of a settings table matches the source
#
# Run from the repo root:  pwsh -File docs/verify-wiki.ps1
# Point -WikiPath at the wiki clone if it is not the sibling default.

[CmdletBinding()]
param(
    [string]$RepoPath = (Split-Path $PSScriptRoot -Parent),
    [string]$WikiPath = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Admins.wiki')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $WikiPath)) {
    Write-Host "Wiki not found at $WikiPath — clone it or pass -WikiPath." -ForegroundColor Yellow
    exit 2
}

$readme = Join-Path $RepoPath 'README.md'
$pages = Get-ChildItem $WikiPath -Filter *.md | ForEach-Object { $_.BaseName }
$failures = @()

function Add-Failure([string]$check, [string]$detail) {
    $script:failures += [pscustomobject]@{ Check = $check; Detail = $detail }
}

# GitHub's heading -> anchor slug.
function ConvertTo-Anchor([string]$heading) {
    $h = [regex]::Replace($heading, '`|\*|_|<[^>]+>', '')
    $h = $h.Trim().ToLowerInvariant()
    $h = [regex]::Replace($h, '[^\w\s-]', '')
    return ($h -replace '\s', '-')
}

# ---- 1 + 2: links and anchors ------------------------------------------------

$anchors = @{}
foreach ($f in Get-ChildItem $WikiPath -Filter *.md) {
    $set = New-Object System.Collections.Generic.HashSet[string]
    $inCode = $false
    foreach ($line in Get-Content $f.FullName) {
        if ($line -match '^```') { $inCode = -not $inCode; continue }
        if (-not $inCode -and $line -match '^#{1,6}\s+(.*)$') { [void]$set.Add((ConvertTo-Anchor $Matches[1])) }
    }
    $anchors[$f.BaseName] = $set
}

foreach ($f in Get-ChildItem $WikiPath -Filter *.md) {
    $text = Get-Content $f.FullName -Raw
    foreach ($m in [regex]::Matches($text, '\]\(([^)\s]+)\)')) {
        $t = $m.Groups[1].Value
        if ($t -match '^(https?:|mailto:)') { continue }

        $parts = $t -split '#', 2
        $page = if ($parts[0] -eq '') { $f.BaseName } else { $parts[0] }

        if ($page -match '\.(png|jpg|jpeg|gif|svg|webp)$') {
            if (-not (Test-Path (Join-Path $WikiPath $page))) {
                Add-Failure 'Assets' "$($f.Name) -> $t  (image not committed to the wiki)"
            }
            continue
        }
        if ($page -match '\.md$') {
            Add-Failure 'Links' "$($f.Name) -> $t  (file-style link; wiki links are bare page names)"
            continue
        }
        if ($pages -notcontains $page) {
            Add-Failure 'Links' "$($f.Name) -> $t  (no such page)"
            continue
        }
        if ($parts.Count -eq 2 -and -not $anchors[$page].Contains($parts[1])) {
            Add-Failure 'Anchors' "$($f.Name) -> $t  (no such heading)"
        }
    }
}

# ---- 3: JSONC keys exist in the source ---------------------------------------

$exempt = @()

$declared = New-Object System.Collections.Generic.HashSet[string]
foreach ($f in Get-ChildItem $RepoPath -Recurse -Filter *.cs -File |
                Where-Object { $_.FullName -notmatch '\\(bin|obj|build)\\' }) {
    $t = Get-Content $f.FullName -Raw
    foreach ($m in [regex]::Matches($t, '(?m)^\s*(?:public|internal)\s+[\w\<\>\?\[\],\s\.]+?\s+(\w+)\s*\{\s*get')) {
        [void]$declared.Add($m.Groups[1].Value)
    }
    foreach ($m in [regex]::Matches($t, '"([A-Za-z_][\w\.\-]*)"')) {
        [void]$declared.Add($m.Groups[1].Value)
    }
}

$keysChecked = 0
foreach ($f in Get-ChildItem $WikiPath -Filter *.md) {
    $text = Get-Content $f.FullName -Raw
    foreach ($blk in [regex]::Matches($text, '(?s)```jsonc\r?\n(.*?)```')) {
        foreach ($line in ($blk.Groups[1].Value -split "`r?`n")) {
            $l = $line.Trim()
            if ($l -eq '' -or $l -match '^//') { continue }
            foreach ($m in [regex]::Matches($l, '"([^"]+)"\s*:')) {
                $key = $m.Groups[1].Value
                $keysChecked++
                if ($exempt -notcontains $key -and -not $declared.Contains($key)) {
                    Add-Failure 'Keys' "$($f.Name): `"$key`" is not declared anywhere in the source"
                }
            }
        }
    }
}

# ---- 4: README and Home navigation -------------------------------------------

$readmeText = Get-Content $readme -Raw
$readmeLinks = 0
foreach ($m in [regex]::Matches($readmeText, '\]\(\.\./\.\./wiki/([^)\s#]+)')) {
    $readmeLinks++
    if ($pages -notcontains $m.Groups[1].Value) {
        Add-Failure 'Navigation' "README -> wiki/$($m.Groups[1].Value)  (no such page)"
    }
}
foreach ($m in [regex]::Matches($readmeText, '\]\((docs/[^)\s]+)\)')) {
    Add-Failure 'Navigation' "README -> $($m.Groups[1].Value)  (repo path; content lives in the wiki)"
}

$homeText = Get-Content (Join-Path $WikiPath 'Home.md') -Raw
$linked = [regex]::Matches($homeText, '\]\(([^)\s#]+)\)') |
          ForEach-Object { $_.Groups[1].Value } |
          Where-Object { $_ -notmatch '^https?:' }
foreach ($p in $pages) {
    if ($p -ne 'Home' -and $linked -notcontains $p) {
        Add-Failure 'Navigation' "Home does not link to $p, so it is unreachable"
    }
}

# ---- 5: documented defaults match the source ---------------------------------
#
# Keyed by class: DatabaseSyncIntervalSeconds and Enabled each exist in several config classes
# with different defaults.

$configFiles = @(
    (Join-Path $RepoPath 'src\Models\Configuration.cs'),
    (Join-Path $RepoPath 'Admins.Contract\src\Enums.cs')
) + (Get-ChildItem (Join-Path $RepoPath 'src\Services') -Recurse -Filter '*Configuration.cs' -File | ForEach-Object { $_.FullName })

# A wiki section or setting prefix -> the class that declares its settings.
$sectionClass = @{
    'Main'     = 'Configuration'
    'Admins'   = 'AccessConfiguration'
    'Bans'     = 'BansConfiguration'
    'Comms'    = 'CommsConfiguration'
    'Sessions' = 'SessionsConfiguration'
    'Kick'     = 'KickConfiguration'
    'Beacon'   = 'BeaconConfiguration'
    'Server'   = 'ServerControlConfiguration'
    'Presence' = 'PresenceConfiguration'
    'Status'   = 'StatusConfiguration'
    'Esp'      = 'EspConfiguration'
}

$sourceDefault = @{}
$enumValue = @{}
foreach ($f in $configFiles | Where-Object { Test-Path $_ }) {
    # An enum setting is written to config.jsonc as its number, so the wiki states the number.
    $enumName = ''
    $next = 0
    foreach ($line in Get-Content $f) {
        if ($line -match '^\s*public\s+enum\s+(\w+)') { $enumName = $Matches[1]; $next = 0; continue }
        if ($enumName -and $line -match '^\s*\}') { $enumName = ''; continue }
        if ($enumName -and $line -match '^\s*(\w+)\s*(?:=\s*(-?\d+)\s*)?,?\s*$' -and $line -notmatch '^\s*(///|//|\[)') {
            $member = $Matches[1]
            if ($Matches[2]) { $next = [int]$Matches[2] }
            $enumValue["$enumName.$member"] = $next
            $next++
        }
    }

    $class = ''
    foreach ($line in Get-Content $f) {
        if ($line -match '^\s*public\s+(?:sealed\s+)?class\s+(\w+)') { $class = $Matches[1]; continue }
        if ($line -match '^\s*public\s+[\w\<\>\?\[\],\s\.]+?\s+(\w+)\s*\{\s*get;\s*(?:set|init);\s*\}\s*=\s*$') {
            $sourceDefault["$class|$($Matches[1])"] = '['
            continue
        }
        if ($line -match '^\s*public\s+[\w\<\>\?\[\],\s\.]+?\s+(\w+)\s*\{\s*get;\s*(?:set|init);\s*\}\s*=\s*(.+?);') {
            $name = $Matches[1]
            $v = $Matches[2].Trim()

            if ($v -match '^new\b') { continue }
            if ($v -eq 'string.Empty') { $v = '""' }
            $sourceDefault["$class|$name"] = $v
        }
    }
}

function Normalise([string]$v) {
    if ($null -eq $v) { return '' }
    $v = ($v.Trim() -replace '`', '' -replace '"', '' -replace "'", '' -replace '\s', '').Trim()
    if ($v -match '^-?\d+(\.\d+)?[fdmFDM]$') { $v = $v.Substring(0, $v.Length - 1) }
    return $v.ToLowerInvariant()
}

$defaultsChecked = 0
foreach ($f in Get-ChildItem $WikiPath -Filter *.md) {
    $lines = Get-Content $f.FullName
    $section = ''
    for ($i = 0; $i -lt $lines.Count - 1; $i++) {
        if ($lines[$i] -match '^##\s+(.+)$') { $section = $Matches[1].Trim(); continue }
        if ($lines[$i] -notmatch '^\|' -or $lines[$i + 1] -notmatch '^\|[\s\-:|]+\|$') { continue }

        $cols = ($lines[$i].Trim('|') -split '\|') | ForEach-Object { $_.Trim() }
        $keyIdx = [array]::FindIndex($cols, [Predicate[string]] { param($c) $c -in @('Setting', 'Field') })
        $defIdx = [array]::FindIndex($cols, [Predicate[string]] { param($c) $c -eq 'Default' })
        if ($keyIdx -lt 0 -or $defIdx -lt 0) { continue }

        for ($j = $i + 2; $j -lt $lines.Count -and $lines[$j] -match '^\|'; $j++) {
            $cells = ($lines[$j] -replace '\\\|', '/' ).Trim('|') -split '\|' | ForEach-Object { $_.Trim() }
            if ($cells.Count -le [Math]::Max($keyIdx, $defIdx)) { continue }

            $path = ($cells[$keyIdx] -replace '`', '').Trim()
            $segments = $path -split '\.'
            $key = $segments[-1]
            $prefix = if ($segments.Count -gt 1) { $segments[0] } else { $section }
            if (-not $sectionClass.ContainsKey($prefix)) { continue }

            $class = $sectionClass[$prefix]
            if (-not $sourceDefault.ContainsKey("$class|$key")) {
                Add-Failure 'Defaults' "$($f.Name): ``$path`` has no matching setting in $class"
                continue
            }

            $doc = Normalise $cells[$defIdx]
            $srcRaw = $sourceDefault["$class|$key"]

            # A list written out over several lines is documented as a phrase, such as "14 reasons".
            if ($srcRaw -eq '[' -or $doc -match '^\d+(reasons|lines)$') { continue }

            $defaultsChecked++
            $accept = New-Object System.Collections.Generic.HashSet[string]
            [void]$accept.Add((Normalise $srcRaw))
            if ($srcRaw -match '^(\w+)\.(\w+)$') {
                [void]$accept.Add($Matches[2].ToLowerInvariant())
                if ($enumValue.ContainsKey($srcRaw)) { [void]$accept.Add([string]$enumValue[$srcRaw]) }
            }

            if (-not $accept.Contains($doc)) {
                Add-Failure 'Defaults' "$($f.Name): ``$path`` is documented as '$($cells[$defIdx])' but the source default is '$srcRaw'"
            }
        }
    }
}

# ---- Report ------------------------------------------------------------------

Write-Host ""
Write-Host "Pages: $($pages.Count)    README wiki links: $readmeLinks    JSONC keys: $keysChecked    Defaults: $defaultsChecked"
Write-Host ""

if ($failures.Count -eq 0) {
    Write-Host "All wiki checks passed." -ForegroundColor Green
    exit 0
}

foreach ($g in $failures | Group-Object Check) {
    Write-Host "$($g.Name) ($($g.Count))" -ForegroundColor Red
    $g.Group | ForEach-Object { Write-Host "  $($_.Detail)" }
    Write-Host ""
}
exit 1
