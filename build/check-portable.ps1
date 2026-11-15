# Checks the Velopack portable ZIP that publish.ps1 packed (T11.3, R12; S11.0 G3 as a lasting guard): the layout a hauler
# unpacks, every file of the publish folder under current\, the PDBs, and the version the package and the Cli report.
# publish.ps1 calls it; on its own it checks a ZIP someone changed by hand (that is how its failures were provoked).
# Usage: ./build/check-portable.ps1 -Zip <portable ZIP> -PublishDir <out\publish\SC-CargoPlan> -Version <x.y.z[-pre]>
# Throws with the list of what is wrong; nothing written outside out\check\.
param(
    [Parameter(Mandatory)][string]$Zip,
    [Parameter(Mandatory)][string]$PublishDir,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$check = Join-Path $root 'out\check'
if (Test-Path $check) {
    Remove-Item $check -Recurse -Force
}

# Unpacked as a hauler would; the checks below then read plain files, and the Cli can run.
# Source: https://learn.microsoft.com/dotnet/api/system.io.compression.zipfile.extracttodirectory
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($Zip, $check)
$current = Join-Path $check 'current'
$problems = [System.Collections.Generic.List[string]]::new()

# The portable root (S11.0): the stub a hauler starts (its name comes from --packTitle), Update.exe, and the app in current\.
foreach ($file in 'SC-CargoPlan.exe', 'Update.exe', 'current\CargoPlan.App.exe', 'current\CargoPlan.Cli.exe', 'current\sq.version') {
    if (-not (Test-Path (Join-Path $check $file) -PathType Leaf)) {
        $problems.Add("$file is missing")
    }
}

# Without ships/ the overlay doesn't start (D56); without outlines/ it shows the bow wedge, and the outlines must not ship
# without their licence note (D55). Without ships/modules/ a module's ship is missing from the list (K5.3). The contract OCR
# (D99) needs its four model files, the two native libraries and its licence note. MIT asks for the licence with every copy.
$required = 'ships\hull_a.json', 'ships\modules\ts_module.json', 'outlines\ship-outlines.json', 'outlines\NOTICE.md',
    'models\v5\ch_PP-OCRv5_mobile_det.onnx', 'models\v5\ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx',
    'models\v5\latin_PP-OCRv5_rec_mobile_infer.onnx', 'models\v5\ppocrv5_latin_dict.txt', 'models\NOTICE.md',
    'onnxruntime.dll', 'libSkiaSharp.dll', 'LICENSE'
foreach ($file in $required) {
    if (-not (Test-Path (Join-Path $current $file) -PathType Leaf)) {
        $problems.Add("current\$file is missing")
    }
}

function Get-RelativeFiles([string]$folder) {
    if (-not (Test-Path $folder)) {
        return @()
    }

    $full = (Resolve-Path $folder).Path
    @(Get-ChildItem $full -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($full.Length + 1) })
}

$published = Get-RelativeFiles $PublishDir
$packed = Get-RelativeFiles $current

# Every ship file of the publish folder, counted on its own because a missing ship is the costliest loss (R12).
$shipsPublished = @($published | Where-Object { $_.StartsWith('ships\') }).Count
$shipsPacked = @($packed | Where-Object { $_.StartsWith('ships\') }).Count
if ($shipsPacked -ne $shipsPublished) {
    $problems.Add("current\ships has $shipsPacked files, the publish folder $shipsPublished")
}

# The whole file set: vpk leaves out createdump.exe (a built-in exclude, kept on purpose) and adds sq.version, its manifest.
# Anything else in one and not the other means vpk packed something we didn't publish, or dropped a file.
$onlyPublished = @($published | Where-Object { $_ -notin $packed -and $_ -ne 'createdump.exe' })
$onlyPacked = @($packed | Where-Object { $_ -notin $published -and $_ -ne 'sq.version' })
if ($onlyPublished.Count -gt 0) {
    $problems.Add("not in current\: $($onlyPublished -join ', ')")
}
if ($onlyPacked.Count -gt 0) {
    $problems.Add("only in current\: $($onlyPacked -join ', ')")
}

# Crash stacks in cargoplan.log keep their line numbers only with the PDBs (D109). vpk leaves them out by default; this count
# is the guard should Velopack change what publish.ps1's --exclude means.
$pdbPublished = @($published | Where-Object { $_.EndsWith('.pdb') }).Count
$pdbPacked = @($packed | Where-Object { $_.EndsWith('.pdb') }).Count
if ($pdbPacked -eq 0 -or $pdbPacked -ne $pdbPublished) {
    $problems.Add("current\ has $pdbPacked .pdb files, the publish folder $pdbPublished")
}

# The package's manifest: packId, version and main exe as publish.ps1 asked for (D109: the packId never changes).
$manifest = Join-Path $current 'sq.version'
if (Test-Path $manifest) {
    $metadata = ([xml](Get-Content $manifest -Raw -Encoding UTF8)).package.metadata
    if ($metadata.id -cne 'CargoPlan') {
        $problems.Add("sq.version has the packId '$($metadata.id)', not CargoPlan")
    }
    if ($metadata.version -cne $Version) {
        $problems.Add("sq.version has the version '$($metadata.version)', not $Version")
    }
    if ($metadata.mainExe -cne 'CargoPlan.App.exe') {
        $problems.Add("sq.version has the main exe '$($metadata.mainExe)', not CargoPlan.App.exe")
    }
}

# The version the build itself carries, as the app and the Cli report it; the SDK appends +<commit> (UpdateService compares
# without it too).
$cli = Join-Path $current 'CargoPlan.Cli.exe'
if (Test-Path $cli) {
    $reported = (& $cli --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        $problems.Add("CargoPlan.Cli.exe --version failed with exit code $LASTEXITCODE")
    }
    elseif (($reported -split '\+')[0] -cne $Version) {
        $problems.Add("CargoPlan.Cli.exe --version says '$reported', not $Version")
    }
}

if ($problems.Count -gt 0) {
    throw "$(Split-Path $Zip -Leaf) failed the layout check:`n- $($problems -join "`n- ")"
}

Write-Output "Layout check: $(Split-Path $Zip -Leaf) passed ($($packed.Count) files in current\, $shipsPacked ship files, $pdbPacked PDBs, version $Version)."
