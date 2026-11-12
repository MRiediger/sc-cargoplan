# Shot list

Every image and GIF in the README and the wiki, in the order they are best recorded. Kept for every release: when the UI
changes, re-record the shots of the pages it touches.

**State of the shots:** most were recorded on 28 Oct 2026, before the perspective ship view (D83) and before hull and
racks came from the game files (D93): their ship view shows the old hull, with the Hull A's racks beside it. They show
the same behavior and stay (D85). For v0.3.0 three were recorded anew: E1 (hero), H15 (Hull B) and D5 (editor). When to
re-record the rest is the backlog line "Screenshots/GIFs auf Perspektive nachdrehen" in [`TASKS.md`](../../TASKS.md).

Each place in the wiki that shows a shot carries `![…](media/<file>)` and, below it, `<!-- SHOT: <id> -->`. The hero GIF
lives in this repo (`docs/images/hero.gif`); the wiki's Home page shows the same file by its raw URL. Every other file
goes into the wiki repo's `media/` folder.

## Conventions

- Tool: [ScreenToGif](https://www.screentogif.com/).
- Look preset **Default** for every shot, except `prefs-presets.gif`.
- GIF: at most 10 s, 15 fps, at most 3 MB per file. Record the overlay's area, not the whole screen.
- **Exception, shots with the game behind the overlay** (E1, E2): the moving background defeats ScreenToGif's
  "detect unchanged pixels", so they are shrunk with ffmpeg from the ScreenToGif export (source 2176 × 1241, 22 s).
  Keep the stop colors: a palette over the whole frame or `stats_mode=diff` with 128 colors; 64 or 96 colors lose
  green and blue.
  - `quickstart-capture-sync.gif` (wiki, 10.1 MB, allowed up to about 10 MB): 10 s from 7.5 s, crop to the contract's
    objectives and the overlay, 720 px, 8 fps, 128 colors, no dither:
    `ffmpeg -ss 7.5 -t 10 -i <export>.gif -vf "crop=1360:1000:816:60,fps=8,scale=720:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=none:diff_mode=rectangle" quickstart-capture-sync.gif`
  - `docs/images/hero.gif` (main repo; it stays in the git history, so as small as the colors allow, at most about
    6 MB): its own recording since v0.3.0, 21 s, 640 × 312 px, 8 fps, 4.8 MB. To cut a shorter one from an export, with
    the palette as above (`<start>` picked from the export):
    `ffmpeg -ss <start> -t 8 -i <export>.gif -vf "fps=6,scale=720:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=full[p];[b][p]paletteuse=dither=none:diff_mode=rectangle" hero.gif`
  - Overlay GIFs over 3 MB (`shipview-orbit-zoom.gif`, `shipview-hologram.gif`): `fps=8` (hologram 800 px wide and
    `fps=7`), then the same palette and `paletteuse` as above.
- Still states as PNG.
- File names `<page>-<action>.<ext>`, lower case, hyphens.
- No player or org names in the picture. The overlay may lie over the contract: a scan hides it for the capture (D100).
- **Test plan:** [`testplan.json`](testplan.json) (MISC Hull A, three missions from Lorville). Load it in the editor with
  **Import…** and press **Plan**, so every shot shows the same destinations and colors. Shots marked *Scan live* need a
  real contract scanned with F3, since a mission file carries no scan numbers; they show whatever contracts the
  terminal offers.
- **Prio:** `must` shots go in before the release. A missing `nice` shot is not waited for: its placeholder line and its
  `SHOT` comment are removed, the text stays.

Page prefixes: `install` Installation-and-first-start · `quickstart` Quick-start-at-the-cargo-elevator · `overlay`
Overlay-basics · `editor` Plan-and-editor · `shipview` Ship-view · `legend` Legend-and-delivery-states · `prefs`
Preferences · `ships` Ships-and-cargo-grids · `scan` Scanning-contracts (from v0.4.0).

## The test plan

| Stop | Destination | Commodity | SCU | Max size | Crates | Rack |
|---|---|---|---|---|---|---|
| 1 | Everus Harbor | Titanium | 12 | 4 | 3×4 | 1 |
| 1 | Everus Harbor | Stims | 3 | 2 | 2+1 | 1 |
| 2 | HDMS-Edmond | Agricium | 8 | 4 | 2×4 | 2 |
| 3 | HDMS-Thedus | Quartz | 6 | 4 | 3×2 (typed as 2+2+2, italic) | 3 |

Pickup Lorville for every row, fill order Bow first. 29 of 64 SCU; rack 4 stays empty for drags.

## Desktop (no game needed)

| Id | Prio | File | Type | Page | What happens (start → end) | Max. | State |
|---|---|---|---|---|---|---|---|
| D1 | nice | `install-smartscreen.png` | PNG | Installation-and-first-start | SmartScreen dialog after **More info**, **Run anyway** visible | – | Windows' own dialog; re-record only when it changes |
| D2 | nice | `install-empty-overlay.png` | PNG | Installation-and-first-start | Overlay without a plan: empty Hull A, "No plan. Open the editor …", **Open editor** | – | No plan (Discard plan) |
| D5 | must | `editor-rows.png` | PNG | Plan-and-editor | Editor with the four test plan rows: header **Max size** whole, crates grouped (`3×4`), Quartz `3×2` in italics, the **Layout** line and **Plan** visible; dark theme, one row selected (calm fill, teal bar); re-recorded for v0.3.0 | – | Test plan imported, before **Plan** |
| D6 | nice | `editor-import-plan.gif` | GIF | Plan-and-editor | **Import…** → pick `testplan.json` → rows appear → **Plan** → overlay shows three racks | 10 s | Empty rows (**Clear rows**) |
| D7 | must | `prefs-window.png` | PNG | Preferences | Settings window from the top: Preset, Export…, Import…, first sections; footer with the version in a second shot or scrolled | – | Preset Default |
| D8 | must | `prefs-presets.gif` | GIF | Preferences | Preset Default → High contrast → Minimal → Default, overlay visible next to the window | 8 s | Test plan |
| D9 | nice | `prefs-color-picker.gif` | GIF | Preferences | Color picker opens on **Text color** of the Legend section | – | Test plan |
| D10 | nice | `prefs-hotkeys.png` | PNG | Preferences | Hotkeys section, one hotkey set (e.g. Lock / unlock `Ctrl+Alt+K`), the rest **Not set** | – | Clear the hotkey afterwards if you don't use it |
| D12 | nice | `delete-mission.gif` | GIF | Plan-and-editor | Select a row → **Delete mission** → the mission leaves the plan and the overlay at once | 10 s | Test plan |

## Hangar (in the game, ship on the pad)

| Id | Prio | File | Type | Page | What happens (start → end) | Max. | State |
|---|---|---|---|---|---|---|---|
| H1 | must | `overlay-header.png` | PNG | Overlay-basics | Header unlocked with every button; Undo visible (do one move before) | – | Test plan, one move done |
| H2 | must | `overlay-locked.png` | PNG | Overlay-basics | Same, padlock locked: buttons hidden, amber padlock, ship name and numbers stay | – | Test plan |
| H3 | must | `ships-select.gif` | GIF | Ships-and-cargo-grids | Click ship name → list with Recent → type `hull` → pick MISC Hull A | 8 s | Test plan, another ship in Recent |
| H4 | nice | `shipview-orbit-zoom.gif` | GIF | Ship-view | Left drag turns, wheel zooms in and out, double-click next to the cargo resets | 10 s | Test plan |
| H5 | must | `shipview-move-crate.gif` | GIF | Ship-view | Right drag a Titanium crate from rack 1 to rack 4, cyan ghost, release | 6 s | Test plan |
| H6 | must | `shipview-rotate-crate.gif` | GIF | Ship-view | Right drag a 2 SCU Quartz crate, one wheel notch turns it, release | 6 s | Test plan |
| H7 | nice | `shipview-select.png` | PNG | Ship-view | One Titanium crate right-clicked: magenta edge, its mission's crates white, the rest dimmed, info line "from Lorville · Titanium · to Everus Harbor" | – | Test plan |
| H8 | nice | `shipview-legend-drag.gif` | GIF | Ship-view | Right drag the HDMS-Edmond legend line onto rack 4, ghosts, release | 8 s | Test plan |
| H9 | nice | `shipview-make-room.gif` | GIF | Ship-view | Drop a 4 SCU Agricium crate into rack 1 (15 of 16 SCU), other crates move out, amber line "Moved … to make room" | 8 s | Test plan |
| H10 | nice | `shipview-split-merge.gif` | GIF | Ship-view | Select a 4 SCU Agricium crate → Split → 2+2 → select both → Merge → 4 | 10 s | Test plan |
| H11 | nice | `shipview-outline.png` | PNG | Ship-view | Hull A with the outline backdrop | – | Test plan, Backdrop Outline |
| H12 | nice | `shipview-hologram.gif` | GIF | Ship-view | Hull A with **Hologram – shaded** | 10 s | Test plan, Backdrop Hologram – shaded; set Outline back |
| H13 | nice | `legend-overview.png` | PNG | Legend-and-delivery-states | Legend with "From Lorville" headers, check boxes, the Everus Harbor line with two commodities | – | Test plan |
| H14 | nice | `legend-checkboxes.gif` | GIF | Legend-and-delivery-states | Tick the box of the HDMS-Edmond line → its crates selected → tick the header's box → whole mission | 8 s | Test plan |
| H15 | must | `docs/images/ships-hull-b.png` (main repo; the wiki shows it by its raw URL) | PNG | README, Ships-and-cargo-grids | MISC Hull B with **Hologram – shaded**, turned so the tilted racks on both sides of the spindle show, crates in them | – | MISC Hull B with any plan; set Outline back |

## Cargo elevator (in the game, at the freight elevator)

| Id | Prio | File | Type | Page | What happens (start → end) | Max. | State |
|---|---|---|---|---|---|---|---|
| E1 | must | `docs/images/hero.gif` (main repo) | GIF | README, Home | MISC Hull A next to the contract in mobiGlas: empty racks → the captured contract's crates appear → legend lines ticked off | 21 s | *Scan live*, own recording (see Conventions); new for v0.4.0 (T10.6) |
| E2 | must | `quickstart-capture-sync.gif` | GIF | Quick-start-at-the-cargo-elevator | F3 at the terminal → a moment later a new mission with `#n` header appears in the legend | 10 s | *Scan live*; shot again for v0.4.0 (the old one shows the import) |
| E3 | must | `quickstart-mark-loaded.gif` | GIF | Quick-start-at-the-cargo-elevator | Click a legend line → `▣`, white crate edges, header `· N ▣` | 6 s | Test plan |
| E4 | must | `quickstart-deliver.gif` | GIF | Quick-start-at-the-cargo-elevator | Click a crate of HDMS-Edmond → all its crates gray, lines struck through | 6 s | Test plan, HDMS-Edmond loaded |
| E5 | must | `legend-states.png` | PNG | Legend-and-delivery-states | Legend with one planned, one loaded and one delivered line | – | Test plan: Everus Harbor planned, HDMS-Edmond loaded, HDMS-Thedus delivered |
| E6 | nice | `legend-markings.png` | PNG | Legend-and-delivery-states | A legend line with a mark such as `4 SCU?` or `⚠ 1 of 3 destinations` | – | *Scan live*, whenever a scan shows one |

**Total:** 30 shots, 17 GIF and 13 PNG; 14 must (8 GIF, 6 PNG), 16 nice. `legend-states.png` and
`legend-markings.png` are one picture on purpose: it shows both states and a `?` mark. D3, D4 and D11 showed the
settings section of the import that went in T10.4a.
