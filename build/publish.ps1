# Publishes the overlay and the CLI into one folder (D46, D48): self-contained win-x64, one shared runtime, no single
# file, no trimming. The only place the publish settings live; CI (T6.3) calls this script too.
# Usage: ./build/publish.ps1   → out/publish
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'out\publish'
$common = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $output)

# Start empty: files a former build left behind (a removed dependency) must not ship.
if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
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
