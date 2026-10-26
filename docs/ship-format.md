# Ship file format

Ships are data, not code. Each ship is one JSON file in [`ships/`](../ships/) in the repository root. The build
copies the folder next to the exe, and the release ZIP carries it as `SC-CargoPlan/ships/` (see [D56](decisions.md)),
so adding a ship means adding one file. No C# and no `.csproj` change needed.

The app reads every `*.json` in that folder at start. A file that is broken costs only its own ship; the reason is
reported (the CLI prints it, the app will show it with the ship selection). Without the folder, or without a valid
`hull_a.json`, the app doesn't start and asks to unzip the release again: the Hull A is the fallback ship.

## Example

`ships/hull_a.json` (shortened to two of four racks):

```json
{
  "schemaVersion": 5,
  "id": "hull_a",
  "name": "MISC Hull A",
  "manufacturer": "MISC",
  "fleetyardsSlug": "misc-hull-a",
  "source": "in game",
  "totalScu": 64,
  "racks": [
    {
      "id": "rack-0",
      "capacityScu": 16,
      "grid": { "length": 4, "width": 2, "stack": 2 },
      "position": { "side": "port", "level": "upper" },
      "origin": [0, 1, 3],
      "stackAxis": "+y"
    },
    {
      "id": "rack-1",
      "capacityScu": 16,
      "grid": { "length": 4, "width": 2, "stack": 2 },
      "position": { "side": "port", "level": "lower" },
      "origin": [0, 1, 0],
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
| `schemaVersion` | number | Must be `5`; `4` is still read, the same way. Bumped when the format changes incompatibly or a field changes its meaning (5: `maxCrateSize` is the door limit, D70); older files are rejected with the version they have and the ones expected. |
| `id` | string | Must equal the file name without `.json` (`hull_a.json` → `"hull_a"`). Use lowercase `snake_case`: Linux file systems are case-sensitive. |
| `name` | string | Display name, e.g. `"MISC Hull A"`. |
| `manufacturer` | string | *Optional.* Manufacturer code as Fleetyards writes it, e.g. `"MISC"`, `"RSI"`; ships are grouped by it. Not empty if present. |
| `fleetyardsSlug` | string | *Optional.* The ship's slug on Fleetyards, e.g. `"misc-hull-a"`. It picks the ship's outline from `outlines/ship-outlines.json` (traced from Fleetyards renders, not under the MIT licence, see `outlines/NOTICE.md`); without a match the ship view shows the bow wedge. Not empty if present. |
| `source` | string | *Optional.* Where the values come from, e.g. `"in game"` (measured in the hangar) or `"Fleetyards"`. Not empty if present. |
| `notes` | string | *Optional.* Anything a reviewer should know, e.g. what was checked and what wasn't. Not empty if present. |
| `holoOffset` | array of 3 integers | *Optional.* `[x, y, z]` in cells: moves the hologram from its middle on the racks, e.g. `[0, 0, 1]` lifts it one cell so the racks sit lower in the hull (Aurora Mk II). It corrects the hologram's middle position against the racks; it is no position of the ship. Whoever sets a value has seen it at the holo gate – source and date go into `notes`, as for limits set by hand. Racks, plans and the outline don't move; a ship with a [cargo module](#cargo-modules) passes it on. The import writes it back on `-Force` and never sets it. |
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
| `origin` | array | Where the rack sits in ship coordinates: `[x, y, z]`, three whole numbers, see [Coordinates](#coordinates). Used to draw the ship, not for planning. Racks must not overlap. |
| `stackAxis` | string | The ship direction crates stack in, away from the plate: `"+x"`, `"-x"`, `"+y"`, `"-y"`, `"+z"` or `"-z"`, see [Stack axis](#stack-axis). |
| `maxCrateSize` | number | *Optional.* The door limit: the largest crate that gets through the rack's door, in SCU (`1`, `2`, `4`, `8`, `16`, `24` or `32`), where that is less than the grid takes. Left out, the grid alone decides. See [Max crate size](#max-crate-size). |
| `minCrateSize` | number | *Optional.* The smallest crate the rack takes, in SCU, where that is more than 1 (the Hull C's spindle: `32`). Left out, any crate the grid and `maxCrateSize` allow. See [Min crate size](#min-crate-size). |

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
what players say, `origin` is geometry. Keep them consistent (a port rack has a positive y, see below).

## Coordinates

All racks of a ship share one coordinate system (see [D14](decisions.md)):

- **Right-handed:** x points to the **bow**, y to **port**, z **up**.
- **Unit:** 1 = one cell = the edge of a 1 SCU crate (1.25 m). Only whole numbers.
- `origin` is the rack's **minimum corner** (smallest x, y and z), whichever way the rack stacks. The rack
  fills `origin` to `origin` plus its grid laid along the axes of its [stack axis](#stack-axis).
- Where the zero point lies is up to you; only the positions of the racks relative to each other matter.
  Racks may touch but must not overlap. The loader names both racks if they do.

### Stack axis

`stackAxis` says which way crates stack, away from the plate (see [D26](decisions.md)). Crates always lie flat
against their plate, so it is the direction a second layer goes: up from a floor, down from a ceiling or a hull
underside, outwards from a spindle. The other two grid axes follow from it, there is no free rotation:

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

## Importing from Fleetyards

Most ship files are drafts converted from Fleetyards' ship data (D53 addendum). To bring them up to date after a
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
   `hull_a.json` is never written, but it is compared: it is the check of the mapping below.
4. **Commit:** run `dotnet test` (every file must load), then commit the changed files with the export date.

What the import does: the id is the Fleetyards slug without the manufacturer code (`crus-c2-hercules` →
`c2_hercules`), cells are metres divided by 1.25, `length` is Fleetyards' y (along the ship) and `width` its x: the
rack lies as Fleetyards gives it, and crates turn to fit (D71). Fleetyards' rack offsets are used when every rack has
one and none overlap (D78): the offset is the hold's min corner, and in ship cells x = Fleetyards y (towards the bow,
checked on the Starlancer MAX), y = −(Fleetyards x + width) (to port), z = Fleetyards z. A hold with `rotation` 90 or 270
is turned about Fleetyards y (the Hull C's upright arms): its x and z extents swap, its offset is the min corner after
the turn, its grid stays; any other rotation stops the import. `stackAxis` is `+z` where that lays the grid in the box,
else the first axis that does. `position` comes from the rack's centre against the ship's, and is left out for a rack
on the ship's middle (the Hull C's arms). Otherwise the racks get a generated row, Rack 1 at the bow and one cell between
racks, and no `position`. Racks that take nothing above 2 SCU (ladders, walkways) go last. The compare checks where
Fleetyards lays the racks up to a shift of the whole ship: the view hangs on the middle of the ship's bounds.
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
- [ ] `schemaVersion` is `5`.
- [ ] Rack capacities add up to `totalScu`, and each rack's `length × width × stack` equals its `capacityScu`.
- [ ] Racks are listed **in rack order** (the order to fill them), each with its `origin` and, where known, its `position`.
- [ ] Racks don't overlap, and `origin` agrees with `position` if given (port = positive y, upper = higher z).
- [ ] Each rack's `stackAxis` points away from its plate, as in the game.
- [ ] Values measured in game (current patch named in the PR description), or their source named in `source`.
- [ ] `maxCrateSize` only where the door takes less than the grid, `minCrateSize` only above 1, both with their source.
- [ ] `dotnet test` passes. The test `Every_shipped_ship_loads` loads every ship file and reports the
      exact problem (file name, field, value) if something is wrong; `LoadAll_loads_every_shipped_file_without_problems`
      fails if any file in `ships/` would be skipped.
