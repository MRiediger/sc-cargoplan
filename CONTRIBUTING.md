# Contributing

Thanks for helping haulers. Questions and ideas go to [Discussions](https://github.com/MRiediger/sc-cargoplan/discussions), bugs and ship requests to [Issues](https://github.com/MRiediger/sc-cargoplan/issues/new/choose).

## Build and test

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). The same steps as CI:

```powershell
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

CI runs them on Windows and Ubuntu. Ubuntu compiles the WPF projects (with `EnableWindowsTargeting=true`) but can't run them, so `CargoPlan.App.Tests` only runs on Windows. Warnings are errors.

A slow test over every crate layout is left out of the normal run (about a minute):

```powershell
dotnet test tests/CargoPlan.Core.Tests -p:RunSettingsFilePath= --filter Category=Exhaustive
```

## Run

```powershell
dotnet run --project src/CargoPlan.App
dotnet run --project src/CargoPlan.App -- --plan tests/CargoPlan.Core.Tests/Fixtures/Plans/split_and_shared.json
dotnet run --project src/CargoPlan.Cli -- plan tests/CargoPlan.Core.Tests/Fixtures/example_missions.json
dotnet run --project src/CargoPlan.Cli -- contracts <copy-of-contracts.db> [--since <id>] [--raw]
```

Run `contracts` against a copy of ContractTracker's `contracts.db`, not the live file.

## Publish

```powershell
./build/publish.ps1 [-Version 0.1.0]
```

It publishes the CLI and then the overlay into `out/publish/SC-CargoPlan/` and zips it, the same way the release workflow does. Don't publish by hand into that folder: both projects write `WindowsBase.dll`, and if the CLI's goes in last, the overlay doesn't start ([D48](docs/decisions.md)).

## Contribute a ship

A ship is one JSON file, no C#. [docs/ship-format.md](docs/ship-format.md) has the format, how to measure a ship in the hangar and a checklist for the pull request. In short:

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
