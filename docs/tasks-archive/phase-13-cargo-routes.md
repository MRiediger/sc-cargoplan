# Archiv: Phase 13 – Cargo-Routen

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 26.11.2026 (T13.8, Stand `2a12976`); Index und offene Tasks stehen dort. Den Release v0.8.0 (T13.8) und den Abschlussvermerk der Phase führt [`release-v0.8.0.md`](release-v0.8.0.md).

## Phase 13 – Cargo-Routen (gestartet 21.11.2026, T13.0–T13.7 abgeschlossen 26.11.2026, D118–D130)

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

### [x] T13.6 Route: Platzhalter, Accepted-Scan füllt, Stationsleiste (abgenommen 25.11.)

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
  | Filterzeile Plan-Reiter | View, Hub (Recipe), Sort (Loops), Rang, System, … | dazu „Start plan“ in Recipe und Loops rechts neben dem System-Button (Gate-Befund 25.11.), Tooltip auch deaktiviert |
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
  | 6 „End plan“; einen Vertrag des Rezepts annehmen und F3 (Accepted) **ohne** Route, dazu einen fremden; dann „Start plan“ | ohne Rescan „✓ #n“ für den Vertrag des Rezepts, „+ #m extra“ für den fremden; cargoplan.log „Routes: start: #n → placeholder k“ / „#m extra“; erneuter F3 → „Already in cargo (#n) · plan ✓“ | Gate-Befund 25.11. (Option 1), K3 als Fallback |
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
- **Gate-Befund 6a (Martin, 25.11.) – „Start plan“ erkennt angenommene Missionen nicht** (Recipe → Start plan → Senior
  annehmen + F3 → End plan → Start plan: 0/10). Ursache: K3 wie freigegeben (nur ein Rescan füllte). **Präzisierung in der
  Planphase, ratifiziert durch Freigabe (Martin, Option 1):** `Route.Adopt` beim Start – jede nicht geschätzte Mission
  der Ladung, die die Route noch nicht hält, in Scan-Reihenfolge gegen die Templates der offenen Platzhalter
  (`TemplateMatch`, Rang/Größe/Auftraggeber aus dem Template, Reward, Waren, Bänder, Stopps, Orte aus der Mission); nur
  ein eindeutiger Treffer füllt, eine Mission höchstens einen Platzhalter; Rest = Extras; **Red Wind nie beim Start**
  (kein Reward, Rang/Größe nur aus dem Template) → Extra, der nächste Accepted-Scan schiebt es in den Platzhalter; K3
  bleibt Fallback; kein neuer Zustand in routes.json; Logzeile je Mission „Routes: start: #n → placeholder k“ / „#n
  extra“. Gate-Schritt 6 neu (oben). Mutationsproben: ohne Eindeutigkeit 1 rot, ohne Covalex-Filter 1 rot. Tests:
  `RouteTests.Start_plan_*`, `RouteTests.A_contract_that_fits_two_placeholders_templates_*`,
  `RouteTests.A_Covalex_contract_with_another_reward_is_no_placeholders_and_a_Red_Wind_contract_stays_open`,
  `RouteBaselineTests.Start_plan_at_Everus_takes_the_accepted_bulk_of_the_cargo_one_of_ten_and_the_other_contract_is_an_extra`
  (Pin: 1/10, ein Extra), `RouteScanTests.Start_plan_takes_the_accepted_cargo_into_its_placeholders_and_the_rest_as_extras_without_a_rescan`,
  `RouteScanTests.A_contract_the_start_cannot_tell_apart_is_an_extra_until_its_rescan_names_the_rank` (Nachfolger von
  `A_contract_scanned_before_the_route_fills_its_placeholder_when_scanned_again`). D125-Addendum.
- **Testnamen:** `RouteTests`, `RouteBaselineTests`, `RoutesFileTests.A_route_*`, `RoutesBookTests`, `RouteScanTests`
  (u. a. `A_contract_scanned_before_the_route_fills_its_placeholder_when_scanned_again` = K3,
  `Starting_and_ending_the_route_leave_plan_json_byte_for_byte`), `RoutesViewModelTests.Start_plan_*`,
  `OverlayRouteXamlTests`; Nachfolger: `RoutesViewModelTests.A_row_without_a_recipe_is_only_marked_on_a_click_and_stays_in_the_loops`
  (vorher `…_does_nothing_on_a_click`), `OverlayLegendXamlTests.Hiding_the_stop_lines_hides_them_and_their_scroll_area_unless_a_route_is_on`
  (vorher `Hiding_the_stop_lines_hides_their_scroll_area`).

**Zwischenabnahme 6a (25.11.2026, Martin):** erteilt nach den Gate-Befunden (Block-Darstellung 15261b2, Start plan
übernimmt die Ladung 80b3be4, „Start plan“ rechts neben dem System-Button 5256cec); damit auch die Präzisierungen von 6a
ratifiziert.

**Stand 6b (25.11., Branch `feat/p13-route`, 164d768..):**
- Commits: `RouteProgress`, `Route.Step`/`Advance`/`Finish` (164d768); Stationsleiste, Finish plan, E(c), Timing (cca6f6d);
  Doku (D126, Backlog, CLAUDE.md, TASKS.md).
- **Präzisierung in der Planphase, ratifiziert durch Freigabe (bei der Umsetzung 6b, zur Abnahme):** nur der Stopp der
  Leiste simuliert seine Rückläufer (die anderen „Unchecked“); ein Ort ohne Position kommt immer vor den Hub; „Gehalten“
  zählt die angenommenen Missionen des Plans (gescannt, nicht geschätzt); E(c) steht in der Notice des Füll-Scans („ ·
  plan: back ✓ 3/10 (planned at HUR-L1)“), kein Zustand dafür; die Leiste rechnet synchron im UI-Thread (gemessen unter
  der 30-ms-Schwelle); Notice nach „Finish plan“ in der Zeile des letzten Ergebnisses (`NoticeSource.Move`, wie ein Scan).
- **Zahlen (Release, `RouteProgressTimingTests`, Hull B, Everus-Ring des Pins im Worst Case, 512/512 SCU, 50 Kisten):**
  `RouteProgress.Of` je Stopp Median 0,1–4,1 ms, erster Aufruf bis 9,3 ms; Rückläufer an L1 16 SCU fits, L4 16, L3 12,
  L5 12, L2 112 SCU fits. App (Debug, Scratch-Datenordner): „Routes: station HUR-L1 (1/7) in 7 ms“, erster Aufruf 53 ms.
- **Mutationsproben:** ohne Permutation bei Extras → 1 rot; Zeit ab Start statt Abflug → 1 rot; Simulation ohne Entladen
  → 1 rot; ohne „jedes Template einmal“ (`Recipe.WaysBack`) → 3 rot (LoopsTests 1, RouteBaselineTests 2).
- Bild (CC, Desktop, Scratch-Datenordner, Hull B, Everus-Ring, Bulk gefüllt, Extra nach Teasa, Leiste an L1, abgeflogen):
  „‹ Station 1/7 · HUR-L1 · unload 1 · free 8 slots ›“ (Teasa als Station vor dem Hub, K6), darunter „accept: Junior Rank
  - Small Cargo Haul · 72,500 aUEC · fits · collect at HUR-L4, HUR-L3, HUR-L5“, dann der Block.
- **Handling gegen main (zusätzlich zu 6a):**

  | Geste | Soll (= main) | Ist Branch (erwartet) |
  |---|---|---|
  | Cargo-Reiter mit Route (neu) | – | Leiste über dem Block, scrollt nicht; ‹ › ohne Fokus; Rückläufer-Zeilen darunter |
  | Abhaken einer Legenden-Zeile mit Route | wie main | gleich; dazu rückt die Leiste nach (letzte Zeile der Station, oder spätere Station) |
  | „Finish plan“ (neu, nur am Hub am Ende) | – | Run in routes.json, Route weg, Ladung bleibt, Notice „Plan finished: …“ |
  | F3 Rückläufer an anderer Station | – | Notice „ · plan: back ✓ k/n (planned at …)“ |
  | Alles ohne Route | wie main | gleich |

