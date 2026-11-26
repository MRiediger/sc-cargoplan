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
| Phase 13 – Cargo-Routen (T13.0–T13.7, v0.8.0) | 21.–26.11.2026 | D118–D130 | [phase-13-cargo-routes.md](docs/tasks-archive/phase-13-cargo-routes.md) |

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
- **Schreiber und Leser einer Datei über jeden Zustand testen, den die Oberfläche erreicht.** Rundweg schreiben → lesen → schreiben für jeden Zustand, den die Bedienung herstellen kann, nicht nur für die gebauten Beispiele. Anlass B18: der Rundweg-Test von `routes.json` lief mit `at` innerhalb der Stationen, die Leiste erreichte mit Extras mehr Stopps; die App schrieb eine Datei, die sie nicht las, und erst der Doku-Abgleich in T13.8 fand es.

---

## Release v0.8.0 – T13.8 (Abschluss Phase 13, Branch `release/v0.8.0`)

Phase 13 (T13.0–T13.7, D118–D130) steht im [Archiv](docs/tasks-archive/phase-13-cargo-routes.md); hier bleibt der Release bis zu seinem Abschluss.

### [ ] T13.8 Release v0.8.0

**Warum:** Ohne Release erreicht die Phase keinen Hauler.
- Pin auf ein Data-Release mit Abschnitt `hauling`; Wiki-Seite „Routes“, README-Absatz, Release-Notes, Known issues
- Credits api.star-citizen.wiki, StarCitizenWiki/scunpacked-data, octfx/ScDataDumper (D119)

**Akzeptanz:** Release-Checkliste wie v0.7.0 (ZIP gemessen, CI-Artefakt = lokales Pack, Netz-Gate); Abschnitt nach
`docs/tasks-archive/` verschoben (K11).

**Startprompt 26.11.:** v0.8.0 (Minor) in vier Stufen mit je einem Halt für Martin: (1) Textabgleich und Abschluss der
Phase, (2) Release-Notes (Freigabe Claude.ai), (3) Merge, Tag (Martin), Nachmessen am veröffentlichten ZIP inkl.
Auto-Update v0.7.0 → v0.8.0 auf `cargotest`, (4) Wiki, Nexus, Ankündigungen (Martin). Kein Produktcode; ein Blocker im
Nachmessen → eigener Bugfix-Branch, Release stoppt.

**Befund vor dem Plan (26.11.2026):**
- `git log v0.7.0..main`: 107 Commits ohne Merges, 9 Merges; 2 `REL:` (Nachlauf v0.7.0), Rest `T13.x`. Ein
  nutzer-sichtbarer Commit ohne TASKS-Zeile: `d60bc1e` (Settings-Reihenfolge, nach der Abnahme) → Nachtrag in T13.7.
- Pin `ships-2026.11.2` = Latest im Shipdata-Repo (23.11.) → kein Pin-Bump. Im Pin: 587 Templates, 157 Positionen,
  Quelle `StarCitizenWiki/scunpacked-data@3cc8072`, Game-Build `4.10.3-LIVE.13165946`.
- Schemata: plan.json 11, Missions-JSON 5, settings.json 3 (`routes {…}` additiv), `routes.json` 3 neu, Hauling 1.
- `docs/user-guide.md` gibt es nicht (Nutzerdoku = Wiki); README-SignPath-Satz schon raus (K12, `7e58081`);
  `--routes-open` fiel in T13.2 (`67259aa`); D-Spanne bis D130 (T13.7c); im Backlog stehen Korrekturfaktor B, Kipprad
  und Red-Wind-Regionen schon.
- Wiki: nur `master` = `origin/master` (`4731161`), keine wartenden Branches. GitHub: 11 Releases, Holo-Releases 2.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (26.11.2026, „Freigabe T13.8 wie geplant, P1–P10
ratifiziert“, mit E):**
- **P1** Nutzerdoku als Wiki-Seite `Plan-tab.md` + Sidebar, Known issues in Troubleshooting, Home/Footer 0.8.0, auf
  Wiki-Branch `v0.8.0`, Merge am Tag.
