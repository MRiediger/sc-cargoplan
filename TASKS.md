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

### [x] B17 Sektionen: Lücke bei Rest-Einzelkiste (19.11.2026)

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

**Befund (19.11.2026, `df46792`):**
- **Rot-Lauf** (`SectionBlockTests` gegen den alten Code): 56 von 88 rot per Assert. R1 High Course in falschen
  Zellen (L7/w3 leer); R2 Loch `Thundering Express @ 4.1.1`; Lückenhinweis fehlt; zweite 1er nicht auf L7/w4; dieselbe
  Lücke ohne Geladenes bleibt (`High Course @ 7.3.0`); Matrix 50 von 72, alle erst beim **zweiten Erfassen** (frischer
  Plan und Scan in leeren Plan waren auch mit Querprüfung lochfrei, der Detektor schlägt nicht falsch an); R4 Lauf 0.
  Grün schon vorher, als Wächter: E1-Gleichstand (2), Abstände genau `sectionGap` (7), R5 (1), Matrix 22.
- **E1 provoziert rot:** Gleichstand testweise auf „letzte Zelle in Füllreihenfolge“ gedreht → 4 rot (beide E1-Fälle,
  Lückenhinweis, Lauf-135-Test), zurückgesetzt.
- **Eigenschaftstest** (200 Mixe × Plan/zweimal erfasst = 400 Prüfungen) nach dem Umbau: 5 Löcher, alle nach dem
  zweiten Erfassen. **4** (Läufe 19, 135, 148, 163) in Racks mit „placed outside sections (capacity)“: Ein Ziel ohne
  eigene Reihen belegt Zellen in fremden Reihen, die dann feste Hindernisse sind (Lauf 135: B 9×1 in Cs Reihe L9, Cs
  zweite 4er passt nicht, L8/w3–w4 leer). **Martin: Backlog-Zeile** (siehe Backlog), Test
  `A_destination_outside_sections_can_leave_a_gap_in_another_section` hält den Fall fest, der Eigenschaftstest nimmt
  solche Racks aus. **1** (Lauf 103) in einer geteilten Sektion (F mit B): Bs 2er lagen fest in Fs Reihen, Fs Lücke war
  rechnerisch unvermeidbar. **Martin: die ganze geteilte Sektion neu** → umgesetzt (`SectionNotes.Sharing` liest die
  Partner transitiv aus den „shares a section“-Hinweisen); Test `A_capture_lays_a_shared_section_anew_whole` war rot
  (`F @ 2.3.0`), ist grün. Danach 4 von 400, nur die Kapazitätsfälle.
- **K9 auf Zellebene:** 48 von 240 Fällen ändern Zellen, **160 Kisten, alle 1 SCU, keine größere**; RAFT-Fixture 52,
  `hull_a_eight_stops`@RAFT 108; Hull A, Freelancer, `crate_mix`: byte-gleich. **Begründung:** Eine 1er nimmt jetzt die
  Zelle mit den meisten Nachbarn ihrer Sektion in derselben Schicht und Reihe; meist Ebene 1, etwa RAFT-Fixture
  crosswise Gap 1: Green PI 1 von L11/w5 nach L10/w4 (neben die eigene Kiste statt in eine neue Breite), Thundering PI 1
  von L5/w3 nach L4/w2. Reihen unverändert, daher bleiben die Reihen-Goldens grün. Der Schritt „geteilte Sektion“ ändert
  den Schnappschuss nicht (Erfassen, kein frischer Plan).
- **Bestehende Tests, die kippten: Core 0, App 0**, darunter alle `RackSectionsSyncTests` mit `NothingMoved`: Die
  Neulage legt die alten Kisten dort auf dieselben Zellen, und `MovedCrates` zählt gleiche Kisten nicht.
- **Änderung:** `CrateLayout` (`snug`: `RackSpace` merkt sich die Zellen der Sektion, 1er nach Nachbarn, Gleichstand nach
  Füllrang), `RackSections` (`Laid` mit `snug`, `Encloses`, `SectionNotes.Gap` und `Sharing`), `LoadPlanner`
  (`StopByStop` übernimmt neu gelegte Kisten, `LayStop.InSection` legt zuerst die Sektion neu, dann die alten Stufen mit
  `snug`; Hinweis bei Restlücke). `BackOnBoard` (K2.2) unverändert. Diff: 3 Quelldateien +212/−15, Tests 1 neu (+355),
  `docs/decisions.md` +2. Kein Schema-Bump (Plan-Schema 10).
- **Docs:** D41-Addendum (B17), D89-Addendum (B17) mit Gegenargumenten. Wiki: Absatz in „Plan and editor“ auf lokalem
  Wiki-Branch `b17-section-block` (`1009927`, ab `master`), Backlog-Zeile für den Push mit dem nächsten Tag.
- **Testzahlen aus sauberen Worktrees:** vorher (`e5400be`) Core 1919, App 2082, Import 280, Ocr 34; nachher (`df46792`)
  Core **2009** (+90), App 2082, Import 280, Ocr 34, alle grün; `dotnet build` 0 Warnungen, `dotnet format
  --verify-no-changes` sauber. Die Arbeitskopie zählt Core +6, App +1 (gitignorte `Scratch*`-Tests, Lehre T11.3).
- **Gate Martin am Aufzug (SC vorn, RAFT):** zweiter Scan mit 2+1 zu einem Ziel im Rack → Block geschlossen, Einzelkisten
  nebeneinander, Gap zwischen den Blöcken sichtbar; geladen abgehakt und erneut gescannt → Geladenes bleibt liegen.
  **Abgenommen 19.11.2026** („ist abgenommen, funktioniert wie erwartet“). Merge `--no-ff`, Martin pusht.

### [x] F10a Seitenweise Planung – Bahnen im Rack (20.11.2026)

**Warum:** Auf flachen Platten (RAFT, Perseus, C2, Taurus-Klasse) will der Hauler erst **eine Seite** voll laden, dann
die andere. Die Breite eines Racks wird in zwei Bahnen geteilt (Backbord/Steuerbord), jede Bahn läuft wie heute das
ganze Rack. F10 ist geteilt (Martin, 19.11.): **F10a** = Bahnen innerhalb eines Racks (reines Layout in Core),
**F10b** = Rack-Gruppen nach Seite (eigener Task, unten).
- Plan-Felder `sides: none | portFirst | starboardFirst` (Default `none`) und `sideOverflow: true | false` (Default
  `true`), im Editor-Abschnitt „Layout“; plan.json 10 → 11, Missions-JSON 4 → 5.
