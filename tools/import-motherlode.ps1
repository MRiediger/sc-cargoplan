# Lays every ship's racks as the game files do (v0.3.0 session 4a, D92): reads a grid catalog (sc-ship-outlines/
# fetch-motherlode.ps1, field names of Motherlode's API) and writes ships/<id>.json. The catalog stays out of the repo;
# only values derived from it go in. Steps: docs/ship-format.md, "Where the data comes from".
# Usage: ./tools/import-motherlode.ps1 -Catalog <catalog.json> [-Previous <older catalog.json>] [-ShipsFolder <folder>]
#        [-Outlines <ship-outlines.json>] [-ModelMap <model-map.csv>] [-Create]
#   Writes every ship whose file differs from what the catalog gives; a ship already in sync stays byte for byte.
#   Per rack: origin, grid, stackAxis, rotation (only real angles, D91), maxCrateSize = min(maxContainerScu, the file's
#   value or what the grid takes); a minimum the catalog doesn't name stays. Rack ids, order and position stay (saved plans
#   name racks by id). Hand-written notes stay; the arrangement phrases of the retired Fleetyards import become "rack positions
#   from game files (Motherlode)".
#   Zero point: the game model's origin plus a fraction s < 1 cell per ship and axis that rounds the racks best; s and
#   every rack's rounding are reported.
#   Anchors (session 4b, D93): a ship with a model (className) and a slug lies where its model lies, since hologram and
#   outline come from that model: holoOrigin = s, outlineOrigin = s with z on the model's floor (floorM of -Outlines, default
#   outlines/ship-outlines.json), no outlineScale. Without a floorM for the slug the anchors stay as written and the ship is
#   listed. A ship without model or slug keeps its anchors.
#   -ModelMap writes id,className,modelKey,sx,sy,sz for every ship with a model and a file: the hologram pipeline
#   (sc-ship-outlines) builds per model and names the files by slug.
#   Not written, for a person to look at: grids that overlap (K1), a rack set or position that doesn't match the file,
#   grids in another order than in -Previous. A ship with a grid without transform stays as it is (Fleetyards).
#   -Create writes ships that have no file yet: a variant takes its base ship's hologram and outline.
#   A module (ships/modules) moves by what its ship's Rack 1 moved.
# Exit code: 0 done, 1 something to look at (new ship not written, hand check, order, anchors kept), 2 an error (then
# nothing is written).
# Runs under Windows PowerShell 5.1 and PowerShell 7. ASCII only: 5.1 reads a script without BOM as ANSI.
param(
    [Parameter(Mandatory = $true)]
    [string]$Catalog,
    [string]$Previous,
    # Default ships/ next to tools/, set below: Windows PowerShell 5.1 leaves $PSScriptRoot empty in parameter defaults.
    [string]$ShipsFolder,
    # Default outlines/ship-outlines.json next to tools/, for the same reason.
    [string]$Outlines,
    [string]$ModelMap,
    [switch]$Create
)
$ErrorActionPreference = 'Stop'
if (-not $ShipsFolder) {
    $ShipsFolder = Join-Path (Split-Path $PSScriptRoot -Parent) 'ships'
}
if (-not $Outlines) {
    $Outlines = Join-Path (Split-Path $PSScriptRoot -Parent) 'outlines\ship-outlines.json'
}
. (Join-Path $PSScriptRoot 'ShipJson.ps1')

