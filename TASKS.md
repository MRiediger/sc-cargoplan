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
| Backlog II (K11, B16, B17, F10a, F10b) | 19.–20.11.2026 | D116–D117 | [backlog-ii.md](docs/tasks-archive/backlog-ii.md) |
| Release v0.7.0 | 20.11.2026 | – | [release-v0.7.0.md](docs/tasks-archive/release-v0.7.0.md) |

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

## Phase 13 – Cargo-Routen (gestartet 21.11.2026, D118–D119)

**Zielbild:** CargoPlan bekommt einen Routenplaner als Schicht über den F3-Scans: Beobachtungen je Template aus den
Scans (U3), Hafen-Matrix, Loop-Ranking, Streckenkarte und eine Route mit Platzhaltern. Gespeist aus den festen Hauling-Templates des Spiels und den
Stationspositionen, beides aus dem Data-Release von `sc-cargoplan-shipdata`, kein Netzzugriff zur Laufzeit (D118, D119).
Design: [`docs/design/cargo-routes.md`](docs/design/cargo-routes.md) (Stand 20./21.11.; wo es abweicht – OCR-Engine
§4.5, `hub-profiles.json` §4.4/§7.5, `distances.json` als Gm-Matrix §9.3 –, gelten D118, D119 und
[`docs/hauling-data-format.md`](docs/hauling-data-format.md)). Zahlen und Tabellen stehen im Design, nicht hier.

**Invarianten (gelten für jeden Task der Phase):**
1. Der heutige Ablauf bleibt: F3 im Offers-Tab, F3 im Accepted-Tab, Ladeplan. Kein neuer Pflichtschritt.
2. Der Planer ist additiv. Ohne Templates, ohne Beobachtungen, ohne Routen-Reiter funktioniert alles wie heute.
3. Game.log-Lesen ist keine Voraussetzung (Backlog mit Auslöser).
4. **Der Planer kennt keine Verfügbarkeit.** Er rechnet immer mit allen Templates des Boards (Rang/System/Area); was am
   Terminal steht, sieht der Spieler. Keine „vergeben“-Zustände, keine Alternativ-Logik (Design §8.3).

Dazu aus D118: plan.json bleibt in der ganzen Phase auf Schema 11; Beobachtungen, Route und Platzhalter liegen in
`routes.json`. Ein Task, der plan.json doch braucht, bekommt einen eigenen D-Eintrag.

**U3 (Martin, 23.11., D118-Addendum):** Spieler-Ablauf = Reiter „Routes“ → Route aus Matrix/Karte (Templates) →
Platzhalter je Template, erkannt an Titel + Reward → Reiter „Cargo“ → Vertrag am Terminal suchen, annehmen, F3 im
Accepted-Tab füllt den Platzhalter → Wizard über die Stationen. **Kein Pool gescannter Angebote, kein Status
`Scanned | Accepted | Done`.** Ein Scan liefert über die Templates hinaus nur **Beobachtungen** (reale Endpunkte der
systemweiten Templates mit leeren Pools, Red-Wind-Rewards); jeder lesbare Scan schreibt sie, unabhängig vom Reiter.

**Zwei Repos:** Templates und Positionen schreibt der Monatslauf in `sc-cargoplan-shipdata` (**SD14**, Entscheidung und
Job dort, eigener Prompt), Vertrag ist [`docs/hauling-data-format.md`](docs/hauling-data-format.md). **Baurhythmus:**
SD14 ist durch: `ships-2026.11.2` trägt `hauling` (veröffentlicht 23.11., Manifest `ba1606df68d4`); T13.2 (Loader)
pinnt es. Jeder Task auf eigenem
Branch `feat/p13-<kurz>`, Merge `--no-ff` nach Martins Abnahme; Gates mit „funktioniert“ brauchen SC im Vordergrund
(Lehre B10).

**Befund T13.0:** Die Kopfzeile des Overlays hat heute keine Reiter; T13.2 führt die Reiterleiste „Cargo | Routes“ ein
(„Cargo“ = heutige Ansicht, „Routes“ = Matrix, Ranking, Karte, Route; ein Reiter, kein Werkzeugfenster, D118). Bis dahin ist „Reiter offen“
(U1) ein Zustand im ViewModel, den Tests und der Entwickler-Schalter `--routes-open` (T13.1, fällt mit T13.2) setzen.

### [x] T13.0 Doku: Phase 13 anlegen (21.11.2026, abgenommen 21.11. mit K3)

**Warum:** Ohne Vertrag zwischen Pipeline und App kann SD14 nicht bauen, und ohne Task-Schnitt fehlt jedem Folgetask die
Grenze.
- Design unter `docs/design/cargo-routes.md`, inhaltlich unverändert
- `docs/hauling-data-format.md`: `hauling-templates.json` und `positions.json` (Schema 1), Manifest-Abschnitt `hauling`
- D118, D119; dieser Abschnitt; CLAUDE.md (Ansatz, Struktur)

**Akzeptanz:** alle fünf Punkte im Repo; D118/D119 im Hausformat mit Gegenargument; Format-Dokument mit vollständigem
Beispiel und Schema-Tabelle je Datei; `dotnet build` und `dotnet test` unverändert grün.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (21.11.2026, mit K1, K2, E1, E2):**
1. `hub-profiles.json` entfällt; `Board.For(system, areaKey, rank, faction)` als Pure Function in Core (T13.3).
2. Zwei Dateien `hauling-templates.json` und `positions.json` (K1: nur Positionen, die App rechnet die Distanz), flache
   Assets, beschrieben im Manifest-Abschnitt `hauling` (wie `holo`) – nicht in `ships.zip`, `files` oder `assets`: der
   Manifest-Reader von v0.6.0/v0.7.0 lässt in `files` nur `outlines.json` und `ships/**` zu und lehnte das Release sonst
   ab; `assets` hat keinen SHA.
3. Ortsnamen: Template ↔ Position per exaktem Namen (Pipeline sorgt dafür), Scan ↔ Position per B1-Schlüssel nur in C#
   (`DestinationNames.Key` zieht in T13.3 nach Core); kein Schlüssel in den Daten.
4. Leere Ortspools bleiben leer; die Pipeline füllt nichts auf.
5. Route und Platzhalter in `routes.json`, nie in plan.json.
6. Vier Invarianten: Design §0 plus §8.3.
7. K2: `hauling` fehlt oder `null` ist nie ein Fehler; ein gescheiterter Hauling-Job blockiert nie das
   Schiffsdaten-Release (Vorgabe für SD14 im Format-Dokument).
8. E2: „Reiter geöffnet“ = der Routen-Reiter ist der ausgewählte Reiter der Kopfzeile, auch im Lock-Modus (D118).
9. K3 (Abnahme): der Planer ist ein Reiter, kein Werkzeugfenster; Reiterleiste „Cargo | Routes“, Platz in oder unter
   der Kopfzeile entscheidet T13.2 (D118).
10. **T13.0a (22.11.2026, Befunde aus H1 im Shipdata-Repo, Freigabe 21.11., abgenommen 22.11.):** Addendum zu `docs/hauling-data-format.md`
    mit V1–V8 und K1–K3 (Quelle Rohrepo, `trainee`, `xs`/`bulk`, Auftraggeber aus `MissionGiver`, `drop`, nichts
    geschätzt, Starmap-Name + `contractNames[]`, `legs ≥ 1`, Name eindeutig je System, `Star`), weiter Schema 1;
    Zeile in D119; Zahlen in T13.3.

### [x] T13.1 Core/Import: Titel, Beobachtungen, Routes-Weiche (Umschnitt U3, 23.11.; abgenommen 23.11.)

**Warum:** Die Identität eines Templates im Scan ist Titel + Reward, und was ein Scan über die Templates hinaus weiß
(reale Endpunkte, Red-Wind-Rewards), muss irgendwo landen. Ein Offer-Scan bei offenem Routes-Reiter darf nicht in den
Ladeplan (U1, U3, D118).
- **Vorprüfung ROI:** steht der Titel im heutigen ROI/Rohtext (`scans.log`, OCR-Samples aus S10.0)? Falls nicht,
  ROI-Erweiterung als T13.1a mit Gate wie S10.0 (1080p und 21:9)
- Titel-Parsing in `ContractTextParser`/`ScannedContract`: Rang, Größe, Direct, Auftraggeber (`<Rang> Rank - [Direct]
  <Größe> Cargo Haul` Covalex; Red Wind siehe Präzisierung); kennt Trainee, Extra Small und Bulk (T13.0a, V2/V3)
- `Observations` in Core (`CargoPlan.Core/Routing/`): Upsert je `ObservationKey(Contractor, Rank, Size, Direct, Reward)`,
  Mengen Pickups/Ziele, SCU-Band je Ware, Count, erste/letzte Zeit; Persistenz `routes.json` Schema 1 neben plan.json,
  `docs/routes-format.md`
- Weiche in `OverlayViewModel.ApplyScan`/`ScanRunner`: Routes ausgewählt + Offer → nichts in den Plan; Entwickler-Schalter
  `--routes-open` bis T13.2

**Akzeptanz:**
- ROI-Vorprüfung mit Zahlen festgehalten (Titel gelesen n/m je Auflösung); bei ROI-Erweiterung bleiben die Lieferfelder
  in `OcrSampleTests` bei 100 %
- Parser liefert Rang, Größe, Direct und Auftraggeber für die Titelformen aus RawText-Fixtures; ein unlesbarer Titel
  ergibt `null`, kein Fehler; der Inhaltsschlüssel (D101) ändert sich für keine bestehende Fixture (Golden über alle)
- Observations: zweiter Scan derselben Variante → ein Eintrag, Count 2, Bänder geweitet; Round-Trip `routes.json`;
  atomar geschrieben, eine unlesbare Datei wird beiseitegelegt, nie überschrieben (D106-Muster); Schema-Doku
- Weiche, sechs Zellen getestet: Offer/Accepted/Unknown × Reiter ja/nein; Reiter nicht ausgewählt → alle bestehenden
  Scan-Tests unverändert grün; ausgewählt + Offer → plan.json byte-gleich, Notice „Observed (Routes)“
- plan.json bleibt Schema 11; A13: alle Schreiber der Scan-Notice gegrept, Zusammenspiel getestet
- Gate am Terminal, SC vorne: mit `--routes-open` 10 Offers → 0 neue Missionen, plan.json-Hash unverändert, Beobachtungen
  mit Count-Summe 10; ohne Schalter F3 wie heute; Accepted → Plan wie heute + Beobachtung; Latenz p95 Taste → Notice
  gegen T10.3

