# Ship hologram format (SCH2)

Ship holograms are an optional download for the ship view (see [D63](decisions.md)). They are not part of the
repository or the app ZIP: each ship is one file `<modelKey>.holo`, an asset of the ship data release
(`sc-cargoplan-shipdata`, tag `ships-YYYY.MM[.N]`) next to its `ships.zip`, listed in the `holo` section of its
`manifest.json` (D114, [Manifest](#manifest)). The files derive from game models (CIG) and are not under the
repository's MIT licence; the release's `NOTICE.md` says so. The data pipeline in `sc-cargoplan-shipdata` writes them.

**History:** until v0.5.x the holograms were a release of their own in this repository, `holo-v1` (v0.1, v0.2),
`holo-v2` (v0.3.0 to v0.5.0) and `holo-v3` (v0.5.1), each with a manifest of its own; `holo-v3` stays published until
v0.5.x has run out (T12.6). v0.6.0 reads only the data releases.

**Source (D93):** since holo-v2 every ship's model comes from cryon.rocks (Motherlode): cargo grid data and ship models
extracted from the Star Citizen game files, the same models the racks come from (D92). holo-v3 took the catalog of
game build 4.10.3 (new: Galaxy, Liberator, Retaliator; 13 models changed). Two ships still use hull data from
fleetyards.net, their holo-v1 file byte for byte (D94): the 600i Executive Edition has no model there, the 135c's cargo
grid has no placement; the F7C Hornet Mk II has the game model since `ships-2026.11.1`. The Cyclone, gone from the 4.10.3
catalog, keeps its holo-v2 file.

## File

Little endian, no compression, no padding. Offsets follow from the header.

| Offset | Type | Field | Meaning |
|---|---|---|---|
| 0 | 4 bytes | `magic` | `SCH2` (ASCII) |
| 4 | uint16 | `formatVersion` | `2` |
| 6 | uint16 | `headerBytes` | `60`; data starts here |
| 8 | uint32 | `flags` | `0`, reserved |
| 12 | uint32 | `vertexCount` | |
| 16 | uint32 | `triangleCount` | |
| 20 | uint32 | `edgeCount` | feature edges, all levels |
| 24 | uint32 | `level1Count` | edges in level 1 |
| 28 | uint32 | `level2Count` | edges in level 2, level 1 included |
| 32 | float32 | `lengthM` | the model's extent along x in metres (holo-v2: the game model's, nothing scaled; holo-v1: Fleetyards' length, the mesh scaled to it) |
| 36 | float32 × 6 | `bounds` | min x, y, z, max x, y, z of the vertices, metres |
| 60 | float32 × 3 × `vertexCount` | vertices | x, y, z in metres |
| … | index × 3 × `triangleCount` | triangles | |
| … | index × 2 × `edgeCount` | edges | |

- **Index type:** `uint16` when `vertexCount` < 65,536, else `uint32`, for triangles and edges alike.
- **File length** is exactly `headerBytes + 12 × vertexCount + indexBytes × (3 × triangleCount + 2 × edgeCount)`. A
  reader rejects any other length, an unknown `magic` or `formatVersion`, an index ≥ `vertexCount`, or levels that are
  not `level1Count ≤ level2Count ≤ edgeCount`.
- **Axes:** the ship's, as in [ship-format.md](ship-format.md): x towards the bow, y towards port, z up, right-handed.
  Units are metres, not cells (1 cell = 1.25 m).
- **Origin, holo-v2 and later (D93):** the game model's own origin. **No centring, no scaling, no mirroring:** the model is only
  turned into ship axes. Motherlode's `.glb` lie in three.js space, glTF = (game x, game z, −game y) with game x
  starboard, y bow, z up, so ship = (−glTF z, −glTF x, glTF y), a rotation (determinant +1) and the same mapping
  the import (`import-motherlode.ps1`, in sc-cargoplan-shipdata since D113) applies to the cargo grids. The ship file puts this origin at `holoOrigin` = the ship's
  fraction s (D92). The Hull A, B and C models carry the spindle in its cargo pose, as at the elevator.
- **Origin, holo-v1** (and the three Fleetyards files in holo-v2 and holo-v3): the middle of the vertices' x/y box, z = 0 at the
  lowest vertex (keel), set before faces hidden from outside are dropped, so the shipped box can lie a little off it
  (up to 0.75 m in x, 0.57 m in z). It is not the origin of the source model (D90).
- **Triangles** are wound counter-clockwise seen from outside. The mesh is welded (vertices shared between
  triangles) and carries no normals: a reader that wants flat shading splits vertices per triangle, smooth shading
  splits them along the feature edges.
- **Edges** are vertex index pairs, ordered by level: first the level 1 edges, then those only in level 2, then those
  only in level 3. Level n is the first `levelNCount` edges (level 3 = all). An edge is a crease of at least 30° between
  two triangles, an edge of three or more triangles, or an open edge.

## Drawing the hidden-line look

Level n draws:

1. the triangles opaque (they hide what lies behind them);
2. the first `levelNCount` edges;
3. the contour: every edge between a triangle facing the camera and one facing away, but **only where the two
   triangles meet at less than 30°** (measured with the winding, as above). Creases at 30° or more are feature edges
   and appear only with their level; drawing them as contour too would put every bevel of the hull into level 1.

The 30° is part of format version 2; the pipeline and the app use the same value.

## Manifest

The `holo` section of the data release's `manifest.json` lists every hologram of the release (the whole manifest:
`sc-cargoplan-shipdata`, `docs/manifest-format.md`). A ship that isn't listed has no hologram; the app shows the
outline (D62).

```json
{
  "dataVersion": "ships-2026.11.1",
  "…": "…",
  "holo": {
    "formatVersion": 2,
    "ships": {
      "misc-hull-a": { "file": "misc-hull-a.holo", "bytes": 185882, "sha256": "…", "model": "MISC_Hull_A",
                       "rawSha256": "…", "budget": 20000, "triangles": 13085, "edges": [1445, 3411, 9620],
                       "passed": true, "fails": [], "metrics": { "errorM": 0.0, "topIou": 1.0, "…": "…" } }
    }
  }
}
```

| Field | Meaning |
|---|---|
| `holo` | required; `null` is the kill switch: the release offers no hologram, every ship shows its outline |
| `holo.formatVersion` | the files' `formatVersion`; an app that doesn't know it uses none of them, the ships still load (R1 of T12.4) |
| `holo.ships.<slug>` | the slug is the ship file's `modelKey` |
| `….file` | the asset's name, flat (`<slug>.holo`); its URL is `…/releases/download/<dataVersion>/<file>` (no `baseUrl`, SD2) |
| `….bytes`, `….sha256` | length and hash of the file; a download of another length or hash is broken, a cached file with another hash is stale |
| `….model` | the Motherlode model (`className`) the file comes from, or `fleetyards (holo-v1)` |
| `….rawSha256`, `budget`, `triangles`, `edges`, `passed`, `fails`, `metrics` | the pipeline's and judge's numbers; the app doesn't read them |
| further fields | a reader ignores fields it doesn't know |

**Withdrawal:** a new data release without the ship in `holo.ships`, or with `holo: null` for all of them. A player on
the older release keeps its holograms until the update; deleting a hologram asset gives that player the outline for
every ship not yet cached (a 404, see below).

**History:** the hologram releases `holo-v1` to `holo-v3` had a `manifest.json` of their own (`dataVersion` = the
holo tag, `ships` as a list with `slug`); v0.5.x fetched it at most once a day and read a ship missing from it as
withdrawn.

## Reading side (SC-CargoPlan)

How the overlay gets and uses the files (D63, D114). The overlay's first network access; the second is the update
check (D109), the third the ship data check (D112).

- **The list:** the `holo` section of the ship data in use, read with its manifest at the start (`ActiveShipData`); it
  changes only with a restart. No request for it.
- **Requests:** plain HTTPS GETs of
  `https://github.com/MRiediger/sc-cargoplan-shipdata/releases/download/<dataVersion>/<file>` (GitHub redirects to its
  asset host; `--ship-data-source` with a repository takes that one), User-Agent `SC-CargoPlan/<version>`, 5 s per
  request. Any 2xx answer is taken; a file is kept only if its length and SHA256 match the manifest. Nothing else is
  sent, no API, no token.
- **Cache:** `%LocalAppData%\SC-CargoPlan\holo\<dataVersion>\`, one `<slug>.holo` per ship. The data version names the
  folder, so new ship data start an empty one. Only that folder stays (D95): another one found at the start, a
  `holo-v<n>` of v0.5.x or another data version, goes at the first request with downloads on, before any download,
  and the overlay says once that the holograms download again; at once and silently when downloads are off, the look
  is no hologram or the data offer none. A folder that can't be deleted is named in the notice and tried again on the
  next start; folders of any other name stay.
- **A ship's file:** taken from the cache when length and hash match the manifest, also with downloads off; else
  downloaded.
- **No holograms on offer** (`holo: null`, or a `formatVersion` this app doesn't read): no request, every ship shows its
  outline, one log line at the start, the settings window says so under the switch; no notice.
- **A 404** (the release or the asset is gone; only the last two data releases stay): the outline for that ship, a
  cached file stays, one log line per run, no notice, the ship isn't asked for again in the run. The ship data update
  line is the way back.
- **Downloads off** (`downloadShipHolograms: false`): no request at all; cached files still show.
- **Reading:** every rule of [File](#file) is checked (`HoloReader`); a file that breaks one shows the outline.
- **Failures** (no connection, timeout, another status, a damaged file) end in the outline; the first of a run names
  its reason in the overlay's notice line.
- **Drawing:** metres ÷ 1.25 = cells, the mesh's origin at the ship file's `holoOrigin`, whatever its racks do (D90; a
  file without it: the middle of the bounds on the middle of the racks in x, y and z). For a ship with a Motherlode
  model `holoOrigin` is its fraction s, written by the import (D93); see
  [ship-format.md](ship-format.md#hologram-and-outline). The looks and the hidden-line levels by zoom (level 1 below
  1.5×, 2 below 3×, 3 from 3×) are described in D63.

## Pipeline (history: holo-v2 and holo-v3)

Since D111 the data pipeline in `sc-cargoplan-shipdata` builds the holograms with every data release (its `TASKS.md`
and `docs/`). holo-v2 and holo-v3 were built in the private repository `sc-ship-outlines` (versions and every call with its arguments in its
`TOOLS.md`). Per model, files in `holo-work-ml/<className>/`:

1. `fetch-motherlode.ps1`: the dated catalog and the models with their SHA-256 list (`motherlode/`).
2. `import-motherlode.ps1 -Catalog <catalog> -ModelMap <model-map.csv>` (in sc-cargoplan-shipdata since T12.5, D113; this repository until v0.5.x): racks, and the map
   id → className, slug, s. **The slug is the ship file's `modelKey`:** a ship without one is left out of every step
   below (holo-v3: Galaxy, Liberator, Retaliator and the 350r got theirs by hand first). The test
   `Every_outline_belongs_to_a_shipped_ship` catches a ship that loses its key.
3. `run-all.ps1 -Source Motherlode`: per model `holo_mesh.py flatten-model` (turned into ship axes, stray specks out,
   welded; origin and size kept), `cull`, meshoptimizer on the ladder 20 k / 30 k / 40 k, `export` with `judge`, as for
   holo-v1. A changed model needs `-Redo flatten`: existing steps are reused.
4. `holo_mesh.py outline <className>` per model: the outline (see [ship-format.md](ship-format.md#hologram-and-outline)).
5. `holo_mesh.py outlines-v2 … holo-v<n> <kept slugs>` → `outlines.json` of the data release (until v0.5.x `outlines/ship-outlines.json` here); `release-v2 … holo-v<n>
   <previous release> <kept slugs>` → the release folder with its manifest; `verify`.
6. `import-motherlode.ps1` again: with the new outlines' `floorM` it writes the anchors.
7. `holo_mesh.py fit` (racks in the hull, outline on the hull), the contact sheet and `ShipPictureTests` against the
   previous release before the upload; the release goes to **this** repository as a pre-release (the app reads only
   from here; `sc-ship-outlines` is private).
