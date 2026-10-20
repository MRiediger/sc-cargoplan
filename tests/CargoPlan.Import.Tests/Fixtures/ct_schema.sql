-- Reconstructed minimal schema of the ContractTracker V2 database: only the columns CargoPlan reads
-- (the fingerprint in docs/contracttracker-schema.md), with their names and declared types.
-- Not a copy of ContractTracker's schema. Observed with ContractTracker 2.2.0.0 (D38).

CREATE TABLE "Contracts" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL
);

CREATE TABLE "ContractObjectives" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "ContractId" INTEGER NOT NULL REFERENCES "Contracts" ("Id") ON DELETE CASCADE,
    "Amount" INTEGER NOT NULL,
    "Destination" TEXT NOT NULL
);