- A1: Vorläufer sind die Backlog-Zeile „Zweiseitige Sektionen“ und D88 (dort `portFirst`/`starboardFirst` als Werte von
  `fillOrder` verworfen).

**Akzeptanz:** Topologie-Tabelle, Berührte-Stellen-Tabelle, D116; alle Tests grün, Repro-Golden RAFT; Default
byteidentisch; `dotnet test` komplett. Gate am Aufzug (unten).

**Befund vor dem Plan (19.11.2026, Branch `feat/f10a-lanes` ab `495a7aa`):**

1. **Topologie** aus den Daten (Baseline `.ship-data/ships-2026.11.1`, 110 Schiffe, 340 Racks). A = ein Rack, Stapel
   ±z; B = Racks nebeneinander (ein Paar in y getrennt, in x überlappend); C = Racks hintereinander (in x getrennt, in y
   überlappend); D = Rest. y-Ausdehnung nach `stackAxis` (`ship-format.md:174`).

   | Typ | Schiffe | Beispiele | mit Bahnen (Rack ≥ 4 breit, Breite entlang y) |
   |---|---|---|---|
   | A | **54** | RAFT 12×8 −z, Perseus 6×8, A2 18×6, Centaurus 10×6 | **20** |
   | B | 22 | Hermes 2×4, Polaris 2×6, Hull A (±y), Apollo, Cutlass Red/Blue | 6 |
   | C | 10 | **C2/M2** (8 + 6), **Taurus** (4 + 2), Liberator, Cutlass Black, 600i Exec | 6 |
   | D | 24 | Carrack, Caterpillar, Idris, Hull B/C, Railen, Freelancer | 18 |

   - Typ A nach Breite: W1 20, **W2 12, W3 2 (fallen nach F3 durch: 14)**, W4 12, W5 4, W6 2, W8 2 → 20 mit Bahnen, jede
     Bahn ≥ 2. Ungerade Breite ≥ 4: Asgard, Phoenix, Valkyrie ×2 (W5 → 2 + 2 + Mitte).
   - Racks nach Achse: +z 277, −z 16, ±y 30, ±x 17. z-Racks nach Breite: 1: 70, 2: 58, 3: 18, 4: 99, 5: 19, 6: 19, 8: 6,
     14: 2, 18: 2.
   - **C2 und Taurus sind Typ C** (zwei Racks hintereinander), nicht Typ A wie im Startprompt.
   - **Seite:** Bei ±z und ±x läuft die Breite entlang Schiffs-y aufwärts, **w = 0 ist Steuerbord** (`Rack.cs:97-129`,
     `FillRows.cs`), unabhängig vom Vorzeichen; die Drehung (D91) ist nur Ansicht. Bei ±y (`StacksSideways`) steht die
     Breite senkrecht: nie Bahnen.
   - **Seiten-Notice** (Racks mit `origin.y > 0` und `< 0`): 42 Schiffe, davon 22 mit bahnfähigen Racks (Carrack,
     Hermes, Polaris, Idris …); C2, Taurus, Liberator, RAFT nicht.
   - FrozenShips (A10): `raft`, `perseus`, `c2_hercules`, `hull_a`, `starfarer`/`caterpillar` (W5); kein W3, kein W7 →
     synthetisch im Test.
2. **Berührte Stellen:**

   | Stelle | Ist | F10a |
   |---|---|---|
   | Zellreihenfolge | `CrateLayout.Ranks` (`CrateLayout.cs:760`), einzige Stelle mit Achsen; Stapel > Fortschritt > Reihe | Bahn außen: Bahn > Stapel > Reihen auf der Bahnbreite; ohne Bahnen der heutige Pfad |
   | Kistenplatz | `FirstFit` (`:802`), `Apart.StartingAt` (`:641`): nur Grid-Grenzen | keine Kiste über eine Bahngrenze (3.) |
   | Rack-Zuordnung | `RackAssignment` (`Fits` `:504`, Wahl über `LayingOf` `:533`), `FirstFitDecreasing` | unverändert ohne Bahnen (F5) |
   | Sektionen | `RackSections` (D89, B17): `RowAxis` `:20`, `Arrangement` `:221`, `Outside` `:153`, `Encloses` `:176`, `RowsFor` `:91` | Reihen = Bahn 1, dann Bahn 2, dann Mitte; Lücke nicht über die Grenze; `Encloses` je Bahn |
   | Erfassen | `LoadPlanner.LayStop` (`:1058`), `InSection` (`:1127`), `LargestFirst` (`:1245`) | Bahnen direkt; `sideOverflow=false`: Stufen in einer Bahn |
   | Plan/Re-plan | `Plan` (`:25`, `:388`), `Sectioned` (`:87`), `PlanKeeping`/`Keeping` (`:429`), `Replan` (`:660`), `ScanSync.cs:62` | Felder durchreichen (4.) |
   | Verschieben, Teilen | `MoveResolution` (`:537`), `PlaceWithin` (`:134`) | unverändert ohne Bahnen |
   | Hinweise | `SectionNotes` (`RackSections.cs:383`), `Names` liest per Wortlaut; einziger Schreiber `NoticeSource.Sections`: `OverlayViewModel.cs:320` | neue Texte + Parser |
   | plan.json | Schema 10 (`PlanFile.cs:20`; „final since T10.4b“ = Ende Phase 10, kein Bump-Verbot), `PlanFileDto.cs:20-31, :59, :104-114, :156` | 11 |
   | Missions-JSON | Schema 4 (`MissionFileReader.cs:15`, `MissionFileDto.cs:18-76`, `MissionFileWriter.cs:16`) | 5 |
   | Editor | `MissionEditorWindow.xaml:99-149` (`WrapPanel`, 660 × 420), `MissionEditorViewModel.cs:41-196, :277-320, :394`; **„880 px“ gibt es nicht** (K9.2: 760, K9.2b zurück auf 660 mit Umbruch) | 2 ComboBoxen |
   | CLI `plan` | Optionen aus der Missionsdatei (`Program.cs:68-70`), `PlanTextRenderer` druckt Warnungen als `!` | 2 Defaults |
   | Look/Export, Settings | Layout ist Plan (D77, `LookFile.cs:86-95`), keine Layout-Defaults (Schema 3) | keine Änderung |
