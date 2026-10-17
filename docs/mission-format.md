# Mission file format

A mission file lists the stops of one hauling run and the cargo for each stop. The command-line tool
reads it, and the mission editor imports and exports the same format.
You can write it by hand in any text editor.

## Example

[`tests/CargoPlan.Core.Tests/Fixtures/example_missions.json`](../tests/CargoPlan.Core.Tests/Fixtures/example_missions.json):

```json
{
  "schemaVersion": 1,
  "stops": [
    {
      "destination": "Riker",
      "items": [
        { "commodity": "Titanium", "scu": 12, "crateSize": 4 }
      ]
    },
    {
      "destination": "Shubin",
      "items": [
        { "commodity": "Quartz", "scu": 10, "crateSize": 2 }
      ]
    },
    {
      "destination": "Orison",
      "items": [
        { "commodity": "Agricium", "scu": 8, "crateSize": 8 }
      ]
    }
  ]
}
```

## Fields

All fields are **required**. Names are camelCase and **case-sensitive**. Unknown fields are an error,
so a typo like `"SCU"` is reported instead of being ignored silently.

### File

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | number | Must be `1`. |
| `stops` | array | At least one stop. **Order matters:** list the stops in the order you fly to them. The planner puts the first stop into Rack 1. |

### Stop

| Field | Type | Rules |
|---|---|---|
| `destination` | string | Name shown in the overlay, e.g. `"Riker"`. Not empty. Each destination appears only once, ignoring case: put all cargo for one place into one stop. |
| `items` | array | At least one item. |

### Item

| Field | Type | Rules |
|---|---|---|
| `commodity` | string | E.g. `"Titanium"`. Not empty. |
| `scu` | number | Total amount in SCU, greater than 0. Must be a multiple of `crateSize`. |
| `crateSize` | number | Crate size in SCU as shown in the contract: `1`, `2`, `4`, `8`, `16`, `24` or `32`. A crate that fits no rack of the ship is rejected by the planner. |

A stop with two crate sizes of the same commodity uses two items:

```json
"items": [
  { "commodity": "Titanium", "scu": 16, "crateSize": 8 },
  { "commodity": "Titanium", "scu": 4, "crateSize": 2 }
]
```

## Errors

A broken file is rejected as a whole, with the file name and the reason, for example:

```
Mission file 'missions.json' is invalid: Stop 'Riker', Titanium: 20 SCU can't be packed in 8 SCU crates. (Parameter 'Scu')
```

Files saved with a UTF-8 byte order mark (Notepad, Windows PowerShell 5.1) are fine.
