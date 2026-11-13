# Shot list

Every image and GIF in the README and the wiki, in the order they are best recorded. Kept for every release: when the UI
changes, re-record the shots of the pages it touches.

**State of the shots:** most were recorded on 28 Oct 2026, before the perspective ship view (D83) and before hull and
racks came from the game files (D93): their ship view shows the old hull, with the Hull A's racks beside it. They show
the same behavior and stay (D85). For v0.3.0 H15 (Hull B) was recorded anew; for v0.4.0 E1 (hero), E2 (scan), D5
(editor), D7 and D10 (settings), and E7 was cut from the E2 recording. When to
re-record the rest is the backlog line "Screenshots/GIFs auf Perspektive nachdrehen" in [`TASKS.md`](../../TASKS.md).

Each place in the wiki that shows a shot carries `![…](media/<file>)` and, below it, `<!-- SHOT: <id> -->`. The hero GIF
lives in this repo (`docs/images/hero.gif`); the wiki's Home page shows the same file by its raw URL. Every other file
goes into the wiki repo's `media/` folder.

## Conventions

- Tool: [ScreenToGif](https://www.screentogif.com/).
- Look preset **Default** for every shot, except `prefs-presets.gif`.
- GIF: at most 10 s, 15 fps, at most 3 MB per file. Record the overlay's area, not the whole screen.
- **Exception, shots with the game behind the overlay** (E1, E2): the moving background defeats ScreenToGif's
  "detect unchanged pixels", so they are shrunk with ffmpeg from the ScreenToGif export (v0.4.0: contract and overlay,
  1367 × 796 px, 14 fps) in two passes with the same filter chain, palette first, then the GIF:
  ```
  F="fps=8,scale=720:-1:flags=lanczos,hqdn3d=3:2:9:6"
  ffmpeg -i <export>.gif -vf "$F,palettegen=max_colors=96:stats_mode=diff" pal.png
  ffmpeg -i <export>.gif -i pal.png -lavfi "$F[x];[x][1:v]paletteuse=dither=none:diff_mode=rectangle" <shot>.gif
  ```
  `hqdn3d` (light in space, strong in time) takes out the game's background noise so still areas stay still; without
  it the GIF is about 6× larger, and with `mpdecimate` plus `gifsicle --lossy` (tried for v0.4.0) the background
  streaks. `dither=none`, since dither brings the noise back. Over 5 MB: `scale=640:-1`. The price: one palette for the
  whole GIF has no room for small light-blue crates next to the gray background, they turn grayish (measured: crate
  78,142,178 → 127,136,148; 128 or 160 colors, `stats_mode=full` or a palette per frame don't change it); orange,
  amber and the text stay.
  - `docs/images/hero.gif` (main repo; it stays in the git history, so under 5 MB): v0.4.0, the E1 export (scan #1,
    offer → accepted), 18.0 s, 720 × 419 px, 144 frames, 4.4 MB.
  - `quickstart-capture-sync.gif` (wiki, up to about 10 MB): the E2 export, the same two passes; 16.5 s, 132 frames,
    3.9 MB.
  - Overlay GIFs over 3 MB (`shipview-orbit-zoom.gif`, `shipview-hologram.gif`): `fps=8` (hologram 800 px wide and
    `fps=7`), then `palettegen=max_colors=128:stats_mode=diff` and the same `paletteuse`, without `hqdn3d` (no game
    behind them).
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
| D5 | must | `editor-rows.png` | PNG | Plan-and-editor | Editor with the four test plan rows: the **Scan** button, header **Max size** whole, crates grouped (`3×4`), Quartz `3×2` in italics, the **Layout** line and **Plan** visible; dark theme, one row selected (calm fill, teal bar); re-recorded for v0.4.0 | – | Test plan imported, before **Plan** |
| D6 | nice | `editor-import-plan.gif` | GIF | Plan-and-editor | **Import…** → pick `testplan.json` → rows appear → **Plan** → overlay shows three racks | 10 s | Empty rows (**Clear rows**) |
| D7 | must | `prefs-window.png` | PNG | Preferences | Settings window from the top: Preset, Export…, Import…, first sections, footer with the version; re-recorded for v0.4.0 | – | Preset Default |
| D8 | must | `prefs-presets.gif` | GIF | Preferences | Preset Default → High contrast → Minimal → Default, overlay visible next to the window | 8 s | Test plan |
| D9 | nice | `prefs-color-picker.gif` | GIF | Preferences | Color picker opens on **Text color** of the Legend section | – | Test plan |
| D10 | must | `prefs-hotkeys.png` | PNG | Preferences | Hotkeys section: the rule "Ctrl, Alt or Win required, except for F keys", **Scan contract** `F3` with its hint below, some hotkeys set; re-recorded for v0.4.0 | – | Clear the hotkeys afterwards if you don't use them |
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
| E1 | must | `docs/images/hero.gif` (main repo) | GIF | README, Home | MISC Hull A next to an offer in the mobiGlas, racks empty → F3 → `Scanned #1 · …`, crates appear → accept → F3 → `Updated #1 · quantities changed` | 18 s | *Scan live*, empty plan; recorded for v0.4.0 (see Conventions) |
| E2 | must | `quickstart-capture-sync.gif` | GIF | Quick-start-at-the-cargo-elevator | Continues E1: a second offer → F3 → `Scanned #2 · …`, the mission `#2` appears in the legend → accept → F3 → `Updated #2 · quantities changed` | 17 s | *Scan live*, right after E1; recorded for v0.4.0 |
| E3 | must | `quickstart-mark-loaded.gif` | GIF | Quick-start-at-the-cargo-elevator | Click a legend line → `▣`, white crate edges, header `· N ▣` | 6 s | Test plan |
| E4 | must | `quickstart-deliver.gif` | GIF | Quick-start-at-the-cargo-elevator | Click a crate of HDMS-Edmond → all its crates gray, lines struck through | 6 s | Test plan, HDMS-Edmond loaded |
| E5 | must | `legend-states.png` | PNG | Legend-and-delivery-states | Legend with one planned, one loaded and one delivered line | – | Test plan: Everus Harbor planned, HDMS-Edmond loaded, HDMS-Thedus delivered |
| E6 | nice | `legend-markings.png` | PNG | Legend-and-delivery-states | A legend line with a mark such as `4 SCU?` or `⚠ 1 of 3 destinations` | – | *Scan live*, whenever a scan shows one |
| E7 | nice | `legend-scan-headers.png` | PNG | Legend-and-delivery-states | Two scanned contracts in the legend, headers `From Everus Harbor · #1` and `#2`, amber line `Updated #2 · quantities changed` | – | Cut from the last frame of the E2 export at full size (`crop=360:165:792:366`) |

**Total:** 31 shots, 17 GIF and 14 PNG; 15 must (8 GIF, 7 PNG), 16 nice. `legend-states.png` and
`legend-markings.png` are one picture on purpose: it shows both states and a `?` mark. D3, D4 and D11 showed the
settings section of the import that went in T10.4a.
