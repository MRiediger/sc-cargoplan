# Contributing

Thanks for helping haulers. Questions and ideas go to [Discussions](https://github.com/MRiediger/sc-cargoplan/discussions), bugs and ship requests to [Issues](https://github.com/MRiediger/sc-cargoplan/issues/new/choose).

## Build and test

Requires exactly the [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) version named in `global.json`;
with any other version, `dotnet` stops with a message in the repository folder (D86). The same steps as CI:

```powershell
dotnet restore
dotnet build -c Release --no-restore
dotnet format SC-CargoPlan.sln --verify-no-changes --no-restore
dotnet test -c Release --no-build
```

CI runs them on Windows and Ubuntu; the format check runs on Windows only and fails if `dotnet format` would change a file (`.editorconfig` has the rules). Ubuntu compiles the WPF projects (with `EnableWindowsTargeting=true`) but can't run them, so `CargoPlan.App.Tests` only runs on Windows. Warnings are errors.

A slow test over every crate layout is left out of the normal run (about a minute):

```powershell
dotnet test tests/CargoPlan.Core.Tests -p:RunSettingsFilePath= --filter Category=Exhaustive
```

## Run

```powershell
dotnet run --project src/CargoPlan.App
dotnet run --project src/CargoPlan.App -- --plan tests/CargoPlan.Core.Tests/Fixtures/Plans/split_and_shared.json
dotnet run --project src/CargoPlan.Cli -- plan tests/CargoPlan.Core.Tests/Fixtures/example_missions.json
dotnet run --project src/CargoPlan.Cli -- scan <screenshot.png>
dotnet run --project src/CargoPlan.App -- --data-dir <full path of a folder>
```

`scan` reads a contract from a screenshot of the game window, as the overlay's scan does. `--data-dir` keeps a test
run's settings, plan and logs in that folder instead of your profile.

## Publish

```powershell
./build/publish.ps1 -Version 0.1.0 [-DeltaBase github|<path of a full nupkg>]
```

It publishes the CLI and then the overlay into `out/publish/SC-CargoPlan/` and packs that folder with Velopack's `vpk`, the same way the release workflow does ([D109](docs/decisions.md)). `vpk` is installed into `out/tools/` in the version of the app's `Velopack` package. The release files land in `out/release/`: the portable ZIP `SC-CargoPlan-v<version>-win-x64.zip` with its `.sha256`, and the update feed (`releases.win.json`, the full and, with `-DeltaBase`, the delta package). The ZIP holds the launcher `SC-CargoPlan.exe`, `Update.exe` and the app in `current/`. `build/check-portable.ps1` then checks that layout. Don't publish by hand into the publish folder: both projects write `WindowsBase.dll`, and if the CLI's goes in last, the overlay doesn't start ([D48](docs/decisions.md)). Trying an update without a release: [docs/update-testing.md](docs/update-testing.md).

## Release

For the maintainer:

1. Commit `docs/release-notes/v<version>.md`, then push the tag `v<version>`. A tag with a hyphen (`v0.5.0-rc.1`) becomes a GitHub pre-release, which stable installs never see.
2. The release workflow builds, tests, takes the last release's full package as the delta base, packs, checks the ZIP's layout and uploads `out/release/` ([D49](docs/decisions.md), [D109](docs/decisions.md)).
3. Check the release: the ZIP and its `.sha256`, `releases.win.json`, the full package and, after the first Velopack release, the delta. The log of the step "Publish and pack" shows `Delta: …` and `Layout check: … passed`. A missing delta is a warning, not a failure.
4. After the stable release, delete its rc releases and tags (`gh release delete v<version>-rc.N --cleanup-tag`, plus `git tag -d` locally).

**Release hygiene:** the app reads only the 10 newest GitHub releases, pre-releases and hologram releases included. Among them must always be the last stable app release, or nobody gets an update. So keep at most two hologram releases, and never more than nine pre-releases or other releases after the last stable one.

After that, the same ZIP goes on Nexus Mods by [docs/nexus/checklist.md](docs/nexus/checklist.md) ([D87](docs/decisions.md)).

## Correct ship data

The racks of 102 ships come from the game files, by way of [cryon.rocks (Motherlode)](https://cryon.rocks/ops/cargo); three ships still use Fleetyards data ([docs/ship-format.md](docs/ship-format.md#where-the-data-comes-from)). If a rack in the overlay doesn't match the game, open a [ship correction](https://github.com/MRiediger/sc-cargoplan/issues/new?template=ship-correction.yml): the ship as the list names it, the rack number, what is wrong, a screenshot from the game and one of the overlay.

A rack in the wrong place or with the wrong size is usually in the game data itself. It is then fixed upstream: we pass it on to Motherlode, and the next import brings the fix to every ship. A door limit is ours to fix, since the game files don't describe doors. A hand fix in a ship file is for what the import can't know; [Correcting a rack](docs/ship-format.md#correcting-a-rack) shows how.

## Contribute a ship

A ship is one JSON file, no C#. Ships the game files have come from the import ([docs/ship-format.md](docs/ship-format.md#importing-from-the-game-files)); a ship the import doesn't have yet can be measured by hand. [docs/ship-format.md](docs/ship-format.md) has the format, how to measure a ship in the hangar and a checklist for the pull request. In short:

- Measure in the game and name the patch.
- Give every rack its `stackAxis`: the direction a second layer of crates goes.
- Name your source in the pull request (your own measurement, or a guide such as Erec's – check its license first).

Not into JSON? Open a [ship request](https://github.com/MRiediger/sc-cargoplan/issues/new?template=ship.yml) with the measurements, and someone turns it into a file.

## Pull requests

- Logic changes come with tests.
- `dotnet build` without warnings and `dotnet test` green.
- Behaviour checked in the running overlay, not only in tests.
- Commit messages in English.
- No new NuGet package without a reason in the pull request: fewer packages, smaller download.
- `CargoPlan.Core` references nothing from WPF or Windows APIs; the app and the importer reference Core, never the other way round.

Bigger changes: open a discussion or issue first, so we agree on the approach before you write the code.
