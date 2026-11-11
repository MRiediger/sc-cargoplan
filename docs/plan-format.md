# Plan file format

A plan file holds one hauling run: the missions you entered or scanned, the plan made
from them, with your moves and ticks. The overlay saves the current plan to
`%LocalAppData%\SC-CargoPlan\plan.json` on every change, so it survives a restart and a crash. You don't
need to write it by hand; this page explains what is in it.

## Example

Three missions. Mission 1 was typed in the editor: Riker, 8 SCU Titanium (max crate 4) collected at Lorville,
two 4 SCU crates on the plate of Rack 1. Mission 2 is a capture imported before v0.4.0 (ids 36 and 37): Shubin, 4 SCU Quartz; the
contract named no max crate size, so the largest one that fits was assumed, and its drop-off list named
three destinations of which one was read. Mission 3 was scanned by CargoPlan itself, scan #3 of the plan, from the offers
view: Orison, 2 SCU Stims, its amount an estimate until the accepted contract is scanned. Saved files write each position
over several lines; the reader doesn't mind.

```json
{
  "schemaVersion": 10,
  "shipId": "hull_a",
  "fillOrder": "bowFirst",
  "fillRows": "lengthwise",
  "grouping": "none",
  "sectionGap": 0,
  "lastScanNo": 3,
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
    },
    {
      "id": 3,
      "unplaced": false,
      "stops": [
        { "destination": "Orison", "items": [ { "commodity": "Stims", "scu": 2, "maxCrateSize": 2, "pickup": ["Everus Harbor"] } ] }
      ],
      "contract": {
        "crateSizeAssumed": false,
        "reward": 35500,
        "scan": { "no": 3, "key": "EVERUSHARBOR | ORISON:STIMS | 2 | 35500", "estimated": true }
      }
    }
  ],
  "crates": [
    { "destination": "Riker", "commodity": "Titanium", "crateSize": 4, "rackId": "rack-0", "position": [2, 0, 0], "delivered": false, "mission": 1, "item": 0 },
    { "destination": "Riker", "commodity": "Titanium", "crateSize": 4, "rackId": "rack-0", "position": [0, 0, 0], "delivered": false, "mission": 1, "item": 0 },
    { "destination": "Shubin", "commodity": "Quartz", "crateSize": 4, "rackId": "rack-1", "position": [2, 0, 0], "delivered": false, "mission": 2, "item": 0 },
    { "destination": "Orison", "commodity": "Stims", "crateSize": 2, "rackId": "rack-2", "position": [0, 0, 0], "delivered": false, "mission": 3, "item": 0 }
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
| `schemaVersion` | number | `10`. Files with `8` and `9` are still read, see [Older plan files](#older-plan-files). |
| `shipId` | string | The ship the plan is for, e.g. `"hull_a"` ([ship files](ship-format.md)). |
| `fillOrder` | string | `"bowFirst"` or `"sternFirst"`: which end of a rack's length crates fill from, when planned and when laid out again, e.g. after a move. Chosen as **Fill order** in the editor. Part of the plan, so everyone loading it sees the same layout. |
| `fillRows` | string | `"lengthwise"` or `"crosswise"`: which way the rows of a layout run. Lengthwise rows run along the rack's length and step across its width; crosswise rows run across the width and step along the length, from the end `fillOrder` names. Width counts from starboard to port either way. Chosen as **Rows** in the editor. A rack that stacks along `+y` or `-y`, like the Hull A's plates, ignores it. |
| `grouping` | string | `"none"` or `"byDestination"`: whether Plan and Re-plan keep each destination as a block of its own in a rack it shares. Blocks run across the width with `crosswise` and along the length with `lengthwise`; a rack that stacks along `+y` or `-y` runs them along its length either way. Chosen as **Grouping** in the editor. Optional; without it, `"none"`. |
| `sectionGap` | number | `0`, `1` or `2`: empty rows between neighbouring blocks with `"byDestination"`, to grip the crates. If they don't fit, crates turn where that saves rows, then the gaps shrink; the overlay doesn't mention it. Chosen as **Section gap** in the editor. Optional; without it, `0`. |
| `lastScanNo` | number | Optional (schema 10): the last scan number given, at least the highest `scan.no` in the file; left out while no contract was scanned. A number is given once in a session, also after an undo; **Discard plan** starts the count again. |
| `contractTrackerWatermark` | number | Before v0.4.0 the highest capture id taken into the plan, `0` for none. Read and kept as it is; nothing raises it since v0.4.0. |
| `missions` | array | Every mission: what you typed or scanned (or imported before v0.4.0), one contract each. May be empty. |
| `crates` | array | Every crate: what, for whom, for which mission, which rack, which cell. Their order is the route: a stop is unloaded in the order of its first crate still on board. |
| `warnings` | array | The planner's hints, word for word as the overlay shows them. Stored; on loading they are derived anew from the crates, so notes an older version wrote (e.g. "Racks not in route order") drop and positions and ticks stay. |

### Mission

| Field | Type | Rules |
|---|---|---|
| `id` | number | 1 or more, unique in the file. Crates name their mission by it. |
| `unplaced` | boolean | `true` if the mission didn't fit when it came; it then has no crates. Once it fits, Re-plan, the editor's Plan or an undo places it. |
| `stops` | array | Its stops as in a [mission file](mission-format.md): `destination` and `items` with `commodity`, `scu`, `maxCrateSize` and optionally `crates` and `pickup`, here also `manual` (written only as `true`): the item was added to this scanned or captured mission or changed by hand in the editor and stays with it. One stop per destination; the same destination in two missions has the same spelling. |
| `contract` | object | Optional: the scan it came from, or a capture imported before v0.4.0. Missing for missions typed in the editor or read from a mission file. |

Missions to the same destination share one stop at the cargo lift: one tick. The legend shows it once, or once per
pickup list when items name their pickup.

### Capture

A contract has either `contractIds` (a capture imported before v0.4.0, D39) or `scan` (read by CargoPlan itself), exactly one of the
two.

| Field | Type | Rules |
|---|---|---|
| `contractIds` | array | The capture's ids, at least one, as the legend and the editor show them ("#36–37"). Read and kept as they are; since v0.4.0 nothing compares them with a list. Left out for a scan. |
| `scan` | object | Schema 10, instead of `contractIds`: `no` (1 or more, once in the file, shown as `#3`), `key` (what the contract is: pickups, destinations with their commodities, max crate size and reward, no SCU; two scans with one key are one contract) and `estimated` (read from the offers view: the game rolls the amounts again when the contract is accepted). A scan is never `gone`. In a schema 8 or 9 file it is an error. |
| `crateSizeAssumed` | boolean | The contract named no max crate size; the legend shows the size with a `?`. |
| `listedDestinations` | number | Optional: how many destinations the contract's drop-off list names. More than the mission's stops: the legend warns that destinations are missing. |
| `reward` | number | Optional: the contract's reward as read from the capture. Left out when unknown. Two captures with different rewards are never marked as duplicates. |
| `gone` | boolean | Optional, written only as `true` (schema 8): before v0.4.0 the capture had left the list it came from (D81). Read and kept, not shown since v0.4.0 (D105); a waiting mission with it isn't placed. |
| `detached` | boolean | Optional, written only as `true` (schema 8), only with `gone`: the ids were an earlier database's (D81). Read and kept. Without `gone` it is an error. |

