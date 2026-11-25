# Routes file format

`routes.json` holds what the route planner learns from your scans and the route you fly (phase 13, D118): the
**observations** (for every hauling contract variant you scanned, where it really picked up and delivered, how much it
asked for, and how often you saw it), the **route** started with "Start plan" and its placeholders (T13.6, D125), and
the **runs**, routes finished at their hub (T13.6, D126). The overlay writes it to
`%LocalAppData%\SC-CargoPlan\routes.json`, next to `plan.json`, after every scan it can read and every change of the
route; you don't need to open it. It is a file of its own: `plan.json` stays schema 11 and never learns about routes.

Why: the game's hauling boards are fixed templates (design `docs/design/cargo-routes.md`, §7). For the system-wide
Covalex templates the game data name no destinations, and Red Wind templates name no reward; a scan shows both. The
matrix and the map (T13.2, T13.7) mark what comes from here as "observed" (design §8.3).

## Example

Two variants. A Covalex Experienced Medium contract for 191,500 aUEC from Everus Harbor, scanned once in the offers view
and once after accepting; the game rolled the amounts again, so each commodity's band spans both. And a Red Wind Member
Medium contract for 532,500 aUEC, whose card names the level "Stellar" but not whether it is direct.

```json
{
  "schemaVersion": 3,
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

A route: the Everus ring of `ships-2026.11.2` for a MISC Hull B at Senior, started from the Plan tab's loops; the bulk
was accepted and scanned (its key, as plan.json's `scan.key` holds it, shortened here), the direct and the ways back are
open. Three of its ten placeholders are shown.

```json
{
  "schemaVersion": 3,
  "observations": [],
  "route": {
    "name": "Everus Harbor ring",
    "startedUtc": "2026-11-24T16:35:52.0833014Z",
    "hub": "Everus Harbor",
    "kind": "localRing",
    "system": "stanton",
    "stations": [
      "HUR-L1 Green Glade Station",
      "HUR-L4 Melodic Fields Station",
      "HUR-L3 Thundering Express Station",
      "HUR-L5 High Course Station",
      "HUR-L2 Faithful Dream Station"
    ],
    "placeholders": [
      {
        "id": 1,
        "kind": "bulk",
        "template": "212df642-26d0-4e99-b8c0-c212ae4cfbd8",
        "title": "Senior Rank - Medium Cargo Haul",
        "legs": [
          { "commodity": "Hydrogen Fuel", "min": 41, "max": 72, "container": 16 },
          { "commodity": "Quantum Fuel", "min": 81, "max": 144, "container": 16 },
          { "commodity": "Ship Ammunition", "min": 82, "max": 144, "container": 16 },
          { "commodity": "Hydrogen Fuel", "min": 40, "max": 72, "container": 16 }
        ],
        "destinations": [
          "HUR-L1 Green Glade Station",
          "HUR-L2 Faithful Dream Station",
          "HUR-L3 Thundering Express Station",
          "HUR-L4 Melodic Fields Station"
        ],
        "reward": 325250,
        "filledBy": "EVERUSHARBOR | GREENGLADE:HYDROGENFUEL … | 16 | 325250"
      },
      {
        "id": 2,
        "kind": "direct",
        "template": "f2cd346d-0339-4f42-adb2-407916037c51",
        "title": "Rookie Rank - Direct Small Cargo Haul",
        "legs": [
          { "commodity": "Stims", "min": 8, "max": 16, "container": 4 }
        ],
        "destinations": [
          "HUR-L1 Green Glade Station"
        ],
        "reward": 35500
      },
      {
        "id": 6,
        "kind": "return",
        "template": "2f054a8b-6bc2-46e6-8b4e-8106670dcba3",
        "title": "Junior Rank - Small Cargo Haul",
        "legs": [
          { "commodity": "Waste", "min": 2, "max": 4, "container": 4 },
          { "commodity": "Waste", "min": 2, "max": 4, "container": 4 },
          { "commodity": "Waste", "min": 2, "max": 4, "container": 4 },
          { "commodity": "Waste", "min": 2, "max": 4, "container": 4 }
        ],
        "destinations": [
          "Everus Harbor"
        ],
        "reward": 72500,
        "acceptAt": "HUR-L1 Green Glade Station",
        "collectAt": [
          "HUR-L4 Melodic Fields Station",
          "HUR-L3 Thundering Express Station",
          "HUR-L5 High Course Station"
        ]
      }
    ],
    "extras": [],
    "at": 0
  }
}
```

## Fields

All fields are **required** unless marked optional. Names are camelCase and **case-sensitive**. Unknown fields are an
error.

### File

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | number | `3`; files of schema `2` (T13.3) and `1` (T13.1) are read, [below](#older-schemas). |
| `observations` | array | One entry per variant, in the order first seen. May be empty. No two entries have the same key. |
| `route` | object | Optional: the [route](#route) started with "Start plan"; left out while none is. |
| `runs` | array | Optional: the [runs](#run), routes finished at their hub, oldest first, at most 50; left out while there are none. |

### Observation

A variant is its **key**: `contractor`, `rank`, `size`, `direct`, `reward` and the **commodities** of `scu`. Two scans with
the same key are the same variant, whatever places and amounts each drew; the rest of the entry is what they showed
together. The commodities are in the key since schema 2 (T13.3): templates that differ only in their commodity share
rank, size, direct and reward (at Everus four Rookie Direct Medium pay 50,500, one per ore), and a template's commodities
are fixed. Commodities compare as OCR may read them (upper case, `l`, `1` and `I` alike, `0` and `O` alike); the file
names each one once.

| Field | Type | Rules |
|---|---|---|
| `contractor` | string | `"covalex"` or `"redWind"`, from the form of the title (below). |
| `rank` | string | `"trainee"`, `"rookie"`, `"junior"`, `"member"`, `"experienced"`, `"senior"` or `"master"`: the title's first word. |
| `size` | string | `"xs"` (Extra Small), `"small"`, `"medium"`, `"large"` or `"bulk"`. |
| `direct` | boolean | Optional: `true` if the title says "Direct", `false` if its form would say it and doesn't. Left out where the form doesn't tell: the Red Wind card (below). |
| `reward` | number | The reward in aUEC, at least 1. |
| `area` | string | Optional: `"stellar"` or `"planetary"`, the level a Red Wind card names; the last one read. Not part of the key. |
| `pickups` | array of strings | Every place of the Collect lines of all scans, sorted, one name per place (below). |
| `destinations` | array of strings | Every destination of all scans, sorted, one name per place. |
| `scu` | array | One band per commodity, sorted by commodity: `commodity` (string, as first read), `min` and `max` (numbers, `1 ≤ min ≤ max`), the smallest and largest SCU one delivery line of it asked for. Part of the key. |
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
("Routes: not observed: title unreadable"); a contract of another contractor without a hauling title says "no hauling
template form" (E1 of T13.3a).

**One name per place.** Where the hauling data are there, each place is written as its position names it, in the
system of the contractor (Covalex: Stanton, Red Wind: Pyro): the card writes a pickup "HUR-L3 Thundering Express
Station" and a destination "Thundering Express Station", the file holds "HUR-L3 Thundering Express Station" for both
(E3 of T13.3a; the name rules in `docs/hauling-data-format.md`, Addendum T13.3). A place no position answers to, and
every place without hauling data, is kept as read, with the name rule of the plan (B1: "Green Glade" and "Green Glade
Station", "Melodic Fieids" and "Melodic Fields Station" are one, the longer name stays). Reading a file joins and names
what an older file kept apart (T13.3), in memory; the next save writes it so, never the reading.

## Route

Started with **Start plan** in the Plan tab (recipe or loops, T13.6): a loop's stations in the order flown and one
placeholder per contract of the loop (D125). What is loaded and delivered is the plan's; the route only keeps which scan
filled which placeholder and the contracts accepted beside it. **End plan** removes it, the cargo stays.

| Field | Type | Rules |
|---|---|---|
| `name` | string | The loop's row as the Plan tab showed it ("Everus Harbor ring"). |
| `startedUtc` | string | When it was started, ISO 8601 in UTC. |
| `departedUtc` | string | Optional (6b): when the station bar first left the hub; not before `startedUtc`. |
| `hub` | string | Where it starts and ends. |
| `kind` | string | `"localRing"` (a hub's ring; a Pyro station's too), `"direct"` (one direct contract there and back) or `"systemWide"` (the LEO ring). |
| `system` | string | `"stanton"`, `"pyro"` or `"nyx"`. |
| `stations` | array of strings | At least one: the stations in the order flown, without the hub. |
| `placeholders` | array | One per contract of the loop: out first, then the ways back. Ids once each. |
| `extras` | array of strings | Scan keys of accepted contracts no placeholder took ("not in plan (extra)"); they count for the route. May be empty. |
| `at` | number | Where the station bar stands (6b): `0` the hub at the start, `1` to n the stations, n + 1 the hub at the end. |

### Placeholder

What the recipe's line shows, kept with the route, so a later data release changes no started route.

| Field | Type | Rules |
|---|---|---|
| `id` | number | 1 or more, once in the route. |
| `kind` | string | `"bulk"`, `"direct"`, `"small"` or `"return"` (a way back). |
| `template` | string | The template's uuid in `hauling-templates.json`; a scan fills a placeholder of its template. |
| `title` | string | As the card shows it: Covalex as the data, Red Wind in the card's form (`Member \| Stellar Medium Haul`). |
| `legs` | array | At least one: `commodity`, `min` and `max` (SCU, `1 ≤ min ≤ max`) and `container` (the largest crate in SCU: 1, 2, 4, 8, 16, 24 or 32). |
| `destinations` | array of strings | The places of the recipe's line; the hub for a way back. |
| `reward` | number | Optional: aUEC from the template, else the last a scan saw (Red Wind); left out where none is known, never estimated. |
| `acceptAt` | string | Only for a way back, required there: the station where it is accepted. |
| `collectAt` | array of strings | Only for a way back, required there: the next stations it collects at (several pickups); empty for one pickup. |
| `filledBy` | string | Optional: the scan key (`plan.json`, `scan.key`) of the accepted contract that filled it. |

**Filled is what the plan says.** A placeholder counts as filled while `plan.json` has a mission with its `filledBy` key.
An undo or Discard cargo takes the mission out, and the placeholder is open again; the next accepted scan of its
template fills it anew. Only a scan in the **accepted** view fills; an offer never does. A scan whose template has no
open placeholder is an extra. **Start plan takes the cargo:** every accepted Covalex contract already in `plan.json`
that fits exactly one open placeholder's template fills it, every other accepted contract is an extra (a Red Wind one
too: its card has no reward to tell it by, so its next accepted scan moves it into its placeholder).

## Run

Written by **Finish plan** at the hub (T13.6, 6b, D126).

| Field | Type | Rules |
|---|---|---|
| `name`, `hub`, `kind`, `system` | | As the route's. |
| `startedUtc`, `finishedUtc` | string | ISO 8601 in UTC, in that order. |
| `departedUtc` | string | Optional: between them; the round's time runs from here, else from `startedUtc`. |
| `landings`, `crates`, `longJumps` | number | Landings at least 1, the others at least 0. |
| `reward` | number | aUEC of the filled placeholders and the extras as the plan read them; never an open placeholder's. |
| `filled`, `total`, `extras` | number | Placeholders filled of all, and the extras; `filled ≤ total`. |
| `ship` | string | Optional when read, always written: the overlay's ship at the finish (its id, `hull_b`). |
| `stations` | array of strings | Optional when read, always written: the route's stations as Start plan made them from the loop, without the extras' places. With `ship`, `hub`, `kind` and `system` the loop a run measured (T13.6c); a run without them measured none. |
| `direct` | object | Only for `kind` `"direct"`, optional when read, written where the template is known: the outbound contract's `rank` and `size` (words as in `hauling-templates.json`) and its `reward` (aUEC from the template, left out where the data have none). Two direct runs to the same station are one loop only with the same three (T13.6c); a direct run without it measured none. |

The Plan tab reads the runs for **measured loop times** (T13.6c, D127): a loop the player finished at least three times
with the overlay's ship ranks by the median of its last seven times instead of `routes.times` (`routes.useMeasured` in
settings.json switches it off).

**A broken run costs only itself.** Each run is read alone: one out of these rules (a word, a time, a count, a missing
field, `direct` on a run that is no direct one) is left out, and `cargoplan.log` names it ("Routes: runs[1] left out: …");
the observations, the route and the other runs are read. The next save writes the file without it.

## Older schemas

**Schema 2** (T13.3) has only `observations` and is read as it is; the next save writes schema 3. A `route` or `runs`
in a file of schema 1 or 2 is an error.

### Schema 1

Schema 1 (T13.1) keyed a variant without its commodities, so one entry could hold scans of several templates. Reading
it takes every entry with one commodity as it is and leaves out every entry with several; `cargoplan.log` names each
("Routes: schema 1 entry covalex senior medium 325250 left out: 2 commodities may be several templates"). The next
save writes schema 3. No release wrote schema 1.

## Offers while Routes is selected

The Routes tab is labelled **Plan** under the overlay's header (D120, T13.2). With it selected, a scan in the offers
view doesn't go into the cargo: the loading plan, its undo steps and the scan numbers stay as they were, and
`plan.json` isn't written. The notice line says "Observed (Plan tab): Covalex Junior Small · 50,250 aUEC – cargo
unchanged", or "Not observed (Plan tab): title unreadable – cargo unchanged". An accepted contract goes into the cargo
as always. With **Cargo** selected nothing changes. The selected tab is kept in settings.json (`routes.tab`).

## Unreadable files

A `routes.json` that can't be read (not JSON, another schema, a word or band of an observation or the route out of these
rules, more runs than kept, or locked; a run out of the rules is only left out, above) is left as
it is; the run starts without observations and without a route, and `cargoplan.log` says why. The first save of the
run (an observation or a route) moves the old file aside before it writes, as `plan.json` does (D106):

```
routes.json                                  the new observations
routes.json.unreadable-20261123-181412       the file that couldn't be read, byte for byte
```

If the file can't be moved or written, the observations stay in memory and the next scan tries again. None of this
shows in the notice line: the file is the planner's notebook, and a failure there never costs a scan or the plan.
