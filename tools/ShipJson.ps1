# The ship file format as tools/import-motherlode.ps1 reads and writes it; kept apart from the import since the retired
# Fleetyards import shared it (D94, in Git history). Dot-sourced: . (Join-Path $PSScriptRoot 'ShipJson.ps1')
# Runs under Windows PowerShell 5.1 and PowerShell 7. ASCII only: 5.1 reads a script without BOM as ANSI.

$ShipSchemaVersion = 6
$CrateSizes = @(1, 2, 4, 8, 16, 24, 32)
# Crate sizes with their shapes in cells (SCU, length, width, stack), fixed orientation, as CrateSize.Dimensions (D57).
# A list, not an ordered hashtable: its [int] indexer would read a position, not a key.
$CrateShapes = @(@(1, 1, 1, 1), @(2, 2, 1, 1), @(4, 2, 2, 1), @(8, 2, 2, 2), @(16, 4, 2, 2), @(24, 6, 2, 2), @(32, 8, 2, 2))

# The largest crate a grid takes: two horizontal sides and the stack. Crates turn 90 degrees about the stack axis (D71),
# so the longer horizontal side takes the crate's length, whichever axis it lies on.
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

# The pairs of boxes that share volume on all three axes, as "i/k"; touching is fine (as in the loader).
function Get-Overlaps([object[]]$Boxes) {
    $pairs = [System.Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $Boxes.Count; $i++) {
        for ($k = $i + 1; $k -lt $Boxes.Count; $k++) {
            $a = $Boxes[$i]; $b = $Boxes[$k]
            $apart = $false
            for ($axis = 0; $axis -lt 3; $axis++) {
                if ($a.Min[$axis] -ge $b.Max[$axis] -or $b.Min[$axis] -ge $a.Max[$axis]) {
                    $apart = $true
                }
            }
            if (-not $apart) {
                $pairs.Add("$i/$k")
            }
        }
    }
    , $pairs
}

function Format-JsonString([string]$Value) {
    if ($Value -match '[\x00-\x1f]') {
        throw "Control character in '$Value'."
    }
    '"' + $Value.Replace('\', '\\').Replace('"', '\"') + '"'
}

# A parsed ship file's model key: modelKey, or fleetyardsSlug, its name before v0.3.0 (D94). The writer writes modelKey.
function Get-ModelKey([object]$File) {
    if ($null -ne $File.modelKey) { $File.modelKey } else { $File.fleetyardsSlug }
}

# The fields set outside the import (K5.3b, D90), as the file writes them: through a number, Windows PowerShell 5.1
# would drop digits and the picture would change.
function Get-HandFields([string]$Text) {
    @([regex]::Matches($Text, '(?m)^\s*"(holoOffset|holoOrigin|outlineOrigin|outlineScale)"\s*:\s*(\[[^\]]*\]|[^,\s}]+)') |
        ForEach-Object { "  ""$($_.Groups[1].Value)"": $($_.Groups[2].Value)," })
}

# The layout of ships/hull_a.json: two spaces, grid, position and origin on one line, LF, final newline.
function Format-Ship([object]$Ship, [string[]]$HandFields = @(), [hashtable]$Rotations = @{}) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('{')
    $lines.Add("  ""schemaVersion"": $ShipSchemaVersion,")
    foreach ($field in 'id', 'name', 'manufacturer', 'modelKey', 'source', 'notes') {
        if ($null -ne $Ship.$field) {
            $lines.Add("  ""$field"": $(Format-JsonString $Ship.$field),")
        }
    }
    # Kept when an import rewrites the file, also a file written anew: racks don't move the hull (D90).
    foreach ($line in $HandFields) {
        $lines.Add($line)
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
        if ($Rotations.ContainsKey([string]$r.id)) {
            $fields.Add("""rotation"": $($Rotations[[string]$r.id])")
        }
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
