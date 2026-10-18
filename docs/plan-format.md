# Plan file format

A plan file holds one hauling run: the missions you entered and the plan made from them, with your
moves and ticks. The overlay saves the current plan to `%LocalAppData%\SC-CargoPlan\plan.json` on every
change, so it survives a restart and a crash. You don't need to write it by hand; this page explains
what is in it.

## Example

[`tests/CargoPlan.Core.Tests/Fixtures/Plans/split_and_shared.json`](../tests/CargoPlan.Core.Tests/Fixtures/Plans/split_and_shared.json),
shortened to one stop:

```json
{
  "schemaVersion": 1,
  "shipId": "hull_a",
  "missions": {
    "schemaVersion": 1,
    "stops": [
      {
        "destination": "Riker",
        "items": [
          { "commodity": "Titanium", "scu": 16, "crateSize": 4 },
          { "commodity": "Quartz", "scu": 4, "crateSize": 2 }
        ]
      }
    ]
  },
  "placements": [
    { "destination": "Riker", "rackId": "rack-0", "item": { "commodity": "Titanium", "scu": 16, "crateSize": 4 }, "delivered": true },
    { "destination": "Riker", "rackId": "rack-1", "item": { "commodity": "Quartz", "scu": 4, "crateSize": 2 }, "delivered": true }
  ],
  "warnings": [
    { "rackId": "rack-0", "message": "Riker spans racks 1+2" },
    { "rackId": "rack-1", "message": "Riker spans racks 1+2" }
  ]
}
```

## Fields

All fields are **required**. Names are camelCase and **case-sensitive**. Unknown fields are an error.

### File

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | number | Must be `1`. |
| `shipId` | string | The ship the plan is for, e.g. `"hull_a"` ([ship files](ship-format.md)). |
| `missions` | object | A complete [mission file](mission-format.md), with its own `schemaVersion`. You can copy it out and use it as a mission file. |
| `placements` | array | Which cargo goes into which rack. |
| `warnings` | array | The planner's hints, word for word as the overlay shows them. Stored, not computed again on loading. |

### Placement

| Field | Type | Rules |
|---|---|---|
| `destination` | string | A destination from `missions`, same spelling. |
| `rackId` | string | A rack `id` of the ship. |
| `item` | object | Commodity, SCU and crate size, as an item in the mission file. A stop's item split over two racks has one placement per rack. |
| `delivered` | boolean | `true` once the stop is ticked off. All placements of a stop are ticked together. |

### Warning

| Field | Type | Rules |
|---|---|---|
| `rackId` | string | A rack `id` of the ship. |
| `message` | string | The hint text. |

The placements must cover the missions exactly: for every destination, commodity and crate size the SCU
of the placements add up to the SCU in the missions. A rack may hold more than its capacity or a crate
that doesn't fit it (after a manual move); the overlay marks such racks red.

## Errors

The overlay never overwrites a plan file it can't read. It starts with the empty ship and shows why in
an amber line, for example:

```
Plan not loaded (Plan file 'C:\Users\you\AppData\Local\SC-CargoPlan\plan.json' is invalid: The plan is for ship 'cutlass_black', not 'hull_a'.). The file stays until you make a new plan.
```

The file stays as it is until a new plan is saved.

## Loading a plan file at start

`CargoPlan.App.exe --plan <file>` shows that plan file instead of the saved one. It becomes the saved
plan on the next save. Meant for development and for trying out the overlay without the editor.
