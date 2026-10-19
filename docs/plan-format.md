# Plan file format

A plan file holds one hauling run: the missions you entered and the plan made from them, with your
moves and ticks. The overlay saves the current plan to `%LocalAppData%\SC-CargoPlan\plan.json` on every
change, so it survives a restart and a crash. You don't need to write it by hand; this page explains
what is in it.

## Example

One stop, 8 SCU Titanium (max crate 4) and 4 SCU Quartz (max crate 2), all in Rack 1: two 4 SCU crates on
the plate, the two 2 SCU crates on top of them. Saved files write each position over several lines; the
reader doesn't mind.

```json
{
  "schemaVersion": 2,
  "shipId": "hull_a",
  "missions": {
    "schemaVersion": 2,
    "stops": [
      {
        "destination": "Riker",
        "items": [
          { "commodity": "Titanium", "scu": 8, "maxCrateSize": 4 },
          { "commodity": "Quartz", "scu": 4, "maxCrateSize": 2 }
        ]
      }
    ]
  },
  "fillOrder": "bowFirst",
  "crates": [
    { "destination": "Riker", "commodity": "Titanium", "crateSize": 4, "rackId": "rack-0", "position": [2, 0, 0], "delivered": false },
    { "destination": "Riker", "commodity": "Titanium", "crateSize": 4, "rackId": "rack-0", "position": [0, 0, 0], "delivered": false },
    { "destination": "Riker", "commodity": "Quartz", "crateSize": 2, "rackId": "rack-0", "position": [2, 0, 1], "delivered": false },
    { "destination": "Riker", "commodity": "Quartz", "crateSize": 2, "rackId": "rack-0", "position": [0, 0, 1], "delivered": false }
  ],
  "warnings": []
}
```

## Fields

All fields are **required**. Names are camelCase and **case-sensitive**. Unknown fields are an error.

### File

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | number | `2`. Files with `1` are still read, see [Older plan files](#older-plan-files). |
| `shipId` | string | The ship the plan is for, e.g. `"hull_a"` ([ship files](ship-format.md)). |
| `missions` | object | A complete [mission file](mission-format.md), with its own `schemaVersion`. Both mission versions are read; saving writes version 2. You can copy it out and use it as a mission file. |
| `fillOrder` | string | `"bowFirst"` or `"sternFirst"`: which end of a rack's length crates fill from when they are laid out again, e.g. after a move. Part of the plan, so everyone loading it sees the same layout. |
| `crates` | array | Every crate: what, for whom, which rack, which cell. |
| `warnings` | array | The planner's hints, word for word as the overlay shows them. Stored, not computed again on loading. |

### Crate

| Field | Type | Rules |
|---|---|---|
| `destination` | string | A destination from `missions`, same spelling. |
| `commodity` | string | The commodity, as in `missions`. |
| `crateSize` | number | 1, 2, 4, 8, 16, 24 or 32 (SCU). Crates are never turned: 1 = 1×1×1 cells, 2 = 2×1×1, 4 = 2×2×1, 8 = 2×2×2, 16 = 4×2×2, 24 = 6×2×2, 32 = 4×4×2 (length × width × stack). |
| `rackId` | string | A rack `id` of the ship. |
| `position` | array | `[length, width, stack]`, the crate's corner cell counted from 0 at the rack's corner: length along the rack, width across it, stack away from the plate. The whole crate must lie inside the rack's `grid`. |
| `delivered` | boolean | `true` once the stop is ticked off. All crates of a stop are ticked together. A delivered crate keeps its cells, so ticking back restores the plan. |

### Warning

| Field | Type | Rules |
|---|---|---|
| `rackId` | string | A rack `id` of the ship. |
| `message` | string | The hint text. |

The crates must match the missions exactly: for every destination, commodity and crate size there are as
many crates as the missions have (worked out from SCU and max crate size, or listed in `crates`).

A crate rests on the plate (stack 0) or on other crates: every cell of its bottom needs a crate right
below it. Crates that overlap or have nothing below them still load, the overlay marks their rack red;
the planner and the overlay's moves never make them.

## Older plan files

Plan files from before crate positions have `"schemaVersion": 1` and a `placements` array (cargo per rack,
no cells) instead of `fillOrder` and `crates`. They are still read: each rack's cargo is laid out the way
a new plan would be, ticks included, and the overlay saves the file as schema 2 right after loading it.
One exception: version 1 allowed more cargo in a rack than it holds (after a manual move); such a plan
can't be laid out and shows the error below.

## Errors

The overlay never overwrites a plan file it can't read. It starts with the empty ship and shows why in
an amber line, for example:

```
Plan not loaded (Plan file 'C:\Users\you\AppData\Local\SC-CargoPlan\plan.json' is invalid: The plan is for ship 'cutlass_black', not 'hull_a'.). The file stays until you make a new plan.
```

A crate outside its rack, a rack the ship doesn't have, or crates that don't match the missions are such
errors. The file stays as it is until a new plan is saved.

## Loading a plan file at start

`CargoPlan.App.exe --plan <file>` shows that plan file instead of the saved one. It becomes the saved
plan on the next save. Meant for development and for trying out the overlay without the editor.