- **P2** „Not affiliated with Motherlode, Fleetyards, the Star Citizen Wiki or CIG.“ überall wie `ship-data/NOTICE.md`
  (README, Wiki-Footer, Nexus).
- **P3** U2, U6, U8 in Design §9.2, nach Nummer einsortiert, Text von Martin.
- **P4** v0.7.0-Gegencheck: CC entpackt das v0.7.0-ZIP in den Scratchpad, startet mit leerem `--data-dir`, Martin
  klickt „Update“, CC liest `cargoplan.log` (Check, Install, Neustart, 110 Schiffe, kein Fehler); Ergebnis in die
  Archivdatei.
- **P5** SignPath-Satz: nur Grep-Nachweis. **P6** `--routes-open` nicht in Notes/Doku, `--log-input` nur im Wiki.
  **P7** D118–D130. **P8** Backlog ohne Doppel, neue Zeilen mit Auslöser (Overlay-Bildlaufleisten in der Kipprad-Zeile).
  **P9** Draft-PR nur für CI vor dem Merge, CC pusht `release/v0.8.0` dafür. **P10** Wiki-Seite „Plan tab“ statt
  „Routes“ (U4); `api.star-citizen.wiki` als „same extraction“ wie im NOTICE.
- **E der Freigabe:** Backlog-Auslöser „Startlauf nach Wiederherstellung“ = „Startzeit-Budget überschritten oder zweiter
  loops-Lauf innerhalb 10 s im Log“.

**Blocker aus Stufe 1 (CC, 26.11.2026) – Release gestoppt:** beim Abgleich von `routes-format.md` gegen Martins
`%LocalAppData%\SC-CargoPlan\routes.json` (geschrieben 12:13:32 vom Dev-Build auf `main`): die Route `custom` „Own plan
from Everus Harbor“ hat `"at": 6` bei 4 `stations`. Seit 12:56:45 meldet jeder Start `Routes not loaded (… is invalid:
the route's at 6 is no stop of its 4 stations and the hub.). The next save keeps the file aside.` – der Plan-Reiter
startet ohne die 13 Beobachtungen, die Route und die 3 Läufe (gemessene Zeiten). Ursache: `RouteProgress.StationsOf`
hängt die Ziele der Extras (Missionen aus plan.json) als weitere Stopps an; `Route.Step` begrenzt `at` auf die Stopps der
Leiste, `RouteFileDto` (Z. 41) prüft gegen `stations.Count + 1`. Die App schreibt also eine Datei, die sie selbst nicht
liest. Sicherung von routes.json, plan.json, settings.json im Scratchpad der Session (Hash gleich). Weiter nur über einen
eigenen Bugfix-Branch ab `main` (Muster 4d); Stufe 1 ist bis dahin halb (Archiv, Backlog, Design §9.2/§9.4, README).
**Behoben mit B18** (Abschnitt unten, Merge `95cf421` auf `main`, abgenommen 26.11.; `main` → `release/v0.8.0` als
`7c9c311`); keine Notes-Zeile (Schema 3 war unveröffentlicht). Auf `95cf421` im sauberen Worktree: Build `0 Warnung(en)`,
Core 2543/2543, App 2312/2312, Import 430/430, Ocr 34/34.

**Befund Stufe 1 (CC, 26.11.2026):**
- Doku gegen Code und Pin: README (Feature, Hauling-Daten, Known issues, Credits, P2), Design §9.2/§9.4, settings-format
  (Map, Rückweg auf v0.7.0), routes-format (B18), CLAUDE.md, Nexus, Shot-Liste P1–P5; Zahlen per
  `BaselineHaulingDataTests` 21/21 (587 Templates, 157 Positionen, Boards 92/88/91/89, lokal 23/19/22/20, Pyro je
  Fraktion 31). Wiki-Branch `v0.8.0` (`b3e7eb6`, lokal): neue Seite `Plan-tab`, 12 Seiten nachgezogen; Link-Check 150
  interne Links, 0 kaputt (Gegenprobe: 2 von 2 gemeldet).