$Cell = 1.25
# Below this a grid counts as straight: the Hull A's plates lean 0.6 degrees, the smallest real angle is 14 (Prowler).
$SnapDegrees = 1.0
# A rack rounded by more than this (sum over the axes, in cells) is listed in the rounding table.
$RoundingNote = 0.25
$WalkwayMaxCrate = 2
$IdPattern = '^[a-z0-9]+(_[a-z0-9]+)*$'
$Invariant = [Globalization.CultureInfo]::InvariantCulture
$SourcePrefix = 'cryon.rocks (Motherlode)'
$GameNote = 'rack positions from game files (Motherlode)'
$GameDoorNote = '; door limits from game files (Motherlode)'
# Phrases of the retired Fleetyards import (D94) that the game files make wrong, replaced word for word; any other text is hand work.
$NoteReplacements = [ordered]@{
    'rack positions from Fleetyards overlap; arrangement generated; grids from Fleetyards'                = $GameNote
    'rack positions from Fleetyards; port/starboard from Fleetyards x sign, not checked in game'          = $GameNote
    'rack arrangement generated; grids from Fleetyards'                                                 = $GameNote
    "; stackAxis set by hand after the Hull A's spindle pattern, not from Fleetyards"                     = ''
    "; stackAxis fills Fleetyards' box with the grid kept (K5.2, D78): level arms z, upright arms y; radial or along the arm not checked in game" = ''
}
# Catalog names whose id isn't the name in snake_case.
$IdOverrides = @{
    'Mercury'                  = 'mercury_star_runner'
    'Argo Mole Carbon Edition' = 'mole_carbon_edition'
    'Argo Mole Talus Edition'  = 'mole_talus_edition'
}
# Display names that aren't "<manufacturer> <catalog name>".
$NameOverrides = @{
    'mole_carbon_edition' = 'ARGO MOLE Carbon Edition'
    'mole_talus_edition'  = 'ARGO MOLE Talus Edition'
}
# Variants without a hologram of their own: they show their base ship's (modelKey, holoOrigin, outlineOrigin).
$Variants = @{
    'mole_carbon_edition'        = 'mole'
    'mole_talus_edition'         = 'mole'
    'carrack_expedition'         = 'carrack'
    'carrack_expedition_w_c8x'   = 'carrack'
    'carrack_w_c8x'              = 'carrack'
    'mustang_alpha_vindicator'   = 'mustang_alpha'
    'valkyrie_liberator_edition' = 'valkyrie'
}
$Permutations = @(@(0, 1, 2), @(0, 2, 1), @(1, 0, 2), @(1, 2, 0), @(2, 0, 1), @(2, 1, 0))
$EvenPermutations = @('012', '120', '201')

function Format-Number([double]$Value, [string]$Format = '0.##') {
    if ([math]::Abs($Value) -lt 0.005) { $Value = 0 }
    $Value.ToString($Format, $Invariant)
}

function Get-ShipId([string]$Name) {
    if ($IdOverrides.ContainsKey($Name)) { return $IdOverrides[$Name] }
    $id = ($Name.ToLowerInvariant() -replace '[^a-z0-9]+', '_').Trim('_')
    if ($id -cnotmatch $IdPattern) { throw "Catalog ship '$Name': id '$id' is not lowercase snake_case." }
    $id
}

# One catalog grid in ship cells. Game axes x starboard, y bow, z up, metres; ship axes x bow, y port, z up, so ship =
# (game y, -game x, game z). The grid's own frame as Motherlode's viewer draws it: size[0] and size[1] centred on the
# translation, size[2] from 0 along the third column of the 3x4 matrix (row by row, translation in elements 3, 7, 11).
function ConvertFrom-Grid([object]$Grid, [string]$Where) {
    $size = @($Grid.size | ForEach-Object { [int]$_ })
    if ($size.Count -ne 3 -or @($size | Where-Object { $_ -le 0 }).Count) { throw "${Where}: size must be three whole numbers above 0." }
    $scu = $size[0] * $size[1] * $size[2]
    if ([int]$Grid.scu -ne $scu) { throw "${Where}: size $($size -join 'x') holds $scu SCU, but scu is $($Grid.scu)." }
    if ($CrateSizes -notcontains [int]$Grid.maxContainerScu) { throw "${Where}: maxContainerScu $($Grid.maxContainerScu) is not a crate size." }
    $result = [pscustomobject]@{
        Where = $Where; Size = $size; Scu = $scu; MaxContainer = [int]$Grid.maxContainerScu
        Smallest = [int](@($Grid.containerSizes | ForEach-Object { [int]$_ }) | Measure-Object -Minimum).Minimum
        Turned = $false; Placed = $null -ne $Grid.transform
        Key = (@($size | Sort-Object) -join 'x'); OrderKey = ''
    }
    if (-not $result.Placed) { return $result }
    $t = @($Grid.transform | ForEach-Object { [double]$_ })
    if ($t.Count -ne 12) { throw "${Where}: transform must have 12 numbers (3x4, row by row), has $($t.Count)." }
    $result.OrderKey = '{0}@{1},{2},{3}' -f ($size -join 'x'), (Format-Number $t[3] '0.0'), (Format-Number $t[7] '0.0'), (Format-Number $t[11] '0.0')
    $cols = @(foreach ($c in 0, 1, 2) { , @($t[4 + $c], (-$t[$c]), $t[8 + $c]) })
    $translation = @($t[7], (-$t[3]), $t[11])

    # The straight frame: each grid axis on the ship axis it lies nearest to, as a whole (best permutation).
    $best = $null; $bestScore = -1.0
    foreach ($p in $Permutations) {
        $score = [math]::Abs($cols[0][$p[0]]) + [math]::Abs($cols[1][$p[1]]) + [math]::Abs($cols[2][$p[2]])
        if ($score -gt $bestScore + 1e-12) { $best = $p; $bestScore = $score }
    }
    $signs = @(foreach ($c in 0, 1, 2) { if ($cols[$c][$best[$c]] -lt 0) { -1 } else { 1 } })
    $even = $EvenPermutations -contains ($best -join '')
    if (($signs[0] * $signs[1] * $signs[2] -eq 1) -ne $even) {
        $signs[0] = -$signs[0]   # a box doesn't change when one floor axis flips; the frame turns proper again
    }
    $angle = 0.0
    foreach ($c in 0, 1, 2) {
        $a = [math]::Acos([math]::Min(1.0, [math]::Abs($cols[$c][$best[$c]]))) * 180 / [math]::PI
        if ($a -gt $angle) { $angle = $a }
    }

    $half = $size[2] * $Cell / 2
    $centre = @(foreach ($i in 0, 1, 2) { ($translation[$i] + $cols[2][$i] * $half) / $Cell })
    $extent = @(0, 0, 0)
    foreach ($c in 0, 1, 2) { $extent[$best[$c]] = $size[$c] }
    $result | Add-Member -NotePropertyMembers @{
        Centre = $centre; Extent = $extent; Angle = $angle
        Min = @(foreach ($i in 0, 1, 2) { $centre[$i] - $extent[$i] / 2.0 })
        StackAxis = $(if ($signs[2] -gt 0) { '+' } else { '-' }) + 'xyz'[$best[2]]
        Rotation = $null
    }
    if ($angle -ge $SnapDegrees) {
        # R = R_ship * S^T: column j of R is sign_c * column c where grid axis c lies on ship axis j.
        $r = @(@(0.0, 0.0, 0.0), @(0.0, 0.0, 0.0), @(0.0, 0.0, 0.0))
        foreach ($c in 0, 1, 2) {
            foreach ($i in 0, 1, 2) { $r[$i][$best[$c]] = $signs[$c] * $cols[$c][$i] }
        }
        # R = Rz(yaw) * Ry(pitch) * Rx(roll), as RackTransform in the App (D91).
        $pitch = [math]::Asin([math]::Max(-1.0, [math]::Min(1.0, -$r[2][0])))
        $roll = [math]::Atan2($r[2][1], $r[2][2])
        $yaw = [math]::Atan2($r[1][0], $r[0][0])
        $result.Rotation = '[{0}, {1}, {2}]' -f (Format-Number ($yaw * 180 / [math]::PI)), (Format-Number ($pitch * 180 / [math]::PI)), (Format-Number ($roll * 180 / [math]::PI))
        $result.Turned = $true
    }
    $result
}

