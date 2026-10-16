# Ship file format

Ships are data, not code. Each ship is one JSON file in
[`src/CargoPlan.Core/Ships/`](../src/CargoPlan.Core/Ships/). Every `*.json` in that folder is compiled
into `CargoPlan.Core.dll` as an embedded resource named `ships/<file name>`, so adding a ship means
adding one file. No C# and no `.csproj` change needed.

## Example

`src/CargoPlan.Core/Ships/hull_a.json`:

```json
{
  "schemaVersion": 1,
  "id": "hull_a",
  "name": "MISC Hull A",
  "totalScu": 64,
  "racks": [
    { "id": "rack-0", "capacityScu": 16, "slots": [] },
    { "id": "rack-1", "capacityScu": 16, "slots": [] },
    { "id": "rack-2", "capacityScu": 16, "slots": [] },
    { "id": "rack-3", "capacityScu": 16, "slots": [] }
  ]
}
```

A rack with a known slot grid:

```json
{
  "id": "rack-0",
  "capacityScu": 16,
  "slots": [
    { "id": "a1", "allowedSizes": [1, 2, 4] },
    { "id": "a2", "allowedSizes": [8] }
  ]
}
```

## Fields

All fields are **required**. Names are camelCase and **case-sensitive**. Unknown fields are an error,
so a typo like `capacitySCU` fails the build instead of being ignored silently.

### Ship

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | number | Must be `1`. Bumped only when the format changes incompatibly. |
| `id` | string | Must equal the file name without `.json` (`hull_a.json` → `"hull_a"`). Use lowercase `snake_case`: Linux file systems are case-sensitive. |
| `name` | string | Display name, e.g. `"MISC Hull A"`. |
| `totalScu` | number | Cargo capacity of the ship. Must equal the sum of all `capacityScu`. |
| `racks` | array | At least one rack. **Order matters:** the first rack is the front of the ship (index 0), the last one the back. The planner puts the first stop in front. |

### Rack

A rack is a separately loadable section of the cargo hold.

| Field | Type | Rules |
|---|---|---|
| `id` | string | Unique within the ship. Convention: `rack-0`, `rack-1`, … front to back. |
| `capacityScu` | number | Greater than 0. |
| `slots` | array | May be empty (`[]`): the rack is then planned by capacity only. Use this when the slot grid is unknown. |

### Slot

A slot is a single crate position inside a rack.

| Field | Type | Rules |
|---|---|---|
| `id` | string | Unique within the rack. |
| `allowedSizes` | array of numbers | Crate sizes in SCU that fit here. Allowed values: `1`, `2`, `4`, `8`. At least one. |

## Checklist for a new ship PR

- [ ] File is `src/CargoPlan.Core/Ships/<id>.json`, lowercase `snake_case`, and `id` inside matches.
- [ ] `schemaVersion` is `1`.
- [ ] Rack capacities add up to `totalScu`.
- [ ] Racks are listed **front to back**.
- [ ] Values verified in game (current patch named in the PR description). Unknown slot grid → `"slots": []`.
- [ ] `dotnet test` passes. The test `Every_shipped_ship_loads` loads every ship file and reports the
      exact problem (file name, field, value) if something is wrong.
