# Settings file format

`%LocalAppData%\SC-CargoPlan\settings.json` holds the overlay's position and size, your hotkeys, where
the legend stands, the overlay's look, the tool windows' theme, where ContractTracker is, your ship and the ships you chose last. Position and size
are saved whenever you move the overlay; the settings window (the gear in the header) sets and saves the look,
the legend layout, the ship view (outline or hologram, its opacity and colours, hologram downloads), the tool window theme, the hotkeys and the ContractTracker settings; the ship name in the overlay header sets the ship and the recent ships. You can still edit the file by hand: close the overlay, edit the file, start
the overlay again.

## Example with every default

Without a file, or without `ship`, `recentShips`, `legend`, `shipOutline`, `downloadShipHolograms`, `holoOpacity`,
`holoHullColor`, `holoEdgeColor`, `toolWindowTheme`, `contractTracker` and `appearance`, the overlay uses exactly these
values.

```json
{
  "schemaVersion": 2,
  "ship": null,
  "recentShips": null,
  "overlay": { "left": 100, "top": 200, "width": 420, "height": 460 },
  "hotkeys": { "toggleLock": "Ctrl+Alt+K", "toggleOverlay": "Ctrl+Alt+H", "syncContractTracker": null, "replan": null, "discardPlan": null },
  "legend": { "visible": true, "position": "bottom-left" },
  "shipOutline": "outline",
  "downloadShipHolograms": true,
  "holoOpacity": 0.4,
  "holoHullColor": "#FF9CB0C0",
  "holoEdgeColor": "#FFE6E6E6",
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
      "halo": { "enabled": false, "color": "#E0141414", "size": 3 }
    },
    "crates": {
      "fillAlpha": 208,
      "edgeAlpha": 192,
      "rackNumberColor": "#FFFFFFFF",
      "rackNumberFontSize": 27,
      "halo": { "enabled": true, "color": "#E0141414", "size": 3 }
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
editor's Re-plan, `discardPlan` empties the plan like the editor's Discard plan. In `hotkeys`, `null` and a
missing entry both mean "no hotkey"; the settings window writes `null` when you clear one, so you see the
entry exists. Every section and every value
in `appearance` is optional: what you leave out keeps its default. `{ "schemaVersion": 2, "appearance":
{ "legend": { "fontSize": 14 } } }` changes only the legend's font size.

## Colours and transparency

Colours are `"#AARRGGBB"` (alpha, red, green, blue) or `"#RRGGBB"` (fully opaque), hex digits in any case.
**Transparency is the alpha of the colour:** `00` is invisible, `FF` is opaque, `80` is about half.
The overlay's background `#A0141414` is a dark grey at 63 %. The crates' colours are the stop colours and
can't be changed, so only their alpha is a number of its own: `0` (invisible) to `255` (opaque).

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
| `crates.fillAlpha` | Opacity of the crates in the ship view | `208` | 0–255 |
| `crates.edgeAlpha` | Opacity of the crates' edges | `192` | 0–255 |
| `crates.rackNumberColor` | Colour of the rack numbers and the "Bow" label | `#FFFFFFFF` | |
| `crates.rackNumberFontSize` | Size of the rack numbers and the "Bow" label in pixels, the same at every zoom | `27` | 8–48 |
| `crates.halo` | Outline of the rack numbers and the "Bow" label | on | |
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

**Alpha at least `01`:** the overlay passes clicks on fully transparent pixels to the window below. With a
background of alpha `00`, you could no longer move the overlay or press a button, so the overlay uses `01`
instead (still invisible) and says so.

## Halo

A halo is a soft glow around text and glyphs, for reading over bright and dark scenes alike.

| Key | What | Default | Range |
|---|---|---|---|
| `enabled` | On or off; off keeps colour and size for later | `false` (crates: `true`) | |
| `color` | Colour of the glow, alpha included | `#E0141414` | |
| `size` | Blur radius in pixels; for the rack numbers the width of their outline | `3` | 1–20 |

Inside a `halo`, missing keys take the defaults of this table, not those of the section: a crates halo
written as `{ "size": 5 }` is off. Write `"enabled": true` with it.

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

## When something is wrong

- **A value outside its range** is set to the nearest limit (`"fontSize": 0` → 8, `"fillAlpha": 300` → 255).
- **A colour that isn't one** (`"red"`, `"#12345"`) takes its default.
- **A legend position that isn't one** (`"up"`, `"Bottom-Left"`) takes `bottom-left`.
- **A tool window theme that isn't one** (`"blue"`, `"Dark"`) takes `system`.
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
| 2 | this version | plus `appearance`; `legend`, `toolWindowTheme`, `contractTracker`, `hotkeys.syncContractTracker`, `hotkeys.replan`, `hotkeys.discardPlan`, `ship`, `recentShips`, `shipOutline`, `downloadShipHolograms`, `holoOpacity`, `holoHullColor` and `holoEdgeColor` came later as optional parts of version 2 |

A version 1 file is read as it is, with the default look. The next save (moving the overlay, changing a setting) writes
version 2 with every `appearance` value, so you see what there is to set. An older overlay can't read a
version 2 file and starts with defaults.