# The fraction s in [0, 1) that rounds these values best: the largest gap between their fractions lies across the halves.
function Get-Shift([double[]]$Values) {
    $f = @($Values | ForEach-Object { $x = [math]::Round($_ - [math]::Floor($_), 9); if ($x -ge 1) { 0.0 } else { $x } } | Sort-Object)
    $n = $f.Count
    $gapAt = $n - 1; $gap = $f[0] + 1 - $f[$n - 1]
    for ($j = 0; $j -lt $n - 1; $j++) {
        if ($f[$j + 1] - $f[$j] -gt $gap + 1e-12) { $gap = $f[$j + 1] - $f[$j]; $gapAt = $j }
    }
    $start = $f[($gapAt + 1) % $n]
    $end = if ($gapAt -eq $n - 1) { $f[$n - 1] } else { $f[$gapAt] + 1 }
    $middle = ($start + $end) / 2
    $s = [math]::Round(-$middle - [math]::Floor(-$middle), 9)
    if ($s -ge 1) { 0.0 } else { $s }
}

# Port/starboard and upper/lower of each placed box, by its middle against the middle of all boxes, doubled to stay whole
# (as the retired Fleetyards import did, D78). A box on the middle has no side, none a level where boxes lie wholly above and below it.
function Get-Positions([object[]]$Boxes) {
    $yBoth = (@($Boxes | ForEach-Object { $_.Min[1] }) | Measure-Object -Minimum).Minimum + (@($Boxes | ForEach-Object { $_.Max[1] }) | Measure-Object -Maximum).Maximum
    $zBoth = (@($Boxes | ForEach-Object { $_.Min[2] }) | Measure-Object -Minimum).Minimum + (@($Boxes | ForEach-Object { $_.Max[2] }) | Measure-Object -Maximum).Maximum
    $above = @($Boxes | Where-Object { 2 * $_.Min[2] -ge $zBoth }).Count -gt 0
    $below = @($Boxes | Where-Object { 2 * $_.Max[2] -le $zBoth }).Count -gt 0
    @(foreach ($b in $Boxes) {
        $y = $b.Min[1] + $b.Max[1]; $z = $b.Min[2] + $b.Max[2]
        if ($y -eq $yBoth -or ($z -eq $zBoth -and $above -and $below)) { '' }
        else { '{0}/{1}' -f $(if ($y -gt $yBoth) { 'port' } else { 'starboard' }), $(if ($z -gt $zBoth) { 'upper' } else { 'lower' }) }
    })
}