- **Gate 6b (Martin, SC vorne, echter Ring Everus, Hull B, Senior):**

  | Schritt | Erwartung | Warum |
  |---|---|---|
  | 1 Route starten, Verträge am Hub annehmen und scannen (Rezept, 5–7) | Block „n/10 filled“, Leiste „Start · Everus Harbor · n contracts · free m slots“; `routes.json` ohne `departedUtc` | Startzustand |
  | 2 Zur ersten Station fliegen, › drücken (oder dort die erste Zeile abhaken) | „Station 1/6 · HUR-L1 · unload k · free s slots“, darunter „accept: … · fits“; `routes.json` hat `departedUtc` | Messung ab Abflug |
  | 3 Abladen abhaken, Rückläufer am Terminal annehmen, Accepted-Tab, F3 | Mission mit Stopp Everus; Rückläufer-Zeile „✓ #n · accept: …“; Slots −1; Holo zeigt neue Kisten; nach der letzten Zeile rückt die Leiste weiter | Füllen am Ring, E(b) |
  | 4 Eine Station mit „too big“ (falls eine kommt) | Zeile nennt Worst-Case-SCU orange; Vertrag trotzdem annehmbar, Scan legt ihn wie heute (ggf. unplaced) | Hinweis, keine Sperre |
  | 5 Alle Stationen, zurück am Hub, abladen, „Finish plan“ | `runs[0]` mit Σ aUEC = Summe der gefüllten + Extras (gegen die Legende), Zeit plausibel gegen die Uhr, Landungen 6; Route weg, Legende bleibt; `plan.json` unverändert | Abschluss |
  | 6 Ohne Route: Cargo-Reiter, Legende, Abhaken, Drag, Undo | exakt wie main | Invariante 1 |

  Zahlen: „Routes: station … in x ms“ aus cargoplan.log, `keyToNoticeMs` der Station-Scans (scans.log), Σ aUEC aus
  `runs[0]` gegen die Summe der Legende. Fokus: ‹ › „Finish plan“ mit `--log-input` (Cargo-Reiter aktiviert nicht, U7).
