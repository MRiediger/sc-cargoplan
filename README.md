# SC-CargoPlan

[![CI](https://github.com/MRiediger/sc-cargoplan/actions/workflows/ci.yml/badge.svg)](https://github.com/MRiediger/sc-cargoplan/actions/workflows/ci.yml)

A cargo load-plan overlay for Star Citizen hauling (Alpha 4.10.x).

## What

You enter your hauling missions as the contract states them (destination, commodity, SCU, max crate size); the crates follow from that. SC-CargoPlan decides which destination goes into which section of your ship and shows the plan as a translucent, always-on-top overlay that turns click-through while you fly. At each stop you tick off what you delivered.

## Why

Covalex contracts often go from one pickup to several destinations. Each crate is bound to a mission and a destination, but the game doesn't show which crate goes where. The freight elevator rejects the whole delivery as soon as one foreign crate is on the platform.

SC-CargoPlan doesn't try to recognize crates. It **plans the loading in advance**: the first stop goes into Rack 1, the next stops into the following racks, one destination per rack. Load by the plan, unload by the plan.

The overlay is a separate window. It does not touch the game process, so it's safe with Easy Anti-Cheat.

## Download

_Coming soon: a single Windows EXE on the [Releases](../../releases) page._

## Usage

Run Star Citizen in **Borderless** window mode. In exclusive fullscreen the game covers every other window, the overlay included.

_Screenshot: coming soon._

**Reading the ship view:** the overlay draws your ship's cargo racks as boxes, at first seen from the front, from the port side and from above: the bow points to the lower left, the port side faces you. Turn and tilt the ship until it looks the way you see the hold, even from below for racks under the hull. Each rack shows its number on its largest visible face. Each destination has its own color, and the legend (below the ship, unless you [move it](docs/settings-format.md#legend-layout)) lists it with its SCU. A destination split over two racks has the same color in both. A rack shared by two destinations shows both colors side by side; the planner's hints below the legend tell you which racks need care. Each crate is drawn as one box in its shape (a 4 SCU crate is 2 × 2 × 1 cells), with a small gap all round; free space shows as single cells. The planner places the crates: the largest first, layer by layer away from the plate (on the Hull A from the spindle outwards, as in the game), and in each layer row by row from the bow. Crates stay where the plan says, after a restart and for everyone who loads the plan, until you drag them somewhere else (see **Changing the plan**). Where the elevator really puts a crate is still up to the game. A crate always rests on the plate or on other crates. A crate that overlaps another or has nothing to rest on gets a red outline, and its rack turns red.

- **Color** = destination. Up to eight colors, from the ninth destination on they repeat (the legend says so).
- **Gray** = delivered.
- **Outline only** = free cell.
- **Red edges** = the loaded plan file has crates in this rack that overlap, don't fit or have nothing below them. Only a plan file edited by hand gets there; the planner and your moves never do.

Hover over a crate to see what it is, e.g. `Riker · Rack 1 · port upper · 4 SCU Titanium crate (3 of 4)`. Inside the view: **left drag** turns and tilts the ship, **right drag** next to the crates moves the view, the **mouse wheel** zooms, a **double-click** next to the cargo resets the view. To move the overlay window, drag it by its header (the ship name), the hint lines below the legend or the border.

**Unloading:** when a destination has taken its cargo, click its line in the legend or any of its cargo in the view. Its racks turn gray, the legend line is struck through, and the hints about that destination disappear, so the next cargo lift only shows what is still on board. The ship stays where it is. A destination split over two racks is ticked once, both racks turn gray. Clicked the wrong one? Click again. When every destination is ticked, the header says "All stops delivered." Ticking doesn't take the focus from the game, so the terminal keeps working. Every tick is saved right away, so a game crash or a restart between two stops keeps your plan and your ticks.

**Changing the plan:** when the elevator puts a crate somewhere else than planned, make the plan follow the plate. **Right drag a crate** onto the cell where it really lies, in the same rack or another one. Point at a crate to put it on top of that crate, or at a free cell to put it into that gap; it drops down to the next crate or the plate below, never hanging in the air. While you drag, the crate fades and a ghost shows where it would go: **cyan** if it fits, **red** if it doesn't, with the reason when you hover. Release on cyan to move it; release on red and nothing moves, an amber line says why. To move several crates at once, **right click** each of them first: a magenta edge marks the selection. Right drag any selected crate and the whole selection goes along, every crate keeping its place relative to the one you hold, even across racks; if one of them would land outside the racks or doesn't fit, nothing moves. Right click a selected crate again to take it out, or right click free space to clear the selection; any change of the plan and locking the overlay clear it too. To move a whole destination, **right drag its line in the legend** onto a rack: its crates fade, the rack gets a cyan edge, and on release its crates are laid out in that rack around the cargo already there. If they don't fit, the amber line says so. Either way, the hints are worked out again for the new layout. Changed your mind? Drag it back. Delivered crates can't be dragged, they are off the ship, but you can put crates on top of them. Moves are saved like ticks; pressing **Plan** in the editor makes a new plan and drops them (see [Entering missions](#entering-missions)).

**When the kiosk delivers other crates:** sometimes the elevator hands out two 2 SCU crates where the plan has one 4 SCU crate, or the other way round. Make the plan match with the two buttons left of the pencil. **Split** (scissors): select one crate and click it; the crate becomes the two largest crates that add up to it, in the same place (a 4 becomes 2 + 2, an 8 becomes 4 + 4, a 32 becomes 16 + 16). Need smaller ones? Click again, the parts stay selected. **Merge** (chain links): select two or more crates and click it; they become one crate. The rules are the game's: only crates of one destination and one commodity, in one rack, adding up to a crate size (1, 2, 4, 8, 16, 24 or 32 SCU) no larger than the contract allows, and lying together in exactly that crate's shape (two 2s side by side make a 4, two 2s in a row don't). If they lie apart, drag them together first. A grayed-out button tells you why when you hover over it. Split and merge never move other crates, and the mission in the editor follows: its crates column then lists the crates as they are, in italics.

Without a plan, the overlay shows your ship with empty racks, "No plan. Open the editor to enter missions." and an **Open editor** button (see [Entering missions](#entering-missions)).

**Never eats your tractor-beam click:** the overlay becomes click-through automatically while Star Citizen has the mouse. In flight and first person the game hides the cursor and it follows your view, so a click over a normal window would be lost. As soon as the cursor shows (mobiGlas, Esc menu, terminals) or another program is in front, the overlay is interactive again: you can zoom, hover and move it.

The small padlock in the overlay's top-right corner locks it by hand: the overlay stays click-through even with the cursor showing, so every click goes to the game. Click the padlock again to unlock. A closed amber padlock means locked, an open white one unlocked. Clicking the padlock or the overlay doesn't take the focus from the game.

Start with `--locked` to begin with the padlock locked.

**Remembers where you put it:** drag the overlay next to your cargo grid and resize it once; position and size are saved right away, so they survive a restart and even a crash. They live in `%LocalAppData%\SC-CargoPlan\settings.json`. To reset the overlay to its default size and position, close it and delete that file (your hotkeys and look go with it). If you unplug a monitor, the overlay moves back onto the screens you have.

**Your look:** background and its transparency, frames, text colours, the legend's font size, crate opacity, rack number colour and size, and a halo for reading over bright scenes are set in the same file, until the settings window arrives. So is where the legend stands (top or bottom; left, centre or right) and whether it shows the destination lines at all. Every key, its default and its range: [settings file format](docs/settings-format.md). The stop colours stay fixed: they are chosen to stay apart with colour blindness.

### Entering missions

**This is the full loop:** accept your contracts, enter them in the editor, press **Plan**, load the racks as the overlay shows, tick each destination at its cargo lift. No terminal, no other tool.

Open the editor with the **pencil** in the overlay's header (next to the padlock), or with **Open editor** while there is no plan. The pencil is hidden while the overlay is locked: planning needs a free cursor. The editor is a normal window, so it takes the focus while you type; afterwards click back into the game (or Alt+Tab).

- **One row per contract item, two numbers from the contract:** Destination, Commodity, **SCU** and **Max crate** (the "4 SCU or smaller" in the contract text). A contract with two commodities for the same destination is two rows with the same destination.
- **The crates appear by themselves:** the **Crates** column shows how the freight elevator delivers them, the largest allowed crate first, the rest in smaller ones: 6 SCU at max 4 is `4+2`, 15 SCU at max 4 is `4+4+4+2+1`.
- **The kiosk delivered something else?** Click the **Crates** cell and type what you got, e.g. `2+2+2`. Crates typed by hand show in *italics*; clear the cell to go back to the worked-out mix.
- **Stop order:** the **Stop** column shows the route. Stops follow the first row of each destination, so `Riker, Shubin, Orison, Shubin` gives stops 1, 2, 3, 2. Use **↑**/**↓** to move rows; destinations match regardless of upper or lower case.
- **New rows:** type into the empty row at the bottom (Tab from the last cell gets you there), or press **Add**. A new row takes the max crate size of the row above; the first row starts at 4.
- **Mistakes show in red:** an empty destination or commodity, an SCU that isn't a whole number, or crates typed by hand that don't add up to the SCU, aren't a crate size or are larger than the max (`2+2+3` for 6 SCU). The reason is in the tool tip and below the grid; **Plan** stays off until every row is fixed. If the missions don't fit the ship, the reason shows in the same place.
- **Plan** sends the rows to the overlay, saves them and closes the editor. If the missions don't fit, the editor stays open with the reason. Planning again makes a new plan: ticks and moved cargo are cleared. The editor says so beforehand if you have ticked a destination.
- **Next route:** the editor opens with your current missions. **Clear rows** empties the rows for the next route; the overlay keeps the old plan until you press **Plan**. Closing the editor without **Plan** throws away your changes, not your plan.
- **Import… / Export…** read and write [mission files](docs/mission-format.md), e.g. to share a route. Export writes the rows as they are, planned or not. Older files with one crate size per row still import; their crates show as typed by hand.
- **Copy as text** copies the current plan as plain text, ready to paste into Discord or your org chat.

When Star Citizen takes the mouse (flight), the overlay turns click-through and an open editor hides, so neither catches a click meant for the game. The editor comes back when the cursor does, without taking the focus. The overlay hotkey hides it too.

### Hotkeys

Two global hotkeys work while Star Citizen has the focus and the cursor is hidden: one switches the padlock, one hides the overlay and brings it back (dogfight, landing). There are **no default hotkeys**: a hotkey takes its keys away from every other program, the game included, and any default could clash with one of your Star Citizen bindings. Set them yourself in `%LocalAppData%\SC-CargoPlan\settings.json` (close the overlay first, start it again afterwards):

```json
{
  "schemaVersion": 2,
  "hotkeys": {
    "toggleLock": "Ctrl+Alt+K",
    "toggleOverlay": "Ctrl+Alt+H"
  }
}
```

Keep the `overlay`, `legend` and `appearance` parts if your file has them. Either hotkey can be left out. A file with `"schemaVersion": 1` works too.

- Format: modifiers and one key joined by `+`, in any order and case. Modifiers are `Ctrl`, `Alt`, `Shift`, `Win`; **at least one of `Ctrl`, `Alt` or `Win` is required**. Keys are `A`–`Z`, `0`–`9`, `F1`–`F24` (not `F12`, Windows reserves it), `Space`, `Pause`, `ScrollLock`, `Insert`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`.
- Pick a combination that **none of your Star Citizen bindings use**, and check it in the game's keybinding settings. If the game still reacts to it, choose another one.
- A hotkey that is taken by another program, or a typo, shows as an amber line next to the legend; the other hotkey still works.
- If a hotkey shows no error but does nothing, another program swallows the keys before Windows hands them out (some tools do that for their own shortcuts). Choose a different combination.
- Hidden, the overlay also leaves the taskbar; press the hotkey again to bring it back where it was.
- A typo in a name (e.g. `toggleLok`) makes the whole file unreadable: the overlay starts with defaults and says so.

No hooks: the hotkeys use the normal Windows hotkey service, nothing touches the game process.

### Where your data lives

Both files are in `%LocalAppData%\SC-CargoPlan\` and are saved as soon as something changes.

| File | What | Deleting it (overlay closed) |
|---|---|---|
| `settings.json` | Overlay position and size, hotkeys, look ([format](docs/settings-format.md)) | Default size, position and look, no hotkeys. Your plan stays. |
| `plan.json` | Your current plan: the missions you entered, which crate lies where in which rack, ticks ([format](docs/plan-format.md)). The editor opens with these missions. A plan file from an older version is converted when the overlay loads it and saved in the new format; your ticks stay. | The overlay starts without a plan. Your settings stay. |

If a file can't be read, the overlay starts without it and says so in an amber line. A single setting out of range or a colour that isn't one is corrected on its own, named in that line, and the rest of `settings.json` applies. It doesn't overwrite the file: `settings.json` stays until you move the overlay, `plan.json` until you make a new plan.

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet build
dotnet test
dotnet run --project src/CargoPlan.App
```

To try the overlay with a plan, start it with a plan file; it becomes your saved plan:

```powershell
dotnet run --project src/CargoPlan.App -- --plan tests/CargoPlan.Core.Tests/Fixtures/Plans/split_and_shared.json
```

## License

[MIT](LICENSE)
