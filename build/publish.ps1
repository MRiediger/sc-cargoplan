# Publishes the overlay and the CLI into one folder (D46, D48, D49) and packs it with Velopack (D109, T11.3): self-contained
# win-x64, one shared runtime, no single file, no trimming. The only place the publish and pack settings live; the release
# workflow and CI call it too, so a local build is the released one (lesson B7).
# Usage: ./build/publish.ps1 -Version 0.1.0 [-DeltaBase github|<path of a full nupkg>]
#   → out/publish/SC-CargoPlan/ (the publish folder), out/releases/ (everything vpk wrote) and out/release/ (what the
#     GitHub release carries: SC-CargoPlan-v<version>-win-x64.zip with its .sha256, the full and delta nupkg,
#     releases.win.json)
# -Version is required: vpk refuses the 0.0.0-dev of Directory.Build.props ("must be >= 0.0.1"), and a package version
# other than the build's would make two versions for one ZIP. Without -DeltaBase there is no delta.
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$DeltaBase
)
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $root 'out\publish'
# The publish folder's name (D49); in the portable ZIP its files sit in current\.
$output = Join-Path $publish 'SC-CargoPlan'
$releases = Join-Path $root 'out\releases'
$release = Join-Path $root 'out\release'
$app = Join-Path $root 'src\CargoPlan.App\CargoPlan.App.csproj'
# The repository UpdateService.RepositoryUrl checks; the delta base comes from its releases.
$repoUrl = 'https://github.com/MRiediger/sc-cargoplan'
$common = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $output, "-p:Version=$Version")

# A warning that also shows in the GitHub Actions summary, for what must not stop a release (R7).
# Source: https://docs.github.com/actions/reference/workflows-and-actions/workflow-commands#setting-a-warning-message
function Write-BuildWarning([string]$message) {
    Write-Warning $message
    if ($env:GITHUB_ACTIONS -eq 'true') {
        Write-Output "::warning::$message"
    }
}

# Start empty: files a former build left behind (a removed dependency, last version's packages) must not ship.
foreach ($folder in $publish, $releases, $release) {
    if (Test-Path $folder) {
        Remove-Item $folder -Recurse -Force
    }
}

# The App goes last, keep the order. A file both publish differs (checked in T6.2), and the App's must win:
# WindowsBase.dll is WPF's in the App and a 16 KB facade in the CLI's runtime, so the overlay wouldn't start with the
# CLI's.
foreach ($project in 'CargoPlan.Cli', 'CargoPlan.App') {
    dotnet publish (Join-Path $root "src\$project") @common
    # A failing native command doesn't throw in PowerShell, not even with ErrorActionPreference = Stop.
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish $project failed with exit code $LASTEXITCODE."
    }
}

# MIT asks for the licence text with every copy.
Copy-Item (Join-Path $root 'LICENSE') $output

# vpk in exactly the version of the Velopack package the app references (R2): Dependabot raises the package, the tool
# follows. Installed into out\tools\ per version, not globally: nothing changes on the machine, CI and local alike.
# Source: https://learn.microsoft.com/visualstudio/msbuild/evaluate-items-and-properties (-getItem)
# Source: https://learn.microsoft.com/dotnet/core/tools/dotnet-tool-install (--tool-path, --version)
$items = (dotnet msbuild $app -getItem:PackageReference) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw "Reading the package references failed with exit code $LASTEXITCODE."
}
$vpkVersion = (($items | ConvertFrom-Json).Items.PackageReference | Where-Object Identity -EQ 'Velopack').Version
if (-not $vpkVersion) {
    throw "CargoPlan.App.csproj references no Velopack package; vpk needs its version."
}
$tools = Join-Path $root "out\tools\vpk-$vpkVersion"
$vpk = Join-Path $tools 'vpk.exe'
if (-not (Test-Path $vpk)) {
    dotnet tool install vpk --version $vpkVersion --tool-path $tools
    if ($LASTEXITCODE -ne 0) {
        throw "Installing vpk $vpkVersion failed with exit code $LASTEXITCODE."
    }
}