- **Gate-Befund 6b (Martin, 25.11.) – Wizard gut, Plan-Block stört. Präzisierung in der Planphase, ratifiziert durch
  Freigabe:** (1) die Kopfzeile des Blocks klappt seine Zeilen per Klick ein/aus (▾ offen, ▸ zu; Button im Stil der
  Legenden-Zeilen, kein neues Element), Zustand `routes.planBlock` in settings.json (`"open"` | `"closed"`, Default
  open, Schema 3 bleibt, falsches Wort → open mit Zeile); (2) „Start plan“ klappt auf, der **erste Abflug** klappt zu
  (das erste › oder ein Haken, der die Leiste vom Hub wegschiebt: beides setzt `departedUtc`), ein Klick kippt den
  Zustand und gilt bis zum nächsten Start bzw. Abflug; Tooltip der Kopfzeile in einem Satz; (3) an der Start-Station
  listet die Leiste alle Hinweg-Rollen als „accept: Bulk · Titel · Reward · Ziele“ (gefüllt „✓ #n · …“). „End plan“ und
  die Trennlinie bleiben auch eingeklappt sichtbar. Golden-Dateien der Settings-Migration (3) und die Reihenfolge des
  `routes`-Abschnitts um `planBlock` nachgezogen. Tests: `RouteBarTests.The_block_is_open_from_Start_plan_folds_at_the_first_departure_and_a_click_on_its_head_toggles_it_for_the_next_start`
  (Start → auf, › → zu, Klick → auf, Neustart → bleibt), `RouteBarTests.At_the_hub_at_the_start_the_bar_lists_what_to_accept_there_so_the_block_isnt_needed`,
  `SettingsStoreTests.Routes_keep_the_plan_blocks_state`, `SettingsStoreTests.A_loop_setting_out_of_range_falls_back_with_a_line`
  (planBlock), `OverlayRouteXamlTests.The_blocks_head_is_a_line_button_that_folds_its_list_and_End_plan_stays`.
  Bild-Gate (Martin) danach.
- **Legenden-Kopf (Martin, 25.11.) – Präzisierung in der Planphase, ratifiziert durch Freigabe:** „From Everus Harbor ·
  to HUR-L5 · #1“ – zwischen Pickup und Nummer das Ziel der Zeile darunter (erste Zeile der Mission) in Kurzform über die
  Positionen der Hauling-Daten (`PlaceNames.ShortOf`, eigenes Segment, kürzt sich nie); ohne Hauling-Daten oder Position
  der Kartenname. Befund aus den Daten: „Teasa Spaceport“ ist ein `contractName` der Landezone „Lorville“ → „to Lorville“.
  D69-Addendum. Tests: `PlaceNamesTests.A_place_as_the_card_writes_it_is_its_positions_short_name_else_as_it_is`,
  `LegendLineTests.The_header_names_the_short_form_of_the_destination_of_the_line_under_it`; vier bestehende
  Kopfzeilen-Tests um „· to …“ nachgezogen. Handling gegen main: Legenden-Kopf „From … · #n“ → „From … · to … · #n“.
- **Nachtrag vor dem Merge (25.11., Martin: „Nachtrag in T13.6“, für T13.6c) – Präzisierung in der Planphase, ratifiziert
  durch Freigabe:** `runs[]` schreibt `ship` (Schiff des Overlays beim Finish) und `stations` (die Stationen der Route aus
  der Loop, ohne Extras) – die Loop-Identität für gemessene Zeiten; beim Lesen optional, ein Lauf ohne sie (Gate 6b) misst
  keine Loop; Schema 3 bleibt (unveröffentlicht). `routes-format.md`, D126-Addendum. Tests:
  `RoutesFileTests.A_run_names_its_ship_and_stations_and_one_written_without_them_is_still_read`, Finish in
  `RouteProgressTests` und `RouteBarTests` mit Schiff und Stationen.
- **Testnamen 6b:** `RouteProgressTests` (Stopps, Entladen, Gehalten/Slots, Simulation fits/too big, ohne Entladen too
  big, gefüllt/collect, Extras K6, Advance, Step/Abflug, Run), `RouteBarTests` (Texte und ‹ ›, Abhaken rückt weiter,
  Rückläufer-Zeilen, Finish mit Run und Notice, Finish nur am Ende), `RouteScanTests.Finishing_the_route_leaves_plan_json_byte_for_byte`
  (E1), `RouteScanTests.A_way_back_scanned_where_the_bar_stands_but_planned_elsewhere_says_so` (E(c)),
  `ScanNoticesTests.A_way_back_accepted_at_another_station_says_where_it_was_planned`,
  `OverlayRouteXamlTests.The_station_bar_*`, `OverlayRouteXamlTests.The_bars_buttons_take_no_focus`; von Hand
  `RouteProgressTimingTests` (`Category=Timing`).
- **Abnahme erteilt (25.11.2026, Martin)**, damit auch alle Präzisierungen von 6b, die Gate-Befunde (Plan-Block klappt ein, Leiste am Hub, Legenden-Kopf „· to HUR-L5 ·“) und der Nachtrag `ship`/`stations` in `runs[]` ratifiziert. Merge `--no-ff` nach main, Martin pusht. Nächster Task: T13.6c (gemessene Loop-Zeiten, eigener Startprompt).

### [x] T13.6c Gemessene Loop-Zeiten (Variante A) (abgenommen 25.11.)

**Warum:** Die ~aUEC/h der Loops rechnen nur mit dem Zeitmodell (`routes.times`, D124). Seit T13.6 hält `runs[]` jede
geflogene Route mit Zeit ab Abflug, Schiff und Stationen. Hat der Spieler eine Loop mindestens dreimal mit demselben
Schiff geflogen, rechnet ihre Zeile mit seiner gemessenen Zeit; alles andere bleibt beim Modell, die Slider werden
nicht nachgeführt (U9). Branch `feat/p13-measured`.

**Akzeptanz (Startprompt 25.11., §6):** Pure Function mit Median und Schwelle 3, Schiff im Schlüssel, Modell/Messung nie
gemischt; Schalter `routes.useMeasured` (Default an) in Settings „Plan tab“, wirkt sofort, persistiert; Status
„measured n×“; Golden unverändert; D127; Backlog „routes.times aus runs“ ersetzt durch Variante B; `settings.json`
additiv.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (25.11.2026, „Freigabe T13.6c mit K1“; Rückfragen
Direct-Schlüssel und unter der Schwelle, K1, E1, E2):**
- **P1/P2 Direct-Schlüssel:** `runs[]` trägt für Direct-Läufe `direct: { rank, size, reward }` der Hinweg-Rolle
  (Template per UUID beim Finish, wie `Route.Adopt`); Schlüssel für Direct = Loop-Schlüssel + diese drei, Zwillinge
  (nur andere Ware) teilen die Messung; Ringe ohne Zusatz. Eigenes Objekt, weil `reward` am Lauf die Σ ist. Damit
  schreibt T13.6c in `routes.json` (statt „nur lesen“), additiv, Schema 3 bleibt.
- **P3:** 1–2 Läufe: Zeile Modell, Tooltip „measured 2× – 3 needed“.
- **P4:** `MeasuredTimes.For(runs, shipId)` filtert nach Schiff (wirkt als Schiff im Schlüssel).
- **P5:** Jeder Lauf in `runs[]` ist beendet; Läufe ohne `ship`/`stations` (Gate 6b) zählen still nicht, Logzeile nur
  für < 1 min und > 6 h.
- **P6:** Median gerader Anzahl = Mittel der beiden mittleren.
- **K1:** Läufe in `runs[]` einzeln geprüft: ein ungültiger (`direct` am Ring, fehlendes `finishedUtc`, unbekanntes
  `kind`, fremdes Feld) wird mit Logzeile „Routes: runs[i] left out: …“ verworfen, Datei bleibt lesbar.
- **E1:** Median über die letzten 7 Läufe (`MedianWindow`), der Zähler zeigt alle.
- **E2:** Checkbox-Tooltip „Loops you finished 3+ times with this ship use your median time instead of the values
  above.“

**Stand (25.11., Branch `feat/p13-measured`):**
- Commits: `direct` in `runs[]`, Läufe einzeln (6a0be74); `MeasuredTimes`, `Loops.Rank` mit Messungen (ab43cdb); Plan-Reiter,
  `routes.useMeasured` (29ca7aa); Gate-Fixture, D127, Doku.
- **Zahlen (Release, `LoopsTimingTests.Ranking_with_measured_times`, Hull B Senior, 43 Loops, 16 gemessen aus 48
  Läufen):** `Loops.Rank` Median 0,025 ms mit, 0,015 ms ohne Messungen (erster Aufruf 0,15 ms). Gate-Fixture gegen den
  Pin (`MeasuredGateFixtureTests`): Everus-Ring 877.000 aUEC, Modell 484.233/h, gemessen 657.750/h (Median 80 min).
- **Mutationsproben:** Mittelwert statt Median → 3 rot; Schwelle 1 → 2 rot; ohne Schiffsfilter → 1 rot; ohne Fenster →
  1 rot (erst nachdem der Fenster-Test so gebaut war, dass der Median über alle 120 statt 60 ergibt; vorher grün);
  Schalter ignoriert → 1 rot.
- **Testnamen:** `MeasuredTimesTests` (Median, Schwelle, Schiff, Ausreißer, ohne Identität, gerade Anzahl, Fenster,
  andere Loop, Direct), `LoopsTests.A_measured_row_overtakes_a_model_row_by_the_hour_and_leaves_loop_and_landing_alone`,
  `LoopsTests.A_loop_started_and_finished_is_measured_as_the_same_loop`,
  `RoutesFileTests.A_direct_run_keeps_rank_size_and_reward_of_its_contract_and_a_ring_run_names_none`,
  `RoutesFileTests.A_broken_run_is_left_out_with_a_line_and_the_rest_of_the_file_reads` (4 Fälle),
  `RouteProgressTests.A_direct_runs_finish_names_rank_size_and_reward_of_its_outbound_template_and_a_rings_none`,
  `RoutesViewModelTests.A_loop_flown_three_times_*`, `…Switched_off_*`, `…Two_runs_*`, `…The_times_from_the_settings_*`,
  `…Without_runs_*`, `SettingsStoreTests.Routes_keep_the_measured_times_switch_and_it_is_on_without_one`,
  `SettingsSessionTests.The_measured_times_switch_goes_into_the_routes_section_and_another_window_follows`,
  `SettingsWindowXamlTests.The_plan_tab_section_binds_rank_and_faction` (Checkbox), `MeasuredGateFixtureTests`; von Hand
  `LoopsTimingTests.Ranking_with_measured_times`. Golden-Dateien der Settings-Migration (3) um `useMeasured` nachgezogen.
- **Handling gegen main:**

  | Geste | Soll (= main) | Ist Branch (erwartet) |
  |---|---|---|
  | Loops-Zeile ohne Läufe | „~…/h“, Status templates/observed | gleich |
  | Loops-Zeile mit ≥ 3 Läufen (Schiff gleich) | – | „…/h“ ohne ~, „measured 3× · 1 h 20 min“, Tooltip mit drei Zeiten |
  | Loops-Zeile mit 1–2 Läufen | – | Zeile wie main, Tooltip „measured 2× – 3 needed“ |
  | Settings → Plan tab | Rang, Fraktion, 3 Slider | dazu Checkbox „Use my measured loop times (3+ runs)“ |
  | Slider ziehen | alle Zeilen ändern sich | gemessene Zeilen bleiben |
  | Finish plan (Ring) | Run in routes.json | gleich |
  | Finish plan (Direct) | Run in routes.json | dazu `direct: {rank, size, reward}` |
  | Kaputter Lauf in routes.json | Datei unlesbar, beiseitegelegt | nur der Lauf fehlt, Logzeile |

- **Gate (Martin):** Ordner `tests/CargoPlan.Core.Tests/Fixtures/Routes/MeasuredEverus` (routes.json mit drei Läufen
  70/80/150 min des Everus-Rings, settings.json mit Hull B, Senior, Reiter Plan) in einen Scratch-Ordner kopieren, dann
  `dotnet run --project src/CargoPlan.App -c Release -- --data-dir <Kopie>` → Plan → Loops:

  | Schritt | Erwartung | Warum |
  |---|---|---|
  | 1 Loops öffnen | Everus-Ring „877k/loop · 657k/h · …“ ohne ~, „… · measured 3× · 1 h 20 min“; Tooltip „measured 3×, median 1 h 20 min: 2 h 30 min (24 Nov) · 1 h 20 min (22 Nov) · 1 h 10 min (20 Nov)“ | Kern |
  | 2 Settings → Plan tab → Slider „Per landing“ ändern | Everus-Zeile bleibt, die anderen ändern sich | U9 |
  | 3 Checkbox aus | Everus „~484k/h“, Status „templates“, Tooltip „measured 3× · 1 h 20 min – not used“; Sortierung springt | Schalter |
  | 4 Checkbox an | wieder gemessen | Schalter |
  | 5 Neustart | Schalter und Anzeige wie vorher | Persistenz |
  | 6 Zähler: eigene Daten → Fixture → Schiffswechsel RAFT | eigene Daten: „No loop measured yet with MISC Hull B – finish a plan 3 times.“; Fixture: „1 loop measured with MISC Hull B“; RAFT im Header: „No loop measured yet with ARGO RAFT – finish a plan 3 times.“, Fenster offen lassen, die Zeile folgt | Gate-Befund |

- **Gate-Befund (Martin, 25.11.) – die Checkbox ändert nichts.** Kein Fehler: Martins `routes.json` hatte zwei Läufe aus
  Gate 6b ohne `ship` (13 s und 33 s), also keine gemessene Loop. **Präzisierung in der Planphase, ratifiziert durch
  Freigabe (Variante 2 mit E):** Unter der Checkbox steht, wie viele Loops das Schiff des Overlays gemessen hat:
  „No loop measured yet with MISC Hull B – finish a plan 3 times.“ / „1 loop measured with …“ / „n loops measured with
  …“ (`MeasuredTimes.EnoughCount`); ohne Schiff keine Zeile. Das Fenster bindet an `IMeasuredSummary` (`Text`,
  `Changed`), das `RoutesViewModel` implementiert; kein Verweis auf den Reiter, Tests mit Fake. `Changed` nur bei neuem
  Text (Schiffswechsel, Finish plan). D127-Addendum, `settings-format.md`. Tests:
  `MeasuredTimesTests.Enough_counts_the_loops_with_three_runs_or_more`,
  `RoutesViewModelTests.The_summary_counts_the_measured_loops_of_the_ship_and_tells_when_it_changes` (Grenze 1/2,
  RAFT), `SettingsSessionTests.The_window_shows_the_measured_summary_under_the_switch_and_follows_it_until_it_closes`
  (Fake, Detach), `SettingsWindowXamlTests.The_plan_tab_section_binds_rank_and_faction` (Zeile). Mutationsproben: ohne
  Einzahl-Fall → 1 rot; `Detach` ohne Abmelden → 1 rot.
- **Abnahme erteilt (25.11.2026, Martin)**, damit auch alle Präzisierungen (P1–P6, K1, E1, E2) und der Gate-Befund
  (Zähler unter der Checkbox mit `IMeasuredSummary`) ratifiziert. Merge `--no-ff` nach main, Martin pusht.

### [x] T13.7 Streckenkarte

**Warum:** Die Route vor dem Annehmen planen, wenn man von der Routine abweicht (Design §8).
- Gerichteter Graph, Layout schematisch (Planet = Hub + L-Ring), Option geografisch aus `positions.json`; Kante =
  Template (Fächer/Trichter, gestrichelt = Pool); Filter aus T13.2 (vormals T13.5); Klick → Route, „An Cargo-Tab übergeben“

**Akzeptanz:**
- Renderentscheidung als D-Eintrag im Plan (A9), nicht in eine bestehende Ansicht eingepasst
- Klick und Übergabe schreiben in `routes.json`, nie in plan.json (D118)
- Darstellung am gerenderten Bild geprüft (Lehre T8.2–T8.5)

**Startprompt 26.11.:** Schnitt **7a** Karte lesen (Gate am Bild, Zwischenabnahme) und **7b** eigene Route aus der Karte
(nur nach Martins Entscheidung nach Gate 7a; gestrichen → Backlog „Nutzer will Loops abseits der Ringe“). Branch
`feat/p13-map`. Akzeptanz (§5): vierte Ansicht Map mit derselben Filterzeile; Layout pure, echte Winkel, Überlappungstest
grün; Kanten = Matrixzellen (Konsistenztest); Template- vs. beobachtete Kanten unterscheidbar; Fokus per Knoten
(Sitzung), aktive Route hervorgehoben; Zoom/Pan mit D-Begründung der Rad-Abweichung; Budget gemessen; D128 (ggf. D129
für 7b); plan.json 11, routes.json 3, settings additiv.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (26.11.2026, „Freigabe T13.7a wie geplant“; K1–K4, E zu K3,
E Timing, Hinweis Radius):**
- **Befunde zum Startprompt:** (1) Die L-Stationen liegen an echten Lagrange-Punkten: nur L1/L2 beim Hub (Everus → HUR-L1
  1,29 Gm), HUR-L3 jenseits des Sterns (25,7 Gm von Everus), L4/L5 ±60° auf der Planetenbahn; `parent` der L-Stationen
  ist der Stern. Einen Cluster „Hub + L1–L5“ gibt es nicht. (2) Die Stellar-Boards nennen **Oberflächenorte**
  (Stanton1 17 um Everus, Stanton3 4, Stanton4 1, Pyro Solar ~13 Outposts) – „keine Kanten im Stellar-Board“ trifft nicht
  zu. (3) Bei komprimiertem Radius bleibt nur der **Polarwinkel um den Stern** erhalten, nicht die Richtung zwischen zwei
  Knoten (Everus → Baijini roh −75,5°, mit √r −91,8°): die Winkeltests prüfen den Polarwinkel. (4) **1100 × 700 ist nicht
  die Startgröße** des Overlays (420 × 460, MinWidth 400; Bilder T13.2/T13.4 bei 470 × 700). (5) „Zoom/Pan nur
  RenderTransform“ würde Schrift, Linien und Pfeile 0,5–6× mitskalieren. (6) Freie Fläche ruft `DragMove`; die Karte
  nimmt den linken Klick selbst.
- **K1 Oberfläche (Martin):** ein Knoten je Planet „Hurston (surface)“: jeder Ort, der keine Station ist (Typ nicht
  `Manmade*`), geht zum nächsten Planeten (Datentest); Kante = Summe der Zellen ihrer Orte, Tooltip je Ort; Konsistenztest
  zweistufig (Station = eine Zelle, Oberfläche = Summe); Paare in einem Knoten nicht gezeichnet, in einer Zeile gezählt.
- **K2 Geometrie (Martin):** geografisch, polar um den Stern, Winkel 1:1; Spreizung nur dort, wo das Layout dicht ist (Hub +
  L1 + L2 + Oberfläche, die dichten Pyro-Paare); Tests Polarwinkel ± 2°, HUR-L3 = Everus + 180°, HUR-L4/L5 = ± 60°.
- **K3 Viewport (Martin):** Überlappungstest mit Assert bei 1100 × 700, bei 470 × 700 nur gezählt. **E:** Hubs und
  Oberflächen immer beschriftet, L-Stationen ab Zoom ≥ 1,5× oder wenn ihr Label kollisionsfrei liegt.
- **K4 Zoom (Martin):** nur Projektion neu, Schrift und Linien konstant, < 5 ms je Raste; **E der Freigabe:** die Zeit je
  Raste als `Category=Timing` von Hand, im Render-Test nur „kein Neuaufbau beim Zoom“.
- Matrix-Filter und die Zeile „N templates not shown …“ auch in Map; Fokus ist ein eigener Sitzungszustand der Karte, der
  Ring markiert den Hub, den Recipe zeigt; Kanten-Tooltip mit Gm; „Start plan“ nicht in Map (7b entscheidet).

**Präzisierung in der Planphase, ratifiziert durch Freigabe (bei der Umsetzung 7a, Martin 26.11.):**
- **Radius linear statt Wurzel.** Die geplante Mutationsprobe „Radius linear → Überlappungstest rot“ blieb grün: die
  Radien um den Stern liegen nur 1:4 (10,6–68 Gm; Stanton 11,6–47,8); die 1:40 des Startprompts waren Knotenabstände (Hub ↔
  L1), die die Spreizung löst. Gemessen bei 1100 × 700: linear 0 versteckte Labels, Wurzel 1 (HUR-L2 erst ab 1,5×).
  Martin: linear, Begründung in D128; Mutationsprobe neu **„Spreizung aus → Überlappungstest rot“**; Gm im
  Kanten-Tooltip bleibt (die Spreizung täuscht Abstände zwischen Knoten). Der Rückfall auf log(1 + r/5 Gm) aus der
  Freigabe entfällt damit.
- Stufen der Kantenstärke bei 60 000 und 350 000 aUEC (Terzile der Stanton-Stellar-Kanten des Pins: 63 500 / 344 750).
- Das Layout-Universum sind alle Orte, die die Templates und Scans des Systems nennen (`RouteFilter.Everything`): ein Filter
  blendet Knoten aus, verschiebt keinen. Stationen einer Route sind Pool-Orte (die Route kommt aus einer Loop) und damit
  bekannt; der Start einer Route verschiebt nichts.
- Labels in zwei Durchgängen: bei 1× die mit Platz, die übrigen ihren Platz bei 1,5× (sonst überlappte HUR-L2 dort).

**Stand 7a (26.11., Branch `feat/p13-map`, 1b4b84c..):**
- Commits: Knoten + Layout in Core (1b4b84c); Graph aus der Matrix (5d19ffd); ViewModel, Ansicht Map (7787c96);
  `RouteMapView` (d90d685); XAML, Messziel `map`, Timing (f37885e); Doku (D128, TASKS.md, CLAUDE.md).
- **Datentests gegen den Pin:** Stanton 25 Knoten, Pyro 18, bei 1100 × 700 kein verstecktes Label, keine Überlappung bei 1×
  und 1,5×; bei 470 × 700 gezählt: 0 Überlappungen bei 1× und 1,5× (Stanton, Pyro). Stanton stellar Senior 25 Knoten / 73
  Kanten (2 aus mehreren Zellen), Stanton alle Areas Master 25 / 113, 1 384 Verträge innerhalb eines Planeten; Pyro
  18 / **281 Kanten** (77 aus mehreren Zellen), 13 innerhalb – Red-Wind-Pools nennen fast das ganze System, ohne Fokus ein
  dichtes Netz (Befund für Gate 5).
- **Zahlen (Release, `RouteMapTimingTests`, CC-Rechner):**

  | Karte | erster Aufbau | Aufbau (Median) | Raste (Median) | Raste (max) |
  |---|---|---|---|---|
  | Stanton | 208,5 ms (JIT) | 6,1 ms | 1,21 ms | 8,37 ms |
  | Pyro | 9,6 ms | 9,4 ms | 2,04 ms | 5,83 ms |

  Aufbau = Umschalten auf Map (Matrix → Graph → Szene) plus Layout und Labels der Ansicht, im UI-Thread. Frame-Messung
  `--measure-frames map` auf dem Desktop (CC): Median 8,33 ms, p95 8,33 ms, max 33,6 ms, 117,7 fps. Über dem Spiel:
  Martins Gate.
- **Mutationsproben:** Spreizung aus → 3 rot (Überlappung Stanton, Pyro, Ruin/Terminus); eigene Zählung statt
  Matrixzellen → 4 rot; Fokus ohne Rückkanten → 1 rot; Radius linear statt Wurzel → 0 rot (Anlass der Präzisierung oben).
- Bild (CC, Render-Test mit `CARGOPLAN_MAP_PNG`, 1100 × 700, Segoe UI 12 px): Stanton – Everus rechts bei 0°, HUR-L3
  gegenüber, HUR-L4/L5 oben/unten auf der Bahn, Cluster gespreizt, Everus mit Ring; mit Fokus Everus nur seine Kanten
  und Rückkanten voll. Pyro ohne Fokus ein Netz, mit Fokus Ruin Station lesbar.
- **Testnamen:** `MapNodesTests`, `MapLayoutTests`, `MapLayoutBaselineTests`, `RouteMapTests`, `RouteMapBaselineTests`,
  `RoutesMapViewModelTests`, `RouteMapViewTests` (Render als Control; die Backlog-Zeile aus T13.2, die der Startprompt
  nennt, steht nicht im Repo – erledigt im Sinn des Startprompts),
  `RouteScanTests.The_map_follows_the_active_route_from_its_start_to_its_end`,
  `OverlayRouteXamlTests.The_map_sits_beside_the_matrix_with_scene_focus_and_the_stop_palette_and_the_matrix_filters_show_with_it`;
  von Hand `RouteMapTimingTests` (`Category=Timing`). Nachfolger:
  `RoutesViewModelTests.The_view_cycles_matrix_recipe_loops_map_and_back` (vorher `…_matrix_recipe_loops_and_back`).
- **Handling gegen main:**

  | Geste | Soll (= main) | Ist Branch (erwartet) |
  |---|---|---|
  | View-Button Plan-Reiter | Matrix → Recipe → Loops | … → Map (Rechtsklick zurück) |
  | Area, Direct, Auftraggeber, Fraktion, Größen | nur in Matrix | in Matrix und Map |
  | Linksziehen auf freier Fläche im Plan-Reiter | bewegt das Fenster | in Map: Pan; sonst gleich |
  | Rad | Matrix/Listen scrollen | in Map: Zoom um den Cursor (0,5×–6×) |
  | Doppelklick / Klick auf Knoten (neu) | – | Fit / Fokus für die Sitzung, nochmal = auf |
  | Hover über Kante/Knoten (neu) | – | Tooltip nach der Hover-Zeit des Systems |
  | Gesperrt | click-through | gleich |
  | Alles andere | wie main | gleich |

- **Gate 7a (Martin, entsperrt, Plan-Reiter, Overlay auf ~1100 × 700 ziehen):**

  | Schritt | Erwartung | Warum |
  |---|---|---|
  | 1 View → Map, Stanton, Senior, Stellar | vier Bahnen um die Mitte; Everus rechts, Cluster Hub + L1 + L2 + „Hurston (surface)“; keine überlappenden Namen; Hubs groß, Everus (routes.hub) mit Ring | K2, K3 |
  | 2 Hover Everus → HUR-L5 | Tooltip „Everus Harbor → HUR-L5 High Course Station · 12.9 Gm · n contracts · Σ …“; Template-Zeilen gleich der Matrixzelle (View → Matrix, Zelle vergleichen) | Kante = Zelle |
  | 3 Beobachtete Kanten | durchgezogen mit Punkt in der Mitte, Template-Kanten gestrichelt – Everus-Cluster gegen deine routes.json | §8.3 |
  | 4 Klick Everus, nochmal | erst nur Kanten ab Everus und ihre Rückkanten voll, Rest blass, zweiter Ring; dann alles voll | Fokus |
  | 5 System → Pyro | Stationen und Oberflächen, Red-Wind-Kanten dünn, Tooltip „– aUEC: no reward in the game data, a scan shows it“; Klick Ruin Station macht es lesbar | U5, Befund 281 Kanten |
  | 6 Recipe/Loops → Everus-Ring „Start plan“, zurück in Plan → Map | Route in Orange über allem, Nummern 1–5 an den Stationen | aktive Route |
  | 7 Rad, Ziehen, Doppelklick; Lock | Zoom um den Cursor, Schrift bleibt gleich groß, Pan, Fit; gesperrt click-through | K4, D11 |
  | 8 Zahlen | cargoplan.log „Routes: map Stanton: … in x ms“ / Pyro; `--measure-frames map --over-game` | Budget |

- **Gate 7a bestanden (Martin, 26.11.), alle Schritte und Tests.** Zahlen aus Martins Läufen (Release, Profilordner):
  - cargoplan.log, Stanton (Rank master, Hull B): erster Aufbau je Start 10,8 / 11,1 / 14,0 / 13,4 / 12,3 ms, danach 1,1–3,1 ms
    (einmal 15,6 ms); 73 bzw. 75 Kanten (75 mit den Beobachtungen aus Martins routes.json). Jeder Fokus-Klick und jeder
    Filter baut die Karte neu und schreibt eine Logzeile.
  - Pyro: kein Gate-Lauf (Martin fliegt nicht nach Pyro); Zahl aus `RouteMapTimingTests` (CC): 9,4 ms Median.
  - measure-frames.log, über dem Spiel: `map 25 nodes, 73 edges over game, ocr warm: frames n 2627, median 8.33 ms, p95
    16.67 ms, max 25.0 ms, late 37.0 %, fps 87.6`. Zum Vergleich vom 23.11. über dem Spiel: `empty` 107,9 fps, late 11,2 %;
    `routes` (Matrix) 74,1 fps, median 16,67 ms. Die Karte liegt über der Matrix; die 37 % late sind ein anderer Tag und eine
    andere Szene, eine `empty`-Messung vom selben Tag fehlt.
- **Zwischenabnahme 7a (26.11.2026, Martin)**, damit auch die Präzisierungen von 7a ratifiziert (Radius linear, Stufen
  60 000 / 350 000, Labels in zwei Durchgängen, Layout-Universum). **7b: Martin entscheidet „umsetzen“** – Plan 7b folgt
  nach A7 mit eigener Freigabe.

**T13.7b – Präzisierung in der Planphase, ratifiziert durch Freigabe (26.11.2026, „Freigabe T13.7b wie geplant“):**
- **Befunde der Planung:** (1) Der LEO-Ring aus §8.6 braucht drei Verträge auf Everus → Baijini, der Startprompt sah
  „nochmal klicken entfernt“ vor. (2) Das Format kannte `acceptAt` nur für Rückläufer; der LEO-Ring aus den Loops wählt an
  Baijini/PT Verträge, die als Platzhalter ohne Station an der Start-Station der Leiste als „accept“ standen (Fehler seit
  T13.6). (3) Ein Mehrstopp-Vertrag bringt mit einem Klick mehrere Orte.
- **(1) Klick (Martin):** Linksklick hängt das nächstbeste noch nicht gewählte Template der Kante an, Rechtsklick nimmt das
  zuletzt angehängte dieser Kante weg (die Geste der Filter-Buttons).
- **(2) acceptAt für jede Rolle (Martin):** additiv in Schema 3; die Stationsleiste listet und simuliert an der Station,
  was dort angenommen wird; der LEO-Ring aus Loops bekommt `acceptAt` für seine Verträge unterwegs gleich mit (Befund
  beheben, nicht Backlog); D125-Addendum.
- **(3) Stationsfolge (Martin):** Reihenfolge der geklickten Kantenenden; nie geklickte Orte eines Mehrstopp-Vertrags in
  Ortsordnung vor der Rückkehr zum Hub; Test: Klicks E→B, B→PT, PT→E, Seraphim→E ergeben E → B → PT → Seraphim → E.
- Rest wie im Plan: Rollen ab Start = Hinweg, nur zum Start = Rückläufer, sonst unterwegs; `LoopKind.Custom`, `runs[]`
  `kind: custom`, gemessen über den Loop-Schlüssel, nie eine Zeile in Loops; Name „Own plan from <Start>“ (Bild-Gate).

**Präzisierung in der Planphase, ratifiziert durch Freigabe (bei der Umsetzung 7b, zur Abnahme):**
- Ein Klick wirkt nur auf eine **helle** Kante und nur mit einer Station im Fokus (eine Oberfläche ist kein Start) und
  einem Schiff; „Clear“ leert den Entwurf und lässt den Fokus, ein anderer Fokus oder ein anderes System beginnen neu;
  nach „Start plan“ ist der Entwurf weg (die Karte zeigt ihn als aktive Route).
- Hell sind neben den Kanten ab dem Fokus auch die ab jeder Station des Entwurfs und die Rückkanten zum Fokus von jedem
  Ort, den sie erreichen (Design §8.4 Punkt 1).
- Die Platzhalter stehen in der Reihenfolge der Annahme: Hinweg, dann Station für Station (unterwegs und zurück).
- Kein Größenausschluss wie im Rezept (Large/Bulk): der Spieler wählt die Kante, „fits / too big“ entscheidet.

**Stand 7b (26.11., Branch `feat/p13-map`, 61384f2..):**
- Commits: `acceptAt` für jede Rolle, LEO-Fix, Leiste (61384f2); `CustomPlan` in Core (f5a5eb9); Karte hell und Entwurf
  gezeichnet (38c71a8); App: eigener Plan aus der Karte (a6b709d); Doku (D129, D125-Addendum, routes-format.md, TASKS.md,
  CLAUDE.md).
- **§8.6 per Klick** (`CustomPlanTests`, Templates mit Bändern auf den SCU von §8.6): sieben Klicks ergeben Everus →
  Baijini → Port Tressler → Seraphim → Everus, Σ 767 250 aUEC (§8.6 nennt „≈ 820k“; die Summe seiner sieben Rewards ist
  767 250), 4 Landungen, alle sieben „fits“ für Hull B; Platzhalter mit `acceptAt` Baijini (Stims), PT (Ice, Rückläufer),
  Seraphim (zwei Rückläufer); drei Läufe messen die Route über ihren Schlüssel.
- **Mutationsproben:** Passprüfung ohne Ladung an Bord → 1 rot; Stationsfolge per kürzester Permutation → 3 rot;
  Rückkante ohne `acceptAt` → 2 rot; LEO-Ring ohne `acceptAt` → 1 rot; Leiste nur mit Rückläufern → 1 rot.
- **Testnamen:** `CustomPlanTests`, `RouteMapTests.A_draft_lights_the_edges_from_its_stations_too_and_draws_its_legs_there_and_back`,
  `RoutesFileTests.A_contract_accepted_on_the_way_keeps_its_station_and_a_custom_route_its_kind`,
  `RoutesFileTests.Only_a_way_back_collects`,
  `RouteProgressTests.A_contract_accepted_on_the_way_is_its_stations_to_accept_and_simulated_to_its_own_place`,
  `RouteBarTests.A_contract_taken_on_the_way_waits_for_its_station_and_names_its_place_there`,
  `LoopsTests.The_LEO_ring_takes_what_scans_saw_between_the_hubs_and_unloads_on_the_way` (um `acceptAt` erweitert),
  `RoutesMapViewModelTests.With_a_station_in_focus_a_click_on_an_edge_builds_an_own_plan_that_Start_plan_starts`,
  `…A_right_click_takes_the_edges_last_contract_back_Clear_and_another_focus_drop_the_plan`,
  `…An_edge_the_focus_dims_is_not_picked`, `RouteMapViewTests.An_own_plan_is_drawn_in_the_lines_colour_there_and_back`,
  `OverlayRouteXamlTests.The_map_sits_beside_…` (Leiste, Clear, Kanten-Befehle).
- **Handling gegen main (zusätzlich zu 7a):**

  | Geste | Soll (= main) | Ist Branch (erwartet) |
  |---|---|---|
  | Klick auf helle Kante, Station im Fokus (neu) | – | nächster Vertrag der Kante in den eigenen Plan, Leiste über der Karte |
  | Rechtsklick auf Kante (neu) | – | letzter Vertrag dieser Kante raus |
  | „Clear“ über der Karte (neu) | – | Entwurf weg, Fokus bleibt |
  | „Start plan“ in Map (neu) | – | ab einem Vertrag: Route `custom`, Reiter Cargo |
  | Stationsleiste an einer Station des LEO-Rings | Verträge unterwegs als „accept“ am Start | dort, wo sie angenommen werden, mit Ziel und fits/too big |
  | Block „Plan“ | Hinweg-Zeilen mit Zielen | dazu „· accept at Baijini“ bei Verträgen unterwegs |
  | Alles ohne Entwurf und ohne LEO-Route | wie 7a | gleich |

- **Gate 7b (Martin, entsperrt, Plan → Map, Hull B, Senior; die systemweiten Kanten zwischen den Hubs gibt es nur aus
  deinen Scans):**

  | Schritt | Erwartung | Warum |
  |---|---|---|
  | 1 Klick Everus (Fokus), dann die Kanten des §8.6-Rings in dieser Folge: E→B (so oft wie Verträge), B→PT, PT→E, Seraphim→E | Leiste „Own plan from Everus Harbor: Everus Harbor → Baijini Point → Port Tressler → Seraphim Station → Everus Harbor · … · fits MISC Hull B“; je Vertrag eine Zeile mit fits/too big; Entwurf weiß auf der Karte | Kern 7b |
  | 2 Rechtsklick auf E→B, dann wieder Klick | letzter Vertrag dieser Kante raus und wieder rein | (1) |
  | 3 „Start plan“ | Reiter Cargo; Block mit Hinweg-Zeilen, die Baijini→PT-Zeile mit „· accept at Baijini“, Rückläufer „accept at …“; `routes.json` `route.kind` = `custom` | Übergabe |
  | 4 Leiste: › nach Baijini | dort „accept: … · Port Tressler · fits“ (oder too big) | (2) |
  | 5 „Finish plan“ am Hub (oder End plan) | `runs[]` mit `kind: custom` | D129 |
  | 6 Loops → LEO-Ring „Start plan“ | an Baijini/PT stehen dessen Verträge an ihrer Station, nicht mehr am Start | LEO-Fix |
  | 7 Ohne Entwurf: Map, Matrix, Cargo-Reiter | wie 7a / main | Invariante |

- **Gate 7b angepasst (26.11.):** Martins `routes.json` hat keine Kante zwischen Everus und Baijini (nur Senior 339 500 von
  ARC-L1 nach Baijini/Everus/PT); der §8.6-Ring und die LEO-Schritte 1–3 und 6 waren damit nicht nachbaubar. Gate auf
  Martins Kanten: Teil A Everus-Ring per Klick (Klick, Rechtsklick, Rückläufer, Klickfolge, Clear), Teil B Vertrag
  unterwegs über ARC-L1 (Baijini → ARC-L1 → PT, `acceptAt` ARC-L1, Start, Leiste, Finish mit `custom`), Teil C wie 7a; der
  LEO-Fix ist durch `LoopsTests` belegt, die §8.6-Summe durch `CustomPlanTests`. Kopie des Datenordners mit `--data-dir`.
- **Gate-Befund 7b (Martin, 26.11.) – Präzisierung in der Planphase, ratifiziert durch Freigabe:** (1) Fenstergröße
  ändern und Linksklick auf eine Kante setzten die Karte zurück (wieder reinzoomen nötig); sie muss in der gewählten
  Zoomstufe bleiben. Ursache: jede neue Größe (auch die, die die Leiste des eigenen Plans der Karte nimmt) baute das
  Layout neu und setzte Zoom und Pan auf Fit. Fix: bei neuer Größe oder neuen Knoten bleibt der Zoom, der Pan folgt dem
  neuen Maßstab (`MapLayout.PixelsPerGm`), der Ausschnitt in der Mitte bleibt; Fit nur beim Öffnen der Ansicht und per
  Doppelklick (D128-Addendum). (2) „Own plan from …“ unter die Karte statt darüber. Tests:
  `RouteMapViewTests.A_new_size_or_a_new_scene_keeps_the_zoom_and_the_part_of_the_map_in_the_middle` (Gegenprobe mit dem
  alten Zurücksetzen → rot), `OverlayRouteXamlTests.The_map_sits_beside_…` (Leiste unten).
- **Gate-Befund 7b (Martin, 26.11.) – Präzisierung in der Planphase, ratifiziert durch Freigabe:** Richtungspfeile an den
  Linien nur bei Maximalzoom (6×), sonst zu viel. Gilt für alle Pfeile (Kanten, aktive Route, Entwurf); darunter zeigt der
  Versatz die Gegenrichtung, die Nummern und die Leiste die Folge. D128-Addendum. Test:
  `RouteMapViewTests.The_arrow_heads_show_only_at_the_most_zoom` (Gegenprobe Pfeile immer → rot).
- **Gate-Befund 7b (Martin, 26.11.) – Präzisierung in der Planphase, ratifiziert durch Freigabe:** Pfeile etwas größer
  (Länge 7 → 10 px, halbe Breite 3 → 4,5 px) und in der Mitte der Linie statt an ihrem Ende. Bei 6× liegt der Pfeil über
  dem Punkt ● einer beobachteten Kante (gleiche Farbe); „beobachtet“ zeigt weiter die durchgezogene Linie. Test:
  `RouteMapViewTests.An_arrow_head_sits_in_the_middle_of_its_line_and_is_ten_pixels_long` (Gegenprobe Pfeil am Ende → rot).
- **Gate-Befund 7b (Martin, 26.11.) – Präzisierung in der Planphase, ratifiziert durch Freigabe:** Pfeile ab der Hälfte
  der Rasten zwischen ganz heraus (0,5×) und ganz hinein (6×), keine Zoomstufen bauen (ersetzt „nur bei 6×“). Das Rad zoomt
  mit Faktor 1,2, die halbe Rastenzahl ist das geometrische Mittel √(0,5 · 6) ≈ 1,73× (`RouteMapView.ArrowsFromZoom`): von
  ganz heraus die 7. von rund 14 Rasten, vom Fit die 4. Test: `RouteMapViewTests.The_arrow_heads_show_from_half_the_wheels_notches_on`
  (Nachfolger von `…The_arrow_heads_show_only_at_the_most_zoom`; Gegenprobe Schwelle 6× → rot).
- **Gate-Befund 7b (Martin, 26.11.) – Präzisierung in der Planphase, ratifiziert durch Freigabe:** Die Leiste „Own plan“
  verhält sich wie die Legende im Cargo-Reiter: höchstens ihr Anteil am Overlay (Settings → Legend layout → Max. height,
  dieselbe Regel `LegendMaxHeightConverter` auf die Höhe von `OverlayContent`), darüber scrollt sie (schmale Leiste, nicht
  fokussierbar). Tests: `OverlayRouteXamlTests.The_map_sits_beside_…` (gleiche Höhenregel wie `StopLinesScroll`, scrollt),
  `CustomBarHeightTests.The_bars_highest_height_is_the_legends_share_of_the_overlay_from_inside_the_plan_tabs_template`
  (die Bindung per ElementName löst aus dem DataTemplate des Plan-Reiters auf, A3).
- **Gate-Befund 7b (Martin, 26.11.) – Präzisierung in der Planphase, ratifiziert durch Freigabe:** Die Höhe der Leiste
  stellt ein **eigener Regler „Map legend height“** ein (ersetzt die Kopplung an „Max. height“ der Legende): Abschnitt
  „Plan tab“ des Einstellungsfensters, `routes.mapLegendHeight` (Anteil) in settings.json, 10–90 % in 5-%-Schritten wie der
  Legenden-Regler, Standard 50 %; außerhalb des Bereichs beim Laden 50 % mit Logzeile; additiv, Schema 3. Der Plan-Reiter
  folgt dem Regler ohne Neuberechnung. Golden-Dateien der Settings-Migration (3) um `mapLegendHeight` nachgezogen,
  `settings-format.md`, D129. Tests: `SettingsStoreTests.Routes_keep_the_map_legend_height_and_one_out_of_its_range_falls_back_with_a_line`,
  `SettingsStoreTests.Save_writes_the_routes_after_the_ship_data` (Reihenfolge),
  `SettingsSessionTests.The_map_legend_height_slider_goes_into_the_routes_section_and_the_plan_tab_follows`,
  `SettingsWindowXamlTests.The_plan_tab_section_binds_rank_and_faction` (Regler), `OverlayRouteXamlTests.The_map_sits_beside_…`
  (bindet `MapLegendHeight`), `CustomBarHeightTests.The_bars_highest_height_is_its_own_share_of_the_overlay_from_inside_the_plan_tabs_template`
  (Nachfolger von `…_is_the_legends_share_…`). Gegenproben: ohne Bereichsprüfung → 2 rot; Plan-Reiter folgt nicht → 1 rot.
- **Gate-Befund 7a/7b (Martin, 26.11.) – Route auf der Karte. Präzisierung in der Planphase, ratifiziert durch Freigabe:**
  Befund „Orange zeigt die Vertragskanten (Speichen), nicht die Flugfolge“ geprüft: Orange **war** die Flugfolge (`Legs`,
  3 px); die Speichen waren die Kanten des Filters in Rangfarbe (Okabe-Ito Senior Vermillion, Trainee Orange) und die Legs ab
  den neben Everus gespreizten L1/L2; die Verträge der Route waren gar nicht markiert. Umgesetzt (Martins Wahl nach der
  Rückfrage, D128-Addendum): Verträge der Route orange durchgezogen (Platzhalter: acceptAt bzw. Hub und collectAt → Ziele),
  Flugfolge gepunktet (2 px alle 6 px, 1 px dunkler Halo `MapHaloBrush`, Auflage 1), Stationsnummern als orange Pillen oben
  links (Hub ohne), Extras orange gestrichelt mit Tooltip „A → B: not in your plan“ + „+ #4 extra · …“ (`MapExtra.Of` aus dem
  Plan, `ActiveRouteViewModel.ExtraWays`), Rest auf 25 % ohne Fokus (lit = Route ∪ Extras ∪ Entwurf, Auflage 3), Hilfezeile
  „· orange: your plan“. Entwurf (7b) im selben Schema in Weiß (R1), Pillen oben rechts (Auflage 2). Neu `Core/Routing/MapPlan.cs`
  (`MapPlan`, `MapContract`, `MapExtra`, `MapPlanLines`, `MapExtraEdge`); `RouteMap.Active`/`Draft`/`Extras`/`Extra()` statt
  `Legs`/`Numbers`/`DraftLegs`. Tests: `RouteMapBaselineTests.The_Everus_rings_stations_are_numbered_L1_1_L4_2_L3_3_L5_4_L2_5_…`
  (Pin, Hull B: Pillen L1=1, L4=2, L3=3, L5=4, L2=5, Akzentlinie = Legs, Rest gedimmt), `RouteMapTests` (+4: Entwurfsnummern,
  lit ohne Fokus, Vertragskanten, Extras), `MapPlanTests` (2), `RouteMapViewTests` (+5: gedimmt mit Route, Halo, Entwurf weiß,
  zwei Pillen ohne Überlappung, Extra-Tooltip), `RoutesMapViewModelTests` (Hilfezeile, Extras), `RouteScanTests.The_map_draws_an_extra_…`.
  Gegenproben: Rest nicht gedimmt → 2 rot; Entwurfskanten gedimmt → 1 rot; Nummer am Hub → 3 rot; Vertragskanten = Legs →
  4 rot; Entwurfspille links → 1 rot. Sichtprüfung am Pin-Bild (Everus-Ring, 6× auf Everus): mit 1,5 px alle 5 px schluckte der
  Halo den Punkt, daher 2 px alle 6 px; die Abnahme des Aussehens bleibt bei Martin.