# Bow first, then port, then top: the order of grids that a file can't tell apart otherwise.
function Get-GeometricOrder([object[]]$Boxes, [int[]]$Indices) {
    @($Indices | Sort-Object { - ($Boxes[$_].Min[0] + $Boxes[$_].Max[0]) }, { - ($Boxes[$_].Min[1] + $Boxes[$_].Max[1]) }, { - ($Boxes[$_].Min[2] + $Boxes[$_].Max[2]) })
}

# Which grid each template rack takes: same cells (any orientation), then the rack's position, then bow-to-stern in the
# template's order. Returns grid index per rack, or the reason it can't.
function Get-Assignment([object[]]$Racks, [object[]]$Grids, [object[]]$Boxes, [string[]]$Positions) {
    $assignment = @(-1) * $Racks.Count
    $keys = @($Racks | ForEach-Object { (@([int]$_.grid.length, [int]$_.grid.width, [int]$_.grid.stack) | Sort-Object) -join 'x' })
    foreach ($key in @($keys + @($Grids | ForEach-Object { $_.Key }) | Sort-Object -Unique)) {
        $rackIdx = @(for ($i = 0; $i -lt $Racks.Count; $i++) { if ($keys[$i] -eq $key) { $i } })
        $gridIdx = @(for ($g = 0; $g -lt $Grids.Count; $g++) { if ($Grids[$g].Key -eq $key) { $g } })
        if ($rackIdx.Count -ne $gridIdx.Count) {
            return "rack set differs: $($rackIdx.Count) racks, $($gridIdx.Count) grids of $key cells"
        }
        $free = [System.Collections.Generic.List[int]]::new()
        foreach ($g in (Get-GeometricOrder $Boxes $gridIdx)) { $free.Add($g) }
        foreach ($i in $rackIdx) {
            if ($null -eq $Racks[$i].position) { continue }
            $wanted = '{0}/{1}' -f $Racks[$i].position.side, $Racks[$i].position.level
            $match = @($free | Where-Object { $Positions[$_] -eq $wanted })
            if ($match.Count -eq 0) { return "$($Racks[$i].id): no grid lies $wanted" }
            $assignment[$i] = $match[0]; [void]$free.Remove($match[0])
        }
        foreach ($i in $rackIdx) {
            if ($assignment[$i] -ge 0) { continue }
            $assignment[$i] = $free[0]; $free.RemoveAt(0)
        }
    }
    , $assignment
}

# An anchor in cells with 4 decimals (at most 0.06 mm off), never "-0".
function Format-Anchor([double]$Value) {
    $r = [math]::Round($Value, 4)
    if ($r -eq 0) { $r = 0.0 }
    $r.ToString('0.####', $Invariant)
}

# Where hologram and outline lie when both come from the ship's model (D93): the model's origin at s, the outline on the
# model's floor, at true size.
function Get-AnchorFields([double[]]$Shift, [double]$FloorM) {
    $holo = @($Shift | ForEach-Object { Format-Anchor $_ }) -join ', '
    $outline = @((Format-Anchor $Shift[0]), (Format-Anchor $Shift[1]), (Format-Anchor ($Shift[2] + $FloorM / $Cell))) -join ', '
    @("  ""holoOrigin"": [$holo],", "  ""outlineOrigin"": [$outline],")
}

function Convert-Notes([string]$Notes) {
    foreach ($old in $NoteReplacements.Keys) {
        $Notes = $Notes.Replace($old, $NoteReplacements[$old])
    }
    $Notes
}

