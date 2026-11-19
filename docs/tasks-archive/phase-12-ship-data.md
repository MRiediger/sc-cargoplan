# Archiv: Phase 12 – Ship data channel

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Phase 12 – Ship data channel (gestartet 16.11.2026, abgeschlossen 19.11.2026, D111)

**Zielbild:** D111. **Zwei Repos:** [`sc-cargoplan-shipdata`](https://github.com/MRiediger/sc-cargoplan-shipdata) baut, prüft und
veröffentlicht die Schiffsdaten als Release `ships-YYYY.MM[.N]`; dieses Repo liest sie (App) und zieht beim Build die Baseline.
Entscheidungen der Pipeline: **SD1–SD9** dort in `docs/decisions.md`, Manifest-Vertrag dort in `docs/manifest-format.md`.

**Im Shipdata-Repo** (Plan, Befunde und Abnahme in dessen `TASKS.md`): S12.0 Spike, T12.0 Doku (diese Phase hier, D111),
T12.1 Pipeline `run.py`, T12.1b Nachträge, T12.2 Workflow `ship-data.yml`. **Hier:** die Tasks mit Präfix **App:**.

- [x] **`-Create` setzt den `modelKey` selbst** (E1 aus V2, Martin 16.11.) – erledigt im Shipdata-Repo mit T12.1 (P6:
  `-KeyMap`, `modelKey` aus dem letzten Release, kein Anker in der Rack-Mitte). Anlass war die 350r, die in `92d9d99`
  ihren Key verlor; `ShipOutlineLoaderTests.Every_outline_belongs_to_a_shipped_ship` bleibt als Netz.

### [x] T12.3 App: Datenkanal (18.11.2026, `9e3c620`, `ab06ae8`, `2f41d0a`, `12a16d2`, abgenommen 18.11.)

**Warum:** Ohne Leser ist das Release wertlos.
- Manifest-Check nach dem Velopack-Check (eine URL `…/releases/latest/download/manifest.json`), `schemaVersion`-Prüfung (unbekannt → „Ship data needs app update“)
- Notice-Zeile mit Update/Skip, Download nur `manifest.json` + `ships.zip` (`assets.ships`, SD4), Prüfung gegen `files`, atomarer Tausch unter `%LocalAppData%\SC-CargoPlan\ships\<version>\`, Reload ohne Neustart
- Plan-Verträglichkeit: geänderter Rack-Satz → „re-plan recommended“
- Settings „Ship data“ (`shipData.checkOnStartup`, `skippedVersion`), CLI `ships --version`
- Netzzugriff 3 → eigener D-Eintrag (CLAUDE.md, Zeile „Netz“)

**Akzeptanz:** Gate am Aufzug, SC im Vordergrund: Update einspielen, Galaxy erscheint, Holo lädt, Plan bleibt.

**Plan (18.11.2026, Branch `feat/p12-data-channel` ab `43b6a41`, Freigabe Martin 18.11.)**

Lage: zwei Data-Releases in `sc-cargoplan-shipdata`, `ships-2026.11` (Seed = `ships/` dieser App) und `ships-2026.11.1`
(Latest: `hyun` neu, F7C, 8 Schiffe mit `missingSince`/`follows`). Die Akzeptanz oben ist präzisiert: `hyun` statt
Galaxy, F7C lädt mit Holo aus holo-v3 **oder** Umriss (Holo aus dem Manifest ist T12.4), Hull-A-Plan bleibt,
`ships --version` nennt `ships-2026.11.1 (downloaded)`; zweiter Start ohne Notice, Skip, Offline-Start ohne Fehler.

**Spike-Frage (gemessen 18.11., ändert das Design nicht):** `…/releases/latest/download/manifest.json` → **2 Redirects**
(github.com 302 → `releases/download/ships-2026.11.1/manifest.json` 302 → `release-assets.githubusercontent.com` 200),
**0,56 s** (curl), Manifest **127 224 B** (111 `files`, 109 `ships`, 102 Holos), `ships.zip` **102 411 B**. `HttpClient`
folgt per Default HTTPS→HTTPS; `HoloSource` lädt über dieselbe Asset-Kette.

**Grep-first (A1):** `ShipLoader.LoadAll(folder)` und `ShipOutlineLoader.Load(path)` sind schon parametrisierbar
(`outlines.json` an der ZIP-Wurzel passt ohne Änderung); `ReadableSchemaVersions` ist privat → `ShipLoader.IsReadable`.
`UpdateViewModel` (Zustandsautomat) und `UpdateSettingsViewModel`/`UpdateSettings` werden **wiederverwendet**, Texte und
Versionsvergleich als Parameter (zweite Verwendung, A4). `HoloSource` bleibt unberührt (T12.4 führt beide Fetches
zusammen). `AtomicFile` kann nur Einzeldateien → Ordnertausch neu. Kein Single-Instance-Mutex → Neustart in `OnExit`
nach dem Flush. Die CLI kennt `AppFolders` nicht → Auswahl des Stands in Core.

**Übernahme per Neustart, nicht Live-Reload** (D111 sagte „ohne Neustart“): Live hieße `OverlayViewModel.Ships`
(get-only) und `Outlines` (init-only) tauschen, `Ship` umhängen, Auswahl-Cache verwerfen, den geladenen Plan gegen neue
Racks prüfen (läuft heute nur beim Laden), Holo und Szene nachziehen. Update = Download + Prüfung + Tausch + Neustart,
Plan über `plan.json`, D59 prüft beim Laden. Gegenargument: ~2 s, der Editor-Entwurf geht (wie beim Exit, D19);
Live-Reload bleibt Backlog.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. **settings.json bleibt Schema 3** (R1): `shipData { checkOnStartup, skippedVersion }` optional wie `updates`
   (D105-Addendum T11.0: ein Schritt auf 4 änderte beim Downgrade nur die Meldung). Gegenargument: Am `schemaVersion`
   ist nicht zu sehen, ob `shipData` drin sein kann.
2. **Auswahl und Installation in Core, Netz in App:** Core `ShipData/` (Dateien, ZIP per `System.IO.Compression`, kein
   Netz, keine Windows-API), App `ShipData/` (HTTP, Gate). `AppProfile.FolderName = "SC-CargoPlan"` in Core, von
   `AppFolders` genutzt.
3. **Unbekannte Rack-Id im Plan = Racks geändert** (R3): `PlanFileDto.Checked` wirft heute → plan.json wird beiseitegelegt
   (D106), der Plan ist weg; ein Datenupdate, das ein Grid streicht, träfe genau das. Neu → `RacksChanged` → D59-Pfad
   „Plan was made for other racks, re-planned for …“. Gegenargument: eine von Hand verdorbene Rack-Id wird still neu
   geplant. Nachfolger für `PlanFileTests` `unknown_rack` zuerst.
4. **Dev-Build prüft nur mit `--ship-data-source`** (anders als `UpdateGate`: `0.0.0-dev` ist für Datenstände kein
   Hindernis, aber `dotnet run` soll nicht bei jedem Start GitHub fragen).
5. **`--ship-data-source <ordner|url>`**: Ordner = flach mit `manifest.json` und Assets; `https://github.com/<owner>/<repo>`
   = gleiches Schema wie GitHub. Vollqualifiziert, bleibt im Neustart.
6. **„Ship data needs app update“** als einfache Notice (`NoticeSource.ShipData`), keine Controls, nur wenn der Stand neuer
   als der aktive ist.
7. **Aufräumen beim Start:** `*.tmp` weg, heruntergeladene Stände **älter als der aktive** weg (einer bleibt), neuere mit
   unbekanntem Schema bleiben.
8. **Check now** im Abschnitt „Ship data“ (der wiederverwendete VM hat ihn).
9. **CLI `plan` liest denselben aktiven Stand**; `ships --version [--data-dir <pfad>]`.
10. **Kaputter heruntergeladener Stand kostet nie den Start:** Log + Notice, Baseline lädt; nur eine kaputte Baseline
    beendet die App (D56).
11. **Push:** der Feature-Branch durch CC, `main` nie (R4).
12. **`ships/modules/` in Install und Zip-Slip-Regel** (E1): erlaubt genau `outlines.json`, `ships/<name>.json`,
    `ships/modules/<name>.json`; Test: nach `Install` lädt `aurora_mk_ii_ts_module`, `ships/modules/x/y.json` und
    `ships/sub/z.json` werden abgelehnt.

**Aufbau:**
- **Core `ShipData/`:** `ShipDataVersion` (`ships-YYYY.MM[.N]`, Ordnung, `Display` `2026.11.1`, `Baseline = ships-2026.11`
  bis T12.5); `ShipDataManifestReader` (STJ, unbekannte Felder ignoriert; Grenze: Version parsebar, Asset flach, Pfade nach
  12, Hash 64 Hex); `ShipDataStore`: `Active()` = höchster Ordner `ships\<v>\` mit gültigem `manifest.json`,
  `dataVersion` = Ordnername, lesbarem Schema und ≥ Baseline, sonst Baseline neben der Exe; `Install` (`<v>.tmp\`, nur
  Einträge aus `files`, Zip-Slip per `GetFullPath`, 64 MiB entpackt höchstens, jede Datei gegen `files`, Menge gleich,
  `manifest.json` zuletzt, `Directory.Move`; jeder Fehler räumt `.tmp`, alter Stand bleibt); `CleanUp`.
- **App `ShipData/`:** `ShipDataGate` (rein: `ShouldCheck`, `Offer` → Angebot | braucht App-Update | nichts);
  `ShipDataSource` (Netzzugriff 3: eigener `HttpClient`, UA `SC-CargoPlan/<v>`, Manifest 10 s, ZIP 30 s, Puffer 4/16 MiB;
  `CheckAsync` nach dem Velopack-Check im selben `ContentRendered`-Handler, `CheckNowAsync`, `DownloadAsync` → `Install`
  auf dem Pool; Gründe `no network`, `download damaged`, `see cargoplan.log`). `UpdateViewModel` mit `UpdateLineTexts`;
  Zeile `Ship data update available · 2026.11.1 [Update] ☐ Skip this version` unter der App-Update-Zeile. Apply =
  Abschaltfolge von T11.2 + Neustart von `Environment.ProcessPath` mit `RestartArguments` in `OnExit`. Settings-Abschnitt
  „Ship data“ mit `Active: ships-2026.11.1 (downloaded)`.
- **CLI:** `ships --version` → `ships-2026.11.1 (downloaded) <pfad>`.
- **Doku:** D112 (Netzzugriff 3), Addenda D111, D59, D105; CLAUDE.md (Netz, Struktur, Befehle); `settings-format.md`,
  `ship-format.md`, `update-testing.md`.

**Tests zuerst:** Core `ShipDataVersionTests`, `ShipDataManifestReaderTests`, `ShipDataStoreTests` (Auswahl, Install,
verfälschte Datei, fehlende/überzählige/Zip-Slip-Einträge, Abbruch mitten im Entpacken, Module, CleanUp), Nachfolger
`unknown_rack`; App `ShipDataGateTests`, `ShipDataSourceTests` (`FakeHandler`), `UpdateViewModelTests`, Settings und
Session für `shipData`, `StartOptionsTests`, XAML-Tests, A13-Test für `NoticeSource.ShipData`. Fixtures aus `FrozenShips` (A10).

**Nicht in T12.3:** Holo aus dem Manifest (T12.4), Baseline aus dem Release (T12.5), Live-Reload (Backlog), Rack-Reihenfolge
bei gleichem Rack-Satz (Pipeline-`diff` über `orderKeys`), `HeaderPanel`-`MinWidth` für neue lange Namen,
`FrameMeasureWindow` lädt weiter Baseline-Umrisse.

**Umsetzung (18.11.2026):** Core `9e3c620`, App `ab06ae8`, CLI `2f41d0a`, Doku (D112, Addenda D59/D105/D111, CLAUDE.md,
`settings-format.md`, `ship-format.md`, `update-testing.md`) im Commit danach. Tests jeweils erst rot gesehen: Core 20
Compile-Fehler (CS0234/CS0246), dann 2 Assert-Fehler im R3-Nachfolger; App CS0234/CS0246. Texte (Taste-Gate Martin):
Zeile `Ship data update available · 2026.11.1`, `Downloading ship data`, `Ship data updated · restarting`,
`Ship data update failed · <Grund>` (Gründe `no network`, `download damaged`, `folder not writable`, `see cargoplan.log`);
Notice `Ship data 2026.12 needs an app update` (statt „Ship data needs app update“ aus dem Prompt: nennt den Stand) und
`Downloaded ship data 2026.11.1 not loaded – using the built-in 2026.11.`; Settings-Abschnitt „Ship data“ mit
`In use: 2026.11.1 (downloaded)` bzw. `(built in)`, `Skipped: 2026.11.1`, Check now.

**Befunde in der Umsetzung:**
- `ShipDataStore.Install` kann einen liegengebliebenen `.tmp`-Ordner nicht immer löschen (Virenscanner, Sperre); dann nennt
  eine `IOException` ihn mit der ersten Ursache als innerer Ausnahme, und `CleanUp` beim nächsten Start nimmt ihn. Kein
  stilles `catch` (NIEMALS-Liste).
- Das Template der Update-Zeile ist jetzt eine Ressource (`UpdateLineTemplate`), beide Zeilen nutzen es; die bestehenden
  XAML-Tests fanden die benannten Elemente weiter.
- Die drei erwarteten Migrations-Fixtures (`Fixtures/Settings/Expected`) tragen `shipData`: ein Speichern schreibt jeden
  Schlüssel (D31).
- Gate-Helfer: `Process.MainWindowHandle` zeigte einmal auf das Lock-Fenster (Titel leer); das Overlay wird per UIA nach
  Prozess und Titel `SC-CargoPlan` gesucht.

**CC-Vorlauf ohne SC (18.11., Debug-Build, `--data-dir` im Scratchpad):**
1. Feed-Ordner mit `manifest.json` + `ships.zip` von `ships-2026.11.1`, Start mit `--plan split_and_shared.json`: Zeile
   `Ship data update available · 2026.11.1` nach dem ersten Frame (Log: 87 ms nach dem Frame). **Update** → Log
   `installed ships-2026.11.1 (downloaded) …`, `restarting with [--data-dir … --ship-data-source …]` (ohne `--plan`),
   neue PID mit genau diesen Argumenten, `in use: ships-2026.11.1 (downloaded) …, 110 ships` (vorher 109: `hyun`),
   `nothing newer than ships-2026.11.1`, keine Zeile. `plan.json` vor und nach dem Neustart byte-gleich (SHA-256).
   `ships --version --data-dir …` → `ships-2026.11.1 (downloaded) …\ships\ships-2026.11.1`.
2. Skip: Zeile weg, `settings.json` `shipData.skippedVersion = "ships-2026.11.1"`; zweiter Start: keine Zeile, Log `skipped`.
3. Offline (Quellordner fehlt): keine Zeile, keine Notice, eine Logzeile `check failed: …`.
4. Echtes GitHub (`--ship-data-source https://github.com/MRiediger/sc-cargoplan-shipdata`): Angebot 0,6 s nach dem Frame,
   Update → Neustart → `nothing newer`.
5. P10: `hull_a.json` im installierten Stand gelöscht → Notice `Downloaded ship data 2026.11.1 not loaded – using the
   built-in 2026.11.`, Baseline läuft.
6. Publish-Build `0.5.99` (Layout-Prüfung bestanden, ZIP 96 730 370 B) aus `out\publish`, ohne Schalter: Log
   `Update: no check: not a Velopack package`, dann `Ship data: ships-2026.11.1 available`, Zeile da.

**Gate-Anleitung (Martin, SC im Vordergrund, am Aufzug).** Publish-Build ohne Velopack-Installation (startet aus
`out\publish`, prüft also kein App-Update, nur Schiffsdaten gegen das echte GitHub):

```powershell
./build/publish.ps1 -Version 0.5.99
$exe  = "$PWD\out\publish\SC-CargoPlan\CargoPlan.App.exe"
$data = "$env:UserProfile\CargoPlan-t123"     # eigener Datenordner, dein Profil bleibt unberührt
New-Item -ItemType Directory -Force $data | Out-Null
Copy-Item "$env:LocalAppData\SC-CargoPlan\plan.json" $data   # optional: dein Hull-A-Plan
& $exe --data-dir $data
```

1. Overlay zeigt unten `Ship data update available · 2026.11.1` mit **Update** und **Skip this version** (gesperrt: nur
   Text). Einen Hull-A-Plan anlegen oder den kopierten nehmen, einen Stopp abhaken.
2. **Update** → `Ship data updated · restarting` → Overlay schließt und kommt mit denselben Einstellungen zurück. In der
   Schiffsauswahl steht **Hyun**; **F7C Hornet Mk II** lädt (Holo aus holo-v3 oder Umriss – T12.4, kein Fehler). Der
   Hull-A-Plan ist unverändert, der Haken auch.
3. `& "$PWD\out\publish\SC-CargoPlan\CargoPlan.Cli.exe" ships --version --data-dir $data` →
   `ships-2026.11.1 (downloaded) …`. Zahnrad → **Ship data**: `In use: 2026.11.1 (downloaded)`.
4. Overlay schließen, neu starten → keine Zeile.
5. Skip: `Remove-Item -Recurse $data\ships`, Start → Zeile → **Skip this version** → Zeile weg; Neustart → keine Zeile;
   Zahnrad → Ship data: `Skipped: 2026.11.1 [Clear]`.
6. Offline (WLAN/Netz aus) → Start: keine Zeile, keine Notice, kein Fehler, das Overlay läuft.

**Offen fürs Gate (Optik = Martin):** die Texte oben, Lage des Abschnitts „Ship data“ (unter „Updates“), zwei Zeilen
übereinander, wenn App- und Datenupdate zugleich kommen.

**Abnahme (Martin, 18.11.2026, SC im Vordergrund):**
- [x] Gate 1–2: Angebot, Update, Neustart, Hyun, F7C, Plan bleibt
- [x] Gate 3: `ships --version`, Abschnitt „In use“
- [x] Gate 4: zweiter Start ohne Zeile
- [x] Gate 5: Skip, Neustart, Settings zeigen den Stand
- [x] Gate 6: offline
- [x] Optik und Texte: passen; Texte, Lage des Abschnitts und zwei Zeilen übereinander bleiben wie gebaut
- [x] Build ohne Warnungen, alle Tests grün (Core 1880, Import 280, Ocr 34, App 2075; ohne die drei lokalen Scratch-Tests
  Core 1878, App 2074), `dotnet format` sauber

### [x] T12.4 App: Holo aus dem Manifest (18.11.2026, `06170bc`, `a8c4900`, abgenommen 18.11.)

**Warum:** `HoloRelease.Tag` ist die letzte Konstante, die ein App-Release pro Datenänderung erzwingt.
- Hashes aus dem Manifest, URL `…/releases/download/<dataVersion>/<file>` (kein `baseUrl`, SD2), `holo: null` → Umriss-Fallback, gelöschtes Release → Baseline-Holo
- Cache-Wechsel nach D95-Muster, Migration für v0.5.x-Caches

**Akzeptanz:** Holo-Dateien aus `ships-*` byte-gleich zu holo-v3 (S12.0: Schwelle 0); Holo-Download aus `ships-*` statt `holo-v3`.

**Plan (18.11.2026, Branch `feat/p12-holo-manifest` ab `b02b90d`, Freigabe Martin 18.11. mit R1 Alternative, R2, R3)**

Festlegungen 1–7 aus dem Startprompt (Martin, 18.11.) gelten, Festlegung 1 mit R1 geändert. Die Akzeptanz oben ist
präzisiert: statt „Holo-Dateien aus `ships-*` byte-gleich zu holo-v3“ (stimmt nur für 11 von 101, s. Befunde) gilt die
Akzeptanz des Startprompts (Hyun mit Holo, F7C mit Motherlode-Holo, Hull A wie immer, Cache unter
`holo\ships-2026.11.1\` mit SHA = Manifest, `holo-v3`-Ordner einmal weg, Kill-Switch, 404). „Gelöschtes Release →
Baseline-Holo“ aus dem Task-Text wird nach Festlegung 5 „Umriss für dieses Schiff, gecachte Datei bleibt“.

**Befunde vorab (gemessen 18.11.):**
- `ships-2026.11.1` vs. holo-v3: **11 Slugs byte-gleich** (u. a. `misc-hull-a`), **90 anders**, 1 neu (`gama-hyun`,
  268 668 B), keiner weg. Die Übernahme aus `holo-v3` (Festlegung 3: nicht) spart also höchstens 11 Downloads.
- F7C: holo-v3 403 390 B (Fleetyards) → `ships-2026.11.1` **88 046 B** (`ANVL_Hornet_F7C_Mk2`). Größte Datei 661 206 B
  (`orig-135c`) < `MaxResponseBytes` 4 MiB; alle `file` = `<slug>.holo`.
- **Zeit vorher/nachher** (curl, 3 Läufe, erster kalt): vorher = Manifest holo-v3 (0,54 / 0,14 / 0,15 s) + F7C-Datei
  (0,49 / 0,22 / 0,25 s) = **1,03 / 0,36 / 0,40 s** für das erste Schiff eines Laufs; nachher nur die Datei
  **0,44 / 0,13 / 0,15 s** (F7C, kleinere Datei), Hyun 0,45 / 0,18 / 0,19 s. Gewinn: ein Round-Trip (0,14–0,54 s) beim
  ersten Schiff des Laufs.

**Grep-first (A1):**

| Treffer | Ist | T12.4 |
|---|---|---|
| `HoloRelease` (App) | `Tag`, `ManifestFile`, `BaseUri`, `Timeout`, `ManifestMaxAge`, `DefaultCacheRoot`, `DefaultCacheFolder`, `UserAgentVersion`; Nutzer `HoloSource`, `FrameMeasureWindow`, Tests `AppFoldersTests`, `FakeHttp`, `HoloLoaderTests`, `HoloSourceTests` | **Datei entfällt.** `Timeout`, `DefaultCacheRoot`, `CacheFolder(root, version)` nach `HoloSource`; `UserAgentVersion` → `AppVersion.Current` wie `ShipDataSource` |
| `HoloManifestReader` + `HoloJsonContext` (Core) | liest das holo-v3-Manifest (Liste `ships[]` mit `slug`); einziger Nutzer `HoloSource` | **entfällt**; den `holo`-Abschnitt (Map `slug → {file, bytes, sha256}`) liest `ShipDataManifestReader` und bildet ihn auf die **bestehenden Records** `HoloManifest`/`HoloManifestEntry` ab: ein Parser, eine Prüfung |
| `HoloSource` | Manifest-GET, 24-h-Alter, Rückzug löscht gecachte Datei, `TagChanged`/`RetireOtherTags` (`holo-v<n>`), eigener `HttpClient` mit `BaseAddress` | Manifest aus `ActiveShipData`, kein Manifest-GET, kein `TimeProvider`; `TagChanged`/D95 bleiben, Ordnererkennung erweitert; eigener Client bleibt |
| `HoloLoader` | einziger Schreiber von `NoticeSource.Holo` und `HoloUpdate` (A13; sonst nur ein `PlanSaver`-Kommentar und `OverlayViewModelTests`) | nur `RetireUnneeded` erweitert (kein Holo im Stand); Notice-Logik unverändert |
| `ShipDataSource` | eigener `HttpClient` (16 MiB, 10/30 s), `Repository`, privat `RepositoryUri(source)`, URL `releases/download/{v}/{asset}` inline | neu `RepositoryFor(string? source)` und `ReleaseFolder(repository, version)`; `DownloadAsync` und `HoloSource` nutzen sie |
| `ActiveShipData` (Core) | gebaut in `ReadBaseline`, `ShipDataStore.Downloaded`, fünf Testkonstruktionen | + `Holo` |
| `ShipPictureTests` | Holos aus `CARGOPLAN_HOLO_FOLDER`, kein `HoloRelease` | unverändert |

**Ein `HttpClient` oder zwei:** `HoloSource` behält den eigenen (A4). Teilen hieße: `HoloSource` nimmt einen fremden
Client statt eines Handlers (alle `FakeHandler`-Tests umbauen), und `MaxResponseContentBufferSize` (4 vs. 16 MiB) gilt
pro Client. Gegenargument: zwei Verbindungspools zu github.com – bei einer Handvoll Requests pro Lauf ohne messbare
Kosten. Geteilt wird nur die **URL-Basis**.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. **`ActiveShipData` trägt das Holo-Angebot** (statt dass `HoloSource` `<Folder>\manifest.json` ein zweites Mal liest):
   `ReadBaseline`, `Active` und `Install` haben das geprüfte Manifest in der Hand. Gegenargument: der Record wächst.
2. **`holo` ist Pflichtfeld**, `null` erlaubt: fehlt der Schlüssel → `ShipDataFormatException` (die Pipeline schreibt ihn
   immer). Prüfung je Eintrag (A6): Slug und `file` flach (`AssetName`), `bytes > 0`, `sha256` 64 Hex.
   `HoloManifest.DataVersion` = `dataVersion` des Manifests, `Ships` nach Slug sortiert.
3. **R1 (Alternative statt Festlegung 1):** `holo.formatVersion ≠ 2` macht das Manifest **nicht** ungültig, sondern heißt
   „kein Holo für diese App“ mit eigener Ursache: `HoloOffer` = `Offered(HoloManifest)` | `NotOffered` (`holo: null`,
   Kill-Switch) | `UnknownFormat(n)`; die Einträge eines unbekannten Formats werden nicht gelesen. Schiffe kommen, Umriss,
   eine Logzeile, Settings-Text. Gegenargument: eine ältere App holt einen Stand, dessen Holos sie nie zeigt.
4. **`GetAsync`-Ablauf:** kein Angebot → `Missing` (kein Request, kein Cache-Lesen) · Slug fehlt → `Missing` · gecachte
   Datei mit Länge+SHA des Manifests → `Found`, **auch bei Download aus** · Download aus → `Missing` · beim ersten Aufruf
   mit Download an: D95-Rückzug + `TagChanged` · Slug schon 404 in diesem Lauf → `Missing` ohne Request · **404** →
   `Missing`, Slug gemerkt, **eine** Logzeile je Lauf, gecachte Datei bleibt · andere Status, Timeout, kein Netz →
   `Failed` wie heute (eine Notice je Lauf) · falscher Hash → `Failed`, Datei weg.
5. **Rückzug innerhalb eines Stands entfällt:** Manifest pro Lauf konstant, Ordner pro Stand neu; eine gecachte Datei ohne
   Manifest-Eintrag gibt es nicht mehr.
6. **Ordner „eines anderen Stands“ (D95):** direkt unter `holo\`, Name `holo-v<Ziffern>` **oder** ein `ShipDataVersion`
   ≠ aktiver Stand; sonst nichts (`notes\`, `ships-x\` bleiben). Ein Downgrade auf v0.5.x lädt neu (wie bei D95 heute).
7. **Kein Angebot (Kill-Switch, unbekanntes Format):** `HoloLoader.RetireUnneeded` räumt sofort und still (Notice nur,
   wenn das Löschen scheitert); `App` schreibt **eine** Logzeile beim Start; kein `TagChanged`, keine Notice.
8. **R3: Settings-Text unter dem Schalter** „Download ship holograms“: `Holograms: not offered by ship data 2026.11.2`
   bzw. `Holograms: ship data 2026.12 needs a newer app (hologram format 3)`, sonst unsichtbar.
9. **R2 `--ship-data-source`:** Repo-URL → Holos aus diesem Repo; Ordner → Holos aus dem Standard-Repo, kein
   Datei-Lesen in `HoloSource` (A4). Ein Fixture-Stand mit erfundenem Tag bekommt 404 → Umriss.
10. **`FrameMeasureWindow`** liest aus `holo\<Baseline-Tag>\` (es lädt die Baseline-Schiffe).
11. **Nutzer-Doku mit holo-v3** (README, `settings-format.md`, `ship-format.md`) zieht T12.4 nach; Nexus-Texte und Wiki
    beschreiben das veröffentlichte v0.5.1 und folgen mit dem Release (T12.6).

**Aufbau:** Core `HoloOffer` (Holo/), `ShipDataManifest.Holo`, `ShipDataManifestReader` (`holo` als `JsonElement`, Format
zuerst), `ActiveShipData.Holo`; App `HoloSource(handler, cacheRoot, repository, data, timeout, version)`,
`ShipDataSource.RepositoryFor`/`ReleaseFolder`, `HoloLoader.RetireUnneeded`, `App.ComposeHolograms` (Logzeile),
`SettingsViewModel.HologramsUnavailable` + TextBlock, `FrameMeasureWindow`; `HoloRelease.cs` und `HoloManifestReader` weg.
**Doku:** D114 „Holograms follow the ship data manifest“, Addenda D63, D95, D112; CLAUDE.md (Netz, Struktur);
`holo-format.md`, `settings-format.md`, `ship-format.md`, README.

**Tests zuerst:** Core `ShipDataManifestReaderTests` (Nachfolger von `HoloManifestReaderTests`: `holo` gelesen, `null`,
fehlt, Format 3 = `UnknownFormat` ohne Eintragsprüfung, `file` mit `/`/`..`, Hash, `bytes 0`, leere Map),
`ShipDataStoreTests`/`BaselineShipDataTests` (`Holo` in `ReadBaseline`, `Active`, `Install`); App `HoloSourceTests` neu
gefasst (Stand-URL, nur `.holo`-Requests, Hash, Kill-Switch, unbekanntes Format, 404 einmal und ein anderes Schiff lädt,
503, Download aus mit geprüftem Cache, D95 mit `holo-v3\` und `ships-2026.11\`), `HoloLoaderTests` (kein Angebot: drei
Schiffswechsel ohne Request und Notice; 404 ohne Notice; A13: Tag-Wechsel-Notice + 404; offline nach Tag-Wechsel),
`AppFoldersTests`, `ShipDataSourceTests`, Settings-Text. Entfallen: alle Tests zu Manifest-GET und 24 h; Nachfolger in
der Umsetzung benannt. `ShipPictureTests` unverändert.

**CC-Vorlauf ohne SC (Publish `0.5.99`, `--data-dir` im Scratchpad):** leerer Datenordner, drei Starts (Hyun, F7C,
Hull A): kein Manifest-GET, Dateien unter `holo\ships-2026.11.1\`, SHA = Manifest; kopierter `holo\holo-v3\` → weg, eine
Notice, zweiter Start ruhig; Fixture-Feed `ships-2026.11.2` mit `holo: null` → eine Logzeile, kein Request,
Settings-Text; `grep -rE "holo-v3|HoloRelease" src` leer.

**Gate Martin (SC vorn):** Hyun → Holo; F7C → Holo (schlanker); Hull A → Plan und Holo wie vorher.

**Nicht in T12.4:** Übernahme byte-gleicher Holos aus `holo-v3`; Löschen des `holo-v3`-Release (T12.6); Nexus- und
Wiki-Texte (T12.6); Live-Reload.

**Umsetzung (18.11.2026):** Plan `68bec39`, Code `06170bc` (Core und App in einem Commit: die App baut ohne den neuen
Leser nicht), Doku im Commit danach (D114, Addenda D63/D95/D112, CLAUDE.md, `holo-format.md`, `settings-format.md`,
`ship-format.md`, README). Tests erst rot gesehen: Core CS0246 (`HoloOffer`), App CS0103 (`HoloManifestReader`).
Nachfolger: `HoloManifestReaderTests` → Abschnitt „Holograms“ in `ShipDataManifestReaderTests`; in `HoloSourceTests`
benennt jeder umgebaute Test seinen Vorgänger im Kommentar (`Was …`); entfallen ersatzlos die zwölf Tests zu
Manifest-GET, 24 h und Rückzug aus dem Netz-Manifest.

**Befunde in der Umsetzung:**
- `HoloManifest` vergleicht nach Inhalt (eigenes `Equals`): `ActiveShipData` ist ein Record, und `Install` und `Active`
  lesen dasselbe Manifest zweimal – mit Listen-Referenzgleichheit wäre `active.Should().Be(installed)` gekippt.
- `holo` wird als `JsonElement?` gelesen und das Format zuerst geprüft: so bindet ein künftiges Format mit anderer
  Eintragsform nie an die DTOs (R1).
- `FrameMeasureWindow` misst den **aktiven** Katalog (App reicht `ships` durch), also liest es jetzt auch den Cache des
  aktiven Stands, nicht der Baseline (Präzisierung 10 war ungenau).
- Die Notice nach dem Cache-Wechsel steht nur, solange das erste Holo lädt (K1) – bei schnellem Netz unter einer
  Sekunde; im Vorlauf per UIA-Abfrage alle 100 ms gefunden.
- `sed -i` in Git Bash macht aus CRLF-Dateien LF (K1-Falle, nochmals): eine Testdatei zurück auf CRLF normalisiert.

**CC-Vorlauf ohne SC (18.11., Publish `0.5.99`, Layout-Prüfung bestanden, ZIP 96 761 281 B, `--data-dir` im Scratchpad):**
1. Leerer Datenordner, drei Starts (`settings.json`: Schiff + `holo-hiddenline`): `gama-hyun.holo` 268 668 B nach
   2,6 s, `anvl-f7c-hornet-mk-ii.holo` **88 046 B** nach 1,9 s, `misc-hull-a.holo` 185 882 B nach 4,2 s (ab Prozessstart),
   **SHA und Länge = Manifest** bei allen dreien; einziger Cache-Ordner `holo\ships-2026.11.1\`, keine `manifest.json`
   darin.
2. Kopie von `holo\holo-v3\` (6 Dateien, aus dem Profil nur gelesen), Hull A nicht gecacht → Notice `Ship holograms
   updated to match the new rack data – each ship re-downloads when first shown` 0,7 s nach dem Start, danach nur
   `holo\ships-2026.11.1\`; zweiter Start: keine Holo-Notice.
3. Fixture-Feed `ships-2026.11.2` (Baseline-Manifest mit `holo: null`, `ships.zip` neu gepackt) per
   `--ship-data-source`: `Ship data update available · 2026.11.2` → Update → Neustart → Log `Holo: ship data 2026.11.2
   offers no holograms (holo: null) – outlines only` (eine Zeile), 8 s ohne Holo-Notice, `holo\` leer (der Ordner des
   alten Stands still geräumt), Settings unter dem Schalter `Holograms: not offered by ship data 2026.11.2`, Ship data
   `In use: 2026.11.2 (downloaded)`. Drei Schiffswechsel ohne Request und Notice deckt der Test
   `Without_holograms_on_offer_ship_changes_…` ab, nicht der Vorlauf.
4. 404 nur als `FakeHandler`-Test (`A_404_…`, `After_a_cache_change_a_404_…`): das echte Release ist vollständig.
5. `grep -rE "holo-v3|HoloRelease" src` → leer. Zeit in der App: kein Vergleich gegen `main` gemessen (die Zeiten oben
   enthalten den Start); der Gewinn ist der weggefallene Manifest-GET aus den curl-Zahlen.

**Gate-Anleitung (Martin, SC im Vordergrund).** Publish-Build aus `out\publish` (kein Velopack-Check), eigener Datenordner:

```powershell
./build/publish.ps1 -Version 0.5.99
$exe  = "$PWD\out\publish\SC-CargoPlan\CargoPlan.App.exe"
$data = "$env:UserProfile\CargoPlan-t124"     # eigener Datenordner, dein Profil bleibt unberührt
New-Item -ItemType Directory -Force "$data\holo" | Out-Null
Copy-Item "$env:LocalAppData\SC-CargoPlan\plan.json", "$env:LocalAppData\SC-CargoPlan\settings.json" $data
Copy-Item -Recurse "$env:LocalAppData\SC-CargoPlan\holo\holo-v3" "$data\holo\holo-v3"   # optional: der alte Cache
& $exe --data-dir $data
```

1. Look **Hologram** (Zahnrad → Ship view). Mit dem kopierten Cache: einmal `Ship holograms updated …`, danach ist
   `$data\holo\` nur noch `ships-2026.11.1`.
2. **GAMA Hyun** wählen → Holo erscheint (gab es in holo-v3 nicht).
3. **F7C Hornet Mk II** → Holo erscheint, schlanker als bisher (Motherlode statt Fleetyards).
4. **Hull A** → Plan und Holo wie vorher.

**Offen fürs Gate (Optik = Martin):** der Settings-Text `Holograms: not offered by ship data …` (Wortlaut, Lage unter
dem Hinweis); das Aussehen der 90 geänderten Holos – nur Hyun, F7C und Hull A sind im Gate.

**Abnahme (Martin, 18.11.2026, SC im Vordergrund):**
- [x] Grep `holo-v3|HoloRelease` über `src` leer
- [x] Publish-Build, leerer Datenordner: Hyun, F7C, Hull A mit Holo, Cache `holo\ships-2026.11.1\`, SHA = Manifest (CC-Vorlauf 1)
- [x] Alter Cache `holo\holo-v3\`: einmal gelöscht, eine Notice, danach Ruhe (CC-Vorlauf 2)
- [x] Kill-Switch per `--ship-data-source`: Umriss, eine Logzeile, Settings-Text, keine Notice (CC-Vorlauf 3, Test)
- [x] 404 per `FakeHandler`: Umriss nur für dieses Schiff, andere laden (Test)
- [x] Gate: Hyun → Holo, F7C → Holo, Hull A → Plan und Holo wie vorher
- [x] Build ohne Warnungen, alle Tests grün (Core 1915, Import 280, Ocr 34, App 2080; inkl. der drei lokalen
  Scratch-Tests), `dotnet format` sauber

### [x] T12.5 App: Baseline aus dem Release (18.11.2026, `46fd06d`, `39c3a50`, abgenommen 18.11.)

**Warum:** Eine Quelle für Schiffsdaten; `ships/` als Quelltext im App-Repo ist der zweite Pfad, der v0.5.1 teuer machte.
- `release.yml` lädt das aktuelle `ships-*`-Release ins ZIP, Tag im Manifest; `ci.yml` gegen eine feste Baseline
- `ships/`, `outlines/`, `tools/import-motherlode.ps1`, `ShipJson.ps1` raus; `ShipPictureTests` liest Daten per Env-Override (S12.0 E3)
- NOTICE und README anpassen

**Akzeptanz:** Publish-Build lokal startet offline mit der Baseline; `git ls-files ships/` ist leer.

**Plan (18.11.2026, Branch `feat/p12-baseline` ab `8e3b9ec`, Freigabe Martin 18.11. mit Antworten R1–R5 und E1)**

T12.5 vor T12.4 (Martin 18.11.): erst hat jeder Stand ein Manifest, dann liest das Holo daraus. Festlegungen aus dem
Startprompt: Pin in `build/ship-data.json`; Baseline neben der Exe im Download-Layout `ship-data\` (`manifest.json`,
`ships\**`, `outlines.json`, `NOTICE.md`); `tools/fetch-ship-data.ps1` holt einmal, geprüft, nach `.ship-data/<tag>/`
(gitignored), schreibt `.ship-data/current`; der Build ruft es; `ships/`, `outlines/`, `tools/import-motherlode.ps1`,
`ShipJson.ps1`, `ImportMotherlodeScriptTests`, `ShipAnchorValueTests` raus; `ShipPictureTests` mit `CARGOPLAN_SHIP_DATA`.
Die Akzeptanz oben ist präzisiert: frischer Klon baut mit einem `dotnet build` (einmal Netz), CI grün auf beiden
Runnern, Publish-Build `0.5.99` offline mit 110 Schiffen inkl. `hyun`, `ships --version` → `ships-2026.11.1 (baseline)`,
keine Update-Zeile, älterer Download wird aufgeräumt; Gate Martin: nichts sieht anders aus.

**Grep-first (A1):** `ActiveShipData.NextToExe` (Konstante + `ShipLoader.DefaultFolder` + `ShipOutlineLoader.DefaultPath`)
nutzen App (`LoadShipsOrExit`) und CLI (`LoadShips`); `ShipDataVersion.Baseline` nur noch Tests. Wiederverwendet:
`ShipDataManifestReader.Read`, `ShipLoader.IsReadable`, `ShipDataRelease` (T12.3-Fixtures), Prozessstart und
Shell-Auswahl aus `ImportMotherlodeScriptTests`. Live-Manifest `ships-2026.11.1`: `files` = `outlines.json` +
`ships/**` (111), **`NOTICE.md` ohne Hash**. `publish.ps1` kopiert keine Schiffe selbst (kommen über Core-Items).
`ShipAnchorTests` ist Mechanik (FrozenShips), kein Datentest. Lokal kein `pwsh`, nur PowerShell 5.1.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. **Beide Dialekte** (R1): das Skript läuft unter PowerShell 5.1 und 7; MSBuild ruft unter Windows `powershell.exe`,
   sonst `pwsh`. Ein frisches Windows braucht keine Installation. Nur die drei bekannten 5.1-Fallen (TLS 1.2,
   `ZipFile` statt `Expand-Archive`, UTF-8 ohne BOM). CI deckt 5.1 (Windows) und 7 (Ubuntu), der Skript-Test läuft
   lokal unter 5.1. Gegenargument: zwei Dialekte im Kopf.
2. **Baseline gewinnt bei Gleichstand** (R2): `Active` nimmt nur Downloads **neuer** als die Baseline, `CleanUp` räumt
   Downloads `≤ Baseline`. Ein Download gleich der Baseline hat keinen Wert, und `ships --version` nennt die wahre
   Quelle. Gegenargument: Regeländerung im eben abgenommenen T12.3-Code (eigener Test).
3. **`ShipAnchorValues` + 5 Tests raus** (R3): die Rechenregel lebt mit Tests in der Pipeline (T12.1); D113 sagt wo.
   Die 5 F7-Mechanik-Tests in `ShipAnchorTests` bleiben.
4. **`NOTICE.md`** (R4): ohne Hash im Manifest → „vorhanden, nicht leer, < 64 KiB“. `NOTICE.md` in `files` ist
   Shipdata-Backlog (T12.6-Liste).
5. **Startfehler ohne Baseline** (R5): `The ship data next to SC-CargoPlan are missing or damaged (ship-data\manifest.json:
   <reason>). Download SC-CargoPlan again.` → `ExitShipsFolderBroken` (D56). CLI: stderr + Exit-Code.
6. **Download sichtbar im Build-Log** (E1): `<Message Importance="high">` „Fetching ship data ships-2026.11.1 (once,
   ≈150 KB)“ vor dem Exec, Exec-Ausgabe mit `StandardOutputImportance="high"`.
7. **Pin in MSBuild per Property-Funktion** (`File.ReadAllText` + `Regex.Match` mit Lookbehind); kein Treffer → `<Error>`.
8. **Items im Target, nicht statisch:** ein `None Include` mit Wildcard wird beim Evaluieren aufgelöst, also vor dem
   Fetch; ein frischer Klon bekäme im ersten Build nichts. Target `FetchShipData` (vor `BeforeBuild`, nur wenn
   `<tag>\manifest.json` fehlt, nicht im Design-Time-Build), Target `IncludeShipData` (vor `AssignTargetPaths`) legt die
   `None`-Items mit `Link="ship-data\…"` an. Gegenargument MSBuild-only: `DownloadFile`/`Unzip`/`VerifyFileHash` gibt
   es, aber keinen JSON-Leser → keine Prüfung gegen `files`, kein Filter gegen Zip-Slip.
9. **Lesbare Schemata** als MSBuild-Property → `-ReadableSchemaVersions`; ein Core-Test vergleicht sie mit `ShipLoader`.
10. **Core:** `ActiveShipData.NextToExe` → `ActiveShipData.BaselineFolder` + `ReadBaseline(folder)` (Manifest per
    `ShipDataManifestReader`, Schema per `IsReadable`, wirft `ShipDataFormatException` mit Dateiname);
    `ShipDataVersion.Baseline` entfällt; `ShipLoader.DefaultFolder` → `ship-data\ships`, `ShipOutlineLoader.DefaultPath`
    → `ship-data\outlines.json`.
11. **Skript:** Pin lesen → fertiger Ordner da: offline neu hashen, passt → `current`, Ende („already there“), sonst neu
    holen; holen in `<tag>.tmp-<zufall>\` (`-Source` = URL-Basis oder lokaler Ordner für den Test), Manifest
    (`dataVersion` = Pin, Schema lesbar, Pfade wie `ShipDataManifestReader`), nur gelistete Einträge entpacken, Menge und
    Hash gleich, dann `Move`; Log nennt Pin, Bytes, geprüfte Dateien; Fehler: eine Zeile, Exit 1, nichts Halbes.
12. **`ShipPictureTests`:** `CARGOPLAN_SHIP_DATA` per `Path.Combine`, ohne `ReadBaseline`, damit die Datei in Worktrees
    älterer Commits läuft.
13. Alte `bin\…\ships\` und `bin\…\outlines\` bleiben lokal bis `dotnet clean`; niemand liest sie mehr.

**Tests zuerst:** Core `FetchShipDataScriptTests` (guter Lauf, zweiter Lauf ohne Quelle, verfälschte/zusätzliche/
fehlende/`../`-Einträge, `dataVersion ≠ Pin`, Schema unlesbar, kaputter Pin, verdorbener fertiger Ordner); `ReadBaseline`
(Version, Pfade, fehlendes Manifest, Schema, Tag); Store mit Manifest-Baseline (älterer Download ignoriert und geräumt,
Gleichstand → Baseline); Pin-Test (Pin = Manifest neben der Assembly, Schemata = `ShipLoader`). Umgestellt:
`ShipDataStoreTests`, `ShipDataSourceTests`, `UpdateSettingsViewModelTests` (explizite Version), `OldPlan350rTests`,
`HeaderPanelTests` (`ShipLoader.DefaultFolder`). Datentests laufen gegen `ships-2026.11.1`; kippt einer, ist das ein
Befund zur Rückfrage.

**Build, Publish, CI, Doku:** `check-portable.ps1` verlangt `ship-data\manifest.json`, `ship-data\ships\hull_a.json`,
`ship-data\ships\modules\ts_module.json`, `ship-data\outlines.json`, `ship-data\NOTICE.md`; `ci.yml`/`release.yml` ohne
eigenen Schritt, solange der frische Klon es beweist. D113 „Baseline comes from the data release“, Addenda D56, D111;
CLAUDE.md (Struktur, Befehle, Distribution), CONTRIBUTING, README, `ship-format.md`, `update-testing.md`,
Issue-Vorlage `ship-correction.yml`, Kommentar in `CargoPlan.Ocr.csproj`.

**Nicht in T12.5:** Holo aus dem Manifest (T12.4), Live-Reload, Pin-Bump per PR aus der Pipeline (Backlog).

**Umsetzung (18.11.2026):** Code `46fd06d`, Doku `39c3a50` (D113, Addenda D56/D111, CLAUDE.md, CONTRIBUTING, README,
`ship-format.md`, `holo-format.md`, `settings-format.md`, Nexus-Checkliste, Issue-Vorlage). Tests erst rot gesehen:
Core 4× CS0117 (`BaselineFolder`, `BaselineFolderName`, `ReadBaseline`, `ReadableSchemaVersions`). Draft-PR #28 nur für
CI: [Lauf 44364056343](https://github.com/MRiediger/sc-cargoplan/actions/runs/44364056343) grün auf `windows-latest`
und `ubuntu-latest`, Fetch auf beiden (231 552 B, 111 Dateien geprüft), Testzahlen wie lokal.

**Befunde in der Umsetzung:**
- **Größe:** der einmalige Download ist **≈ 230 KB**, nicht ≈ 150 KB (Manifest 127 KB + `ships.zip` 102 KB + Notice);
  Build-Log und CONTRIBUTING nennen 230.
- **Datentest kippte erwartungsgemäß:** `ships-2026.11.1` hat die Racks der **F7C Hornet Mk II** aus den Spieldateien
  (die F7C-Änderung aus T12.3). `Every_ship_but_three_…` → `Every_ship_but_two_…` (135c, 600i EE). Sonst keiner:
  `HeaderPanelTests` (D110, längster Name), `ShipOutlineLoaderTests`, `ReplanTests`, `OldPlan350rTests` grün mit 110 Schiffen.
- `check-portable.ps1` prüft zusätzlich `ships --version --data-dir <leer>` = `<Pin> (baseline)`: das ZIP trägt
  nachweislich den gepinnten Stand, unabhängig vom Profil.
- PS-5.1-Falle beim Doku-Helfer (Skript ohne BOM → „–“ kaputt, Abbruch vor dem Schreiben); `fetch-ship-data.ps1` ist
  reines ASCII, die csproj ohne BOM.
- **Für T12.6 (README beider Repos):** README und Issue-Vorlage `ship-correction.yml` nennen noch „105 ships, 102 from
  the game files, three Fleetyards (… F7C …)“; mit der Baseline `ships-2026.11.1` sind es 110 und zwei.
- `ShipAnchorTests` war schon Mechanik (FrozenShips); nur die Wertetests gingen (R3), die 5 F7-Tests bleiben.

**CC-Vorlauf ohne SC (18.11.):**
1. `git ls-files ships outlines tools/import-motherlode.ps1 tools/ShipJson.ps1` → leer.
2. Frischer Klon (Scratchpad) → `dotnet build`: `Fetching ship data ships-2026.11.1 (once, ≈230 KB)`, `Ship data
   ships-2026.11.1 fetched: 231552 bytes, 111 files checked against manifest.json, in …\.ship-data\ships-2026.11.1`;
   zweiter `dotnet build` ohne Fetch-Zeile; `dotnet test` grün (Core 1889, Import 280, Ocr 34, App 2069),
   `dotnet format --verify-no-changes` sauber. `ship-data\` (113 Dateien) neben App, Cli, Core-, App- und Import-Tests.
3. `./build/publish.ps1 -Version 0.5.99` → `Layout check: SC-CargoPlan-v0.5.99-win-x64.zip passed (388 files in current\,
   110 ship files of ships-2026.11.1, 5 PDBs, version 0.5.99).`
4. Start aus `out\publish` mit `--data-dir` (darin `ships\ships-2026.11\`, mit dem Skript geholt): Log `removed
   ships-2026.11`, `in use: ships-2026.11.1 (baseline) …\ship-data, 110 ships`, gegen das echte GitHub `nothing newer
   than ships-2026.11.1` → keine Zeile. CLI `ships --version --data-dir …` → `ships-2026.11.1 (baseline) …`.
   `ship-data\ships\hyun.json` = „GAMA Hyun“.
5. Unerreichbare Quelle (`--ship-data-source <fehlt>`): 110 Schiffe aus der Baseline, eine Logzeile `check failed`.
6. Manifest beiseite → CLI `ship-data\manifest.json: missing`, Exit 1; die App zeigt `The ship data next to SC-CargoPlan
   are missing or damaged (ship-data\manifest.json: missing). Download SC-CargoPlan again.` (Test `BaselineMissingTextTests`).

**Gate-Anleitung (Martin, SC im Vordergrund):**

```powershell
./build/publish.ps1 -Version 0.5.99
$exe  = "$PWD\out\publish\SC-CargoPlan\CargoPlan.App.exe"
$data = "$env:UserProfile\CargoPlan-t125"     # eigener Datenordner, dein Profil bleibt unberührt
New-Item -ItemType Directory -Force $data | Out-Null
Copy-Item "$env:LocalAppData\SC-CargoPlan\plan.json" $data   # dein Hull-A-Plan
& $exe --data-dir $data
```

1. Overlay startet ohne Ship-data-Zeile, Hull-A-Plan wie vorher.
2. Ein Schiff wechseln (z. B. **Hyun**, dann zurück zur Hull A): nichts sieht anders aus als in v0.5.1 / T12.3.
3. Optional: `& "$PWD\out\publish\SC-CargoPlan\CargoPlan.Cli.exe" ships --version --data-dir $data` →
   `ships-2026.11.1 (baseline) …`.

**Abnahme (Martin, 18.11.2026, SC im Vordergrund):**
- [x] Gate: Publish-Build, Hull-A-Plan, Schiffswechsel – unverändert
- [x] Build ohne Warnungen, alle Tests grün (Core 1889, Import 280, Ocr 34, App 2069 im sauberen Klon), `dotnet format`
  sauber, CI grün auf beiden Runnern

### [x] T12.6 Release (beide Repos; Teile mit **App:** hier) – T12.6a und T12.6b erledigt 19.11.2026

**Warum:** Erst ein echtes Data-Release, dann die App, die es liest, dann der erste Monatslauf.
- [x] Erstes Data-Release `ships-2026.11` per Knopf (= Stand 4.10.3 / holo-v3) – erledigt mit T12.2 im Shipdata-Repo
  (`ships-2026.11` Seed, `ships-2026.11.1` Latest, Dispatch 1/2)
- **App:** v0.6.0 mit dieser Baseline, Wiki (Installation, Ships), README beider Repos, Notes → **T12.6a** (unten)
- `sc-ship-outlines` archivieren → **T12.6b** (Martin-Klick); `holo-v3` im App-Repo bleibt bis v0.5.x ausgelaufen ist
  → T12.6a Punkt 6
- [x] Danach: erster echter Monatslauf per Knopf – erledigt mit T12.2 (Dispatch 1/2 → `ships-2026.11.1`)

**Akzeptanz:** Netz-Gate, SC im Vordergrund: v0.5.1 → v0.6.0 per Velopack, dann Data-Update im Overlay. Ab hier Branch + `--no-ff` für jede Änderung an diesem Repo.

**Aufteilung (Startprompt Master-Chat, 19.11.2026):** **T12.6a App-Release v0.6.0** (dieses Repo, zuerst) und
**T12.6b Shipdata-Abschluss** (Shipdata-Repo, Branch `docs/p12-close`, Plan `docs/plans/T12.6b-abschluss.md`, danach
oder parallel). Das Data-Update im Overlay aus der Akzeptanz oben entfällt für v0.6.0: Latest = Baseline, also **keine**
Ship-data-Zeile (geprüft im Netz-Gate); den Kanal hat das T12.3-Gate gegen das echte GitHub schon belegt.

#### [x] T12.6a App: Release v0.6.0 (19.11.2026, Merge `b2dc6d6`, Tag `v0.6.0`, Wiki `4f42138`)

**Lage:** `main` trägt T12.3–T12.5 (D112–D114). Pin `build/ship-data.json` = `ships-2026.11.1` = Latest → v0.6.0
startet mit 110 Schiffen inkl. GAMA Hyun, ohne Ship-data-Zeile. Veröffentlicht ist v0.5.1 (GitHub, Nexus-Upload offen
aus V5); Velopack v0.5.1 → v0.6.0 ist der Netz-Gate. `holo-v3` (Pre-Release) bleibt für v0.5.x. CI auf `main` grün
(Run 44372291418).

**Plan (19.11.2026, Freigabe Master-Chat 19.11. mit R1 Vorschlag, R2 keine Zeile)**

1. **Release-Notes `docs/release-notes/v0.6.0.md`** (eigener Commit, **einzeln zur Freigabe vor dem Tag**), Aufbau wie
   `v0.5.1.md`:
   - *Updating:* v0.5.1 → Update-Zeile (Delta, wenn genau eine Version zurück); v0.5.0 → Update-Zeile, voll; v0.4.0
     und älter → Handanleitung von v0.5.0. SHA, Borderless.
   - *Ship data:* eigener Kanal `sc-cargoplan-shipdata`, monatliches Data-Release, Zeile `Ship data update available ·
     <v>` mit Update / Skip this version, Übernahme per Neustart, Plan bleibt; 110 Schiffe, neu GAMA Hyun, F7C Hornet
     Mk II jetzt aus den Spieldateien; Quelle Motherlode (cryon.rocks).
   - *Changes:* Hologramme folgen dem Datenstand; Settings-Abschnitt „Ship data“; Holo-Schalter mit Text, wenn der
     Stand keine Holos anbietet; CLI `ships --version`; gespeicherter Plan mit unbekanntem Rack wird neu geplant statt
     beiseitegelegt (R3/D59-Addendum).
   - *Network access:* drei, alle GET, nur github.com, je Schalter: App-Update-Check, Ship-data-Check (+ Download auf
     Klick), Holo-Dateien.
   - *Before you update:* alter Cache `holo\holo-v3` wird einmal gelöscht (eine Notice), Holos kommen einmal neu.
   - *Known issues:* Datenstand älter als zwei Releases → fehlende Hologramme als Umriss, die Update-Zeile ist der Weg
     zurück; Downgrade auf v0.5.x lädt den Holo-Cache neu; 135c und 600i EE weiter Fleetyards; die übrigen Update-KIs
     aus v0.5.1, soweit gültig. Notice- und Settings-Wortlaut per Grep aus `src/`, nicht aus dem Gedächtnis.
   - *Vollständigkeit (Lehre T9.2):* jeder der 21 Nicht-Merge-Commits aus `git log v0.5.1..main` in einer Tabelle hier
     zugeordnet; `9832bea` (Dependabot `action-gh-release`, nur CI) ohne Zeile (R2).
   - Credits/Fuß: Motherlode als Quelle der Schiffsdaten; Not-under-MIT-Satz wie README (NOTICE des Data-Release).
2. **README:** Z. 19 → 110 Schiffe, zwei mit Fleetyards-Daten (135c, 600i EE); Abschnitt **Ship data** (woher, wie oft,
   Meldeweg nach R1); Liste **Network access** (drei GET, Schalter); Fleetyards-Credit auf zwei Schiffe, bleibt als
   Geschichte; „Not affiliated“ bleibt.
3. **Wiki** (`..\sc-cargoplan.wiki`, Branch `v0.6.0` ab `master` `8916f01`): Installation and first start (erster Start
   ohne Netzpflicht, zwei Update-Zeilen, Dateitabelle `holo\<data version>\` und `ships\<tag>\`), Ships and cargo grids
   (110, Hyun, Quelle, monatlich, Meldeweg, Holos aus dem Data-Release), Preferences (Abschnitt Ship data, Holo-Schalter
   mit Kill-Switch-Text), Overlay basics (zwei Update-Zeilen). **Push erst nach dem Tag.**
4. **Nexus-Texte** `docs/nexus/description.bbcode`, `checklist.md`, `summary.txt`: 110 Schiffe, Datenkanal, drei
   Netzzugriffe (Nexus-Regel: offenlegen), holo-v3 → Data-Release. Upload macht Martin mit demselben ZIP.
5. **`ship-correction.yml`** und **CONTRIBUTING** „Correct ship data“: Daten liegen in `sc-cargoplan-shipdata`, ein PR
   geht dorthin; Zahlen auf heute.
6. **`holo-v3`-Auslauf:** D63-Addendum (19.11.2026): Release bleibt bis **31.01.2027** (zwei Monate nach v0.6.0);
   Backlog-Zeile „`holo-v3` löschen“ mit Auslöser 31.01.2027 und Anlass D114. Kein Löschen in T12.6.
   **Durchsicht (Martin, 19.11.):** `holo-v2` bekommt dasselbe Datum; Addendum und Backlog-Zeile nennen beide.
7. **Stopp:** Notes zur Freigabe (Master-Chat), Rest als Diff zur Durchsicht.
8. **Release nach Notes-Freigabe:** Draft-PR `release/v0.6.0` → `main` nur für CI, grün abwarten → Merge `--no-ff` lokal
   → auf dem Merge-Commit `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` im sauberen Worktree,
   `./build/publish.ps1 -Version 0.6.0 -DeltaBase github` (Layout-Prüfung inkl. `ship-data\` = `ships-2026.11.1
   (baseline)`), B7-Referenz → Martin pusht `main` → CI grün → Tag `v0.6.0` (Martin) → `release.yml` →
   **Release-Prüfung (CC)** als Tabelle wie T11.5 (Latest, Body = Notes, Assets inkl. Delta und Feed, SHA, ZIP-Wurzel,
   `ship-data\`, Runtime = lokal, Größe) → ZIP-Größe in CLAUDE.md → CC merged Wiki `v0.6.0` lokal `--no-ff`, Martin
   pusht → **Netz-Gate** (Martin) → Nexus (Martin).

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. Wiki zusätzlich: Updates (dritter Netzzugriff), FAQ („two things“, „three from Fleetyards“), Troubleshooting (zwei
   Entwürfe statt drei), `_Footer` → v0.6.0 – sonst widersprechen sie den vier genannten Seiten.
2. „Settings“ im Wiki = Seite `Preferences` (eine Seite „Settings“ gibt es nicht).
3. Draft-PR nur für CI vor dem Merge (Lehre V5: `MinWidth` war erst auf `main` rot, der Branch lief nie durch CI).
4. Backup von `%LocalAppData%\SC-CargoPlan` (settings, plan, `.bak`) vor dem Netz-Gate in der Anleitung: das
   Velopack-Update läuft ohne `--data-dir` im echten Profil.
5. ZIP-Größe v0.6.0 in CLAUDE.md nach der Release-Prüfung (Muster `3d15333`).
6. **R1 Meldeweg falscher Grids** (Sciirofs Wunsch steht aus): `ship-correction.yml` bleibt im App-Repo als Einstieg
   (Hauler kommen hier an), nennt `sc-cargoplan-shipdata` als Ort der Daten und Ziel eines PR, „wir geben es an
   Motherlode weiter“; README/Wiki: Issue hier oder im Shipdata-Repo. Direkt bei Motherlode erst, wenn Sciirof es so will.
7. **R2:** keine Notes-Zeile für `9832bea` (Dependabot, nur CI, nicht im ZIP; wie Test.Sdk in V4).
8. Sciirof-Antwort (18.11., Master-Chat) im Backlog unten nachgetragen; die drei Pipeline-Punkte gehen an T12.6b.

**Schritt 1 – Notes (19.11.2026):** `docs/release-notes/v0.6.0.md`, zur Freigabe im Master-Chat.

Vollständigkeit `git log --no-merges v0.5.1..main` (21 Commits):

| Commit | Inhalt | In den Notes |
|---|---|---|
| `9832bea` | Dependabot `softprops/action-gh-release` | nein (R2: nur CI, nicht im ZIP) |
| `3d15333`, `3cb3d4d`, `0a7d284` | REL/V5: Release-Prüfung v0.5.1, Netz-Gate, Nexus-Texte, holo-v1 gelöscht | nein (Doku, betrifft v0.5.1) |
| `a5a00c5` | T12.0: D111, Phase-12-Tasks | Ship data (Kanal), Network access |
| `845785b`, `802ef6d`, `2d017ec`, `8880770`, `68bec39`, `61d6d35` | Pläne, Abnahmen | nein (nur TASKS.md) |
| `9e3c620` | Core: Datenstand lesen, installieren, wählen; R3 unbekanntes Rack | Ship data (Update), Changes (Plan mit unbekanntem Rack) |
| `ab06ae8` | App: Check, Download, Neustart; Settings „Ship data“ | Ship data, Changes (Settings), Network access 2, KI (Editor-Zeilen, `Program Files`, Downgrade `shipData`) |
| `2f41d0a` | CLI `ships --version` | Changes (Command line) |
| `12a16d2`, `39c3a50`, `a8c4900` | Doku D112–D114 | indirekt (Quelle der Texte) |
| `6371e49`, `f2fcae2` | Umsetzungsnotizen, Gate-Anleitungen | nein |
| `46fd06d` | Build holt die Baseline `ships-2026.11.1` | Ship data (In this ZIP, Hyun, F7C, 109 + Modul) |
| `06170bc` | Holos aus dem Manifest, D95-Cache-Wechsel, Kill-Switch-Text | Changes (Holograms), Network access 3, Before you update, KI (alte Datenstände, Downgrade lädt neu) |

**Präzisierungen beim Schreiben:**
1. **109 Schiffe und eine Modulkonfiguration**, nicht „110 Schiffe“: die Baseline hat 110 Dateien unter `ships\`, eine
   davon ist `modules/ts_module.json`; v0.5.1 nannte „108 ships“ ebenso ohne Modul. „110“ im Log zählt die Modul-Datei
   mit. README, Wiki und Nexus folgen derselben Zählung (Gegenargument: der Prompt und der T12.5-Log sagen 110).
2. **F7C: kein „re-plan“-Satz.** Rack-ID und Gitter bleiben (1×2×1, 2 SCU), nur `origin` wandert → der Plan lädt wie
   gespeichert, ohne Notice (wie die 350r in V1); bei einem Rack liegen die Kisten trotzdem richtig.
3. **Neues Known issue „Going back to v0.5.x“:** v0.5.x kennt `shipData` nicht und startet nach einem Speichern in
   v0.6.0 mit Default-Settings (`settings-format.md`, D105-Addendum) – gleiche Abhilfe wie v0.4.0/`updates`.
4. **Ship-data-Update braucht nie Adminrechte** (Ziel `%LocalAppData%\SC-CargoPlan\ships`), als Satz am KI
   `Program Files`.
5. Delta-Satz: nur wer v0.5.1 über die Update-Zeile bekam, hat die Basis in `packages\` (T11.3: ein entpacktes Portable
   hat keine) → „only the changes“, sonst voll.

**Notes freigegeben (Master-Chat, 19.11.)** mit zwei Wortlaut-Korrekturen: Schiffszahl in Notes, README, Wiki und Nexus
einheitlich „109 ships and one cargo module configuration“; Netz-Satz präzisiert: GitHub sieht IP-Adresse und
User-Agent – `SC-CargoPlan/<version>` (Ship data, Holos; `ShipDataSource`, `HoloSource`) bzw. `Velopack/<version>`
(Update-Check; `HttpClientFileDownloader.UserAgent` in velopack/velopack, Release-Liste über die GitHub-API, daher
„GitHub“ statt „github.com“).

**Schritt 2 – übrige Texte (19.11.2026):** README (Schiffszeile, Abschnitte *Ship data* mit Meldeweg R1 und *Network
access*, Fleetyards-Credit zwei Schiffe), CONTRIBUTING, `ship-correction.yml`, Nexus `description.bbcode` und
`checklist.md` (`summary.txt` ohne Zahl und Netz, unverändert), D63-Addendum + Backlog-Zeile `holo-v3` (31.01.2027).
Wiki Branch `v0.6.0`: Ships and cargo grids (neuer Abschnitt *Ship data updates*, Holos aus dem Data-Release,
Meldeweg), Updates (Tabelle *What it sends*, neuer Abschnitt *Ship data* mit Meldungen und Fehlergründen), Preferences
(Holo-Schalter mit Kill-Switch-Text, Abschnitt *Ship data*), Overlay basics (neuer Abschnitt *Update lines at the
bottom*), Installation (keine Netzpflicht, `ship-data\`, Downgrade v0.5.x, Dateitabelle), FAQ, Troubleshooting,
`_Footer` v0.6.0. Wortlaute (Status, Notices, Fehlergründe, Settings) per Grep aus `src/`.

**Nebenfunde in Schritt 2 (gleich korrigiert, Präzisierung beim Schreiben):**
- „The Retaliator is not included.“ stand noch in Nexus-Beschreibung und Wiki-Troubleshooting; sie ist seit v0.5.1
  drin (V2) → Zeile gestrichen.
- Wiki Preferences: „Hologram opacity (10–100 %)“ → **1–100 %** (`AppearanceRules.MinHoloOpacity = 0.01`, D63-Addendum
  T8.5).
- Wiki Troubleshooting: `current\ships` → `current\ship-data` (D113).
- Nexus und Notes nennen im Lizenzsatz jetzt auch `models\NOTICE.md` (stand im v0.5.1-Text, fehlte bei Nexus).

**Nicht geändert, nur gemeldet:** `docs/ship-format.md` Überschrift „The three Fleetyards ships“ (Text nennt die F7C
schon als Spieldatei) – Entwickler-Doku, gehört in die nächste Doku-Runde oder T12.6b; `decisions.md`-Stellen mit „102“
/ „three ships“ sind Geschichte.

**Schritt 3 – Draft-PR, Merge, Probe (CC, 19.11.2026):**
- Draft-PR [#29](https://github.com/MRiediger/sc-cargoplan/pull/29) nur für CI: Run 44473277459 grün auf `ubuntu-latest`
  und `windows-latest`.
- Merge `--no-ff` lokal → **`b2dc6d6`**. Darauf, SDK 10.0.404: Build `0 Warnung(en)`, `0 Fehler`;
  `dotnet format --verify-no-changes` Exit 0. Test im sauberen Worktree:
  `Bestanden! … erfolgreich: 1913, … gesamt: 1913 - CargoPlan.Core.Tests.dll`, `… 280 … Import`, `… 34 … Ocr`,
  `… 2079 … CargoPlan.App.Tests.dll`.
- `./build/publish.ps1 -Version 0.6.0 -DeltaBase github`: `Delta base: CargoPlan-0.5.1-full.nupkg`, Delta 860 241 B;
  `Layout check: … passed (388 files in current\, 110 ship files of ships-2026.11.1, 5 PDBs, version 0.6.0)`. ZIP
  96 761 203 B. B7-Referenz: Runtime 10.0.15 (beide), `coreclr.dll` 10.0.1526.53008, Cli `0.6.0+b2dc6d6…`.
- Martin (19.11.): `main` gepusht, CI grün (Run 44474726157), Tag `v0.6.0` auf `b2dc6d6`. Dieses Protokoll kommt als
  Folge-Commit nach dem Tag, damit der Tag auf dem geprüften Commit steht.

**Release-Prüfung (CC, 19.11.2026):**

| Prüfung | Soll | Ist |
|---|---|---|
| CI auf `main` (Merge-Push) | grün | Run 44474726157 success |
| Workflow „Release“ | grün | Run 44475183595 success, 2 min 52 s |
| Tag | auf dem Merge | `b2dc6d6`; **leichtgewichtig**, nicht annotiert (wie schon `v0.5.1`; `v0.5.0` war annotiert) – kein Einfluss aufs Release |
| Release | Latest, kein Pre-Release, kein Draft | `isPrerelease false`, `isDraft false`, Latest, 06:53:50 UTC |
| Body | = `docs/release-notes/v0.6.0.md` | identisch (ohne CRLF verglichen) |
| Assets | ZIP, `.sha256`, Full-, Delta-nupkg, `releases.win.json` | genau diese 5: ZIP 96 760 709, Full 96 804 720, Delta 850 683, Feed 732, `.sha256` 98 B |
| SDK im Runner | 10.0.404 | `dotnet-install: Installed version is 10.0.404` |
| Delta | 0.5.1 → 0.6.0 | `Delta base: CargoPlan-0.5.1-full.nupkg`, 850 683 B |
| Layout-Prüfung | grün | `passed (388 files in current\, 110 ship files of ships-2026.11.1, 5 PDBs, version 0.6.0)` |
| SHA256 ZIP | = `.sha256` | `108145AC…7891E8`, gleich |
| `releases.win.json` | 0.6.0 voll + Delta, Size/SHA = Asset | beide gleich; dazu `Full 0.5.1` (Asset von v0.5.1, wie bei v0.5.1 der 0.5.0-Eintrag) |
| ZIP-Wurzel | `SC-CargoPlan.exe`, `Update.exe`, `.portable`, `current\` | genau diese, 388 Dateien in `current\` |
| `ship-data\` im ZIP | Pin `ships-2026.11.1` | Manifest `ships-2026.11.1`, 110 Schiffsdateien, `NOTICE.md`, `hyun.json`; CLI `ships --version` mit leerem Datenordner → `ships-2026.11.1 (baseline)` |
| Runtime (B7) | = lokales Pack | beide 10.0.15, `coreclr.dll` 10.0.1526.53008 |
| Cli-Version | `0.6.0+b2dc6d6…` | `0.6.0+b2dc6d61544153ef0383b85efa58f636cbf04923`, lokal gleich |
| Größe | „about 90 MB“ | 92,3 MiB; Delta von v0.5.1 0,85 MB |
| Hygiene | letztes stable unter den 10 neuesten | 10 Releases sichtbar, v0.6.0 an der Spitze (davon 2 Holo-Pre-Releases) |

**Wiki:** CC hat `v0.6.0` lokal mit `--no-ff` nach `master` gemergt (`4f42138`); Martin pusht.

**Netz-Gate (Martin, 19.11., `C:\Tools\SC-CargoPlan`, „passt alles“; Belege aus `cargoplan.log` und dem Profil):**
07:06:35 `Update: 0.6.0 available (installed 0.5.1)` → 07:06:56 `Velopack: Downloading delta 0.6.0`, `All delta packages
downloaded and verified`, `Applying 1 patches to CargoPlan-0.5.1-full.nupkg` (**erstes Delta übers Netz**, 850 683 B)
→ 07:07:05 `restarted by Update.exe as 0.6.0`, `current\sq.version` 0.6.0. `Ship data: in use: ships-2026.11.1
(baseline) …, 110 ships`, 07:07:07 `Ship data: nothing newer than ships-2026.11.1` → keine Ship-data-Zeile. Holo-Cache:
nur `holo\ships-2026.11.1\` (`holo-v3` weg, D95), darin `misc-hull-a.holo` und `misc-hull-b.holo`. Nachgeholt
(Martin, 08:12): Hyun ausgewählt, Holo „klappt“; `gama-hyun.holo` 268 668 B, SHA = Manifest. Die einmalige Holo-Notice
ist nicht aus dem Log belegbar (Notices stehen nicht darin); Martin meldete nichts Auffälliges.

**Aufräumen (CC, 19.11.):** Branch `release/v0.6.0` lokal und remote gelöscht (PR #29 merged), Wiki-Branch `v0.6.0`
lokal gelöscht, Prüfordner im Scratchpad weg. CI auf `main` für `1ce6871` grün (Run 44477012407).

**Akzeptanz:**
- [x] Notes vom Master-Chat freigegeben (19.11.); `git log v0.5.1..main` ohne unerwähnten Nutzer-sichtbaren Commit
  (Tabelle oben)
- [x] **Netz-Gate (Martin, SC vorn, 19.11.):** v0.5.1-Portable in eigenem Ordner → Start → Velopack bietet 0.6.0 → Update →
  Neustart → 110 Schiffe, Hyun mit Holo, `holo-v3`-Notice einmal, Hull-A-Plan da, **keine** Ship-data-Zeile, Settings
  `In use: 2026.11.1 (built in)` – bestanden, Delta übers Netz (Belege oben)
- [x] Nexus-Seite auf 0.6.0 (Martin, 19.11.2026, nach `docs/nexus/checklist.md`, dasselbe ZIP):
  <https://www.nexusmods.com/starcitizen/mods/52>. Die beiden Prüfungen aus Schritt 7 (SHA des Nexus-Downloads, Größe)
  nicht ins Protokoll gemeldet.
- [x] Commits `T12.6a: …`, kein `Co-Authored-By`

#### [x] T12.6b Shipdata-Abschluss (Shipdata-Repo, Plan und Abnahme in dessen `TASKS.md`)

**Erledigt (abgehakt in K12):** Shipdata-Branch `docs/p12-close`, Commits `8bb42d5`/`b1e4d29`/`f4467d1`/`20fe78d`,
Merge `49c0250` 19.11.2026; SD13 und SD3-Addendum dort. Befund 4 (Schiffsdatei-PRs in der App-Doku) erledigt K12 hier.

README des Shipdata-Repos, Phase 12 dort schließen (Lehren), Backlog konsolidieren, SD13, `cache/`-Hygiene,
Martin-Klicks (`sc-ship-outlines` archivieren, „Automatically delete head branches“, Sciirof-Mail). **Backlog-Nachtrag
Sciirof (18.11., Master-Chat) für dort:** (1) Motherlode liefert die Modell-URL in der `/cargo-grids`-Antwort: `fetch`
liest sie, wenn das Feld da ist, sonst die Seite, Summary zählt „from API / from page“, einen Monat beides, dann
Scraper raus (SD-Eintrag); Feldname unbekannt, Task erst, wenn das Feld erscheint. (2) 135c bekommt voraussichtlich
eine Transform: liefert der Katalog eine Transform für ein Schiff in `overrides.json`, endet der Lauf mit Stopp → PR
„override obsolete“, nie still (Regel prüfen/ergänzen). (3) Cyclone ggf. zurück: SD6 „wieder da“ deckt es ab, nur Zeile.

---

