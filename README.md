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

**Never eats your tractor-beam click:** the overlay becomes click-through automatically while Star Citizen has the mouse. In flight and first person the game hides the cursor and it follows your view, so a click over a normal window would be lost. As soon as the cursor shows (mobiGlas, Esc menu, terminals) or another program is in front, the overlay is interactive again: you can click and drag it.

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
