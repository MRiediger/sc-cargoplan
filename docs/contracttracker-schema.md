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
| `ContractObjectives` | `Id` | `INTEGER` |
| `ContractObjectives` | `ContractId` | `INTEGER` |
| `ContractObjectives` | `Amount` | `INTEGER` |
| `ContractObjectives` | `Destination` | `TEXT` |

The same columns, rebuilt for tests: [`tests/CargoPlan.Import.Tests/Fixtures/ct_schema.sql`](../tests/CargoPlan.Import.Tests/Fixtures/ct_schema.sql).

## Mapping

| CargoPlan | ContractTracker | Note |
|---|---|---|
| `MissionStop.Destination` | `ContractObjectives.Destination` | OCR text, may carry a tail of contract prose |
| `MissionItem.Commodity` | `Contracts.Name` | one `Contracts` row per commodity |
| `MissionItem.Scu` | `ContractObjectives.Amount` | per destination |
| `MissionItem.MaxCrateSize` | – | **missing**; only in the prose of `Contracts.RawOcrText`, if at all |
| `MissionStop.Order` | – | **missing**; ContractTracker has no route order |
| Pickup | (`Contracts.PickupLocation`) | CargoPlan has no pickup field; the text varies per capture, several pickups are joined with ` / ` |
| Identity | `Contracts.Id`, `ContractObjectives.Id` | AUTOINCREMENT, never reused, also after a reset |

`ContractObjectives.ContractId` references `Contracts.Id` with `ON DELETE CASCADE`.

## Not read

| Column | Why not |
|---|---|
| `Contracts.PickupLocation` | no field in CargoPlan; unreliable as a key |
| `Contracts.Reward` | no field in CargoPlan; 0 in every row observed |
| `Contracts.Status` | `Active` in every row observed; ContractTracker deletes instead |
| `ContractObjectives.Completed` | 0 in every row observed; ContractTracker deletes instead |
| `Contracts.CapturedAt` | UTC without a marker; not needed for the plan |
| `Contracts.RawOcrText`, `Contracts.SessionId`, `Sessions`, `LocationMappings` | not needed yet; T5.3 may add `LocationMappings` (OCR text → correct name) or parse the max crate size from `RawOcrText`, and then extends the fingerprint |

## Open questions

- Is `Contracts.Status` ever anything but `Active`, or `ContractObjectives.Completed` ever 1?
- Does ContractTracker apply a `LocationMappings` row to later captures (`UseCount`)?
- Does ContractTracker skip an identical repeated capture, or was the key press missed?
- What makes ContractTracker close its connection and checkpoint while running?