# The delta base: the full package of the release before this one, which vpk pack finds in its output folder. It must be
# the published file, a new build of the same code differs (S11.0). A stable tag builds on the newest stable release (R7);
# a pre-release on the newest release of all, else an rc would never get a delta (T11.3, precision 1). A failure (the
# first Velopack release, a rate limit) costs the delta, never the release.
# Source: vpk download github -H (1.2.161): --repoUrl, --outputDir, --pre, --token (or VPK_TOKEN)
if ($DeltaBase -eq 'github') {
    $download = @('--skip-updates', 'download', 'github', '--repoUrl', $repoUrl, '--outputDir', $releases)
    if ($Version.Contains('-')) {
        $download += '--pre'
    }
    & $vpk @download
    if ($LASTEXITCODE -ne 0) {
        Write-BuildWarning "vpk download github failed with exit code $LASTEXITCODE; packing without a delta."
    }
}
elseif ($DeltaBase) {
    New-Item -ItemType Directory $releases -Force | Out-Null
    Copy-Item $DeltaBase $releases
}
$base = Get-ChildItem $releases -Filter 'CargoPlan-*-full.nupkg' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($DeltaBase -and -not $base) {
    Write-BuildWarning "No delta base found; packing without a delta."
}
elseif ($base) {
    Write-Output "Delta base: $($base.Name)"
}

# --exclude: its default is .*\.pdb, and a value that isn't empty replaces it (an empty one counts as not set). (?!) never
# matches, so the PDBs stay in the package and crash stacks keep their line numbers (D109); vpk's built-in excludes
# (createdump, .nupkg, .vshost.) still apply. check-portable.ps1 counts the PDBs, should Velopack change this meaning.
# Source: vpk pack -H (1.2.161) and src/vpk/Velopack.Packaging/PackageBuilder.cs at tag 1.2.161.
# packId CargoPlan, never changed (D109); --packTitle names the stub SC-CargoPlan.exe; --noInst: no Setup.exe (D109).
# --runtime as published: without it vpk builds the stub and Update.exe for x86 (its warning, T11.3).
$pack = @(
    '--skip-updates', 'pack',
    '--packId', 'CargoPlan',
    '--runtime', 'win-x64',
    '--packVersion', $Version,
    '--packDir', $output,
    '--mainExe', 'CargoPlan.App.exe',
    '--packTitle', 'SC-CargoPlan',
    '--icon', (Join-Path $root 'src\CargoPlan.App\cargoplan.ico'),
    '--channel', 'win',
    '--exclude', '(?!)',
    '--noInst',
    '--outputDir', $releases
)
& $vpk @pack
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed with exit code $LASTEXITCODE."
}

$full = Join-Path $releases "CargoPlan-$Version-full.nupkg"
$delta = Join-Path $releases "CargoPlan-$Version-delta.nupkg"
if (Test-Path $delta) {
    Write-Output "Delta: $(Split-Path $delta -Leaf), $((Get-Item $delta).Length) bytes (full: $((Get-Item $full).Length) bytes)"
}
elseif ($base) {
    Write-BuildWarning "vpk pack made no delta against $($base.Name)."
}

# The release's files (R4): the portable ZIP under the name haulers, the README and Nexus know (R5), its hash, and the feed
# the app's GithubSource reads, releases.win.json plus the packages it names by file name. Not the delta base (an earlier
# release's file), not RELEASES (Squirrel's old format) or assets.win.json (vpk upload's list): the app reads neither.
New-Item -ItemType Directory $release | Out-Null
$zipName = "SC-CargoPlan-v$Version-win-x64.zip"
$zip = Join-Path $release $zipName
Move-Item (Join-Path $releases 'CargoPlan-win-Portable.zip') $zip
Copy-Item $full, (Join-Path $releases 'releases.win.json') $release
if (Test-Path $delta) {
    Copy-Item $delta $release
}

& (Join-Path $PSScriptRoot 'check-portable.ps1') -Zip $zip -PublishDir $output -Version $Version

# sha256sum format ("<hash>  <name>"), so `sha256sum -c` checks it; Get-FileHash compares case-insensitively.
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText("$zip.sha256", "$hash  $zipName`n")

Get-ChildItem $release | ForEach-Object { Write-Output "Release file: $($_.Name), $($_.Length) bytes" }
