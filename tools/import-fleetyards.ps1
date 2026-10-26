# Imports ship drafts from a Fleetyards export (T7.1b): ships.json from sc-ship-outlines/fetch.ps1. The export stays out
# of the repo (D56, licence); only values derived from it go into ships/<id>.json. Steps: docs/ship-format.md,
# "Importing from Fleetyards".
# Usage: ./tools/import-fleetyards.ps1 -Source <ships.json> [-ShipsFolder <folder>] [-Create] [-Force <id>]
#   Default: compares the grid data of existing files with the export and, where Fleetyards lays the racks (offsets on
#   every hold, no overlap), the boxes they take up to a shift of the whole ship (D78); writes nothing. hull_a is
#   compared too, as the calibration of that mapping.
#   -Create writes ships that have no file yet, never hull_a. -Force <id> rewrites one ship's grid data (grid, capacityScu,
#   totalScu, source); where Fleetyards lays the racks also origins, positions and a stack axis that fills each box. Rack
#   order, a stack axis that fits, the rest of the notes, and the origins of a generated row are hand work and stay. A
#   changed rack set keeps the racks whose id and grid still match, adds the new ones and drops the vanished (K5.2).
#   A door limit (maxCrateSize, from limits.max.capacity, D70) and a minimum (minCrateSize, limits.min.capacity, K3.3)
#   are added where the file has none, never overwritten.
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
# Turns Fleetyards gives a hold (about its y, the axis along the ship); the index of 26 Oct 2026 has none other (K5.2).
$Rotations = @(0, 90, 270)
# Stack axes in the order a fitting one is looked for: the file's sign first, then z, y, x (D78).
$StackAxes = @('+z', '+y', '+x', '-z', '-y', '-x')
$ArrangementNotes = [ordered]@{
    fleetyards = 'rack positions from Fleetyards; port/starboard from Fleetyards x sign, not checked in game'
    overlap    = 'rack positions from Fleetyards overlap; arrangement generated; grids from Fleetyards'
    generated  = 'rack arrangement generated; grids from Fleetyards'
}

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

# The largest crate a hold of these cells (x, y, z in Fleetyards axes) takes. Crates turn 90 degrees about the stack
# axis (D71), so the longer horizontal side takes the crate's length, whichever axis it lies on.
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

# The ship axes (0 x, 1 y, 2 z) of a rack's length, width and stack for a stack axis: Rack.FrameOf in Core (D26). The
# sign only mirrors the grid inside its box.
function Get-Frame([string]$Axis) {
    switch ($Axis.Substring(1)) {
        'x' { @(2, 1, 0) }
        'y' { @(0, 2, 1) }
        'z' { @(0, 1, 2) }
        default { throw "Unknown stack axis '$Axis'." }
    }
}

# The stack axis that lays the grid in the box: the preferred one where it does, else the first that does, same sign
# first (K5.2, D78). The Hull C keeps its 8x8x6 grids, so its level arms stack along z, its upright ones along y.
function Get-StackAxis([object]$Box, [object]$Grid, [string]$Preferred, [string]$Where) {
    $sign = $Preferred.Substring(0, 1)
    $candidates = @($Preferred) + @($StackAxes | Where-Object { $_.StartsWith($sign) }) + @($StackAxes | Where-Object { -not $_.StartsWith($sign) })
    $extent = @(($Box.Max[0] - $Box.Min[0]), ($Box.Max[1] - $Box.Min[1]), ($Box.Max[2] - $Box.Min[2]))
    foreach ($axis in $candidates) {
        $frame = Get-Frame $axis
        if ($extent[$frame[0]] -eq $Grid.length -and $extent[$frame[1]] -eq $Grid.width -and $extent[$frame[2]] -eq $Grid.stack) {
            return $axis
        }
    }
    throw "${Where}: no stack axis lays grid $(Get-GridText $Grid) in Fleetyards' box of $($extent -join 'x') cells."
}

# The box a ship file's rack takes in ship cells: origin, then the grid along the axes of its stack axis.
function Get-FileBox([object]$Rack) {
    $frame = Get-Frame ([string]$Rack.stackAxis)
    $grid = @([int]$Rack.grid.length, [int]$Rack.grid.width, [int]$Rack.grid.stack)
    $min = @([int]$Rack.origin[0], [int]$Rack.origin[1], [int]$Rack.origin[2])
    $max = @($min[0], $min[1], $min[2])
    for ($i = 0; $i -lt 3; $i++) {
        $max[$frame[$i]] += $grid[$i]
    }
    [pscustomobject]@{ Min = $min; Max = $max }
}