3. **Großkisten:** Heute prüfen nur die Grid-Grenzen (`FirstFit` `w+size.W<=W`, `StartingAt`; `Rack.CanHold` das ganze
   Grid). Eine Rangfolge allein reicht nicht: `FirstFit` vergleicht Ränge, eine 2-breite Kiste läge auf w = h−1..h.
   **Maske im Rack:** `Lanes` (Bahn-ID je Breitenindex, zusammenhängend) prüft mit `lane[w] == lane[w+W−1]` an genau
   zwei Stellen (`FirstFit`, `StartingAt`). Gedrehte 2er nie in der Mitte, 4er/8er/16er nie über die Grenze: F2 ohne
   eigene Regel. Gegenargument: Eine Bahn als eigener `RackSpace` wäre isolierter, aber Kapazität, Türlimit, Racknummer,
   Stützprüfung und Geladenes hängen am echten Rack (die verworfene Alternative „Rack virtuell teilen“ aus F7). Ab Breite
   4 passt jede Kistengröße ungedreht in eine Bahn (D57).

**Plan (19.11.2026):**

1. **Modell:** `Sides { None, PortFirst, StarboardFirst }` (`Models/`), `Json/SidesJson.cs` nach `FillRowsJson`;
   `LoadPlan.Sides` (Default `None`), `LoadPlan.SideOverflow` (Default `true`). `RackSide` (Rack-Position) bleibt.
2. **`Lanes`** (Core/Planning): `Of(rack, sides)` = null bei `None`, ±y oder Breite < 4; sonst Bahn 1 = Backbordhälfte
   (hohe w) bei `portFirst`, Steuerbordhälfte bei `starboardFirst`, Bahn 2 die andere, Bahn 3 die Mitte bei ungerader
   Breite; Breitenfolge je Bahn von außen zur Mitte.
3. **`CrateLayout`:** `Ranks(…, lanes)` mit Bahn außen; `FirstFit`/`StartingAt` verwerfen Kisten über einer Grenze;
   `RackSpace`, `Lay` reichen `lanes` durch (optional, Default null, jede Aufrufstelle angesehen).
4. **Wer legt mit Bahnen (E1, ein Mechanismus: `CrateLayout.Lay` mit `lanes`):**

   | Aufrufer | Bahnen | Warum |
   |---|---|---|
   | `RackAssignment.Fits`, `LayingOf` (Wahl), `FirstFitDecreasing` | nein | Sie wählen Racks und Wartende; mit Bahnen könnte die Wahl kippen (F5) |
   | `RackAssignment.Built` (Lage der gewählten Zuordnung), `PlanStops`-Rückfallschleife (`:73`) | **direkt** | Die Wahl ist getroffen; nur die Lage folgt |
   | `RackSections.Arrangement` (byDestination) | **direkt** | Sektionen sind schon der Durchgang nach dem Planer (D89) |
   | `LayStop`, `LargestFirst` bei Erfassen (`PlaceInto`, `PlaceAs`, `RetryUnplaced`) | **direkt** | Kein Durchgang danach; Sync-Abweichung wie D89 akzeptiert |
   | `Keeping` (Plan/Re-plan mit Geladenem) | **Nachdurchgang** | Mission für Mission: legte jede mit Bahnen, hinge die nächste davon ab, Wartende wären nicht mehr wie `none`. Erst ohne Bahnen platzieren, dann je Rack die geplanten Kisten mit `Lay(lanes)` neu, Geladenes als Hindernis |
   | `MoveResolution`, `PlaceWithin`, `LayOutShip` | nein | Ein Zug ist kein Schloss (D89); Teilen bleibt in der Kiste; Migration alter Pläne |

   Nachdurchgang sonst nur für, was `Lay` nicht trägt: Hinweise (F3, Seiten) und `sideOverflow=false` (Mission wartet).
   Scheitert `Lay(lanes)` in einem Rack, gilt `Lay` ohne Bahnen mit Hinweis `Rack N: lanes don't fit – laid as without
   lanes`.
5. **Sektionen:** `RowAxis` mit Bahnen (Reihen-IDs je Bahn, Folge Bahn 1, 2, Mitte). `sideOverflow=true`: ein Cursor über
   alle Reihen, ein Ziel darf an der Grenze geteilt werden, Lücke nur innerhalb der Bahn. `false`: Cursor je Bahn, jedes
   Ziel ganz in die erste Bahn, die es nimmt (Legendenfolge); passt es in keine, **wartet seine Mission** (Hinweis
   `Rack N: X waits – lanes full (side overflow off)`), das Rack wird neu gelegt. Ohne Missionen (`Plan` mit Stopps, CLI)
   wartet nichts: das Ziel liegt wie mit `true` und bekommt `Rack N: X crosses sides – side overflow off`.
6. **Hinweise** (`SectionNotes`, `Names`/`IsNote`), nur bei `sides ≠ none`, nur Racks mit geplanter Fracht, Lebensdauer
   wie K9.3: `Rack N: lanes need a rack at least 4 wide`, `Rack N: no lanes on a rack that stacks sideways`,
   `Rack N: lanes don't fit – laid as without lanes`, die zwei aus 5., schiffsweit `lanes apply per rack – side-first rack
   order is not available yet` (K1; Verweis auf F10b nur im Kommentar und in D116). A13: alle Schreiber von
   `NoticeSource.Sections` gegrept, Zusammenspiel mit K9/B17 getestet.
7. **Formate:** plan.json 11 (`sides`, `sideOverflow` ab 11 Pflicht, darunter Fehler; 8–10 lesen als `none`/`true`),
   Missions-JSON 5 (optional, immer geschrieben, in ≤ 4 Fehler). `FillRowsFormatTests.OlderGolden` nachziehen.
