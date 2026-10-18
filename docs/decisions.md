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
- **Decision:** Racks fill in rack order (Rack 1 first) in route order, one rack per stop while racks last. Only route-neighbours share a rack (1+2, never 1+3), and sharing is spread evenly: the planner minimises the stop count of the fullest rack, then the sum of squared stop counts per rack. On a tie, sharing goes to the lowest-numbered racks. Every shared rack produces a warning.
- **Counter-argument:** Packing neighbours whenever they fit would leave more racks free for later pickups. A tie-break by crate count in the shared rack (fewer crates are easier to tell apart) would help the player more than "share in the first racks".
- **Consequence:** Free racks stay together at the end of the rack order. The crate-count tie-break is deliberately left out (YAGNI) and can become a task if the first-racks rule hurts in practice.

## D6 – Splitting stops across racks

- **Context:** A stop larger than a rack, or a route that only fits with a split, used to fail although the cargo fits the ship. A split stop costs the player an extra rack to unload; a split item puts one commodity in two places.
- **Decision:** Split only if no plan without a split exists. Ranking: fewest split stops, then fewest split items, then the D5 criteria. The planner cuts the route as one row of crates (stops in route order, items in input order), so racks hold whole crates and a split stop fills neighbouring racks. Every rack holding part of a split stop gets a warning. `PlanException` only when the stops exceed the ship (naming the missing SCU and the first stop that doesn't fit) or whole crates don't fit behind the previous stops. Player-facing text numbers racks from 1; `Rack.Index` stays 0-based.
- **Counter-argument:** Items keep their input order; sorting crates large-first would fit some tight routes that now fail with "Crates don't fit". Splitting instead of failing hides an awkward route; the player only sees it in the warnings.
- **Consequence:** "Fewest stops in the fullest rack" isn't additive, so the planner runs its DP once per cap on stops per rack and takes the smallest cap that reaches the fewest splits. That makes the D5 criteria exact; the single pass from T2.1 wasn't guaranteed optimal for ships with many racks. Item reordering can become a task if tight routes fail in practice.

## D7 – Structured planner warnings

- **Context:** The text renderer shows warnings per rack and once in a summary; the overlay will show them on the rack. As plain strings, the rack was only part of the text ("Rack 1: Riker spans racks 1+2").
- **Decision:** `LoadPlan.Warnings` is a list of `PlanWarning(RackId, Message)`. The message reads on its own without the rack; a split stop gets the same message in every rack it spans, so the summary is a `Distinct()` over messages.
- **Counter-argument:** Plain strings were simpler and enough for a log; the renderer could parse the "Rack N" prefix instead of changing the model and the T2.2 tests.
- **Consequence:** No hidden coupling between the planner's wording and the renderer. `Validate` (T2.3) returns `PlanWarning`s too, so an overloaded rack can be highlighted. The player-facing rack number moved to `Rack.Number`, shared by planner and renderer.

## D8 – Hand-written CLI argument parsing

- **Context:** The CLI exists to try the planner on mission files: one command (`plan`), one option (`--ship`).
- **Decision:** Parse `args` with a C# list pattern in `Options.Parse`; anything else prints the usage and exits with 2. No `System.CommandLine`.
- **Counter-argument:** `System.CommandLine` brings `--help`, validation, error messages and tab completion, and scales to more commands without hand-written parsing.
- **Consequence:** No extra package for a debugging tool. If the CLI grows beyond a few commands or options, switching to `System.CommandLine` becomes a task of its own.

## D9 – Rack grid and physical position

- **Context:** Hangar data for the Hull A (Alpha 4.10): four plates on the spindle, 2 sides × 2 levels, each a cell grid of 4 × 2 × 2 (length × width × stack, 1 cell = 1 SCU). The racks don't run front to back, so the old wording was wrong, and the speculative slot model from T1.1 never matched the game.
- **Decision:** A rack has a `Grid` (`Dimensions` in cells) and a `Position` (side, level); the grid volume must equal `capacityScu`. Crate shapes are a fixed table in Core (1 = 1×1×1, 2 = 2×1×1, 4 = 2×2×1, 8 = 2×2×2, 16 = 4×2×2, 24 = 6×2×2, 32 = 4×4×2), never turned. No 3D packer: on a Hull A plate every mix up to 16 SCU fits. An 8 fills two length cells completely; flat 4s have 2 areas × 2 levels = 4 places, each 8 takes 2; 2s and 1s fill any rest; a 16 fills the plate alone. So the SCU sum check from D6 is exact for the Hull A. Shape check (`CanHold`) is per crate, not per mix – the sum argument covers the mix for Hull A; a packer stays out until a ship breaks that argument. The planner rejects a crate that fits no rack before planning.
- **Counter-argument:** Ships with irregular plates may need a real packer, and the sum argument has to be redone for every new grid. `capacityScu` is redundant to the grid. On a ship with racks of different grids, the pre-check only asks "fits any rack"; the planner could still put the crate into a rack that is too small.
- **Consequence:** Rack order stays the planning order, separate from the physical place: "neighbouring racks" in D5/D6 means neighbours in rack order, and on the Hull A Racks 2 and 3 sit on different sides. The overlay has to show rack numbers and draw racks by `Position` (T3.5). A per-rack shape check inside the planner, or a packer, becomes a task once a ship breaks the sum argument.

## D10 – Manual override: hints on move, violations on validate

- **Context:** The player can move a placement to another rack (T2.3, drag-and-drop in T3.9). Afterwards the planner's hints can be wrong: a stale "Riker spans racks 1+2" sends the player to a rack without Riker cargo, a missing "Rack 1 shared by …" puts a foreign crate on the lift platform and the hand-over is refused. A move can also overload a rack or put a crate into a rack too small for its shape.
- **Decision:** `MovePlacement(plan, ship, placement, targetRackId)` derives the hints (shared racks, split stops) anew from the placements; `Plan` uses the same derivation. It never refuses a move. `Validate(plan, ship)` returns only violations: more SCU than the rack holds, and crates whose shape the rack can't hold (`Rack.CanHold`, per crate as in D9). Precision in the planning phase, ratified by approval.
- **Counter-argument:** Letting `Validate` return hints and violations would keep the task's `MovePlacement` signature without a ship and keep all messages in one place; the UI would just always call `Validate`.
- **Consequence:** `LoadPlan.Warnings` never goes stale, and "rack in `Validate`" means "can't be loaded" (T3.9 colours it red) without parsing messages. `MovePlacement` needs the ship for rack numbers and to check the target rack. "Copy as text" shows violations only through the rack header (`22/16`). Delivered placements still count towards capacity.

## D11 – Click-through as lock mode, default interactive

- **Context:** Games with raw mouse input send clicks to the window under the hidden OS cursor, and in Star Citizen the hidden cursor moves with the view. Reproduced in-game: a crate could not be released because the hidden cursor was over a non-click-through third-party overlay. The overlay must also be clickable while loading (tick off, drag, later the editor).
- **Decision:** Click-through is a mode, `OverlayViewModel.IsLocked`, default `false` (interactive). Locked sets `WS_EX_TRANSPARENT`; unlocked removes only that bit. `WS_EX_LAYERED` stays: WPF sets it itself for `AllowsTransparency` (checked on the running window) and removing it breaks rendering. The style is applied from `SourceInitialized` on, when the HWND exists. Auto-lock (T3.3) locks while `StarCitizen.exe` is in the foreground and `CURSOR_SHOWING` is not set; a manual lock (T3.2, hotkey in T3.7) forces locked as an override: `IsLocked = Manual || Auto`.
- **Counter-argument:** Permanent click-through with a hotkey to unlock would be simpler and never in the game's way; detecting the cursor state polls Win32 and can guess wrong after a game patch.
- **Consequence:** A locked overlay can't be clicked, so unlocking needs a window of its own (T3.2) or a hotkey; until then the `--locked` start switch tests the mode and unlocking means a restart. Clicks on fully transparent pixels pass through WPF windows anyway, so only a click on the dark background proves the mode. Only read-only Win32 queries on other windows, nothing in the game process (EAC).
- **Addendum (T3.2):** The lock window is a second, owned window that never gets `WS_EX_TRANSPARENT`, so it stays clickable while the overlay is locked; a restart is no longer needed. It gets `WS_EX_NOACTIVATE` and an unfocusable button: at the freight elevator terminal the cursor is free, and a click that took the foreground would make Star Citizen lose the mouse. The overlay itself stays as in T3.1 (`DragMove` activates it); changing that is not needed yet. The lock sits inside the overlay's top-right corner but stays a window of its own: `WS_EX_TRANSPARENT` covers the whole overlay window and can't spare a button, and `HTTRANSPARENT` only forwards to windows of the same thread. Rejected: a button in the overlay that only locks, with unlocking by hotkey (needs the keyboard, pulls T3.7 forward); polling the cursor over the button (brings back the hidden-cursor problem above).
- **Addendum (T3.3):** Auto-lock reads the cursor state instead of guessing the game's menus: key heuristics (mobiGlas/Esc) can drift out of sync, as seen in-game with a third-party overlay; the cursor is the actual state. `IsLocked = IsManuallyLocked || IsAutoLocked`; the lock window and `--locked` switch only the manual lock. A `DispatcherTimer` polls every 150 ms on the UI thread: `GetForegroundWindow` (process name resolved only on a window change) and `GetCursorInfo`. Rejected: `SetWinEventHook` for foreground changes – Windows has no event for the cursor state, so polling stays anyway. While auto-locked the lock window is click-through as well (amends T3.2's "never"): the hidden cursor could sit over it too, and nobody can aim at it then. `GetCursorInfo` failing throws, except `ERROR_ACCESS_DENIED` on the secure desktop (Win+L, UAC; from experience reports, not documented), which counts as "cursor showing". Spike before the code (Star Citizen hides the cursor with `ShowCursor`; a state with `flags = 1, hCursor = 0` did not occur, so `CURSOR_SHOWING` alone decides). Precision in the planning phase, ratified by approval.

  | Phase | Foreground process | flags | hCursor |
  |---|---|---|---|
  | Main menu | StarCitizen | 1 | game's own |
  | Loading screen | StarCitizen | 0 | 0 |
  | First person / flight | StarCitizen | 0 | 0 |
  | mobiGlas | StarCitizen | 1 | game's own (changes on hover) |
  | Esc menu | StarCitizen | 1 | game's own |
  | Freight elevator terminal | StarCitizen | not logged; overlay unlocks in-game, so `CURSOR_SHOWING` is set | |
  | Typing in Windows Terminal | WindowsTerminal | 0 | 0 |

  The last row is why the foreground process is needed: other programs hide the cursor too. Counter-argument: a game patch can change how the cursor is hidden; the spike table is the reference to check against.