**Präzisierung in der Planphase, ratifiziert durch Freigabe (23.11.2026, mit E1, E2):**
1. ROI-Vorprüfung: Titel steht im Rohtext, **kein T13.1a**. 3440×1440 20/20 (`set`, ROI + up2-gray) und 22/22 (c21–c42,
   Vollbild, Titel in der ROI-Box), 1920×1080 20/20 (`set-1080s`), `scan-probe.log` 227/227, `scans.log` 46/46.
2. **Red Wind, Befund Issue #22** (Fixtures `carbon_or_cut`, `potassium_or_cut`, `waste_or_smaller`): die Karte zeigt
   `<Rang> | Stellar [Direct] <Größe> Haul | to/from <Ort>`, der API-`DisplayTitle` lautet `<Rang> Hauler Needed for
   [Direct] <Größe> Shipment`; keine Aufnahme enthält die API-Form. Der Parser liest beide; in der |-Form ist Direct
   `null` (nicht geraten), `ObservationKey.Direct` ist `bool?`. `AreaLevel` (`Stellar | Planetary`) aus dem Titel als
   optionales Feld, nicht im Schlüssel.
3. Titel ganz oder gar nicht: fehlt Auftraggeber, Rang oder Größe, ist der Titel `null`; mehrere Titelzeilen → die erste.
4. `ScuSeen` = Band je Ware über die einzelnen Lieferzeilen (eine Zeile = ein Leg, wie `legs[].minScu/maxScu`).
5. Beobachtet wird nur View `Offer`/`Accepted`, nicht in harter Ungewissheit, mit Stopps, mit lesbarem Titel **und**
   Reward; sonst nur eine Log-Zeile.
6. Die Weiche greift nur bei View `Offer`; `Unknown` bleibt geschätzt im Plan (D102).
7. Ortsnamen wie vom Parser normalisiert, ordinal vereinigt; B1-Faltung erst in T13.3 (E1 dort).
8. `routes.json` unlesbar → beim ersten Speichern beiseitegelegt (`PlanStore.KeepUnreadable`), Log statt Notice.
9. `--routes-open` bleibt beim Neustart nach Update erhalten; fällt mit T13.2.
10. Beobachtung nach der Notice (Latenz Taste → Notice unverändert); scans.log bekommt `observeMs=`.
11. **E2:** jeder lesbare Scan schreibt `routes.json`, auch ohne Routes-Reiter (D118-Addendum).

**Stand 23.11. (Branch `feat/p13-observations`, abgenommen 23.11.):**
- Titel mit dem neuen Parser: S10.0 `set` (3440×1440) 20/20, `set-1080s` 20/20; `scans.log` (19.–20.11.) 46/46
  (43 Offer, 3 Accepted), alle 46 wären Beobachtungen; RawText-Fixtures 20/21 (ohne Titel nur `ice_and_food`).
- `OcrSampleTests` von Hand: Lieferfelder 138/138 (21:9) und 135/138 (1080p, nur c06 Textanimation) – wie T10.1a.
- D101-Schlüssel aller 21 Fixtures golden gepinnt (vor dem Parser aufgenommen), unverändert.
- Mutation ohne Weiche: 8 von 16 `RoutesSwitchTests` rot.
- Start mit `--routes-open` (Release, `--data-dir`): Log-Zeile „Routes counts as the selected tab“, kein `routes.json`
  ohne Scan.
- **Gate am Terminal (Martin, 23.11., SC vorne, Profilordner):** Weiche hält, plan.json unverändert; `routes.json` mit
  einem Eintrag Senior Medium 325 250, Count 3, Bänder geweitet, `direct: false`.
- **Latenz aus scans.log (n = 3, kein p95 möglich):**

  | Scan | Ergebnis | Taste → Rohtext | Taste → Notice | `ocrMs` | `observeMs` |
  |---|---|---|---|---|---|
  | 1, erster nach Start, ohne Schalter | Added #1 | 1 665 | 1 807 | 1 490 | 28 |
  | 2, erster nach Start, `--routes-open` | Observed | 1 663 | 1 672 | 1 500 | 14 |
  | 3, zweiter im selben Lauf | Added #1 (Accepted) | 1 538 | 1 789 | 1 464 | 3 |

  `observeMs` Median 14, max 28 ms, nach der Notice, also nicht in Taste → Notice. Gegen T10.3 (p95 1 389 / 1 423 ms,
  n = 24/22) nicht vergleichbar: zwei der drei sind erste Scans nach dem Start (T10.3: 1 694 ms), und die Mehrzeit
  steckt schon vor dem Rohtext (`ocrMs` ≈ 1,5 s), bevor T13.1-Code läuft; Rohtext → Notice 9 ms (Observed) bzw.
  142/251 ms (erster Plan-Schritt im Prozess). Eine Messung mit n ≥ 20 steht aus, falls die OCR-Zeit auffällt.
- Red-Wind-Kartenform bleibt: „Direct“ da → `true`, sonst `null` (Abnahme). T13.6-Text bleibt bis zur T13.6-Planung.

### [x] T13.2 App: Reiter „Routes“ (UI: „Plan“), Loader, Hafen-Matrix (abgenommen 23.11.)

**Warum:** Die U1-Weiche braucht einen Reiter, den der Spieler sieht, und der erste sichtbare Inhalt des Reiters ist die
Matrix: sie braucht nur Templates (U3; bisher Loader in T13.3, Matrix in T13.5).
- Reiterleiste „Cargo | Routes“ (Befund T13.0: heute keine Reiter); Platz in oder unter der Kopfzeile, Verhalten im
  Lock-Modus und Breite im Plan entscheiden (A9, ggf. D-Eintrag); ersetzt `--routes-open`
- Loader für `hauling-templates.json` und `positions.json` aus dem aktiven Datenstand (`ActiveShipData`, D113-Muster);
  Pin `ships-2026.11.2` (erstes Release mit `hauling`)
- Hafen-Matrix (Design §8.5): DataGrid Pickup × Dropoff aus Templates plus beobachtete Kanten aus den Beobachtungen
  (T13.1); Zelle = Anzahl / Σ aUEC / max SCU; Filter Rang, Größe, Direct, Auftraggeber, System als gemeinsames
  Filtermodell (T13.7 nutzt es)

**Akzeptanz:**
- Fälle aus dem Format-Dokument je mit Test: `hauling` fehlt/`null` → Reiter ohne Templates, keine Notice;
  unbekanntes Schema oder Prüfung gescheitert → UnknownFormat-Muster; Schiffe laden in jedem Fall
- Install des Downloads holt die zwei Assets mit Länge und SHA; ein gescheitertes Hauling-Asset kostet die Schiffsdaten
  nicht; D112-Addendum, Netz-Zeile in CLAUDE.md im selben Task
- **E1 (T13.0):** Whitelist in `tools/fetch-ship-data.ps1`, Layout in `build/publish.ps1` und `build/check-portable.ps1`
  nehmen die zwei Hauling-Dateien in die Baseline auf; ZIP-Größe vorher/nachher gemessen
- Datentest gegen den Pin: jeder Pool-Name hat eine Position
- Matrix: beobachtete Kanten sind von Template-Kanten unterscheidbar; leere Ortspools erscheinen nur über Beobachtungen;
  jede Filterkombination gegen eine Fixture getestet; Zelle ohne Reward zeigt keine aUEC-Summe
- Gate am Terminal, SC vorne: Reiter „Routes“ ausgewählt + Offer → plan.json-Hash unverändert; Reiter „Cargo“ → F3 wie
  heute
- Handling-Liste gegen main (Soll/Ist je berührter Geste)

**Präzisierung in der Planphase, ratifiziert durch Freigabe (23.11.2026, mit K1, E1):**
1. Beobachtete Kanten und „leere Pools nur über Beobachtungen“ gehen nach **T13.3**; die Matrix zeigt nur Templates.
2. **Befund Pin:** leerer Dropoff-Pool 162, leerer Pickup-Pool 229; in den lokalen Stellar-Blöcken (Stanton1–4) ist die
   Hub-Seite leer, nur 4 von 160 Covalex-Stellar-Templates haben beide Pools. **D121:** dort steht der Hub des Boards
   (Everus Harbor, Seraphim Station, Baijini Point, Port Tressler) für die leere Seite, im Tooltip markiert; nur stellar
   (für planetary/local kein Beleg). Systemweite `Stanton`-Templates (75 stellar) bleiben ohne Ort.
3. Filter „Area“ (local/planetary/stellar/interstellar), Default alle; System Stanton | Pyro | Interstellar, Default
   Stanton (Standort kennt die App nicht); Fraktion wirkt nur auf Red Wind.
4. Zeile unter der Matrix zählt nach Filter: „N templates not shown: the game data name no pickup or destination for
   them.“ (Default: 268).
5. Paare Pickup = Dropoff zählen nicht.
6. Unbekanntes `system`-Wort: Template/Position übersprungen wie die anderen Enum-Felder; ein Template, dessen Pool eine
   übersprungene Position nennt, mit.
7. Der Manifest-Leser wirft nie wegen `hauling` (auch nicht im Check auf ein neues Release); fehlen die Dateien nach
   einem Install → keine Hauling-Daten, keine Notice.
8. Datentests Boards 92/19/22 (bisher T13.3) und Distanzen 1,3/22,9 Gm (bisher T13.4) ziehen hierher.
9. Frame-Ziel `routes` für `--measure-frames` (Matrix scrollt statt Kamera).
10. Ladezeit synchron beim Start gemessen (Grenze 100 ms für einen Hintergrund-Task): 38–47 ms, bleibt synchron.
11. Filter als Umschalt-Buttons im Overlay-Stil (Klick vor, Rechtsklick zurück) und fünf Größen-Kästchen statt
    ComboBoxen: keine Popups über dem Spiel.
12. **K1:** Die Reiter heißen im UI „Cargo | Plan“; Code, Dateien, Settings-Schlüssel und D-Einträge behalten „Routes“,
    `routes.tab` = `"cargo" | "plan"`.
13. **E1:** kein UI-Text trägt „plan“ in beiden Bedeutungen: „Observed (Plan tab): … – cargo unchanged“, „Not observed
    (Plan tab): … – cargo unchanged“; Re-plan, Discard plan bleiben (T13.8/Wiki).
14. **Bild-Gate (Martin, 23.11.), Commit 11:** Matrix zu dicht. (1) Default Area = stellar (`RouteFilter.Default`, der
    Filter bleibt; ersetzt „Default alle“ aus Punkt 3, Zeile darunter dann 75); (2) L-Stationen mit Kurznamen `XXX-Ln` in
    Zeilen und Spalten, Hubs voll, voller Name im Tooltip; (3) Reihenfolge Hubs, dann L-Stationen je Planet Hurston,
    Crusader, ArcCorp, microTech, Rest alphabetisch, als `PlaceNames.Order` in Core (T13.7 nutzt sie); (4) Zelltext
    „13 · 1.1M“, max SCU in den Tooltip; „Direct: any“, „Any contractor“; Bildlaufleisten im Overlay-Stil (schmale Leiste
    jetzt auch waagerecht).

