# Publishes the overlay and the CLI into one folder and zips it (D46, D48, D49): self-contained win-x64, one shared
# runtime, no single file, no trimming. The only place the publish settings live; the release workflow calls it too.
# Usage: ./build/publish.ps1 [-Version 0.1.0]
#   → out/publish/SC-CargoPlan/ and out/publish/SC-CargoPlan-v<version>-win-x64.zip with its .sha256
# Without -Version the build keeps the 0.0.0-dev of Directory.Build.props.
param(
    [string]$Version
)
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $root 'out\publish'
# The ZIP's root folder (D49): 260 files loose in the extract folder would bury the EXE.
$output = Join-Path $publish 'SC-CargoPlan'
$common = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $output)
if ($Version) {
    $common += "-p:Version=$Version"
}

# Start empty: files a former build left behind (a removed dependency) must not ship.
if (Test-Path $publish) {
    Remove-Item $publish -Recurse -Force
}

# The App goes last, keep the order. Two of the files both publish differ (checked in T6.2), and the App's must win:
# WindowsBase.dll is WPF's in the App and a 16 KB facade in the CLI's runtime, so the overlay wouldn't start with the
# CLI's; SQLitePCLRaw.provider.e_sqlite3.dll is the same version built for net6.0-windows, which the CLI runs with too.
foreach ($project in 'CargoPlan.Cli', 'CargoPlan.App') {
    dotnet publish (Join-Path $root "src\$project") @common
    # A failing native command doesn't throw in PowerShell, not even with ErrorActionPreference = Stop.
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish $project failed with exit code $LASTEXITCODE."
    }
}

# MIT asks for the licence text with every copy.
Copy-Item (Join-Path $root 'LICENSE') $output

# The ZIP's name carries the version the build used; without -Version it comes from MSBuild, not a second copy here.
$zipVersion = $Version
if (-not $zipVersion) {
    $zipVersion = (dotnet msbuild (Join-Path $root 'src\CargoPlan.App\CargoPlan.App.csproj') -getProperty:Version).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Reading the version failed with exit code $LASTEXITCODE."
    }
}

# Entries one by one with forward slashes, as the ZIP format asks: under Windows PowerShell 5.1 (.NET Framework) both
# Compress-Archive and ZipFile.CreateFromDirectory write backslashes, pwsh 7 (the release runner) doesn't. This way a
# local ZIP is the same as the released one.
# Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.zipfileextensions.createentryfromfile
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zipName = "SC-CargoPlan-v$zipVersion-win-x64.zip"
$zip = Join-Path $publish $zipName
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $output -Recurse -File) {
        $entry = (Split-Path $output -Leaf) + '/' + $file.FullName.Substring($output.Length + 1).Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $file.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $archive.Dispose()
}

# Without ships/ the overlay doesn't start (D56); without outlines/ it shows the bow wedge, and the outlines must not
# ship without their licence note (D55). dotnet publish takes both folders from Core's items (measured in T7.1a), so
# this checks the ZIP the haulers get, not a copy step of its own.
$required = 'SC-CargoPlan/ships/hull_a.json', 'SC-CargoPlan/outlines/ship-outlines.json', 'SC-CargoPlan/outlines/NOTICE.md'
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $missing = @($required | Where-Object { $null -eq $archive.GetEntry($_) })
}
finally {
    $archive.Dispose()
}
if ($missing.Count -gt 0) {
    throw "$zipName has no $($missing -join ', ') - the ships or outlines folder didn't reach the publish folder."
}

# sha256sum format ("<hash>  <name>"), so `sha256sum -c` checks it; Get-FileHash compares case-insensitively.
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText("$zip.sha256", "$hash  $zipName`n")