- **Downgrade-Hinweis nötig** (Präzisierung zum Startprompt „kein Downgrade-Hinweis nötig“): v0.7.0 liest settings.json
  strikt (`UnmappedMemberHandling.Disallow`) und startet mit einer Datei mit `routes` mit Defaults; belegt in Martins
  Log 26.11. 12:12 (`Settings not loaded, using defaults (The JSON property 'routes' could not be mapped …)`). plan.json
  11 und Missions-JSON 5 lesen beide Versionen, `routes.json` kennt v0.7.0 nicht. → Notes „Files“, Wiki Installation
  und Troubleshooting.
- **Changed für Bestandsnutzer** (für die Notes): „Discard plan“ → „Discard cargo“, „No plan. …“ → „No cargo. …“,
  `Already in plan` → `Already in cargo` (U4, `b3ecacd`); Legendenkopf „· to HUR-L5 ·“ (`b58f6d5`); im Wiki 8 Stellen
  mit altem Wortlaut nachgezogen. „fits as / too big for“ steht nur in der `Observed`-Notice (Plan-Reiter gewählt).
- SignPath: kein Treffer mehr (P5). `0.7`-Grep: übrig nur Geschichte und richtige Spannen.
- **v0.7.0-Gegencheck (P4) bestanden:** Ergebnis im Abschlussvermerk von
  [`phase-13-cargo-routes.md`](docs/tasks-archive/phase-13-cargo-routes.md).
- Screenshots (Martin, 26.11.): P1–P5 alle geliefert, P2 dazu; geprüft gegen die Shot-Liste (Hull B, Everus, kein
  Spielbild, Texte wie im Wiki zitiert). P4 als `docs/images/plan-map.png` im Hauptrepo (README relativ, Wiki per
  Raw-URL, Muster H15; die Wiki-Kopie nach Hash-Vergleich entfernt), P1/P2/P3/P5 in `media/` des Wikis.

**Halt 1 bestanden (Martin, 26.11.2026, „Halt 1 Wiki: bestanden“)** mit E1 (Scanning-contracts: `no hauling template
form` neben den Lesefehlern, Wiki `0e0146c`) und E2 (Backlog: Settings-Reader hält unbekannte Abschnitte, `63bcb36`).

**Befund Stufe 2 (CC, 26.11.2026):** Entwurf `docs/release-notes/v0.8.0.md`, Aufbau v0.7.0: Headline · Updating ·
New: the Plan tab · New: a plan with its stops · New: what your scans teach the planner · Settings · Changed · Files ·
Ship and hauling data · Known issues · Fuß. Jede Zeile hat Commits in der Vollständigkeitstabelle oben (Abschnitt
„In den Notes“); keine Zeile für B18, `--log-input`, Messziele. Gültige Known issues aus v0.7.0 übernommen (ohne den
Rückweg auf v0.6.0, steht im Wiki). Link-Check: 9 externe URLs 200, 3 Wiki-Ziele gegen den Wiki-Branch `v0.8.0`
(`Plan-tab` ist erst nach dem Wiki-Push online → Wiki-Push direkt nach dem Tag).

**Notes freigegeben (Halt 2, 26.11.2026):** `a253d3f` von Claude.ai freigegeben, keine Korrektur; Martin hat sie als
Nutzer gelesen. Stufe 3 wie geplant.