## D12 – App tests run on Windows only

- **Context:** `CargoPlan.App.Tests` targets `net10.0-windows`. The Linux runner compiles it (`EnableWindowsTargeting`) but can't run it: there is no `Microsoft.WindowsDesktop.App` runtime. T0.2 rejected a solution filter as a second command path.
- **Decision:** The test project sets `IsTestProject` to `false` when not building on Windows. `dotnet test` only runs projects with `IsTestProject == 'true'` (VSTest target in the SDK's `Microsoft.TestPlatform.targets`), so one command stays on both runners.
- **Counter-argument:** View models in a separate `net10.0` project would be tested on Linux too, without an MSBuild condition. Linux skips silently: a broken App test only shows in the Windows run.
- **Consequence:** `ci.yml` is unchanged; the Windows run is the only one that runs App tests. Core, Import and Cli tests run on both. If view models grow into logic that needs the Linux check, a separate project becomes a task.

## D13 – Settings in LocalAppData, saved on change

- **Context:** The overlay should stand next to the spindle at the same spot every session. Star Citizen crashes often take the whole PC down (GPU reset, driver), so saving only on a clean exit would lose the position every time. T3.1 planned a timer that re-asserts `Topmost` every 2 s against borderless games.
- **Decision:** `%LocalAppData%\SC-CargoPlan\settings.json` (`SettingsStore.DefaultPath`), not next to the EXE (Program Files needs admin rights) and not roaming `%AppData%` (window bounds belong to this machine's monitors). Model in the app, not in Core: it is UI state. `LocationChanged`/`SizeChanged` restart a 500 ms `DispatcherTimer`, which saves; `Closed` writes only what is still pending. Only in `WindowState.Normal` (minimized reports `-32000`). Listening starts at `ContentRendered`, so startup positioning is no change. Writes go to `settings.json.tmp`, then `File.Move(overwrite: true)`. Restoring clamps the bounds into the virtual desktop (`WindowBoundsRestore.Clamp`, same frame as `LockWindowPlacement`) and enforces the window's `MinWidth`/`MinHeight`. A broken file or unknown `schemaVersion` gives defaults plus a notice line in the overlay and stays untouched until the overlay is moved; a failed write shows a notice, no crash. Strict JSON options like the mission files.
- **Counter-argument:** Saving only on exit is less code and never writes during a session. Clamping to the virtual desktop instead of the nearest monitor can leave part of the window in a gap between monitors of different heights. Strict options make an older app treat a newer file as broken.
- **Consequence:** No settings dialog yet (no setting a user sets by hand); resetting means deleting the file (README). The topmost timer is dropped: in-game the overlay stayed above borderless Star Citizen (T3.1), so there is no proven problem to fix; it returns only with an issue report and a repro (TASKS.md backlog). Precision in the planning phase, ratified by approval.