**T13.7c – Plan aus einzelnen Missionen (Martin, 26.11., neue Anforderung nach Gate 7b; Branch `feat/p13-map`, D130):**
Pläne entstehen aus einzelnen Missionen, nicht über Station oder Bündel. Ersetzt den Klick- und Reihenfolgeteil von
T13.7b (D129); Art `custom`, `acceptAt`, Leiste, Start plan und Messschlüssel bleiben.
- **Akzeptanz:**
  - [x] Klick auf eine Kante → angeheftetes Panel mit den Missionen der Kante, je Zeile [+] (und [−], wenn im Plan).
  - [x] Liste unter der Karte: Plan-Zeilen mit [−], mit Fokus darunter „Missions at <Station>“ je Zeile [+].
  - [x] Stationsklick ist Filter, kein Pflichtschritt; er behält den Plan.
  - [x] Start = Hub des Boards; Stopps = Pickups ∪ Ziele; Reihenfolge berechnet (Pickup vor Ziel); Pillen nach ihr.
  - [x] Mission eines anderen Boards abgelehnt mit „different board“.
  - [x] Kapazität: Peak entlang der Route (Pickups +, Ziele −) mit First Fit gegen das ganze Schiff; nicht passend →
    aufgenommen und markiert „likely doesn't fit at …“, nicht gesperrt.
  - [x] Kanten-Tooltip „k/n in plan“.
