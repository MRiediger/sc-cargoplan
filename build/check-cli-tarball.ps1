# Checks the Linux CLI tarball that publish.ps1 packed (D115, K12): every file of the publish folder under SC-CargoPlan-Cli/,
# the modes Linux needs (CargoPlan.Cli executable, packed on Windows), the licence notes, ship data, models and native
# libraries, and the version the build carries. publish.ps1 calls it; on its own it checks a tarball someone packed by hand
# (that is how its failures were provoked).
# Usage: ./build/check-cli-tarball.ps1 -Tarball <tar.gz> -PublishDir <out\publish\linux\SC-CargoPlan-Cli> -Version <x.y.z[-pre]>
# Throws with the list of what is wrong; nothing written outside out\check-cli\.
param(
    [Parameter(Mandatory)][string]$Tarball,
    [Parameter(Mandatory)][string]$PublishDir,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$check = Join-Path $root 'out\check-cli'
if (Test-Path $check) {
    Remove-Item $check -Recurse -Force
}
New-Item -ItemType Directory $check | Out-Null

# Windows' own bsdtar, as publish.ps1 packs with it; Git's GNU tar may come first on the PATH of a runner.
$tar = Join-Path $env:SystemRoot 'System32\tar.exe'
$top = 'SC-CargoPlan-Cli'
$problems = [System.Collections.Generic.List[string]]::new()

# The listing keeps the modes, which an extraction on Windows loses. A line: mode, links, owner, group, size, date (three
# fields), name; names have no spaces (publish.ps1 refuses them). Directories end in "/".
$modes = @{}
foreach ($line in & $tar -tvzf $Tarball) {
    $fields = $line -split '\s+'
    $name = $fields[-1] -replace '^\./', ''
    if (-not $name.EndsWith('/')) {
        $modes[$name] = $fields[0]
    }
}
if ($LASTEXITCODE -ne 0) {
    throw "Listing $Tarball failed with exit code $LASTEXITCODE."
}

# One folder at the top, so the tarball doesn't spill into the folder it is unpacked in.
$outside = @($modes.Keys | Where-Object { -not $_.StartsWith("$top/") })
if ($outside.Count -gt 0) {
    $problems.Add("outside $top/: $($outside -join ', ')")
}

# Linux starts CargoPlan.Cli only with its execute bit (K12 spike: "Permission denied" after a plain bsdtar on Windows);
# every other file is plain data. createdump is the runtime's crash dump tool, executable as in a Linux publish.
$executables = "$top/CargoPlan.Cli", "$top/createdump"
foreach ($name in $modes.Keys) {
    $want = if ($name -in $executables) { '-rwxr-xr-x' } else { '-rw-r--r--' }
    if ($modes[$name] -cne $want) {
        $problems.Add("$name has the mode $($modes[$name]), not $want")
    }
}

# As check-portable.ps1 asks of the Windows ZIP (D55, D99, D113), with the native libraries' Linux names. MIT asks for the
# licence with every copy; the ship data and the OCR models carry their own notes (not MIT).
$required = 'CargoPlan.Cli', 'CargoPlan.Cli.dll', 'LICENSE',
    'ship-data/manifest.json', 'ship-data/ships/hull_a.json', 'ship-data/ships/modules/ts_module.json',
    'ship-data/outlines.json', 'ship-data/NOTICE.md', 'ship-data/hauling-templates.json', 'ship-data/positions.json',
    'models/v5/ch_PP-OCRv5_mobile_det.onnx', 'models/v5/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx',
    'models/v5/latin_PP-OCRv5_rec_mobile_infer.onnx', 'models/v5/ppocrv5_latin_dict.txt', 'models/NOTICE.md',
    'libonnxruntime.so', 'libSkiaSharp.so'
foreach ($file in $required) {
    if (-not $modes.ContainsKey("$top/$file")) {
        $problems.Add("$top/$file is missing")
    }
}

# The whole file set against the publish folder: anything in one and not the other was dropped or added by the packing.
$full = (Get-Item $PublishDir).FullName
$published = @(Get-ChildItem $full -Recurse -File -Force | ForEach-Object { "$top/" + $_.FullName.Substring($full.Length + 1).Replace('\', '/') })
$packed = @($modes.Keys)
$onlyPublished = @($published | Where-Object { -not $modes.ContainsKey($_) })
$onlyPacked = @($packed | Where-Object { $_ -notin $published })
if ($onlyPublished.Count -gt 0) {
    $problems.Add("not in the tarball: $($onlyPublished -join ', ')")
}
if ($onlyPacked.Count -gt 0) {
    $problems.Add("only in the tarball: $($onlyPacked -join ', ')")
}
$shipsPacked = @($packed | Where-Object { $_.StartsWith("$top/ship-data/ships/") }).Count

# Version and ship data without running Linux code: the version the SDK wrote into the dll (ProductVersion = the
# informational version, "<version>+<commit>"), and the pinned release in the manifest the CLI reads as its baseline (D113,
# D115: on Linux nothing downloads newer data).
# Source: https://learn.microsoft.com/dotnet/api/system.diagnostics.fileversioninfo.productversion
& $tar -xzf $Tarball -C $check "./$top/CargoPlan.Cli.dll" "./$top/ship-data/manifest.json"
if ($LASTEXITCODE -ne 0) {
    $problems.Add("extracting CargoPlan.Cli.dll and the ship data manifest failed with exit code $LASTEXITCODE")
}
$dll = Join-Path $check "$top\CargoPlan.Cli.dll"
if (Test-Path $dll) {
    $reported = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion
    if (($reported -split '\+')[0] -cne $Version) {
        $problems.Add("CargoPlan.Cli.dll has the version '$reported', not $Version")
    }
}
$pin = (Get-Content (Join-Path $root 'build\ship-data.json') -Raw -Encoding UTF8 | ConvertFrom-Json).dataVersion
$manifest = Join-Path $check "$top\ship-data\manifest.json"
if (Test-Path $manifest) {
    $dataVersion = (Get-Content $manifest -Raw -Encoding UTF8 | ConvertFrom-Json).dataVersion
    if ($dataVersion -cne $pin) {
        $problems.Add("ship-data/manifest.json names '$dataVersion', not the pin $pin")
    }
}

if ($problems.Count -gt 0) {
    throw "$(Split-Path $Tarball -Leaf) failed the tarball check:`n- $($problems -join "`n- ")"
}

Write-Output "Tarball check: $(Split-Path $Tarball -Leaf) passed ($($packed.Count) files in $top/, $shipsPacked ship files of $pin, CargoPlan.Cli executable, version $Version)."