8. **Editor:** `SidesChoice`, `SideOverflowChoice`; „Sides“ und „Side overflow“ in der `WrapPanel`-Zeile; „Side overflow“
   aktiv nur mit `sides ≠ none` und `byDestination`. **CLI:** Defaults `none`/`true`.
9. **Docs:** D116 (Kontext, Optionen: `fillOrder`-Werte nach D88, Rack virtuell teilen, Bahn als `RackSpace`, Maske;
   Gegenargumente: Packdichte am Bahnende, Warten trotz freier Zellen, Mitte nur 1er/2er, Züge ignorieren Bahnen;
   Messwerte), Addenda D88, D89; `plan-format.md`, `mission-format.md`, `CLAUDE.md`; Wiki „Layout“ auf lokalem Branch
   `f10a-lanes`.
10. **Commits (E2):** 1 = Modell + `Lanes` + `CrateLayout` mit R11 grün; dann Planer/Sektionen, Formate, Editor, Docs.

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:**
- P1 Bahnen pro Rack auf allen Schiffstypen (Stapel ±z/±x, Breite ≥ 4), nie auf ±y; Seiten-Notice per Vorzeichen von
  `origin.y`, keine Topologie-Klasse in Core. Ersetzt F8 „Typ C/D wirkt nicht“.
- P2 Modell „Seite voll zuerst“: Bahn 1 nimmt Ziele in Legendenfolge bis zum Rand, dann Bahn 2, dann Mitte. `true`: ein
  Ziel darf an der Grenze geteilt werden, jeder Teil ein Block (B17). `false`: jedes Ziel ganz in einer Bahn, First Fit
  nach Bahn; passt keins, wartet die Mission (F5-Ausnahme, gewollt). „Kapazitätszeile unverändert“ = Schiffskapazität.
- P3 Ohne Sektionen ist `sides` reine Zellreihenfolge (Ziele gemischt wie heute); `sideOverflow` wirkt nur mit
  `byDestination`.
- P4 Tests korrigiert (Martin): „2 × 24 portFirst“ → beide Backbord, Steuerbord leer; „8 Ziele“ nur als Golden, wenn
  vier Ziele eine Bahn füllen.
- P5 Gate korrigiert: Mission 1 und 2 auf Backbord, die dritte läuft über und landet Steuerbord.
- P6 Jede Bahn von ihrer Außenkante zur Mitte; Mitte zuletzt; `starboardFirst` = Spiegelbild von `portFirst`.
- P7 Goldens: Mit Schema 11 ändert sich der Kopf der 12 Schema-10-Goldens (`schemaVersion` + zwei Zeilen); der Test
  beweist, dass genau diese 3 Zeilen abweichen; dazu B17-Zell-Schnappschuss (240 Fälle) unverändert.
- P8 Editor-Import ohne die Felder behält den Editorwert (D88-Muster); Reader und CLI setzen `none`/`true`.
- P9 Züge legen ohne Bahnen; Geladenes über einer Grenze bleibt liegen; Eigenschaftstest prüft nur Geplantes.
- P10 Eigener Text für ±y-Racks („at least 4 wide“ wäre bei den 4 breiten Railen-Seiten falsch).
- P11 (E1) Zuordnung der Aufrufer wie in Plan 4; Stopps-Weg ohne Missionen mit Hinweis statt Warten (Plan 5).

**Tests (erst rot):** R1 RAFT `portFirst` crosswise 2 × 24 → beide in w4–7, w0–3 leer; `starboardFirst` gespiegelt.
R2 Überlauf A = 80, B = 24: `true` B 16 + 8 geteilt, je Block; `false` B ganz Steuerbord; `false` A = 90, B = 100 → B
wartet, Hinweis. R3 Breite 7: 3 + 3 + Mitte, keine 4er in der Mitte, nichts über einer Grenze. R4 Breite 3 und Hull A:
ohne Wirkung, Hinweis, Bild = `none`. R5 Golden 8 × 24 Gap 0 (A–D Bahn 1, E–H Bahn 2); 8 × 16 Gap 1 (Lücken nur in
der Bahn, E ohne Lücke am Anfang von Bahn 2), B17 hält. R6 Goldens `lengthwise` und `sternFirst` mit Bahnen. R7 C2:
Bahnen in beiden Racks, kein Seiten-Hinweis; Racks y > 0 und < 0: Hinweis. R8 Geladen + Umschalten. R9 Formate.
R10 Eigenschaftstest (Seed fest, 200 Fälle): nichts über einer Grenze; `false`: kein Ziel geteilt, keins in Bahn 2, das
zu seiner Zeit in Bahn 1 gepasst hätte; `true`: Bahn 2 erst belegt, wenn Bahn 1 keine Reihe mehr frei hat.
R11 Default: 12 Goldens nur 3 Kopfzeilen anders, B17-Schnappschuss gleich, alle Tests grün. R12 Hinweise (A13).
R13 Editor-VM.

**Gate Martin am Aufzug (SC vorn, RAFT):** zwei Missionen scannen, `portFirst`, `byDestination` → beide auf der
Backbordhälfte; dritte Mission mit Überlauf → der Rest landet Steuerbord; Umschalten auf `none` → heutiges Bild.

**Freigabe 20.11.2026:** P1–P10 ratifiziert, D116. **K1:** Seiten-Notice ohne „F10b“ („side-first rack order is not
available yet“), Verweis nur in Kommentar und D116. **E1:** bei `grouping: none` ein Mechanismus – `Lay` mit `lanes`
legt direkt, Nachdurchgang nur für Fälle, die `Lay` nicht trägt; im Plan zugeordnet (Plan 4, P11). **E2:** Commit 1 =
Modell + `Lanes` + `CrateLayout` mit R11 grün, dann der Rest. Rot-Lauf mit Zahlen zuerst.

**Befund (20.11.2026, `309ccd8` … `1d98ba5`):**
- **Commits:** `309ccd8` Modell + `Lanes` + `CrateLayout` (E2: R11 grün, Core 2045 / App 2083 inkl. Scratch);
  `9ec973f` Planer, Sektionen, Erfassen; `9abcf8d` plan.json 11, Missions-JSON 5, CLI; `97d2cd8` Editor; `1d98ba5`
  D116, Addenda D41/D88/D89, Formatdoku, Timing-Spalte. Diff gegen `495a7aa`: Core 7 Quelldateien + 2 neu (`Lanes`,
  `SidesJson`, `Sides`), App 1 + 2 neu, Tests 3 neu (`LanesTests` 30, `LanePlanTests` 33, `SidesFormatTests` 16).