### Crate

| Field | Type | Rules |
|---|---|---|
| `destination` | string | A destination of its mission, same spelling. |
| `commodity` | string | The commodity, as in its mission. |
| `crateSize` | number | 1, 2, 4, 8, 16, 24 or 32 (SCU). Unturned: 1 = 1×1×1 cells, 2 = 2×1×1, 4 = 2×2×1, 8 = 2×2×2, 16 = 4×2×2, 24 = 6×2×2, 32 = 8×2×2 (length × width × stack). |
| `rackId` | string | A rack `id` of the ship; `""` for a crate that is `offShip`. |
| `position` | array | `[length, width, stack]`, the crate's corner cell counted from 0 at the rack's corner: length along the rack, width across it, stack away from the plate. The whole crate must lie inside the rack's `grid`. `[0, 0, 0]` and not checked for a crate that is `offShip`. |
| `delivered` | boolean | `true` once its delivery is ticked off: a stop (every mission's crates for it) or one mission's crates for it (a legend line). A delivered crate is off the ship: its cells are free for new crates, and it carries nothing; what lay on it settles straight down. Ticked back, it goes back into its cells while they are free, else into free cells like new cargo, or is refused if there is no room. |
| `mission` | number | The `id` of the mission the crate belongs to; not an `unplaced` one. |
| `item` | number | Which item of its mission the crate belongs to, counted from 0 over the mission's stops and their items in file order. Its destination and commodity are the crate's. Two items of one kind (e.g. from two pickups) keep their own crates. |
| `rotated` | boolean | Optional, written only as `true`: the crate is turned 90° about its rack's stack axis, so length and width swap (a turned 2 is 1×2×1, a turned 32 is 2×8×2). Only 2, 16, 24 and 32 turn; `true` on a 1, 4 or 8 is an error. Missing means unturned (D71). |
| `offShip` | boolean | Optional, written only as `true`: the crate was delivered before the plan was planned anew for another ship or for racks that changed, so it was never on this ship. It lies in no rack (`rackId` `""`), takes no room and isn't drawn. Needs `delivered`. Ticked back, it is laid into free cells like new cargo, or refused if there is no room. |
| `loaded` | boolean | Optional, written only as `true`: its delivery is marked loaded, on board in the game (K6.4). The planner never moves it (Plan, Re-plan, sync, making room); only the player does, and it settles straight down when the cargo below it goes. Never together with `delivered`, never `offShip`: both are errors. Planned anew for another ship or for racks that changed, it is no longer loaded. |

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

Schema 8 and 9 are read and saved as schema 10 right after loading; moves and ticks stay. An app from before schema 10
refuses a schema 10 file; until v0.4.0 schema 10 may still change without a new number, no release has written it.

- **Schema 9** (v0.2.0 to v0.3.1, before scans) has no `scan` and no `lastScanNo`: every contract is a capture
  (`contractIds`). Either in a schema 9 file is an error.
- **Schema 8** (v0.1.0, before the row axis) has no `fillRows`, `grouping` or `sectionGap` either: every plan is
  lengthwise and ungrouped. Any of them in a schema 8 file is an error, and a schema 9 or 10 file without `fillRows` is
  one too.

Schema 1 to 7 are refused with the error below ("schemaVersion 7 is not supported (expected 8 or 9 or 10)"): no released
version wrote them, v0.1.0 already wrote schema 8.

## A plan for another ship

A plan file whose `shipId` is not the overlay's ship loads anyway: its missions are planned anew for the
overlay's ship, as a ship change in the overlay header does (D59). Missions, capture ids, the watermark, the fill
order and the ticks stay; crate positions, manual moves and `loaded` don't. Each ticked delivery (a legend line) stays ticked
and goes `offShip`: it takes no room on the new ship, so only the cargo still on board is planned. If the missions don't fit as a
whole plan, they go in mission by mission and the rest waits as "doesn't fit" in the legend. An amber line says
"Plan was made for CRUS C2 Hercules, re-planned for MISC Hull A.", and the next save writes the plan for the new ship.
Its crates are not checked against the old ship's racks; the crates against their missions still are.

Without a `ship` in [settings.json](settings-format.md), the overlay takes the plan's ship instead, if it loads.

## Racks that changed

A crate that lies outside its rack in today's ship file means the racks changed since the file was written: a newer
app version with updated ship data (for example, the racks of 21 ships went back to Fleetyards' orientation, D71).
Such a file loads like a plan for another ship: its missions are planned anew, ticks stay (`offShip`), positions
and moves don't, and the amber line says "Plan was made for other racks, re-planned for MISC Freelancer.". This holds
for every schema version.

## Errors

The overlay never overwrites a plan file it can't read. It starts with the empty ship and shows why in
an amber line, for example:

```
Plan not loaded (Plan file 'C:\Users\you\AppData\Local\SC-CargoPlan\plan.json' is invalid: Crate for Riker is in rack 'rack-9', which ship 'hull_a' doesn't have.). The file stays until you make a new plan.
```

A rack the ship doesn't have, a crate of a mission the file doesn't have, or crates that don't match their
missions are such errors. The file stays as it is until a new plan is saved.

## Loading a plan file at start

`CargoPlan.App.exe --plan <file>` shows that plan file instead of the saved one. It becomes the saved
plan on the next save. Meant for development and for trying out the overlay without the editor.
