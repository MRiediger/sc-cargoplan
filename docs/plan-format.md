# Plan file format

A plan file holds one hauling run: the missions you entered or ContractTracker captured, the plan made
from them, with your moves and ticks. The overlay saves the current plan to
`%LocalAppData%\SC-CargoPlan\plan.json` on every change, so it survives a restart and a crash. You don't
need to write it by hand; this page explains what is in it.

## Example

Two missions. Mission 1 was typed in the editor: Riker, 8 SCU Titanium (max crate 4) collected at Lorville,
two 4 SCU crates on the plate of Rack 1. Mission 2 came from ContractTracker (capture ids 36 and 37): Shubin, 4 SCU Quartz; the
contract named no max crate size, so the largest one that fits was assumed, and its drop-off list named
three destinations of which one was read. Saved files write each position over several lines; the reader
doesn't mind.

```json
{
  "schemaVersion": 4,
  "shipId": "hull_a",
  "fillOrder": "bowFirst",
  "contractTrackerWatermark": 37,
  "missions": [
    {
      "id": 1,
      "unplaced": false,
      "stops": [
        { "destination": "Riker", "items": [ { "commodity": "Titanium", "scu": 8, "maxCrateSize": 4, "pickup": ["Lorville"] } ] }
      ]
    },
    {
      "id": 2,
      "unplaced": false,
      "stops": [
        { "destination": "Shubin", "items": [ { "commodity": "Quartz", "scu": 4, "maxCrateSize": 4 } ] }
      ],
      "contract": { "contractIds": [36, 37], "crateSizeAssumed": true, "listedDestinations": 3, "reward": 50250 }
    }
  ],
  "crates": [
    { "destination": "Riker", "commodity": "Titanium", "crateSize": 4, "rackId": "rack-0", "position": [2, 0, 0], "delivered": false, "mission": 1, "item": 0 },
    { "destination": "Riker", "commodity": "Titanium", "crateSize": 4, "rackId": "rack-0", "position": [0, 0, 0], "delivered": false, "mission": 1, "item": 0 },
    { "destination": "Shubin", "commodity": "Quartz", "crateSize": 4, "rackId": "rack-1", "position": [2, 0, 0], "delivered": false, "mission": 2, "item": 0 }
  ],
  "warnings": []
}
```

## Fields

All fields are **required** unless marked optional. Names are camelCase and **case-sensitive**. Unknown
fields are an error.