# One ship: the file to write (or why not) and the lines for the report.
function Convert-Ship([object]$Entry, [string]$Id, [object]$Context) {
    $grids = @(for ($g = 0; $g -lt @($Entry.grids).Count; $g++) { ConvertFrom-Grid @($Entry.grids)[$g] "$($Entry.name) grid $($g + 1) ($(@($Entry.grids)[$g].name))" })
    $out = [pscustomobject]@{ Id = $Id; Status = ''; Detail = ''; Text = $null; Lines = [System.Collections.Generic.List[string]]::new(); Module = $null
        ClassName = [string]$Entry.className; Slug = $null; Shift = $null; AnchorsKept = $false }
    $path = Join-Path $ShipsFolder "$Id.json"
    $exists = Test-Path -LiteralPath $path -PathType Leaf
    if (@($grids | Where-Object { -not $_.Placed }).Count) {
        $out.Status = 'kept'; $out.Detail = 'a grid without transform, stays as it is (Fleetyards)'
        return $out
    }
    if (-not $exists -and -not $Create) {
        $out.Status = 'new'; $out.Detail = "$($grids.Count) grids, $(($grids | Measure-Object Scu -Sum).Sum) SCU - not written, run with -Create"
        return $out
    }

    # Zero point: model origin plus s per axis.
    $shift = @(foreach ($a in 0, 1, 2) { Get-Shift ([double[]]@($grids | ForEach-Object { $_.Min[$a] })) })
    $out.Shift = $shift
    $boxes = @(foreach ($g in $grids) {
        $origin = @(foreach ($a in 0, 1, 2) { [int][math]::Floor($g.Min[$a] + $shift[$a] + 0.5) })
        [pscustomobject]@{ Min = $origin; Max = @(foreach ($a in 0, 1, 2) { $origin[$a] + $g.Extent[$a] }); Error = @(foreach ($a in 0, 1, 2) { $g.Min[$a] + $shift[$a] - $origin[$a] }) }
    })
    $positions = Get-Positions $boxes

    # The template: the ship's own file, else its base ship's, else none (racks in bow-to-stern order, walkways last).
    $text = $null; $file = $null; $baseId = $Variants[$Id]; $baseText = $null; $base = $null
    if ($exists) {
        $text = Get-Content -LiteralPath $path -Raw -Encoding UTF8
        $file = $text | ConvertFrom-Json
    }
    if ($baseId -and (Test-Path -LiteralPath (Join-Path $ShipsFolder "$baseId.json"))) {
        $baseText = Get-Content -LiteralPath (Join-Path $ShipsFolder "$baseId.json") -Raw -Encoding UTF8
        $base = $baseText | ConvertFrom-Json
    }
    # @() outside: an if hands a one-element array on as its element, and Windows PowerShell 5.1 gives a single object no Count.
    $template = @(if ($file) { $file.racks } elseif ($base) { $base.racks })
    if ($template.Count) {
        $assignment = Get-Assignment $template $grids $boxes $positions
        if ($assignment -is [string]) {
            if ($file) { $out.Status = 'handcheck'; $out.Detail = $assignment; return $out }
            $template = @()   # a base ship with other grids: no template
        }
    }
    if (-not $template.Count) {
        $all = @(0..($grids.Count - 1))
        $big = @($all | Where-Object {
            $frame = Get-Frame $grids[$_].StackAxis
            $fits = Get-GridCrate @($grids[$_].Extent[$frame[0]], $grids[$_].Extent[$frame[1]], $grids[$_].Extent[$frame[2]])
            [math]::Min($grids[$_].MaxContainer, $fits) -gt $WalkwayMaxCrate
        })
        $small = @($all | Where-Object { $big -notcontains $_ })
        $order = @(Get-GeometricOrder $boxes $big) + @(Get-GeometricOrder $boxes $small)
        $template = @(for ($i = 0; $i -lt $order.Count; $i++) { [pscustomobject]@{ id = "rack-$i"; position = $null; stackAxis = $null; grid = $null; origin = $null; maxCrateSize = $null; minCrateSize = $null } })
        $assignment = $order
    }

    # K1: straight grids that share volume aren't laid by the script.
    $straight = @(for ($i = 0; $i -lt $template.Count; $i++) { if (-not $grids[$assignment[$i]].Turned) { $i } })
    $pairs = Get-Overlaps @($straight | ForEach-Object { $boxes[$assignment[$_]] })
    if ($pairs.Count) {
        $names = @($pairs | ForEach-Object { $ab = $_ -split '/'; '{0}/{1}' -f $template[$straight[[int]$ab[0]]].id, $template[$straight[[int]$ab[1]]].id })
        $out.Status = 'handcheck'; $out.Detail = "grids overlap: $($names -join ', ')"
        return $out
    }

    $doorFromGame = $false
    $rotations = @{}
    $racks = @(for ($i = 0; $i -lt $template.Count; $i++) {
        $t = $template[$i]; $g = $grids[$assignment[$i]]; $b = $boxes[$assignment[$i]]
        $frame = Get-Frame $g.StackAxis
        $grid = [pscustomobject]@{ length = $g.Extent[$frame[0]]; width = $g.Extent[$frame[1]]; stack = $g.Extent[$frame[2]] }
        $fits = Get-GridCrate @($grid.length, $grid.width, $grid.stack)
        $hand = if ($null -ne $t.maxCrateSize) { [int]$t.maxCrateSize } else { $fits }
        $limit = [math]::Min([math]::Min($g.MaxContainer, $hand), $fits)
        $limit = (@($CrateSizes | Where-Object { $_ -le $limit }) | Measure-Object -Maximum).Maximum
        if ($limit -lt $hand) { $doorFromGame = $true }
        $min = if ($g.Smallest -gt 1) { $g.Smallest } elseif ($null -ne $t.minCrateSize) { [int]$t.minCrateSize } else { $null }
        if ($null -ne $min -and $min -gt $limit) { throw "$($g.Where): smallest crate $min is above the largest it takes ($limit)." }
        if ($g.Turned) { $rotations[[string]$t.id] = $g.Rotation }

        $e = $b.Error; $sum = [math]::Abs($e[0]) + [math]::Abs($e[1]) + [math]::Abs($e[2])
        $out.Lines.Add(('round    {0,-28} {1,-8} e=[{2}, {3}, {4}] cells, sum {5} cells = {6} m{7}' -f $Id, $t.id,
            (Format-Number $e[0] '0.00'), (Format-Number $e[1] '0.00'), (Format-Number $e[2] '0.00'), (Format-Number $sum '0.00'), (Format-Number ($sum * $Cell) '0.00'),
            $(if ($sum -gt $RoundingNote) { '  *' } else { '' })))
        $out.Lines.Add(('axis     {0,-28} {1,-8} {2} -> {3}{4}' -f $Id, $t.id, $(if ($t.stackAxis) { $t.stackAxis } else { '(new)' }), $g.StackAxis,
            $(if ($t.stackAxis -and $t.stackAxis -ne $g.StackAxis) { '  *' } else { '' })))
        if ($t.grid -and (Get-GridText $t.grid) -ne (Get-GridText $grid)) {
            $out.Lines.Add(('grid     {0,-28} {1,-8} {2} -> {3}' -f $Id, $t.id, (Get-GridText $t.grid), (Get-GridText $grid)))
        }
        $newMax = if ($limit -lt $fits) { $limit } else { $null }
        if ($file -and ("$($t.maxCrateSize)" -ne "$newMax" -or "$($t.minCrateSize)" -ne "$min")) {
            $out.Lines.Add(('crate    {0,-28} {1,-8} max {2} -> {3}, min {4} -> {5}' -f $Id, $t.id, $(if ($null -ne $t.maxCrateSize) { $t.maxCrateSize } else { '-' }),
                $(if ($null -ne $newMax) { $newMax } else { '-' }), $(if ($null -ne $t.minCrateSize) { $t.minCrateSize } else { '-' }), $(if ($null -ne $min) { $min } else { '-' })))
        }
        if ($g.Turned) {
            $out.Lines.Add(('rotation {0,-28} {1,-8} {2} ({3} degrees off the axes)' -f $Id, $t.id, $g.Rotation, (Format-Number $g.Angle '0.0')))
        }
        [pscustomobject]@{
            id = [string]$t.id; capacityScu = $g.Scu; grid = $grid; position = $t.position; origin = $b.Min
            stackAxis = $g.StackAxis; maxCrateSize = $newMax; minCrateSize = $min
        }
    })
    $out.Lines.Insert(0, ('shift    {0,-28} s=[{1}, {2}, {3}] cells' -f $Id, (Format-Number $shift[0] '0.000'), (Format-Number $shift[1] '0.000'), (Format-Number $shift[2] '0.000')))

    $total = ($racks | Measure-Object capacityScu -Sum).Sum
    if ($total -ne [int]$Entry.cargoScu) {
        $out.Status = 'handcheck'; $out.Detail = "grids hold $total SCU, catalog cargoScu says $($Entry.cargoScu)"
        return $out
    }

    if ($file) {
        $notes = Convert-Notes ([string]$file.notes)
        $hand = Get-HandFields $text
        $name = $file.name; $manufacturer = $file.manufacturer; $slug = Get-ModelKey $file
    }
    elseif ($base) {
        $notes = "$GameNote; hologram and outline of the $($base.name)"
        $hand = Get-HandFields $baseText
        $name = if ($NameOverrides.ContainsKey($Id)) { $NameOverrides[$Id] } else { "$($Entry.manufacturer) $($Entry.name)" }
        $manufacturer = $Entry.manufacturer; $slug = Get-ModelKey $base
    }
    else {
        # No hologram: the anchors at the racks' middle, as docs/ship-format.md says for a ship set up by hand.
        $notes = $GameNote
        $middle = @(foreach ($a in 0, 1, 2) {
            $low = (@($boxes | ForEach-Object { $_.Min[$a] }) | Measure-Object -Minimum).Minimum
            $high = (@($boxes | ForEach-Object { $_.Max[$a] }) | Measure-Object -Maximum).Maximum
            Format-Number (($low + $high) / 2.0) 'R'
        })
        $hand = @("  ""holoOrigin"": [$($middle -join ', ')],", "  ""outlineOrigin"": [$($middle -join ', ')],")
        $name = if ($NameOverrides.ContainsKey($Id)) { $NameOverrides[$Id] } else { "$($Entry.manufacturer) $($Entry.name)" }
        $manufacturer = $Entry.manufacturer; $slug = $null
    }
    $out.Slug = $slug
    if ($out.ClassName -and $slug) {
        if ($Floors.ContainsKey($slug)) {
            $hand = Get-AnchorFields $shift $Floors[$slug]
        }
        else {
            $out.AnchorsKept = $true
            $out.Lines.Add(('anchors  {0,-28} kept: no floorM for {1} in {2}, the outline is not from the model' -f $Id, $slug, (Split-Path $Outlines -Leaf)))
        }
    }
    if ($doorFromGame -and -not $notes.Contains($GameDoorNote)) { $notes += $GameDoorNote }

    $ship = [pscustomobject]@{
        id = $Id; name = $name; manufacturer = $manufacturer; modelKey = $slug
        source = "$SourcePrefix $($Context.GameBuild), $($Context.UpdatedAt)"; notes = $notes
        totalScu = $total; racks = $racks
    }
    $out.Text = Format-Ship $ship $hand $rotations
    $summary = '{0} racks, {1} SCU{2}' -f $racks.Count, $total, $(if ($rotations.Count) { ", $($rotations.Count) turned" } else { '' })
    if (-not $file) {
        $out.Status = 'created'; $out.Detail = $summary
        return $out
    }
    if ($text.Replace("`r`n", "`n") -eq $out.Text) {
        $out.Status = 'same'; $out.Detail = $summary
        return $out
    }
    $out.Status = 'updated'
    $moved = 0
    foreach ($r in $racks) {
        $old = @($file.racks | Where-Object { [string]$_.id -eq $r.id })[0]
        for ($a = 0; $a -lt 3; $a++) { $moved = [math]::Max($moved, [math]::Abs([int]$old.origin[$a] - $r.origin[$a])) }
    }
    $out.Detail = "$summary, racks moved up to $moved cells"

    # A module lies relative to its ship's racks: it moves by what Rack 1 moved.
    $first = @($file.racks)[0]
    $delta = @(foreach ($a in 0, 1, 2) { $racks[0].origin[$a] - [int]$first.origin[$a] })
    if ($delta[0] -or $delta[1] -or $delta[2]) {
        $out.Module = $delta
    }
    $out
}

