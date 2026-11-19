# Archiv: K10 Repo-Hygiene

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## K10 Repo-Hygiene (09.11.2026) – Branch `chore/k10-repo-hygiene`

Befunde aus `docs/review/2026-11-pre-p10.md` (Abschnitt 6.2, K10 vor Phase 10). Ein Commit je Befund, Prefix `K10-Fn`.
Nichts, was Phase 10 ändert: kein ContractTracker-Code, kein Plan- oder Settings-Schema.

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:** F6 → Backlog (Schema 4 steckt in 23 `TestShips`, Umbau
größer als der Nutzen); F9 mit Testumleitung des Logs (`AppLog.Path` internal setzbar, `TestLog` per Modul-Initialisierer)
und ohne die bewusst schluckenden catches (`PlanStore.PeekShipId`, `HoloSource` 3×), 19 Stellen; F13 löscht
`LoadPlanner.MoveCrate` trotz „kein LoadPlanner“ (reine Löschung); T6.6 als Duplikat von T9.3 gestrichen; Backlog auch
für F22 und F6; F25 glättet zusätzlich `AssemblyInfo.cs` (9 Format-Treffer der WPF-Vorlage); F26 nimmt 15,0 s statt < 8 s
Wandzeit hin (Takt jetzt `ImportMotherlodeScriptTests`, Backlog).

- [x] F25 `.editorconfig` an den Code, Format-Gate in CI
- [x] F26 FillRows 4 × 6 × 3 als `Slow`
- [x] F12 `ShipChoicesTests` ohne `ships/` (A10)
- [x] F13 `FromRect`, `ReserveLabel`, `MoveCrate` raus
- [x] F9 jede Notice auch ins Log
- [x] F18 TASKS-Häkchen, Backlog F6 F10 F16 F20 F21 F22
- [x] F19 `.gitignore` → `.git/info/exclude` (die Dateien im Root räumt Martin)
- [x] Abschluss: Messtabelle, Review 6.2

| Befund | Commit | Ergebnis |
|---|---|---|
| F25 | `8ac30e9` | `dotnet format --verify-no-changes` 614 → **0**; CI-Schritt `Format` (Windows) |
| F26 | `b67d0d3` | 4 × 6 × 3 in `Slow` (4 statt 2); Ursache: einziges Grid mit Stapelhöhe 3 |
| F12 | `58a857c` | `ShipLoader.LoadAll()` ohne Ordner nur noch in `ShipPictureTests` (Handlauf) |
| F13 | `9141f7a` | `FromRect`, `ReserveLabel`, `LoadPlanner.MoveCrate` raus; Test-Helfer `PlannerMoves` |
| F9 | `3564698` | 19 neue Logzeilen; Tests schreiben nach `%TEMP%`, das echte Log blieb byte-gleich |
| F18 | `2660944` | offene `### [ ]` nur noch T9.3, R5, T10.x, T11.x; 7 Backlog-Zeilen |
| F19 | `f8c2017` | 10 Zeilen aus `.gitignore` nach `.git/info/exclude` |

| Messung (lokal, Release) | vorher | nachher |
|---|---:|---:|
| Tests Core / Import / App | 1 819 / 164 / 1 698 | 1 817 / 164 / 1 701 |
| davon CI (ohne lokalen Scratch-Test) | 1 818 / 164 / 1 698 | 1 816 / 164 / 1 701 |
| `Category=Slow` (Core) | 2 | 4 |
| Core-Wandzeit Default-Run | 17,6 s | 15,1 s |
| `AppLog.Write`-Stellen in `src/` (Zählung Review) | 4 | 23 |
| Build-Warnungen | 0 | 0 |

**Nebenbefund (nicht K10):** `CargoPlan.App.Tests` hängt lokal ab und zu (`--blame-hang-timeout 60s`: 4 von 10 Läufen auf
`main` `da9c49e`, also vor K10). Aktiv beim Hänger u. a. `LookSceneTests.Each_ship_view_key_changes_the_picture`,
`MaxCrateDropdownTests`, `ToolComboBoxStyleTests`. CI war bisher grün. Eigener Task, Entscheidung Martin. → B11

