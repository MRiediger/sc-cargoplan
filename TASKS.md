# TASKS – SC-CargoPlan

Regeln: **ein Task pro Session**, Reihenfolge einhalten. Jeder Task hat eine „Warum“-Zeile und prüfbare Akzeptanzkriterien. Erledigt → `[x]`.
Abgeschlossenes wandert mit dem Schließen ins Archiv: die Abschluss-Session verschiebt ihren Abschnitt (Phase, Release oder Session) wortgleich nach `docs/tasks-archive/` und setzt eine Index-Zeile unter „Geschichte (Archiv)“ (K11).

Session-Start-Prompt für Claude Code:
> Lies CLAUDE.md und TASKS.md; die Archivdatei einer Phase nur, wenn der Task darauf verweist. Wir machen Task **Tx.y**. Fass den Task zusammen, dann los.

Schiffsdaten: die Pipeline (Racks, Umrisse, Holos, Data-Release `ships-*`) lebt in [`sc-cargoplan-shipdata`](https://github.com/MRiediger/sc-cargoplan-shipdata) (D111); deren `TASKS.md` ist der Einstieg für Pipeline-Tasks. Die App-Seite ist Phase 12 ([Archiv](docs/tasks-archive/phase-12-ship-data.md)).

---

## Geschichte (Archiv)

Abgeschlossene Phasen, Releases und Sessions, wortgleich verschoben (K11). Zeitraum aus Überschriften und Commits; D = D-Nummern der Überschrift und die, die in TASKS.md in diesem Abschnitt zuerst vorkommen.

| Abschnitt | Zeitraum | D | Archiv |
|---|---|---|---|
| Phase 0 – Fundament | 16.10.2026 | D1–D4 | [phase-00-fundament.md](docs/tasks-archive/phase-00-fundament.md) |
| Phase 1 – Datenmodell (Core) | 16.–17.10.2026 | D7, D9 | [phase-01-datenmodell.md](docs/tasks-archive/phase-01-datenmodell.md) |
| Phase 2 – Verteilalgorithmus | 17.–18.10.2026 | D6, D8, D10 | [phase-02-verteilalgorithmus.md](docs/tasks-archive/phase-02-verteilalgorithmus.md) |
| Phase 3 – Overlay (WPF) | 18.–19.10.2026 | D11–D27 | [phase-03-overlay.md](docs/tasks-archive/phase-03-overlay.md) |
| Phase 4 – Settings & Konfiguration | 19.–20.10.2026 | D28–D36 | [phase-04-settings.md](docs/tasks-archive/phase-04-settings.md) |
| Phase 5 – Import aus ContractTracker V2 | 20.–21.10.2026 | D37–D42, D44 | [phase-05-import-contracttracker.md](docs/tasks-archive/phase-05-import-contracttracker.md) |
| Phase 6 – Release | 21.–22.10.2026 | D45–D52, D87 | [phase-06-release.md](docs/tasks-archive/phase-06-release.md) |
| Phase 7 – Schiffe vor Nexus | 22.–27.10.2026 | D53–D64, D66, D68–D69, D83 | [phase-07-schiffe.md](docs/tasks-archive/phase-07-schiffe.md) |
| Bugfix B1–B6 (Planregeln) | 24.–25.10.2026 | D5, D65 | [bugfix-b1-b6-plan-rules.md](docs/tasks-archive/bugfix-b1-b6-plan-rules.md) |
| Backlog-Session K1–K7 | 25.–26.10.2026 | D67, D70–D80 | [backlog-session-k1-k7.md](docs/tasks-archive/backlog-session-k1-k7.md) |
| Bugfix Sync-Gone (S1) | 29.10.2026 | D43, D81–D82 | [bugfix-sync-gone.md](docs/tasks-archive/bugfix-sync-gone.md) |
| Bugfix Sync-Net (N1) | 29.10.2026 | D82 | [bugfix-sync-net.md](docs/tasks-archive/bugfix-sync-net.md) |
| Bugfix Holo-Pump (P1) | 30.10.2026 | D76 | [bugfix-holo-pump.md](docs/tasks-archive/bugfix-holo-pump.md) |
| Phase 8 – 3D-Schiffsansicht | 30.–31.10.2026 | D83–D84 | [phase-08-3d-view.md](docs/tasks-archive/phase-08-3d-view.md) |
| Phase 9 – Release | 31.10.–02.11.2026 | D85–D86, D94 | [phase-09-release.md](docs/tasks-archive/phase-09-release.md) |
| Bugfix B7 SDK-Pinnung | 02.11.2026 | D86 | [bugfix-b7-sdk-pin.md](docs/tasks-archive/bugfix-b7-sdk-pin.md) |
| K9 Sektionen pro Ziel | 05.11.2026 | D88–D89 | [k9-sections.md](docs/tasks-archive/k9-sections.md) |
| Release v0.2.0 | 05.11.2026 | – | [release-v0.2.0.md](docs/tasks-archive/release-v0.2.0.md) |
| v0.3.0 – Feedback-Release | 06.–08.11.2026 | D90–D93, D95–D96 | [feedback-v0.3.0.md](docs/tasks-archive/feedback-v0.3.0.md) |
| Release v0.3.0 | 08.11.2026 | – | [release-v0.3.0.md](docs/tasks-archive/release-v0.3.0.md) |
| Bugfix B10 – Hotkeys im Spiel | 09.11.2026 | D97 | [bugfix-b10-hotkeys.md](docs/tasks-archive/bugfix-b10-hotkeys.md) |
| Release v0.3.1 | 09.11.2026 | – | [release-v0.3.1.md](docs/tasks-archive/release-v0.3.1.md) |
| K10 Repo-Hygiene | 09.11.2026 | – | [k10-repo-hygiene.md](docs/tasks-archive/k10-repo-hygiene.md) |
| Bugfix B11 App-Tests hängen | 09.11.2026 | D98–D99 | [bugfix-b11-app-tests-hang.md](docs/tasks-archive/bugfix-b11-app-tests-hang.md) |
| Phase 10 – OCR (v0.4.0) | 09.–13.11.2026 | D100–D108 | [phase-10-ocr.md](docs/tasks-archive/phase-10-ocr.md) |
| Release v0.4.0 | 12.–19.11.2026 | – | [release-v0.4.0.md](docs/tasks-archive/release-v0.4.0.md) |
| Phase 11 – nach dem Launch (v0.5.0) | 13.–15.11.2026 | D109 | [phase-11-after-launch.md](docs/tasks-archive/phase-11-after-launch.md) |
| Release v0.5.1 | 16.11.2026 | D110 | [release-v0.5.1.md](docs/tasks-archive/release-v0.5.1.md) |
| Phase 12 – Ship data channel | 16.–19.11.2026 | D111–D114 | [phase-12-ship-data.md](docs/tasks-archive/phase-12-ship-data.md) |
| K12 Doku-Abschluss, Backlog II, F11 | 19.11.2026 | D115 | [k12-docs-backlog-ii.md](docs/tasks-archive/k12-docs-backlog-ii.md) |

---

## Backlog II (19.11.2026)

Aus der Backlog-Session II (Feedback-Chats nach v0.6.0). Reihenfolge (Martin): **K11 → B16 → B17 → F10**; F11 lief mit
K12. **F8** bleibt zurückgestellt (Zeile in „v0.3.0 – Feedback-Release“ unverändert). Ein Task pro Session, je mit Plan
und Freigabe.

### [x] K11 TASKS.md-Archivierung (19.11.2026)

**Warum:** TASKS.md hat 832 KB / 7 756 Zeilen (19.11.); jede CC-Session liest sie, der Kontext ist der Preis.
- Abgeschlossene Phasen und Sessions nach `docs/tasks-archive/`, eine Datei pro Phase oder Release.
- TASKS.md behält Regeln, offene Tasks, Lehren, Backlog und je Phase eine Zeile mit Verweis aufs Archiv. Die Lehren
  bleiben vollständig in TASKS.md.

**Akzeptanz:** TASKS.md < 100 KB; kein Inhalt verloren (`git diff --stat` plus Stichprobe D-Verweise); die Verweise von
CLAUDE.md auf TASKS-Abschnitte stimmen noch.

**Plan (19.11.2026, Branch `chore/k11-tasks-archive` ab `a735ba6`):**

Messung (nur gemessen, nicht gelesen): 859 128 B in der Arbeitskopie (CRLF), 8 063 Zeilen, Blob 851 065 B; 33
`##`-Abschnitte, davon 30 abgeschlossen (≈ 826 KB, 180 `### [x]`); bleiben: Backlog II (2,9 KB, darin F11 `[x]`),
Lehren (5,8 KB), Backlog (24,1 KB). `### [x]` = 181, `### [ ]` = 4; acht historische `- [ ]`-Zeilen in abgeschlossenen
Tasks (1593, 1623, 2950, 3153, 4686, 4790, 5002, 5004).

1. **Verschieben** per Skript im Scratchpad (`git show a735ba6:TASKS.md`, Schnitt an den `## `-Zeilen, nichts von Hand):
   eine Datei je abgeschlossenem `##`-Abschnitt (30) unter `docs/tasks-archive/`, Kopf `# Archiv: <Überschrift>` plus
   Herkunftszeile, dann der Abschnitt Byte für Byte. TASKS.md behält Kopf, Index `## Geschichte (Archiv)` (je Abschnitt
   eine Tabellenzeile: Abschnitt, Zeitraum, D-Nummern, Link), Backlog II, Lehren, Backlog. Zeitraum aus den Datumsangaben
   der Überschriften (sonst `git log` der Task-Präfixe), D = im Abschnitt zuerst erwähnte D-Nummern; jede Zeile
   gegengelesen.
   - **K1:** im Session-Start-Prompt ändert sich nur der Lese-Satz („Lies CLAUDE.md und TASKS.md; die Archivdatei einer
     Phase nur, wenn der Task darauf verweist.“), der Rest bleibt wortgleich. Zeile 6 enthält heute keine Folge
     Plan → Commit → Push → Stopp → Freigabe; K11 fügt keine hinzu.
   - **E1:** neue Kopfzeile „Ein Abschnitt wandert mit dem Schließen ins Archiv und bekommt eine Index-Zeile; das ist
     Teil der Abschluss-Session“, dieselbe Regel als neue Lehre (Anlass K11).
   - Kopfnotiz Shipdata (neu): Pipeline in `sc-cargoplan-shipdata` (D111), deren `TASKS.md` ist der Einstieg, App-Seite
     Phase 12 → Archivdatei.
2. **Backlog:** Punkte, die mit `- ~~` beginnen, samt eingerückten Folgezeilen → `docs/tasks-archive/backlog-done.md`
   (`## Umzug am 19.11.2026 (K11)`); teilweise erledigte Zeilen ohne führendes `~~` bleiben.
3. **Verweise:** Grep `TASKS` in CLAUDE.md, `docs/`, `.github/`, `src/`, `tests/`, Wiki (heute 43 Treffer in 10
   Dateien, Wiki 0). Ort-Verweise auf Verschobenes → Archivpfad (`docs/decisions.md` ≈ 22, `docs/ship-format.md:115`,
   Kommentare in `LoadPlannerTests.cs:25`, `ScanRegionTests.cs:5`); Ziele im Backlog und Prozess-Verweise bleiben.
   CLAUDE.md: eine Zeile `tasks-archive/` im Strukturbaum.
4. Befund, `[x]`, Merge `--no-ff` nach Abnahme, Martin pusht.

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:**
- P1 eine Datei je `##`-Abschnitt (30), auch Bugfix- und K-Sessions.
- P2 Zeile 3874: zwei relative Links nach `src/…` bekommen `../../` vor das Ziel, Linktext gleich – einzige Änderung im
  verschobenen Text; 7709 (zitierter README-Vorschlag) und 1246–1251 (Wiki-Syntaxbeispiele) bleiben.
- P3 `docs/review/2026-11-pre-p10.md` bleibt unverändert (Schnappschuss mit alten Zeilennummern).
- P4 Einstieg Shipdata als neue Kopfnotiz; das Shipdata-Repo bleibt unberührt, sein Verweis „Abschnitt Phase 12“ trifft
  die Index-Zeile.
- P5 zwei `.cs`-Kommentare → einmal `dotnet build`, kein `dotnet test`.
- P6 zusätzlich Block-Gleichheit je Archivdatei gegen den alten Text.
- P7 `### [ ]` = 4 nach dem Verschieben, 3 nach K11 `[x]`; `### [x]` 181 bzw. 182.
- K1, E1 (Freigabe): siehe oben.

**Verifikation:** Block-Gleichheit je Archivdatei; Vereinigung als Multimenge (jede alte Zeile mindestens so oft wie
vorher, erlaubt fehlen nur alte Zeile 6 und 3874), Zahl der verglichenen Zeilen und Liste aller neuen Zeilen; Zähler
`### [x]`/`### [ ]`; Stichprobe 10 D-Verweise (221, 629, 670, 863, 864, 1009, 1015, 1134, 1142, 1202/1303); Grep auf
tote Verweise; Größe vorher/nachher; `dotnet build`; `git status` sauber, keine Binärdatei (A17).

**Freigabe 19.11.2026** mit K1 und E1; P1–P7 ratifiziert. Die historischen `- [ ]` 4686 und 4790 werden wortgleich
archiviert, Martins Antwort folgt.

**Befund (19.11.2026):**
- **Größe TASKS.md:** vorher 859 128 B / 8 063 Zeilen (Blob 851 065 B), nachher 36 235 B / 333 Zeilen (Blob
  35 902 B). Archiv: 31 Dateien (30 Abschnitte + `backlog-done.md`), 831 887 B Blob.
- **Block-Gleichheit:** 30 Archivdateien = 7 806 alte Zeilen, Byte für Byte (einzige Ausnahme Zeile 3874, P2).
- **Vereinigung:** 8 061 von 8 063 alten Zeilen verglichen (erlaubt fehlen 6 und 3874), **fehlend 0**; neue Zeilen nur
  Kopf, Index, Plan, Befund, Archivköpfe, Lehre, Umzugsüberschrift.
- **Zähler:** `### [x]` 181 → 181, `### [ ]` 4 → 4 nach dem Verschieben; mit diesem Haken 182 / 3.
- **Backlog:** 34 erledigte Punkte (48 Zeilen) → `backlog-done.md`, 59 offene bleiben. Darunter der Punkt „Erster
  Selbsttausch übers Netz“ mit dem Satz „SmartScreen für `Update.exe` ist weiter unbelegt“ – passt zum offenen Punkt
  Setup.exe/Code-Signing, steht jetzt nur im Archiv.
- **Verweise:** 25 umgehängt – `docs/decisions.md` 21 (Plan: ≈ 22), `docs/ship-format.md` 1, `docs/nexus/staff-request.md`
  1, Kommentare in `LoadPlannerTests.cs` (jetzt „Cases from T2.1 …“) und `ScanRegionTests.cs`. **Abweichung vom Plan:**
  `staff-request.md:12` stand im Plan unter „Prozess, unverändert“, zeigt aber auf T9.3 (archiviert) → umgehängt.
  Grep danach: 22 `TASKS`-Treffer außerhalb TASKS.md und Archiv, alle zugeordnet – Prozess 9 (CLAUDE.md 18, 68, 179, 184,
  191; decisions 1353; holo-format 151 = Shipdata-TASKS; nexus/checklist 14, 115), neue Archivzeile CLAUDE.md 1,
  Backlog 5 (decisions 115, 1072, 1164; SHOTLIST 11; review 267), Lehren 1 (decisions 1015), frühere Bearbeitung 2
  (decisions 178, 385), Review-Schnappschuss 4 (P3). Wiki: 0. Alle 31 genannten Archivpfade existieren.
- **Stichprobe D-Verweise (12):** decisions 221 → T3.12, 629 und 670 → T7.1c, 863 → „32 Racks in 16 Schiffen“, 864 →
  K3.1, 1009 → S8.0, 1122 → „Erste Datenkorrektur“, 1134 → „Erlaubnis Motherlode“, 1142 → „Rundungsprotokoll“, 1198 → B11,
  1202 → S10.0, 1303 → S11.0: jedes Ziel genau in der genannten Archivdatei gefunden.
- **Build (P5):** `dotnet build` – 0 Warnung(en), 0 Fehler (8,52 s). Kein `dotnet test` (Docs und zwei Kommentare).
- **A17:** `git diff --numstat a735ba6` ohne Binärdatei; `git status` sauber.
- Index-Zeitraum: Überschriften plus Commits der eindeutigen Task-IDs, alle 30 Zeilen gegengelesen.
- Am Rande: `gh` fehlt im PATH dieser Maschine (Eintrag `C:\Program Files\GitHub CLI"` mit verirrtem Anführungszeichen);
  aufgerufen über den vollen Pfad.

### [x] B16 Legende: Missionsreihenfolge (19.11.2026)

**Warum:** Die Missionen (`#`) erscheinen in der Legende nicht chronologisch (z. B. #1, #3, #2); am Aufzug sucht der
Hauler die zuletzt gescannte Mission unten.
- Soll: neue Missionen werden unten angehängt (Append), die bestehende Reihenfolge bleibt bei Scan und Plan stabil.

**Akzeptanz:** Test mit drei Scans nacheinander, Legende zeigt #1, #2, #3; ein Re-plan ändert die Reihenfolge nicht.

**Befund vor dem Plan (19.11.2026, Branch `fix/b16-legend-order` ab `7b6d90a`):**
- **Fehlerstelle:** `src/CargoPlan.Core/Models/LoadPlan.cs:54` – `LegendMissions` sortiert nach dem frühesten Ziel in
  `Route` (`.OrderBy(m => m.Min(c => rank[c.Destination]))`), die Missionsliste nur als Tie-Break (`:55`). Das ist die
  D69-Regel wörtlich, K9.2 zog sie unverändert nach Core. Keine SCU/Name-Sortierung im Legendenpfad (die B2-Tie-Breaks
  gelten nur für Planer-Suche und Fallback, `LoadPlanner.cs:47`). **Warum #3 vor #2:** `Route` = Ziele nach erstem
  Crate, `PlaceInto` legt neue Kisten direkt hinter die ihres Ziels (D64-Addendum). Scan #1 → A, #2 → B, #3 → A: Rang
  0 / 1 / 0, Tie #1/#3 per Liste → #1, #3, #2.
- **Leser der Legendenfolge** (nur über `LegendMissions`/`LegendDestinations`): `ShipViewBuilder.LegendLines`
  (`ShipViewBuilder.cs:96`; Zeilen, Kopf „From …“, `#`, Kopf-Checkbox `MissionCrates` hängen daran) und
  `LoadPlanner.Sectioned` (`LoadPlanner.cs:97` → `RackSections.Lay`, Sektionsfolge D89 bei Editor-Plan, `PlanKeeping`,
  Re-plan). Wartende Missionen (`ShipViewBuilder.cs:56`) in Listenfolge. **Nicht betroffen:** `MissionPalette.Assign`
  (`Placements` = `Route`, Farben bleiben), `LoadPlanner.Plan(ship, stops)` (`:29`, CLI, `Route`), Editor K6.3 (nur
  Ansicht, Plan liest `Rows`), `PlanTextRenderer`, Missions-Export.
- **Persistenz:** `missions[]` wird in Listenfolge geschrieben und gelesen (`PlanFileDto.cs:108`, `:44–55`);
  `PlaceInto` hängt an, Re-Scan (`ReplaceMission`) behält den Platz, `Replan`, `RemoveMission`, `RenameStop`,
  Split/Merge, Undo (`PlanHistory` hält ganze `SavedPlan`) behalten die Folge. **Aber** der Editor-Plan baut die Liste
  neu: `MissionRows.FromMissions` gruppiert nach Ziel in `Route`-Folge (`MissionRows.cs:111–116`), `ToMissions` gibt
  Missionen in Folge der ersten Zeile je Gruppe zurück und nummeriert getippte neu (`:138–167`). `missions[]` allein
  kann die Quelle also nicht sein. Stabil ist `ContractSource.Scan.No`: nicht nullable, je Plan eindeutig
  (`SavedPlan`-Konstruktor), nie wiederverwendet (`LastScanNo` außerhalb der Undo-Historie, `OverlayViewModel.cs:582`),
  steht schon in plan.json (Schema 10) → **kein Schema-Bump, kein neues Feld.**
- **Goldens:** `LegendOrderTests` (4) bleiben unter der neuen Regel grün (nachgerechnet). RAFT-Fixture: 4 getippte
  Missionen, Legende heute #1, #2, #4, #3, neu #1, #2, #3, #4; `LegendDestinations` bleibt Green, High, Thunder,
  Faithful, Melodic → K9-Goldens voraussichtlich 0 kippen (Zählung im Rot-Lauf).

**Plan (19.11.2026):**

Regel: zuerst Missionen mit Vertrag nach `Scan.No` aufsteigend, dahinter Missionen ohne Vertrag in Listenfolge;
Missionen, die die Liste nicht kennt (Plan ohne Missionen), danach wie heute nach Route-Rang. Zeilen innerhalb einer
Mission bleiben in Route-Folge.

1. `SavedPlan.LegendOrder` (Ids, abgeleitet bei jedem Lesen wie `Stops`): eine Stelle für die Regel.
2. `LoadPlan.LegendMissions(missionOrder)`: primär Position in `missionOrder`, nicht gelistete dahinter nach Route-Rang.
3. `LoadPlanner.Sectioned` und `ShipViewBuilder` (Legende und wartende Missionen) übergeben `saved.LegendOrder`.
4. Docs: D69-Addendum (B16) mit Gegenargument, Verweis in D89; Wiki-Satz `Legend-and-delivery-states.md:36`.

Gegenargument: Listen- und Legendenfolge laufen nach einem Editor-Plan auseinander; Editor-Hoch/Runter (K6.3) verschiebt
gescannte Missionen in der Legende nicht mehr. Die Alternative („`ToMissions` behält die bekannte Folge“) griffe in
Editor, D42-Neunummerierung und K6.3 ein – verworfen. Spielerreihenfolge bleibt Backlog (Legenden-Drag).

Tests (erst rot): R1 Repro (Scans #1 → A, #2 → B, #3 → A → #1, #2, #3; heute #1, #3, #2); stabil nach Re-plan,
Abhaken #2, Plan-Datei schreiben/lesen, Editor-artigem Plan; #2 löschen → #1, #3, neuer Scan → #1, #3, #4; getippte
Mission zwischen zwei Scans steht dahinter; Sektionsfolge = Legendenfolge bei Scan-Folge ≠ Route, RAFT-Goldens benannt;
App: drei `ApplyScan` → Legende #1, #2, #3, unverändert nach Re-plan, Abhaken, Undo, Redo, Editor-Plan.

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:**
- P1 Quelle = `Scan.No`, nicht die Array-Folge von `missions[]` (der Editor-Plan baut die Liste um).
- P2 Wartende Missionen in der Legende folgen derselben Ordnung.
- P3 Wiki-Satz wird mitgezogen.
- P4 Zeilen innerhalb einer Mission bleiben in Route-Folge.
- K1 Wiki-Satz auf lokalem Branch `b16-legend-order` im Wiki-Repo, Push erst mit dem nächsten Tag; Backlog-Zeile wie K12.

**Freigabe 19.11.2026:** P1–P4 ratifiziert, F1 bestätigt (Sektionen folgen der Legende), K1. Rot-Lauf zuerst, R1 muss
rot sein.

**Befund (19.11.2026, `9f396a3`):**
- **Rot-Lauf, Stufe 1 (App, alter Code, Core-Testdatei geparkt):** `LegendScanOrderTests` 2 von 2 rot per Assert,
  R1 „Expected … {1, 2, 3}, but {1, 3, 2} differs at index 1“ – der gemeldete Fall.
- **Rot-Lauf, Stufe 2 (Core, `LegendOrder` als Stub = heutige Listenfolge):** 5 von 45 rot per Assert (R1 `{1, 3, 2}`,
  getippte Mission `{1, 0, 2}`, Sektionen `{A, C, B}`, Re-plan `{1, 3, 2}`, Rang-Fallback `{3, 1, 2}`); der Lösch-Test
  war schon vorher grün (wie geplant). **Bestehende Tests, die kippten: Core 0** (alle K9-Goldens in `RackSectionsTests`
  unverändert grün), **App 1:** `ShipViewBuilderTests.The_legend_keeps_the_order_of_entry_and_a_stop_keeps_its_colour`
  hielt die B6-Folge #1, #3, #2 als Soll („the new mission's line stands next to the delivered one“) – Erwartung auf
  #1, #2, #3 umgestellt, der Farbteil (Riker behält seine Farbe) unverändert.
- **P2-Test** (wartende Missionen, Liste ≠ Scan-Folge) rot gegen den alten `ShipViewBuilder` (`{1, 2}` statt `{2, 1}`).
  Im selben Lauf: mit alter Übergabe der Listenfolge kippte die Legende erst **nach dem Editor-Plan** auf {1, 3, 2} –
  P1 am echten Ablauf belegt.
- **Änderung:** `SavedPlan.LegendOrder` (neu, eine Stelle für die Regel), `LoadPlan.LegendMissions` (Ordnung des
  Aufrufers zuerst, Rang-Fallback dahinter), `LoadPlanner.Sectioned` und `ShipViewBuilder` (Legende, wartende) lesen
  `LegendOrder`. Diff: 4 Quelldateien, +23/−13; Tests 2 Dateien geändert, 1 neu (+171/−3). Kein Schema-Bump (Plan-Schema 10).
- **Docs:** D69-Addendum (B16, mit Gegenargument), D89-Addendum. Wiki: Satz in „Legend and delivery states“ auf lokalem
  Wiki-Branch `b16-legend-order` (`0386c22`, ab `master`), Backlog-Zeile für den Push mit dem nächsten Tag (K1).
- **Testzahlen aus sauberen Worktrees:** vorher (`7b6d90a`) Core 1913, App 2079, Import 280, Ocr 34; nachher
  (`9f396a3`) Core **1919** (+6), App **2082** (+3), Import 280, Ocr 34, alle grün; `dotnet build` 0 Warnungen,
  `dotnet format --verify-no-changes` sauber. Die Arbeitskopie zählt 3 mehr (gitignorte `Scratch*`-Tests, Lehre T11.3).
- **Gate Martin am Aufzug (SC vorn):** drei Verträge scannen → #1, #2, #3 von oben; Re-plan → unverändert;
  App-Neustart → unverändert. **Abgenommen 19.11.2026** („abgenommen, passt“). Merge `--no-ff`, Martin pusht.

### [ ] B17 Sektionen: Lücke bei Rest-Einzelkiste

**Warum:** ARGO RAFT, `grouping: byDestination`, Kistenmix 2+1 (auch 4+1): der Planer legt die 1-SCU-Kiste neben die
Sektion mit einer leeren Zelle dazwischen oder in eine eigene Reihe – die Sektion ist kein Block mehr.
- Soll: die Einzelkiste rückt in die Lücke neben die 2er-Kiste, jede Sektion ist ein geschlossener Block ohne leere
  Zelle. Nur Core (`RackSections`, D89-Addendum).

**Akzeptanz:** Golden mit Martins RAFT-Plan (Scan #4 Everus Harbor, 10 SCU an 4 Ziele) ohne Loch; die bestehenden
K9-Goldens unverändert oder begründet.

**Befund vor dem Plan (19.11.2026, Branch `fix/b17-section-hole` ab `e5400be`):**
- **Szene synthetisch** (Martins `plan.json` mit dem Stims-Scan gibt es nicht mehr): RAFT aus `FrozenShips`, ein
  Vertrag „Stims“ ab Everus Harbor, Green Glade 3 (max 2 → 2+1), High Course 3 (2+1), Thundering Express 2, Melodic
  Fields 2 = 10 SCU, vier Ziele. Harness lokal (`ScratchSectionHoleProbe.cs`, `.git/info/exclude`, Lehre T11.3):
  504 Fälle = Schiff (RAFT, Perseus, Hull A) × Mix (2+1, 4+1, 4+2+1) × Weg × `fillRows` × `sectionGap` 0/1/2 ×
  `fillOrder`. Wege: frischer Plan, Scan in leeren Plan erfasst (`PlaceInto`), derselbe Scan zweimal erfasst, Green
  geladen + Re-plan (D41), auf der RAFT zusätzlich der Scan als 4. Vertrag in Martins Fixture
  (`Fixtures/Plans/raft_hand_2026-11-05.json` ohne Mission 3) – erfasst und frisch geplant. Detektor: leere Zelle einer
  Ebene, deren nächste belegte Zelle entlang der Reihe auf beiden Seiten demselben Ziel gehört.
- **Martins Bild ist das Loch beim Erfassen** (Bild B, von Martin per ASCII bestätigt): Scan in den Fixture-Plan,
  crosswise, Bug zuerst, Gap 1, High Course (Fixture 4+2+1, Scan 2+1), L7/L8:
  ```
          L7 L8                       Soll (Martin)
    w5     .  H1   <- Scan                 .  .
    w4    H2  H2   <- Scan                H1 H1
    w3     .  H1   <- L7/w3 leer          H2 H2
    w2    H2  H2                          H2 H2
    w1    H4  H4                          H4 H4
    w0    H4  H4                          H4 H4
  ```
  Das **L im frischen Plan** (2+1 crosswise: 2er längs über zwei Reihen, 1er daneben, eine Zelle frei) ist **kein
  Fehler** (Martin); Drehen bleibt D71 (nur wenn ungedreht scheitert). Kandidat (a) aus dem Review ist damit verworfen.
- **Ursache:** Ein Scan zu einem Ziel, das im geteilten Rack schon liegt, geht `ScanSync.Apply`
  (`ScanSync.cs:26`) → `LoadPlanner.PlaceInto` → `LayStop.InSection` (`LoadPlanner.cs:1107-1138`): `RowsFor` gibt die
  eigenen Reihen (`RackSections.OwnRows`, `RackSections.cs:96-99`, `:128-150`), und `Laid` (`LoadPlanner.cs:1143`)
  nimmt **alle** Kisten im Rack als Hindernis, auch die noch nicht geladenen desselben Ziels (D41: „a capture moves
  nothing“, K9.2-Addendum `docs/decisions.md:492`). Die neuen Kisten werden also ans alte L angehängt:
  `CrateLayout.FirstFit` (`CrateLayout.cs:746`) findet für die 2er auf w3 keinen Platz (L8/w3 hat die alte 1er), legt
  sie auf w4; die 1er geht nach Füllrang auf L8/w5 (crosswise läuft der Rang quer über die Breite einer Längsreihe,
  `CrateLayout.cs:714-722`) statt auf L7/w3 – die Zelle ist eingeschlossen. `RackSections.Lay` selbst (frischer Plan,
  Re-plan) erzeugt kein Loch; eine 1er-Lieferung desselben Ziels ist auch keine eigene Sektion (`GroupBy` nach
  `Destination`, `RackSections.cs:58`).
- **Reichweite** (Fälle mit Loch / Fälle je 3 Gaps × 2 Füllrichtungen):

  | Schiff | Weg | 2+1 cross | 2+1 längs | 4+1 cross | 4+1 längs | 4+2+1 cross | 4+2+1 längs |
  |---|---|---|---|---|---|---|---|
  | RAFT (−z) | Scan in Fixture-Plan | 6/6 | 0/6 | 6/6 | 6/6 | 6/6 | 6/6 |
  | RAFT | Scan zweimal | 6/6 | 0/6 | 6/6 | 6/6 | 6/6 | 6/6 |
  | Perseus (+z) | Scan zweimal | 2/6 | 0/6 | 6/6 | 6/6 | 6/6 | 0/6 |
  | Hull A (±y) | Scan zweimal | 0/6 | 0/6 | 0/6 | 0/6 | 0/6 | 0/6 |
  | alle | frischer Plan, Scan in leeren Plan, Geladen + Re-plan, Fixture frisch geplant | 0 | 0 | 0 | 0 | 0 | 0 |

  Gap 0 ist so betroffen wie 1 und 2. Hull A: ein Ziel je Rack, keine geteilten Racks, also keine Sektionen.

**Plan (19.11.2026):**

Regel (Martin): **Eine Sektion hat keine leere Zelle zwischen Kisten derselben Sektion, weder entlang der Reihe noch
quer dazu.** Beim Erfassen wird die Sektion neu gelegt, aber nur mit den Kisten, die weder geladen noch abgeliefert
sind; diese bleiben exakt liegen (D41/D79, einziges Bewegen an Geladenem bleibt das Absinken aus K6.4). Lässt sich die
Sektion um feste Kisten nicht lückenlos schließen, bleibt die Lücke und bekommt einen Hinweis; es wird nichts
verschoben. In den neu gelegten Kisten: größte zuerst, Einzelkisten füllen freie Zellen der Sektion, bevor eine neue
Reihe beginnt.

1. **Erfassen legt die Sektion neu** (`LoadPlanner.LayStop.InSection`): Liegt das Ziel schon im geteilten Rack, legt der
   Planer die geplanten Kisten dieses Ziels in diesem Rack zusammen mit den neuen in dessen Reihen (`RowsFor`-Stufen
   unverändert). Hindernis sind nur fremde Kisten und die geladenen des Ziels. Die alten Kisten bekommen neue
   Positionen in der Kistenliste, die neuen gehen wie heute hinter die ihres Ziels (`Insert`). Findet die Neulage keinen
   Platz, gilt der heutige Weg (anhängen) unverändert. Andere Ziele, andere Racks, wer wartet: unverändert (F5).
2. **Einzelkisten kompakt** (`CrateLayout.RackSpace.Add`, nur auf Wunsch der Sektionen, also `RackSections.Laid` und
   `LayStop.InSection`): Eine 1er nimmt in der untersten Ebene mit freiem, getragenem Platz (Schicht für Schicht wie
   D24) die Zelle mit den meisten Flächennachbarn derselben Sektion; Gleichstand nach heutigem Füllrang. Das ergibt
   Martins Soll (zweite 1er auf L7/w4: zwei Nachbarn gegen einen auf L8/w5) und lässt das L des frischen Plans stehen
   (1er auf L11/w1, Gleichstand, Füllrang wie heute). Pläne ohne Sektionen bleiben byte-gleich.
3. **Hinweis bei Restlücke:** neuer Satz in `SectionNotes` („Rack N: X has a gap – loaded crates“), gesetzt, wenn nach
   der Neulage eine eingeschlossene Zelle bleibt und das Ziel geladene Kisten im Rack hat. Die App zeigt ihn über die
   vorhandene `NoticeSource.Sections` (`OverlayViewModel.cs:320`); `Names`/`IsNote` lesen ihn, Lebensdauer wie die
   anderen Sektionshinweise. Keine UI-Änderung.
4. **Docs:** D41-Addendum (B17: In einem Plan mit Sektionen legt ein Scan die geplanten Kisten seines Ziels im Rack neu;
   Geladenes und Abgeliefertes bleibt), D89-Addendum (Blockregel, Einzelkisten kompakt, Hinweis), D71 ausdrücklich
   unverändert. Kein Schema-Bump, kein Format.

**Gegenargument (A5):** D41 versprach, dass ein Scan keine Kiste bewegt. Hat der Hauler Kisten schon ins Schiff gestellt,
aber nicht als geladen abgehakt, stimmt das Overlay nach dem Scan nicht mehr mit dem Laderaum überein. Martins Regel
nimmt das in Kauf: Geplant heißt „noch nicht im Schiff“, und der Haken „geladen“ ist der Schutz. Die Alternative
(anhängen, dann nur die neuen Kisten in die Lücken der alten schieben) schließt Bild B nicht, weil die alte 1er auf
L8/w3 liegen bleibt und die 2er dort keinen Platz hat.

**Tests (erst rot):**
- **R1 Bild B:** Fixture ohne Mission 3, crosswise, Bug zuerst, Gap 1, Scan erfasst. Vorher: Assert auf die leere
  L7/w3 zwischen H2 und H2 (rot); nachher High Course genau wie Martins Soll (H4 w0–1, H2 w2, H2 w3, H1 L8/w4,
  H1 L7/w4), keine eingeschlossene Zelle in keiner Sektion, fremde Kisten auf denselben Zellen wie vor dem Scan.
- **R2 Geladen:** wie R1, aber Mission 1 (High Course 4+2+1) als geladen markiert: deren Kisten liegen exakt wie vorher,
  die neuen um sie herum; bleibt eine Lücke, steht der Hinweis da, sonst nicht.
- **R3 Matrix:** 2+1, 4+1, 4+2+1 × crosswise/lengthwise × Gap 0/1/2 × Bug/Heck auf RAFT und Perseus, Wege frischer
  Plan, Scan in leeren Plan, Scan zweimal, Scan in Fixture: keine eingeschlossene Zelle; frischer Plan der Szene auf
  der RAFT: Abstand zwischen den Sektionen genau `sectionGap`.
- **R4 Eigenschaftstest:** zufällige Mixe (Seed fest, 200 Fälle, 1–6 Ziele, 1–12 SCU, max 1/2/4) auf der RAFT, frisch
  geplant und zweimal erfasst: keine eingeschlossene Zelle in einer Sektion ohne geladene Kisten. Findet er Löcher, die
  nicht von Einzelkisten kommen (etwa nur 2er), stoppe ich und melde das, bevor die Regel breiter wird.
- **R5 F5:** Rack-Zuordnung und wartende Missionen nach einem Scan wie ohne Neulage (`RackSectionsSyncTests` bleiben
  grün); keine geladene oder abgelieferte Kiste bewegt sich.
- **Bestand:** `RackSectionsTests`, `SectionGapTests`, `RackSectionsSyncTests`, `HullAGoldenTests`, Plan-Goldens unter
  `Fixtures/Plans/golden_*`: unverändert grün. Im Rot-Lauf zähle ich, welche Zellen von Einzelkisten sich in den
  RAFT-Fixture-Plänen verschieben (die Goldens prüfen Reihen, nicht Zellen); kippt ein Assert, ist das ein Befund und
  kein Grund zum Neuziehen (F4).

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:**
- P1 Szene synthetisch im Test gebaut (kein `plan.json` mehr); Fixture-Ordner ist `Fixtures/Plans/` (Startprompt:
  `plans/`), dort liegt Martins RAFT-Plan schon.
- P2 Hull A stapelt ±y, nicht +z (Startprompt); +z-Gegenprobe ist die Perseus (ein Rack, 6 × 8 × 2).
- P3 Das L im frischen Plan bleibt (Martin); D71 unverändert, Kandidat (a) verworfen.
- P4 Fehlerort ist der Erfassungsweg, nicht `RackSections.Lay`; F1 hält trotzdem (nur Core), die Neulage sitzt in
  `LoadPlanner.LayStop`, die Kompaktregel in `CrateLayout`. Dazu ein D41-Addendum (Martins Soll).
- P5 F3: Die Neulage ist der erste Versuch in `InSection` vor den unveränderten Stufen; schlägt sie fehl, läuft alles
  wie heute.
- P6 Hinweis über `SectionNotes` und die vorhandene `NoticeSource.Sections`, kein neuer Schalter, keine UI.
- P7 F4: Die Prämisse „K9-Goldens haben keine Rest-Einzelkiste“ stimmt nicht (RAFT-Fixture: Green/High/Thundering
  7 SCU max 4 = 4+2+1). Ihre Asserts prüfen Reihen und bleiben voraussichtlich grün, Zellen von Einzelkisten dürfen
  sich innerhalb der Reihen verschieben; gezählt im Rot-Lauf.

**Gate Martin am Aufzug (SC vorn, RAFT):** Plan mit Sektionen, crosswise, Gap 1; einen Vertrag scannen, dann einen
zweiten mit 2+1 zu einem Ziel, das schon im Rack liegt → die Farbe bleibt ein geschlossener Block, die Einzelkisten
liegen nebeneinander. Den Gap zwischen den Blöcken siehst du weiter. Danach eine Lieferung als geladen abhaken und
erneut scannen → die geladenen Kisten bleiben liegen.

**Freigabe 19.11.2026:** P1–P7 ratifiziert. **E1:** Bei Gleichstand der meisten Nachbarn nimmt die Einzelkiste die in
der Füllreihenfolge erste Zelle, festgenagelt per Test. **K9:** Bleiben die Goldens grün, gut; ändert sich ein
Zellbild, kommt die Begründung in den Befund (Reihen-Asserts allein belegen nichts). Vergleich auf Zellebene: Schnappschuss
aller K9-Fixture-Pläne mit Sektionen (RAFT-Plan + neun Paare aus `RackSectionsTests.Fixtures`, je Reihen, Richtung,
Gap 0–2, Plan und Re-plan; 3768 Kistenzeilen) vor und nach der Änderung. Tests rot zuerst, dann bauen.

### [ ] F10 Seitenweise Planung

**Warum:** Auf Schiffen mit Racks nebeneinander (Hull B: Spindel links/rechts) wollen Hauler erst eine Seite auffüllen,
dann die andere, statt Missionen nach Ziel und Kapazität über beide Seiten zu verteilen.
- Neue Layout-Option neben `fillOrder`/`fillRows`/`grouping` im Editor-Abschnitt „Layout“; neues Plan-Feld,
  plan.json-Schema-Bump (heute 10), Missions-JSON (heute 4) mitziehen.
- A1: Vorläufer ist die Backlog-Zeile „Zweiseitige Sektionen“ (→ F10).

**Akzeptanz:** Hull-B-Fixture (`FrozenShips`, A10); das Default-Verhalten ist byteidentisch zu heute.

### [x] F11 Linux: CLI statt Overlay (19.11.2026, in K12)

**Warum:** Anfrage nach Linux. Ein Overlay-Port wäre ein zweites Projekt (WPF + Win32); die CLI läuft ohne Windows.
Umgesetzt in **K12** (D115, `linux-x64`-Tarball, README-Zeile), Plan und Befunde dort.

**Akzeptanz:** siehe K12 (Tarball lokal, `--version` unter WSL, `ci.yml` grün).

---

## Lehren

Regeln aus Fehlern, die durchgerutscht sind. Gelten für jeden Task.

- **Vor jedem Push `gh run list -L 3`.** Rote Läufe erst klären, dann pushen. Anlass T7.1d: Die CI war seit T7.0 rot (1-s-Zeit-Assert in `OrderSearchLimitTests`, 1.099 / 1.297 ms auf den Runnern), unbemerkt über mehrere Pushes.
- **Tests vor der Implementierung heißt: erst rot sehen.** Neue Tests vor dem Code laufen lassen und das Scheitern (Compile- oder Assert-Fehler) festhalten, dann implementieren. Anlass T7.2: Die Core-Tests (Replan, PlanFile, Katalog) wurden vor dem Code geschrieben, aber erst danach ausgeführt; in der App lief es richtig.
- **Neues Darstellungselement → Renderarchitektur neu entscheiden**, nicht das Element in die bestehende einpassen. Zwei Renderer für ein Bild sind ein Provisorium, kein Entwurf. Anlass Phase 8: das Holo kam in T7.4c als `Viewport3D`-Kulisse hinter den 2D-Painter; zwei Kameras mussten per Code gleich gehalten und gegeneinander gemessen werden (Holo-Pump Teil 2–5), und die Abhilfe gegen das Pumpen, die Perspektive (S8.0), hätte beide Zeichner getroffen. Umbau in Phase 8 (D83). Ergebnis (T8.7): eine Szene, eine Kamera, ein Hit-Test auf derselben `CameraMath`; im Spiel kein Pumpen auf fünf Schiffen, ≈ 960 Zeilen Painter-Code gelöscht.
- **Laufzeit messen wie Tests laufen lassen: fest geregelt, vorher und nachher.** Drei Läufe je Ziel, gewertet der Median; gegen das leere Fenster desselben Builds; der Anteil später Frames statt p95, weil p95 auf den Bildtakt quantisiert ist. Anlass Phase 8: einzelne Läufe streuten mehr als die Ziele sich unterschieden (`empty` 2,5–14,2 % spät), und ein p95 sprang zwischen 16,9 und 33,1 ms, ohne dass sich etwas geändert hatte. **Ab jetzt immer über dem Spiel messen** (`--over-game`, SC vorn): die Basislinie ohne SC misst den Desktop-Compositor (T8.7: `empty` 13,1 % spät ohne SC, 0,1 % mit SC). Gate-Regel für künftige Phasen: gleiches Ziel vorher/nachher über dem Spiel, nicht relativ zu `empty`.
- **Bestehende Tests sind beim Umbau das Orakel, und jeder gestrichene Test braucht einen benannten Nachfolger.** Nachfolger zuerst schreiben, grün gegen den alten Code, dann löschen; jeden einmal per Mutation rot sehen. Anlass T8.6: die Nachfolger-Tabelle fand drei Pfade, die nur der alte Code getestet hatte (Bow-Label in 3D, Fit-Regeln, Teile der Navigation).
- **Darstellung am gerenderten Bild prüfen, nicht an der Geometrie.** Anlass T8.2–T8.5: alle Kantenstreifen der Szene waren falsch gewickelt und unsichtbar; drei Tasks lang grün, weil die Tests nur Positionen prüften. Gefunden erst mit `RenderTargetBitmap` (`ViewPictures`); in T8.6 bewies ein Byte-Vergleich der Bilder vorher/nachher, dass der Rückbau nichts änderte.
- **Mechanik-Tests laden Fixtures aus dem Test-Ordner, nie aus `ships/`** (CLAUDE.md A10). Datentests prüfen `ships/` selbst. Anlass Session 4a (D92): Die Racks aus den Spieldateien brachen 70 Tests (Treffertest, Drag, Szene, Kamera, Rack-Geometrie), ohne dass die App einen Fehler hatte, weil sie feste Zellen und Pixel der ausgelieferten Hull A, Freelancer und Caterpillar prüften. Seitdem: `tests/CargoPlan.Core.Tests/FrozenShips`, `TestPlans.HullA()`, `FrozenShips.Load`.
- **Mess- und Gate-Befehle selbst einmal laufen lassen, bevor Martin sie bekommt.** Anlass T8.5 (Messbefehl) und T8.7 (`--over-game`): erst der eigene Lauf zeigt, ob das Log das Richtige zählt; die 343 Vorschau-Wechsel des Sweeps hätten einen von SC gefangenen Cursor nicht verraten, daher „path keys“.
- **Eine Notice-Zeile, mehrere Schreiber: alle greppen, Zusammenspiel testen** (CLAUDE.md A13). Anlass B14: B13 baute „nach dem Speichern ist ein Ladeproblem gelöst, Zeile leeren“, T10.4b setzte in dieselbe Zeile den Konvertierungshinweis; jeder Task war für sich getestet, der Hinweis stand im fertigen ZIP 500 ms. Gefunden erst von der Upgrade-Probe in T10.6; ein Grep nach `SetNotice(NoticeSource.Plan` hätte beide Schreiber gezeigt.
- **Fremd-Framework zuerst als Spike mit Gates am echten Layout.** Anlass Phase 11: S11.0 fand vor dem ersten Prod-Commit CS0017 (zwei `Main`), den Setup-Hang mit App-Argumenten (velopack#1039), das Löschverhalten je packId und das 10er-Fenster von `GithubSource`; jedes davon hätte sonst ein Release getroffen.
- **Bei fremden CLIs heißt „leer“ oft „nicht gesetzt“.** Anlass T11.3: `vpk --exclude ""` ließ die PDBs trotzdem weg (Default `.*\.pdb`). Der Wert, der nie matcht, ist `(?!)`, und der Wächter ist ein Test, der einmal provoziert rot war (PDB-Zählung in `check-portable.ps1`).
- **UAC nur im Standardkonto prüfen.** Anlass S11.0 G5: im Admin-Konto mit `ConsentPromptBehaviorAdmin = 0` kam keine Abfrage; erst `cargotest` als Standardnutzer zeigte Abfrage, Ablehnung und `cancelled` (T11.2 Gate 5b).
- **A1-Grep nach dem Zweck, nicht nach dem Schalter.** Anlass `e7d06bb`: `--updates-prerelease` hob nur einen von zwei Pre-Release-Filtern auf (`GithubSource`, nicht `UpdateGate.Offer`); der Grep nach dem Schalternamen fand den zweiten nicht, einer nach „pre-release“ hätte ihn gefunden.
- **Ein Pack-Skript für lokal und CI, und das CI-Artefakt gegen das lokale vergleichen.** Anlass B7 (Runtime des Release-ZIPs nie geprüft) und T11.3: `build/publish.ps1` ist der einzige Ort für Publish und Pack, `ci.yml` und `release.yml` rufen es auf; der lokale Lauf auf dem Merge-Commit ist die Probe für den Tag. T11.5 hat die Lücke geschlossen: Runtime und Cli-Version des CI-ZIPs = lokales Pack mit demselben SDK. Jedes Release so prüfen.
- **Testzahlen aus einem sauberen Worktree.** Anlass T11.3/T11.4: drei gitignorte Scratch-Tests (2 Core, 1 App) zählten mit, gemeldet waren 1772/1969 statt 1770/1968. Zählen mit `git worktree add --detach <scratch> <commit>`, `dotnet test`, dann `git worktree remove`.
- **Abschnitt mit dem Schließen archivieren.** Die Abschluss-Session einer Phase, eines Releases oder einer Session verschiebt ihren Abschnitt wortgleich nach `docs/tasks-archive/` und setzt eine Index-Zeile unter „Geschichte (Archiv)“. Anlass K11: TASKS.md war auf 859 KB / 8 063 Zeilen gewachsen, und jede Session las sie ganz.

---

## Backlog (nur bei Bedarf)

Kein Plan, sondern Reaktion: wird erst zum Task, wenn der Anlass eintritt.

- Kapazitäts-Ausweichweg erst nach den anderen Zielen eines Scans legen (B17, Lauf 135: B 9×1 drängt Cs 4er aus dem
  Block). Heute legt `StopByStop` die Ziele eines Scans größte zuerst; ein Ziel „placed outside sections (capacity)“
  belegt dabei Zellen in fremden Reihen, die für die späteren Ziele feste Hindernisse sind. B17 nimmt solche Racks von
  der Blockregel aus, `SectionBlockTests.A_destination_outside_sections_can_leave_a_gap_in_another_section` hält den
  Fall fest. Auslöser: Hauler-Report über ein Loch in einem Rack mit Kapazitätshinweis.

- winget-Manifest (`winget install SC-CargoPlan`), früher T11.2. Auslöser: Ein Nutzer fragt danach (D109).
- Setup.exe und/oder Code-Signing (D109: heute nur Portable, unsigniert). Auslöser: Nutzer-Reports, dass SmartScreen
  sie abhält (T11.5: Warnung einmal je Download, „More info → Run anyway“).
  **Stand 19.11.2026:** kostenlose Signatur bei der SignPath Foundation beantragt (README-Absatz `7901350`) und am
  19.11.2026 **abgelehnt** (Kriterien: Stars/Forks/Contributors, externe Artikel, Reddit/YouTube-Referenzen). Martins
  Entscheidung: unsigniert bleiben, keine bezahlte Signatur; der README-Absatz ist raus (K12), D109 gilt unverändert.
  **Auslöser neu:** (a) erster Nutzer-Report, dass SmartScreen ihn abhält, oder (b) genug Sichtbarkeit für einen
  SignPath-Reapply. Dann: D-Eintrag (Signatur im Release-Workflow, Gegenargument), „unsigned“-Texte nachziehen – Nexus
  `description.bbcode` (Known issues „Unsigned“, Installationsschritt 3), Wiki Installation (Schritt 4,
  SmartScreen-Bild) und Troubleshooting („Unsigned“), README Schritt 3; Nexus- und SignPath-Bedingungen für
  Herkunftsangaben prüfen (A3).
- Screenreader-Zugang zur Skip-Checkbox der Update-Zeile: Toggle per UIA löst den Command nicht aus (`OnToggle` statt
  `OnClick`, T11.2); dazu die beiden „Clear“-Buttons im Settings-Fenster unterscheidbar benennen. Auslöser: Report eines
  Screenreader-Nutzers.
- Velopack-Log vollständig umleiten: `SetLogger` schreibt heute ab Information und erst ab gesetztem Datenordner nach
  `cargoplan.log`, Velopacks eigene Datei unter `%LocalAppData%\velopack\` bleibt (T11.2, Known issue 6). Auslöser: ein
  Bug-Report, der ohne diese Datei nicht zu klären ist.

- Rechteck-Picker für das Scan-ROI (der Nutzer zieht den Vertragsbereich einmal auf), für Seitenverhältnisse, bei denen
  das relative 16:9-Center-ROI nicht trifft (32:9, 16:10, geänderte HUD-Skalierung; S10.0 nur 21:9 und 16:9 geprüft).
  Auslöser: Nutzer-Report „ROI trifft nicht“.
- `PlanHistory.Entry.Reference` hat seit T10.4b (D107) keinen Leser mehr: `Record`/`PushUndo`/`PushRedo` reichen den Plan
  nach dem Schritt nur noch durch. Auslöser: der nächste Umbau der Undo-Historie (Messung `UndoTimingTests`, Test allein).
- `ScreenCaptureTests.A_hundred_captures_leave_the_gdi_object_count_as_it_was` zählt GDI-Objekte des ganzen Testprozesses,
  während WPF-Tests parallel rendern; einmal rot in T10.4b. Auslöser: zweites Auftreten → eigene Collection ohne
  Parallelität oder Toleranz mit Messung begründen (Lehre T7.1d).
- Vertragsliste in einem Scan statt eines Scans pro Vertrag (S10.0: ein Scan = ein Vertrag). Auslöser: Nutzer-Report.
- Mehrseitige Verträge (Text länger als eine Bildschirmseite, scrollen und zusammensetzen). Auslöser: Nutzer-Report
  oder ein Vertrag, dessen Lieferungen im Scan fehlen.
- **Kiosk-Manifest als autoritative Quelle** für die Mengen. Auslöser: Der Aufzug liefert eine andere SCU-Summe als der
  ACCEPTED-Text. Martins Antwort steht aus; IC STARC-175359/223408 deuten darauf hin.

- `holo-v2`- und `holo-v3`-Release (Pre-Releases im App-Repo) samt Tags löschen. Anlass: D114 – seit v0.6.0 kommen die
  Holos aus dem Data-Release, nur v0.3.0–v0.5.0 fragen noch `holo-v2` und v0.5.1 `holo-v3` (D63-Addendum T12.6a,
  Martin 19.11.: gleiches Datum für beide). **Auslöser: 31.01.2027** (zwei Monate nach v0.6.0). Danach: beide
  Manifest-URLs 404 prüfen, die alten Versionen zeigen den Umriss; CONTRIBUTING „Release hygiene“ (Holo-Releases im
  10er-Fenster) mitziehen.
- Modelle für 135c, 600i Executive Edition und F7C Hornet Mk II bei Sciirof (Motherlode) anfragen, mit Rückmeldung zur
  Handprüfung als Gegenleistung (Session 4a/4c). Auslöser: nach dem Launch, mit der Nachricht zu v0.3.0 (R5).
  **Stand 19.11.:** F7C erledigt (Spieldateien seit `ships-2026.11.1`, T12.2), 135c-Transform angekündigt
  (Shipdata-Backlog), **600i EE angefragt** (Discord 19.11.). **Auslöser: Sciirofs Antwort.**
- Sciirof fragen, warum die **Cyclone** im Katalog 2026-11-16 (4.10.3-LIVE.13165946) fehlt; bis dahin behält sie ihre
  holo-v2-Datei und ihren Umriss, `ships/cyclone.json` bleibt `absent` (V2). Auslöser: nächste Nachricht an Sciirof.
  **Antwort Sciirof (18.11.):** kommt ggf. zurück; die Pipeline deckt das mit SD6 „wieder da“ ab → Shipdata-Backlog
  (T12.6b), hier nichts zu tun. **→ Shipdata-Backlog.**
- Sciirof fragen nach einer Platzierung (Transform) für das Grid der **135c**: Modell `ORIG_135c` ist seit 2026-11-16 im
  Katalog, aber ohne Transform bleibt sie `kept` (Fleetyards-Racks, holo-v1). Mit Transform: Racks und Anker aus dem
  Modell. Auslöser: nächste Nachricht an Sciirof.
  **Antwort Sciirof (18.11.):** Transform kommt voraussichtlich; die Pipeline stoppt dann mit PR „override obsolete“
  (Regel in T12.6b), hier nichts zu tun. **→ Shipdata-Backlog.**

- Sektionsreihenfolge per Legenden-Drag (Spielerreihenfolge). Auslöser: Hauler-Report, dass die Legendenreihenfolge nicht reicht.
- `defaultFillOrder`, `fillRows`/`grouping`-Default pro Schiff in der Schiffs-JSON. Auslöser: zweites Schiff braucht einen anderen Default.
- Neubewertung „Sync-Mission wartet trotz freier Zellen“ (D89, Entscheidung 2). Auslöser: Live-Test, in dem eine Sync-Mission wartet, die mit `none` Platz hätte.
- Sektionsgrenzen in der Schiffsansicht zeichnen. Auslöser: Hauler-Report, die Farben zeigen es heute.
- Legend limit covers stop rows only; warnings stack below unbounded – revisit if a user reports it (K7/D80;
  16.11.: RAFT und Hull A mit 16 Missionen bei 30 % ok). Auslöser: Nutzer-Report.

- Warteeinheit Lieferung statt Mission (K6.4, E1): eine teilweise geladene Mission, deren Rest keinen Platz findet, lässt
  heute den Editor-Plan ablehnen. Auslöser: eine solche Ablehnung am Aufzug, vor allem nach K6.5 (Ziel an geladener
  CT-Mission nachgetragen). Aufwand: ≈ 20 Stellen an `Mission.Unplaced`, Formatfeld `stops[].unplaced`, Wartezeilen pro Lieferung.
- `RackAssignment` mit Hindernissen (K6.4, E2): mit geladenen Kisten legt der Planer nach der D41-Regel statt exakt (D64).
  Auslöser: Hauler-Report „Fragmentierung bei geladenen Kisten“ bzw. „Re-plan found no better layout“ ohne erkennbaren Grund.
- `CODE_OF_CONDUCT.md`, sobald es Contributors gibt; Community Standards mahnt es an, ein Solo-Repo braucht es vor dem ersten Hauler nicht (T6.4).
- Topmost-Fallback (`SetWindowPos(HWND_TOPMOST)` per Timer) nur bei Issue-Report mit Repro. Im T3.1-Test blieb das Overlay über Borderless-SC (D13).
- Vorschau-Maximum mit SC: einzelne Schlüsselwechsel kosten 20–25 ms (T8.7, M5; p95 5,2 ms, ohne SC max 12 ms); die
  16-ms-Regel (D65) fängt sie ab. Erst klären, ob CPU-Konkurrenz mit dem Spiel oder eine GC die Spitzen macht, dann ggf.
  der Recorder im echten Overlay (T8.7 R1). Auslöser: spürbares Nachziehen der Vorschau am Aufzug.
- `ShipCamera.PxPerCell`/`PanX`/`PanY` (Lesefelder für Kameras von vor D83, T8.1) entfernen. Auslöser: der nächste
  Schema-Bump von settings.json; nach v0.1.0 schreibt kein Build sie mehr.
- Hysterese für Rack-Nummern: Fläche wechseln oder ausblenden erst nach einem Mindestabstand, damit eine Nummer beim Drehen
  nicht hin- und herspringt (D83, T8.2). Auslöser: Flackern am Aufzug (T8.7) oder Hauler-Report.
- Kiste unter einem leeren vorderen Rack ist in der Ansicht nicht anklickbar (Regel D21 „erstes Rack entscheidet“);
  Alternative (b) nächste Kiste über alle Racks, Ablage behält „erstes Rack“. Auslöser: Hauler-Report oder Martin am Aufzug
  (Hull A von oben, Rack 1 leer). Wächter: `SceneHitTestTests.An_empty_rack_in_front_covers_the_cargo_behind_it` (T8.3, E1).
- Orbit-Snap auf feste Winkel (z. B. 45°-Raster per Taste), falls freies Drehen schwer wiederholbar ist (D15).
- Editor-Position in `settings.json`, Entwurfs-Autosave, Undo im Editor, falls der Zwei-Minuten-Durchlauf daran scheitert (D19). Undo-Teil → K2.
- Backdrop-Blur hinter dem Fenster (Spike: DWM vs. AllowsTransparency) (D28). Auslöser: Hauler-Report „Legende trotz Halo unlesbar“.
- IBM-Palette (`648FFF 785EF0 DC267F FE6100 FFB000`) als vierte Stop-Palette, sobald eine IBM-eigene Quelle die Hex-Werte nennt; Orange liegt ΔE76 26 am Problemrot (unter der Schwelle 33,6), müsste also ohne Orange kommen (K4.2). Auslöser: Hauler fragt danach.
- `appearance.crates.customStopColors` (Hex-Liste, aktiv bei `stopPalette = custom`), falls ein Hauler mit seltener Farbfehlsicht keine der drei Paletten lesen kann. Gegen: ungeprüfte Farben (D29), und eine Liste im Look-Record bricht die Gleichheit, an der K4.3 „Custom“ erkennt (K4.2).
- Startordner der Look-Dialoge (Export/Import, K4.3): ohne `InitialDirectory` nimmt Windows den zuletzt benutzten Ordner der App, bei Martin `AppData\Roaming\VoiceAccess\UserPhrases\de-DE`. „Dokumente“ als Start beim ersten Export, falls Hauler ihre Datei nicht wiederfinden. Auslöser: Meldung.
- `MissionFrame` (weiß, D68) ist dieselbe Farbe wie Okabe-Weiß (ΔE 0), obwohl der Kommentar „white is in no stop colour“ sagt: ein ausgewählter Missionsrahmen um eine weiße Kiste trägt nichts. Befund aus K4.2, kein Task; Auslöser: Look-Gate oder Hauler-Meldung.
- Starbreaker (Fleetyards-Extractor, Rust) als Quelle für die Gitter von Frachtmodulen. Auslöser: zweites Schiff mit Frachtmodul.
- Sync Ships in der App (Fleetyards-API), falls sich Frachtgitter häufiger ändern als Releases erscheinen oder Hauler zwischen Releases fehlende Schiffe melden. Voraussetzungen: D-Eintrag Netzwerkzugriff des Overlays, Lizenzfrage für Laufzeitabrufe, Merge-Logik aus import-fleetyards.ps1 nach C# (T7.1b; das Skript liegt seit 4c nur noch in der Git-Historie, `a3430b9^`, D94). Seit D92/D94 wäre die Quelle Motherlode, nicht Fleetyards.
- `CrateLayout.InOrder` beschleunigen (`StartingAt` ohne LINQ, Pruning), Auslöser: ein echter Plan mit der Notiz „too many crates to rearrange“ (D54). Erwartet bei Racks ab ~80 Zellen mit vielen 1-SCU-Kisten (T7.1c: 9×3×3, 32 + 40 × 1).
- Optionale Spieler-Reihenfolge (Legende per Drag sortieren) → Stapelhinweise relativ zur Spieler-Reihenfolge („Shubin liegt auf Riker, Riker kommt zuerst“). Auslöser: Hauler wollen wieder wissen, welche Kiste zuerst runter muss (D64).
- B1-Restfall: abgeschnittenes Fragment ohne Ergänzung. Capture 21–25 (24.10.): „to Thur“ bricht um, OCR liest „Thundering“ als „Thur…“, „Melg“ entsprechend; die Ergänzung aus der Drop-off-Liste greift nicht, weil das Fragment kein Präfix ist. Auslöser: weitere Fälle in echten Captures.
- Mengen pro Abholort („collect 3 from A, 2 from B“) statt einer Ortsliste ohne Mengen (D68). Auslöser: CT oder das Spiel
  nennen die Teilmengen.
- Kisten anderer Missionen ausblenden statt dimmen (D68). Auslöser: Martin oder ein Hauler will es nach dem Spieltest.
- Abholort ändern ohne Re-plan (heute braucht jede Editor-Änderung „Plan“, D19). Auslöser: ein Hauler trägt Abholorte erst
  nach dem Beladen nach und verliert dabei Moves.
- Mehrsprachigkeit, falls nicht-englische Hauler danach fragen. Beginnt beim Parser (deutsche CT-Captures als Fixtures), nicht bei `.resx` (D53).
- Fleetyards-Lage für Modul-Laderäume (Carrack, Caterpillar-Module und -Nase): heute modul-lokale Offsets, die überlappen, also generierte Reihe (D78). Auslöser: Fleetyards liefert schiffsweite Offsets für Module.
- Hull C: Stapelrichtung je Arm. Heute quer zum Arm, damit die Gitter 8×8×6/8×4×6 bleiben (D78); radial nach außen wie bei der Hull A hieße 8×6×8/8×6×4 mit Achse vom Kreuz weg. Auslöser: Beobachtung im Spiel oder in einem Beladungsvideo.
- Hull C: innerer und äußerer Block eines Arms ein Gitter oder zwei. Heute zwei Racks wie bei Fleetyards, beide einzeln voll packbar (K3.2). Auslöser: ein Hauler meldet, dass ein 32er über die Blockgrenze liegt.
- Kisten großer Schiffe im Default-Overlay klein: C2 13,2 px, Hull C 5,1 px pro Kistenkante (400 × 302, nach K5.2c), unter dem Hull-A-Wert 27,2 (Befund K5.2c, D62-Addendum). Auslöser: ein Hauler meldet zu kleine Kisten auf einem großen Schiff.
- Railen: die generierte Reihe (53 Zellen) ist länger als der Rumpf (40 Zellen), ragt aus dem maßstabsgetreuen Umriss (K5.2c). Auslöser: Fleetyards-Offsets für die Railen oder eine Meldung aus dem Spiel.
- Doku aus `docs/wiki/` im Hauptrepo per Action ins Wiki spiegeln – Auslöser: erster externer Doku-Beitrag.
- Wiki-Branch `k12-ship-reports` (lokal, `06b4004`: „Ships and cargo grids“ ohne Schiffsdatei-PR, K12) nach `master`
  mergen (`--no-ff`, CC), Martin pusht. **Auslöser: der nächste Tag.**
- Wiki-Branch `b16-legend-order` (lokal, `0386c22`: „Legend and delivery states“, Missionen in Scan-Reihenfolge, B16)
  nach `master` mergen (`--no-ff`, CC), Martin pusht. **Auslöser: der nächste Tag.**
- Wiki-Branch `b17-section-block` (lokal, `1009927`: „Plan and editor“, ein Scan schließt den Block seines Ziels, B17)
  nach `master` mergen (`--no-ff`, CC), Martin pusht. **Auslöser: der nächste Tag.**
- Screenshots/GIFs auf Perspektive nachdrehen (alle Shots von `docs/shots/SHOTLIST.md`, Hero-GIF eingeschlossen; D85) –
  Auslöser: nächstes Release mit sichtbarer Änderung der Ansicht oder erster Nutzer-Report „sieht bei mir anders aus“.
- UI-Text-Konsistenz (Befund T7.5a): Schiffswechsel-Dialog mit Yes als Default, Re-plan mit No; Export… und Remove ohne
  Tooltip, gesperrte Knöpfe nennen ihren Grund nur teilweise (Plan, ↑/↓, Add to mission ja; Export…, Re-plan, Discard plan,
  Delete mission nein); Hotkey-Notice „already in use“ nennt den JSON-Key statt des Fenster-Labels; Notices mal mit, mal
  ohne Schlusspunkt, „crates capped …“ klein; Weiß dreifach belegt (Rand „loaded“, Missionsrahmen, Okabe-Weiß). Auslöser:
  nach v0.1.0.
- Auffangnetz je `DispatcherTimer` (Review F10): das D82-Netz (`GuardedAsync`, Pause nach 5 Fehlern) deckt nur den
  Sync-Tick; `PlanSaver`, `OverlayBoundsSaver`, `CursorStateMonitor`, `HotkeyService`, `SettingsSession` laufen in
  `DispatcherUnhandledException` (Log + Dialog, App endet). Bewusst so, die Ticks fangen selbst. Auslöser: ein Crash-Report
  aus einem dieser Ticks.
- Virtual-Key-Codes an einem Ort (Review F16): `NativeMethods.cs` (Maustasten), `HotkeyPoller.cs` (Modifier),
  `HotkeyParser.cs` (Tasten), kein Wert doppelt. Eine `VirtualKeys`-Datei. Auslöser: T10.3 braucht den Scan-Default als VK
  oder ein vierter Ort.
- Ubuntu-CI ohne WPF (Review F20): Ubuntu kompiliert alle Projekte inkl. WPF (`EnableWindowsTargeting`), testet aber nur
  Core und Import. Auf `Core`, `Import`, `Cli` und ihre Tests beschränken spart die Referenzpack-Kompilierung. Auslöser:
  die CI-Laufzeit stört.
- `HoloLoaderTests.WaitFor` (Review F21): Polling 200 × 10 ms (2 s Deckel), einziger zeitabhängiger Test im Default-Run.
  Auslöser: der erste rote Lauf auf einem Runner (Lehre T7.1d).
- Lange Methoden im Planer (Review F22): `CrateLayout.Apart` (192 Z.), `LoadPlanner.LayStop` (106 Z.); `App.OnStartup`
  schneidet T10.1. Auslöser: die nächste inhaltliche Änderung an einer der beiden.
- Schiffs-Schema 4 nicht mehr lesen (Review F6): v0.1.0 lieferte 5, Community-Dateien sind 5 oder 6. 23 `TestShips` und
  `PlainShip()` in `ShipLoaderTests` stehen auf 4, zwei Theories prüfen 4 ausdrücklich (K10: Umbau größer als der Nutzen).
  Auslöser: der nächste Schiffs-Schema-Sprung (7), im selben Zug.
- `ImportMotherlodeScriptTests` aufteilen (K10, F26): 13,4 s in einer Klasse, jeder Test startet PowerShell; xUnit führt
  eine Klasse nacheinander aus, sie bestimmt die Core-Wandzeit (15,0 s). Zwei bis drei Klassen laufen parallel. Auslöser:
  der Core-Testlauf stört.