# Modules for a ship (ships/modules/*.json naming it), with their origins moved by Delta. Only the numbers change, the
# file's line ends and everything else stay.
function Move-Modules([string]$ShipId, [int[]]$Delta, [hashtable]$Writes, [System.Collections.Generic.List[string]]$Report) {
    $folder = Join-Path $ShipsFolder 'modules'
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { return }
    foreach ($path in @(Get-ChildItem -LiteralPath $folder -Filter '*.json' | Sort-Object Name | ForEach-Object { $_.FullName })) {
        $text = Get-Content -LiteralPath $path -Raw -Encoding UTF8
        $module = $text | ConvertFrom-Json
        if (@($module.ships) -notcontains $ShipId) { continue }
        if (@($module.ships).Count -gt 1) { throw "Module $($module.id) belongs to several ships; move it by hand (Rack 1 of $ShipId moved by [$($Delta -join ', ')])." }
        $moved = [regex]::Replace($text, '"origin"\s*:\s*\[\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\]', {
            param($m)
            '"origin": [{0}, {1}, {2}]' -f ([int]$m.Groups[1].Value + $Delta[0]), ([int]$m.Groups[2].Value + $Delta[1]), ([int]$m.Groups[3].Value + $Delta[2])
        }.GetNewClosure())
        $Writes[$path] = $moved
        $Report.Add(('module   {0,-28} origins moved by [{1}] with {2} Rack 1' -f $module.id, ($Delta -join ', '), $ShipId))
    }
}

