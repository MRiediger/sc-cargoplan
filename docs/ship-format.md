# Ship file format

Ships are data, not code. Each ship is one JSON file in [`ships/`](../ships/) in the repository root. The build
copies the folder next to the exe, and the release ZIP carries it as `SC-CargoPlan/ships/` (see [D56](decisions.md)),
so adding a ship means adding one file. No C# and no `.csproj` change needed.

The app reads every `*.json` in that folder at start. A file that is broken costs only its own ship; the reason is
reported (the CLI prints it, the app lists the ship under **Not loaded** in the ship list, the reason as its tool tip). Without the folder, or without a valid
`hull_a.json`, the app doesn't start and asks to unzip the release again: the Hull A is the fallback ship.

## Example

`ships/hull_a.json` (shortened to two of four racks):

```json
{
  "schemaVersion": 6,
  "id": "hull_a",
  "name": "MISC Hull A",
  "manufacturer": "MISC",
  "fleetyardsSlug": "misc-hull-a",
  "source": "cryon.rocks (Motherlode) 4.10.2-LIVE.12913470, 2026-11-07",
  "holoOrigin": [0.1877, 0.505, 0.1355],
  "outlineOrigin": [0.1877, 0.505, -2.1253],
  "totalScu": 64,
  "racks": [
    {
      "id": "rack-0",
      "capacityScu": 16,
      "grid": { "length": 4, "width": 2, "stack": 2 },
      "position": { "side": "port", "level": "upper" },
      "origin": [-9, 3, 1],
      "stackAxis": "+y"
    },
    {
      "id": "rack-1",
      "capacityScu": 16,
      "grid": { "length": 4, "width": 2, "stack": 2 },
      "position": { "side": "port", "level": "lower" },
      "origin": [-9, 3, -2],
      "stackAxis": "+y"
    }
  ]
}
```

## Fields

All fields are **required** unless the table says *optional*. Optional fields came later without a schema bump: a
file without them loads as before. Names are camelCase and **case-sensitive**, and so are the values of
`side`, `level` and `stackAxis`. Unknown fields are an error, so a typo like `capacitySCU` fails the build instead of
being ignored silently.

### Ship

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | number | Must be `6`; `4` and `5` are still read, every rack straight. Bumped when the format changes incompatibly or a field changes its meaning (5: `maxCrateSize` is the door limit, D70; 6: a rack may have a `rotation`, D91); older files are rejected with the version they have and the ones expected. |
| `id` | string | Must equal the file name without `.json` (`hull_a.json` → `"hull_a"`). Use lowercase `snake_case`: Linux file systems are case-sensitive. |
| `name` | string | Display name, e.g. `"MISC Hull A"`. |
| `manufacturer` | string | *Optional.* Manufacturer code as Fleetyards writes it, e.g. `"MISC"`, `"RSI"`; ships are grouped by it. Not empty if present. |
| `fleetyardsSlug` | string | *Optional.* The ship's slug on Fleetyards, e.g. `"misc-hull-a"`. It picks the ship's outline from `outlines/ship-outlines.json` (traced from Fleetyards renders, not under the MIT licence, see `outlines/NOTICE.md`); without a match the ship view shows the bow wedge. Not empty if present. |
| `source` | string | *Optional.* Where the values come from, e.g. `"in game"` (measured in the hangar) or `"Fleetyards API (CIG ship matrix), export 2026-10-26"` (written by the import). Not empty if present. |
| `notes` | string | *Optional.* Anything a reviewer should know, e.g. what was checked and what wasn't. Not empty if present. |
| `holoOrigin` | array of 3 numbers | *Optional, every shipped ship has it.* `[x, y, z]` in cells, fractions allowed: where the hologram's origin lies in ship coordinates, see [Hologram and outline](#hologram-and-outline). A [cargo module](#cargo-modules) passes it on unless it says its own. For a ship with a game model the import writes it (the ship's fraction s, D93); otherwise it keeps it to the digit. |
| `outlineOrigin` | array of 3 numbers | *Optional, every shipped ship has it.* `[x, y, z]` in cells, fractions allowed: where the outline's origin lies; z is the plane it lies in. Passed on, written and kept like `holoOrigin`. |
| `outlineScale` | number | *Optional, only with `outlineOrigin`.* Greater than 0. The factor on the outline's true size, left out for 1. No shipped ship has it since D93: outlines from the game model are at true size. |
| `holoOffset` | array of 3 integers | *Optional, not with `holoOrigin`, for older files.* `[x, y, z]` in cells: moves the hologram from its middle on the racks. Only a file without `holoOrigin` uses that rule; no shipped ship does any more. The import keeps it and never sets it. |
| `totalScu` | number | Cargo capacity of the ship. Must equal the sum of all `capacityScu`. |
| `racks` | array | At least one rack. **Order matters:** the array order is the **rack order** (Rack 1, Rack 2, …). The planner fills racks in this order, puts the first stop into Rack 1 and lets only racks next to each other in this order share a stop. The rack order says nothing about where a rack sits; that is `position`. |