# Boxes as sorted text, shifted so the arrangement starts at 0 on every axis: holo and camera hang on the middle of the
# ship's bounds (D63, D76), so a shift of the whole ship is no deviation.
function Get-ArrangementText([object[]]$Boxes) {
    $low = @(foreach ($axis in 0, 1, 2) { [int](@($Boxes | ForEach-Object { $_.Min[$axis] }) | Measure-Object -Minimum).Minimum })
    @($Boxes | ForEach-Object {
        '[{0},{1},{2}]-[{3},{4},{5}]' -f ($_.Min[0] - $low[0]), ($_.Min[1] - $low[1]), ($_.Min[2] - $low[2]),
            ($_.Max[0] - $low[0]), ($_.Max[1] - $low[1]), ($_.Max[2] - $low[2])
    } | Sort-Object)
}

# Generated row (no usable offsets): Rack 1 at the bow, each next rack aft of it with one cell gap, either way round.
# Returned as one list: a single origin written to the pipeline would unroll into three numbers (Salvation, one rack).
function Get-RowOrigins([object[]]$Grids) {
    $origins = [System.Collections.Generic.List[object]]::new()
    $x = 0
    for ($i = 0; $i -lt $Grids.Count; $i++) {
        if ($i -gt 0) {
            $x = $x - 1 - [math]::Max([int]$Grids[$i].length, [int]$Grids[$i].width)
        }
        $origins.Add(@($x, 0, 0))
    }
    , $origins
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
    $rotation = 0
    if ($null -ne $Hold.rotation) {
        $turn = [decimal]$Hold.rotation
        if ($turn -ne [decimal]::Truncate($turn) -or $Rotations -notcontains [int]$turn) {
            throw "${where}: rotation $($Hold.rotation) is not supported ($($Rotations -join ', '))."
        }
        $rotation = [int]$turn
    }
    # Ship cells (D14, D78): x = Fleetyards y towards the bow (checked on the Starlancer MAX, K5.2), y = -(Fleetyards x)
    # to port, z = z. A hold turned 90 or 270 degrees about Fleetyards y (the Hull C's upright arms) swaps its x and z
    # extents; the offset is its min corner after the turn. The grid stays as the hold gives it.
    $box = $null
    if ($null -ne $Hold.offset) {
        $offset = @(
            (Get-Cells $Hold.offset.x "$where offset x"),
            (Get-Cells $Hold.offset.y "$where offset y"),
            (Get-Cells $Hold.offset.z "$where offset z"))
        $extent = if ($rotation -eq 0) { $size } else { @($size[2], $size[1], $size[0]) }
        $low = @($offset[1], (-($offset[0] + $extent[0])), $offset[2])
        $box = [pscustomobject]@{ Min = $low; Max = @(($low[0] + $extent[1]), ($low[1] + $extent[0]), ($low[2] + $extent[2])) }
    }
    [pscustomobject]@{
        Id       = "rack-$Index"
        Size     = $size
        # Fleetyards' own orientation (D71): length along the ship (y), width across (x).
        Grid     = [pscustomobject]@{ length = $size[1]; width = $size[0]; stack = $size[2] }
        Capacity = $capacity
        MaxCrate = $max
        Door     = $door
        Min      = $min
        Box      = $box
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

    $withOffsets = @($racks | Where-Object { $null -eq $_.Box }).Count -eq 0
    $overlap = $withOffsets -and (Test-Overlap @($racks | ForEach-Object { $_.Box }))
    $arrangement = if ($withOffsets -and -not $overlap) { 'fleetyards' }
        elseif ($overlap) { 'overlap' }
        else { 'generated' }

    $origins = @{}
    $axes = @{}
    $positions = @{}
    if ($arrangement -eq 'fleetyards') {
        $all = @($racks | ForEach-Object { $_.Box })
        $yBoth = (@($all | ForEach-Object { $_.Min[1] }) | Measure-Object -Minimum).Minimum + (@($all | ForEach-Object { $_.Max[1] }) | Measure-Object -Maximum).Maximum
        $zBoth = (@($all | ForEach-Object { $_.Min[2] }) | Measure-Object -Minimum).Minimum + (@($all | ForEach-Object { $_.Max[2] }) | Measure-Object -Maximum).Maximum
        $above = @($all | Where-Object { 2 * $_.Min[2] -ge $zBoth }).Count -gt 0
        $below = @($all | Where-Object { 2 * $_.Max[2] -le $zBoth }).Count -gt 0
        foreach ($r in $racks) {
            $origins[$r.Id] = $r.Box.Min
            $axes[$r.Id] = Get-StackAxis $r.Box $r.Grid '+z' "$($Ship.slug) $($r.Id)"
            # Rack centre against ship centre, both doubled to stay in whole numbers. A rack on the middle can't be told
            # port or starboard, nor upper or lower where racks lie wholly above and below it: no position (Hull C).
            $y = $r.Box.Min[1] + $r.Box.Max[1]
            $z = $r.Box.Min[2] + $r.Box.Max[2]
            $positions[$r.Id] = if ($y -eq $yBoth -or ($z -eq $zBoth -and $above -and $below)) { $null }
                else {
                    [pscustomobject]@{
                        side  = if ($y -ge $yBoth) { 'port' } else { 'starboard' }
                        level = if ($z -gt $zBoth) { 'upper' } else { 'lower' }
                    }
                }
        }
    }
    else {
        $row = Get-RowOrigins @($ordered | ForEach-Object { $_.Grid })
        for ($i = 0; $i -lt $ordered.Count; $i++) {
            $origins[$ordered[$i].Id] = $row[$i]
            $axes[$ordered[$i].Id] = '+z'
        }
    }

    $notes = $ArrangementNotes[$arrangement]
    if (@($racks | Where-Object { $null -ne $_.Door }).Count) {
        $notes += '; door limits from Fleetyards limits.max.capacity'
    }
    if (@($racks | Where-Object { $null -ne $_.Min }).Count) {
        $notes += '; crate minimums from Fleetyards limits.min.capacity'
    }

    $fileRacks = @(foreach ($r in $ordered) {
        # Box and Walkway are for -Force and the compare; Format-Ship writes only the file's fields.
        [pscustomobject]@{
            id           = $r.Id
            capacityScu  = $r.Capacity
            grid         = $r.Grid
            position     = $positions[$r.Id]
            origin       = $origins[$r.Id]
            stackAxis    = $axes[$r.Id]
            maxCrateSize = $r.Door
            minCrateSize = $r.Min
            Box          = $r.Box
            Walkway      = $r.MaxCrate -le $WalkwayMaxCrate
        }
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

# Grid data, and where Fleetyards lays the racks the boxes they take up to a shift of the whole ship (D78). A generated
# row's origins, every stack axis that fills the same box, position, order and notes are hand work and not compared.
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
    if ($Draft.Arrangement -eq 'fleetyards') {
        $inFile = @(Get-ArrangementText @(@($File.racks) | ForEach-Object { Get-FileBox $_ }))
        $inExport = @(Get-ArrangementText @($Draft.racks | ForEach-Object { $_.Box }))
        if (($inFile -join ' ') -ne ($inExport -join ' ')) {
            $onlyFile = @($inFile | Where-Object { $inExport -notcontains $_ })
            $onlyExport = @($inExport | Where-Object { $inFile -notcontains $_ })
            $differences.Add(('arrangement: {0} of {1} racks lie elsewhere (file {2}; Fleetyards {3})' -f
                $onlyFile.Count, $inFile.Count, (@($onlyFile | Select-Object -First 2) -join ' '), (@($onlyExport | Select-Object -First 2) -join ' ')))
        }
    }
    , $differences
}

# Where Fleetyards lays the racks, -Force lays them so: origin, position and a stack axis that fills the box, the file's
# own where it does (D78). The arrangement part of the notes follows; the rest of the notes is hand work.
function Set-Arrangement([object]$File, [object]$Racks, [object]$Draft) {
    if ($Draft.Arrangement -ne 'fleetyards') {
        return
    }
    foreach ($r in @($Racks)) {
        $d = @($Draft.racks | Where-Object { $_.id -eq [string]$r.id })[0]
        Set-Field $r 'origin' $d.origin
        Set-Field $r 'stackAxis' (Get-StackAxis $d.Box $r.grid ([string]$r.stackAxis) "$($File.id) $($r.id)")
        Set-Field $r 'position' $d.position
    }
    $notes = [string]$File.notes
    foreach ($old in $ArrangementNotes.Values) {
        if ($notes.StartsWith($old, [System.StringComparison]::Ordinal)) {
            $notes = $ArrangementNotes['fleetyards'] + $notes.Substring($old.Length)
            break
        }
    }
    Set-Field $File 'notes' $(if ($notes) { $notes } else { $Draft.notes })
}

# A door or minimum set by hand stays, also against Fleetyards (D70); a missing one comes from the export.
function Add-Limits([object]$Rack, [object]$DraftRack) {
    foreach ($limit in 'maxCrateSize', 'minCrateSize') {
        if ($null -eq $Rack.$limit -and $null -ne $DraftRack.$limit) {
            Set-Field $Rack $limit $DraftRack.$limit
        }
    }
}

# -Force: the grid data from the export, the hand work kept where it still maps (K5.2, D78). The same rack set maps by id
# and takes the export's grids. A changed set (Fortune, Moth) keeps a rack whose id and grid Fleetyards still has, with
# all its hand work; new racks join behind the last kept one that takes more than 2 SCU, walkways last; vanished ones go.
# Nothing kept: the draft. Returns the ship to write and what changed in the rack set.
function Update-Ship([object]$File, [object]$Draft, [string]$ExportDate) {
    $byId = @{}
    foreach ($d in $Draft.racks) {
        $byId[$d.id] = $d
    }
    $fileIds = @(@($File.racks) | ForEach-Object { [string]$_.id } | Sort-Object)
    $draftIds = @($Draft.racks | ForEach-Object { $_.id } | Sort-Object)
    $report = ''
    if (($fileIds -join ',') -eq ($draftIds -join ',')) {
        $racks = @($File.racks)
        foreach ($r in $racks) {
            $d = $byId[[string]$r.id]
            $r.grid = $d.grid
            $r.capacityScu = $d.capacityScu
            Add-Limits $r $d
        }
    }
    else {
        $kept = @(@($File.racks) | Where-Object { $byId.ContainsKey([string]$_.id) -and (Get-GridText $_.grid) -eq (Get-GridText $byId[[string]$_.id].grid) })
        if ($kept.Count -eq 0) {
            $Draft.notes += "; regenerated from Fleetyards export $ExportDate (rack set changed)"
            return [pscustomobject]@{ Ship = $Draft; Report = 'rack set changed, file regenerated' }
        }
        $keptIds = @($kept | ForEach-Object { [string]$_.id })
        $added = @($Draft.racks | Where-Object { $keptIds -notcontains $_.id })
        $dropped = @(@($File.racks) | ForEach-Object { [string]$_.id } | Where-Object { $keptIds -notcontains $_ })
        foreach ($r in $kept) {
            Add-Limits $r $byId[[string]$r.id]
        }
        $lastLarge = -1
        for ($i = 0; $i -lt $kept.Count; $i++) {
            if (-not $byId[[string]$kept[$i].id].Walkway) {
                $lastLarge = $i
            }
        }
        $racks = [System.Collections.Generic.List[object]]::new()
        for ($i = 0; $i -le $lastLarge; $i++) { $racks.Add($kept[$i]) }
        foreach ($d in @($added | Where-Object { -not $_.Walkway })) { $racks.Add($d) }
        for ($i = $lastLarge + 1; $i -lt $kept.Count; $i++) { $racks.Add($kept[$i]) }
        foreach ($d in @($added | Where-Object { $_.Walkway })) { $racks.Add($d) }
        $racks = @($racks)
        if ($Draft.Arrangement -ne 'fleetyards') {
            $row = Get-RowOrigins @($racks | ForEach-Object { $_.grid })
            for ($i = 0; $i -lt $racks.Count; $i++) {
                Set-Field $racks[$i] 'origin' $row[$i]
            }
        }
        $parts = @()
        if ($added.Count) { $parts += "$(@($added | ForEach-Object { $_.id }) -join ', ') added" }
        if ($dropped.Count) { $parts += "$($dropped -join ', ') dropped" }
        $report = "racks $($parts -join ', ')"
        $base = if ($File.notes) { [string]$File.notes } else { $Draft.notes }
        Set-Field $File 'notes' "$base; $report (Fleetyards export $ExportDate)"
        $File.racks = $racks
    }
    Set-Arrangement $File $racks $Draft
    $File.totalScu = $Draft.totalScu
    Set-Field $File 'source' $Draft.source
    [pscustomobject]@{ Ship = $File; Report = $report }
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
                $update = Update-Ship $file $draft $exportDate
                $writes[$path] = Format-Ship $update.Ship
                $status = 'updated'; $detail = if ($differences.Count) { $differences -join '; ' } else { 'grid data was in sync' }
                if ($update.Report) {
                    $detail += " ($($update.Report))"
                }
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
