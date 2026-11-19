# SC-CargoPlan

[![CI](https://github.com/MRiediger/sc-cargoplan/actions/workflows/ci.yml/badge.svg)](https://github.com/MRiediger/sc-cargoplan/actions/workflows/ci.yml)

A load-plan overlay for Star Citizen haulers: it decides which destination goes into which rack before you load, and
shows it in a translucent window next to the game.

![F3 on a contract in the mobiGlas: its crates appear in the racks of the MISC Hull A](docs/images/hero.gif)
<!-- SHOT: E1 -->

## Features

- One color per destination, one rack per destination where the ship allows it, every crate named in the legend.
- Optionally a block per destination in a shared rack, with a gap to get at the crates.
- Press F3 on an open contract: CargoPlan reads it itself and adds it to the plan; or type it in the editor.
- Drag, turn, split and merge crates to match what the cargo elevator really did.
- Mark each delivery loaded, then delivered at its stop; undo any step.
- Click-through while the game has the mouse. A separate window: nothing is injected into the game.
- 109 ships and one cargo module configuration. Racks, hull and outline of all but two come from the game files; the
  135c and the 600i Executive Edition still use Fleetyards data.

![The MISC Hull B with its tilted racks in the ship's hologram](docs/images/ships-hull-b.png)
<!-- SHOT: H15 -->

## Install

1. Download `SC-CargoPlan-v<version>-win-x64.zip` from the [latest release](https://github.com/MRiediger/sc-cargoplan/releases/latest).
   Also on [Nexus Mods](https://www.nexusmods.com/starcitizen/mods/52) – the same ZIP; updates still come from GitHub.
2. Right-click it → **Extract All** into a new folder you can write to (not `Program Files`).
3. Start `SC-CargoPlan.exe` in that folder. Windows warns that the app is unsigned: **More info → Run anyway**.

Code signing: this project has applied for free code signing by the SignPath Foundation; once granted, releases will
carry a signature issued to SignPath Foundation.

Run Star Citizen in **Borderless** window mode; exclusive fullscreen covers the overlay.

Coming from v0.4.0 or older: extract v0.5.0 into a new folder, not over the old one; your settings and plan stay. Steps:
[release notes of v0.5.0](https://github.com/MRiediger/sc-cargoplan/releases/tag/v0.5.0).

## Updates

From v0.5.0 on, CargoPlan checks GitHub for a new version once per start and offers it at the bottom of the overlay;
it downloads only when you click **Update**. Switch it off in the settings, section **Updates**. More:
[Updates](https://github.com/MRiediger/sc-cargoplan/wiki/Updates).

## Ship data

The ships come with the app: cargo grids and ship models from the game files, by way of
[cryon.rocks (Motherlode)](https://cryon.rocks/ops/cargo). They are built once a month in a repository of their own,
[sc-cargoplan-shipdata](https://github.com/MRiediger/sc-cargoplan-shipdata). If anything changed, there is a new data
release, and from v0.6.0 on CargoPlan offers it at the bottom of the overlay like a new version: **Update** or **Skip
this version**, section **Ship data** in the settings. More:
[Ships and cargo grids](https://github.com/MRiediger/sc-cargoplan/wiki/Ships-and-cargo-grids).

A rack that doesn't match the game: open a
[ship correction](https://github.com/MRiediger/sc-cargoplan/issues/new?template=ship-correction.yml) here or an issue in
[sc-cargoplan-shipdata](https://github.com/MRiediger/sc-cargoplan-shipdata/issues). We check it and pass it on to
Motherlode, so the next data release fixes it for everyone. A pull request with a corrected ship file goes to
sc-cargoplan-shipdata.

## Network access

CargoPlan downloads (GET) from GitHub, three kinds, each with its own switch in the settings; no account, no token.
GitHub sees your IP address and the User-Agent (`SC-CargoPlan/<version>`, for the update check `Velopack/<version>`);
nothing else is sent.

- **App update check:** once per start, the release list of this project (*Updates*, **Check for updates on startup**).
- **Ship data check:** once per start, the `manifest.json` of the latest data release, about 130 KB (*Ship data*,
  **Check for ship data updates on startup**).
- **Ship holograms:** one file per ship, about 0.3 MB, when you pick a ship while a hologram look is on (*Ship view*,
  **Download ship holograms**).

A new version or new ship data are downloaded only when you click **Update**. Without a network CargoPlan works as
before: the ship data are in the ZIP.

## Requirements

- Windows 10/11 x64 (tested on Windows 11). Nothing else to install: .NET comes with it.

## Known issues

Reading contracts is new in v0.4.0:

- Checked at 3440×1440 and 1920×1080 only. If a scan finds no contract on 32:9, 16:10 or 4K, start
  `SC-CargoPlan.exe --keep-scans`, scan again and attach the picture from `%LocalAppData%\SC-CargoPlan\scans` to a
  [bug report](https://github.com/MRiediger/sc-cargoplan/issues/new?template=bug.yml).
- A contract whose destinations run past one mobiGlas page is read as far as it shows; the legend says
  `⚠ 1 of 3 destinations` when the contract tells.
- ContractTracker also reacts to F3: while it runs, CargoPlan says so. Pick another key or close ContractTracker.
- Two contracts with the same route and reward count as one: add the second in the editor.
- CargoPlan's windows over the contract blink briefly during a scan.
- After many scans CargoPlan uses up to about 900 MB of memory. Clear the scan key in the settings and the reader
  doesn't load at start.

All known issues: [Troubleshooting](https://github.com/MRiediger/sc-cargoplan/wiki/Troubleshooting#known-issues).

## Documentation

- [Wiki](https://github.com/MRiediger/sc-cargoplan/wiki) ·
  [Quick start](https://github.com/MRiediger/sc-cargoplan/wiki/Quick-start-at-the-cargo-elevator) ·
  [Troubleshooting](https://github.com/MRiediger/sc-cargoplan/wiki/Troubleshooting)
- Bugs, wrong cargo grids, ideas: [Issues](https://github.com/MRiediger/sc-cargoplan/issues/new/choose) ·
  questions: [Discussions](https://github.com/MRiediger/sc-cargoplan/discussions)
- Build, test, contribute a ship: [CONTRIBUTING.md](CONTRIBUTING.md) · security: [SECURITY.md](SECURITY.md)

## Credits & disclaimer

- Cargo grid data and ship models from [cryon.rocks (Motherlode)](https://cryon.rocks/ops/cargo), extracted from the
  Star Citizen game files and used with its operators' permission: the racks, the hull holograms and the outlines come
  from the same game data.
- Two ships (135c, 600i Executive Edition) still use hull data from
  [fleetyards.net](https://fleetyards.net): their grids, hologram and outline. Fleetyards' data made the first ship list
  possible.
- Thanks to both for letting this project use their data.
- Stop colors: the [Okabe-Ito palette](https://jfly.uni-koeln.de/color/) and [Paul Tol's](https://sronpersonalpages.nl/~pault/) schemes.
- Not affiliated with Motherlode, Fleetyards or CIG. Star Citizen is a trademark of Cloud Imperium Rights LLC.

[MIT](LICENSE). Not under MIT: the ship data and silhouettes (`ship-data\NOTICE.md` next to the app, from the
[ship data releases](https://github.com/MRiediger/sc-cargoplan-shipdata/releases)) and the ship holograms (assets of
the same releases, under the same `NOTICE.md`).

If it helps you:
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Buy%20me%20a%20coffee-FF5E5B?logo=kofi&logoColor=white)](https://ko-fi.com/plintonium)