### Rack

A rack is a separately loadable section of the cargo hold, usually one plate.

| Field | Type | Rules |
|---|---|---|
| `id` | string | Unique within the ship. Convention: `rack-0`, `rack-1`, … in rack order. Players see "Rack 1", "Rack 2", … Ships imported from Fleetyards use the hold's index there instead (`rack-<index>`), so the import finds the rack again after hand reordering; their ids need not run in order. |
| `capacityScu` | number | Greater than 0. Must equal `length × width × stack` of the grid. |
| `grid` | object | The rack's cell grid, see below. |
| `position` | object | *Optional.* Where the rack sits in player words ("port upper"), see below. Shown in the crate tooltip, not used for planning. Leave it out where it isn't known: a ship draft with a generated rack arrangement has none, and the tooltip then skips it. |
| `origin` | array | Where the rack sits in ship coordinates: `[x, y, z]`, three whole numbers, see [Coordinates](#coordinates). Used to draw the ship, not for planning. Straight racks must not overlap. |
| `stackAxis` | string | The ship direction crates stack in, away from the plate: `"+x"`, `"-x"`, `"+y"`, `"-y"`, `"+z"` or `"-z"`, see [Stack axis](#stack-axis). |
| `maxCrateSize` | number | *Optional.* The door limit: the largest crate that gets through the rack's door, in SCU (`1`, `2`, `4`, `8`, `16`, `24` or `32`), where that is less than the grid takes. Left out, the grid alone decides. See [Max crate size](#max-crate-size). |
| `minCrateSize` | number | *Optional.* The smallest crate the rack takes, in SCU, where that is more than 1 (the Hull C's spindle: `32`). Left out, any crate the grid and `maxCrateSize` allow. See [Min crate size](#min-crate-size). |
| `rotation` | array of 3 numbers | *Optional, from schema 6, not in a cargo module.* `[yaw, pitch, roll]` in degrees: how the rack is turned about its middle, for a rack that lies at an angle in the hull. Left out, or `[0, 0, 0]`, the rack lies straight. See [Turning a rack](#turning-a-rack). |

### Max crate size

The grid says which crates fit by shape; the rack's door may let less through (D70). The Clipper's hold is 3×2×2 cells
and takes an 8 SCU crate by shape, but its door only crates up to 2 SCU: `"maxCrateSize": 2`. The planner never puts a
larger crate into the rack. A mix it derives from a contract is split up to the largest crate any rack of the ship
takes, so on the Clipper an 8 SCU contract becomes four 2 SCU crates, and the overlay says "crates capped to 2 SCU
(door limit)". A mix the player set in the editor stays as typed; if no rack takes one of its crates, the mission
waits with "no rack accepts 4 SCU crates, door limit 2 SCU". A move puts a crate the door refuses into another rack,
saying "Rack 1: door limit 2 SCU – placed in rack 2", and a merge that would make one is refused.

The value comes from Fleetyards' `cargoHolds[].limits.max.capacity` (index of 25 Oct 2026). It is written only where the
door takes less than the grid; a test checks that every shipped value limits something, and a value the grid enforces
anyway is left out (`hull_a.json` has none). Values above 32, the largest crate the app knows, count as 32. A 1 on a grid
that takes more is not written: it is the one value that can also be a default, so it stays unverified until Fleetyards
confirms it (list in TASKS.md, K3.1). By hand, set it only from a source and name it in `notes`.

Schema 4 called the same field "the game's limit on top of the grid" and read it the same way, so a schema 4 file loads
unchanged; schema 5 marks the meaning.

### Min crate size

Some racks take only large crates: the Hull C's spindle takes 32 SCU crates only, the MPUV Tractor's rack 16s, the
CSV-SM's 4s (Fleetyards' `limits.min.capacity`, index of 25 Oct 2026). `"minCrateSize": 32` says so; it must be a crate
the rack takes (no larger than `maxCrateSize` and fitting the grid), else the file doesn't load. The planner never puts a
smaller crate into the rack and never rounds a contract up: the kiosk delivers nothing above the contract's max. A
contract whose crates the ship can't take waits with "doesn't fit (ship takes 32 SCU crates only)", also when only a rest
is too small (40 SCU at max 32 is 32 + 8 on the Hull C). A move past such a rack says "Rack 1: takes 32 SCU crates
only – placed in rack 2", and a split below the minimum is refused. The import writes values above 1 only; 0 counts as
none, values above 32 as 32.

### Grid

One cell is 1 SCU, a cube of 1.25 m.

| Field | Type | Rules |
|---|---|---|
| `length` | number | Cells along the plate (on the Hull A: along the spindle). Greater than 0. |
| `width` | number | Cells across the plate. Greater than 0. |
| `stack` | number | Cells crates stack away from the plate. Greater than 0. |

Where these three axes lie in the ship is the rack's `stackAxis`.

### Position

| Field | Type | Rules |
|---|---|---|
| `side` | string | `"port"` or `"starboard"`, seen facing the bow. |
| `level` | string | `"upper"` or `"lower"`. |

`position` and `origin` describe the same rack in two ways and are not derived from each other: `position` is
what players say, `origin` is geometry. Keep them consistent: a port rack lies at a larger y than the ship's middle, see
below. Its y itself may be negative, as the zero point is the file's (the Hull B's port racks lie at y −4 and −2).

## Coordinates

Racks, hologram and outline of a ship share one coordinate system (see [D14](decisions.md), [D90](decisions.md)):

- **Right-handed:** x points to the **bow**, y to **port**, z **up**.
- **Unit:** 1 = one cell = the edge of a 1 SCU crate (1.25 m). Rack origins are whole numbers.
- `origin` is the rack's **minimum corner** (smallest x, y and z), whichever way the rack stacks. The rack
  fills `origin` to `origin` plus its grid laid along the axes of its [stack axis](#stack-axis).
- The zero point is the file's own: pick one when you start a ship and keep it. Racks, hologram and outline each
  count from it, none from another. Saved cameras count from it too.
  Straight racks may touch but must not overlap. The loader names both racks if they do.

### Stack axis

`stackAxis` says which way crates stack, away from the plate (see [D26](decisions.md)). Crates always lie flat
against their plate, so it is the direction a second layer goes: up from a floor, down from a ceiling or a hull
underside, outwards from a spindle. The other two grid axes follow from it; a rack at an angle stays on one of these
axes and adds a [`rotation`](#turning-a-rack) on top:

| `stackAxis` | `length` along | `width` along | `stack` along |
|---|---|---|---|
| `"+z"` / `"-z"` | x | y | up / down |
| `"+y"` / `"-y"` | x | z | to port / to starboard |
| `"+x"` / `"-x"` | z | y | to the bow / to the stern |

`length` lies along the ship (bow to stern) wherever it can. A minus axis counts from the rack's far side: the
first layer lies at the plate, at the rack's largest coordinate on that axis.

Hull A, laid as Fleetyards' offsets give it (D78): three cells of spindle between the plates, one between the levels.
The plates sit on the spindle, crates stack outwards:

| Rack | Position | `origin` | `stackAxis` | Fills x · y · z |
|---|---|---|---|---|
| 1 | port upper | `[0, 1, 3]` | `"+y"` | 0..4 · 1..3 · 3..5 |
| 2 | port lower | `[0, 1, 0]` | `"+y"` | 0..4 · 1..3 · 0..2 |
| 3 | starboard upper | `[0, -4, 3]` | `"-y"` | 0..4 · -4..-2 · 3..5 |
| 4 | starboard lower | `[0, -4, 0]` | `"-y"` | 0..4 · -4..-2 · 0..2 |

Two more examples for other kinds of holds:

```json
{ "id": "floor", "capacityScu": 24, "grid": { "length": 4, "width": 3, "stack": 2 },
  "position": { "side": "port", "level": "lower" }, "origin": [0, 0, 0], "stackAxis": "+z" }
```

An interior floor (`"+z"`): crates go up from z 0, the rack fills x 0..4 · y 0..3 · z 0..2.

```json
{ "id": "underside", "capacityScu": 8, "grid": { "length": 2, "width": 2, "stack": 2 },
  "position": { "side": "starboard", "level": "lower" }, "origin": [0, -2, -3], "stackAxis": "-z" }
```

A rack under the hull (`"-z"`): the plate is the hull at z −1, crates hang down from it, the rack fills
x 0..2 · y −2..0 · z −3..−1, and the first layer is z −2..−1.

### Turning a rack

Most racks lie along the ship's axes. For one that lies at an angle in the hull, `rotation` turns it (D91):

```json
{ "id": "rack-0", "capacityScu": 16, "grid": { "length": 4, "width": 2, "stack": 2 },
  "origin": [0, 1, 3], "stackAxis": "+y", "rotation": [0, 40, 0] }
```

First lay the rack as if it were straight, with `origin` and `stackAxis` as above. Then `rotation` turns the whole rack
about **its middle**, so changing an angle leaves the rack in place and `origin` keeps meaning the corner of the straight
box. Everything inside the rack turns with it and means what it did: cells, the stack axis, the crates and how they lie.

| Value | Turns about | Positive means |
|---|---|---|
| `yaw` | the ship's z | the bow end turns to port |
| `pitch` | the ship's y | the bow end turns down |
| `roll` | the ship's x | the port side turns up |

Right-handed about the ship's axes, applied in the order roll, pitch, yaw (R = Rz(yaw) · Ry(pitch) · Rx(roll)).

**Change the angle, check the picture.** That is the whole method. Start from what you see in the hull, change one value,
and look at the rack against the hologram from two sides. Then, in the app, drag a crate onto it and hover over a crate.

Where it stops:

- **Overlap isn't checked** between a turned rack and any other: its box isn't where it lies. Only the picture shows it.
- **`origin` stays whole cells.** A rack's middle can only lie on half or whole cells, so a place from outside fits to
  half a cell at most (0.625 m).
- **The rack's own directions stay its own.** "Bow first" fills along the rack's length: a rack yawed past 90° fills
  towards the stern. Keep such a rack's angle below 90° if you can, with its length lying the way you'd load it.
- **A straight angle is no rotation.** Use `stackAxis` and swap the grid's axes for 90°, 180° and 270°; `rotation` is for
  real angles.
- **A selection across racks** keeps its offset where the racks lie straight, so between racks turned differently it isn't
  the offset you see. The preview shows where the crates go; if two would share a cell, all go into the rack of the crate
  held, without cells.
- **Cargo modules** can't say a `rotation` yet.

### Hologram and outline

Three things lie in ship coordinates, each on its own (D90):

| What | Field | Its origin, the point the field places |
|---|---|---|
| A rack | `origin` | the rack's minimum corner |
| The hologram | `holoOrigin` | the mesh's origin: the game model's origin (holo-v2, see [holo-format.md](holo-format.md)) |
| The outline | `outlineOrigin`, `outlineScale` | the game model's origin too; it lies flat at z |

**Ships with a game model (D93):** racks, hologram and outline come from the same Motherlode model, and the file's zero
point is that model's origin plus the ship's fraction s (D92). So the anchors follow from s, and the import writes them:

- `holoOrigin` = s, 4 decimals;
- `outlineOrigin` = s with z moved to the model's floor: s.z + `floorM` ÷ 1.25, `floorM` from
  `outlines/ship-outlines.json` (the model's lowest point below its origin, metres), so no rack reaches through it;
- no `outlineScale`: the outline is the model's top view at true size.

An outline without `floorM` (a Fleetyards trace) leaves the anchors as they are and the import lists the ship. Don't
correct these values by hand; correct a rack instead (below), and the next import writes them again.

**Ships without a game model** (135c, 600i Executive Edition, F7C Hornet Mk II) keep the values D90 froze from the
older rule; they look long because they keep the place the ship had to the pixel, so `-9.491073608398438` is no
measurement. Their hologram is holo-v1's file, so nothing moves. A new ship of that kind gets them by hand or from the
test `ShipAnchorValueTests` (see `CLAUDE.md`): x and y the racks' common box middle, z the box's middle minus half the
hull's height in cells, for both fields. A file without the fields still works: hologram and outline hang on the racks'
middle as they used to, so one corrected rack moves them too. A test holds every shipped ship to having them.

**`holoOrigin` belongs to a hologram release**, today `holo-v2`. A release that kept the game model's origin needs
nothing; one that moved it would need its own rule in the import.

### Correcting a rack

Rack 1 of the Hull A sits one cell too far aft, say. Change that rack's `origin` and nothing else:

```diff
-      "origin": [0, 1, 3],
+      "origin": [1, 1, 3],
```

Only that rack moves in the picture. Racks 2–4, the hologram and the outline stay, at their size. With a hologram look
the default view stays too, as the hull's box holds the rack; with the outline it looks half a cell further to the bow,
since a fit shows the racks. A camera the player left stays where it is. To move the hull against all racks instead,
change `holoOrigin`, or `outlineOrigin` for the outline.

## Measuring a ship

In the hangar, load the empty hold with 1 SCU crates. For each plate, count the cells along it
(`length`), across it (`width`) and how many crates stack away from it (`stack`); `capacityScu` is the
product. Note which side (facing the bow) and level each plate is on (`position`), and which way the second
layer goes (`stackAxis`). Community guides such as Erec's show the stacking direction per ship; check it in the
hangar if in doubt. Then decide the rack
order: the order in which haulers should fill the plates for their first, second, … stop.

For `origin`, sketch the hold from above and from behind on cell paper: pick a zero point, put each rack's
minimum corner on the grid and leave a cell of gap where something sits between the racks (a spindle, a floor).
A schematic layout is enough; the overlay draws boxes, not the ship.

If you take grid sizes measured by others, such as a community catalogue, check its license before copying
values into a ship file, name the source in your PR and in `source`. Only values derived from it go into the repo,
not the catalogue's raw data (D56).

## Where the data comes from

The racks of almost every ship come from the game files (D92): the cargo grid catalog of
[cryon.rocks (Motherlode)](https://cryon.rocks/ops/cargo), extracted from the game and used with its operators'
permission. `source` names the catalog's game build and date, e.g.
`"cryon.rocks (Motherlode) 4.10.2-LIVE.12913470, 2026-11-07"`. Three ships stay Fleetyards drafts for now: the 135c
(its grids have no place in the catalog), the 600i Executive Edition and the F7C Hornet Mk II (not in the catalog).
Hologram and outline come from the same models (D93): the hologram release `holo-v2` and `outlines/ship-outlines.json`
(the three Fleetyards ships keep their Fleetyards hologram and outline). Only values derived from the catalog and the
models go into the repo, never the catalog or a model itself (D56).

### Importing from the game files

After a patch, or when Motherlode publishes a new catalog:

1. **Fetch:** run `fetch-motherlode.ps1` of the `sc-ship-outlines` pipeline. It takes the grids and the game build from
   Motherlode's API (one request per run, a key from `MOTHERLODE_API_KEY` or `motherlode/api-key.txt`, never committed)
   and the model URLs from its page, and archives both with the joined `motherlode/catalog-<date>.json`, each with its
   SHA-256, plus the game's models; all of it stays outside this repo. The build lands in the ships' `source`.
2. **Import:** `./tools/import-motherlode.ps1 -Catalog <path>/catalog-<date>.json -Previous <path>/catalog-<older date>.json -ModelMap <path>/model-map.csv`.
   It writes every ship whose racks changed and reports each as `same`, `updated`, `created` (with `-Create`), `new`
   (without `-Create`), `kept` (a grid without place), `handcheck` or `order`, then per rack its rounding, stack axis,
   grid, crate limits and rotation. Exit code 0 means done, 1 something to look at, 2 an error (then nothing is written).
3. **Holograms and outlines** (only when the models changed): the pipeline builds them per model from the map
   ([holo-format.md](holo-format.md#pipeline-holo-v2)) and writes `outlines/ship-outlines.json`; run the import once
   more, and it writes the anchors from the new outlines' `floorM`. A new hologram release needs a new tag and the app
   constant `HoloRelease.Tag`.
4. **Look at what it didn't write:** `handcheck` means overlapping grids, a rack set or position that doesn't match the
   file, or a total that differs from the catalog's; `order` means the catalog lists the same grids in another order
   than the previous one, so the script can't tell which rack is which; `anchors ... kept` means the ship's outline has
   no `floorM`. Correct those by hand or ask on GitHub.
5. **Commit:** run `dotnet test` (every file must load), then commit the changed files with the catalog's build and date.

What the import does: game axes (x starboard, y bow, z up, metres) become ship cells as x = game y, y = −game x,
z = game z, divided by 1.25. A grid's `size[0]` and `size[1]` lie centred on its translation, `size[2]` goes from there
along the third column of its 3×4 matrix: that axis becomes `stackAxis`. A grid whose axes are all less than 1° off the
ship's axes lies straight (the grid's sizes swap as needed); any other gets a `rotation` (D91). The zero point is the game
model's origin plus a fraction of a cell per axis that rounds the racks best; the report names it and every rack's
rounding. Rack ids, order and `position` stay, so saved plans keep their racks; hand-written `notes` stay too. The
anchors follow s for a ship with a model and a slug ([Hologram and outline](#hologram-and-outline)); a ship without
either keeps `holoOrigin`, `outlineOrigin` and `outlineScale` to the digit. `maxCrateSize` is the smaller of the catalog's `maxContainerScu` and
the file's value (or what the grid takes): the catalog can tighten a door, never loosen one. A `minCrateSize` the
catalog doesn't name stays. A cargo module moves with its ship's Rack 1. A new ship from `-Create` that is a variant of
another (Carrack w/C8X, MOLE editions) takes its base ship's hologram and outline.

The catalog's form, in the field names of Motherlode's API: `{ gameBuild, updatedAt, ships: [ { id, className, name,
manufacturer, cargoScu, containerSizes, grids: [ { name, scu, size, containerSizes, maxContainerScu, external,
transform, reportedMaxSize? } ], modelUrl? } ] }`. A grid with `transform: null` has no place; its ship is kept.

## Importing from Fleetyards

Most ship files were drafts converted from Fleetyards' ship data (D53 addendum); the racks now come from the game
files (above), and the Fleetyards import leaves a ship whose `source` names Motherlode alone: it is neither compared nor
written, and `-Force` on it is an error. Session 4c retires the script. For the three Fleetyards ships, after a
patch:

1. **Fetch:** run `fetch.ps1` of the `sc-ship-outlines` pipeline. It writes `ships.json`, Fleetyards' raw export,
   which stays outside this repo.
2. **Compare:** `./tools/import-fleetyards.ps1 -Source <path>/ships.json`. It compares every file in `ships/` with the
   export and writes nothing. Each ship is listed as `same`, `new`, `different` (with the file's and Fleetyards' value)
   or `skipped` (no cargo grid). Exit code 0 means in sync, 1 a difference or a new ship, 2 an error.
3. **Check the differences:** add new ships with `-Create`. For a changed ship, look at the patch notes or in game, then
   take Fleetyards' values with `-Force <id>`. That rewrites the grid data (grid, `capacityScu`, `totalScu`, `source`)
   and, where Fleetyards lays the racks, `origin`, `position` and a `stackAxis` that fills each rack's box. Rack order,
   a `stackAxis` that already fits, `notes` apart from their arrangement part, door limits (`maxCrateSize`) and the
   origins of a generated row are hand work and stay. If the rack set changed, the racks whose id and grid Fleetyards
   still has keep all their hand work, new racks join behind the last rack that takes more than 2 SCU (walkways last),
   vanished racks are dropped and named, and `notes` record it; if no rack maps, the file is written anew.
   `holoOrigin`, `outlineOrigin` and `outlineScale` stay to the digit in every case, so origins that change move the
   racks against the hull: look at the ship afterwards. `-Create` sets none of them. A rack's `rotation` stays to the digit
   by rack id too, unless the file is written anew, and a ship with a turned rack keeps its racks where the file lays them
   (D91): neither `-Force` nor the compare goes by Fleetyards' arrangement there.
   `hull_a.json` is never written. Its racks come from the game files now; the mapping below is checked against the
   Hull A of before (a frozen copy in the tests).
4. **Commit:** run `dotnet test` (every file must load), then commit the changed files with the export date.

What the import does: the id is the Fleetyards slug without the manufacturer code (`crus-c2-hercules` →
`c2_hercules`), cells are metres divided by 1.25, `length` is Fleetyards' y (along the ship) and `width` its x: the
rack lies as Fleetyards gives it, and crates turn to fit (D71). Fleetyards' rack offsets are used when every rack has
one and none overlap (D78): the offset is the hold's min corner, and in ship cells x = Fleetyards y (towards the bow,
checked on the Starlancer MAX), y = −(Fleetyards x + width) (to port), z = Fleetyards z. A hold with `rotation` 90 or 270
is turned about Fleetyards y (the Hull C's upright arms): its x and z extents swap, its offset is the min corner after
the turn, its grid stays; any other rotation stops the import. That only says how the box lies, which `stackAxis` already
says, so the import never writes a rack's `rotation`. `stackAxis` is `+z` where that lays the grid in the box,
else the first axis that does. `position` comes from the rack's centre against the ship's, and is left out for a rack
on the ship's middle (the Hull C's arms). Otherwise the racks get a generated row, Rack 1 at the bow and one cell between
racks, and no `position`. Racks that take nothing above 2 SCU (ladders, walkways) go last. The compare checks where
Fleetyards lays the racks up to a shift of the whole ship: Fleetyards' offsets are local to the hold, they start at 0, and
the file's zero point is the file's (D90).
The door limit comes from `limits.max.capacity` as described in [Max crate size](#max-crate-size); `maxContainerSize`
isn't read. `-Create` writes it, the compare reports a door the file lacks or holds otherwise, and `-Force` adds a
missing one but never overwrites one already in the file. Racks whose `limits.max.capacity` is an unverified 1 are named
after the ship's line ("limits.max 1 unverified: rack-0") without counting as a difference.

## Cargo modules

Some ships change their hold with a module (the Aurora Mk II's TS module; later the Retaliator, the Caterpillar). A
module is a file of its own in `ships/modules/<id>.json`, not a field in the ship file (D59): every ship it names gets
one more entry in the ship list, its own racks first, the module's after them.

```json
{
  "schemaVersion": 1,
  "id": "ts_module",
  "name": "TS module",
  "ships": ["aurora_mk_ii"],
  "source": "in game (Martin, 2026-10-26)",
  "notes": "platform at the stern …",
  "racks": [
    { "id": "ts-module", "capacityScu": 6, "grid": { "length": 3, "width": 2, "stack": 1 },
      "origin": [-6, -1, -1], "stackAxis": "+z", "maxCrateSize": 2 }
  ]
}
```

| Field | Meaning |
|---|---|
| `schemaVersion` | `1` (the module format's own version, not the ship file's) |
| `id` | lowercase snake_case, equal to the file name |
| `name` | shown after the ship's name: "RSI Aurora Mk II · TS module" |
| `ships` | the ids of the ships that carry it; each becomes the entry `<ship>_<module>` (`aurora_mk_ii_ts_module`) |
| `racks` | as in a ship file; their ids must differ from the ship's, their boxes must not overlap its racks |
| `source`, `notes` | optional, appended to the ship's under the module's name |
| `holoOrigin`, `outlineOrigin`, `outlineScale` | optional, as in a ship file: a module adds racks and doesn't move the hull, so they are left out and the ship's hold. The TS module has its own, three cells aft of the ship's, to keep the picture it had before D90; delete them and its hull lies where the ship's does |

The entry's `totalScu` is the ship's plus the module's racks, its manufacturer and Fleetyards slug are the ship's, so it
shows the ship's outline and hologram. It is checked like a ship file; a broken module, a ship it names that doesn't
load, or a rack that doesn't fit is one line in the ship list's problems and costs only those entries. The import
(`tools/import-fleetyards.ps1`) never touches `modules/`: Fleetyards has no module data, so a module is counted in game.

## Crate sizes

Crates have a shape in cells (length × width × stack). Turned 90° about the rack's stack axis, length and width swap (D71); a 1, 4 or 8 looks the same either way:

| SCU | Shape |
|---|---|
| 1 | 1×1×1 |
| 2 | 2×1×1 |
| 4 | 2×2×1 (flat) |
| 8 | 2×2×2 |
| 16 | 4×2×2 |
| 24 | 6×2×2 |
| 32 | 8×2×2 |

A crate fits a rack if each of its three sides fits the grid, turned or not, and it is no larger than the rack's
[`maxCrateSize`](#max-crate-size). A mix the planner derives is split up to the largest crate any rack takes (D70): a
32 SCU contract on the Hull A becomes two 16 SCU crates, "crates capped to 16 SCU (largest rack)". The planner rejects
a set mix with a crate that fits no rack of the ship. It does not pack crates in 3D: for the Hull A, any mix of crates up
to the rack capacity fits (see [D9](decisions.md)). If a new ship's grid breaks that, open an issue.

### Adding a crate size

1. Add the value to `CrateSize` in [`src/CargoPlan.Core/Models/CrateSize.cs`](../src/CargoPlan.Core/Models/CrateSize.cs).
2. Add its shape to the `Dimensions` switch in the same file.
3. Add one row each to the theories `CrateSize_dimensions_match_the_game` and `CrateSize_volume_equals_scu`
   in `tests/CargoPlan.Core.Tests/ModelsTests.cs`.
4. Add it to `CrateMix.Sizes` in [`src/CargoPlan.Core/Models/CrateMix.cs`](../src/CargoPlan.Core/Models/CrateMix.cs)
   at its place (largest first), if the freight elevator uses it when it works out the crates of a contract (D23).
5. Add the size to the `maxCrateSize` list in [mission-format.md](mission-format.md) and to the table above.

That's it: mission files and the editor accept the new size, and the shape check and the planner's pre-check
use it automatically.

## Checklist for a new ship PR

- [ ] File is `ships/<id>.json`, lowercase `snake_case`, and `id` inside matches.
- [ ] `schemaVersion` is `6`.
- [ ] Rack capacities add up to `totalScu`, and each rack's `length × width × stack` equals its `capacityScu`.
- [ ] Racks are listed **in rack order** (the order to fill them), each with its `origin` and, where known, its `position`.
- [ ] Racks don't overlap, and `origin` agrees with `position` if given (port = larger y than the ship's middle, upper = higher z).
- [ ] `holoOrigin` and `outlineOrigin` are set, see [Hologram and outline](#hologram-and-outline).
- [ ] Each rack's `stackAxis` points away from its plate, as in the game.
- [ ] A `rotation` only for a real angle, checked in the picture from two sides, with its source named.
- [ ] Values measured in game (current patch named in the PR description), or their source named in `source`.
- [ ] `maxCrateSize` only where the door takes less than the grid, `minCrateSize` only above 1, both with their source.
- [ ] `dotnet test` passes. The test `Every_shipped_ship_loads` loads every ship file and reports the
      exact problem (file name, field, value) if something is wrong; `LoadAll_loads_every_shipped_file_without_problems`
      fails if any file in `ships/` would be skipped.
