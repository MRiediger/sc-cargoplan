# Archiv: Release v0.8.0

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 26.11.2026 (T13.8, Nachlauf); Index und offene Tasks stehen dort. Phase 13 selbst: [`phase-13-cargo-routes.md`](phase-13-cargo-routes.md); B18: [`bugfix-b18-route-at.md`](bugfix-b18-route-at.md).

## Release v0.8.0 – T13.8 (Abschluss Phase 13, Branch `release/v0.8.0`)

Phase 13 (T13.0–T13.7, D118–D130) steht im [Archiv](docs/tasks-archive/phase-13-cargo-routes.md); hier bleibt der Release bis zu seinem Abschluss.

### [x] T13.8 Release v0.8.0 (abgeschlossen 26.11.2026)

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
| Szenario B18 am ZIP | eigener Plan mit Extras → „›“ ×2 → Neustart → Reiter vollständig | bestanden (Martin, 26.11.) |
| Auto-Update v0.7.0 → v0.8.0 auf `cargotest` | Notice → Download → Neustart → Version 0.8.0 | bestanden (Martin, 26.11.) |

**Halt 3 (Martin, 26.11.2026):** „Erster Frame akzeptiert, Backlog-Zeile wie vorgeschlagen; Gates bestanden. aufräumen
und fertig.“ Größen in CLAUDE.md (Distribution: Publish-Ordner 202 MiB, 390 Dateien; ZIP 92,7 MiB, Delta von v0.7.0
1,30 MB; Tarball 66,7 MB, 328 Dateien).

**Nachlauf (CC, 26.11.2026):**
- `release/v0.8.0` lokal und remote gelöscht; PR #32 hat GitHub mit dem Push von `main` selbst auf *merged* gesetzt.
  Gemergte lokale Branches `fix/route-at`, `feat/p13-loops`, `feat/p13-map`, `feat/p13-measured`, `feat/p13-route`
  gelöscht (`git branch -d`); Wiki-Branch `v0.8.0` gelöscht. Beide Repos haben nur noch `main` bzw. `master`.
- Release-Hygiene: keine rc-Reste, Holo-Releases 2 (`holo-v2`, `holo-v3`; Löschung per Backlog ab 31.01.2027).
- Design §9.4: Phase 13 abgeschlossen, Phase 14 „VR“ nur als Backlog-Zeile F12.
- Nexus-Upload (Texte `docs/nexus/` auf 0.8.0) und Ankündigungen (Reddit, Spectrum, Sciirof, Torlek, ggf. Reikal) macht
  Martin, außerhalb von TASKS.md (wie seit K12).
- Dieser Abschnitt geht ins Archiv (`docs/tasks-archive/release-v0.8.0.md`), B18 nach
  `docs/tasks-archive/bugfix-b18-route-at.md`.

**Akzeptanz (Startprompt 26.11.):**
- [x] Archiv Phase 13 (`phase-13-cargo-routes.md`, 1184 von 1185 Zeilen gleich) und Backlog mit Auslösern
- [x] Doku gegen Code und Pin geprüft (Befund Stufe 1), Wiki live
- [x] Notes freigegeben (Halt 2, keine Korrektur)
- [x] Tag `v0.8.0` mit Nachmess-Tabelle, inkl. Auto-Update v0.7.0 → v0.8.0 und Szenario B18 (Martin)
- [x] Wiki und Nexus-Texte auf 0.8.0 (Upload auf Nexus: Martin)
- [x] keine offene Zeile in Phase 13; DoD auf `main` (`b65bde2`, sauberer Worktree: 0 Warnungen, 5319/5319 Tests)

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
