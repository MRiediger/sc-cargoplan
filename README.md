# SC-CargoPlan

[![CI](https://github.com/MRiediger/sc-cargoplan/actions/workflows/ci.yml/badge.svg)](https://github.com/MRiediger/sc-cargoplan/actions/workflows/ci.yml)

A cargo load-plan overlay for Star Citizen hauling (Alpha 4.10.x).

## What

You enter your hauling missions (destination, commodity, SCU, crate size). SC-CargoPlan decides which destination goes into which section of your ship and shows the plan as a translucent, click-through, always-on-top overlay. At each stop you tick off what you delivered.

## Why

Covalex contracts often go from one pickup to several destinations. Each crate is bound to a mission and a destination, but the game doesn't show which crate goes where. The freight elevator rejects the whole delivery as soon as one foreign crate is on the platform.

SC-CargoPlan doesn't try to recognize crates. It **plans the loading up front**: first stop in front, last stop at the back, one destination per rack. Load by the plan, unload by the plan.

The overlay is a separate window. It does not touch the game process, so it's safe with Easy Anti-Cheat.

## Download

_Coming soon: a single Windows EXE on the [Releases](../../releases) page._

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet build
dotnet test
dotnet run --project src/CargoPlan.App
```

## License

[MIT](LICENSE)
