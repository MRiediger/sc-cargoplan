# ContractTracker database

CargoPlan reads the contracts that ContractTracker V2 captures, read-only, from its SQLite database.
ContractTracker is closed source; this file lists only the tables and columns CargoPlan reads, not its schema.
How ContractTracker behaves, and what follows for CargoPlan, is in [D38](decisions.md).

**Licence:** asked of the author in the Nexus Discord on 2026-10-20 09:26, no answer yet. If the author objects, this file is removed.

## Location

`%APPDATA%\SCContractTracker\contracts.db`

The name is fixed; ContractTracker's `settings.json` in the same folder has no setting for it.
While ContractTracker runs, `contracts.db-wal` and `contracts.db-shm` appear next to it.

## Observed with

| | |
|---|---|
| ContractTracker | 2.2.0.0 (`SC Contract Tracker.exe`) |
| `PRAGMA user_version` | 0 |
| `PRAGMA journal_mode` | `wal` |
| `PRAGMA schema_version` | 13 |
| Migrations table | none |

## Fingerprint

The reader reads exactly these columns. Before reading, each must exist with this declared type
(`pragma_table_info`); more tables or columns are ignored. Any difference: nothing is read.

| Table | Column | Type |
|---|---|---|
| `Contracts` | `Id` | `INTEGER` |
| `Contracts` | `Name` | `TEXT` |
| `Contracts` | `CapturedAt` | `TEXT` |
| `Contracts` | `RawOcrText` | `TEXT` |
| `ContractObjectives` | `Id` | `INTEGER` |
| `ContractObjectives` | `ContractId` | `INTEGER` |
| `ContractObjectives` | `Amount` | `INTEGER` |
| `ContractObjectives` | `Destination` | `TEXT` |

The same columns, rebuilt for tests: [`tests/CargoPlan.Import.Tests/Fixtures/ct_schema.sql`](../tests/CargoPlan.Import.Tests/Fixtures/ct_schema.sql).

## Mapping

CargoPlan parses `Contracts.RawOcrText` itself (D39); ContractTracker's destination rows are the fallback when the
text holds no readable delivery line. Both go through the same normalisation (D40).

| CargoPlan | ContractTracker | Note |
|---|---|---|
| `MissionStop.Destination` | "Deliver … to *destination*" lines in `RawOcrText`; fallback `ContractObjectives.Destination` | station name only, cut before " at …"/" above …" or a torn "abov"/"aboy" (D40) |
| `MissionItem.Commodity` | "… SCU of *commodity* to …" in `RawOcrText`; fallback `Contracts.Name` | one capture may hold several commodities |
| `MissionItem.Scu` | "… 0/*N* SCU …" in `RawOcrText`; fallback `ContractObjectives.Amount` | per destination |
| `MissionItem.MaxCrateSize` | prose in `RawOcrText` ("Max size will be N SCU.", "none of the containers are bigger than N SCU.") | tolerant: up to two misread words after "Max size", O/I/L/\| in the number corrected; missing or no crate size → largest crate that fits the SCU, marked as assumed (D40) |
| `ContractSource.Reward` | "Reward" line at the start of `RawOcrText` ("Reward\n50,250"), not `Contracts.Reward` | null if missing or the line is not just a number; decides duplicates (D39 addendum) |
| `MissionStop.Order` | – | order in the text; ContractTracker has no route order |
| Capture time | `Contracts.CapturedAt` | UTC without a marker, e.g. `2026-10-20 08:22:54.1193454` |
| Pickup | (`Contracts.PickupLocation`) | CargoPlan has no pickup field; the text varies per capture, several pickups are joined with ` / ` |
| Identity | `Contracts.Id` | AUTOINCREMENT, never reused, also after a reset; the rows of one capture have consecutive ids and identical `RawOcrText` |

`ContractObjectives.ContractId` references `Contracts.Id` with `ON DELETE CASCADE`.

## How CargoPlan opens it

`Mode=ReadOnly;Pooling=False`, retrying a busy or locked database for 5 seconds. Read-only means no write into tables
or schema; it does **not** mean no file is touched: while ContractTracker is closed, SQLite creates
`contracts.db-wal` and `contracts.db-shm` next to the database and may leave them there (D38). CargoPlan leaves them
alone. Without pooling no handle stays open after a read.

## Not read

| Column | Why not |
|---|---|
| `Contracts.PickupLocation` | no field in CargoPlan; unreliable as a key |
| `Contracts.Reward` | 0 in every row observed; CargoPlan reads the reward from `RawOcrText` instead |
| `Contracts.Status` | `Active` in every row observed; ContractTracker deletes instead |
| `ContractObjectives.Completed` | 0 in every row observed; ContractTracker deletes instead |
| `Contracts.SessionId`, `Sessions` | not needed for the plan |
| `LocationMappings` | ContractTracker's corrections apply to its own parsing; CargoPlan parses the raw text and normalises itself (D40) |

## Raw text layouts

Observed with ContractTracker 2.2.0.0, English game client.

| | Offer view (contract list) | Accepted view (ids 37/38) |
|---|---|---|
| Header | "Contract Availability" + remaining time | "Contract Deadline" |
| Start of the contract text | `DETAILS` | `DETAILS` |
| End of the contract text | `ACCEPT OFFER` | `TRACK`, `ABANDON` (no `ACCEPT OFFER`) |

In both, delivery lines ("Deliver 0/N SCU of … to …") may break **anywhere, also mid-word**: "to Thunde" with
"oss Station" two lines further down; "to High Course" with the rest of the name lost; "to Everus Harbor abov" with
"Hurston." on the next line (ids 47/48). The parser completes such a fragment by character prefix, only if exactly
one name matches: first from the drop-off list, then from "to a freight elevator at …" in the prose (D40).

Ids 47/48 start with "Reward" and the amount on the next line; ids 28–38 start with the contract title, so whether the
reward is in the text depends on the capture region. Whatever else is inside the region is read too, our own overlay
included ("MISC Hull A", "No plan. Open t…").

## Open questions

- Is `Contracts.Status` ever anything but `Active`, or `ContractObjectives.Completed` ever 1?
- Does ContractTracker apply a `LocationMappings` row to later captures (`UseCount`)?
- Does ContractTracker skip an identical repeated capture, or was the key press missed?
- What makes ContractTracker close its connection and checkpoint while running?
- What does the raw OCR text look like with a German game client? Observed in English only.
