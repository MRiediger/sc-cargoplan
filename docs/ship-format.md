# Ship file format

Ships are data, not code. Each ship is one JSON file in
[`src/CargoPlan.Core/Ships/`](../src/CargoPlan.Core/Ships/). Every `*.json` in that folder is compiled
into `CargoPlan.Core.dll` as an embedded resource named `ships/<file name>`, so adding a ship means
adding one file. No C# and no `.csproj` change needed.

## Example

`src/CargoPlan.Core/Ships/hull_a.json` (shortened to two of four racks):

```json
{
  "schemaVersion": 3,
  "id": "hull_a",
  "name": "MISC Hull A",
  "totalScu": 64,
  "racks": [
    {
      "id": "rack-0",
      "capacityScu": 16,
      "grid": { "length": 4, "width": 2, "stack": 2 },
      "position": { "side": "port", "level": "upper" },
      "origin": [0, 1, 3]
    },
    {
      "id": "rack-1",
      "capacityScu": 16,
      "grid": { "length": 4, "width": 2, "stack": 2 },
      "position": { "side": "port", "level": "lower" },
      "origin": [0, 1, 0]
    }
  ]
}
```

## Fields

All fields are **required**. Names are camelCase and **case-sensitive**, and so are the values of
`side` and `level`. Unknown fields are an error, so a typo like `capacitySCU` fails the build instead of
being ignored silently.

### Ship

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | number | Must be `3`. Bumped only when the format changes incompatibly; older files are rejected with the version they have and the one expected. |
| `id` | string | Must equal the file name without `.json` (`hull_a.json` → `"hull_a"`). Use lowercase `snake_case`: Linux file systems are case-sensitive. |
| `name` | string | Display name, e.g. `"MISC Hull A"`. |
| `totalScu` | number | Cargo capacity of the ship. Must equal the sum of all `capacityScu`. |
| `racks` | array | At least one rack. **Order matters:** the array order is the **rack order** (Rack 1, Rack 2, …). The planner fills racks in this order, puts the first stop into Rack 1 and lets only racks next to each other in this order share a stop. The rack order says nothing about where a rack sits; that is `position`. |

### Rack

A rack is a separately loadable section of the cargo hold, usually one plate.

| Field | Type | Rules |
|---|---|---|
| `id` | string | Unique within the ship. Convention: `rack-0`, `rack-1`, … in rack order. Players see "Rack 1", "Rack 2", … |
| `capacityScu` | number | Greater than 0. Must equal `length × width × stack` of the grid. |
| `grid` | object | The rack's cell grid, see below. |
| `position` | object | Where the rack sits in player words ("port upper"), see below. Shown as text, not used for planning. |
| `origin` | array | Where the rack sits in ship coordinates: `[x, y, z]`, three whole numbers, see [Coordinates](#coordinates). Used to draw the ship, not for planning. Racks must not overlap. |

### Grid

One cell is 1 SCU, a cube of 1.25 m.

| Field | Type | Rules |
|---|---|---|
| `length` | number | Cells along the plate (on the Hull A: along the spindle). Greater than 0. |
| `width` | number | Cells across the plate. Greater than 0. |
| `stack` | number | Cells crates stack away from the plate. Greater than 0. |

For drawing, `length`/`width`/`stack` map to x/y/z. The physical stacking direction is not modelled
(backlog: `orientation`).

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
- `origin` is the rack's **minimum corner** (smallest x, y and z). The rack fills `origin` to
  `origin + [length, width, stack]`.
- Grid axes are fixed to the ship axes: `length` along x, `width` along y, `stack` along z. Racks are not rotated.
- Where the zero point lies is up to you; only the positions of the racks relative to each other matter.
  Racks may touch but must not overlap. The loader names both racks if they do.

Hull A, drawn schematically (no measurement) with one cell of gap for the spindle and between the levels:

| Rack | Position | `origin` | Fills x · y · z |
|---|---|---|---|
| 1 | port upper | `[0, 1, 3]` | 0..4 · 1..3 · 3..5 |
| 2 | port lower | `[0, 1, 0]` | 0..4 · 1..3 · 0..2 |
| 3 | starboard upper | `[0, -2, 3]` | 0..4 · -2..0 · 3..5 |
| 4 | starboard lower | `[0, -2, 0]` | 0..4 · -2..0 · 0..2 |

## Measuring a ship

In the hangar, load the empty hold with 1 SCU crates. For each plate, count the cells along it
(`length`), across it (`width`) and how many crates stack away from it (`stack`); `capacityScu` is the
product. Note which side (facing the bow) and level each plate is on (`position`). Then decide the rack
order: the order in which haulers should fill the plates for their first, second, … stop.

For `origin`, sketch the hold from above and from behind on cell paper: pick a zero point, put each rack's
minimum corner on the grid and leave a cell of gap where something sits between the racks (a spindle, a floor).
A schematic layout is enough; the overlay draws boxes, not the ship.

If you take grid sizes measured by others, such as a community catalogue, check its license before copying
values into a ship file, and name the source in your PR.

## Crate sizes

Crates have a fixed shape in cells (length × width × stack) and are never turned:

| SCU | Shape |
|---|---|
| 1 | 1×1×1 |
| 2 | 2×1×1 |
| 4 | 2×2×1 (flat) |
| 8 | 2×2×2 |
| 16 | 4×2×2 |
| 24 | 6×2×2 |
| 32 | 4×4×2 |

A crate fits a rack if each of its three sides fits the grid. The planner rejects a mission with a
crate that fits no rack of the ship. It does not pack crates in 3D: for the Hull A, any mix of crates up
to the rack capacity fits (see [D9](decisions.md)). If a new ship's grid breaks that, open an issue.

### Adding a crate size

1. Add the value to `CrateSize` in [`src/CargoPlan.Core/Models/CrateSize.cs`](../src/CargoPlan.Core/Models/CrateSize.cs).
2. Add its shape to the `Dimensions` switch in the same file.
3. Add one row each to the theories `CrateSize_dimensions_match_the_game` and `CrateSize_volume_equals_scu`
   in `tests/CargoPlan.Core.Tests/ModelsTests.cs`.
4. Add the size to the `crateSize` list in [mission-format.md](mission-format.md) and to the table above.

That's it: mission files accept the new size, and the shape check and the planner's pre-check use it
automatically.

## Checklist for a new ship PR

- [ ] File is `src/CargoPlan.Core/Ships/<id>.json`, lowercase `snake_case`, and `id` inside matches.
- [ ] `schemaVersion` is `3`.
- [ ] Rack capacities add up to `totalScu`, and each rack's `length × width × stack` equals its `capacityScu`.
- [ ] Racks are listed **in rack order** (the order to fill them), each with its `position` and `origin`.
- [ ] Racks don't overlap, and `origin` agrees with `position` (port = positive y, upper = higher z).
- [ ] Values measured in game (current patch named in the PR description).
- [ ] `dotnet test` passes. The test `Every_shipped_ship_loads` loads every ship file and reports the
      exact problem (file name, field, value) if something is wrong.
