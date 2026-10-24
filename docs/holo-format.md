# Ship hologram format (SCH2)

Ship holograms are an optional download for the ship view (see [D63](decisions.md)). They are not part of the
repository or the app ZIP: each ship is one file `<fleetyardsSlug>.holo` in a GitHub release of its own (tag
`holo-v1`), next to a `manifest.json`. The files derive from game models (CIG) through Fleetyards and are not under
the repository's MIT licence; the release's `NOTICE.md` says so. A pipeline outside this repository writes them.

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
| 32 | float32 | `lengthM` | the ship's length in metres (Fleetyards), the mesh is scaled to it |
| 36 | float32 × 6 | `bounds` | min x, y, z, max x, y, z of the vertices, metres |
| 60 | float32 × 3 × `vertexCount` | vertices | x, y, z in metres |
| … | index × 3 × `triangleCount` | triangles | |
| … | index × 2 × `edgeCount` | edges | |

- **Index type:** `uint16` when `vertexCount` < 65,536, else `uint32`, for triangles and edges alike.
- **File length** is exactly `headerBytes + 12 × vertexCount + indexBytes × (3 × triangleCount + 2 × edgeCount)`. A
  reader rejects any other length, an unknown `magic` or `formatVersion`, an index ≥ `vertexCount`, or levels that are
  not `level1Count ≤ level2Count ≤ edgeCount`.
- **Axes:** the ship's, as in [ship-format.md](ship-format.md): x towards the bow, y towards port, z up, right-handed.
  Units are metres, not cells (1 cell = 1.25 m). Origin: the middle of the vertices' x/y box, z = 0 at the lowest
  vertex (keel).
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
  "dataVersion": "holo-v1",
  "formatVersion": 2,
  "generatedAt": "2026-10-24",
  "pipelineCommit": "2959336",
  "tools": { "meshoptimizer": "1.3.0", "gltf-transform": "4.5.1", "node": "24.16.0", "python": "3.14.5", "numpy": "2.5.3" },
  "ships": [
    { "slug": "misc-hull-a", "file": "misc-hull-a.holo", "bytes": 396120, "sha256": "…", "rawSha256": "…",
      "budget": 20000, "triangles": 19878, "edges": [2472, 6140, 15000], "passed": true,
      "metrics": { "errorM": 0.0507, "topIou": 0.9969, "…": "…" } }
  ]
}
```

| Field | Meaning |
|---|---|
| `dataVersion` | the release tag |
| `formatVersion` | the files' `formatVersion`; an app that doesn't know it uses none of them |
| `pipelineCommit` | the pipeline's commit the files came from |
| `ships[].sha256` | hash of the file; a cached file with another hash is stale |
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

- **Requests:** plain HTTPS GETs of `https://github.com/MRiediger/sc-cargoplan/releases/download/holo-v1/<file>`
  (GitHub redirects to its asset host), User-Agent `SC-CargoPlan/<version>`, 5 s per request. Any 2xx answer is
  taken; a file is kept only if its length and SHA256 match the manifest. Nothing else is sent, no API, no token.
- **Cache:** `%LocalAppData%\SC-CargoPlan\holo\holo-v1\`, one `<slug>.holo` per ship and the last `manifest.json`.
- **Manifest:** a cached copy younger than 24 hours is used without a request; otherwise the app asks once per run,
  when it first needs a hologram. A manifest fetched now or within those 24 hours withdraws ships it doesn't list; an
  older one only stands in while the release can't be reached.
- **A ship's file:** taken from the cache when its hash matches the manifest, else downloaded. Without any manifest
  (offline on the first run) a cached file is shown, it was checked when it was written.
- **Downloads off** (`downloadShipHolograms: false`): no request at all; cached files still show.
- **Reading:** every rule of [File](#file) is checked (`HoloReader`); a file that breaks one shows the outline.
- **Failures** end in the outline; the first of a run names its reason in the overlay's notice line.
- **Drawing:** metres ÷ 1.25 = cells, the middle of the bounds on the middle of the racks in x, y and z; the looks
  and the hidden-line levels by zoom (level 1 below 1.5×, 2 below 3×, 3 from 3×) are described in D63.