- **Präzisierung in der Planphase, ratifiziert durch Freigabe:** R1 Start = Hub des Boards; ohne gewählte Mission das Board
  von routes.hub (der Hub, den Recipe zeigt), Pyro `Solar`; Pyro ohne Hub (D121): Pickup der ersten Mission. R2 SCU-Peak +
  First Fit, frei = ganzes Schiff, „likely doesn't fit“ markiert, nicht gesperrt; Restplatz-Variante ggf. später als Option.
  R3 Plan + Missionen des Fokus (Fokus-Zeilen ohne fits je Zeile: jede würde die Folge neu rechnen; das Panel zeigt fits).
  R4 Entwurf ohne Fokus wirkt wie Fokus auf seinen Start (ersetzt Auflage 3 für diesen Fall). Systemweite Stanton-Templates
  liegen auf jedem Stanton-Board (`Board.On`), §8.6 bleibt klickbar. Zwei Missionen je Richtung zwischen zwei Orten: eine
  Regel bricht, die Mission „a stop before its pickup“. Über 8 Stopps nächster Nachbar mit Pickup vor Ziel. Panel in der
  Karte (oben rechts), kein Popup; schließt per ×, freie Fläche, dieselbe Kante. Rechtsklick auf Kante bleibt (letzte
  Mission der Kante raus).
