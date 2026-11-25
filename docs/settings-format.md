# Settings file format

`%LocalAppData%\SC-CargoPlan\settings.json` holds the overlay's position and size, your hotkeys, where
the legend stands, the overlay's look, the tool windows' theme, your ship, the ships you chose last and the ship view as you left it on each. Position and size
are saved whenever you move the overlay; the settings window (the gear in the header) sets and saves the look,
the legend layout, the ship view (outline or hologram, its opacity and colours, hologram downloads, how far it zooms), the tool window theme and the hotkeys, and loads or saves a look as a [look file](#look-files); the ship name in the overlay header sets the ship and the recent ships. You can still edit the file by hand: close the overlay, edit the file, start
the overlay again.

## Example with every default

Without a file, or without `ship`, `recentShips`, `legend`, `shipOutline`, `downloadShipHolograms`, `holoOpacity`,
`holoHullColor`, `holoEdgeColor`, `shipView`, `updates`, `shipData`, `routes`, `toolWindowTheme` and `appearance`, the overlay uses exactly
these values.

```json
{
  "schemaVersion": 3,
  "ship": null,
  "recentShips": null,
  "overlay": null,
  "hotkeys": null,
  "legend": { "visible": true, "position": "bottom-left", "maxHeightShare": 0.5 },
  "shipOutline": "outline",
  "downloadShipHolograms": true,
  "holoOpacity": 0.4,
  "holoHullColor": "#FF9CB0C0",
  "holoEdgeColor": "#FFE6E6E6",
  "shipView": { "maxZoomPxPerCell": 160 },
  "updates": { "checkOnStartup": true, "skippedVersion": null },
  "shipData": { "checkOnStartup": true, "skippedVersion": null },
  "routes": { "tab": "cargo", "rank": "master", "faction": null, "hub": "Everus Harbor", "loopsSort": "hour", "planBlock": "open", "useMeasured": true, "times": { "station": 10, "crate": 20, "jump": 4 } },
  "toolWindowTheme": "system",
  "appearance": {
    "window": {
      "background": "#A0141414",
      "borderColor": "#FFE6E6E6",
      "borderThickness": 0
    },
    "header": {
      "foreground": "#FFE6E6E6",
      "halo": { "enabled": false, "color": "#E0141414", "size": 3 }
    },
    "legend": {
      "foreground": "#FFE6E6E6",
      "fontSize": null,
      "checkBoxColor": "#FFB0B0B0",
      "checkMarkColor": "#FFB0B0B0",
      "halo": { "enabled": false, "color": "#E0141414", "size": 3 }
    },
    "crates": {
      "fillAlpha": 208,
      "edgeAlpha": 192,
      "rackNumberColor": "#FFFFFFFF",
      "rackNumberFontSize": 27,
      "stopPalette": "okabeIto",
      "selectionColor": "#FFFF00FF",
      "selectionWidth": 2.5,
      "halo": { "enabled": true, "color": "#E0141414", "size": 3 },
      "selectionOutline": { "enabled": true, "color": "#A0000000", "size": 1 }
    },
    "buttons": {
      "background": "#01000000",
      "borderColor": "#FFE6E6E6",
      "borderThickness": 0,
      "cornerRadius": 3,
      "halo": { "enabled": false, "color": "#E0141414", "size": 3 }
    }
  }
}
```

`overlay` and `hotkeys` are described in the wiki ([Overlay basics](https://github.com/MRiediger/sc-cargoplan/wiki/Overlay-basics#hotkeys)); `replan` lays out the cargo on board anew like the
editor's Re-plan, `discardPlan` empties the cargo like the editor's Discard cargo (Discard plan before v0.8.0; the key keeps its name), `undo` and `redo` go back and forward like the header's
↶ and ↷, `resetView` fits the ship view like the header's reset next to the gear, `scanContracts` reads the contract
open in the mobiGlas into the plan like the editor's Scan button. In `hotkeys`, `null` and a missing entry both mean
"no hotkey"; the settings window writes `null` when you clear one, so you see the entry exists. The one exception is
`scanContracts`: missing means **F3**, `null` means off (D104). With `null` (or a key that isn't valid) the OCR doesn't
load at start; the editor's Scan button still works and loads it on its first press, setting a key in the settings
window loads it at once (D108). A hotkey needs Ctrl, Alt or Win unless it is an F key
(F1–F11, F13–F24); F12 and Space are reserved. Hotkeys work while Star Citizen or CargoPlan is in front, and the keys
reach the game too, so pick a combination Star Citizen doesn't use (D97). Every section and every value
in `appearance` is optional: what you leave out keeps its default. `{ "schemaVersion": 3, "appearance":
{ "legend": { "fontSize": 14 } } }` changes only the legend's font size.

## Colours and transparency

Colours are `"#AARRGGBB"` (alpha, red, green, blue) or `"#RRGGBB"` (fully opaque), hex digits in any case.
**Transparency is the alpha of the colour:** `00` is invisible, `FF` is opaque, `80` is about half.
The overlay's background `#A0141414` is a dark grey at 63 %. The crates' colours are the stop colours from
`crates.stopPalette`, one of the checked sets below, never free colours; so their alpha is a number of its own: `0`
(invisible) to `255` (opaque).

## Values

| Key | What | Default | Range |
|---|---|---|---|
| `window.background` | Background of the overlay | `#A0141414` | alpha at least `01` |
| `window.borderColor` | Frame around the overlay | `#FFE6E6E6` | |
| `window.borderThickness` | Width of that frame in pixels, `0` = none | `0` | 0–10 |
| `header.foreground` | Ship name and the glyphs (split, merge, re-plan, pencil, undo, redo, reset view, gear, exit, padlock) | `#FFE6E6E6` | |
| `header.halo` | Halo around ship name and "All stops delivered." | off | |
| `legend.foreground` | Stop lines; also the outline of "Open editor" | `#FFE6E6E6` | |
| `legend.fontSize` | Font size of the legend, warnings and notices | `null` | 8–32 |
| `legend.halo` | Halo around the legend's text | off | |
| `legend.checkBoxColor` | Frame of the check boxes that select a line's crates | `#FFB0B0B0` | |
| `legend.checkMarkColor` | Tick (all selected) and bar (part selected) inside the box | `#FFB0B0B0` | |
| `crates.fillAlpha` | Opacity of the crates in the ship view | `208` | 0–255 |
| `crates.edgeAlpha` | Opacity of the crates' edges | `192` | 0–255 |
| `crates.rackNumberColor` | Colour of the rack numbers and the "Bow" label | `#FFFFFFFF` | |
| `crates.rackNumberFontSize` | Size of the rack numbers and the "Bow" label in pixels, the same at every zoom | `27` | 8–48 |
| `crates.halo` | Outline of the rack numbers and the "Bow" label | on | |
| `crates.stopPalette` | The set of stop colours, see below | `"okabeIto"` | |
| `crates.selectionColor` | Edge of the crates you selected with a right click | `#FFFF00FF` | |
| `crates.selectionWidth` | Width of that edge in pixels | `2.5` | 1–8 |
| `crates.selectionOutline` | Dark contour under that edge, a [halo](#halo) whose `size` is its width on each side | on, `#A0000000`, `1` | |
| `buttons.background` | Background of the glyph buttons and "Open editor" | `#01000000` | alpha at least `01` |
| `buttons.borderColor` | Frame of the glyph buttons | `#FFE6E6E6` | |
| `buttons.borderThickness` | Width of that frame, `0` = none | `0` | 0–10 |
| `buttons.cornerRadius` | Rounded corners of the buttons | `3` | 0–13 |
| `buttons.halo` | Halo around the glyphs and the button text | off | |

**`legend.fontSize`: `null` is the default and means "the Windows font size"** (the size the legend had
before it was a setting; Windows sets it, often 11 or 12). A number overrides it. `0` does *not* mean
"system": it is outside the range, becomes 8 and shows a notice.

**`crates.rackNumberFontSize` stays the same while you zoom.** Only when you zoom far out or make the
overlay small, so that a number would be taller than its rack, does it shrink with the racks, down to 12.
The "Bow" label lies flat on the floor of the bow wedge, its capitals as tall on the screen as the rack
numbers' digits, shrinking included; only where the floor has no room for that does it stay smaller. Only a ship without
an outline has the wedge and the label (see [Ship view](#ship-view)).

**`crates.stopPalette`:** each set is documented as safe with the common kinds of colour blindness by its source;
colours that vanish on the dark overlay or look like delivered grey were left out.

| Value | Colours |
|---|---|
| `"okabeIto"` | 8: Okabe & Ito's palette without black, with white. The default, the colours before this was a setting. |
| `"tolBright"` | 6: Paul Tol's bright scheme without its grey |
| `"tolMuted"` | 7: Paul Tol's muted scheme without indigo and wine |

Exactly as written (camelCase). Missing means `"okabeIto"`; any other text means `"okabeIto"` too, with a notice. With
more destinations than colours the colours repeat hatched "/", then hatched "\", then all of it again; every destination
keeps its place in the order when you switch.

**Alpha at least `01`:** the overlay passes clicks on fully transparent pixels to the window below. With a
background of alpha `00`, you could no longer move the overlay or press a button, so the overlay uses `01`
instead (still invisible) and says so. The legend's check boxes may go down to `00`: the box keeps an invisible
background of its own that takes the click.

## Halo

A halo is a soft glow around text and glyphs, for reading over bright and dark scenes alike.

| Key | What | Default | Range |
|---|---|---|---|
| `enabled` | On or off; off keeps colour and size for later | `false` (crates and selection outline: `true`) | |
| `color` | Colour of the glow, alpha included | `#E0141414` | |
| `size` | Blur radius in pixels; for the rack numbers the width of their outline | `3` | 1–20 |

Inside a `halo`, missing keys take the defaults of this table, not those of the section: a crates halo
written as `{ "size": 5 }` is off. Write `"enabled": true` with it. The same holds for `crates.selectionOutline`.

## Legend layout

`legend` sits at the top level, next to `appearance`, not inside it: it says where the legend stands, while
`appearance.legend` says how it looks. A `position` inside `appearance.legend` is an unknown key and makes
the whole file unreadable.

| Key | What | Default |
|---|---|---|
| `legend.visible` | Show the stop lines | `true` |
| `legend.position` | Where the legend stands | `"bottom-left"` |
| `legend.maxHeightShare` | Share of the overlay's height the stop lines take at most before they scroll, 0.2–0.8 | `0.5` |

**Positions:** `top-left`, `top-center`, `top-right`, `bottom-left`, `bottom-center`, `bottom-right`, in
lower case. `top` puts the legend between the header and the ship, `bottom` below the ship. `left`,
`center` and `right` move the legend as a block, as wide as its longest line; inside, the lines stay
left-aligned, so the colour squares stand in one column. Amber notices always stand at the very bottom,
across the full width, wherever the legend is.

**`"visible": false` hides only the stop lines** and their mission headers with their check boxes. Red warnings stay
where the legend is, amber notices at the bottom. You still tick a destination by clicking its crates in the ship view.
**Dragging a legend line onto a rack is gone with the lines:** to move a mission's crates, right click each of them and
right drag the selection.

Ticking a destination off never moves the ship view, in any position.

**Many missions: the stop lines scroll.** They take what they need, at most `maxHeightShare` of the overlay's height
(0.5 = half); beyond that the mouse wheel over them scrolls, and a slim bar in the legend's colour shows where you are.
The ship view keeps the rest. Missions that don't fit, red warnings and the colour hint stand below the lines and never
scroll out of sight. While the overlay is locked it takes no mouse, so unlock it to scroll.

## Ship view

`shipOutline` sets what goes with the racks in the ship view; `holoOpacity`, `holoHullColor`, `holoEdgeColor` and
`downloadShipHolograms` go with it. All of them sit at the top level, so the crates' **Reset to defaults** never switches them; the settings window's
**Ship view** section has a reset of its own, which leaves `downloadShipHolograms` as it is.

| `shipOutline` | What |
|---|---|
| `"outline"` | The ship's outline seen from above, under the racks: bow, sides and shape. A ship without an outline in the ship data's `outlines.json` shows the bow wedge with "Bow" on its floor. The default. |
| `"holo-shaded"` | The ship's 3D hologram around the racks, true to scale, softly lit from the camera. Until it is there, and for a ship without one, the outline. |
| `"holo-flat"` | The same, every face in one shade. |
| `"holo-hiddenline"` | The same as a dark hull with its visible edges as lines; zooming in shows more edges (from 1.5× and 3×). |
| `"none"` | Racks only, no outline, no wedge, no label. |

Lower case only. Missing or `null` means `"outline"`; any other text means `"outline"` too, with a notice.

`holoOpacity` is the hologram's opacity, from `0.01` to `1` (the settings window moves it in steps of 1 %); missing
means `0.4`. A value outside is clamped, with a notice. Builds before T8.5 clamp a value under `0.1` to `0.1`.

`holoHullColor` colours the hologram's hull in `"holo-shaded"` and `"holo-flat"` (default `"#FF9CB0C0"`),
`holoEdgeColor` its edges in `"holo-hiddenline"` (default `"#FFE6E6E6"`); the hidden-line hull stays dark, it only hides
what lies behind it. `"#RRGGBB"` or `"#AARRGGBB"`, written back as `"#FFRRGGBB"`: a hologram is always opaque, its
opacity is `holoOpacity`, so an alpha below `FF` is set to `FF` with a notice. Not a colour, missing or `null` means the
default, with a notice for one that isn't a colour.

`downloadShipHolograms`: `true` (the default) lets a hologram look download the ship's file from the release of the ship
data in use on GitHub (`sc-cargoplan-shipdata`, e.g. `ships-2026.11.1`) when you pick a ship, one file per ship; the list
of files comes with the ship data, so nothing else is asked for (D114). Nothing else is sent. `false` means no hologram
downloads at all (the update check and the ship data check have their own switches, `updates.checkOnStartup` and
`shipData.checkOnStartup`); holograms already downloaded still show. They are kept in
`%LocalAppData%\SC-CargoPlan\holo\<ship data version>\`, e.g. `holo\ships-2026.11.1\` ([format](holo-format.md)). Only
the folder of the ship data in use stays (D95): another one, such as `holo-v3` of v0.5.1 or an older ship data version,
goes at the first hologram request, and the overlay says once that the holograms download again; with downloads off, a
look without hologram or no holograms on offer it goes at once and nothing is said. When the ship data offer no
holograms (or none in a format this version reads), every ship shows its outline and the settings window says so under
the switch, e.g. `Holograms: not offered by ship data 2026.11.2`.

`shipView` holds the zoom and the view per ship. Missing or `null` means the defaults.

| Key | What | Default | Range |
|---|---|---|---|
| `shipView.maxZoomPxPerCell` | How far the mouse wheel zooms in: pixels per cell (1 SCU, 1.25 m) in the plane the view turns around, the same on every ship, so a Hull C zooms in as far as a Hull A. The view is in perspective, so cells nearer to you look larger, those further back smaller. Pixels as Windows scales them: at 150 % display scaling 160 are 240 screen pixels. Zooming out stops at twice the distance that fits the window in the ship's starting view (or further, to see the whole outline), at every angle, so turning never changes the size. | `160` | 40–240 |
| `shipView.cameras` | The view as you left it, per ship id: `{ "hull_c": { "yaw": 70, "pitch": 35.26, "distance": 90, "target": { "x": 1.5, "y": -2.25, "z": 3 } } }`. Written by the overlay whenever you turn, zoom or pan, half a second after you stop; you don't edit it. | `null` | – |

A `maxZoomPxPerCell` outside the range is clamped, with a notice. The **Ship view** section's reset sets it back and
leaves the cameras alone.

In a camera, `yaw` and `pitch` are degrees (yaw wraps round, pitch stops at ±85). `distance` and `target` come together
or not at all: without them the view fits the window at that camera. `target` is the point the view turns around, in
cells in the ship's coordinates (x towards the bow, y towards port, z up), and `distance` how far the camera stands
from it, in cells; both are fixed to the ship, so the view stays on the same rack when a sync loads cargo elsewhere or
the overlay has another size. Only the ships under "Recent" and the current one keep a camera; a double-click next to
the cargo (or **Reset view**) removes the ship's entry. An entry that is `null` or has an empty id is dropped.

Before the perspective view (D83) a camera held `pxPerCell`, `panX` and `panY` instead. Such an entry is still read,
but dropped: that ship starts at its own view once. An older version of the overlay can't read a file with `distance`
or `target`; it starts with the default settings and says so, and `settings.json.bak` keeps the file as it was.

## Tool window theme

`toolWindowTheme` sets light or dark for the settings window and the mission editor, colour picker included.
It doesn't change the overlay: that is `appearance`. It sits at the top level, not inside `appearance`, so
**Reset to defaults** in the settings window never switches it.

| Value | What |
|---|---|
| `"system"` | Follows the Windows app mode (Settings → Personalisation → Colours), also while the windows are open. The default. |
| `"light"` | Always light |
| `"dark"` | Always dark, easier on the eyes next to the game |

Lower case only. Missing or `null` means `"system"`. File dialogs and message boxes keep the Windows look.

## Ship

`ship` is the id of the ship you haul with, the name of its file in the ship data's `ships\` without `.json` (`"c2_hercules"`).
The ship selection on the ship name in the overlay header writes it; changing it there plans the current plan anew for
the new ship.

| Value | What |
|---|---|
| `null` or missing | The ship of the saved plan, if that ship loads; otherwise the Hull A. Nothing is written until you choose. The default. |
| an id | That ship. If it is missing or its file is broken, the overlay starts with the Hull A and an amber line says why ("Ship 'x' not loaded: … – using MISC Hull A."). The value stays in the file, so the ship comes back once its file is fixed. |

`recentShips` lists the ships you chose last, latest first, the current one included (`["c2_hercules", "hull_a"]`).
Every change in the header writes it together with `ship`; the list keeps 4 ids. The ship list shows up to 3 of them,
without the current ship, in the group "Recent" on top.

| Value | What |
|---|---|
| `null` or missing | No group "Recent" yet. The default. |
| ids | Those ships under "Recent". An id whose ship doesn't load is skipped there and stays in the file; an empty entry (`null`, `""`) is dropped. |

## Updates

`updates` holds the update check (D109). Missing or `null` means the defaults, a missing key inside it its default.

| Key | What | Default |
|---|---|---|
| `updates.checkOnStartup` | Once per start, after the overlay is up, ask GitHub whether a newer release of SC-CargoPlan is out (one request to the release list of this repository, pre-releases left out). A newer one shows as the overlay's bottom line with *Update* and *Skip this version*. `false` means no request at all. Nothing is downloaded without your click. | `true` |
| `updates.skippedVersion` | A release you chose to skip, like `"0.5.1"`. **This version only:** it isn't offered again, a newer one is. The value stays until you skip another version or clear it. Not a version means none, with a notice. | `null` |

Both keys are set in the settings window's **Updates** section too: *Check for updates on startup*, the skipped version
with *Clear*, and *Check now*. The overlay's *Skip this version* writes `skippedVersion`, *Clear* sets it back to `null`.
*Check now* asks even with `checkOnStartup` off and keeps a skipped version skipped (status `v0.5.1 is skipped`).

A check that fails (offline, GitHub's limit, no answer within 10 seconds) shows nothing; `cargoplan.log` has one line
about it. A build without a release version (`0.0.0-dev`) and a copy not started from the release ZIP's package never
check. Older versions of the overlay don't know `updates`: they start with the default settings and say so, and
`settings.json.bak` keeps the file as it was.

## Ship data

`shipData` holds the ship data check (D112): the cargo grids and outlines come as data releases of
[sc-cargoplan-shipdata](https://github.com/MRiediger/sc-cargoplan-shipdata), named `ships-YYYY.MM[.N]`. Missing or `null`
means the defaults, a missing key inside it its default.

| Key | What | Default |
|---|---|---|
| `shipData.checkOnStartup` | Once per start, after the update check, ask GitHub for the latest data release's `manifest.json` (one request). A newer one shows as the overlay's bottom line, below the update line, with *Update* and *Skip this version*; *Update* downloads its `ships.zip` (about 100 KB) and, from `ships-2026.11.2` on, the two hauling files of the Plan tab (about 0.6 MB), checks every file and restarts the overlay with the new data. `false` means no request at all. | `true` |
| `shipData.skippedVersion` | A data release you chose to skip, like `"ships-2026.11.1"`. **This release only:** a newer one is offered. Not a data release tag means none, with a notice. | `null` |

Both keys are set in the settings window's **Ship data** section too, with *Check now* and the data the overlay runs on
(`In use: 2026.11.1 (downloaded)`, or `(built in)` for the data in the release ZIP).

Downloaded releases lie in `%LocalAppData%\SC-CargoPlan\ships\<release>\`. The overlay uses the newest one it can read,
never one older than the data in its ZIP; at the start it removes older ones, so one stays. A release made for a newer
overlay says "Ship data … needs an app update" instead of offering a download. A check that fails shows nothing;
`cargoplan.log` has one line about it. Older versions of the overlay don't know `shipData`: they start with the default
settings and say so, and `settings.json.bak` keeps the file as it was.

## Routes

`routes` holds the route planner's settings (T13.2, D120): the tab the overlay shows and the rank and faction its port
matrix starts with; the loops' sort and times (T13.4, D124). Missing or `null` means the defaults, a missing key inside it
its default; a word out of the list or a time out of its range falls back to the default, with a notice.

| Key | What | Default |
|---|---|---|
| `routes.tab` | `"cargo"` or `"plan"`: the tab under the header the overlay starts in; every switch is saved. With **Plan** selected, F3 on an offer only notes the contract in `routes.json` and leaves the cargo as it is ("Observed (Plan tab): … – cargo unchanged"). | `"cargo"` |
| `routes.rank` | The highest rank the port matrix shows, `"trainee"`, `"rookie"`, `"junior"`, `"member"`, `"experienced"`, `"senior"` or `"master"`; set with the matrix's rank button or in the settings window's "Plan tab" section (T13.3), which show each other's change. `"master"` shows every template. | `"master"` |
| `routes.faction` | Your side in Pyro, `"hh"` (Headhunters) or `"cfp"` (Citizens for Prosperity); narrows Red Wind contracts only; set with the matrix's faction button or in the "Plan tab" section ("Both sides"). `null` shows both. | `null` |
| `routes.hub` | The Stanton hub you are at (T13.3b, D123): `"Everus Harbor"`, `"Seraphim Station"`, `"Baijini Point"` or `"Port Tressler"`; set with the Plan tab's hub button, which shows with the recipe. The recipe and the check of a scanned offer ("fits as …") use it; the loops (T13.4) measure the travel from it and start the system-wide ring there; the route (T13.6) starts there. A click on a loop's row shows that hub's recipe without changing this. Another name falls back to Everus Harbor, with a notice. | `"Everus Harbor"` |
| `routes.loopsSort` | What the Plan tab's loop list is sorted by (T13.4): `"hour"` (~aUEC/h), `"loop"` (aUEC per loop) or `"landing"` (aUEC per landing); set with the list's sort button. | `"hour"` |
| `routes.planBlock` | The Cargo tab's block "Plan" (T13.6): `"open"` shows a line per placeholder, `"closed"` only its head. Start plan opens it, the first departure from the hub closes it (the station bar lists what to accept at each stop), a click on its head toggles it; another word is `"open"` with a line. | `"open"` |
| `routes.useMeasured` | Your own loop times (T13.6c): `true` ranks a loop you finished at least three times with the overlay's ship by the median of your last seven times (from `routes.json`, `runs`) instead of `routes.times`; its row shows "measured 3× · 1 h 20 min" and no "~". `false` ranks every loop by `routes.times`, and a row's tool tip still names your runs ("– not used"). Set in the settings window's "Plan tab" section. | `true` |
| `routes.times` | The loops' times for ~aUEC/h (T13.4, design §4.7): `station` minutes per landing (docking, the elevator, the short jumps near a station; 1–60), `crate` seconds per crate (5–120), `jump` minutes per quantum jump over 10 Gm (1–30); whole numbers, set in the settings window's "Plan tab" section, shown in the list's filter line. Each out of its range falls back alone, with a notice. | `{ "station": 10, "crate": 20, "jump": 4 }` |

The other filters of the matrix (system, area, direct, contractor, sizes), the view (Matrix, Recipe or Loops) and the hub a loop's row shows last for the session. Older versions of the
overlay don't know `routes`: they start with the default settings and say so, as for `shipData`.

## Look files

A look file (`*.cargoplan-look.json`) carries a look from one overlay to another: **Export…** in the settings window
writes one, **Import…** reads one, and the presets in the **Preset** box are look files that ship with the app. It
holds exactly these keys of settings.json, with the same names and shapes as above: `appearance` (all of it), `legend`,
`toolWindowTheme`, `shipOutline`, `holoOpacity`, `holoHullColor`, `holoEdgeColor` and `shipView` with
`maxZoomPxPerCell` only. Your hotkeys, the overlay's position and size, the cameras per
ship, the ship and the hologram download switch are not in it: they belong to your machine or your run.

```json
{
  "schemaVersion": 1,
  "legend": { "visible": true, "position": "bottom-left", "maxHeightShare": 0.5 },
  "shipOutline": "outline",
  "holoOpacity": 0.4,
  "holoHullColor": "#FF9CB0C0",
  "holoEdgeColor": "#FFE6E6E6",
  "shipView": { "maxZoomPxPerCell": 160 },
  "toolWindowTheme": "system",
  "appearance": { "window": { … }, "header": { … }, "legend": { … }, "crates": { … }, "buttons": { … } }
}
```

- **`schemaVersion` is 1** and comes first. A file with another number is refused with
  "schemaVersion 2 is not supported (expected 1).", whatever else it holds.
- **What a file leaves out stays as it is:** a missing key above, or a missing section of `appearance`. Inside a section the
  file has, a missing value takes its default, as in settings.json: `{ "schemaVersion": 1, "appearance": { "crates":
  { "fillAlpha": 100 } } }` changes only the crates, and resets their other values.
- **Anything else refuses the whole file** and changes nothing: an unknown key (`cameras`, `hotkeys`, a typo), a wrong
  type, a file that isn't JSON. The overlay says why in an amber line ("Look not imported: …").
- **Values are checked like settings.json's:** out of range is clamped, a colour that isn't one takes its default, each
  with a line in the notice.
- Export writes every key. Importing applies at once and is saved like any change; there is no undo, the sections'
  **Reset to defaults** are the way back, and `settings.json.bak` holds the file as it was before the first change of this
  run (see below).

## When something is wrong

- **A value outside its range** is set to the nearest limit (`"fontSize": 0` → 8, `"fillAlpha": 300` → 255).
- **A colour that isn't one** (`"red"`, `"#12345"`) takes its default.
- **A legend position that isn't one** (`"up"`, `"Bottom-Left"`) takes `bottom-left`.
- **A legend height share outside 0.2–0.8** is set to the nearest limit (`"maxHeightShare": 1` → 0.8).
- **A tool window theme that isn't one** (`"blue"`, `"Dark"`) takes `system`.
- **A stop palette that isn't one** (`"ibm"`, `"OkabeIto"`) takes `okabeIto`.
- In all these cases everything else in the file applies, and an amber line in the overlay names the value. The
  file stays as it is until you move the overlay or change a setting; then it is saved with the corrected values.
- **A misspelt key** (`"fontsize"`), **a wrong type** (`"fontSize": "big"`), `null` for a colour or a file
  that isn't JSON makes the whole file unreadable: the overlay starts with defaults, no hotkeys, and says so.
  It doesn't overwrite the file until you move the overlay or change a setting. Fix the file before that; if you
  don't, the first save keeps the old file as `settings.json.bak` next to it.

The first save after each start copies `settings.json` as it was to `settings.json.bak`, byte for byte, even if it
isn't JSON; later saves in the same run leave the backup alone. The next start's first save replaces it, so copy it
away if you want to keep it longer.

## Versions

| `schemaVersion` | Written by | Content |
|---|---|---|
| 1 | versions before the look became a setting | `overlay`, `hotkeys` |
| 2 | v0.1.0 to v0.3.1 | plus `appearance`, `legend`, `toolWindowTheme`, `contractTracker`, `hotkeys.syncContractTracker`, `hotkeys.replan`, `hotkeys.discardPlan`, `hotkeys.undo`, `hotkeys.redo`, `hotkeys.resetView`, `ship`, `recentShips`, `shipOutline`, `downloadShipHolograms`, `holoOpacity`, `holoHullColor`, `holoEdgeColor`, `shipView` and the newer `appearance` values, each optional within version 2 |
| 3 | v0.4.0 and later | without `contractTracker`, `hotkeys.syncContractTracker` and a camera's `pxPerCell`, `panX`, `panY`; plus `hotkeys.scanContracts` (optional); from v0.5.0 plus `updates`, from v0.6.0 plus `shipData`, from v0.8.0 plus `routes` (all optional, no new version, D105 addenda, D120) |

### Older files

A version 1 or 2 file is migrated when it is read, nothing else in it changes:

- `contractTracker` goes (the import from ContractTracker is gone since v0.4.0; contracts come in by the scan).
- `hotkeys.syncContractTracker` goes. If you had set one, an amber line says which key is free again, and the file is
  written as version 3 right away, so you see the line once; the old file stays as `settings.json.bak`.
- A camera in `shipView.cameras` that still has `pxPerCell`, `panX` or `panY` set (the flat view before v0.1.0) goes;
  that ship starts at its default camera, as it did before. Those three keys set to `null` just go.
- A version 1 file also gets the default look.

Apart from the sync hotkey, nothing is written until you move the overlay or change a setting; then the file becomes
version 3. Everything else follows the rules above: a misspelt key still makes the whole file unreadable. An overlay older than v0.4.0 can't
read a version 3 file and starts with defaults; to go back, rename `settings.json.bak` to `settings.json` before its
first save.
