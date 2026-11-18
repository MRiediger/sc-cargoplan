# Testing updates

For developers. SC-CargoPlan updates itself with [Velopack](https://velopack.io) from its GitHub releases
([D109](decisions.md)). This page shows how to try an update without a real release, and how a release carries the
update feed. Players need none of this.

## What a release carries

`build/publish.ps1 -Version <v>` publishes the app and packs it with `vpk` in the version of the app's `Velopack`
package reference. The release workflow uploads `out/release/`:

| File | For |
|---|---|
| `SC-CargoPlan-v<v>-win-x64.zip` + `.sha256` | People: Velopack's portable ZIP under the usual name |
| `releases.win.json` | The app: the feed it reads |
| `CargoPlan-<v>-full.nupkg` | The app: the whole version |
| `CargoPlan-<v>-delta.nupkg` | The app: the change from the release before; only with a delta base |

The app finds the packages by the file names in `releases.win.json`, so they are never renamed.

**Deltas help only who is exactly one version behind; everyone else downloads the full package.** The release workflow
builds the delta against the newest full package of the release before (a stable tag: the last stable release; a
pre-release tag: the newest release of all). The first update of a freshly unpacked ZIP is always full: the ZIP has no
base package, only an update leaves one in `packages\`.

## A local feed: `--updates-source`

Pack two versions, the second against the first:

```powershell
./build/publish.ps1 -Version 0.9.0
Copy-Item out\release\* ..\feed\                  # keep 0.9.0's feed and ZIP
Expand-Archive out\release\SC-CargoPlan-v0.9.0-win-x64.zip ..\portable-0.9.0
./build/publish.ps1 -Version 0.9.1 -DeltaBase ..\feed\CargoPlan-0.9.0-full.nupkg
Copy-Item out\release\* ..\feed\ -Force           # releases.win.json now names 0.9.0 and 0.9.1
```

Start the old version against the folder, with a data folder of its own so your profile stays untouched:

```powershell
..\portable-0.9.0\SC-CargoPlan.exe --data-dir <full path> --updates-source <full path of ..\feed>
```

The overlay offers `v0.9.1`. Both switches need full paths. Velopack writes its own log to
`%LocalAppData%\velopack\velopack_CargoPlan.log` either way; the app adds Velopack's lines to `cargoplan.log` as
`Velopack:`.

A build started from `bin\` or `out\publish\` isn't a Velopack package and checks nothing (`cargoplan.log`: "not a
Velopack package"). A `0.0.0-dev` build never checks.

## Pre-releases over GitHub: `--updates-prerelease`

Stable installs never see a release marked as pre-release on GitHub (`GithubSource(prerelease: false)`); every `v…-rc.N`
tag becomes one. To update one rc to the next over the network, start the older rc with:

```powershell
SC-CargoPlan.exe --updates-prerelease --data-dir <full path>
```

Each check then logs `checking GitHub with pre-releases (--updates-prerelease)`. The restart after the update keeps the
switch. Together with `--updates-source` it is ignored (a local feed has no pre-release flag) and the notice line says so.

## The window of 10 releases

The app reads only GitHub's first page of 10 releases, pre-releases and hologram releases included, before it filters
anything ([D109](decisions.md)). If ten newer releases hide the last stable one, nobody gets an update. Hence the
release hygiene in [CONTRIBUTING.md](../CONTRIBUTING.md#release): rc releases go after the stable tag, at most two
hologram releases at a time.

## Ship data updates: `--ship-data-source`

The ship data come as data releases of
[sc-cargoplan-shipdata](https://github.com/MRiediger/sc-cargoplan-shipdata) ([D112](decisions.md)), not through
Velopack. A developer's build (`0.0.0-dev`) checks them only with `--ship-data-source`, which takes a folder or a
repository:

```powershell
# A folder: a data release's manifest.json and ships.zip side by side
$feed = '<full path of a new empty folder>'
$base = 'https://github.com/MRiediger/sc-cargoplan-shipdata/releases/download/ships-2026.11.1'
Invoke-WebRequest "$base/manifest.json" -OutFile "$feed\manifest.json"
Invoke-WebRequest "$base/ships.zip" -OutFile "$feed\ships.zip"
dotnet run --project src/CargoPlan.App -- --data-dir <full path> --ship-data-source $feed

# The real repository (or a fork of the same layout), as a release build asks it
dotnet run --project src/CargoPlan.App -- --data-dir <full path> --ship-data-source https://github.com/MRiediger/sc-cargoplan-shipdata
```

The overlay offers `Ship data update available · 2026.11.1` below the update line. **Update** installs the release into
`<data folder>\ships\ships-2026.11.1\` and restarts with the same arguments (without `--plan`). `cargoplan.log` has the
lines `Ship data: in use: …`, `… available`, `installed …`, `… restarting with […]`, and after the restart
`nothing newer than …`. `CargoPlan.Cli ships --version --data-dir <full path>` names the data in use. A release
build checks `sc-cargoplan-shipdata` without the switch, unless `shipData.checkOnStartup` is off.