- **Rot-Läufe:** `LanesTests` 23 von 30 rot gegen die bloße Oberfläche, 16 von 30 mit nur `Lanes.Of` (alle
  Layout-Asserts); `LanePlanTests` 30 von 31; `SidesFormatTests` 15 von 16; Editor 11 Compile-Fehler. Grün schon vorher,
  als Wächter: Breite 1–3, ±y, `none`, Ranks ohne Bahnen, „Racks und Wartende wie ohne Bahnen“, Schema 4 ohne Seiten.
- **Mutationen:** `StartingAt` ohne Grenzprüfung → 1 rot (Eigenschaftstest über die exakte Suche), `FirstFit` → 3;
  Planer M1–M8 (Lücke am Bahnanfang, eine Seite ohne Überlauf, Warten, `InLanes` nach `Keeping`, abgeleitete Hinweise
  ×2, Bahnen in `Built`, Bahnen beim Erfassen, `Encloses` je Bahn) je ≥ 1 rot. **M1 und M8 überlebten zuerst:** dafür
  kamen „Sektion endet am Bahnende, Bahn 2 ohne Lücke“ (32 + Gap + 56 SCU) und „Block endet an seiner Bahn“ dazu.
- **F8 Default byteidentisch, zweifach:** die zehn Hull-A-Goldens per Skript um genau 3 Zeilen geändert
  (`schemaVersion` 10 → 11, `sides`, `sideOverflow`; je +3/−1), `HullAGoldenTests` byte-gleich dagegen; der
  B17-Zell-Schnappschuss (3768 Kistenzeilen, K9-Fixtures × Reihen × Richtung × Gap 0–2, Plan und Re-plan) auf `495a7aa`
  und `HEAD` identisch.
- **Bestehende Tests, die kippten:** 21 an Schemanummer oder Feldfolge (Core 20, App 1: Ablehnungstext „8 or 9 or 10
  or 11“, Schema 11/12 statt 10/11, Missions-Writer 5 statt 4, Feldfolge mit `sides`), 2 an der Zahl der
  Layout-ComboBoxen (4 → 6); **keiner an Verhalten.** Der Byte-Test des Missions-Writers liest eine neue Schema-5-Fixture
  (`sides_port_first.json`), die Schema-4-Fixture bleibt Lesefixture.
- **Messung** (Release, ohne Tiered JIT, Median aus 3, Bug zuerst, `FillOrderTimingTests` mit neuer Spalte `sides`):

  | Fall | ohne Bahnen | `portFirst` |
  |---|---|---|
  | RAFT-Fixture (66 SCU, 5 Ziele) | 3,1–18,2 ms | 5,3–18,7 ms |
  | RAFT acht Ziele | 2,0–33,1 ms | 2,9–33,2 ms |
  | Hull C acht Ziele | 45–82 ms | 48–112 ms (crosswise Gap 2) |

  Knoten der Rack-Suche in allen 48 Paaren identisch (RAFT 30/45, Hull C am Limit), keine Suche gab auf: die Wahl
  sieht keine Bahnen (F5). 200 gemischte Pläne auf der RAFT: 65 s mit Bahnen, 108 s ohne.
- **App selbst gestartet** (`--data-dir`, `--plan` RAFT 40 + 24 + 32 SCU, `portFirst`, `byDestination`): läuft,
  speichert Schema 11; Everus Harbor 40 und Riker 24 Backbord, Green Glade 16 Backbord + 16 Steuerbord. CLI `plan`
  mit Schema-5-Datei (`sideOverflow: false`, 88 + 100 SCU): „Rack 1: Shubin crosses sides – side overflow off“.
- **Testzahlen aus sauberem Worktree:** vorher (`495a7aa`, B17) Core 2009, App 2082, Import 280, Ocr 34; nachher
  (`1d98ba5`) Core **2088** (+79), App **2090** (+8), Import 280, Ocr 34, alle grün; `dotnet build` 0 Warnungen,
  `dotnet format --verify-no-changes` sauber; `Category=Slow` 4/4; `Category=Exhaustive` 2/2 (6 min 19 s, Release).
- **Wiki:** Abschnitt „Layout“ (sechs Optionen, Unterabschnitt „Sides“, Missions-JSON Schema 5) auf lokalem
  Wiki-Branch `f10a-lanes` (`4954528`, auf `b17-section-block`), Push mit dem nächsten Tag (Backlog-Zeile).

**Präzisierungen nach der Freigabe (zur Ratifizierung):**
- P12 **Erfassen ohne Überlauf:** Die Sektionsstufen nehmen nur Reihen einer Bahn (ein neues Ziel darf auch am Anfang
  einer späteren Bahn beginnen). Findet keine Stufe Platz, liegt das Ziel wie bisher außerhalb der Sektionen
  („placed outside sections (capacity)“) statt zu warten (Plan 5 sagte „wartet“). Grund: D41/D89 – ein Scan lässt nur
  warten, wenn kein Platz ist; der nächste Plan oder Re-plan legt nach der Regel ohne Überlauf (eine Seite oder warten).
- P13 **Ein Ziel allein im Rack** hat keine Sektion (D89) und läuft wie mit Überlauf in Zellreihenfolge über beide
  Seiten; `sideOverflow` wirkt nur in geteilten Racks (Folge von P3).
- P14 **Passt ein Ziel ohne Überlauf in keine Bahn und auch nicht quer über die freien Reihen**, bleibt das Rack wie der
  Planer es legte, und das Ziel wird gemeldet: mit Missionen wartet es (danach wird das Rack ohne es neu in Bahnen
  gelegt, `InLanes`), ohne Missionen (CLI) mit „crosses sides“.
