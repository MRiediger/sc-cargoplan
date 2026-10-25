# Imports ship drafts from a Fleetyards export (T7.1b): ships.json from sc-ship-outlines/fetch.ps1. The export stays out
# of the repo (D56, licence); only values derived from it go into ships/<id>.json. Steps: docs/ship-format.md,
# "Importing from Fleetyards".
# Usage: ./tools/import-fleetyards.ps1 -Source <ships.json> [-ShipsFolder <folder>] [-Create] [-Force <id>]
#   Default: compares the grid data of existing files with the export and writes nothing.
#   -Create writes ships that have no file yet, never hull_a. -Force <id> rewrites one ship's grid data (grid, capacityScu,
#   totalScu, source); origins, stack axes, positions, rack order and notes are hand work and stay. A door limit
#   (maxCrateSize, from limits.max.capacity, D70) and a minimum (minCrateSize, limits.min.capacity, K3.3) are added
#   where the file has none, never overwritten.
# Exit code: 0 in sync, 1 a deviation or a new ship not written, 2 an error (then nothing is written).
# Runs under Windows PowerShell 5.1 and PowerShell 7. ASCII only: 5.1 reads a script without BOM as ANSI.
param(
    [Parameter(Mandatory = $true)]
    [string]$Source,
    # Default ships/ next to tools/, set below: Windows PowerShell 5.1 leaves $PSScriptRoot empty in parameter defaults.
    [string]$ShipsFolder,
    [switch]$Create,
    [string]$Force
)
$ErrorActionPreference = 'Stop'
if (-not $ShipsFolder) {
    $ShipsFolder = Join-Path (Split-Path $PSScriptRoot -Parent) 'ships'
}

$SchemaVersion = 5
$CellMeters = [decimal]1.25
$FallbackShipId = 'hull_a'
$CrateSizes = @(1, 2, 4, 8, 16, 24, 32)
# Crate sizes with their shapes in cells (SCU, length, width, stack), fixed orientation, as CrateSize.Dimensions (D57).
# A list, not an ordered hashtable: its [int] indexer would read a position, not a key.
$CrateShapes = @(@(1, 1, 1, 1), @(2, 2, 1, 1), @(4, 2, 2, 1), @(8, 2, 2, 2), @(16, 4, 2, 2), @(24, 6, 2, 2), @(32, 8, 2, 2))
# Racks that take nothing above 2 SCU are ladders and walkways: they go last in rack order, but stay, so the capacity
# matches the game (Caterpillar 576).
$WalkwayMaxCrate = 2
$IdPattern = '^[a-z0-9]+(_[a-z0-9]+)*$'

# Fleetyards metres to cells; a value off the 1.25 m grid stops the import.
function Get-Cells([object]$Meters, [string]$Where) {
    $cells = [decimal]$Meters / $CellMeters
    if ($cells -ne [decimal]::Truncate($cells)) {
        throw "${Where}: $Meters m is no multiple of $CellMeters m."
    }
    [int]$cells
}

