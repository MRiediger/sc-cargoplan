# Settings file format

`%LocalAppData%\SC-CargoPlan\settings.json` holds the overlay's position and size, your hotkeys, where
the legend stands, the overlay's look, the tool windows' theme, where ContractTracker is, your ship, the ships you chose last and the ship view as you left it on each. Position and size
are saved whenever you move the overlay; the settings window (the gear in the header) sets and saves the look,
the legend layout, the ship view (outline or hologram, its opacity and colours, hologram downloads, how far it zooms), the tool window theme, the hotkeys and the ContractTracker settings, and loads or saves a look as a [look file](#look-files); the ship name in the overlay header sets the ship and the recent ships. You can still edit the file by hand: close the overlay, edit the file, start
the overlay again.

## Example with every default

Without a file, or without `ship`, `recentShips`, `legend`, `shipOutline`, `downloadShipHolograms`, `holoOpacity`,
`holoHullColor`, `holoEdgeColor`, `shipView`, `toolWindowTheme`, `contractTracker` and `appearance`, the overlay uses exactly these
values.

```json
{
  "schemaVersion": 2,
  "ship": null,
  "recentShips": null,
  "overlay": { "left": 100, "top": 200, "width": 420, "height": 460 },
  "hotkeys": { "toggleLock": "Ctrl+Alt+K", "toggleOverlay": "Ctrl+Alt+H", "syncContractTracker": null, "replan": null, "discardPlan": null, "undo": null, "redo": null, "resetView": null },
  "legend": { "visible": true, "position": "bottom-left" },
  "shipOutline": "outline",
  "downloadShipHolograms": true,
  "holoOpacity": 0.4,
  "holoHullColor": "#FF9CB0C0",
  "holoEdgeColor": "#FFE6E6E6",
  "shipView": { "maxZoomPxPerCell": 160 },
  "toolWindowTheme": "system",
  "contractTracker": { "path": null, "autoSync": true },
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

`overlay` and `hotkeys` are described in the [user guide](user-guide.md#hotkeys); `syncContractTracker` takes
new ContractTracker captures into the plan like the editor's button, `replan` lays out the cargo on board anew like the
editor's Re-plan, `discardPlan` empties the plan like the editor's Discard plan, `undo` and `redo` go back and forward like the header's
↶ and ↷, `resetView` fits the ship view like the header's reset next to the gear. In `hotkeys`, `null` and a
missing entry both mean "no hotkey"; the settings window writes `null` when you clear one, so you see the
entry exists. Every section and every value
in `appearance` is optional: what you leave out keeps its default. `{ "schemaVersion": 2, "appearance":
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
| `header.foreground` | Ship name and the glyphs (pencil, gear, split, merge, exit, padlock) | `#FFE6E6E6` | |
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

## Ship view

`shipOutline` sets what lies behind the racks in the ship view; `holoOpacity`, `holoHullColor`, `holoEdgeColor` and
`downloadShipHolograms` go with it. All of them sit at the top level, so the crates' **Reset to defaults** never switches them; the settings window's
**Ship view** section has a reset of its own.

| `shipOutline` | What |
|---|---|
| `"outline"` | The ship's outline seen from above, under the racks: bow, sides and shape. A ship without an outline in `outlines/ship-outlines.json` shows the bow wedge with "Bow" on its floor. The default. |
| `"holo-shaded"` | The ship's 3D hologram behind the racks, true to scale, softly lit from the camera. Until it is there, and for a ship without one, the outline. |
| `"holo-flat"` | The same, every face in one shade. |
| `"holo-hiddenline"` | The same as a dark hull with its visible edges as lines; zooming in shows more edges (from 1.5× and 3×). |
| `"none"` | Racks and frame only, no outline, no wedge, no label. |

Lower case only. Missing or `null` means `"outline"`; any other text means `"outline"` too, with a notice.

`holoOpacity` is the hologram's opacity, from `0.1` to `1`; missing means `0.4`. A value outside is clamped, with a
notice.

`holoHullColor` colours the hologram's hull in `"holo-shaded"` and `"holo-flat"` (default `"#FF9CB0C0"`),
`holoEdgeColor` its edges in `"holo-hiddenline"` (default `"#FFE6E6E6"`); the hidden-line hull stays dark, it only hides
what lies behind it. `"#RRGGBB"` or `"#AARRGGBB"`, written back as `"#FFRRGGBB"`: a hologram is always opaque, its
opacity is `holoOpacity`, so an alpha below `FF` is set to `FF` with a notice. Not a colour, missing or `null` means the
default, with a notice for one that isn't a colour.

`downloadShipHolograms`: `true` (the default) lets a hologram look download the ship's file from this repository's
release `holo-v1` on GitHub when you pick a ship, one file per ship, and the list of files at most once a day. Nothing
else is sent. `false` means no network access at all; holograms already downloaded still show. They are kept in
`%LocalAppData%\SC-CargoPlan\holo\holo-v1\` ([format](holo-format.md)).

`shipView` holds the zoom and the view per ship. Missing or `null` means the defaults.

| Key | What | Default | Range |
|---|---|---|---|
| `shipView.maxZoomPxPerCell` | How far the mouse wheel zooms in: pixels per cell (1 SCU, 1.25 m), the same on every ship, so a Hull C zooms in as far as a Hull A. Pixels as Windows scales them: at 150 % display scaling 160 are 240 screen pixels. Zooming out stops at half the size that fits the window. | `160` | 40–240 |
| `shipView.cameras` | The view as you left it, per ship id: `{ "hull_c": { "yaw": 70, "pitch": 35.26, "pxPerCell": 90, "panX": 1.5, "panY": -2.25 } }`. Written by the overlay whenever you turn, zoom or pan, half a second after you stop; you don't edit it. | `null` | – |

A `maxZoomPxPerCell` outside the range is clamped, with a notice. The **Ship view** section's reset sets it back and
leaves the cameras alone.

In a camera, `yaw` and `pitch` are degrees (yaw wraps round, pitch stops at ±85). `pxPerCell`, `panX` and `panY` come
together or not at all: without them the view fits the window at that camera, which is what you get after only turning
the ship. `panX`/`panY` say where the middle of the view lies, measured in cells from the ship's centre, so the view
stays on the same rack when a sync loads cargo elsewhere or the overlay has another size. Only the ships under "Recent"
and the current one keep a camera; a double-click next to the cargo (or **Reset view**) removes the ship's entry. An
entry that is `null` or has an empty id is dropped.

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

## ContractTracker

`contractTracker` sits at the top level. Without it, or with `null`, the overlay finds ContractTracker at its
own place and takes every capture into the plan by itself.

| Key | What | Default |
|---|---|---|
| `contractTracker.path` | Full path of ContractTracker's `contracts.db`; `null` = `%AppData%\SCContractTracker\contracts.db` | `null` |
| `contractTracker.autoSync` | `true`: captures come into the plan by themselves. `false`: an amber line says "N new contracts"; **Sync from ContractTracker** (editor or hotkey) takes them in | `true` |

The settings window sets both (**Browse…**, **Detect automatically**, **Auto-sync**); a new path is watched at once,
without a restart. A path whose file doesn't exist is kept: ContractTracker may come later. The settings window
says "ContractTracker not found" and links to its page.

## Ship

`ship` is the id of the ship you haul with, the name of its file in `ships/` without `.json` (`"c2_hercules"`).
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

## Look files

A look file (`*.cargoplan-look.json`) carries a look from one overlay to another: **Export…** in the settings window
writes one, **Import…** reads one, and the presets in the **Preset** box are look files that ship with the app. It
holds exactly these keys of settings.json, with the same names and shapes as above: `appearance` (all of it), `legend`,
`toolWindowTheme`, `shipOutline`, `holoOpacity`, `holoHullColor`, `holoEdgeColor` and `shipView` with
`maxZoomPxPerCell` only. Your hotkeys, the ContractTracker settings, the overlay's position and size, the cameras per
ship, the ship and the hologram download switch are not in it: they belong to your machine or your run.

```json
{
  "schemaVersion": 1,
  "legend": { "visible": true, "position": "bottom-left" },
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
- **A tool window theme that isn't one** (`"blue"`, `"Dark"`) takes `system`.
- **A stop palette that isn't one** (`"ibm"`, `"OkabeIto"`) takes `okabeIto`.
- **A ContractTracker path that isn't a full path** (`"contracts.db"`, `""`) is detected instead (`null`).
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
| 2 | this version | plus `appearance`; `legend`, `toolWindowTheme`, `contractTracker`, `hotkeys.syncContractTracker`, `hotkeys.replan`, `hotkeys.discardPlan`, `hotkeys.undo`, `hotkeys.redo`, `hotkeys.resetView`, `ship`, `recentShips`, `shipOutline`, `downloadShipHolograms`, `holoOpacity`, `holoHullColor`, `holoEdgeColor`, `shipView`, `appearance.legend.checkBoxColor`, `appearance.legend.checkMarkColor`, `appearance.crates.stopPalette`, `appearance.crates.selectionColor`, `appearance.crates.selectionWidth` and `appearance.crates.selectionOutline` came later as optional parts of version 2 |

A version 1 file is read as it is, with the default look. The next save (moving the overlay, changing a setting) writes
version 2 with every `appearance` value, so you see what there is to set. An older overlay can't read a
version 2 file and starts with defaults.