- P15 **Eigenschaftstest (R10)** geschärft: Mit Überlauf hat Bahn 1 keine **zwei benachbarten** leeren Reihen, wenn
  Bahn 2 belegt ist (eine einzelne leere Reihe am Bahnende ist Geometrie: Kisten über 1 SCU sind 2 lang und drehen nur,
  wo sie müssen, D71; gefunden in Lauf 24). Mixe bis 6 Ziele / 120 SCU statt 8 / 160: die dichten Mixe kosteten auch
  ohne Bahnen Sekunden je Plan (Suche D64), Testdauer 2:46 min → 5 s; Bahn 2 wird in > 10 Läufen erreicht.
- P16 **Sektionen sind ganze Reihen:** 2 lange Kisten belegen Reihen paarweise. RAFT 40 + 40 SCU füllt alle 12 Reihen
  der ersten Seite, eine dritte Mission liegt dann ganz auf der zweiten, obwohl einzelne Zellen frei sind. Für das Gate
  wichtig (unten).

**Gate Martin am Aufzug (SC vorn, RAFT):** Editor → Layout: **Rows** Crosswise, **Grouping** By destination,
**Sides** Port first, **Side overflow** Cross over. Zwei Missionen scannen, die zusammen deutlich unter 96 SCU liegen
(etwa 40 + 24) → beide liegen auf der Backbordhälfte, Steuerbord leer. Dritte Mission, die die 96 SCU überschreitet →
ihr Rest landet Steuerbord (bei 40 + 24 + 32: 16 + 16). Umschalten auf **Sides: None** und **Plan** → das heutige
Bild. Optional: **Side overflow: Wait** und Plan → die dritte liegt ganz Steuerbord.

**Abgenommen 20.11.2026** (Martin), P12–P16 ratifiziert. **Auflage:** P12 und P16 je ein Satz im Wiki „Layout“
(Branch `f10a-lanes`, `ea9252a`) und in D116 – erledigt. Merge `--no-ff` nach `main`, Martin pusht.

### [ ] F10b Seitenweise Planung – Rack-Gruppen nach Seite

**Warum:** Auf Schiffen mit getrennten Racks links und rechts (Hull B, Railen, Hull A, Carrack, Hermes, Polaris) heißt
„erst eine Seite“ nicht Bahnen im Rack, sondern erst die Racks der einen Seite, dann die der anderen. Das greift in die
Rack-Zuordnung (B2/D64) ein, die F10a bewusst nicht anfasst. F10a sagt auf diesen Schiffen „lanes apply per rack –
side-first rack order is not available yet“ (42 Schiffe mit Racks bei `origin.y > 0` und `< 0`).

**Akzeptanz:** Hull-B-Fixture (`FrozenShips`, A10), `sides` ordnet Racks nach Seite; das Default-Verhalten ist
byteidentisch zu heute; Zusammenspiel mit den Bahnen aus F10a festgelegt.

**Befund vor dem Plan (20.11.2026, Branch `feat/f10b-side-racks` ab `1426252`):**

1. **Seitengruppen aus den Daten** (Baseline `ships-2026.11.1`, 110 Schiffe). Seite aus der y-Ausdehnung des Racks
   (`Rack.Bounds`: Breite bei ±z/±x, Stapel bei ±y): Backbord = `min.y ≥ 0`, Steuerbord = `max.y ≤ 0`, Mitte =
   `min.y < 0 < max.y`. **42 Schiffe** mit Racks auf beiden Seiten (dieselben 42 wie F10a mit `origin.y`). Wo die Daten
   `position.side` nennen (30 Racks, u. a. Hull A, Hull B), stimmt die Seite überall.

   | Schiff | Backbord (SCU) | Steuerbord (SCU) | Mitte (SCU) |
   |---|---|---|---|
   | Hull B | 1, 2, 5, 6, 9, 10, 13, 14 (256) | 3, 4, 7, 8, 11, 12, 15, 16 (256) | – |
   | Hull A | 1, 2 (32) | 3, 4 (32) | – |
   | Railen | 1, 2, 4 (320) | 3, 5, 6 (320) | – |
   | Hermes | 1 (144, 18×4) | 2 (144, 18×4) | – |
   | Polaris | 1 (288, 12×6) | 2 (288, 12×6) | – |
   | Carrack | 1, 4, 7 (192) | 3, 6, 9 (192) | 2, 5, 8 (72, 3×4 auf der Mittellinie) |
   | Caterpillar | 8 (8) | – | 1–7, 9–14 (568) → **keine Seiten, F3** |

   Weitere mit Mitte: Hull C (4 + 4 + 8 Spindel-Racks), Idris M/P, 890 Jump, Galaxy, Starfarer, Moth, 600i Explorer,
   Freelancer MAX, Zeus CL, Ironclad. Ungleiche Seiten: Freelancer 60/6, Fortune 10/6, Syulen 4/2.
   - **„y = 0“ im Startprompt:** 36 Racks beginnen bei `origin.y = 0` (Richtung +y), 22 enden bei 0 (von −y), u. a.
     Hull B 3/7/11/15 (y −2..0, laut Daten `starboard`) und Railen 4/6 (0..4 / −4..0). Wörtlich gelesen wären sie Mitte
     → P1.
   - **Hull-B-Spindel:** zwei Blöcke entlang x (Bug x −11..−3, Heck x −20..−12), je 8 Platten um die Spindel (±42°
     gedreht, Stapel ±y). Je Block und Seite: unten außen + unten innen, oben außen + oben innen. Folge innerhalb
     Backbord = Racknummer: **1, 2 (Bug unten) → 5, 6 (Heck unten) → 9, 10 (Bug oben) → 13, 14 (Heck oben)**; Steuerbord
     3, 4 → 7, 8 → 11, 12 → 15, 16. Alle 16 Racks 8×2×2, Stapel ±y → nie Bahnen; D64 behandelt sie als gleichartig, die
     Folge innerhalb einer Seite bleibt die Racknummer (Tie-Break D64).
