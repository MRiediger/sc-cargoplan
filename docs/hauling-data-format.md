# Hauling data format (templates and positions)

**Status: contract of Phase 13, written with T13.0 (21 Nov 2026), no reader or writer yet.** The data pipeline in
[sc-cargoplan-shipdata](https://github.com/MRiediger/sc-cargoplan-shipdata) writes both files with its hauling job
(SD14 there); the app reads them from T13.3 on. Why the data come this way: [D119](decisions.md); what the route planner
does with them: [D118](decisions.md) and the design, [`design/cargo-routes.md`](design/cargo-routes.md) (§7, §9.3).
Where the design differs from this page (it names `hub-profiles.json` and a `distances.json` matrix), this page holds.

The route planner needs two things the game knows and the contracts don't show: every hauling contract the boards can
offer (the **templates**) and where the places lie (the **positions**). Both are extracted from the game files
(`Data.p4k`) by [octfx/ScDataDumper](https://github.com/octfx/ScDataDumper): the templates as published by
`api.star-citizen.wiki` (`/api/missions` and `/api/missions/<uuid>`), the positions as `starmap_positions.json` of
[StarCitizenWiki/scunpacked-data](https://github.com/StarCitizenWiki/scunpacked-data). Both are CIG material, not under
this repository's MIT licence; the data release's `NOTICE.md` says so and credits the sources ([Credits](#credits)).
The app makes no request to either source; it reads the files the data release carries (D119).

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
| `hauling` missing or `null` | Routes tab without templates (pool, matrix and ranking from scans only); ships load; **no notice**. |
| `hauling.schemaVersion` unknown, or a file fails its length, hash or a [loader check](#checks-of-the-loader) | Like a hologram format the app doesn't read (`HoloOffer.UnknownFormat`, D114): one log line, a hint in the Routes tab; ships load. |
| Both files read | Templates and positions for the run; a data change counts from the next start (D111 addendum). |

**Rule for the pipeline (SD14):** a hauling job that fails (API down, a place without a position, a gap in the alias
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
the API publishes without a reward (design §7.6).

```json
{
 "schemaVersion": 1,
 "dataVersion": "ships-2026.12",
 "source": {"api": "https://api.star-citizen.wiki/api/missions", "gameBuild": "4.10.3-LIVE.13165946",
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

| Field | Type | Required | Meaning | From (API field or name part) |
|---|---|---|---|---|
| `schemaVersion` | int | yes | `1` | – |
| `dataVersion` | string | yes | Tag of the data release that carries the file. | the run |
| `source.api`, `source.gameBuild`, `source.fetchedAt` | string | yes | Where and when the run read the templates; the game build the API names. | the run, API |
| `templates[]` | array | yes | Sorted by `uuid`, so the release diff is line by line. | |
| `uuid` | string | yes | Identity of the template, stable across runs; the diff compares by it. | `uuid` |
| `debugName` | string | yes | The game's name, kept for tracing. | `debug_name` |
| `contractor` | `covalex` \| `redWind` | yes | Who offers it. | prefix `HaulCargo_` \| `RedWind_` |
| `title` | string | yes | The title as the contract shows it; the scan matches on it (T13.3). | `title` |
| `rank` | `rookie` \| `junior` \| `member` \| `experienced` \| `senior` \| `master` | yes | Rank needed; the order is the list's. A board shows every rank up to the player's (design §1.2). | first word of `title` |
| `size` | `xs` \| `small` \| `medium` \| `large` | yes | Size class; the generator maps the title's size words, an unknown word stops the hauling job. | `title` |
| `direct` | bool | yes | `Direct` in the title. | `title` |
| `shape` | `aToB` \| `singleToMulti` \| `multiToSingle` | yes | One pickup and one drop-off, one pickup and n drop-offs, n pickups and one drop-off. | form part `AToB`, `SingleToMulti<n>`, `Multi<n>ToSingle` |
| `stops` | int ≥ 1 | yes | n of the form; `1` for `aToB`. | form part |
| `system` | `stanton` \| `pyro` \| `null` | yes | Board's system; `null` for `Interstellar`. | geo / area part |
| `areaKey` | string | yes | Covalex: `Stanton` (system-wide), `Stanton1` Hurston, `Stanton2` Crusader, `Stanton3` ArcCorp, `Stanton4` microTech, `CrossStanton`, `Interstellar`. Red Wind: `Solar`, `RegionA`…`RegionD`, `Planetary`. | geo part (`HaulCargo_`), area part (`RedWind_Pyro_`) |
| `missionType` | `local` \| `planetary` \| `stellar` \| `interstellar` | yes | Area of operations (design §1.1, §7.2). All four are in the file; which the planner uses is the app's (Phase 13: `stellar`). | `mission_type` `Hauling - <Type>` |
| `faction` | `hh` \| `cfp` \| `null` | yes | Red Wind's side, Headhunters or Citizens for Prosperity; `null` for Covalex. | faction part (`RedWind_Pyro_`) |
| `reward` | int \| `null` | yes | Fixed reward of the variant in aUEC; `null` where the API has none (all of Red Wind). Never estimated. | `reward_min` |
| `legs[]` | array, ≥ 1 | yes | One per hauling order, in the API's order. | `hauling_orders[]` |
| `legs[].commodity` | string | yes | Commodity as the contract names it. | order's commodity |
| `legs[].minScu`, `legs[].maxScu` | int | yes | SCU band an instance is drawn from. | `min_scu`, `max_scu` |
| `legs[].containerScu` | int | yes | Largest crate, one of 1, 2, 4, 8, 16, 24, 32. | `max_container_size` |
| `pickupPool[]`, `dropoffPool[]` | string array | yes | Names of the places an instance draws from; each is exactly the `name` of one entry of `positions.json`. May be `[]` where the API's pool is empty (design §5); the pipeline fills nothing in, the app shows such edges from scans (design §8.3). | `mission_tokens` `Location\|Address`, `Destination*\|Address` |

### Filter

The file holds templates that can be offered, and nothing else:

- `released = true` and `not_for_release = false`; anything else is a template the game never offers.
- Geo part `TEMPLATE` goes: an abstract parent, not in the game.
- Only the two naming schemes `HaulCargo_*` and `RedWind_Pyro_*` (design §7.1, §7.6). Dead Saints and Ling Family
  (interstellar, no board, design §7.6) are not in schema 1.

### Normalizing the two naming schemes

| Field | `HaulCargo_<Form>_<Category>_<Commodity>_<Geo>_<Grade>` | `RedWind_Pyro_<Grade>_<Area>_<Faction>_<RouteType>_<Commodity>_CargoHauling_<Form>` |
|---|---|---|
| `contractor` | `covalex` | `redWind` |
| `shape`, `stops` | `<Form>` | `<Form>` |
| `system` | `Stanton…`, `CrossStanton` → `stanton`; `Interstellar` → `null` | `pyro` |
| `areaKey` | `<Geo>` as written | `<Area>` as written |
| `faction` | `null` | `<Faction>` lower case |
| `missionType` | `mission_type` | `mission_type` |
| `title`, `rank`, `size`, `direct` | title form `<Rank> Rank - [Direct] <Size> Cargo Haul` | title form `<Rank> Hauler Needed for [Direct] <Size> Shipment` |

`<Category>`, `<Grade>` and `<RouteType>` stay in `debugName` only; the fields above carry what the planner needs. The
title forms are the ones `ContractTextParser` learns with T13.1, so a scan and a template speak the same words.

## positions.json

### Example

Coordinates illustrative, chosen so the distances are the ones the design measured from `starmap_positions.json`
(§9.3): Everus Harbor → HUR-L1 1.3 Gm, → HUR-L3 25.7 Gm. The two Pyro places complete the templates example.

```json
{
 "schemaVersion": 1,
 "dataVersion": "ships-2026.12",
 "source": {"repository": "StarCitizenWiki/scunpacked-data", "file": "starmap_positions.json", "commit": "<sha>",
            "fetchedAt": "2026-12-01T04:22:00Z"},
 "positions": [
  {"uuid": "2f7e…", "name": "Checkmate", "system": "pyro", "type": "Manmade", "parent": null,
   "x": -41255000000, "y": 18730000000, "z": 0},
  {"uuid": "9a41…", "name": "Everus Harbor", "system": "stanton", "type": "Manmade", "parent": null,
   "x": 12850400000, "y": 0, "z": 0},
  {"uuid": "c3d0…", "name": "HUR-L1 Green Glade Station", "system": "stanton", "type": "Manmade_VisibleOnInteraction",
   "parent": null, "x": 11550400000, "y": 0, "z": 0},
  {"uuid": "51be…", "name": "HUR-L3 Thundering Express Station", "system": "stanton",
   "type": "Manmade_VisibleOnInteraction", "parent": null, "x": -12849600000, "y": 0, "z": 0},
  {"uuid": "e88a…", "name": "Ruin Station", "system": "pyro", "type": "Manmade", "parent": null,
   "x": -27600000000, "y": 9900000000, "z": 0}
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
| `uuid` | string | yes | The starmap entity's `uuid`, for tracing and the diff ("moved", "renamed"). |
| `name` | string | yes | The place's name **as contracts show it**. Unique in the file. Where the starmap names a place differently, the pipeline's alias table maps it; a pool name with no position stops the hauling job. |
| `system` | `stanton` \| `pyro` \| `nyx` | yes | Coordinates are per system; a distance across systems is not defined. |
| `type` | string | yes | The starmap's `type` as written (`Manmade`, `Manmade_VisibleOnInteraction`, `Planet`, …). |
| `parent` | string \| `null` | yes | `name` of the parent position when it is in the file (a station's planet), else `null`. For the schematic map (planet = hub + L ring, T13.7). |
| `x`, `y`, `z` | number | yes | Position in metres, system coordinates. |

**Content:** every place any `pickupPool` or `dropoffPool` names, plus the stations (`Manmade`,
`Manmade_VisibleOnInteraction`) and planets of Stanton, Pyro and Nyx. Planets in Star Citizen do not move, so the
positions are static between game builds.

**Distances are computed, not stored:** the distance of two places of one system is the Euclidean distance of their
positions; the app shows it in Gm (10⁹ m). A matrix in the file would be redundant and grow with the square of the places.

## Names: two matches, each in one place

- **Template ↔ position: exact string.** Every pool name equals one `name` in `positions.json` of the same release, byte
  for byte. The pipeline ensures it (alias table, stop of the hauling job otherwise); the app's loader checks it.
- **Scan ↔ position: the B1 key, in the app only.** A scanned place is matched by `DestinationNames.Key` (OCR-tolerant,
  suffix "Station" optional), which moves to Core with T13.3. The files carry no key: a second implementation in the
  pipeline would drift. The app's data test checks that no two positions of one system share a key.

## Checks of the loader

At this boundary the app checks (CLAUDE.md A6); a failed check means the case "fails a loader check" above:

- both `schemaVersion` values equal `hauling.schemaVersion` and are known;
- `name` unique in `positions.json`; every pool name found there;
- per leg `1 ≤ minScu ≤ maxScu` and `containerScu` ∈ {1, 2, 4, 8, 16, 24, 32};
- `legs` has one entry for `aToB` and `stops` entries for `singleToMulti` (design §7.3: four bands for a four-stop
  Senior Medium); for `multiToSingle` SD14 states the count from the API before the app checks it.

Unknown fields are ignored. A template with an unknown enum value (`contractor`, `rank`, `size`, `shape`,
`missionType`, `faction`) is skipped and logged, the others load: a new rank or size in a game build then costs only
its templates.

## Release diff

The hauling job writes into the release's changelog what changed against the previous release: templates new and gone
(by `uuid`), reward changes, places new, gone, renamed or moved (by `uuid`; the threshold for "moved" is SD14's).

## Credits

The data release's `NOTICE.md` names, for both files: data from the Star Citizen game files (CIG), extracted with
octfx/ScDataDumper; templates via **api.star-citizen.wiki**; positions via StarCitizenWiki/scunpacked-data. The README
and the wiki page "Routes" repeat the credit (T13.8).
