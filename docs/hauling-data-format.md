# Hauling data format (templates and positions)

**Status: contract of Phase 13, written with T13.0 (21 Nov 2026), amended with T13.0a (22 Nov 2026) from the findings
of H1 ([Addendum T13.0a](#addendum-t130a)); still schema 1, no release carries it yet.** The data pipeline in
[sc-cargoplan-shipdata](https://github.com/MRiediger/sc-cargoplan-shipdata) writes both files with its hauling job
(H1, SD14 there); the app reads them from T13.3 on. Why the data come this way: [D119](decisions.md); what the route planner
does with them: [D118](decisions.md) and the design, [`design/cargo-routes.md`](design/cargo-routes.md) (§7, §9.3).
Where the design differs from this page (it names `hub-profiles.json` and a `distances.json` matrix), this page holds.

The route planner needs two things the game knows and the contracts don't show: every hauling contract the boards can
offer (the **templates**) and where the places lie (the **positions**). Both are extracted from the game files
(`Data.p4k`) by [octfx/ScDataDumper](https://github.com/octfx/ScDataDumper) and read from **one commit** of
[StarCitizenWiki/scunpacked-data](https://github.com/StarCitizenWiki/scunpacked-data): the templates from
`contracts/<uuid>.json`, the positions from `starmap_positions.json` (V1). The same extraction is published by
`api.star-citizen.wiki` (`/api/missions`); the pipeline doesn't call it, but follows its normalization rules (for
example a leg without an SCU band, below). Both are CIG material, not under this repository's MIT licence; the data
release's `NOTICE.md` says so and credits the sources ([Credits](#credits)). The app makes no request to any source; it
reads the files the data release carries (D119).

## Files in the data release

| Asset | Content |
|---|---|
| `hauling-templates.json` | every released hauling template of Covalex and Red Wind Linehaul, normalized ([Templates](#hauling-templatesjson)) |
| `positions.json` | every place a template names, plus the stations and planets of the systems, in metres ([Positions](#positionsjson)) |

Both are flat assets of the data release `ships-YYYY.MM[.N]`, next to `ships.zip` and the holograms. They are **not**
in `ships.zip` and **not** in the manifest's `files` or `assets`: the manifest reader of v0.6.0 and v0.7.0 accepts in
`files` only `outlines.json` and `ships/**` and would turn down the whole release. The manifest describes them in a
section of their own, which older apps skip as an unknown field.

### Manifest section `hauling`

```json
"hauling": {
 "schemaVersion": 1,
 "templates": {"file": "hauling-templates.json", "bytes": 1482113, "sha256": "<sha256>"},
 "positions": {"file": "positions.json", "bytes": 41877, "sha256": "<sha256>"}
}
```

| Field | Meaning |
|---|---|
| `hauling` | Optional. Missing or `null`: this release has no hauling data. |
| `hauling.schemaVersion` | The schema of **both** files, today `1`; equal to the `schemaVersion` inside each file. |
| `hauling.templates`, `hauling.positions` | Asset name (flat), length and SHA-256 of each file, as for holograms (`holo.ships.<slug>`). The URL is `…/releases/download/<dataVersion>/<file>`. |

**The hauling data never cost the ship data** (K2 of the T13.0 approval):

| Case | App |
|---|---|
| `hauling` missing or `null` | Routes tab without templates (matrix and ranking from observations only, D118 addendum T13.1); ships load; **no notice**. |
| `hauling.schemaVersion` unknown, or a file fails its length, hash or a [loader check](#checks-of-the-loader) | Like a hologram format the app doesn't read (`HoloOffer.UnknownFormat`, D114): one log line, a hint in the Routes tab; ships load. |
| Both files read | Templates and positions for the run; a data change counts from the next start (D111 addendum). |

**Rule for the pipeline (SD14):** a hauling job that fails (source down, a place without a position, a gap in the alias
table) writes `hauling: null` or carries the previous release's two files forward unchanged; it **never** stops the ship
data release. "Stop" below means the hauling job stops, not the run.

Where the app keeps them: next to `manifest.json` in the data folder, the baseline's `ship-data\` and a downloaded
`ships\<tag>\` alike; the download and the baseline fetch come with T13.3.

## hauling-templates.json

One entry per template variant. A template is fixed game data: on a board it is either offered or not, and an offer is
one instance of it with SCU and places drawn from its bands and pools (design §8.3). The file says nothing about
availability (design §0, invariant 4).

### Example

Values illustrative: the first entry has the shape of the Experienced Direct Medium seen at Everus on 19 and 21 Nov
(design §8.3), its pool cut down to the places of the positions example below; the second is a Red Wind template, which
the game data carry without a reward (design §7.6).

```json
{
 "schemaVersion": 1,
 "dataVersion": "ships-2026.12",
 "source": {"repository": "StarCitizenWiki/scunpacked-data", "commit": "<sha>", "gameBuild": "4.10.3-LIVE.13165946",
            "fetchedAt": "2026-12-01T04:10:00Z"},
 "templates": [
  {
   "uuid": "0b5c1f2e-7d1a-4c55-9a0e-3f0f6c2b8e11",
   "debugName": "HaulCargo_AToB_Supply_PressurizedIce_Stanton1_SupplyGrade2",
   "contractor": "covalex",
   "title": "Experienced Rank - Direct Medium Cargo Haul",
   "rank": "experienced",
   "size": "medium",
   "direct": true,
   "shape": "aToB",
   "stops": 1,
   "system": "stanton",
   "areaKey": "Stanton1",
   "missionType": "stellar",
   "faction": null,
   "reward": 160250,
   "legs": [{"commodity": "Pressurized Ice", "minScu": 90, "maxScu": 144, "containerScu": 16}],
   "pickupPool": ["Everus Harbor"],
   "dropoffPool": ["HUR-L1 Green Glade Station", "HUR-L3 Thundering Express Station"]
  },
  {
   "uuid": "6e1d0c9a-2b44-4f7e-8d3a-91c2a4f05b7d",
   "debugName": "RedWind_Pyro_LargeGrade1_Solar_CFP_StationToStation_Carbon_CargoHauling_AToB",
   "contractor": "redWind",
   "title": "Senior Hauler Needed for Large Shipment",
   "rank": "senior",
   "size": "large",
   "direct": false,
   "shape": "aToB",
   "stops": 1,
   "system": "pyro",
   "areaKey": "Solar",
   "missionType": "stellar",
   "faction": "cfp",
   "reward": null,
   "legs": [{"commodity": "Carbon", "minScu": 400, "maxScu": 600, "containerScu": 32}],
   "pickupPool": ["Checkmate"],
   "dropoffPool": ["Ruin Station"]
  }
 ]
}
```

### Schema 1

| Field | Type | Required | Meaning | From (field of `contracts/<uuid>.json` or name part) |
|---|---|---|---|---|
| `schemaVersion` | int | yes | `1` | – |
| `dataVersion` | string | yes | Tag of the data release that carries the file. | the run |
| `source.repository`, `source.commit`, `source.gameBuild`, `source.fetchedAt` | string | yes | The repository and commit the run read, the game build of that commit, and when (V1). | the run; `gameBuild` from the commit message |
| `templates[]` | array | yes | Sorted by `uuid`, so the release diff is line by line. | |
| `uuid` | string | yes | Identity of the template, stable across runs; the diff compares by it. | `UUID` |
| `debugName` | string | yes | The game's name, kept for tracing. | `DebugName` |
| `contractor` | `covalex` \| `redWind` | yes | Who offers it (V4). | `MissionGiver` `Covalex Independent Contractors` \| `Red Wind Linehaul` |
| `title` | string | yes | The title as the contract shows it; the scan matches on it (T13.3). | `DisplayTitle` (`Title` when empty) |
| `rank` | `trainee` \| `rookie` \| `junior` \| `member` \| `experienced` \| `senior` \| `master` | yes | Rank needed; the order is the list's (V2: `trainee` first). A board shows every rank up to the player's (design §1.2). | first word of the title |
| `size` | `xs` \| `small` \| `medium` \| `large` \| `bulk` | yes | Size class (V3): `Extra Small` → `xs`, `Small`, `Medium`, `Large`, `Bulk` → `bulk`, in this order. An unknown word stops the hauling job. | title |
| `direct` | bool | yes | `Direct` in the title. | title |
| `shape` | `aToB` \| `singleToMulti` \| `multiToSingle` | yes | One pickup and one drop-off, one pickup and n drop-offs, n pickups and one drop-off. | form part `AToB`, `SingleToMulti<n>`, `Multi<n>ToSingle` |
| `stops` | int ≥ 1 | yes | n of the form; `1` for `aToB`. | form part |
| `system` | `stanton` \| `pyro` \| `null` | yes | Board's system; `null` for `Interstellar`. | geo / area part |
| `areaKey` | string | yes | Covalex: `Stanton` (system-wide), `Stanton1` Hurston, `Stanton2` Crusader, `Stanton3` ArcCorp, `Stanton4` microTech, `CrossStanton`, `Interstellar`. Red Wind: `Solar`, `RegionA`…`RegionD`, `Planetary`. | geo part (`HaulCargo_`), area part (`RedWind_Pyro_`) |
| `missionType` | `local` \| `planetary` \| `stellar` \| `interstellar` | yes | Area of operations (design §1.1, §7.2). All four are in the file; which the planner uses is the app's (Phase 13: `stellar`). | `MissionType.Name` `Hauling - <Type>` |
| `faction` | `hh` \| `cfp` \| `null` | yes | Red Wind's side, Headhunters or Citizens for Prosperity; `null` for Covalex. | faction part (`RedWind_Pyro_`) |
| `reward` | int \| `null` | yes | Fixed reward of the variant in aUEC; `null` where the game data have none (all of Red Wind stellar). Never estimated. | `FixedReward.Amount` |
| `legs[]` | array, ≥ 1 | yes | One per hauling order, in the source's order. | `HaulingOrders[]` |
| `legs[].commodity` | string | yes | Commodity as the contract names it. | order's `Name` |
| `legs[].minScu`, `legs[].maxScu` | int | yes | SCU band an instance is drawn from. | `MinScu`, `MaxScu` |
| `legs[].containerScu` | int | yes | Largest crate, one of 1, 2, 4, 8, 16, 24, 32. | `MaxContainerSize` |
| `pickupPool[]`, `dropoffPool[]` | string array | yes | Places an instance draws from, each the resolved **starmap name**: exactly the `name` of one entry of `positions.json` in the template's system (K1). Contract forms the alias table maps go into that position's `contractNames`, names it drops leave the pool (V6). May be `[]` where the source's pool is empty or held only dropped names (design §5); the pipeline fills nothing in, the app shows such edges from scans (design §8.3). | `MissionTokens` `Location<n>\|Address`, `Destination<n>\|Address` |

### Filter

The file holds templates that can be offered, and nothing else:

- Released only (`NotForRelease` false); anything else is a template the game never offers.
- Geo part `TEMPLATE` goes: an abstract parent, not in the game.
- Only the two naming schemes `HaulCargo_*` and `RedWind_Pyro_*` (design §7.1, §7.6), and of those only the two
  contractors of `MissionGiver` (V4). Other givers (Dead Saints, Ling Family: interstellar, no board, design §7.6) go,
  even under one of the two prefixes.
- **Nothing is estimated (V8):** a template goes, with its reason counted in the changelog, when a leg can't be
  expressed (no SCU band: raw `MaxScu = 0`, the API's `min_scu` null; or `MaxContainerSize` −1, or any size outside the
  set above) or its form has no stop count. A title not in its contractor's form also goes with a counted reason.

### Normalizing the two naming schemes

| Field | `HaulCargo_<Form>_<Category>_<Commodity>_<Geo>_<Grade>` | `RedWind_Pyro_<Grade>_<Area>_<Faction>_<RouteType>_<Commodity>_CargoHauling_<Form>` |
|---|---|---|
| `contractor` | from `MissionGiver`, not the prefix (V4) | from `MissionGiver`, not the prefix (V4) |
| `shape`, `stops` | `<Form>` | `<Form>` |
| `system` | `Stanton…`, `CrossStanton` → `stanton`; `Interstellar` → `null` | `pyro` |
| `areaKey` | `<Geo>` as written | `<Area>` as written |
| `faction` | `null` | `<Faction>` lower case |
| `missionType` | `MissionType.Name` | `MissionType.Name` |
| `title`, `rank`, `size`, `direct` | title form `<Rank> Rank - [Direct] <Size> Cargo Haul` | title form `<Rank> Hauler Needed for [Direct] <Size> Shipment` |

`<Category>`, `<Grade>` and `<RouteType>` stay in `debugName` only; the fields above carry what the planner needs. The
title forms are the ones `ContractTextParser` learns with T13.1 (rank words including `Trainee`, size words including
`Extra Small` and `Bulk`), so a scan and a template speak the same words.

## positions.json

### Example

Coordinates illustrative, chosen so the distances are the ones the design measured from `starmap_positions.json`
(§9.3): Everus Harbor → HUR-L1 1.3 Gm, → HUR-L3 25.7 Gm. The two Pyro places complete the templates example; their
`contractNames` are the Pyro long forms of the alias table (K1).

```json
{
 "schemaVersion": 1,
 "dataVersion": "ships-2026.12",
 "source": {"repository": "StarCitizenWiki/scunpacked-data", "file": "starmap_positions.json", "commit": "<sha>",
            "fetchedAt": "2026-12-01T04:22:00Z"},
 "positions": [
  {"uuid": "07463f54…", "name": "Checkmate", "system": "pyro", "type": "Manmade", "parent": null,
   "x": -41255000000, "y": 18730000000, "z": 0, "contractNames": ["Checkmate at the L4 Lagrange of Pyro II"]},
  {"uuid": "9a41…", "name": "Everus Harbor", "system": "stanton", "type": "Manmade", "parent": null,
   "x": 12850400000, "y": 0, "z": 0, "contractNames": []},
  {"uuid": "c3d0…", "name": "HUR-L1 Green Glade Station", "system": "stanton", "type": "Manmade_VisibleOnInteraction",
   "parent": null, "x": 11550400000, "y": 0, "z": 0, "contractNames": []},
  {"uuid": "51be…", "name": "HUR-L3 Thundering Express Station", "system": "stanton",
   "type": "Manmade_VisibleOnInteraction", "parent": null, "x": -12849600000, "y": 0, "z": 0, "contractNames": []},
  {"uuid": "1c585a2e…", "name": "Ruin Station", "system": "pyro", "type": "Manmade", "parent": null,
   "x": -27600000000, "y": 9900000000, "z": 0, "contractNames": ["Ruin Station above Pyro VI"]}
 ]
}
```

### Schema 1

| Field | Type | Required | Meaning |
|---|---|---|---|
| `schemaVersion` | int | yes | `1` |
| `dataVersion` | string | yes | Tag of the data release that carries the file. |
| `source.repository`, `source.file`, `source.commit`, `source.fetchedAt` | string | yes | The raw file the run read, at which commit. |
| `positions[]` | array | yes | Sorted by `system`, then `name`. |
| `uuid` | string | yes | The starmap entity's `uuid`, for tracing and the diff ("moved", "renamed"). One position per `uuid`: several contract names on one `uuid` make one position (K1). |
| `name` | string | yes | The place's **starmap name** (K1). Unique **per system**, not per file: the gateways carry the same name in both systems (K3). Pools name places by it. |
| `contractNames[]` | string array | no | Other forms contracts use for this place, from the pipeline's alias table (K1), for example a Pyro long form. Missing or `[]`: contracts use `name` only. |
| `system` | `stanton` \| `pyro` \| `nyx` | yes | Coordinates are per system; a distance across systems is not defined. |
| `type` | string | yes | The starmap's `type` as written (`Manmade`, `Manmade_VisibleOnInteraction`, `Planet`, …). |
| `parent` | string \| `null` | yes | `name` of the parent position when it is in the file (a station's planet), else `null`. For the schematic map (planet = hub + L ring, T13.7). |
| `x`, `y`, `z` | number | yes | Position in metres, system coordinates. |

**Content:** every place any `pickupPool` or `dropoffPool` names, plus the stations (`Manmade`,
`Manmade_VisibleOnInteraction`), planets and the star (`Star`, the system's centre for the map) of Stanton, Pyro and
Nyx. Starmap entries whose `uuid` is given to more than one entry (asteroids, points of interest) are left out: one
position per `uuid` would pull in all of them. Planets in Star Citizen do not move, so the positions are static between
game builds.

**Distances are computed, not stored:** the distance of two places of one system is the Euclidean distance of their
positions; the app shows it in Gm (10⁹ m). A matrix in the file would be redundant and grow with the square of the places.

## Names: two matches, each in one place

- **Template ↔ position: exact string.** Every pool name equals one `name` in `positions.json` of the same release and
  of the template's system, byte for byte. The pipeline ensures it; the app's loader checks it. The pipeline resolves
  each contract name in this order: its **alias table** maps the name to a starmap `uuid` (the pool then carries the
  starmap name, the contract form goes into `contractNames`, K1) or **drops** it with a reason (V6: placeholder texts,
  caves, wrecks, generic names; no position exists, the name leaves the pool, the changelog counts the drops); else the
  exact starmap name in the template's system. Anything else is a gap and stops the hauling job.
- **Scan ↔ position: the B1 key, in the app only.** A scanned place is matched by `DestinationNames.Key` (OCR-tolerant,
  suffix "Station" optional), which moves to Core with T13.3, against `name` **and** every `contractNames` entry of the
  scan's system (K1). The files carry no key: a second implementation in the pipeline would drift. The app's data test
  (T13.3) checks that no key occurs twice in one system, across both fields.

## Checks of the loader

At this boundary the app checks (CLAUDE.md A6); a failed check means the case "fails a loader check" above:

- both `schemaVersion` values equal `hauling.schemaVersion` and are known;
- `name` unique per system in `positions.json` (K3); every pool name found there as a `name` of the template's system
  (for a template without a system: of any system; the pipeline resolves such a name only where it is unique across
  the three systems);
- per leg `1 ≤ minScu ≤ maxScu` and `containerScu` ∈ {1, 2, 4, 8, 16, 24, 32};
- `legs` has at least one entry (K2). How many legs a form has (`min(stops × commodities, 4)` in the first run) is a
  hint in the pipeline's changelog, not a check: the app must not reject a template the game offers.

Unknown fields are ignored. A template with an unknown enum value (`contractor`, `rank`, `size`, `shape`,
`missionType`, `faction`) is skipped and logged, the others load: a new rank or size in a game build then costs only
its templates.

## Release diff

The hauling job writes into the release's changelog what changed against the previous release: templates new and gone
(by `uuid`), reward changes, places new, gone, renamed or moved (by `uuid`; the threshold for "moved" is SD14's).

## Credits

The data release's `NOTICE.md` names, for both files: data from the Star Citizen game files (CIG), extracted with
octfx/ScDataDumper; templates and positions via StarCitizenWiki/scunpacked-data; the normalization rules of
**api.star-citizen.wiki** (V1: the credit stays although the pipeline reads the raw repository). The README and the wiki
page "Routes" repeat the credit (T13.8).

## Addendum T13.0a

Findings of H1 in sc-cargoplan-shipdata (SD14 there), approved 21 Nov 2026 and worked into the text above on 22 Nov.
Nothing had read or released schema 1 yet, so it stays schema 1.

| # | Change |
|---|---|
| V1 | Source is one commit of StarCitizenWiki/scunpacked-data (`contracts/` and `starmap_positions.json`), not the wiki API; templates head `source {repository, commit, gameBuild, fetchedAt}` instead of `source.api`; the credit for api.star-citizen.wiki stays. |
| V2 | `rank` gains `trainee`, first in the order (259 Covalex and 40 Red Wind templates in the source). |
| V3 | `size`: `Extra Small` → `xs`; new value `bulk` after `large` (title word `Bulk`). |
| V4 | `contractor` from `MissionGiver`, not from the name prefix; other givers (Dead Saints, Ling Family) go. |
| V6 | The alias table knows `drop` with a reason: placeholders, caves, wrecks leave the pool; their count is in the changelog. |
| V8 | A template with a leg that can't be expressed (no SCU band, container size −1) or a form without a stop count goes with a counted reason; nothing is estimated. |
| K1 | `positions[].name` is the starmap name; new optional `contractNames[]`; pools carry the starmap name; one position per `uuid`. The loader checks pool names against `name`, the app's B1 match runs against `name` and `contractNames`. |
| K2 | Loader rule for `legs`: only `len(legs) ≥ 1`; the count formula is a changelog hint of the pipeline. |
| K3 | `name` unique per system instead of per file (gateways in both systems). |
| – | Positions also take `type` `Star`; starmap entries with a shared `uuid` are left out. |

First run (local, source commit `f182b7f`, 4.10.3-LIVE.13165946): 587 templates (Red Wind 194, after the filter, instead
of the 208 the design counted), 157 positions, 62 aliases of which 24 `drop`; `hauling-templates.json` 570,973 B and
`positions.json` 42,563 B, about 0.6 MB together instead of the 1.5 MB estimated. The byte counts in the manifest example
stay illustrative.