2. **Rack-Zuordnung heute:**

   | Stelle | Ist | Einschränkbar? |
   |---|---|---|
   | `RackAssignment.Assign` (`RackAssignment.cs:41`) | Ganze Zuordnung auf einmal über ein `Rack[]`, Prio 1–3 lexikografisch, Suchfolge Ziel nach SCU/Name, `MaxSearchNodes` 1 Mio. (D64), Layout-Schritte `StackingBudget` (D58), beide je Aufruf | **Ja:** das `racks`-Array ist die Kandidatenmenge; ein Aufruf je Gruppe lässt die Prioritäten innerhalb der Gruppe unberührt |
   | `PlanStops` (`LoadPlanner.cs:38`) | `Assign` über alle, sonst `FirstFitDecreasing` | gruppenweise statt `Assign` über alle |
   | `FirstFitDecreasing` (`:1435`) | Kandidaten: Racks mit dem Ziel, erstes leeres, dann Racknummer | Folge je Gruppe |
   | `Keeping` → `TryPlace` → `StopByStop`/`LayStop`, `LargestFirst` | Mission für Mission (D41) | über die Kandidatenfolge von `LayStop`/FFD |
   | `Replan` → `ReplanInPlace`/`ReplanForShip`/`PlanOrPlace` | alle über `PlanMissions` → `PlanStops`, Rückfall `RetryUnplaced` → `TryPlace` | erben `PlanStops` und `LayStop` |
   | `Sectioned` (`:162`) | einziger Durchgang nach allen Plan-Wegen mit Missionen; F10a lässt dort Missionen warten | Ort für das Warten ohne Überlauf |

   „Schrittbudget D58“ im Startprompt: in `Assign` sind es zwei, D64 (Knoten) und D58 (Layout-Schritte).
3. **Erfassen** (`LayStop`, `:1166`): Kandidaten = Racks mit dem Ziel an Bord → **erstes leeres Rack (Racknummer)** →
   alle in Racknummer; ganz in ein Rack (`Best`), sonst Kiste für Kiste über **alle Racks in Racknummer**. Auf der Hull B
   landet heute das dritte Ziel in Rack 3 (Steuerbord), ein 48-SCU-Ziel nach 1 + 2 das nächste auf 2 + 3. Hier muss die
   Seitenfolge greifen; ein Scan wartet weiter nur ohne Platz (P12).
4. **Zusammenspiel mit F10a:** Bahnen sind Layout im Rack (`Lanes.Of`, `CrateLayout`), Gruppen sind Zuordnung. `Assign`
   wählt ohne Bahnen (F5), `Built` legt Bahnen; das bleibt je Gruppe so. Folge **Seite → Rack → Bahn**; Hermes mit
   `portFirst`: Rack 1 Backbordbahn, Rack 1 Steuerbordbahn, Rack 2 Backbordbahn (innen), Rack 2 Steuerbordbahn, Bahnen
   wie F10a unverändert. Berührung nur beim Warten: F10a (`LanesFull`) und F10b (Seite) in derselben Schleife von
   `Sectioned`, wartende Missionen vereinigt.

**Plan (20.11.2026):**

1. **`SideGroups`** (Core/Planning, neu; A1: kein Treffer für Seitengruppen, `RackSide`/`RackPosition` nutzt der Planer
   nicht): `Of(racks, sides)` → Gruppen in Füllfolge `[erste Seite, zweite Seite, Mitte]`, je in Racknummer, Mitte nur
   wenn vorhanden; **null** bei `None` oder ohne Racks auf beiden Seiten (F3). Seite aus `Rack.Bounds` (P1). Dazu die
   Kandidatenfolge: Racks mit dem Ziel (in Gruppenfolge), dann je Gruppe ihr erstes leeres Rack und ihre übrigen. Mit
   einer Gruppe = heutige Folge (byteidentisch per Konstruktion).
2. **Zuordnung gruppenweise** (`RackAssignment`, `Assign` je Gruppe):
   - Ziele in **Legendenfolge** (P2). Probe = Machbarkeitsprüfung (Modus `Feasible`, Kappen offen) nach SCU- und
     `CanHold`-Vorfilter, danach ein `Assign` je Gruppe mit ihren Zielen.
   - `sideOverflow = true`: Ziele nehmen die erste Seite, solange sie passen; das erste, das nicht ganz passt, wird
     geteilt (so viele Kisten wie die Seite nimmt, Linien wie D64 Prio 1b, größte zuerst; scheitert die Probe, die letzte
     Kiste raus), Rest und alle späteren auf die nächste Seite, zuletzt Mitte.
   - `false`: jedes Ziel ganz in die erste Gruppe, die es nimmt (First Fit; ein späteres kleines geht zurück auf Seite 1,
     wie F10a). Passt es in keine, wird es danach wie mit `true` gelegt und als kreuzend gemeldet.
   - Rückfall wie heute: liefert eine Gruppe kein `Assign`, `FirstFitDecreasing` in Seitenfolge über alle.
3. **`PlanStops`**: mit Gruppen die gruppenweise Zuordnung, sonst unverändert. Ohne Missionen (`Plan(Ship, stops)`,
   CLI) bekommen kreuzende Ziele den vorhandenen Hinweis `Rack N: X crosses sides – side overflow off`.
4. **Warten ohne Überlauf** in `Sectioned` (nach Plan, PlanKeeping, Re-plan): mit `byDestination`, `sideOverflow =
   false` und Gruppen ist auch jedes Ziel kreuzend, dessen geplante Kisten in Racks zweier Gruppen liegen. Seine
   Missionen warten (außer mit Geladenem/Geliefertem, K6.4, wie F10a), die verlassenen Racks werden ohne sie neu gelegt
   (Geladenes als Hindernis, Bahnen wo vorhanden), Hinweis neu `Rack N: sides full – a mission waits (side overflow
   off)`. Kapazitätszeile unverändert, Wartezeile wie F10a-P2 (F7).
5. **Erfassen und Missionsweise** (`LayStop`, `FirstFitDecreasing`, damit `TryPlace`, `Keeping`, `RetryUnplaced`,
   `BackOnBoard`): Kandidaten in Seitenfolge, Kiste für Kiste über die Racks in Seitenfolge. Mit `sideOverflow = false`
   und `byDestination` zuerst Kiste für Kiste innerhalb je einer Seite, dann über alle (P6). Verschieben bleibt ohne
   Seiten (F5).
