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

### [ ] T13.3 Core: Board, Matching, Rezept

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

### [ ] T13.4 Core/App: Loop-Ranking

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

### ~~T13.5 App: Hafen-Matrix~~ – entfällt (in T13.2, U3, 23.11.)

### [ ] T13.6 Route starten

**Warum:** Aus dem Entschluss wird ein Ablauf: Stationsfolge, freie Slots, Rückfracht (Design §4.3 Schritt 5–7).
- Stationsfolge aus *Accepted*-Einträgen, Slot-Simulation je Station mit `LoadPlanner`, Stations-Checkliste (abladen,
  frei, Rückfracht-Platzhalter), Platzhalterkarten
- Accepted-Scan aktualisiert die Route; Abschluss am Hub schreibt Messwerte in die Zeitparameter

**Akzeptanz:**
- Route und Platzhalter liegen in `routes.json`, plan.json unberührt (D118)
- Fehlt ein Accepted-Scan, fehlt der Vertrag in der Route mit Hinweis, kein Fehler
- Gate am Terminal, SC vorne: ein Ring mit Rückfracht von Start bis Abschluss

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
