# Routes file format

`routes.json` holds what the route planner learns from your scans (phase 13, D118). Today that is the
**observations**: for every hauling contract variant you scanned, where it really picked up and delivered, how much it
asked for, and how often you saw it. The overlay writes it to `%LocalAppData%\SC-CargoPlan\routes.json`, next to
`plan.json`, after every scan it can read; you don't need to open it. It is a file of its own: `plan.json` stays
schema 11 and never learns about routes.

Why: the game's hauling boards are fixed templates (design `docs/design/cargo-routes.md`, §7). For the system-wide
Covalex templates the game data name no destinations, and Red Wind templates name no reward; a scan shows both. The
matrix and the map (T13.2, T13.7) mark what comes from here as "observed" (design §8.3).

## Example

Two variants. A Covalex Experienced Medium contract for 191,500 aUEC from Everus Harbor, scanned once in the offers view
and once after accepting; the game rolled the amounts again, so each commodity's band spans both. And a Red Wind Member
Medium contract for 532,500 aUEC, whose card names the level "Stellar" but not whether it is direct.

```json
{
  "schemaVersion": 1,
  "observations": [
    {
      "contractor": "covalex",
      "rank": "experienced",
      "size": "medium",
      "reward": 191500,
      "pickups": [
        "Everus Harbor"
      ],
      "destinations": [
        "Faithful Dream Station",
        "Green Glade Station"
      ],
      "scu": [
        {
          "commodity": "Hydrogen Fuel",
          "min": 55,
          "max": 70
        },
        {
          "commodity": "Quantum Fuel",
          "min": 81,
          "max": 136
        },
        {
          "commodity": "Ship Ammunition",
          "min": 115,
          "max": 133
        }
      ],
      "count": 2,
      "firstUtc": "2026-11-23T18:04:12Z",
      "lastUtc": "2026-11-23T18:07:12Z",
      "direct": false
    },
    {
      "contractor": "redWind",
      "rank": "member",
      "size": "medium",
      "reward": 532500,
      "pickups": [
        "Rat's Nest",
        "Starlight Service Station"
      ],
      "destinations": [
        "Ruin Station"
      ],
      "scu": [
        {
          "commodity": "Carbon",
          "min": 200,
          "max": 200
        }
      ],
      "count": 1,
      "firstUtc": "2026-11-23T18:13:12Z",
      "lastUtc": "2026-11-23T18:13:12Z",
      "area": "stellar"
    }
  ]
}
```

## Fields

All fields are **required** unless marked optional. Names are camelCase and **case-sensitive**. Unknown fields are an
error.

### File

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | number | `1`. |
| `observations` | array | One entry per variant, in the order first seen. May be empty. No two entries have the same key. |

### Observation

A variant is its **key**: `contractor`, `rank`, `size`, `direct` and `reward`. Two scans with the same key are the same
variant, whatever places and amounts each drew; the rest of the entry is what they showed together.

| Field | Type | Rules |
|---|---|---|
| `contractor` | string | `"covalex"` or `"redWind"`, from the form of the title (below). |
| `rank` | string | `"trainee"`, `"rookie"`, `"junior"`, `"member"`, `"experienced"`, `"senior"` or `"master"`: the title's first word. |
| `size` | string | `"xs"` (Extra Small), `"small"`, `"medium"`, `"large"` or `"bulk"`. |
| `direct` | boolean | Optional: `true` if the title says "Direct", `false` if its form would say it and doesn't. Left out where the form doesn't tell: the Red Wind card (below). |
| `reward` | number | The reward in aUEC, at least 1. |
| `area` | string | Optional: `"stellar"` or `"planetary"`, the level a Red Wind card names; the last one read. Not part of the key. |
| `pickups` | array of strings | Every place of the Collect lines of all scans, sorted, each once. Names as the scan read them. |
| `destinations` | array of strings | Every destination of all scans, sorted, each once. |
| `scu` | array | One band per commodity, sorted by commodity: `commodity` (string), `min` and `max` (numbers, `1 ≤ min ≤ max`), the smallest and largest SCU one delivery line of it asked for. |
| `count` | number | Scans recorded, at least 1. Scanning the same contract again counts too. |
| `firstUtc`, `lastUtc` | string | The first and the last scan, ISO 8601 in UTC (`Z`), first not after last. |

The words are those of the hauling data (`docs/hauling-data-format.md`), so a template and an observation compare
without translating.

## Which scans are observed

Every scan the overlay reads, whatever tab is selected, if all of this holds:

- the contract is in the **offers** or the **accepted** view (the view isn't told apart: no observation);
- its objectives were read whole (not "Contract text still loading") and name at least one delivery;
- its **title** and its **reward** were read.

Titles come in three forms (T13.1):

| Contractor | Title | Direct |
|---|---|---|
| Covalex | `Senior Rank - Direct Medium Cargo Haul` | "Direct" there or not |
| Red Wind, as the card shows it | `Member \| Stellar Medium Haul \| to Ruin Station` | `true` with "Direct", else unknown |
| Red Wind, as the game data name it | `Senior Hauler Needed for Large Shipment` | "Direct" there or not |

No capture has shown the third form yet; the parser reads it should the game ever print it. A title read in part
(an unknown rank or size word) is no title, and the scan isn't observed. `cargoplan.log` names why
("Routes: not observed: title unreadable").

Names are kept as read. Two readings of one place ("Green Glade Station", "Green Glade") can both be there until
T13.3 joins them by the name rule of the plan (B1) when it loads the file; that needs no new schema.

## Offers while Routes is selected

With **Routes** selected, a scan in the offers view doesn't go into the plan: the plan, its undo steps and the scan
numbers stay as they were, and `plan.json` isn't written. The notice line says "Observed (Routes): Covalex Junior
Small · 50,250 aUEC – plan unchanged", or "Not observed (Routes): title unreadable – plan unchanged". An accepted
contract goes into the plan as always. With **Cargo** selected nothing changes. Until the tab strip comes (T13.2), the
start switch `--routes-open` selects Routes, for testing.

## Unreadable files

A `routes.json` that can't be read (not JSON, another schema, a word or band out of these rules, or locked) is left as
it is; the run starts without observations and `cargoplan.log` says why. The first observation of the run moves the
old file aside before it writes, as `plan.json` does (D106):

```
routes.json                                  the new observations
routes.json.unreadable-20261123-181412       the file that couldn't be read, byte for byte
```

If the file can't be moved or written, the observations stay in memory and the next scan tries again. None of this
shows in the notice line: the file is the planner's notebook, and a failure there never costs a scan or the plan.

## Later

The route and its placeholders (T13.6) come into this file, not into `plan.json` (D118). Until then a file with any
other field is refused.
