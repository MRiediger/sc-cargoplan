# SC-CargoPlan

[![CI](https://github.com/MRiediger/sc-cargoplan/actions/workflows/ci.yml/badge.svg)](https://github.com/MRiediger/sc-cargoplan/actions/workflows/ci.yml)

A cargo load-plan overlay for Star Citizen hauling (Alpha 4.10.x).

## What

You enter your hauling missions (destination, commodity, SCU, crate size). SC-CargoPlan decides which destination goes into which section of your ship and shows the plan as a translucent, always-on-top overlay that turns click-through while you fly. At each stop you tick off what you delivered.

## Why

Covalex contracts often go from one pickup to several destinations. Each crate is bound to a mission and a destination, but the game doesn't show which crate goes where. The freight elevator rejects the whole delivery as soon as one foreign crate is on the platform.

SC-CargoPlan doesn't try to recognize crates. It **plans the loading in advance**: the first stop goes into Rack 1, the next stops into the following racks, one destination per rack. Load by the plan, unload by the plan.

The overlay is a separate window. It does not touch the game process, so it's safe with Easy Anti-Cheat.

## Download

_Coming soon: a single Windows EXE on the [Releases](../../releases) page._

## Usage

Run Star Citizen in **Borderless** window mode. In exclusive fullscreen the game covers every other window, the overlay included.

_Screenshot: coming soon._

**Reading the ship view:** the overlay draws your ship's cargo racks as boxes, seen from the front, from the port side and from above: the bow points to the lower left, the port side faces you. Each rack shows its number on the front face. Each destination has its own color, and the legend below lists it with its SCU. A destination split over two racks has the same color in both. A rack shared by two destinations shows both colors side by side; the planner's hints below the legend tell you which racks need care.

- **Color** = destination. Up to eight colors, from the ninth destination on they repeat (the legend says so).
- **Gray** = delivered.
- **Outline only** = free space.
- **Red edges** = this rack can't be loaded as planned (too much cargo, or a crate that doesn't fit).

Hover over a colored area to see what goes there, e.g. `Rack 1 · port upper · 16 SCU Titanium (4×4)` (4 crates of 4 SCU). Scroll the mouse wheel to zoom, drag inside the view to move it, double-click to fit the whole ship again. To move the overlay window, drag it by its header (the ship name), the legend or the border.

**Never eats your tractor-beam click:** the overlay becomes click-through automatically while Star Citizen has the mouse. In flight and first person the game hides the cursor and it follows your view, so a click over a normal window would be lost. As soon as the cursor shows (mobiGlas, Esc menu, terminals) or another program is in front, the overlay is interactive again: you can zoom, hover and move it.

The small padlock in the overlay's top-right corner locks it by hand: the overlay stays click-through even with the cursor showing, so every click goes to the game. Click the padlock again to unlock. A closed amber padlock means locked, an open white one unlocked. Clicking the padlock doesn't take the focus from the game.

Start with `--locked` to begin with the padlock locked.

**Remembers where you put it:** drag the overlay next to your cargo grid and resize it once; position and size are saved right away, so they survive a restart and even a crash. They live in `%LocalAppData%\SC-CargoPlan\settings.json`. To reset the overlay to its default size and position, close it and delete that file. If you unplug a monitor, the overlay moves back onto the screens you have.

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet build
dotnet test
dotnet run --project src/CargoPlan.App
```

## License

[MIT](LICENSE)
