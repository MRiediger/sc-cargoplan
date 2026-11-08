# Ship hologram format (SCH2)

Ship holograms are an optional download for the ship view (see [D63](decisions.md)). They are not part of the
repository or the app ZIP: each ship is one file `<modelKey>.holo` in a GitHub release of its own (tag
`holo-v2` since v0.3.0; `holo-v1` stays for v0.1 and v0.2), next to a `manifest.json`. The files derive from game
models (CIG) and are not under the repository's MIT licence; the release's `NOTICE.md` says so. A pipeline outside
this repository writes them ([Pipeline](#pipeline-holo-v2)).

**Source (D93):** holo-v2 takes every ship's model from cryon.rocks (Motherlode): cargo grid data and ship models
extracted from the Star Citizen game files, the same models the racks come from (D92). The three ships without a model
there (135c, 600i Executive Edition, F7C Hornet Mk II) still use hull data from fleetyards.net: their holo-v1 file,
derived from Fleetyards' hologram models, byte for byte (D94).

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
- **Origin, holo-v2 (D93):** the game model's own origin. **No centring, no scaling, no mirroring:** the model is only
  turned into ship axes. Motherlode's `.glb` lie in three.js space, glTF = (game x, game z, −game y) with game x
  starboard, y bow, z up, so ship = (−glTF z, −glTF x, glTF y), a rotation (determinant +1) and the same mapping
  `tools/import-motherlode.ps1` applies to the cargo grids. The ship file puts this origin at `holoOrigin` = the ship's
  fraction s (D92). The Hull A, B and C models carry the spindle in its cargo pose, as at the elevator.
- **Origin, holo-v1** (and the three Fleetyards files in holo-v2): the middle of the vertices' x/y box, z = 0 at the
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

`manifest.json` lists every hologram of the release. A ship that isn't listed has no hologram; the app shows the
outline (D62).

```json
{
  "dataVersion": "holo-v2",
  "formatVersion": 2,
  "generatedAt": "2026-11-07",
  "pipelineCommit": "…",
  "tools": { "meshoptimizer": "1.3.0", "node": "24.16.0", "python": "3.14.5", "numpy": "2.5.3" },
  "ships": [
    { "slug": "misc-hull-a", "file": "misc-hull-a.holo", "bytes": 185882, "sha256": "…", "model": "MISC_Hull_A",
      "rawSha256": "…", "budget": 20000, "triangles": 13085, "edges": [1445, 3411, 9620], "passed": true,
      "metrics": { "errorM": 0.0, "topIou": 1.0, "…": "…" } }
  ]
}
```

| Field | Meaning |
|---|---|
| `dataVersion` | the release tag |
| `formatVersion` | the files' `formatVersion`; an app that doesn't know it uses none of them |
| `pipelineCommit` | the pipeline's commit the files came from |
| `ships[].sha256` | hash of the file; a cached file with another hash is stale |
| `ships[].model` | holo-v2: the Motherlode model (`className`) the file comes from, or `fleetyards (holo-v1)` |
| `ships[].rawSha256` | hash of the source model, for the pipeline |
| `ships[].budget`, `triangles`, `edges` | the budget step the ship came out at (20 k / 30 k / 40 k), its triangles, its edge counts per level |
| `ships[].passed`, `metrics` | whether the ship met every criterion of the pipeline, and the measurements; published either way once approved (D63) |
| further fields | a reader ignores fields it doesn't know |

**Withdrawal:** a ship removed from the manifest is withdrawn; an app deletes its cached file and shows the outline.
A manifest that can't be fetched (offline, timeout, a moment without the file while it is replaced) withdraws
nothing: the app keeps its cache.

**Withdrawing everything:** publish a manifest with an empty `ships` list. Don't delete the manifest: a 404 reads as
"can't be fetched" and keeps every cache.

## Reading side (SC-CargoPlan)

How the overlay gets and uses the files (D63). The overlay's only network access.

- **Requests:** plain HTTPS GETs of `https://github.com/MRiediger/sc-cargoplan/releases/download/holo-v2/<file>`
  (GitHub redirects to its asset host), User-Agent `SC-CargoPlan/<version>`, 5 s per request. Any 2xx answer is
  taken; a file is kept only if its length and SHA256 match the manifest. Nothing else is sent, no API, no token.
- **Cache:** `%LocalAppData%\SC-CargoPlan\holo\holo-v2\`, one `<slug>.holo` per ship and the last `manifest.json`.
  The tag names the folder, so a new release starts an empty one. Only the current tag's folder stays (D95): another
  `holo-v<n>` folder found at the start goes once the current manifest is checked (fetched, or cached and younger than
  24 hours), before the first file downloads, and the overlay says once that the holograms download again; at once
  when downloads are off or the look is no hologram. A manifest that can't be had leaves it, so the older app still
  finds its cache. A folder that can't be deleted is named in the notice and tried again on the next start.
- **Manifest:** a cached copy younger than 24 hours is used without a request; otherwise the app asks once per run,
  when it first needs a hologram. A manifest fetched now or within those 24 hours withdraws ships it doesn't list; an
  older one only stands in while the release can't be reached.
- **A ship's file:** taken from the cache when its hash matches the manifest, else downloaded. Without any manifest
  (offline on the first run) a cached file is shown, it was checked when it was written.
- **Downloads off** (`downloadShipHolograms: false`): no request at all; cached files still show.
- **Reading:** every rule of [File](#file) is checked (`HoloReader`); a file that breaks one shows the outline.
- **Failures** end in the outline; the first of a run names its reason in the overlay's notice line.
- **Drawing:** metres ÷ 1.25 = cells, the mesh's origin at the ship file's `holoOrigin`, whatever its racks do (D90; a
  file without it: the middle of the bounds on the middle of the racks in x, y and z). For a ship with a Motherlode
  model `holoOrigin` is its fraction s, written by the import (D93); see
  [ship-format.md](ship-format.md#hologram-and-outline). The looks and the hidden-line levels by zoom (level 1 below
  1.5×, 2 below 3×, 3 from 3×) are described in D63.

## Pipeline (holo-v2)

In the private repository `sc-ship-outlines` (versions in its `TOOLS.md`). Per model, files in `holo-work-ml/<className>/`:

1. `fetch-motherlode.ps1`: the dated catalog and the models with their SHA-256 list (`motherlode/`).
2. `tools/import-motherlode.ps1 -Catalog <catalog> -ModelMap <model-map.csv>` (this repository): racks, and the map
   id → className, slug, s.
3. `run-all.ps1 -Source Motherlode`: per model `holo_mesh.py flatten-model` (turned into ship axes, stray specks out,
   welded; origin and size kept), `cull`, meshoptimizer on the ladder 20 k / 30 k / 40 k, `export` with `judge`, as for
   holo-v1.
4. `holo_mesh.py outline <className>` per model: the outline (see [ship-format.md](ship-format.md#hologram-and-outline)).
5. `holo_mesh.py outlines-v2` → `outlines/ship-outlines.json` here; `release-v2` → the release folder; `verify`.
6. `tools/import-motherlode.ps1` again: with the new outlines' `floorM` it writes the anchors.
7. `holo_mesh.py fit` (racks in the hull, outline on the hull) and the contact sheet before the upload.
