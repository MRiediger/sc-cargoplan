# SC-CargoPlan

[![CI](https://github.com/MRiediger/sc-cargoplan/actions/workflows/ci.yml/badge.svg)](https://github.com/MRiediger/sc-cargoplan/actions/workflows/ci.yml)

A cargo load-plan overlay for Star Citizen hauling (Alpha 4.10.x).

## What

Covalex contracts often send crates from one pickup to several destinations, and the freight elevator refuses the whole
delivery as soon as one crate for another stop is on the platform. SC-CargoPlan decides before you load which
destination goes into which rack of your MISC Hull A and shows it as a translucent overlay next to the game, a separate
window that never touches the game process.

<!-- image: docs/screenshots/overlay-ingame-elevator.png -->

## Download & install

1. Download `SC-CargoPlan-v<version>-win-x64.zip` from the
   [latest release](https://github.com/MRiediger/sc-cargoplan/releases/latest). Nothing else to install: .NET comes
   with it.
2. Right-click the ZIP → **Extract All**. You get a folder `SC-CargoPlan`.
3. Start `SC-CargoPlan\CargoPlan.App.exe`.
4. The first time, Windows says **"Windows protected your PC"**: SC-CargoPlan isn't signed with a paid certificate.
   Click **More info → Run anyway**. Windows asks once per download.

   <!-- image: docs/screenshots/first-start/smartscreen.png -->

5. Run Star Citizen in **Borderless** window mode; in exclusive fullscreen the game covers the overlay.

**Optional: check the download.** Each release has a `.sha256` file next to the ZIP. Put both in one folder, open
PowerShell there and run:

```powershell
$zip = Get-Item .\SC-CargoPlan-*.zip; (Get-FileHash $zip).Hash -eq (Get-Content "$zip.sha256").Split(' ')[0]
```

`True` means the ZIP is exactly the one the release built.

## First start

Without a plan the overlay shows your empty ship and an **Open editor** button.

<!-- image: docs/screenshots/first-start/A-empty-overlay.png -->

- **Editor** (the pencil, or **Open editor**): one row per contract item with destination, commodity, SCU and max
  crate size, as the contract states them. The crates follow by themselves. Press **Plan**; while a row is wrong,
  **Plan** stays off and its tool tip says why.

  <!-- image: docs/screenshots/editor.png -->

- **Click-through by itself:** while Star Citizen has the mouse (flight, first person) the overlay lets every click
  through to the game. With the cursor showing (mobiGlas, terminals) you can use it. The padlock locks it
  click-through by hand.
- **Hotkeys:** there are **none by default**, because any default could clash with your Star Citizen bindings. Set them
  in the settings window (the gear), section **Hotkeys**: lock/unlock, hide/show the overlay, sync from ContractTracker,
  re-plan.
  At least one of Ctrl, Alt or Win plus one key.
- **Settings** (the gear): look, legend position, hotkeys, ContractTracker; see [Settings](#settings).

More: [user guide – first start](docs/user-guide.md#first-start), [entering missions](docs/user-guide.md#entering-missions),
[hotkeys](docs/user-guide.md#hotkeys).

## ContractTracker

If you capture contracts with [ContractTracker](https://www.nexusmods.com/starcitizen/mods/34) (F3 at the terminal),
the overlay finds it by itself and takes every capture into the plan a few moments later, without a click. Nothing that
is already loaded moves. The editor's **Sync from ContractTracker** (or its hotkey) does the same by hand. If a new
contract doesn't fit into the layout that grew, although there is room, **Re-plan** (↻ in the header, or in the editor) lays
everything on board out anew after asking; your ticks stay.

<!-- image: docs/screenshots/first-start/B-plan-from-ct.png -->

The legend marks what a capture couldn't tell for sure:

- `4 SCU?` – the contract named no max crate size; the largest that fits was assumed. Check at the kiosk.
- `⚠ 1 of 3 destinations` – fewer destinations read than the contract lists. Capture again or add the rows.
- `⚠ duplicate?` – looks like a second capture of a contract you have. Delete one in the editor if it is.
- `⚠ doesn't fit (16 SCU, 8 free) – tick to drop` – no room for the whole contract. Click to drop it, or it is placed
  once there is room.

If ContractTracker starts over with a new database, the overlay imports everything in it once and says
`ContractTracker database looks new – all contracts imported`. With **Auto-sync** off (settings, section
ContractTracker), captures wait and an amber line says how many are new.

<!-- image: docs/screenshots/first-start/C-new-db-notice.png -->
<!-- image: docs/screenshots/first-start/A-settings-ct-not-found.png -->

More: [user guide – ContractTracker](docs/user-guide.md#contracttracker).

## At the cargo lift

- **Tick** a destination when the lift has taken its cargo: click its legend line or any of its crates. It turns gray;
  click again to undo. Ticks are saved at once.
- **Drag** (right mouse button) a crate to where the lift really put it, or a legend line onto another rack. Cyan ghosts
  show where everything goes; if the cell is taken, the app finds room and says what it moved. Red only when the cargo
  fits nowhere.
- **Split / Merge** (scissors, chain links): when the kiosk hands out two 2s instead of a 4, or the other way round.
- **Colors say what lies where:** the app doesn't know your route. Two colors in a rack are two destinations; a color
  on top of another has to come off first. **Red** is only for crates that can't lie like that (overlapping, nothing
  below).

More: [user guide – at the cargo lift](docs/user-guide.md#at-the-cargo-lift),
[reading the ship view](docs/user-guide.md#reading-the-ship-view).

## Settings

<!-- image: docs/screenshots/settings.png -->

The gear opens the settings window. Every change shows at once and is saved; there is no OK. Sections: **Window**,
**Legend**, **Legend layout**, **Crates**, **Header**, **Buttons** (each with **Reset to defaults**), **Tool windows**,
**Hotkeys**, **ContractTracker**. The footer shows the version.

Settings are stored in `%LocalAppData%\SC-CargoPlan\settings.json`, your plan in `plan.json` next to it. Before the
first save after each start, the old `settings.json` is kept as `settings.json.bak`.

More: [user guide – settings](docs/user-guide.md#settings-and-your-look),
[where your data lives](docs/user-guide.md#where-your-data-lives), [settings file format](docs/settings-format.md).

## Known issues

- **Only the MISC Hull A** so far. Want another ship? [Request it](https://github.com/MRiediger/sc-cargoplan/issues/new/choose).
- **Unsigned:** Windows warns at the first start of every new version (see [Download & install](#download--install)).
- **No auto-update:** check the [releases](https://github.com/MRiediger/sc-cargoplan/releases) for new versions.
- **Exclusive fullscreen** hides the overlay; use Borderless.
- **Camera and zoom** of the ship view are back to the default after a restart.

## Report a bug

1. Copy the version from the footer of the settings window (e.g. `SC-CargoPlan 0.1.0+…`).
2. Open a [bug report](https://github.com/MRiediger/sc-cargoplan/issues/new?template=bug.yml) (needs a GitHub account)
   and fill in the form.
3. If ContractTracker contracts are involved, add what SC-CargoPlan read from them. Close ContractTracker, open
   PowerShell in the `SC-CargoPlan` folder, copy the database there and run the CLI on the **copy**:

   ```powershell
   Copy-Item "$env:APPDATA\SCContractTracker\contracts.db" .\contracts-copy.db
   .\CargoPlan.Cli.exe contracts .\contracts-copy.db --raw
   ```

   Paste the output into the form. It contains the contracts' text as ContractTracker read it.

## Credits

- [ContractTracker](https://www.nexusmods.com/starcitizen/mods/34) by Yuiki captures the contracts. SC-CargoPlan only
  reads its local database, never writes it, and sends none of it anywhere.
- Ship holograms are an optional download from this repository's releases; they derive from game models and may be
  withdrawn on request – the app checks at most once a day. Turn downloads off under Settings → Ship view; holograms
  already downloaded stay. They come from [Fleetyards](https://fleetyards.net)' holo models of CIG's ships; the
  release's `NOTICE.md` has the details.
- The stop colours are the [Okabe-Ito palette](https://jfly.uni-koeln.de/color/) (Color Universal Design), chosen to
  stay apart with colour blindness.

## Contributing

Found a bug, want a ship added or have an idea? [Open an issue](../../issues/new/choose); questions go to
[Discussions](../../discussions). To build, test or contribute a ship file, see [CONTRIBUTING.md](CONTRIBUTING.md) and
the [ship file format](docs/ship-format.md). Security issues: [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE)