- **Befunde der Umsetzung:** (1) Die übrigen Ziele einer Mehrstopp-Mission nach Ortsordnung legten die Legs anders als
  §8.6 (S vor PT); jetzt nach der berechneten Flugfolge. (2) `Loops.Order` brauchte für 8 Stopps mit 4 Regeln 130 ms (je
  Permutation neue Arrays, zwei Positionssuchen je Leg); jetzt Distanzmatrix + Heap-Permutationen: **6,3 ms** (Release,
  `CustomPlanTests.Eight_stops_with_pickups_before_their_stops_are_ordered_in_under_50_ms`, `Category=Timing`). Loops gegen
  den Pin danach: Hull B Master erster 198 ms, Median 119 ms; Senior 109/99 ms (Budget 200 ms). Alle Loops-Baselines
  unverändert grün. (3) Ein eigener SCU-Check neben `Recipe.Fits` wäre doppelt: `LoadPlanner.Lays` prüft die SCU zuerst.
- **Tests:** `CustomPlanTests` (neu geschrieben: §8.6 aus Missionen 767 250 aUEC, Peak 463 an Everus; Start am Hub bei
  Rückweg zuerst; Folge statt Klickfolge; Pickup vor Ziel; Kreis → „behind“; Board Stanton/systemweit/Pyro; Peak Hull A mit
  Markierung und Abladen; Missionen und „k/n“; [−]/Rechtsklick; Messschlüssel; > 8 Stopps; leer), `RouteMapTests` (R4,
  Fokus auf anderer Station hält Entwurfskanten), `RoutesMapViewModelTests` (Panel, [+]/[−], Fokus-Liste, different board,
  likely doesn't fit, no ship, Panel schließen), `RouteMapViewTests`, `OverlayRouteXamlTests` (Panel, Zeilen-Template),
  `MissionRowBindingTests` (A3: ElementName aus dem Zeilen-Template in Leiste und Panel).
- **Gegenproben:** Pickup-vor-Ziel aus → 1 rot; kein Abladen → 2 rot; Board-Prüfung aus → 2 rot; First Fit aus → 1 rot;
  R4 aus → 1 rot; Entwurfskanten unter Fokus gedimmt → 1 rot; Fokus löscht Entwurf → 1 rot.
- **Handling gegen den Branch-Stand 7b:**

  | Geste | 7b | 7c |
  |---|---|---|
  | Linksklick Kante | nächster Vertrag (nur mit Fokus) | Panel der Kante (immer) |
  | Rechtsklick Kante | letzter der Kante raus | gleich |
  | Klick Knoten | Fokus, löscht Entwurf | Fokus als Filter, Plan bleibt |
  | Klick freie Fläche | – | schließt das Panel |
  | [+] / [−] | – | Mission an / ab |

- **Gate 7c (Martin, entsperrt, Plan → Map, Hull B, Senior, routes.hub Everus):**

  | Schritt | Erwartung | Warum |
  |---|---|---|
  | 1 Ohne Fokus Kante Everus → HUR-L1 klicken | Panel oben rechts „Everus Harbor → HUR-L1 … · 0/n in plan“, Zeilen mit „· fits“ und [+] | Panel |
  | 2 [+] auf zwei Zeilen | Zeilen „· in plan“ mit [−]; Leiste unter der Karte mit beiden, Summe mit „peak …/… SCU at Everus Harbor“; weiße Pille 1 an L1; Tooltip der Kante „2/n in plan“ | Plan |
  | 3 HUR-L1 anklicken | Plan bleibt; unter ihm „Missions at HUR-L1“ mit [+] | R3 |
  | 4 [+] auf einen Rückweg HUR-L1 → Everus | Zeile „Back · … · accept at HUR-L1“; Start bleibt Everus | R1 |
  | 5 Kante eines anderen lokalen Boards (z. B. ab Baijini) öffnen | Zeilen „· different board“ ohne [+]; systemweite mit [+] | Board |
  | 6 Missionen nachlegen, bis es nicht passt | Zeile „· likely doesn't fit at …“, trotzdem im Plan, Summe „n likely don't fit …“ | R2 |
  | 7 [−] in der Leiste, Rechtsklick auf eine Kante | Mission raus, Pillen nummerieren neu | Folge |
  | 8 ×, freie Fläche, dieselbe Kante | Panel zu | Panel |
  | 9 Start plan | Reiter Cargo mit Platzhaltern; Route auf der Karte violett (Gate-Befund Routenfarbe) | Übergabe |

- **Gate-Befund (Martin, 26.11.) – Routenfarbe fällt mit einer Rangfarbe zusammen. Präzisierung in der Planphase,
  ratifiziert durch Freigabe:** Das Routen-Amber #FFB000 (`NoticeForegroundBrush`) liegt ΔE 9,2 vom Okabe-Ito-Orange
  #E69F00 (Trainee). Bild-Gate mit zwei Varianten (Pin, Everus-Ring, Hull B, Senior, Fit/2×/6×; Wegwerf-Spike, nicht
  committet): A eigener Ton, B Rangfarbe mit Glow. **Martins Wahl: A**, `MapRouteBrush` **#E040FB** in App.xaml für
  Verträge, Extras, Punktlinie und Pillen der Route auf der Karte; `NoticeForegroundBrush` bleibt für Hinweise und den
  Cargo-Block; Hilfezeile „· violet: your plan“. Auswahl aus 15 Kandidaten nach dem kleinsten ΔE*ab zu allen 20
  Rangfarben der drei Paletten: normal **48,4** (Tol Muted Purple), Deuteranopie **22,0**, Protanopie **45,8** (Okabe-Ito
  Blau), zu Weiß ≥ 73. Datentest `RouteToneTests` (Farbe aus App.xaml, ΔE ≥ 20 fix für alle drei Sehweisen, Rechnung an
  Weiß/Schwarz = 100 geprüft); Gegenprobe #FFB000 als Routenfarbe → 3 rot. D128-Addendum.