**Stand 23.11. (Branch `feat/p13-routes-tab`, 1324cee..e7488d8, abgenommen 23.11.):**
- Loader: Mechanik-Tests mit kleinen Fixtures (A10), mit abgeschalteten Checks 10 von 37 rot; Hash-Prüfung im Install
  mutiert → 1 rot; Hub-Regel, Selbstkante, Rang-Filter mutiert → 8 rot.
- Datentests gegen `ships-2026.11.2`: 587 Templates, 157 Positionen, 0 übersprungen; Everus 92, Crusader 19, ArcCorp 22;
  Everus Harbor → HUR-L1 1,29 Gm, → Baijini Point 22,88 Gm; vier Hubs sind Stanton-Stationen; mit Hubs bleiben in Stanton
  (stellar) genau die 75 systemweiten ohne Ort. Pin-Sprung: 135c jetzt mit Racks aus den Spieldateien
  (`ShipLoaderTests` nennt nur noch die 600i Executive Edition).
- Ladezeit beim Start (Debug): 587 Templates, 157 Positionen, 38–47 ms.
- Pack `publish.ps1 -Version 0.7.99`: `check-portable.ps1` grün (390 Dateien in `current\`), `check-cli-tarball.ps1` grün
  (328 Dateien); **ZIP 96 975 161 B gegen v0.7.0 96 791 336 B (+183 825 B ≈ +0,18 MB)**, Tarball 66 540 399 gegen
  66 381 450 B (+158 949 B); die zwei JSON-Dateien (0,61 MB) komprimieren gut. „Vorher“ ist das v0.7.0-Release, nicht
  `main` (T13.1 dazwischen).
- Frames auf dem Desktop, nicht über dem Spiel (Release): `empty` 0,9 % spät, `routes` 1,2 % spät (63 × 67 Zellen),
  ein Frame 254 ms.
- Bilder (CC, Desktop): Plan-Reiter mit Filterzeile, Matrix und Zeile darunter; Cargo-Reiter mit Schiff wie bisher.
- Sauberer Worktree auf 8529264: `dotnet build` 0 Warnungen, `dotnet test` 4747/4747 (Core 2212, App 2144, Import 357,
  Ocr 34); `dotnet format --verify-no-changes` grün nach dem IDE0062-Commit.

**Gates (Martin, 23.11.):**
- **Gates 1–5 grün:** Reiter wechseln entsperrt; sperren → Reiter bleibt, Click-through; Plan + Offer → „Observed (Plan
  tab)“, plan.json-Hash gleich; Cargo → F3 wie heute; Neustart mit Plan startet dort; Handling-Liste unten.
- **Frames über dem Spiel** (Release, 2271864): `routes` 63 × 67 Zellen 1,3 % spät (median 16,67 ms, max 267,5 ms,
  74,1 fps) gegen `empty` 11,2 % spät (107,9 fps) – Regel ≤ empty + 2 pp erfüllt.
- **Bild-Gate:** Matrix zu dicht → Präzisierung 14, Commit 11; danach neuer Screenshot bei 420 × 460 (CC, Desktop):
  Hubs zuerst, `HUR-L1` …, Zellen „6 · 327k“, Filter „Stellar · Direct: any · Any contractor“, schmale Leisten in beide
  Richtungen, Zeile „75 templates not shown“. Abgenommen 23.11. (Martin).
- **Handling-Liste gegen `main`:**

  | Geste | Soll (= main) | Ist Branch (erwartet) |
  |---|---|---|
  | Schiffsauswahl + Suche | Liste, Suche, Enter/Esc | gleich; mit Plan gewählt wechselt das Schiff im Hintergrund |
  | Split / Merge / Re-plan | wie main | gleich, auch mit Plan gewählt |
  | Stift (Editor) | öffnet den Editor | gleich |
  | Undo / Redo (+ Hotkeys) | wie main | gleich |
  | Reset view (+ Doppelklick) | Kamera zurück | gleich; mit Plan gewählt auf die verborgene Ansicht |
  | Zahnrad | Settings | gleich |
  | Schloss / toggleLock | sperrt, Click-through | gleich; Leiste bleibt sichtbar, Auswahl bleibt |
  | Exit | schließt | gleich |
  | Fenster ziehen | Kopfzeile, Rand, Notices | dazu freie Fläche der Reiterleiste |
  | Reiter klicken (neu) | – | entsperrt Wechsel; gesperrt geht der Klick ans Spiel |

### [x] T13.3 Core: Board, Matching, Rezept

**Warum:** Erst mit den Templates kennt der Planer jedes Board ohne Anflug und kann einen Scan einem Template zuordnen.
- `Board.For(system, areaKey, rank, faction)`; Matching Scan → Template (Titel + Reward; Fallback Shape + Ware +
  SCU-Band); Rezept-Generator als Pure Function (Kapazität und Racks des gewählten Schiffs, Prüfung per `LoadPlanner`)
- **Red Wind nie über den Titel matchen; Rang + Größe + Stopps + Ware + SCU-Band** (Befund T13.1: API-`DisplayTitle` ≠
  Kartentitel, Issue #22)
- Settings „Routes“: Rang, Fraktion; Hinweis im Reiter („fits“ / „too big – take the other one“)
- Alle 75 systemweiten Covalex-Stellar-Templates haben leere Drop-off-Pools, kein Stellar-Template nennt einen Hub:
  systemweite Kanten entstehen nur aus Scans (Design §8.3; T13.0a, H1-Befund 9)
- Observations beim Laden über `DestinationNames.Key` falten (Mengen vereinigen), idempotent, kein Schema-Bump
  (E1 der T13.1-Freigabe, Folge von Präzisierung 7)
- **Aus T13.2:** beobachtete Kanten in der Hafen-Matrix, von Template-Kanten unterscheidbar; die systemweiten Templates
  ohne Ort erscheinen nur über Beobachtungen; die Board-Hubs (D121) gelten auch für Board und Matching

**Akzeptanz:**
- `DestinationNames.Key` (mit `ContractText.AnchorForm`) nach Core verschoben, nicht nachgebaut (A1)
- Mechanik-Tests mit Test-Fixtures (A10); Datentests gegen den Pin: Everus/Senior-Board 92 (Stanton + Stanton1),
  Crusader 19, ArcCorp 22 (T13.0a, statt 18–21 aus Design §7.3; als Zählung seit T13.2, hier über `Board.For`), kein
  B1-Schlüssel doppelt je System über `name` und `contractNames`
- Matching: jede Covalex-Offer-Fixture eindeutig zugeordnet oder mit Grund nicht; lokal gegen die Everus-Serie
  mindestens 78/84 (Design §7.3)
- settings.json bleibt Schema 3 (optionaler Abschnitt wie `shipData`)
- Beispiele in `docs/hauling-data-format.md` durch echte Einträge aus dem ersten Data-Release mit `hauling` ersetzt
  (Nit der T13.0-Abnahme; Design §9.3 nennt nur Distanzen, keine Koordinaten)
- Addendum in `docs/hauling-data-format.md`, Feld `title`: „as the API shows it; Covalex equals the card, Red Wind does
  not“ (Befund T13.1, Issue #22)

**Schnitt (Startprompt 23.11.):** T13.3a Daten zusammenführen (B1 nach Core, Faltung, Board, Matching, beobachtete
Kanten, Settings-Abschnitt), Zwischenabnahme; T13.3b Rezept und Prüfung vor Annahme. Ein Branch `feat/p13-board`,
`--no-ff` nach 3b.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (23.11.2026, „Freigabe T13.3 (a und b) mit K1“):**
1. **L-Kurzform (Martin):** die Karte schreibt Lieferungen an „Green Glade Station“, positions.json kennt nur „HUR-L1
   Green Glade Station“ (ohne `contractNames`). `Positions.Resolve` vergleicht den B1-Schlüssel auch mit dem Namen ohne
   L-Code – ein Fakt der Karte, nicht der Daten, also C# (K1 von T13.0a); `contractNames` bleibt für Token-Formen.
   Datentest: voll, ohne Code, ohne Code und „Station“, `contractNames` – kein Schlüssel nennt zwei Orte eines Systems.
2. Matching nimmt eine `Observation` (Scan und gefalteter Eintrag gleich), Kandidaten = alle Stellar-Boards des Systems
   über alle Ränge (`Board.Matchable`); der Rang kommt aus dem Titel.
3. Stopps: die Mehrfachseite nennt `min(stops, Pool)` Orte bei einem Scan, mindestens so viele bei mehreren.
4. Faltung: gleicher B1-Schlüssel → der längere Name (`DestinationNames.Preferred`), Treffer je Name kennt die Datei nicht.
5. Echte Zwillinge (zwei systemweite Paare, nur die uuid verschieden) → `Ambiguous`, keine Kante.
6. Rezept-Hub (Martin): Button „Hub“ in der Filterzeile, nur bei Recipe, `routes.hub`, Default Everus Harbor, gilt auch
   für T13.4/T13.6.
7. Rezept-Kandidaten: lokale Templates mit Pickup am Hub, systemweite nur mit beobachteter Kante ab dem Hub; Bulk →
   Direct → Smalls; Rückläufer als Liste, nicht in der Abflug-Prüfung (Slot-Simulation T13.6).
8. „fits“ = das gescannte Template ersetzt die Rolle seiner Art im Rezept und `LoadPlanner` passt.
9. **K1 (Freigabe):** `ObservationKey` bekommt die Waren (Menge in `OcrText.AnchorForm`); 27 Schlüssel decken 64
   Templates, die sich nur in der Ware unterscheiden. `routes.json` Schema 2 ohne neues Feld (die Waren sind die von
   `scu`), Schema 1 gelesen: Einträge mit einer Ware 1:1, mit mehreren verworfen mit Logzeile. D118-Addendum.

**Befunde Planphase:** Everus lokal = **23** (nicht 22 wie im Startprompt: Rookie 11, Junior 5, Member 2, Experienced 3,
Senior 2); Port Tressler 89/20; Covalex-Schlüssel ohne Waren nicht eindeutig (s. K1); eine Red-Wind-Lieferzeile trägt
mehrere Legs (Carbon 200 SCU aus zwei Pickups, Legs je 25–175); keine Red-Wind-Fixture hat einen lesbaren Reward.

**Stand 3a (23.11., Branch `feat/p13-board`, 5e30e98..):**
- Commits: B1-Umzug ohne Verhaltensänderung (Import 357 Tests unverändert, Golden-Keys grün); K1 + Schema 2 + Faltung
  (Mutation ohne B1-Gruppierung: 3 rot); `Positions.Resolve` (Mutation ohne L-Code-Form: 18 rot); `Board.For`;
  `TemplateMatch` (Mutation Schlüssel ohne Waren: Eisen + Quarz → eine Beobachtung, `None`); `ObservedEdges` + Matrix;
  Settings-Abschnitt „Plan tab“.
- **Boards gegen den Pin (Senior):** Everus 92 (lokal 23), Seraphim 88 (19), Baijini 91 (22), Port Tressler 89 (20);
  Pyro `Solar` 31 je Fraktion, 62 beide.
- **Orte der 21 RawText-Fixtures:** alle aufgelöst (auch „Melodic Fieids“, Pyro-Orte aus Issue #22) außer fünf vom
  Capture abgerissenen Namen in drei Fixtures („Everus H“, „HUR-L1 Green Gle“, „HUR-L3 Thundg“, …).
- **Matching-Quote:** Covalex-Fixtures **5/7** eindeutig, 2 mit benanntem Grund (abgerissene Orte); Red-Wind-Fixtures
  3/3 `Ambiguous` mit je 2 Kandidaten (HH/CFP-Zwillinge); Red Wind mit API-Titel im Scan = gleiche Kandidaten wie mit
  Kartentitel. **Martins scans.log (19.–20.11., von Hand):** 44/46 Scans eindeutig, 2 `None` (abgerissenes „HUR-“), 0
  mehrdeutig; nach dem Aufzeichnen 10/11 Einträge – über den 78/84 aus Design §7.3.
- **Präzisierung zur Zwischenabnahme (CC, bei der Umsetzung):** Faktions-Zwillinge (Templates gleich bis auf die
  Fraktion) teilen eine mehrdeutige Beobachtung: jeder Red-Wind-Scan ist so ein Paar, sonst erschiene nie ein
  beobachteter Red-Wind-Reward; der Fraktionsfilter zeigt die eigene Seite (D122).
- Settings-Abschnitt heißt im UI „Plan tab“ (K1 von T13.2: UI sagt Plan), Zeilen „Your rank“, „Your faction“ („Both
  sides“); Filter-Button und Fenster zeigen die Änderung des anderen (Test).
- D122, D121-Addendum, D118-Addendum (K1); `hauling-data-format.md` mit echten Einträgen aus `ships-2026.11.2`,
  `title`-Hinweis, Addendum T13.3; `routes-format.md` Schema 2; `settings-format.md`.
- **Hänger:** ein voller `dotnet test`-Lauf blieb einmal im App-Testhost stehen (10 min, CPU konstant); drei folgende Läufe
  mit `--blame-hang-timeout` grün in 11–17 s, nicht reproduziert. Beobachten.

**Zwischenabnahme 3a (24.11.2026, erteilt) – K und drei E vor 3b, Commits 9–12. Präzisierung in der Planphase,
ratifiziert durch Freigabe („Freigabe für Commits 9–12 wie geplant“):**
- **K (Commit 9) – Matrix scrollt per Thumb und Shift+Rad.** Befund mit echter Eingabe (`SendInput`, UIA, ohne Spiel):
  von 8 px der Slim-Leiste griffen nur die mittleren 4, die 2 px Track-Einzug links/rechts trafen nichts → DragMove
  (Fenster wanderte); Rad vertikal scrollte überall; Shift+Rad scrollte vertikal. Fix: Track ohne Einzug, Thumb mit
  Hit-Test-Hintergrund über die volle Breite, Grip 2 px innen (gilt auch für Legende und Schiffsliste); Shift+Rad
  seitwärts (`PortMatrixGrid.WheelSideways`), Rad ohne Modifier bleibt vertikal, auch ohne vertikalen Überlauf (Martin);
  Kipprad → Backlog. Nachher: alle 8 Spalten greifen, kein Fenster wandert, Shift+Rad 1,6 % je Raste. Test:
  `PortMatrixGridTests` (Shift+Rad). D120-, D80-Addendum.
  **Rückfrage Martin (24.11.): greift die vertikale Leiste (Legende) mit demselben Einzug?** Gemessen mit echter Eingabe,
  XAML vor Commit 9: nein, auch die Legende griff nur in den Spalten 3–6, Spalten 1, 2, 7, 8 → DragMove (24 px); beide
  Zweige des Templates sind bis auf die Orientierung gleich, der Einzug ist die Ursache für beide. Nach Commit 9: alle
  8 Spalten greifen, kein Fenster wandert. Nachweis ist der Gate (Martin), der Pixel-Treffertest `SlimScrollBarTests`
  ist wieder entfernt (Folge-Commit, Commit 9 liegt unter 10–12).
  **Befund Martin über SC (24.11.):** ohne Spiel beide Leisten einwandfrei, über SC springt der Track-Klick, Thumb-Drag
  und Rad gehen nicht → Fokus/Capture, kein Styling. Diagnose aus dem Code: `WS_EX_NOACTIVATE` dauerhaft seit T3.6,
  Entsperren schaltete nur `WS_EX_TRANSPARENT`. **Präzisierung in der Planphase, ratifiziert durch Freigabe („Freigabe
  mit Änderung“):** Margin-Fix bleibt; `--log-input` (b0b0816); Ex-Style aus Sperre × Reiter (a52bc32): gesperrt
  TRANSPARENT + NOACTIVATE, entsperrt Cargo NOACTIVATE wie T3.6, entsperrt Plan keins; nie `SetForegroundWindow`;
  Test am echten HWND, vier Kombinationen; Thumb/RepeatButton lassen los, wenn die Taste oben ist ohne WM_LBUTTONUP
  (`ScrollCaptureGuard`, D45-Muster). Desktop: Plan-Reiter entsperrt Ex-Style `0x000C0108`, nach dem Klick Vordergrund =
  Overlay, Drag auf allen 8 Spalten. D11-Addendum.
- **E1 (Commit 10) – „no hauling template form“ getrennt von „title unreadable“.** Anlass: Ling-Family-Hauling-Angebot
  in Martins scans.log (24.11.), Titel ganz gelesen, Meldung „title unreadable“. `TitleMiss` im Parser: andere Form nur
  bei positivem Zeichen (Auftraggeber gelesen, weder Covalex noch Red Wind, keine titelähnliche Zeile), sonst
  unlesbar; ein Text für Notice und Log (`ScannedContract.NotObservedReason`). Alle Fixtures ohne Titel bleiben
  „unreadable“. D120-Addendum.
- **E2 (Commit 11) – Hub-Hinweis einmal je Tooltip.** Kopfzeile darunter „⌂ pickup: the board's hub, not in the game
  data“ (bzw. drop-off), betroffene Template-Zeilen mit „⌂“ (Martins Hinweis: kurzes Zeichen je Zeile, Kopf erklärt
  einmal). „⌂“ kommt aus der WPF-Ersatzschrift, gerendert geprüft. D121-Addendum.
- **E3 (Commit 12) – routes.json mit den Namen der Positionen.** Anlass: Martins routes.json, Pickup „HUR-L3 Thundering
  Express Station“, Ziel „Thundering Express Station“. `Observation.Named(Positions)` im System des Auftraggebers, dann
  gefaltet; `RoutesBook.Load(path, positions)` (Pflichtparameter), beim Aufzeichnen benannt, beim Laden nur im
  Speicher. „Contractor ↔ System“ als eine Tabelle `ContractorSystems` mit Kommentar (vorher doppelt in `TemplateMatch`
  und `ObservedEdges`, Martins Hinweis), Datentest rot bei einem Template mit fremdem System (Mutation Covalex → Pyro:
  rot). D118-Addendum, `routes-format.md`.
**Stand 3b (24.11., Branch `feat/p13-board`, d5d2253..):**
- Commits: `Recipe.For` + `LoadPlanner.Lays` (d5d2253); Plan-Reiter Matrix | Recipe mit Hub-Button, `routes.hub`
  (90a7fec); fits / too big beim Offer-Scan, `RecipeFit.Of` (2651a38); Doku (D123, `settings-format.md`, CLAUDE.md).
- **Präzisierung in der Planphase, ratifiziert durch Freigabe** (bei der Umsetzung, zur Abnahme): (1) „passt“ =
  `LoadPlanner.Lays`, der First Fit des Planers (D54), nicht `Plan` mit Suche: die Suche kostete auf der Hull B rund
  1 s je Frage (Hull-B-Test 5 s), mit `Lays` Median 44–48 ms für das Everus-Rezept (Release); wo `Lays` ja sagt, legt
  `Plan` es auch, wo nein, könnte die Suche selten noch eine Lage finden (Test: `Lays` = `Plan` auf fünf Fällen);
  (2) der Hub kommt aus `Board.Hub`, kein eigener Parameter; (3) höchstens 10 Verträge (`Recipe.MaxContracts`,
  Design §3/§4.1); (4) in der Recipe-Ansicht verschwinden die reinen Matrix-Filter (Area, Direct, Contractor, Faction,
  Größen) und die Matrix-Hinweise; (5) `RecipeFit.Of` nimmt die gescannten Ziele, wo die Daten keine nennen
  (systemweit); (6) die Commit-Nummern 9–12 des Plans waren vergeben, 3b zählt weiter.
- **Zahlen (Pin `ships-2026.11.2`, Everus, `RecipeTimingTests`):** Hull B Senior/Master: Senior Medium 432 SCU (Bulk),
  Rookie Direct Small, drei Smalls, 594,500 aUEC, 512/512 SCU, 50 Kisten, vier Rückwege „accept at HUR-L1…L4“; erster
  Aufruf 142 ms, Median 44–48 ms. Hull A: Direct + zwei Smalls, 64/64. Hull C: leer („nothing from this hub fits the
  ship“) – ihre Racks nehmen nur 32er (`minCrateSize: 32`), stellare Covalex-Verträge höchstens 16er.
- Mutation: `Lays` durch Summe ersetzt → Freelancer-Test rot (drei 16er, 48 von 66 SCU, das 9×2×3-Rack legt zwei).
- **Handling gegen main:**

  | Geste | Soll (= main) | Ist Branch (erwartet) |
  |---|---|---|
  | Filterzeile Plan-Reiter | Rang, System, Area, Direct, Contractor, Faction, Größen | vorn neu „Matrix/Recipe“; im Recipe „Hub: …“, Rang, System, die übrigen verborgen |
  | Klick ins Overlay, entsperrt | nie aktivierend (T3.6) | Cargo-Reiter gleich; Plan-Reiter aktiviert (D11-Addendum T13.3a) |
  | F3 auf Angebot, Plan-Reiter | „Observed (Plan tab): … – cargo unchanged“ | dazu „ · fits as …“ / „ · too big for …“ |
  | Settings-Fenster „Plan tab“ | Rang, Fraktion | gleich (Hub nur über den Button) |
  | Alles andere | wie main | gleich |

- **Gate 3b (Martin, über SC):** (1) Scroll: Thumb, Rad, Shift+Rad im Plan-Reiter; Cargo-Reiter am Terminal abhaken →
  SC behält den Cursor (T3.6); Lock → click-through; `--log-input` für die Zeilen. (2) Red-Wind-Zwillinge in einer
  Pyro-Session. (3) Recipe-Ansicht für das eigene Schiff an einem Hub (Aussehen, Rollen plausibel, Hub-Button, Rang),
  F3 auf Angebote → „fits as …“ / „too big for …“ (eines aus dem Rezept, eines zu groß). Zahlen: Rezept-Laufzeit
  `RecipeTimingTests` (oben). Danach Abnahme → `merge --no-ff` → Martin pusht.
- **Gate 3b bestanden, Abnahme erteilt (24.11.2026, Martin)**, damit auch die Präzisierungen von 3b ratifiziert
  (`Lays` statt Suche, Hub aus dem Board, 10 Verträge, Matrix-Filter in der Recipe-Ansicht verborgen, gescannte Ziele
  für systemweite Templates). Merge `--no-ff` nach main, Martin pusht.

### [x] T13.4 Core/App: Loop-Ranking (abgenommen 24.11.)

**Warum:** Die Kernfrage der Phase: welche Loop ist für mein Schiff die effektivste, auch an einem anderen Hub (Design §4.7).
- Kandidaten: lokaler Ring je Hub, Direct-Einzellauf, systemweite Loops
- Kennzahlen: aUEC/Umlauf, Landungen, Kisten, Sprünge > 10 Gm, aUEC/Landung, aUEC/h mit Zeitparametern (Settings,
  Defaults 10 min / 20 s / 4 min); Ringreihenfolge per Permutation ≤ 5 Stationen aus `positions.json`; Anreise-Abzug bei
  Hub ≠ Standort

**Akzeptanz:**
- Ringreihenfolge = kürzeste Distanzsumme, Gleichstand → Station mit Ein-Ziel-Vertrag zuerst (Test)
- Datentest gegen den Pin: Everus Harbor → Baijini 22,9 Gm, → HUR-L1 1,3 Gm (±0,1, Design §9.3) – seit T13.2 erfüllt
  (`BaselineHaulingDataTests`)
- Ohne Reward (Red Wind ungescannt) zeigt der Kandidat Struktur, nie aUEC
- Ranking kippt mit den Zeitparametern (Test mit zwei Parametersätzen)

**Präzisierung in der Planphase, ratifiziert durch Freigabe (24.11.2026, „Freigabe T13.4 mit K1“; Startprompt und
Martins Antworten auf vier Rückfragen):**
1. **Sprünge:** Spalte „jumps (long)“ = Legs der Folge, davon lange (> 10 Gm), z. B. „6 (4)“; t_Sprung zählt nur die
   langen, kurze QT-Legs stecken in t_Station. §4.7 meint mit „Sprünge“ die langen (Everus 4 von 6 Legs).
2. **Direct-Einzellauf = hin + zurück, immer 2 Landungen / 2 Legs** (die Hub-Landung zählt wie beim Ring; §4.7 war hier
   inkonsistent), Rückläufer dazu, wenn `LoadPlanner` ihn legt; Ziel = fernster Ort des Pools (Worst Case) bzw. der
   fernste beobachtete; Commodity-Zwillinge eine Zeile „×n“. Golden-Direct lange Sprünge **2** (Everus ↔ HUR-L3
   25,69 Gm), nicht 1 – §4.7 zählte nur den Hinweg (bestätigt mit der Freigabe).
3. **Pyro = Ring je Station** (Red Wind `Solar`), nur Struktur, aUEC nur beobachtet („–“, „+“ für Teilsummen); Fraktion aus
   Settings, bei „Both sides“ je Faktions-Zwilling einer. **Der System-Filter gilt auch für Loops** (Stanton → keine
   Pyro-Zeilen, Interstellar → „No loops for this system.“).
4. **Zeilenklick = nur Ansicht:** Stanton-Zeile (Ring, Direct) → Recipe dieses Hubs für die Sitzung („Hub: Baijini
   Point (from Loops)“), `routes.hub` nur per Hub-Button. **K1 (Freigabe):** `FitOf` („fits / too big“) rechnet weiter
   gegen `routes.hub` – der Scan passiert am gespeicherten Hub.
5. Ringstationen = Vereinigung der Drop-off-Pools der Hinweg-Rollen (Worst Case wie das SCU-Band oben), Pool > 5 → die
   Orte des Rezepts; die Passprüfung des Rezepts (erste Stopps, D123) bleibt.
6. `Loops.For` liefert die Struktur, `Loops.Rank` sortiert mit Zeiten und Sortierung ohne neuen `LoadPlanner`-Lauf.
7. Anreise = Spalte „travel“ (Gm, „–“ am gleichen Hub, „travel: Pyro“), nicht in aUEC/h (D124: Sessionlänge unbekannt).
8. LEO-Ring ab `routes.hub` über die vier Hubs, systemweite Verträge nur über beobachtete Kanten zu einem Hub voraus,
   Greedy je Station mit dem, was noch an Bord ist; Status „observed k/4 hubs · needs scans at <Hub>“.
9. Sortierung als Umschalt-Button „Sort: …“ in der Filterzeile (Bild-Gate entscheidet), `routes.loopsSort`.
10. Status: „templates“ | „observed n/m“ | „needs scans at …“ (blass, unten); Kisten mit Beobachtung = größter
    beobachteter Wert je Ware (`ObservedEdges.ScuOf`), Reward Covalex unverändert.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (bei der Umsetzung, zur Abnahme):**
- **Gleichstand ≤ 0,1 Gm** (Genauigkeit §9.3) statt Rechengenauigkeit: Seraphim L1 → L4 → L5 und L1 → L5 → L4 trennen
  < 0,01 Gm; mit der Regel ergibt sich §3s Folge. Eine Rundreise ist immer gleich lang wie ihre Umkehrung.
- **Pyro-Ringe:** je Rolle die nächsten Orte ihres Pools (so viele wie Stopps, bei Multi → Single die weiteren Pickups
  mit), höchstens 5 Stationen (`Recipe.Pick` mit Prädikat). Anlass: Red Wind `Solar` ist überwiegend Multi → Single mit
  11 Pickups und je einem anderen festen Ziel; die Vereinigung ergab Ringe mit 12–15 Landungen.
- **`FitCache`:** Antworten von `LoadPlanner.Lays` je (Schiff, Stopps) über Läufe, weil die App-Messung über 200 ms lag
  (Startprompt §3: erst cachen).

**Stand 24.11. (Branch `feat/p13-loops`, ff43ee2..):**
- Commits: `ObservedEdges.ScuOf`; `Recipe.At`/`Pick` ohne Verhaltensänderung; `Loops` + `LoopCandidate`; Settings
  `routes.times`/`routes.loopsSort` mit drei Slidern (+ Golden-Dateien der Migration nachgezogen); Plan-Reiter
  Matrix | Recipe | Loops mit Hintergrundlauf; `LoopsTimingTests`; `FitCache`; Doku (D124, D123-Addendum,
  `settings-format.md`, CLAUDE.md).
- **Golden gegen §3/§4.7 (Pin `ships-2026.11.2`, Hull B, Senior, Default-Zeiten, ohne Scans), nach dem Rückläufer-Fix
  unten:** Struktur exakt – Everus 6 Landungen / 4 lang, Baijini 6/4, Seraphim 4/3, Port Tressler 6/4, Direct Exp
  Direct Medium Everus → HUR-L3 2/2 (2 lang, 211 750 aUEC mit Rückläufer). aUEC/Umlauf: Everus 877 000 (**−13 %** gegen
  1,01 M), Baijini 959 500 (**−11 %** gegen 1,08 M), Seraphim 675 000 (**−23 %** gegen 875 k, außerhalb ±15 %), Port
  Tressler 1 028 000 (**+3 %** gegen ~1,0 M aus §4.7). Seraphim: Senior Medium 296 250, Rookie Direct Small 34 750, drei
  Smalls, Rückläufer 67 500 (Multi3, an CRU-L1, sammelt CRU-L4, CRU-L5), 42 000 (an CRU-L4), 34 750 (an CRU-L5); §3 nennt
  Senior 328k, Smalls und ein Rookie Direct Medium aus einem Scan und ~240k Rückfracht – hier Templates im Worst Case
  und der Greedy des Rezepts statt der gescannten Auswahl. Vor dem Fix: Everus 957 000 (−5 %), Baijini 1 109 000
  (+3 %), Seraphim 733 250 (−16 %), Port Tressler 1 173 000 – geschönt durch ein Template je Station mehrfach.
- **Folgen gegen §3 (Distanzsummen aus `positions.json` des Pins, Legs in Gm):**

  | Ring | Folge | Summe | Legs | lang |
  |---|---|---|---|---|
  | Everus, berechnet | L1 → L4 → L3 → L5 → L2 | **72,8752 Gm** | 1,29 · 12,25 · 22,25 · 22,25 · 13,54 · 1,29 | 4 |
  | Everus, §3 | L1 → L2 → L4 → L3 → L5 | **74,7592 Gm** | 1,29 · 2,58 · 13,54 · 22,25 · 22,25 · 12,85 | 4 |
  | Seraphim, berechnet = §3 | L1 → L4 → L5 | **72,4841 Gm** | 1,91 · 18,26 · 33,16 · 19,15 | 3 |
  | Seraphim, Umkehrung der Mitte | L1 → L5 → L4 | **72,4813 Gm** | 1,91 · 18,27 · 33,16 · 19,14 | 3 |

  Everus: die berechnete Folge ist 1,88 Gm kürzer als §3 (§3 fliegt L1 → L2 zuerst, dann einmal quer über L4). Seraphim:
  L1 → L5 → L4 ist 0,0028 Gm kürzer, liegt aber im Gleichstand (≤ 0,1 Gm); dort gewinnt die Station mit
  Ein-Ziel-Vertrag (beide beginnen mit CRU-L1, dem Ziel des Direct), dann die Ortsordnung → §3s Folge. Baijini
  **L1 → L4 → L3 → L5 → L2 (162,50 Gm)** statt §3 (166,71 Gm). Die §3-Folgen sind Spielerfahrung; Distanzen in den
  Testnamen (`LoopsBaselineTests`). LEO: Everus → Baijini → Seraphim → Port Tressler (161,07 Gm) statt §8.6s Everus → Baijini
  → Port Tressler → Seraphim (171,74 Gm).
- **Ranking kippt (Hull B, Senior, nach dem Fix):** bei 10 min Port Tressler 586k/h, Baijini 561k, Seraphim 557k,
  Everus 484k, Directs ab 330k; bei 5 min 819k / 792k / 769k / 669k, der Direct Port Tressler → S4DC05 (0 lange Sprünge)
  springt vor die Directs mit zwei langen Sprüngen; bei **15 min überholt Seraphim Baijini** (437k gegen 434k). Weniger
  Zeit je Landung hebt Loops mit vielen Landungen, mehr hebt die kurzen (Seraphim, Directs) – umgekehrt zum Startprompt
  („10 → 5 min hebt Seraphim“). Mechanik-Test mit zwei Parametersätzen (`The_ranking_tips_with_the_time_per_station`).
- **Mutationsproben:** ohne Permutation (`MaxPermuted = 0`) 6 rot, ohne Rückfracht 2 rot, ohne Zeitparameter (Default
  statt Settings) 1 rot.
- **Budget (Release, Martins Rechner, Hull B Senior):** im Testprozess Median 97–108 ms, erster Lauf 101–114 ms (Master
  302–330 ms mit JIT). **In der App ohne Cache:** Start 434–462 ms, spätere Läufe 190–230 ms – über 200 ms; ein Cache
  der Rückwege je Aufruf brachte ~10 %, Pyro allein kostete 84 von ~100 ms (warm). **Mit `FitCache`:** Start
  **253 / 259 / 254 ms** (drei Starts, erster Lauf im Prozess), neue Rangstufe 40–43 ms, Wiederholung 0–11 ms; im
  Testprozess zweiter Lauf 4–7 ms (~100 verschiedene Fragen je Ranking). **Der Start bleibt ~55 ms über dem Budget**,
  im Hintergrund, die UI blockiert nie; nach einem Scan wenige ms. Nächster Hebel, falls gewünscht (Rückfrage): Pyro nur
  rechnen, wenn der System-Filter Pyro zeigt (Pyro ≈ 80 % der warmen Zeit).
- Logzeile beim Start: `Routes: loops 43 candidates, 0 observed, 254 ms (MISC Hull B, senior, Everus Harbor)`.
- Bild (CC, Desktop, 470 × 700): Filterzeile „Loops · Sort: ~aUEC/h · Rank ≤ Senior · Stanton · 10 min · 20 s · 4 min“,
  Port Tressler, Baijini, Seraphim, Everus ring, dann die Directs („Everus Harbor → HUR-L2 · Rookie Direct Medium ×3“).
- **Handling gegen main:**

  | Geste | Soll (= main) | Ist Branch (erwartet) |
  |---|---|---|
  | View-Button Plan-Reiter | Matrix ↔ Recipe | Matrix → Recipe → Loops (Rechtsklick zurück) |
  | System-Button | Matrix-Filter | wirkt auch in Loops (Stanton ohne Pyro-Zeilen) |
  | Hub-Button (Recipe) | zyklisch, speichert `routes.hub` | gleich; nach Zeilenklick „(from Loops)“, der nächste Klick geht vom gezeigten Hub weiter und speichert |
  | Sort-Button (neu) | – | nur Loops, zyklisch, speichert `routes.loopsSort` |
  | Klick auf Loop-Zeile (neu) | – | Stanton-Zeile → Recipe dieses Hubs, nichts gespeichert; LEO/Pyro nichts |
  | Settings „Plan tab“ | Rang, Fraktion | + drei Zeit-Slider (per landing, per crate, per long jump) |
  | F3 auf Angebot, Plan-Reiter | „fits as …“ gegen `routes.hub` | gleich (K1), auch nach Zeilenklick |
  | Alles andere | wie main | gleich |

- **Bild-Gate (Martin, 24.11.) – zwei Befunde, Fix:**
  (1) Recipe Port Tressler „Σ 690 500 + 4 × 96 500 Back“ ≠ Loops „1.2M/loop“: Σ des Rezepts ist nur der Hinweg (D123,
  `Recipe.RewardSum`); der Loops-Ring zählte die Rückwege dazu, und zwar fünf (er fährt alle fünf MIC-L-Stationen an,
  das Rezept nur seine vier Ziele): 690 500 + 5 × 96 500 = 1 173 000. (2) Die „Member Small 96 500 accept at
  MIC-L1…L4“ waren **ein** Template (`9ce4eae8`, Multi → Single, 3 Stopps, Pool alle fünf MIC-L), je Station neu gewählt
  (Fehler seit T13.3b, `BestReturn` je Station); ebenso Everus (Junior Small 72 500, Multi4), Baijini (Member Small 89 750,
  Multi4), Seraphim (Junior Small 67 500, Multi3), und der Direct-Lauf nahm einen Multi-Pickup-Rückläufer. **Fix
  (Präzisierung zur Abnahme):** `Recipe.WaysBack` entlang des Rings, jedes Template höchstens einmal; ein Rückläufer mit
  mehreren Pickups wird an der ersten Station angenommen und sammelt an den nächsten Ringstationen seines Pools
  (`RecipeRole.CollectAt`, Zeile „accept at MIC-L1, collect at MIC-L2, MIC-L3“), reichen die folgenden Stationen nicht,
  gilt er dort nicht; ein Direct-Lauf nimmt nur Rückläufer mit einem Pickup. Gleiche Regel in Recipe und Loops.
  **Σ-Zeile (Martin):** „Σ 594,500 out · +232,000 back = 826,500 aUEC · worst case 512/512 SCU in 50 crates · fits MISC
  Hull B“ (Everus, Hull B, Senior; `Recipe.ReturnSum`); der Worst Case bleibt der des Hinwegs. Tests: Recipe (Multi3 einmal, dann die nächsten), Loops (Ring zählt Multi4 einmal, Direct
  ohne), Recipe-Zeile mit „collect at“.
- **Σ-Zeile Recipe gegen Loops (Bild 1):** Recipe Everus „Σ 594,500 out · +232,000 back = 826,500 aUEC“, der
  Everus-Ring in Loops 877 000. Der Hinweg ist gleich (594 500); die Rückwege nicht: das Rezept geht sie entlang seiner
  vier Ziele in Ortsordnung (HUR-L1 Junior Small 72 500 sammelt L2–L4, L2 Junior Small 60 750, L3 Rookie XS 50 250, L4
  Rookie Direct XS 48 500 = 232 000), der Ring entlang aller fünf Stationen in der kürzesten Folge (L1 72 500, L4
  60 750, L3 50 250, L5 48 500, L2 Rookie Direct Medium 50 500 = 282 500). Gleiche Regel, anderer Ring (D124).
- **Gate T13.4 (Martin, SC vorne, Everus gespeichert):** (1) Loops zeigt vier Stanton-Ringe, Direct-Läufe, LEO-Ring mit
  „needs scans“; System → Pyro: Pyro-Ringe ohne aUEC; (2) Everus-Ring aUEC/Umlauf plausibel gegen Σ der Matrix-Zeile
  Everus → HUR-Lx plus Rückläufer (Tooltip der Zeile listet die Verträge); (3) Settings „Per landing“ auf 5 min →
  Reihenfolge ändert sich sichtbar, Neustart → bleibt; (4) fünf lokale Offers scannen → Everus-Zeile „observed n/m“,
  Kisten ändern sich; (5) ein systemweites Offer Everus → Hub scannen → LEO-Zeile bekommt Zahlen oder nennt den nächsten
  Hub; (6) Hub-Button auf Baijini → Anreise-Spalte wechselt. Zahlen: Logzeile beim Start und nach dem Scan (ms). Offen
  aus T13.3, falls eine Pyro-Session möglich ist: Red-Wind-Zwillinge (gemeinsame Beobachtung, Fraktionsfilter).
- **Gate bestanden, Abnahme erteilt (24.11.2026, Martin)**, damit auch die Präzisierungen bei der Umsetzung ratifiziert
  (Gleichstand 0,1 Gm, Pyro-Ringe, `FitCache`, ein Rückläufer = ein Vertrag, Σ-Zeile out/back). **Budget aus Martins
  Gate (Release, Profilordner):** erster Lauf nach dem Start 361 ms (ARGO RAFT) bzw. 261 ms (Hull B), der erste Lauf
  für ein neues Schiff 216 ms, mit Cache 10–43 ms (Hub-Wechsel 10/43/18 ms, Rang 16 ms). Der Startlauf bleibt über
  200 ms, im Hintergrund.
- **Drei E der Abnahme:**
  - **E1 Startlauf nach der Wiederherstellung:** kein Fehler. Rang, Fraktion, Hub, Zeiten und Beobachtungen stehen im
    `RoutesViewModel` vor dem ersten `ShowShip`; der Start rechnet einmal mit dem gespeicherten Schiff (je Start genau
    eine Logzeile, jeder Lauf wird geloggt, auch ein verworfener). Die ARGO-RAFT-Läufe im Log (14:17:40, 5 s nach dem
    Start; 14:27:37 als Startschiff, dann Hull B 14:27:45) kamen aus Schiffswechseln: das Schiff ändert sich nach dem Start
    nur über `ChangeShip` (Schiffsliste), und settings.json hatte beim zweiten Start RAFT gespeichert.
  - **E2 Grund der none-Beobachtung:** umgesetzt. `ObservedEdges.Unmatched` (Schlüssel + Grund), die Startzeile nennt
    ihn: „8 observations: 7 matched, 0 ambiguous, 1 none (covalex experienced medium 163250: places outside the
    template's pools: Everus Harbor, Teasa Spaceport in Lorville)“ – ein Planetary-Vertrag nach Lorville, kein
    Stellar-Template. Test: `ObservedEdgesTests.Templates_ambiguous_beyond_the_faction_or_none_add_nothing`.
  - **E3 Kisten Pyro (100/193 bei Hull B):** kein Fehler. Red Wind liefert höchstens in 8er-Containern (Member/
    Experienced Medium) bzw. 4ern (Smalls), Covalex in 16ern: Member Medium 2 × 175 SCU = 2 × (21 × 8 + 4 + 2 + 1) =
    48 Kisten, Experienced Medium 4 × 87 SCU = 4 × 13 = 52; Checkmate & Co. = 48 + 52 = 100, Ruin Station 193.
- **Bilder zur Abnahme (CC, `--data-dir` mit Kopie von Martins routes.json, Hull B, Senior, Everus):** (1) Recipe Everus
  mit „Σ 594,500 out · +232,000 back = 826,500 aUEC · worst case 512/512 SCU in 50 crates · fits MISC Hull B“ und
  „Back · Junior Rank - Small Cargo Haul · 72,500 aUEC · … · accept at HUR-L1, collect at HUR-L2, HUR-L3, HUR-L4“;
  (2) Loops ans Ende gescrollt: „LEO ring from Everus Harbor / needs scans at Everus Harbor“ blass als letzte Zeile.
- **Testnamen:** K1 `RoutesViewModelTests.A_rows_click_shows_its_hubs_recipe_for_the_session_and_the_offer_check_stays_at_routes_hub`
  (Zeilenklick Baijini → Recipe Baijini, `FitOf` weiter Everus); Persistenz `SettingsStoreTests.Routes_keep_the_loops_times_and_sort`,
  `SettingsStoreTests.Save_then_Load_keeps_the_routes`, `SettingsStoreTests.A_loop_setting_out_of_range_falls_back_with_a_line`,
  `SettingsSessionTests.The_settings_windows_loop_times_go_into_the_routes_section_and_another_window_follows`,
  `RoutesViewModelTests.The_sort_cycles_is_saved_and_the_times_from_the_settings_rank_without_a_new_computation`; LEO mit
  den §8.6-Kanten `LoopsTests.The_LEO_ring_takes_what_scans_saw_between_the_hubs_and_unloads_on_the_way`.
- **Offen:** Red-Wind-Zwillinge bei der ersten Pyro-Session (aus T13.3).

### ~~T13.5 App: Hafen-Matrix~~ – entfällt (in T13.2, U3, 23.11.)

### [ ] T13.6 Route: Platzhalter, Accepted-Scan füllt, Stationsleiste

**Warum:** Aus dem Entschluss wird ein Ablauf: aus einer Loop- oder Recipe-Zeile eine Route mit Platzhaltern im
Cargo-Reiter, die der Accepted-Scan am Terminal füllt, und eine Stationsleiste führt mit Abladen, freien Slots und
Rückläufern über den Ring bis zum Abschluss am Hub mit Σ aUEC und Zeit (Design §4.3 Schritt 4–7, §8.4 Punkt 4).
Zwei Teile mit je eigenem Gate: **6a** Start, Block „Plan“, Accepted-Scan füllt; **6b** Stationsleiste,
Slot-Simulation, Rückläufer, Abschluss. Branch `feat/p13-route`, Zwischenabnahme nach 6a, Abnahme nach 6b.

**Akzeptanz (Startprompt 24.11., §5):**
- Route und Platzhalter in `routes.json` Schema 3, `plan.json` Schema 11 und byte-gleich bei Start/End/Finish (D118).
- Accepted-Scan füllt über `TemplateMatch`, nie über Titeltext; ohne Platzhalter Mission wie heute + Hinweis, Vertrag
  zählt zur Route (U9.2); fehlt ein Scan, fehlt der Vertrag in der Route – Hinweis, kein Fehler.
- Erfüllt wird aus dem Plan gelesen; Undo öffnet den Platzhalter.
- Stationsleiste über der Legende, Cargo-Ansicht darunter unverändert (Handling-Tabelle gegen `main`); Slot-Simulation
  auf dem echten Plan; Rückläufer-Platzhalter je `AcceptAt`.
- Abschluss schreibt `runs[]` mit Σ aUEC der gefüllten + Extras und Zeit ab Abflug; kein Nachführen der Zeitparameter
  (Backlog mit Auslöser).
- U4 eingehalten: „Start plan“, „End plan“, „Finish plan“, Block „Plan“; „Discard plan“ → „Discard cargo“.
- D125 und D126 mit Gegenargument; `routes-format.md` Schema 3; CLAUDE.md-Zeilen zu `routes.json`; beide Gates mit
  Zahlen; DoD (`dotnet build` 0 Warnungen, komplette `dotnet test`-Zusammenfassung wörtlich, `dotnet format
  --verify-no-changes`); Commits ohne Co-Authored-By.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (24.11.2026, „Freigabe T13.6 mit K1“; U9 aus der
Vorbesprechung, Befunde 1–8, K1–K6, E(a)–(c), E1, E2):**
- **U9:** (1) Platzhalter in `routes.json` (Schema 3, `route`), im Cargo-Reiter als grauer Block „Plan“ über der
  Legende, eine Zeile je Rolle; keine Kisten im Schiff; erfüllt = „✓ #n“, der Platzhalter bleibt. (2) Accepted-Scan ohne
  passenden Platzhalter: Mission wie heute, „· not in plan (extra)“, zählt zur Route. (3) Stations-Wizard = eine
  Stationsleiste über der Legende, keine dritte Ansicht. (4) Schnitt 6a / 6b.
- **K1:** „k/n filled“ zählt alle Platzhalter (Hinweg + Back); Everus = 10.
- **K2 (U4):** „Discard plan“ → „Discard cargo“ (Editor-Button, Hotkey-Label), Notice „Cargo discarded – …“, Leerzustand
  „No cargo. Open the editor to enter missions.“; Settings-Schlüssel `discardPlan` und Code-Namen bleiben.
- **K3:** Ein Accepted-Scan mit `AlreadyInPlan` (Vertrag vor „Start plan“ gescannt) füllt einen offenen Platzhalter:
  der D101-Schlüssel trägt keinen Titel (Rang/Größe), deshalb kann „Start plan“ vorhandene Missionen nicht selbst
  zuordnen – nur ein erneuter Accepted-Scan (D125, mit Gegenargument).
- **K4:** „Start plan“ in Loops = die zuletzt geklickte Zeile, sonst die oberste mit Zahlen; in Recipe = die zuletzt
  geklickte, wenn ihr Start der gezeigte Hub ist (Direct-Lauf), sonst der Ring des gezeigten Hubs. Ein Klick auf eine
  LEO-/Pyro-Zeile markiert sie (vorher: nichts). Tooltip nennt das Ziel oder warum nicht.
- **K5:** Leiste „Start · Hub“, dann „Station 1/n“ … „n/n · Hub · Finish plan“, n = Landungen (6b).
- **K6:** Extras außerhalb des Rings: vor dem Abflug neu permutiert (`Loops.Order`), danach vor den Hub (6b).
- **E(a):** Offer-Scan eines offenen Platzhalter-Templates: „ · plan: bulk“ in beiden Reitern. **E(b):** Auto-Weiter-
  schalten beim Übergang „letzte offene Lieferung der Station abgehakt“ (6b). **E(c):** „accepted at … (planned …)“ (6b).
- **K1 der Freigabe:** Gate 6a mit K3-Schritt. **E1:** plan.json-Hash auch bei Finish. **E2:** U9 (und U4, U5, U7) in
  Design §9.2; U6 und U8 stehen nicht im Repo (Master-Chat), daher nicht nachgetragen.

**Befunde Planphase (bestätigt):** (1) Everus-Ring 10 Rollen (5 Hinweg 594 500, 5 Back 282 500), nicht 7. (2) „Discard
plan“ steht nicht im Overlay, sondern im Editor und als Hotkey-Label; „No plan …“/„Plan discarded …“ aber in der
Cargo-Ansicht. (3) Scan-Notices laufen über `NoticeSource.Move`, nicht `Scan`. (4) Route nimmt `LoopKind` + `StarSystem`
(Pyro = `LocalRing` + Pyro), kein neues Enum. (5) `Loops.Order` ist die gesuchte Permutation (internal, aus Core
nutzbar). (6) Zeilenklick wechselte nach Recipe bzw. tat nichts → K4. (7) Design §9.4 gibt es nicht. (8) Kartennamen
im Plan vs. Positionsnamen der Route → `Positions.Resolve` (6b).

**Präzisierung in der Planphase, ratifiziert durch Freigabe (bei der Umsetzung 6a, zur Zwischenabnahme):**
- Core nimmt statt `ContractView` (lebt in `CargoPlan.Import`) die Schätzung der Scan-Identität: geschätzt ⇔ nicht
  Accepted (D102); `Route.Fill` bekommt die Identität des Scans (der Capture), nicht der Mission.
- „Already in plan (#3)“ → „Already in cargo (#3)“ (U4: neben „· plan ✓“ stünde „plan“ zweimal).
- `runs` steht ab 6a im Schema (kein zweiter Bump in 6b).
- Die Platzhalterzeile zeigt die Orte der Rezeptzeile (wie Recipe) und zusätzlich die Waren; Format `RoleText` für
  Recipe, Loop-Tooltip und Platzhalter (A1).
- Der Leerzustand der Legende steht hinter den Stopp-Zeilen (Block darüber; ohne Route keine Änderung); der Scrollbereich
  bleibt mit Route auch bei `legend.visible = false`, die Stopp-Zeilen folgen der Einstellung.
- Block-Zeilen einzeilig mit Ellipse wie die Legenden-Zeilen (K6.1), voller Text im Tooltip (Bild-Check CC).

**Stand 6a (24.11., Branch `feat/p13-route`, 5caecc7..c403039):**
- Commits: `Route`/`Placeholder`/`From` + Golden (5caecc7); `Route.Fill` + Füll-Matrix (b262422); routes.json Schema 3 +
  `RoutesBook` (0420b39); `ActiveRouteViewModel`, `ApplyScan`, Notices, `RoleText` (3ff6e41); „Start plan“ (d212210);
  Block + K2 (b3ecacd); Block einzeilig (0a307de); plan.json-Hash bei Start/End (c403039); Doku.
- **Golden (Pin `ships-2026.11.2`, Hull B, Senior):** Everus-Ring L1 → L4 → L3 → L5 → L2, 10 Platzhalter, Hinweg
  594 500, Back 282 500 (L1 72 500 „collect at L4, L3, L5“, L4 60 750, L3 50 250, L5 48 500, L2 50 500); Direct
  Everus → HUR-L3 eine Station, Rückläufer mit einem Pickup, 211 750; Port Tressler 1 028 000 (`RouteBaselineTests`).
- **Füll-Matrix** 48 Zellen (View × ScanChange × Bulk-Platzhalter), dazu Ambiguous, zwei Platzhalter eines Templates,
  Undo öffnet, Extra wandert zum offenen Platzhalter (`RouteTests`).
- **Mutationsproben:** `IsFilled` ohne die Keys des Plans → 2 rot; `at`-Bereichsprüfung aus → 1 rot. Die Format-Tests
  (Commit 3) liefen nicht vor dem Code (Lehre T7.2 verletzt), dafür die Mutation.
- Bild (CC, Desktop, `--data-dir` mit Scratch-Datenordner: Everus-Ring des Pins, Bulk gefüllt als #1, ein Extra):
  „Plan: Everus Harbor ring · 1/10 filled · Σ 594,500 out · +282,500 back“ mit „End plan“, „✓ #1 · Bulk · …“, vier graue
  Hinweg-Zeilen, „Back“, fünf Rückläufer, „+ #2 extra · 41,750 aUEC“, darunter die Legende.
- **Handling gegen main:**

  | Geste | Soll (= main) | Ist Branch (erwartet) |
  |---|---|---|
  | Filterzeile Plan-Reiter | View, Hub (Recipe), Sort (Loops), Rang, System, … | dazu „Start plan“ in Recipe und Loops, Tooltip auch deaktiviert |
  | Klick auf Loop-Zeile | Stanton → Recipe des Hubs; LEO/Pyro nichts | Stanton gleich, dazu markiert; LEO/Pyro markiert (Balken links), bleibt in Loops |
  | „Start plan“ (neu) | – | Route in routes.json, Reiter → Cargo; zweite Route nur nach Ja |
  | Cargo-Reiter mit Route (neu) | – | Block „Plan“ über der Legende, scrollt mit ihr; „End plan“ |
  | F3 Accepted mit Route | „Scanned #n · …“ | dazu „ · plan: bulk ✓ k/n“ / „ · not in plan (extra)“ / „ · plan ✓“ |
  | F3 Offer mit Route | wie main | dazu „ · plan: bulk“, wenn ein offener Platzhalter das Template hat |
  | F3 gleicher Vertrag nochmal | „Already in plan (#n)“ | „Already in cargo (#n)“ (U4) |
  | Editor „Discard plan“, Hotkey-Label, Leerzustand | „Discard plan“, „No plan. …“ | „Discard cargo“, „No cargo. …“ (K2) |
  | Alles ohne Route | wie main | gleich |

- **Gate 6a (Martin, SC vorne, Everus, Hull B, Senior, `--data-dir` mit Kopie des Datenordners):**

  | Schritt | Erwartung | Warum |
  |---|---|---|
  | 1 Plan → Loops → Everus ring (Klick) → „Start plan“ | Reiter Cargo; „Plan: Everus Harbor ring · 0/10 filled · Σ 594,500 out · +282,500 back“, 5 graue Zeilen, 5 unter „Back“; `routes.json` hat `route`; `plan.json`-Hash gleich | Start schreibt nur die Planer-Datei (D118) |
  | 2 Bulk am Terminal (Titel + Reward der Zeile), annehmen, Accepted, F3 | Mission wie heute; „✓ #n · Bulk · …“; Notice „Scanned #n · … · plan: bulk ✓ 1/10“ | Kernpfad U3 |
  | 3 Fremder Vertrag (z. B. Rookie Direct Small eines anderen Templates), F3 | Mission wie heute; „· not in plan (extra)“; „+ #m extra · …“ | U9.2 |
  | 4 Undo auf 2 | Zeile wieder grau; `routes.json` unverändert | erfüllt kommt aus dem Plan |
  | 5 Redo, App neu starten | Block wie vorher, ✓ #n bleibt | Persistenz Route + Plan |
  | 6 **K3:** „End plan“; einen Vertrag annehmen und F3 (Accepted) **ohne** Route; dann „Start plan“; erneut F3 im Accepted-Tab | „Already in cargo (#n) · plan: … ✓ 1/10“, Zeile „✓ #n“ | K1 der Freigabe |
  | 7 „End plan“ | Block weg, Legende und Schiff unverändert, `routes.json` ohne `route` | Route additiv |
  | 8 Ohne Route: Offer im Cargo-Reiter, Accepted-Scan | exakt wie main | Invariante 1 |

  Zahlen: `keyToNoticeMs` (scans.log) für 2/3 gegen T13.1 (1 672–1 807 ms), `fillMs=` aus cargoplan.log („Routes: fill
  #n: … fillMs=“). Fokus: „End plan“ und „Start plan“ mit `--log-input` (Cargo-Reiter aktiviert nicht, U7).

- **Gate-Befund 6a (Martin, 25.11.) – Darstellung des Blocks, kein Logikfehler. Präzisierung in der Planphase,
  ratifiziert durch Freigabe:** (K1) Platzhalterzeilen brechen um statt „…“ – Ziele und „collect at“ ganz lesbar (nimmt
  die Einzeiligkeit aus 0a307de zurück); (K2) Zeile = Rolle · Titel · Reward · Ziele bzw. Back · Titel · Reward ·
  accept at X · collect at Y, Z; Waren und SCU-Band (je Leg und gesamt) nur im Tooltip; (K3) „accept at“ im Orange der
  Statuszeile (`NoticeForegroundBrush`), „collect at“ normal (`PlaceholderRowView.Accept`/`Collect` als eigene Runs);
  (E1) Trennlinie (1 px, Legendenfarbe, 40 %) zwischen Block und Stopp-Zeilen. Tests:
  `RouteScanTests.A_way_back_says_where_to_accept_and_collect_apart_and_the_tool_tip_holds_commodities_and_bands`,
  `OverlayRouteXamlTests.Placeholder_lines_wrap_and_accept_at_takes_the_notice_colour`,
  `OverlayRouteXamlTests.A_line_parts_the_block_from_the_stop_lines`. Die Kopfzeile bleibt einzeilig (nicht Teil des
  Befunds). Bild-Gate (Martin) danach.
- **Testnamen:** `RouteTests`, `RouteBaselineTests`, `RoutesFileTests.A_route_*`, `RoutesBookTests`, `RouteScanTests`
  (u. a. `A_contract_scanned_before_the_route_fills_its_placeholder_when_scanned_again` = K3,
  `Starting_and_ending_the_route_leave_plan_json_byte_for_byte`), `RoutesViewModelTests.Start_plan_*`,
  `OverlayRouteXamlTests`; Nachfolger: `RoutesViewModelTests.A_row_without_a_recipe_is_only_marked_on_a_click_and_stays_in_the_loops`
  (vorher `…_does_nothing_on_a_click`), `OverlayLegendXamlTests.Hiding_the_stop_lines_hides_them_and_their_scroll_area_unless_a_route_is_on`
  (vorher `Hiding_the_stop_lines_hides_their_scroll_area`).

### [ ] T13.7 Streckenkarte

**Warum:** Die Route vor dem Annehmen planen, wenn man von der Routine abweicht (Design §8).
- Gerichteter Graph, Layout schematisch (Planet = Hub + L-Ring), Option geografisch aus `positions.json`; Kante =
  Template (Fächer/Trichter, gestrichelt = Pool); Filter aus T13.2 (vormals T13.5); Klick → Route, „An Cargo-Tab übergeben“

**Akzeptanz:**
- Renderentscheidung als D-Eintrag im Plan (A9), nicht in eine bestehende Ansicht eingepasst
- Klick und Übergabe schreiben in `routes.json`, nie in plan.json (D118)
- Darstellung am gerenderten Bild geprüft (Lehre T8.2–T8.5)

### [ ] T13.8 Release v0.8.0

**Warum:** Ohne Release erreicht die Phase keinen Hauler.
- Pin auf ein Data-Release mit Abschnitt `hauling`; Wiki-Seite „Routes“, README-Absatz, Release-Notes, Known issues
- Credits api.star-citizen.wiki, StarCitizenWiki/scunpacked-data, octfx/ScDataDumper (D119)

**Akzeptanz:** Release-Checkliste wie v0.7.0 (ZIP gemessen, CI-Artefakt = lokales Pack, Netz-Gate); Abschnitt nach
`docs/tasks-archive/` verschoben (K11).

---

## Backlog (nur bei Bedarf)

Kein Plan, sondern Reaktion: wird erst zum Task, wenn der Anlass eintritt.

- Standorterkennung ersetzt den Hub-Button des Rezepts (T13.3b, D123): der Hub, an dem der Spieler steht, statt
  `routes.hub`. Auslöser: Game.log-Location wird gelesen.

- QT-Zeit aus Distanz und der Quantum-Geschwindigkeit des Schiffs statt einer Zeit je langem Sprung (T13.4, D124:
  heute `routes.times.jump` je Sprung > 10 Gm). Auslöser: Nutzer-Report, dass das Ranking lange und kurze Sprünge
  falsch gewichtet, oder Schiffsdaten mit QT-Werten.

- Kipprad (`WM_MOUSEHWHEEL`) scrollt die Hafen-Matrix seitwärts (K der Zwischenabnahme T13.3a: heute nur Shift+Rad).
  WPF hat dafür kein Event, es braucht einen `HwndSource`-Hook. Auslöser: Nutzer-Report mit Kipprad-Maus.

- Proben der Zuordnung nach Seite sparen (F10b, D117): `RackAssignment.AssignBySide` prüft jedes Ziel je Seite mit
  einer Machbarkeitssuche; nimmt ein leeres Rack der Seite das Ziel allein, gibt es sicher eine Zuordnung und die Probe
  kann entfallen (Hull B: fast alle 8 Proben). Auslöser: spürbare Planzeit mit Seiten auf einem Schiff mit vielen Racks.

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

- Game.log-Reader (Phase 13, Design §1.6, §4.1): angenommene Verträge, missionId und Abschluss aus dem Log statt aus dem
  Accepted-Scan. Auslöser: Nutzer wollen den Accepted-Scan sparen.
- Hotkey für den Reiterwechsel Cargo | Plan (T13.2, D120): gesperrt lässt sich die Leiste nicht klicken. Auslöser: ein
  Spieler wechselt im Lock-Modus.
- Fenstergröße je Reiter (T13.2, D120): die Matrix nutzt die Fläche von Legende und Schiffsansicht. Auslöser: Bild- oder
  Spieltest zeigt, dass die Standardgröße für die Matrix nicht reicht.
- Board-Hub für planetary und local (D121): dort bleibt eine leere Pool-Seite leer. Auslöser: ein Scan zeigt, wo ein
  solches Board steht.
- Zähler-Überwachung „HAULING – STELLAR n“ (Phase 13, Design §1.4, §4.3 Schritt 8): ändert sich n, Hinweis zum
  Nachscannen; nur Komfort, keine Planer-Logik (Invariante 4; den Pool gibt es seit U3 nicht mehr). Auslöser:
  Nutzer-Report „Angebote veraltet“.
- Planetary- und CrossStanton-Verträge im Planer (Phase 13 plant nur Stellar; Design §5: Sichtbarkeitsregel offen,
  vermutlich Nähe zum Pickup). Auslöser: Sichtbarkeitsregel geklärt oder Nutzer-Report.
- Pyro-Fraktionsgate HH/CFP (Design §7.6): sieht ein Spieler nur eine Seite? Bis dahin Fraktion als Filter in den
  Settings (T13.3). Auslöser: empirischer Befund im Spiel.
- Red-Wind-Regionen (`RegionA`–`RegionD`) auf dem Pyro-Board (T13.3, Design §5, §7.6): heute nur `Solar`, der Reiter sagt
  es. Auslöser: erster Pyro-Scan einer Region-Variante zeigt, welche Station welche Region hat.