6. **Hinweise:** `PerRackText`, `SectionNotes.PerRack` und der Block in `LaneNotes` entfallen (A13: Schreiber von
   `NoticeSource.Sections` gegrept, Zusammenspiel mit `LanesFull`/`Crosses` getestet). F10a war nie released, keine
   Nutzerdatei trägt den Text; ein gespeicherter fällt in `Notes` weg (unbekannter Wortlaut).
7. **App:** nur der Tooltip von „Sides“ (`MissionEditorWindow.xaml:152`) nennt beide Bedeutungen.
8. **Docs:** D117 (Optionen: Seite als Tie-Break in D64, Seite als Prio 0, gruppenweise; Gegenargumente aus F4, dazu
   Legendenfolge zwischen den Gruppen, Hull A mit kleinen Zielen, Probenkosten; Messwerte), Addenda D41, D64, D89, D116;
   `plan-format.md`, `mission-format.md`, CLAUDE.md; Wiki „Layout“ ein Satz auf lokalem Branch `f10a-lanes`.
9. **Commits:** 1 = `hull_b` einfrieren + rote Tests; 2 = `SideGroups` + Zuordnung + `PlanStops`; 3 = Erfassen; 4 =
   Warten + Hinweise; 5 = Tooltip; 6 = Docs + Messung.

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:**
- P1 **Seite** aus `Rack.Bounds`: wer die Mittellinie nur berührt, gehört zu seiner Seite; Mitte nur bei
  `min.y < 0 < max.y`. Gegenargument: der Nullpunkt ist der der Datei (D90), nicht zwingend die Schiffsmitte (Hull B:
  y ≈ 0,5); die Seiten stimmen trotzdem mit `position.side`.
- P2 **Ziele in Legendenfolge** auf die Seiten (wie F10a-P2 und wie der Scan sie bringt: Scan und späterer Plan geben
  dieselbe Seite). Gegenargument: D64 ist eingabe-unabhängig; mit Seiten bestimmt die Legendenfolge, welches Ziel
  Backbord liegt (innerhalb einer Gruppe bleibt D64 unabhängig).
- P3 **`sideOverflow` wirkt nur mit `byDestination`** (wie F10a-P3 und wie der Editor die ComboBox aktiviert); die
  Seitenfolge der Racks wirkt auch ohne Grouping.
- P4 **Seite voll heißt Kapazität:** mit `true` teilt ein Ziel lieber ein Backbord-Rack, als ein leeres Steuerbord-Rack
  zu nehmen (F1; D64 Prio 2 nur innerhalb der Gruppe). R3 nutzt Ziele à 16 SCU, ein Fall 3 × 8 SCU zeigt das Teilen.
- P5 **Warten im Durchgang `Sectioned`**, nicht in `PlanMissions`: ein Ort für alle Plan-Wege; verlassene Racks werden
  neu gelegt, nicht neu zugeordnet. Gegenargument: die übrigen Ziele der Gruppe liegen, wie D64 sie mit dem kreuzenden
  Teil fand.
- P6 **Erfassen ohne Überlauf** bevorzugt eine Seite, wartet nie bei Platz (P12 sinngemäß); `RetryUnplaced` (Undo/Redo)
  kann eine wartende Mission seitenübergreifend legen, bekannt aus D116, der nächste Plan legt nach der Regel.

**Tests (erst rot):** `SideRackTests` + `FrozenShips/hull_b.json` (Kopie der Baseline, A10). R1 Hull B `portFirst`,
byDestination, 2 × 96 SCU → nur Backbord; `starboardFirst` gespiegelt. R2 dritte 96 SCU: `true` 64 Backbord + 32 auf
Rack 3, je Block; `false` ganz Steuerbord; `false` über beide Seiten → Mission wartet, Hinweis, Kapazität gleich. R3
Hull A 2 × 16 → Racks 1, 2, drittes → Rack 3; 3 × 8 → alle Backbord (P4); `none` Goldens byteidentisch. R4 Hermes-artig
synthetisch: Seite → Rack → Bahn. R5 Mitte synthetisch: zuletzt, mit `false` ganze Ziele. R6 Scan Hull B: neues Ziel
Backbord solange Platz (48 + 48 → Rack 5 statt 3), Nachschub dort, sonst P12; `false` innerhalb einer Seite zuerst. R7
Default: alle Goldens und B17-Schnappschuss mit `none` byteidentisch; `sides ≠ none` ohne Seiten (RAFT, C2,
Caterpillar) wie F10a. R8 Eigenschaftstest (Seed fest, Hull B, 1–8 Ziele). R9 Hinweise (A13). Messung: Knoten und
Laufzeit je Gruppe, Hull B/Hull C `none` gegen `portFirst`.

**Gate Martin am Aufzug (SC vorn, Hull B):** Editor → Layout: **Grouping** By destination, **Sides** Port first,
**Side overflow** Cross over. Zwei Missionen scannen, zusammen ≤ 256 SCU und je > 32 SCU (etwa 96 + 96) → beide auf den
Backbord-Racks (1, 2, 5, 6 …), Steuerbord leer. Dritte Mission über die 256 SCU (etwa 96) → ihr Rest liegt Steuerbord
(64 + 32). **Sides: None** + **Plan** → das heutige Bild. Optional **Side overflow: Wait** + Plan → die dritte ganz
Steuerbord.

**Freigabe 20.11.2026:** P1–P6 ratifiziert, D117. **E1:** Knotenbudget plan-weit wie heute (1 Mio. D64-Knoten für alle
Gruppen und Proben zusammen, nicht je Gruppe multipliziert); Probe mit eigenem kleinem Deckel nach dem Vorfilter.
Abbruch, wenn `portFirst` auf Hull B oder Hull C mehr als 2× die Laufzeit von `none` braucht – dann Zahlen hierher.
Commit 1 (`hull_b` einfrieren + rote Tests) zuerst.

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
- Wiki-Branch `f10a-lanes` (lokal, `ea9252a` auf `b17-section-block`: „Plan and editor“, Layout mit Sides und Side
  overflow, Missions-JSON Schema 5, F10a) statt `b17-section-block` nach `master` mergen (`--no-ff`, CC; enthält B17),
  Martin pusht. **Auslöser: der nächste Tag** (plan.json 11 und Missions-JSON 5 kommen erst mit dem Release).
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