- **Abnahme erteilt (26.11.2026, Martin: „Volle Abnahme“)** für T13.7 (7a, 7b, 7c), damit auch alle Präzisierungen und
  Gate-Befunde ratifiziert: Zoom bleibt bei neuer Größe, Leiste unter der Karte mit eigenem Regler „Map legend height“,
  Pfeile ab der Hälfte der Rasten, Route auf der Karte (Verträge, Flugfolge mit Halo, Pillen, Extras, Rest 25 %),
  Plan aus einzelnen Missionen (D130), Routenfarbe Violett (#E040FB). Merge `--no-ff` nach main, Martin pusht.
  Nächster Task: T13.8 (Release v0.8.0).
- **Nachtrag (T13.8, 26.11.2026):** nach der Abnahme `d60bc1e` (Merge `2a12976`, Branch `feat/p13-settings-order`): im
  Einstellungsfenster steht der Abschnitt „Plan tab“ nach „Hotkeys“, vor „Updates“ – der Abschnitt wird beim Spielen
  gestellt, Update- und Schiffsdaten-Check selten. Nur Reihenfolge, keine Bindung, kein Text geändert.

---

## Abschlussvermerk (T13.8, 26.11.2026)

Nach dem Verschieben angehängt, nicht Teil des wortgleichen Blocks.

**Data-Release-Verträglichkeit (T13.8 Stufe 1, P4):**
- **Nachweis aus H1 (D119, Format-Addendum K2):** der Manifest-Reader von v0.6.0 und v0.7.0 lässt in `files` nur
  `outlines.json` und `ships/**` zu und überspringt unbekannte Abschnitte; deshalb liegen die Hauling-Dateien im
  Abschnitt `hauling`, nicht in `ships.zip`, `files` oder `assets`.
- **Gegenprobe am veröffentlichten v0.7.0-ZIP** (CC startet, Martin klickt): `SC-CargoPlan-v0.7.0-win-x64.zip` frisch
  von GitHub (96 791 336 B, SHA256 = `.sha256`), entpackt in den Scratchpad, Start mit leerem `--data-dir`. Log:
  `Ship data: in use: ships-2026.11.1 (baseline) …, 110 ships` → `Update: nothing newer than 0.7.0` →
  `Ship data: ships-2026.11.2 available (in use: ships-2026.11.1)` → Klick **Update** (14:31:23) →
  `Ship data: installed ships-2026.11.2 (downloaded) …` → `restarting with [--data-dir …]` →
  `Ship data: in use: ships-2026.11.2 (downloaded) …, 110 ships` → `Ship data: nothing newer than ships-2026.11.2`.
  Keine Fehlerzeile. Im installierten Ordner `ships\ships-2026.11.2\`: `ships\` (110 Dateien), `manifest.json`,
  `outlines.json` – die Hauling-Dateien lädt v0.7.0 nicht, wie vorgesehen.

**Release:** Phase 13 geht mit v0.8.0 raus (T13.8, `docs/tasks-archive/release-v0.8.0.md`); der Blocker B18
(`routes.json` mit `at` als Index) ist vor dem Release behoben (Merge `95cf421`).