# Adds or replaces a property: ConvertFrom-Json objects only have the fields the file has.
function Set-Field([object]$Target, [string]$Name, [object]$Value) {
    if ($Target.PSObject.Properties[$Name]) {
        $Target.$Name = $Value
    }
    else {
        $Target | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
}

# The largest crate a hold of these cells (x, y, z in Fleetyards axes) takes, as the ship file lays it: length the
# longer horizontal side.
function Get-GridCrate([int[]]$Size) {
    $grid = @([math]::Max($Size[0], $Size[1]), [math]::Min($Size[0], $Size[1]), $Size[2])
    $largest = 0
    foreach ($shape in $CrateShapes) {
        if ($shape[1] -le $grid[0] -and $shape[2] -le $grid[1] -and $shape[3] -le $grid[2]) {
            $largest = $shape[0]
        }
    }
    $largest
}

function Get-GridText([object]$Grid) {
    '{0}x{1}x{2}' -f $Grid.length, $Grid.width, $Grid.stack
}

# Two boxes overlap if they share volume on all three axes; touching is fine (as in the loader).
function Test-Overlap([object[]]$Racks) {
    for ($i = 0; $i -lt $Racks.Count; $i++) {
        for ($k = $i + 1; $k -lt $Racks.Count; $k++) {
            $a = $Racks[$i]; $b = $Racks[$k]
            $apart = $false
            for ($axis = 0; $axis -lt 3; $axis++) {
                if ($a.Min[$axis] -ge $b.Max[$axis] -or $b.Min[$axis] -ge $a.Max[$axis]) {
                    $apart = $true
                }
            }
            if (-not $apart) {
                return $true
            }
        }
    }
    $false
}

# One Fleetyards cargo hold, checked and in cells. Fleetyards axes: x across (to starboard, see D53 addendum), y along
# the ship, z up; the offset is the hold's min corner (measured: a centre reading overlaps more).
function ConvertFrom-Hold([object]$Hold, [int]$Index, [string]$Slug) {
    $where = "$Slug rack $($Index + 1) ($($Hold.name))"
    $size = @(
        (Get-Cells $Hold.dimensions.x "$where x"),
        (Get-Cells $Hold.dimensions.y "$where y"),
        (Get-Cells $Hold.dimensions.z "$where z"))
    if (@($size | Where-Object { $_ -le 0 }).Count) {
        throw "${where}: dimensions must be greater than 0."
    }
    $capacity = [int]$Hold.capacity
    if ($size[0] * $size[1] * $size[2] -ne $capacity) {
        throw "${where}: $($size -join 'x') cells hold $($size[0] * $size[1] * $size[2]) SCU, but capacity is $capacity."
    }
    # The door limit is limits.max.capacity (D70); maxContainerSize isn't read. Values above the app's largest crate are
    # clamped to 32. Only a door smaller than what the grid takes is written: one the grid enforces anyway says nothing.
    $gridCrate = Get-GridCrate $size
    $limit = $null
    if ($null -ne $Hold.limits -and $null -ne $Hold.limits.max -and $null -ne $Hold.limits.max.capacity) {
        $limit = [math]::Min([int]$Hold.limits.max.capacity, $CrateSizes[-1])
        if ($CrateSizes -notcontains $limit) {
            throw "${where}: limits.max.capacity $($Hold.limits.max.capacity) is not a crate size."
        }
    }
    # A 1 is the one value that can also be a default: on a grid that takes more it stays unverified and isn't written
    # (Martin, K3.1). A missing limit costs a hauler one crate at the lift; a wrong one locks the whole rack.
    $unverified = $limit -eq 1 -and $gridCrate -gt 1
    if ($unverified) {
        $limit = $null
    }
    $max = if ($null -ne $limit -and $limit -lt $gridCrate) { $limit } else { $gridCrate }
    $door = if ($max -lt $gridCrate) { $max } else { $null }
    # The smallest crate the hold takes (K3.3, Hull C 32): written above 1 only, 0 counts as none, above 32 clamped.
    $min = $null
    if ($null -ne $Hold.limits -and $null -ne $Hold.limits.min -and [int]$Hold.limits.min.capacity -gt 1) {
        $min = [math]::Min([int]$Hold.limits.min.capacity, $CrateSizes[-1])
        if ($CrateSizes -notcontains $min) {
            throw "${where}: limits.min.capacity $($Hold.limits.min.capacity) is not a crate size."
        }
        if ($min -gt $max) {
            throw "${where}: limits.min.capacity $min is above what the hold takes ($max)."
        }
    }
    $offset = $null
    if ($null -ne $Hold.offset) {
        $offset = @(
            (Get-Cells $Hold.offset.x "$where offset x"),
            (Get-Cells $Hold.offset.y "$where offset y"),
            (Get-Cells $Hold.offset.z "$where offset z"))
    }
    [pscustomobject]@{
        Id       = "rack-$Index"
        Size     = $size
        Capacity = $capacity
        MaxCrate = $max
        Door     = $door
        Min      = $min
        Offset   = $offset
        # Crates don't turn (D9): length takes the longer horizontal side, so a rack wider than long still takes
        # the crates the game allows.
        Turned   = $size[0] -gt $size[1]
        Unverified = $unverified
    }
}

# A ship file's content (same shape as a file read back with ConvertFrom-Json) from one Fleetyards ship.
function ConvertTo-Draft([object]$Ship, [string]$ExportDate) {
    $prefix = "$($Ship.manufacturer.ToLowerInvariant())-"
    if (-not $Ship.slug.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
        throw "$($Ship.slug): the slug doesn't start with the manufacturer code '$prefix'."
    }
    $id = $Ship.slug.Substring($prefix.Length).Replace('-', '_')
    if ($id -cnotmatch $IdPattern) {
        throw "$($Ship.slug): id '$id' is not lowercase snake_case."
    }

    $holds = @($Ship.cargoHolds)
    $racks = @(for ($i = 0; $i -lt $holds.Count; $i++) { ConvertFrom-Hold $holds[$i] $i $Ship.slug })
    $ordered = @($racks | Where-Object { $_.MaxCrate -gt $WalkwayMaxCrate }) + @($racks | Where-Object { $_.MaxCrate -le $WalkwayMaxCrate })

    $withOffsets = @($racks | Where-Object { $null -eq $_.Offset }).Count -eq 0
    $boxes = @($racks | Where-Object { $null -ne $_.Offset } | ForEach-Object {
        $o = $_.Offset; $s = $_.Size
        [pscustomobject]@{ Min = $o; Max = @(($o[0] + $s[0]), ($o[1] + $s[1]), ($o[2] + $s[2])) }
    })
    $overlap = $withOffsets -and (Test-Overlap $boxes)
    $turned = @($racks | Where-Object { $_.Turned }).Count -gt 0
    $arrangement = if ($withOffsets -and -not $overlap -and -not $turned) { 'fleetyards' }
        elseif ($overlap) { 'overlap' }
        else { 'generated' }

    # Ship coordinates (D14, cells): x = Fleetyards y, y = -(Fleetyards x) to port, z = z.
    $origins = @{}
    $positions = @{}
    if ($arrangement -eq 'fleetyards') {
        $spans = @{}
        foreach ($r in $racks) {
            $origins[$r.Id] = @($r.Offset[1], (-($r.Offset[0] + $r.Size[0])), $r.Offset[2])
            $spans[$r.Id] = @($origins[$r.Id][1], ($origins[$r.Id][1] + $r.Size[0]), $r.Offset[2], ($r.Offset[2] + $r.Size[2]))
        }
        $allSpans = @($spans.Values)
        $yBoth = ($allSpans | ForEach-Object { $_[0] } | Measure-Object -Minimum).Minimum + ($allSpans | ForEach-Object { $_[1] } | Measure-Object -Maximum).Maximum
        $zBoth = ($allSpans | ForEach-Object { $_[2] } | Measure-Object -Minimum).Minimum + ($allSpans | ForEach-Object { $_[3] } | Measure-Object -Maximum).Maximum
        # Rack centre against ship centre, both doubled to stay in whole numbers.
        foreach ($r in $racks) {
            $s = $spans[$r.Id]
            $positions[$r.Id] = [pscustomobject]@{
                side  = if ($s[0] + $s[1] -ge $yBoth) { 'port' } else { 'starboard' }
                level = if ($s[2] + $s[3] -gt $zBoth) { 'upper' } else { 'lower' }
            }
        }
    }
    else {
        # Generated row: Rack 1 at the bow, each next rack aft of it with one cell gap.
        $x = 0
        for ($i = 0; $i -lt $ordered.Count; $i++) {
            if ($i -gt 0) {
                $x = $x - 1 - [math]::Max($ordered[$i].Size[0], $ordered[$i].Size[1])
            }
            $origins[$ordered[$i].Id] = @($x, 0, 0)
        }
    }

    $notes = switch ($arrangement) {
        'fleetyards' { 'rack positions from Fleetyards; port/starboard from Fleetyards x sign, not checked in game' }
        'overlap' { 'rack positions from Fleetyards overlap; arrangement generated; grids from Fleetyards' }
        default { 'rack arrangement generated; grids from Fleetyards' }
    }
    $turnedNumbers = @(for ($i = 0; $i -lt $ordered.Count; $i++) { if ($ordered[$i].Turned) { $i + 1 } })
    if ($turnedNumbers.Count) {
        $noun = if ($turnedNumbers.Count -eq 1) { 'rack' } else { 'racks' }
        $notes += "; $noun $($turnedNumbers -join ', ') turned so length is the longer side (crates don't turn)"
    }
    if (@($racks | Where-Object { $null -ne $_.Door }).Count) {
        $notes += '; door limits from Fleetyards limits.max.capacity'
    }
    if (@($racks | Where-Object { $null -ne $_.Min }).Count) {
        $notes += '; crate minimums from Fleetyards limits.min.capacity'
    }

    $fileRacks = @(foreach ($r in $ordered) {
        $rack = [pscustomobject]@{
            id           = $r.Id
            capacityScu  = $r.Capacity
            grid         = [pscustomobject]@{
                length = [math]::Max($r.Size[0], $r.Size[1])
                width  = [math]::Min($r.Size[0], $r.Size[1])
                stack  = $r.Size[2]
            }
            position     = $positions[$r.Id]
            origin       = $origins[$r.Id]
            stackAxis    = '+z'
            maxCrateSize = $r.Door
            minCrateSize = $r.Min
        }
        $rack
    })

    [pscustomobject]@{
        id             = $id
        name           = "$($Ship.manufacturer) $($Ship.name)"
        manufacturer   = $Ship.manufacturer
        fleetyardsSlug = $Ship.slug
        source         = "Fleetyards API (CIG ship matrix), export $ExportDate"
        notes          = $notes
        totalScu       = ($racks | Measure-Object -Property Capacity -Sum).Sum
        racks          = $fileRacks
        Arrangement    = $arrangement
        Unverified     = @($racks | Where-Object { $_.Unverified } | ForEach-Object { $_.Id })
    }
}

function Format-JsonString([string]$Value) {
    if ($Value -match '[\x00-\x1f]') {
        throw "Control character in '$Value'."
    }
    '"' + $Value.Replace('\', '\\').Replace('"', '\"') + '"'
}

# The layout of ships/hull_a.json: two spaces, grid, position and origin on one line, LF, final newline.
function Format-Ship([object]$Ship) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('{')
    $lines.Add("  ""schemaVersion"": $SchemaVersion,")
    foreach ($field in 'id', 'name', 'manufacturer', 'fleetyardsSlug', 'source', 'notes') {
        if ($null -ne $Ship.$field) {
            $lines.Add("  ""$field"": $(Format-JsonString $Ship.$field),")
        }
    }
    $lines.Add("  ""totalScu"": $($Ship.totalScu),")
    $lines.Add('  "racks": [')
    $racks = @($Ship.racks)
    for ($i = 0; $i -lt $racks.Count; $i++) {
        $r = $racks[$i]
        $fields = [System.Collections.Generic.List[string]]::new()
        $fields.Add("""id"": $(Format-JsonString $r.id)")
        $fields.Add("""capacityScu"": $($r.capacityScu)")
        $fields.Add("""grid"": { ""length"": $($r.grid.length), ""width"": $($r.grid.width), ""stack"": $($r.grid.stack) }")
        if ($null -ne $r.position) {
            $fields.Add("""position"": { ""side"": $(Format-JsonString $r.position.side), ""level"": $(Format-JsonString $r.position.level) }")
        }
        $fields.Add("""origin"": [$(@($r.origin) -join ', ')]")
        $fields.Add("""stackAxis"": $(Format-JsonString $r.stackAxis)")
        foreach ($limit in 'maxCrateSize', 'minCrateSize') {
            if ($null -ne $r.$limit) {
                $fields.Add("""${limit}"": $($r.$limit)")
            }
        }
        $lines.Add('    {')
        $lines.Add('      ' + ($fields -join ",`n      "))
        $lines.Add('    }' + $(if ($i -lt $racks.Count - 1) { ',' } else { '' }))
    }
    $lines.Add('  ]')
    $lines.Add('}')
    ($lines -join "`n") + "`n"
}

# Grid data only: origin, stack axis, position, order and notes are hand work and never compared.
function Compare-Ship([object]$File, [object]$Draft) {
    $differences = [System.Collections.Generic.List[string]]::new()
    if ([int]$File.totalScu -ne [int]$Draft.totalScu) {
        $differences.Add("totalScu: file $($File.totalScu), Fleetyards $($Draft.totalScu)")
    }
    $fileRacks = @{}
    foreach ($r in @($File.racks)) {
        $fileRacks[[string]$r.id] = $r
    }
    foreach ($d in $Draft.racks) {
        $f = $fileRacks[$d.id]
        if ($null -eq $f) {
            $differences.Add("$($d.id): not in file")
            continue
        }
        if ((Get-GridText $f.grid) -ne (Get-GridText $d.grid)) {
            $differences.Add("$($d.id): grid file $(Get-GridText $f.grid), Fleetyards $(Get-GridText $d.grid)")
        }
        if ([int]$f.capacityScu -ne $d.capacityScu) {
            $differences.Add("$($d.id): capacityScu file $($f.capacityScu), Fleetyards $($d.capacityScu)")
        }
        # A door or minimum is compared only where Fleetyards names one (D70): a hand value it doesn't know is no deviation.
        foreach ($limit in 'maxCrateSize', 'minCrateSize') {
            if ($null -ne $d.$limit -and $null -ne $f.$limit -and [int]$f.$limit -ne $d.$limit) {
                $differences.Add("$($d.id): $limit file $($f.$limit), Fleetyards $($d.$limit)")
            }
            elseif ($null -ne $d.$limit -and $null -eq $f.$limit) {
                $differences.Add("$($d.id): $limit not in file, Fleetyards $($d.$limit)")
            }
        }
    }
    $draftIds = @($Draft.racks | ForEach-Object { $_.id })
    foreach ($id in $fileRacks.Keys) {
        if ($draftIds -notcontains $id) {
            $differences.Add("${id}: not in Fleetyards")
        }
    }
    , $differences
}

function Get-Summary([object]$Ship) {
    $arrangement = switch ($Ship.Arrangement) {
        'fleetyards' { 'positions from Fleetyards' }
        'overlap' { 'arrangement generated (Fleetyards positions overlap)' }
        default { 'arrangement generated' }
    }
    '{0} racks, {1} SCU, {2}' -f @($Ship.racks).Count, $Ship.totalScu, $arrangement
}

try {
    if ($Force -eq $FallbackShipId) {
        throw "$FallbackShipId.json is never touched by the import: it is the fallback ship, measured in game (D56)."
    }
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        throw "Export not found: $Source"
    }
    if (-not (Test-Path -LiteralPath $ShipsFolder -PathType Container)) {
        throw "Ships folder not found: $ShipsFolder"
    }

    $exportDate = (Get-Item -LiteralPath $Source).LastWriteTime.ToString('yyyy-MM-dd')
    # Windows PowerShell 5.1 passes the whole JSON array on as one object; foreach unrolls it in both versions.
    $export = Get-Content -LiteralPath $Source -Raw -Encoding UTF8 | ConvertFrom-Json
    $all = @(foreach ($ship in $export) { $ship })

    $drafts = @{}
    $skipped = [System.Collections.Generic.List[string]]::new()
    foreach ($ship in $all) {
        if (@($ship.cargoHolds).Count -eq 0) {
            $skipped.Add($ship.slug)
            continue
        }
        $draft = ConvertTo-Draft $ship $exportDate
        if ($drafts.ContainsKey($draft.id)) {
            throw "Ship id '$($draft.id)' comes from both $($drafts[$draft.id].fleetyardsSlug) and $($ship.slug)."
        }
        $drafts[$draft.id] = $draft
    }
    if ($Force -and -not $drafts.ContainsKey($Force)) {
        throw "-Force ${Force}: no ship with this id and a cargo grid in the export."
    }

    $ids = [string[]]@($drafts.Keys)
    [Array]::Sort($ids, [StringComparer]::Ordinal)
    $report = [System.Collections.Generic.List[string]]::new()
    $writes = [ordered]@{}
    $counts = [ordered]@{ same = 0; new = 0; created = 0; updated = 0; different = 0; skipped = $skipped.Count }
    foreach ($id in $ids) {
        $draft = $drafts[$id]
        $path = Join-Path $ShipsFolder "$id.json"
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            if ($id -eq $FallbackShipId) {
                # Measured in game, not taken from Fleetyards: a missing hull_a.json is restored from git, not imported.
                $status = 'skipped'; $detail = 'never written by the import (fallback ship, D56) - restore it from git'
            }
            elseif ($Create) {
                $writes[$path] = Format-Ship $draft
                $status = 'created'; $detail = Get-Summary $draft
            }
            else {
                $status = 'new'; $detail = "$(Get-Summary $draft) - not written, run with -Create"
            }
        }
        else {
            $file = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
            $differences = Compare-Ship $file $draft
            if ($id -eq $Force) {
                $fileIds = @(@($file.racks) | ForEach-Object { [string]$_.id } | Sort-Object)
                $draftIds = @($draft.racks | ForEach-Object { $_.id } | Sort-Object)
                if (($fileIds -join ',') -ne ($draftIds -join ',')) {
                    throw "-Force ${id}: the rack set changed ($($fileIds -join ', ') in the file, $($draftIds -join ', ') in the export) - delete the file and run -Create."
                }
                foreach ($r in @($file.racks)) {
                    $d = @($draft.racks | Where-Object { $_.id -eq [string]$r.id })[0]
                    $r.grid = $d.grid
                    $r.capacityScu = $d.capacityScu
                    # A door or minimum set by hand stays, also against Fleetyards (D70).
                    foreach ($limit in 'maxCrateSize', 'minCrateSize') {
                        if ($null -eq $r.$limit -and $null -ne $d.$limit) {
                            Set-Field $r $limit $d.$limit
                        }
                    }
                }
                $file.totalScu = $draft.totalScu
                Set-Field $file 'source' $draft.source
                $writes[$path] = Format-Ship $file
                $status = 'updated'; $detail = if ($differences.Count) { $differences -join '; ' } else { 'grid data was in sync' }
            }
            elseif ($differences.Count) {
                $status = 'different'; $detail = $differences -join '; '
            }
            else {
                $status = 'same'; $detail = Get-Summary $draft
            }
        }
        if ($draft.Unverified.Count) {
            $detail += " (limits.max 1 unverified: $($draft.Unverified -join ', '))"
        }
        $counts[$status]++
        $report.Add(('{0,-10} {1,-30} {2}' -f $status, $id, $detail))
    }
    foreach ($slug in $skipped) {
        $report.Add(('{0,-10} {1,-30} {2}' -f 'skipped', ($slug -split '-', 2)[1].Replace('-', '_'), 'no cargo grid'))
    }

    # Written only after every ship converted and compared: an error above leaves the folder as it was.
    $utf8 = New-Object System.Text.UTF8Encoding $false
    foreach ($path in $writes.Keys) {
        [System.IO.File]::WriteAllText($path, $writes[$path], $utf8)
    }

    '{0}: {1} ships, {2} with a cargo grid' -f "Fleetyards export $exportDate", $all.Count, $drafts.Count
    $report
    ($counts.Keys | ForEach-Object { "$($counts[$_]) $_" }) -join ', '

    if ($counts.different -gt 0 -or $counts.new -gt 0) {
        exit 1
    }
    exit 0
}
catch {
    [Console]::Error.WriteLine("import-fleetyards: $($_.Exception.Message)")
    exit 2
}