**Stufe 3 – Draft-PR, Merge, Probe (CC, 26.11.2026):**
- Draft-PR [#32](https://github.com/MRiediger/sc-cargoplan/pull/32) nur für CI: Run 45843964257 grün auf
  `ubuntu-latest`, `windows-latest` und „Linux CLI from the release tarball“.
- `CLAUDE.md` hatte in der Arbeitskopie die zwei B18-Vermerke verloren (nicht committet, vermutlich ein Speichern aus dem
  Editor); Martin: verwerfen, Vermerk bleibt.
- Merge `--no-ff` lokal → **`b65bde2`**, Inhalt gleich `release/v0.8.0`. Im sauberen Worktree, SDK 10.0.404: der Build
  holt die Baseline selbst (`ships-2026.11.2`, 113 Dateien gegen das Manifest), Build `0 Warnung(en)`,
  `dotnet format --verify-no-changes` Exit 0, Tests Core 2543/2543, App 2312/2312, Import 430/430, Ocr 34/34.
- `./build/publish.ps1 -Version 0.8.0 -DeltaBase github`: `Delta base: CargoPlan-0.7.0-full.nupkg`, 126 Dateien
  gepatcht, 264 gleich, 2 neu, Delta 1 300 377 B; `Layout check: … passed (390 files in current\, 110 ship files of
  ships-2026.11.2, 5 PDBs, version 0.8.0)`; `Tarball check: … passed (328 files in SC-CargoPlan-Cli/, 110 ship files of
  ships-2026.11.2, CargoPlan.Cli executable, version 0.8.0)`. ZIP 97 222 813 B, Tarball 66 682 586 B. B7-Referenz:
  Runtime 10.0.15 (beide), `coreclr.dll` 10.0.1526.53008, Cli `0.8.0+b65bde2…`.
- Wiki `v0.8.0` → `master` lokal `--no-ff` (`cfef4a9`). Martin (26.11.): `main` gepusht, CI grün (Run 45847101733),
  Tag `v0.8.0` (annotiert) auf `b65bde2`, Wiki gepusht.

**Release-Prüfung (CC, 26.11.2026)**, alle Assets frisch in einen leeren Ordner geladen:

| Prüfung | Soll | Ist |
|---|---|---|
| CI auf `main` (Merge-Push) | grün | Run 45847101733 success |
| Workflow „Release“ | grün | Run 45848124404 success, 3 min 37 s |
| Tag | auf dem Merge | `b65bde2`; annotiert (v0.7.0 war leichtgewichtig) – ohne Folgen für `release.yml` |
| Release | Latest, kein Pre-Release, kein Draft | `isPrerelease false`, `isDraft false`, `releases/latest` = `v0.8.0`, 15:24:07 UTC |
| Body | = `docs/release-notes/v0.8.0.md` | zeichengleich nach CRLF→LF (9 374 Zeichen, UTF-8) |
| Assets | 7 | ZIP 97 221 218, `.sha256` 98, Full 97 265 229, Delta 1 296 902, Feed 733, Tarball 66 680 561, `.sha256` 107 |
| SHA256 ZIP / Tarball | = `.sha256` | beide gleich |
| `releases.win.json` | 0.8.0 voll + Delta, Size/SHA1 = Asset | beide gleich; dazu `Full 0.7.0` 96 835 347 = Asset von v0.7.0 |
| `check-portable.ps1` gegen den Download | passed | `passed (390 files in current\, 110 ship files of ships-2026.11.2, 5 PDBs, version 0.8.0)` |
| `check-cli-tarball.ps1` gegen den Download | passed | `passed (328 files in SC-CargoPlan-Cli/, 110 ship files of ships-2026.11.2, CargoPlan.Cli executable, version 0.8.0)` |
| ZIP-Wurzel | `SC-CargoPlan.exe`, `Update.exe`, `.portable`, `current\` | genau diese |
| Runtime (B7) | = lokales Pack | 10.0.15 beide, `coreclr.dll` 10.0.1526.53008 |
| Cli-Version | `0.8.0+b65bde2…` | `0.8.0+b65bde24a53e5b5083e9a264be0f23194ab6d40d`; `ships --version` mit leerem Datenordner `ships-2026.11.2 (baseline)` |
| `ship-data/` | Hauling-Dateien + Pin | `hauling-templates.json`, `positions.json`, `manifest.json` `ships-2026.11.2`, `hauling` Schema 1 |
| Linux unter WSL (Ubuntu 24.04.2) | entpackt, startet ohne `chmod` | `CargoPlan.Cli` `-rwxr-xr-x`, `--version` `0.8.0+b65bde2…`, `ships --version` `ships-2026.11.2 (baseline)`, beide Hauling-Dateien |
| Start ohne `routes.json` | Plan-Reiter leer, kein Fehler | 3 Starts mit leerem `--data-dir`: `Routes: 0 observations: 0 matched …`, keine Fehlerzeile |
| Hauling beim Start | ≤ 60 ms | `Hauling: 587 templates, 157 positions, 0 left out, 39 ms` (3 × 39 ms) |
| Erster Frame gegen v0.7.0 | unverändert | **nicht erfüllt:** abwechselnd je 3 Starts aus den veröffentlichten ZIPs, leerer Datenordner: v0.7.0 Median 706 ms (707, 706, 692), v0.8.0 785 ms (804, 781, 785), **+79 ms (+11 %)**; erste Serie gleich (730 / 805) → Entscheidung Martin |
| Größe | +≈ 0,2 MB gegen v0.7.0 | ZIP 92,7 MiB (97 221 218 B), +429 882 B (+0,43 MB) gegen v0.7.0; Delta von v0.7.0 1,30 MB; Tarball 66,7 MB → CLAUDE.md |
| Wiki | live | `Plan-tab` 200 mit Footer v0.8.0, `plan-map.png` (Raw-URL `main`) und `media/plan-matrix.png` 200 |
| Szenario B18 am ZIP | eigener Plan mit Extras → „›“ ×2 → Neustart → Reiter vollständig | **offen (Martin)** |
| Auto-Update v0.7.0 → v0.8.0 auf `cargotest` | Notice → Download → Neustart → Version 0.8.0 | **offen (Martin)** |

**Nachmessen Stufe 3 (Ergänzung aus B18, Martin):** eigener Plan mit Extras → „›“ ×2 → Neustart → Plan-Reiter
vollständig, Leiste am selben Stopp – am veröffentlichten ZIP.

**Vollständigkeit `git log --no-merges v0.7.0..main` (107 Commits) → Abschnitt der Notes:**

| Commits | Inhalt | In den Notes |
|---|---|---|
| `5b632f1`, `8be7336` | REL: Nachlauf v0.7.0 | nein (betrifft v0.7.0) |
| `d86dc95`, `a9f4f02`, `5df36bb` | T13.0/T13.0a: Design, Hauling-Format, D118, D119 | indirekt (Data & credits) |
| `f377846`, `a72ddca`, `723ac6e`, `2271864`, `0fd1b1f`, `b9379b4`, `b0f9105`, `e0724f1`, `01cc9cb`, `71da88d`, `d809c5f`, `3011167`, `a4f9647`, `ce4ff01`, `99aa9f2`, `bdfeb97`, `7c8d409`, `a87b503`, `cf74e03` | Doku, Gates, Abnahmen | nein (TASKS.md, Formatdoku) |
| `b2314b5`, `4863d75`, `323e004`, `f2f4aef`, `85411a4`, `5e30e98`, `8529264`, `64de6de` | Tests, Umbau ohne Verhaltensänderung, Messziel `routes` | nein |
| `fecf2c4`, `7482793`, `4aa0c29`, `cfb59c5`, `500850f`, `f7e9d0e`, `33312a6`, `ff43ee2`, `2e706e2` | Titel, Beobachtungen je Template, Orte, Matching, SCU-Bänder | New: Observations; Files |
| `627225a` | Offer-Scan bleibt bei gewähltem Plan-Reiter aus der Ladung (U1) | Changed |
| `1324cee`, `4cf484e`, `2c4d553` | Hauling-Loader, Install mit den Schiffsdaten, Pin | New: Hauling data; Data & credits |
| `67259aa` | Reiterleiste Cargo \| Plan (D120) | New: Plan tab |
| `c65b203`, `e4bf39c`, `e7488d8`, `56d6369`, `6b54273` | Hafen-Matrix, Filter, Kurznamen, beobachtete Kanten, Hub-Markierung | New: Matrix |
| `2b61520`, `b47ebaa`, `d5d6213`, `b32ce5f`, `d60bc1e` | Settings `routes`, Abschnitt „Plan tab“, Zeiten, Sortierung, Reihenfolge | New: Settings |
| `d1cd20f`, `4139f0b`, `a52bc32`, `b0b0816` | Bildlauf der Matrix, Fokus je Reiter (U7), `--log-input` | Changed (Fokus); Wiki (`--log-input`) |
| `75e814e`, `d5d2253`, `90a7fec`, `2651a38`, `e0d8695` | Board eines Hubs, Rezept, Hub-Button, „fits as / too big for“ | New: Recipe |
| `0491d88`, `4a1d6de`, `4367923`, `e7a0a49` | Loop-Ranking, Ansicht Loops, Rückläufer | New: Loops |
| `5caecc7`, `b262422`, `3ff6e41`, `d212210`, `0a307de`, `15261b2`, `80b3be4`, `5256cec`, `164d768`, `cca6f6d`, `99a7590`, `61384f2` | Route mit Platzhaltern, Füllen per Accepted-Scan, Block „Plan“, Stationsleiste, Finish plan, `acceptAt` | New: Start plan / Finish plan |
| `b3ecacd` | Block „Plan“; „Discard cargo“, „No cargo“ (U4) | New + Changed (Wortlaut) |
| `b58f6d5` | Legendenkopf nennt das Ziel kurz („· to HUR-L5 ·“) | Changed |
| `0420b39`, `c403039` | routes.json Schema 3; Start/End lassen plan.json byte-gleich | Files |
| `bf09530`, `6a0be74`, `ab43cdb`, `29ca7aa`, `9cadb99` | Läufe, gemessene Loop-Zeiten, `routes.useMeasured`, Zähler | New: Measured times |
| `1b4b84c`, `5d19ffd`, `7787c96`, `d90d685`, `f37885e`, `b5e18db` | Karte, Routenfarbe Violett | New: Map |
| `f5a5eb9`, `38c71a8`, `a6b709d`, `ba913cd`, `343b562`, `7c5cb8d`, `c00aed6`, `4feffd3`, `1eef097`, `0b60926`, `cbf715c` | eigener Plan aus der Karte und aus einzelnen Missionen, Leiste, Regler | New: Map |

---

## Bugfix B18 – Leiste per Stationsname (Branch `fix/route-at`, 26.11.2026, D126-Addendum)

**Warum:** Gefunden in T13.8 Stufe 1 (Release gestoppt): die App schrieb eine `routes.json`, die sie selbst nicht las.
Eine eigene Route mit Extras (Martins Datei, 12:13:32) stand bei `"at": 6` mit 4 `stations`; seit 12:56:45 meldete jeder
Start `Routes not loaded (… the route's at 6 is no stop of its 4 stations and the hub.)`, der Plan-Reiter lief ohne 13
Beobachtungen, Route und 3 Läufe. Ursache: die Leiste zählt die Orte der Extras mit und ordnet vor dem Abflug neu
(`RouteProgress.StationsOf`, K6), `Route.Step` begrenzt auf ihre Stopps, `RouteDto` prüfte gegen `stations.Count + 1`.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (26.11.2026, „Freigabe B18 wie geplant, K1–K4
ratifiziert“, E1, E2):**
- Richtung C (Martin): `at` ist der Name des Stopps, `stations` und Loop-Schlüssel (D127) unverändert, Schema 3 bleibt
  (unveröffentlicht).
- **K1** Start = `at` fehlt, Ende = Name des Hubs (der Hub steht zweimal in der Leiste). **K2** voller Name wie in
  `stations`. **K3** alte Zahl gegen `[hub, ..stations, hub]`, außerhalb Start + Zeile. **K4** unbekannter Name → Start,
  eine Logzeile, Datei erst bei der nächsten Bewegung.
- **K (Martin):** Rundweg-Test über jeden per `Route.Step` erreichbaren Stopp, mit und ohne Extras; „gleich“ = Bytes nach
  erneutem Schreiben und aufgelöster Index; Martins Datei als Fixture.
- **E1** `ScratchRoutePicture.cs` (Spike T13.6) gelöscht statt angepasst, ihr Eintrag in `.git/info/exclude` mit.
- **E2** Zeile der alten Zahl: „Routes: route position 6 is outside the 4 stations – bar reset to start“.

**Stand (26.11.2026, CC):**
- Core: `Route.At` `string?`; `Route.Step(delta, progress, now)`, `Route.Advance` über `RouteProgress.IndexOf`;
  `RouteProgress.IndexOf(route)` (`null` = kein Stopp), `NameAt(index)`, die Simulation der Rückläufer mit demselben
  Index; `RouteDto.At` als optionales `JsonElement` (Name oder alte Zahl), `Position()` mit K3/E2, Zeile über `Dropped`
  ins Log. App: `ActiveRouteViewModel` Index aus `IndexOf`, ‹ › mit dem Fortschritt, K4-Zeile einmal je Name.
- Tests vor dem Code, rot gesehen (CS0029/CS1503/CS1929: `At` `int`, `IndexOf`/`NameAt` fehlen); dabei ein echter
  Testfehler (CS0136, Name `stops` in `RouteProgressTimingTests`), behoben. Neu: `RoutesFileTests` (Name, Start, Hub;
  alte Zahl 0/1/2/3; 4 und −1 → Start + Zeile; leer/`true`/`1.5` abgelehnt; Martins Datei `Fixtures/Routes/AtIndexCustom`:
  13 Beobachtungen, Route `custom` mit 2 Platzhaltern und 5 Extras, 3 Läufe, Leiste am Start, genau die E2-Zeile),
  `RouteProgressTests` (Rundweg ×4: ohne/mit Extra Teasa, vor/nach Abflug, jeder Stopp bis zum Hub; unbekannter Name),
  `RouteBarTests` (K4: Start, eine Logzeile, nichts gespeichert, › speichert Riker). Bestehende `At = n` auf Namen.
- **Mutationsproben:** Hub-Name als Start → 5 rot; alte Zahl außerhalb still auf den Hub → 3 rot.
- Build `0 Warnung(en)`, `dotnet format --verify-no-changes` Exit 0; Tests Core 2551/2551, App 2313/2313, Import
  430/430, Ocr 34/34.
- Doku: `routes-format.md` (`at` per Name, Abschnitt „Bar position“, Beispiele aus Martins Datei: zwei Beobachtungen,
  Route `custom`, ein Lauf – die alten Beispiele nannten Orte vor `33312a6` und eine Red-Wind-Beobachtung, die keine
  Datei hat), D126-Addendum.

**Gate:**
- CC: Start mit `--data-dir` auf einer Kopie von Martins gesicherter Datei → kein `Routes not loaded`, `13 observations`,
  die E2-Zeile.
- Martin: eigener Plan mit Extras → „›“ ×2 → Neustart → Reiter vollständig, Leiste am selben Stopp.

**Gate CC (26.11.2026, bestanden):** Dev-Build mit `--data-dir` auf einer Kopie von Martins Datei: `Routes: route position
6 is outside the 4 stations – bar reset to start`, `Routes: 13 observations: 12 matched, 0 ambiguous, 1 none`, `station
Everus Harbor (0/5)`; kein `Routes not loaded`; `routes.json` unverändert (13 898 B, K4). Nebenbei `Hauling: 587
templates, 157 positions, 0 left out, 50 ms`.

**Abnahme erteilt (26.11.2026, Martin: „Gate bestanden, B18 abgenommen“).**

**Akzeptanz:**
- [x] Gate CC und Gate Martin
- [x] Merge `--no-ff` nach `main` nach Martins Abnahme, dann `main` → `release/v0.8.0`; Abschnitt beim Schließen ins
  Archiv (K11)

---

## Backlog (nur bei Bedarf)

Kein Plan, sondern Reaktion: wird erst zum Task, wenn der Anlass eintritt.

- Settings-Reader toleriert unbekannte Top-Level-Abschnitte und schreibt sie unverändert zurück (T13.8, Halt 1, E2):
  heute lehnt jede Version eine settings.json mit einem Abschnitt einer neueren ab (`UnmappedMemberHandling.Disallow`)
  und startet mit Defaults – v0.4.0 mit `updates`, v0.5.x mit `shipData`, v0.7.0 mit `routes`; ohne Änderung wiederholt
  sich der Downgrade-Befund bei jeder Version. Gegen: die Strenge fängt Tippfehler (settings-format „A misspelt key
  makes the whole file unreadable“) – unbekannte Schlüssel *in* bekannten Abschnitten bleiben deshalb ein Fehler, nur
  ganze unbekannte Abschnitte (Objekte auf oberster Ebene) werden gehalten; und es hilft erst beim Rückweg *auf* eine
  Version, die es schon kann. D-Eintrag (D105-Addendum). **Auslöser:** der nächste additive Abschnitt in settings.json.
- **F12 VR-Overlay (Phase 14):** CargoPlan in VR lesbar, kein Plan, kein Entwurf vor dem Start. Auslöser: v0.8.0
  veröffentlicht und Martin startet Phase 14.
- Red-Wind-Zwillinge live prüfen (T13.3, offen aus T13.4): Faktions-Zwillinge HH/CFP teilen heute eine mehrdeutige
  Beobachtung, der Fraktionsfilter zeigt die eigene Seite (D122, U5). Auslöser: die erste Pyro-Session mit Scans.
- Zuordnung per Fraktion, wenn die Rewards der Zwillinge abweichen (D122): ein Scan, dessen Reward nur zu einem Zwilling
  passt, ginge eindeutig an diesen statt `Ambiguous`. Auslöser: ein Pyro-Scan, dessen Reward einen Zwilling ausschließt.
- Tooltip-Zeile anklickbar: in Matrix und Karte ein bestimmtes Template einer Zelle wählen statt der ganzen Zelle.
  Auslöser: Nutzerwunsch, ein einzelnes Template in den Plan zu nehmen.
- Pyro-Kisten gegen `legs[].containerScu` der Red-Wind-Templates prüfen (größte Kiste, `hauling-data-format.md`; bisher
  nicht gegen Pyro-Scans geprüft). Auslöser: ein Pyro-Accepted-Scan, dessen Kisten vom Template abweichen.
- Startlauf nach der Wiederherstellung (E1 T13.4: heute kein Fehler, der Start rechnet einmal mit dem gespeicherten
  Schiff). Auslöser: Startzeit-Budget überschritten oder zweiter `loops`-Lauf innerhalb 10 s im Log.
- Testhost-Hänger (T13.3: ein voller `dotnet test` blieb einmal 10 min im App-Testhost stehen, mit
  `--blame-hang-timeout` nicht reproduziert). Auslöser: zweites Auftreten, dann Dump mit `--blame-hang-timeout`.
- Route überall violett (Variante zum Gate-Befund T13.7, D128-Addendum: heute nur auf der Karte in #E040FB). Auslöser:
  Martin oder ein Nutzer will die aktive Route auch in Matrix und Loops markiert sehen.

- Variante B der gemessenen Loop-Zeiten (T13.6c, D127): ein Korrekturfaktor (gemessen ÷ Modell) über alle Läufe des
  Schiffs, angewandt auf jede Loop, auch ungeflogene; heute rechnet nur eine Loop mit ≥ 3 eigenen Läufen mit ihrem
  Median (Variante A). **Auslöser:** Nutzerwunsch oder ≥ 10 Läufe in einer routes.json.

- Standorterkennung ersetzt den Hub-Button des Rezepts (T13.3b, D123): der Hub, an dem der Spieler steht, statt
  `routes.hub`. Auslöser: Game.log-Location wird gelesen.

- QT-Zeit aus Distanz und der Quantum-Geschwindigkeit des Schiffs statt einer Zeit je langem Sprung (T13.4, D124:
  heute `routes.times.jump` je Sprung > 10 Gm). Auslöser: Nutzer-Report, dass das Ranking lange und kurze Sprünge
  falsch gewichtet, oder Schiffsdaten mit QT-Werten.

- Kipprad (`WM_MOUSEHWHEEL`) scrollt die Hafen-Matrix seitwärts (K der Zwischenabnahme T13.3a: heute nur Shift+Rad).
  WPF hat dafür kein Event, es braucht einen `HwndSource`-Hook; derselbe Hook gälte für jede waagerecht scrollbare Fläche
  des Overlays (schmale Bildlaufleisten im Overlay-Stil, T13.2). Auslöser: Nutzer-Report mit Kipprad-Maus.

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
