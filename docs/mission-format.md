# Mission file format

A mission file lists the stops of one hauling run and the cargo for each stop. The command-line tool
reads it, and the mission editor imports and exports the same format.
You can write it by hand in any text editor.

## Example

Based on [`tests/CargoPlan.Core.Tests/Fixtures/crate_mix.json`](../tests/CargoPlan.Core.Tests/Fixtures/crate_mix.json):

```json
{
  "schemaVersion": 4,
  "fillOrder": "sternFirst",
  "fillRows": "crosswise",
  "grouping": "byDestination",
  "sectionGap": 1,
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
reported instead of being ignored silently. All fields are **required** except `fillOrder`, `fillRows`, `grouping`, `sectionGap`, `crates` and `pickup`.

### File

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | number | `4`. Files with `3` are still read (see below); export always writes `4`. |
| `fillOrder` | string | Optional. Which end of each rack the crates start from: `"bowFirst"` or `"sternFirst"`, as in the [plan file](plan-format.md). Importing a file with it sets the editor's **Fill order**; without it the editor keeps its own. Export always writes it. |
| `fillRows` | string | Optional. Which way the rows of a layout run: `"lengthwise"` or `"crosswise"`, as in the [plan file](plan-format.md). Importing a file with it sets the editor's **Rows**; without it the editor keeps its own. Export always writes it. |
| `grouping` | string | Optional. `"none"` or `"byDestination"`: whether each destination is kept as a block of its own in a rack it shares, as in the [plan file](plan-format.md). Importing a file with it sets the editor's **Grouping**; without it the editor keeps its own. Export always writes it. |
| `sectionGap` | number | Optional. `0`, `1` or `2`: empty rows between blocks, as in the [plan file](plan-format.md). Importing a file with it sets the editor's **Section gap**; without it the editor keeps its own. Export always writes it. |
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

`manual` (an item added by hand to a ContractTracker mission) belongs to [plan files](plan-format.md) only: a mission
file says nothing about ContractTracker, so an item with `manual` is an error, and export never writes it.

A crate that fits no rack of the ship is rejected by the planner.

## Schema 3 (still read)

Files written before version 4 are the same without `fillRows`, `grouping` and `sectionGap`; importing one keeps the
editor's. Any of them in a version 3 file is an error: change `schemaVersion` to `4` together with it.

## Schema 1 and 2 (refused)

Versions 1 and 2 were written only before v0.1.0, which wrote 3. Since v0.4.0 a file with either is an error
("schemaVersion 1 is not supported, expected 3 or 4"). In version 1 an item had one `crateSize` instead of
`maxCrateSize`: such an item is `"maxCrateSize": N, "crates": [N, N, …]` (SCU / N crates of N), then
`schemaVersion` `4`. A version 2 file only needs `schemaVersion` `4`.

## Errors

A broken file is rejected as a whole, with the file name and the reason, for example:

```
Mission file 'missions.json' is invalid: Stop 'Riker', Titanium: The crates add up to 4 SCU, not 6. (Parameter 'Crates')
```

Files saved with a UTF-8 byte order mark (Notepad, Windows PowerShell 5.1) are fine.