function Read-Catalog([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Catalog not found: $Path" }
    $parsed = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $parsed.gameBuild -or -not $parsed.updatedAt) { throw "${Path}: gameBuild and updatedAt are required." }
    $parsed
}

try {
    if (-not (Test-Path -LiteralPath $ShipsFolder -PathType Container)) { throw "Ships folder not found: $ShipsFolder" }
    # Not $catalog: names are case-insensitive, and the [string] parameter $Catalog would turn the object into text.
    $data = Read-Catalog $Catalog
    $context = [pscustomobject]@{ GameBuild = [string]$data.gameBuild; UpdatedAt = [string]$data.updatedAt }
    $entries = [ordered]@{}
    foreach ($entry in @($data.ships)) {
        $id = Get-ShipId ([string]$entry.name)
        if ($entries.Contains($id)) { throw "Ship id '$id' comes from both '$($entries[$id].name)' and '$($entry.name)'." }
        $entries[$id] = $entry
    }
    # The model's floor per slug, in metres below its origin: only outlines made from the model carry it (D93).
    if (-not (Test-Path -LiteralPath $Outlines -PathType Leaf)) { throw "Outlines not found: $Outlines" }
    $Floors = @{}
    foreach ($o in @((Get-Content -LiteralPath $Outlines -Raw -Encoding UTF8 | ConvertFrom-Json).ships)) {
        if ($null -ne $o.floorM) { $Floors[[string]$o.slug] = [double]$o.floorM }
    }
    $previousOrder = @{}
    if ($Previous) {
        foreach ($entry in @((Read-Catalog $Previous).ships)) {
            $previousOrder[(Get-ShipId ([string]$entry.name))] = @($entry.grids | ForEach-Object { if ($null -ne $_.transform) { (ConvertFrom-Grid $_ 'previous').OrderKey } else { '' } })
        }
    }

    $ids = [string[]]@($entries.Keys)
    [Array]::Sort($ids, [StringComparer]::Ordinal)
    $statusLines = [System.Collections.Generic.List[string]]::new()
    $detailLines = [System.Collections.Generic.List[string]]::new()
    $writes = @{}
    $counts = [ordered]@{ same = 0; updated = 0; created = 0; new = 0; kept = 0; handcheck = 0; order = 0 }
    $anchorsKept = 0
    $mapLines = [System.Collections.Generic.List[string]]::new()
    foreach ($id in $ids) {
        $result = Convert-Ship $entries[$id] $id $context
        if ($previousOrder.ContainsKey($id) -and $result.Status -in 'updated', 'created', 'same') {
            $now = @($entries[$id].grids | ForEach-Object { if ($null -ne $_.transform) { (ConvertFrom-Grid $_ 'order').OrderKey } else { '' } })
            $before = $previousOrder[$id]
            if (($now -join ' ') -ne ($before -join ' ') -and ((@($now | Sort-Object) -join ' ') -eq (@($before | Sort-Object) -join ' '))) {
                $result.Status = 'order'; $result.Detail = 'same grids as the previous catalog in another order: which rack is which? (not written)'
                $result.Text = $null; $result.Module = $null
            }
        }
        if ($result.Status -in 'updated', 'created') {
            $writes[(Join-Path $ShipsFolder "$id.json")] = $result.Text
            if ($result.Module) { Move-Modules $id $result.Module $writes $detailLines }
        }
        $counts[$result.Status]++
        $statusLines.Add(('{0,-10} {1,-30} {2}' -f $result.Status, $id, $result.Detail))
        foreach ($line in $result.Lines) { $detailLines.Add($line) }
        if ($result.Status -in 'updated', 'created', 'same') {
            if ($result.AnchorsKept) { $anchorsKept++ }
            if ($result.ClassName) {
                $mapLines.Add((@($id, $result.ClassName, [string]$result.Slug) + @($result.Shift | ForEach-Object { $_.ToString('0.#########', $Invariant) })) -join ',')
            }
        }
    }
    foreach ($path in @(Get-ChildItem -LiteralPath $ShipsFolder -Filter '*.json' | Sort-Object Name)) {
        if (-not $entries.Contains($path.BaseName)) {
            $statusLines.Add(('{0,-10} {1,-30} {2}' -f 'absent', $path.BaseName, 'not in the catalog, stays as it is'))
        }
    }

    # Written only after every ship converted: an error above leaves the folder as it was.
    $utf8 = New-Object System.Text.UTF8Encoding $false
    foreach ($path in $writes.Keys) {
        [System.IO.File]::WriteAllText($path, $writes[$path], $utf8)
    }
    if ($ModelMap) {
        # $ids is sorted ordinally, so the map is too; LF, no BOM, for the pipeline on any checkout.
        [System.IO.File]::WriteAllText($ModelMap, ((@('id,className,modelKey,sx,sy,sz') + $mapLines) -join "`n") + "`n", $utf8)
    }

    '{0} {1}, {2}: {3} ships' -f $SourcePrefix, $context.GameBuild, $context.UpdatedAt, $entries.Count
    $statusLines
    ''
    $detailLines
    ''
    (@($counts.Keys | ForEach-Object { "$($counts[$_]) $_" }) + "$anchorsKept anchors kept") -join ', '
    if (-not $Previous) { 'no previous catalog given: grid order not compared' }

    if ($counts.new -gt 0 -or $counts.handcheck -gt 0 -or $counts.order -gt 0 -or $anchorsKept -gt 0) {
        exit 1
    }
    exit 0
}
catch {
    [Console]::Error.WriteLine("import-motherlode: $($_.Exception.Message)")
    exit 2
}
