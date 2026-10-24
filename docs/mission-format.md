# Mission file format

A mission file lists the stops of one hauling run and the cargo for each stop. The command-line tool
reads it, and the mission editor imports and exports the same format.
You can write it by hand in any text editor.

## Example

[`tests/CargoPlan.Core.Tests/Fixtures/crate_mix.json`](../tests/CargoPlan.Core.Tests/Fixtures/crate_mix.json):

```json
{
  "schemaVersion": 2,
  "stops": [
    {
      "destination": "Green Glade",
      "items": [
        { "commodity": "Stims", "scu": 6, "maxCrateSize": 4 },
        { "commodity": "Scrap", "scu": 15, "maxCrateSize": 4 }
      ]
    },
    {
      "destination": "Riker",
      "items": [
        { "commodity": "Titanium", "scu": 6, "maxCrateSize": 4, "crates": [2, 2, 2] }
      ]
    }
  ]
}
```

An item holds what the contract says: the SCU and the largest crate size it allows ("4 SCU or smaller").
The tool works out the crates the way the freight elevator delivers them: the largest allowed crate as
often as it fits, the rest in smaller ones. 6 SCU at max 4 are a 4 and a 2; 15 SCU at max 4 are
4 + 4 + 4 + 2 + 1. If the kiosk delivers something else, list the crates in `crates`.

## Fields

Names are camelCase and **case-sensitive**. Unknown fields are an error, so a typo like `"SCU"` is
reported instead of being ignored silently. All fields are **required** except `crates` and `pickup`.

### File

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | number | `2`. Files with `1` are still read (see below); export always writes `2`. |
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
| `scu` | number | Total amount in SCU, greater than 0. |
| `maxCrateSize` | number | The largest crate size the contract allows, in SCU: `1`, `2`, `4`, `8`, `16`, `24` or `32`. |
| `crates` | array of numbers | Optional. The crates as the kiosk delivered them, e.g. `[2, 2, 2]`. Each one a crate size up to `maxCrateSize`, together exactly `scu`. Leave it out to let the tool work them out. |
| `pickup` | array of strings | Optional. Where the crates are collected, e.g. `["Lorville"]`, or several places for a contract with several pickups, without amounts. Spaces around a name, empty names and a name repeated (in any case) are dropped. The overlay shows it when you select a crate and groups the legend by it. |

A crate that fits no rack of the ship is rejected by the planner.

## Schema 1 (still read)

Files written before version 2 have one crate size per item and no maximum:

```json
{
  "schemaVersion": 1,
  "stops": [
    {
      "destination": "Riker",
      "items": [
        { "commodity": "Titanium", "scu": 12, "crateSize": 4 }
      ]
    }
  ]
}
```

`scu` must be a multiple of `crateSize`. Such an item is read as exactly those crates: `maxCrateSize` is
the `crateSize`, and `crates` lists them (`[4, 4, 4]`), so the editor shows them as entered by hand.
A stop with two crate sizes of the same commodity used two items. Version 2 doesn't know `crateSize`:
change `schemaVersion` only together with the items.

## Errors

A broken file is rejected as a whole, with the file name and the reason, for example:

```
Mission file 'missions.json' is invalid: Stop 'Riker', Titanium: The crates add up to 4 SCU, not 6. (Parameter 'Crates')
```

Files saved with a UTF-8 byte order mark (Notepad, Windows PowerShell 5.1) are fine.