### File

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | number | `4`. Files with `1`, `2` and `3` are still read, see [Older plan files](#older-plan-files). |
| `shipId` | string | The ship the plan is for, e.g. `"hull_a"` ([ship files](ship-format.md)). |
| `fillOrder` | string | `"bowFirst"` or `"sternFirst"`: which end of a rack's length crates fill from, when planned and when laid out again, e.g. after a move. Chosen as **Fill order** in the editor. Part of the plan, so everyone loading it sees the same layout. |
| `contractTrackerWatermark` | number | The highest ContractTracker id taken into the plan, `0` for none. Captures above it are new; it only rises. |
| `missions` | array | Every mission: what you typed or ContractTracker captured, one contract each. May be empty. |
| `crates` | array | Every crate: what, for whom, for which mission, which rack, which cell. Their order is the route: a stop is unloaded in the order of its first crate still on board. |
| `warnings` | array | The planner's hints, word for word as the overlay shows them. Stored; on loading they are derived anew from the crates, so notes an older version wrote (e.g. "Racks not in route order") drop and positions and ticks stay. |

### Mission

| Field | Type | Rules |
|---|---|---|
| `id` | number | 1 or more, unique in the file. Crates name their mission by it. |
| `unplaced` | boolean | `true` if the mission didn't fit when it came; it then has no crates. The overlay places it as soon as it fits, at the next sync with ContractTracker. |
| `stops` | array | Its stops as in a [mission file](mission-format.md): `destination` and `items` with `commodity`, `scu`, `maxCrateSize` and optionally `crates` and `pickup`. One stop per destination; the same destination in two missions has the same spelling. |
| `contract` | object | Optional: the ContractTracker capture it came from. Missing for missions typed in the editor or read from a mission file. |

Missions to the same destination share one stop at the cargo lift: one tick. The legend shows it once, or once per
pickup list when items name their pickup.

### Capture

| Field | Type | Rules |
|---|---|---|
| `contractIds` | array | ContractTracker's ids of the capture's rows, at least one. Once all of them are deleted in ContractTracker and nothing of the mission is on board, the mission leaves the plan. |
| `crateSizeAssumed` | boolean | The contract named no max crate size; the legend shows the size with a `?`. |
| `listedDestinations` | number | Optional: how many destinations the contract's drop-off list names. More than the mission's stops: the legend warns that destinations are missing. |
| `reward` | number | Optional: the contract's reward as read from the capture. Left out when unknown. Two captures with different rewards are never marked as duplicates. |

### Crate

| Field | Type | Rules |
|---|---|---|
| `destination` | string | A destination of its mission, same spelling. |
| `commodity` | string | The commodity, as in its mission. |
| `crateSize` | number | 1, 2, 4, 8, 16, 24 or 32 (SCU). Crates are never turned: 1 = 1×1×1 cells, 2 = 2×1×1, 4 = 2×2×1, 8 = 2×2×2, 16 = 4×2×2, 24 = 6×2×2, 32 = 8×2×2 (length × width × stack). |
| `rackId` | string | A rack `id` of the ship. |
| `position` | array | `[length, width, stack]`, the crate's corner cell counted from 0 at the rack's corner: length along the rack, width across it, stack away from the plate. The whole crate must lie inside the rack's `grid`. |
| `delivered` | boolean | `true` once the stop is ticked off. All crates of a stop are ticked together. A delivered crate is off the ship: its cells are free for new crates, and it carries nothing. Ticking back works until another crate takes one of its cells. |
| `mission` | number | The `id` of the mission the crate belongs to; not an `unplaced` one. |
| `item` | number | Which item of its mission the crate belongs to, counted from 0 over the mission's stops and their items in file order. Its destination and commodity are the crate's. Two items of one kind (e.g. from two pickups) keep their own crates. |

### Warning

| Field | Type | Rules |
|---|---|---|
| `rackId` | string | A rack `id` of the ship. |
| `message` | string | The hint text. |

The crates must match the missions exactly: for every item of every mission and every crate size there are as
many crates as the item has (worked out from SCU and max crate size, or listed in `crates`).
Splitting or merging crates in the overlay changes both: the item's `crates` list then names the crates as
they lie, and goes away once they are the worked-out mix again.

A crate rests on the plate (stack 0) or on other crates on board: every cell of its bottom needs a crate
right below it. Crates that overlap or have nothing below them still load, the overlay marks their rack
red; the planner and the overlay's moves never make them.

## Older plan files

All older versions are read and saved as schema 4 right after loading; moves and ticks stay.

- **Schema 3** (before crates named their item) has crates without `item`. Each mission's crates of one kind
  take its items of that kind in order.

- **Schema 2** (before missions had ids) has `missions` as one embedded [mission file](mission-format.md)
  and crates without `mission`. Each stop becomes a mission of its own, its crates belong to it, and the
  watermark is 0.
- **Schema 1** (before crate positions) has a `placements` array (cargo per rack, no cells) instead of
  `fillOrder` and `crates`. Each rack's cargo is laid out the way a new plan would be, ticks included, then
  read as schema 2. One exception: version 1 allowed more cargo in a rack than it holds (after a manual
  move); such a plan can't be laid out and shows the error below.

## A plan for another ship

A plan file whose `shipId` is not the overlay's ship loads anyway: its missions are planned anew for the
overlay's ship, as a ship change in the overlay header does (D59). Missions, ContractTracker ids, the watermark, the fill
order and the ticks of whole stops stay; crate positions and manual moves don't. If the missions don't fit as a
whole plan, they go in mission by mission and the rest waits as "doesn't fit" in the legend. An amber line says
"Plan was made for CRUS C2 Hercules, replanned for MISC Hull A.", and the next save writes the plan for the new ship.
Its crates are not checked against the old ship's racks; the crates against their missions still are.

Without a `ship` in [settings.json](settings-format.md), the overlay takes the plan's ship instead, if it loads.

A schema 1 file for another ship is an error (below): laying out its cargo needs that ship's grid.

## Errors

The overlay never overwrites a plan file it can't read. It starts with the empty ship and shows why in
an amber line, for example:

```
Plan not loaded (Plan file 'C:\Users\you\AppData\Local\SC-CargoPlan\plan.json' is invalid: Crate for Riker is in rack 'rack-9', which ship 'hull_a' doesn't have.). The file stays until you make a new plan.
```

A crate outside its rack, a rack the ship doesn't have, a crate of a mission the file doesn't have, or
crates that don't match their missions are such errors. The file stays as it is until a new plan is saved.

## Loading a plan file at start

`CargoPlan.App.exe --plan <file>` shows that plan file instead of the saved one. It becomes the saved
plan on the next save. Meant for development and for trying out the overlay without the editor.
