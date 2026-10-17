# Decisions

Architecture decisions with their trade-offs, numbered in the order they were made.
A decision changes only through a new entry that supersedes the old one.

## D1 – .NET 10

- **Context:** The app needs a supported runtime for years; users should not hit an end-of-life runtime.
- **Decision:** Target .NET 10 (LTS, supported until November 2028).
- **Counter-argument:** .NET 8 has more existing tutorials and wider installed runtimes, which helps the small framework-dependent build.
- **Consequence:** .NET 8 support ends November 2026, so starting on it would force a migration right after release. The self-contained EXE makes the installed runtime irrelevant for most users.

## D2 – WPF for the overlay

- **Context:** The overlay must be a borderless, transparent, always-on-top window with click-through, which needs the Win32 window handle.
- **Decision:** WPF (`AllowsTransparency`, `WindowInteropHelper` for the HWND).
- **Counter-argument:** WinUI 3 is Microsoft's newer UI stack; Avalonia would run on Linux and macOS.
- **Consequence:** WinUI 3 handles transparent windows poorly, and Star Citizen runs on Windows only, so cross-platform buys nothing. WPF forces software rendering with `AllowsTransparency` and can't be trimmed; both are acceptable for a small overlay. The app project is Windows-only; Core stays `net10.0`.

## D3 – Immutable records for the domain model

- **Context:** Planner, overlay and import pass ships, stops and plans around; accidental mutation would make plans inconsistent.
- **Decision:** Domain types are `sealed record`s with `IReadOnlyList`/`IReadOnlySet` and constructor validation; changes go through `with`.
- **Counter-argument:** Mutable classes are simpler to bind in WPF and need no copies when one value changes.
- **Consequence:** Value equality makes tests short and plans comparable. Validated properties are get-only so `with` can't bypass validation. The UI needs view models instead of binding the records directly.

## D4 – Single-file EXE distribution

- **Context:** Haulers should start the overlay without installing anything.
- **Decision:** Publish a self-contained single-file EXE (~70 MB), plus a small framework-dependent EXE (~3 MB) for users who have the .NET Desktop Runtime.
- **Counter-argument:** An installer (e.g. MSIX, Velopack) would add auto-update and a Start menu entry; one framework-dependent build would be much smaller.
- **Consequence:** Downloads are large because WPF can't be trimmed. Unsigned EXEs trigger SmartScreen warnings, which the README has to explain. Auto-update is a later evaluation (T6.2).

## D5 – Planner rack sharing

- **Context:** A cargo lift refuses the whole hand-over if a foreign crate is on the platform, so mixing destinations in one rack costs the player time. With more stops than racks, mixing can't be avoided.
- **Decision:** Racks fill from the front in route order, one rack per stop while racks last. Only route-neighbours share a rack (1+2, never 1+3), and sharing is spread evenly: the planner minimises the stop count of the fullest rack, then the sum of squared stop counts per rack. On a tie, sharing goes to the front racks. Every shared rack produces a warning.
- **Counter-argument:** Packing neighbours whenever they fit would leave more racks free for later pickups. A tie-break by crate count in the shared rack (fewer crates are easier to tell apart) would help the player more than "share at the front".
- **Consequence:** Free racks stay together at the back. The crate-count tie-break is deliberately left out (YAGNI) and can become a task if the front rule hurts in practice.
