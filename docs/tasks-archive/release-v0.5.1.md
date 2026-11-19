# Archiv: Release v0.5.1

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Release v0.5.1 – Branch `fix/v0.5.1` (16.11.2026)

**Warum:** Kleines Release vor Phase 12: Schiffsdaten Motherlode 4.10.3 (schon auf `main`, `92d9d99`), Holo-v3, Kopfzeile
(B15), Abhängigkeiten (Dependabot #26), erster Selbsttausch übers Netz. Plan zur Freigabe nur für B15, der Rest ist Ablauf.
Vorbedingung #26: beim Abzweig noch nicht auf `main`; der Branch wird lokal auf `main` rebased, sobald Martin #26 gemerged
hat (berührt B15 nicht).

**Reihenfolge (Martin, 16.11.):** B15 → #26 mergen (Martin) → Rebase (CC) → Push (Martin) → V1 → V2 → V4 → V5.

### [x] V1 Alte 350r-Pläne laden (`aeea958`, abgenommen 16.11.)

- Eingefrorene `plan.json` aus 0.5.0 mit 350r (alte Gitterpositionen) lädt ohne Absturz; Verhalten bei Positionen
  außerhalb des neuen Gitters im Befund; Notes-Zeile „saved plans load, re-plan recommended“ (wie 4a-E2).

**Befund (16.11.):**
- **Was sich änderte** (`92d9d99`): 0.5.0 hatte eine Platte `rack-0` 2×2×1 bei `[-4,-1,0]` (4 SCU). Heute ist `rack-0` eine
  neue 4×2×1 bei `[-2,-1,0]` (8 SCU) und die alte Platte heißt `rack-1` (12 SCU gesamt).
- **Fixture echt aus 0.5.0:** Worktree auf Tag `v0.5.0` im Scratchpad, Wegwerf-Test plant mit dem alten Code auf der alten
  350r und schreibt per `PlanFile.Write` (Schema 10): Shubin 2 Quartz (Kiste 2, abgehakt), Riker 2 Titanium (2 × 1), alle
  in `rack-0` → `tests/CargoPlan.Core.Tests/Fixtures/Plans/350r_v0.5.0.json`.
- **Verhalten:** Keine alte Position liegt außerhalb des neuen Gitters (das 2×2 passt ins 4×2). Die Datei lädt **wie
  gespeichert**, ohne Re-plan und ohne Notice; Haken bleibt. Die Kisten stehen aber auf der **vorderen** Platte, also
  nicht dort, wo der Spieler geladen hat; nichts weist darauf hin. Re-plan legt alles fehlerfrei auf die heutigen Racks,
  Haken bleibt. Im echten Overlay (`--plan`, `--data-dir` im Scratchpad): `ORIG 350r 2/12 · 10 free`, Kisten auf Rack 1,
  keine Re-plan-Meldung.
- **Positionen außerhalb des neuen Gitters** (allgemein, nicht bei der 350r): Die Datei liest als „racks changed“, wird
  beim Laden neu geplant, Haken bleiben (`offShip`), Amber-Zeile „Plan was made for other racks, re-planned for …“
  (`PlanFileDto.Checked`, Tests in `CrateRotationTests`). Eine Rack-ID, die das Schiff nicht mehr hat, ist ein Fehler:
  kein Plan, Notice, die Datei wird beim ersten Speichern beiseitegelegt (D106).
- Tests: `OldPlan350rTests` (4, Datentest über `ships/`, A10): Prämisse der Racks, lädt wie gespeichert ohne Re-plan,
  Kisten auf der vorderen Platte, Re-plan ohne Wartende mit Haken. `docs/plan-format.md` „Racks that changed“: Absatz für
  den Fall „Rack verschoben oder gewachsen, Kisten passen noch“.
- **Notes-Zeile (Vorschlag für V4):** „Saved plans load as they were. ORIG 350r: the game added a second cargo grid in
  front; a 350r plan saved before shows its crates there. Re-plan once.“
- **Nebenfund für V2:** `92d9d99` hat der 350r das `modelKey` (`orig-350r`) genommen; `outlines/ship-outlines.json` kennt
  den Slug weiter. Bis V2 zeigt die 350r nur den Bug-Keil, kein Hologramm, keinen Umriss (gesehen im Overlay). Nur die
  350r ist betroffen; Galaxy, Liberator, Retaliator haben noch keinen `modelKey` (kommt mit V2).
- [x] Abnahme Martin (16.11.2026)

### [x] V2 Holo-v3 (Martin baut, CC verdrahtet; `e21f52a`, `65e5b88`, abgenommen 16.11.)

- Pipeline (`model-map.csv`) → Umrisse der drei neuen Schiffe, `import-motherlode.ps1` (Anker), Bildvergleich gegen
  `holo-v2` (nur Galaxy, Liberator, Retaliator, 350r dürfen abweichen), Release `holo-v3` **vor** dem Tag,
  `HoloRelease.Tag` → `holo-v3`, `docs/holo-format.md`, Cache-Wechsel nach D95 in die Notes.
- `holo-v1` wird erst **nach** dem Netz-Gate gelöscht (kurzzeitig v1 + v2 + v3, gewollt; E2 der Freigabe).

**Präzisierung in der Planphase, ratifiziert durch Freigabe (16.11.):** Slugs `rsi-galaxy`, `anvl-liberator`,
`aegs-retaliator`, 350r zurück auf `orig-350r`; die Cyclone (nicht mehr im Katalog) behält Datei und Umriss aus holo-v2;
die 135c bleibt holo-v1 (Grid ohne Transform, Import `kept`); Liberator `passed: false` (hausdorff) bleibt drin (D63);
101 Schiffe statt der geschätzten 118; `outlines-v2`/`release-v2` nehmen den Tag als Argument (Pipeline-Branch
`holo-v3`); K1-Test; E1 nach Phase 12.

**Befund (16.11.):**
- **Ohne `modelKey` kein Holo:** Map, Umrisse und Release bauen nur Slugs, die der Import aus den Schiffsdateien liest.
  Galaxy, Liberator, Retaliator kamen in `92d9d99` ohne Key, die 350r verlor ihren dort. **Ursache (K1):** neue Rack-Zahl
  → `handcheck` „rack set differs“ → Datei neu mit `-Create`; dieser Zweig (keine Datei, keine Variante) setzt keinen
  Slug und die Anker auf die Rack-Mitte. Test `ShipOutlineLoaderTests.Every_outline_belongs_to_a_shipped_ship` (rot mit
  `orig-350r`, dann grün); Abhilfe im Import: Phase 12 (E1).
- **`run-all` hatte 18 Modelle gerechnet, die 14 geänderten aber aus dem alten `flat.npz`** (`decimate.ps1` nutzt
  vorhandene Schritte; `PP.holo` byteidentisch zu holo-v2). Neu mit `run-all.ps1 -Source Motherlode -Redo flatten
  -Slugs @(…)` (10,9 min, keine Fehlschläge) und `holo_mesh.py outline` je Modell. In `TOOLS.md` (Pipeline) festgehalten.
- **Umrisse** (`outlines/ship-outlines.json`, dataVersion `holo-v3`): 101 Einträge, neu Galaxy, Liberator, Retaliator;
  geändert nur der Prowler Utility (174 → 144 Punkte, Maße und Boden gleich); 97 byteidentisch.
- **Release-Ordner** `sc-ship-outlines\holo\ml\release-v3` (Pipeline `2ea2a2c`, Manifest `pipelineCommit` `e6943af`):
  101 `.holo`, 32,9 MB, `verify` ohne Probleme, 22 `passed: false` (holo-v2: 21; neu der Liberator). Gegen holo-v2: 85
  byteidentisch (darunter 350r und die vier behaltenen), 3 neu, 13 geändert = die neuen Modelle (Carrack, Paladin,
  Cutter ×3, Prowler Utility, Railen, Tyilui, Starlancer Max/TAC, Constellation Phoenix/Taurus, Perseus).
- **Anker** (Import 2, Exit 0): 350r, Galaxy, Liberator, Retaliator; die 14 bleiben `same` (Boden unverändert).
- **`fit`** (`holo\ml\fit-v3.csv`): alle Rack-Ecken im Modell, größtes Eindringen Retaliator rack-0 0,054, Umriss-IoU
  ≥ 0,956 (Tyilui). **Kontaktabzug** `sc-ship-outlines\holo\ml\sheet-v3\PP-*.png` (18 Modelle inkl. 135c zur Info):
  Draufsicht deckt sich überall mit dem roten Umriss.
- **Release `holo-v3`** (Martin, 16.11.): Pre-release „Ship holograms v3“ in **diesem** Repo (die App liest nur hier,
  `sc-ship-outlines` ist privat), Ziel `main`, 103 Assets. Nachgeladen und verglichen: byteidentisch mit dem Ordner,
  `verify` ohne Probleme, Manifest anonym 200.
- **App:** `HoloRelease.Tag` = `holo-v3`. Vorab rot: 5 Holo-Tests, weil `FakeHttp` holo-v2-Manifeste baute (der Reader
  lehnt einen fremden `dataVersion` ab) → `FakeHttp` nimmt jetzt `HoloRelease.Tag`; die URL in
  `HoloSourceTests.Fetches_manifest_and_file_from_the_release_and_caches_both` bleibt Literal (fängt einen Tippfehler).
  Cache-Wechsel ohne Code: D95 löscht jeden anderen `holo-v<n>`-Ordner, einmal die Notice.
- **Bildvergleich** `ShipPictureTests` (P1: nur das `ShipViewControl`, keine Kopfzeile, B15 zählt nicht): Baseline
  `3d98aad` (Branch vor V2) mit holo-v2 gegen den Branch mit holo-v3, 1399 Bilder, **bestanden**: 1284 identisch, 143
  verschieden, 4 fehlend, keine unerwartete Abweichung. Verschieden: 350r, Galaxy, Liberator, Retaliator, Prowler Utility
  (je 11, Umriss und Holo), Paladin, Cutter ×3, Railen, Tyilui, Starlancer Max/TAC, Constellation Phoenix/Taurus, Perseus
  (je 8, nur Holo). Fehlend: `outline;above` der vier Schiffe, die jetzt ein Holo haben (die Draufsicht zeigt es).
  **Carrack** gleich trotz neuer Datei: dieselben Dreiecke, 244 in anderer Reihenfolge. Kontaktabzug
  `sc-ship-outlines\preview\holo-v3\sheet\_sheet1.png` … `_sheet10.png` (alle 109 Schiffe, schräg und von oben).
- **Doku:** D93-Addendum „holo-v3“, `holo-format.md` (Tag, Quelle, Cache, `model`, Abschnitt „Pipeline“ ohne Tag),
  `ship-format.md`, `settings-format.md`, README, CLAUDE.md; Wiki (Branch `v0.5.1`): Ships and cargo grids, Installation.
  Nicht geändert: alte Release-Notes; Nexus-Texte (`holo-v2` in `docs/nexus/description.bbcode`, `checklist.md`) → V5.
- [x] Bildvergleich `ShipPictureTests` (Baseline `3d98aad` + holo-v2, Branch + holo-v3): **bestanden**, 1399 Bilder,
  1284 identisch, 143 verschieden (nur innerhalb der 20 erwarteten IDs), 4 fehlend (`outline;above` der vier Schiffe mit
  neuem Holo), 0 unerwartet.
- [x] Sichtprüfung und Abnahme Martin (16.11.2026)

### [x] B15 Kopfzeile – Schiffsname bleibt sichtbar (D110, revidiert D67; `7463bc5`, `0119040`, `89d5a71`, abgenommen 16.11.)

- **Anlass:** Screenshot RAFT, schmal: Name `AR…`, rechts `All stops delivered.` voll.
- **Ursache:** nicht `TextTrimming`, sondern das `DockPanel` der Kopfzeile, das Platz in Markup-Reihenfolge vergibt:
  Buttons (210 px, mit ↶ ↷ 270 px, **auch gesperrt**, nur `Hidden`), dann der Status, dann innen die Kapazität, der Name
  zuletzt.
- **Fix:** `HeaderLayout.Stage` (reine Funktion, ein Durchgang) und `HeaderPanel` (die ganze Kopfzeile: Name,
  Kapazität lang/kurz, Status, **Tools** = Split, Merge, Re-plan, Editor, ↶, ↷, Reset; **Chrome** = Zahnrad, X; misst die
  Kinder ohne Grenze, ordnet weggefallene Teile in ein leeres Rechteck, ändert nie eine Visibility). Stufen: Status weg →
  Kapazität kurz (`54/64 +16`) → Tools in die zweite Zeile, linksbündig ab x = 0, gleiche Reihenfolge, rechts frei
  (DragMove); Zahnrad und X bleiben rechts in Zeile 1 neben dem Schloss. Der Name kürzt nur, wenn er mit kurzer Kapazität
  und Chrome breiter ist als die Zeile. Erste Zeile 30 px (`LineHeight`), Rand 30 px fürs Lock-Fenster. Tooltip an
  beiden Kapazitäten mit `All stops delivered.` als zweiter Zeile. `HeaderButtonStyle` gesperrt `Collapsed` (Zeile 2
  entfällt von selbst). `AllDeliveredText` von `OverlayViewModel` nach `ShipScene`. **`MinWidth` 240 → 380** aus den
  Daten.
- **Präzisierung in der Planphase, ratifiziert durch Freigabe:** Nummer B15 statt B11 (vergeben, D98), D110; Breiten per
  `Measure(∞)` statt `FormattedText`; gesperrt `Collapsed` statt `Hidden`; `waiting` kurz als `+16` (Variante B); `▣`
  fällt in der kurzen Form weg; Tooltip stufenunabhängig; `available = +∞` → `Full` per Fixture (E1). Zweite Freigabe
  (K1 neu, ersetzt „Stufe 3 = Ellipse“ und „`MinWidth` bleibt 240“): Stufe 3 = Umbruch, Name nie; R1 Zeile 1 bleibt
  kompakt (Monotonie beim Ziehen); R3 30-px-Rand fürs Lock-Fenster. Dritte Freigabe: nur die Tools brechen um, linksbündig;
  Zahnrad und X bleiben in Zeile 1; R4 kein Scan-Button in der Kopfzeile (es gibt keinen, „Auswahl“ = Split); R5 Reset
  view gehört zu den Tools; `MinWidth` pro Schiff (Name mit Chevron + eigene Kurzform `N/N +N` + Chrome 60 px, Maximum
  über alle Schiffe, + Rand, auf 10 aufgerundet) als Konstante, nicht dynamisch; Doku nur im Wiki (`docs/user-guide.md`
  gibt es seit T7.5c nicht mehr); Gate-Zeile Lock in Stufe 3.

**Befund:**
- Tests vorab: `HeaderLayoutTests` (jede Grenze ±1 inkl. Stufe 3 und Notfall-Ellipse mit Chrome, ∞, gesperrt ohne
  Buttons), `HeaderPanelTests` (Slots je Stufe, Tools in Zeile 2 bei `(0, 30)`, Chrome rechts in Zeile 1, Höhe 30 + 26,
  gesperrt eine Zeile, Clip, echte `TextBlock`s für RAFT und Hull C, Daten-Fixture über `ships/`),
  `ShipViewBuilderTests` (+3), `OverlayHeaderXamlTests` (`HeaderPanel` mit Tools- und Chrome-Block ersetzt die D67-Regel,
  Inhalt und Reihenfolge links → rechts, `Collapsed`, kurze Kapazität ohne `IsLocked`).
- **Daten-Fixture** (Meldungsschrift 12 px), pro Schiff: breiteste erste Zeile **RSI Constellation Mk V Centaurus**,
  Name 189,4 + `240/240 +240` 76,2 + Chrome 60 = 325,6 px, Fenster **375,6 px → `MinWidth` 380**; danach Constellation
  Mk IV Andromeda (260,1 + 60), Carrack Expedition w/C8X (259,0 + 60). Zeile 2: 7 Tools = 210 px. Der Test schlägt fehl,
  sobald ein neues Schiff mehr braucht.
- Verworfene Zwischenstände: Stufe 3 = Ellipse bei `MinWidth` 240 (entsperrt −20 px für Name und Kapazität); alle Buttons
  in Zeile 2 mit Regel „längster Name × breiteste Kurzform verschiedener Schiffe“ (360, mit Chrome wären es 420 gewesen).
- Echtes Overlay (`--data-dir` im Scratchpad, 125 %): Hull A 600 px `54/64 · 10 free` in einer Zeile; Constellation Mk V
  Centaurus 760 px alles in einer Zeile, bei 475 px (= 380 DIP) Zeile 1 `RSI Constellation Mk V Centaurus ˅ 0/240` mit
  Zahnrad, X und Schloss rechts, Zeile 2 die Tools linksbündig.
- [x] Gate Martin (16.11., bestanden): RAFT und Hull C, breit → schmal, SC vorne, entsperrt **und** gesperrt; Hull C entsperrt bei `MinWidth`
- [x] Gate Martin (16.11., bestanden): bei schmalem Fenster (Stufe 3) das Schloss klicken und wieder entsperren – Zeile 2 verschwindet und
  kommt zurück, das Lock-Fenster bleibt an Ort und Stelle, die Legende rutscht ohne Sprung
- [x] Wiki lokal committet (`2e610dc`, `3b30ae2`, `345e0ef` auf Wiki-Branch `v0.5.1`, Push mit dem Release, V5): Overlay basics (Kopfzeile, schmales Overlay, `+16`, gesperrt), Quick start
  (Status)

### [x] V4 Release-Notes `v0.5.1.md` (`25b7632`, abgenommen 16.11.)

- Ships (vier Zeilen inkl. 350r), Holo-v3 (einmaliges Neuladen), Kopfzeile, Abhängigkeiten; Known issues: `holo-v1`-Zeile
  neu, KI 3 mit „this is the first self-update – it downloads the full package“.

**Stand (16.11.):** `docs/release-notes/v0.5.1.md` (der Release-Workflow verlangt die Datei vor dem Tag und nimmt sie als
Release-Text). Abschnitte wie v0.5.0: Updating (v0.5.0 → Update-Zeile; v0.4.0 und älter → Link auf die Handanleitung
von v0.5.0), Ship data (Quelle 4.10.3, drei neue mit SCU, 350r 8 + 4, neue Holos, Cyclone), Changes (Kopfzeile nach
Wiki-Wortlaut, ONNX Runtime 1.32.0), Before you update (Holos einmal neu, 350r-Satz aus V1), Known issues (KI 3 mit dem
Satz aus dem Task, die übrigen Update-KIs aus v0.5.0, neu `holo-v1`, die drei Fleetyards-Schiffe), Fußzeile `holo-v3`.
- **Präzisierung beim Schreiben:** „New holograms for **12** ships“, nicht 13: Die Carrack hat eine neue Datei, aber
  dieselben Dreiecke in anderer Reihenfolge, ihr Bild ist gleich (V2-Bildvergleich). Test.Sdk 18.12.0 aus #26 fehlt
  bewusst (nur Tests, nicht im ZIP).
- Wiki-Anker `Overlay-basics#on-a-narrow-overlay` geht erst mit dem Wiki-Push in V5 live.
- **Präzisierung bei der Abnahme (K1):** Cyclone-Satz „missing from the grid catalog this release was built from“ statt
  „no longer in the game data we use“ (der Katalog fehlt sie, über das Spiel sagt das nichts).
- [x] Abnahme Martin (16.11.2026)

### [x] V5 Merge, Tag, Netz-Gate, Nachlauf

- Merge `--no-ff` nach Abnahme, Martin pusht und taggt.
- [x] Merge `f4c8789` (Wiki `8916f01`, Pipeline `25b4031`, lokal). **CI auf `main` rot** (Run 43887955591, windows-latest):
  `HeaderPanelTests.At_the_minimum_width_…` brauchte 397,6 px, `MinWidth` 380. Ursache: B15 maß mit der Meldungsschrift
  des Testrechners, Segoe UI **11 px** (Martins Anzeige 110 %, LogPixels 106); Windows-Standard und Runner haben 12 px.
  Fix: Test misst fest mit Segoe UI 12 px, **`MinWidth` 400** (D110-Addendum). B15 lief vor dem Merge nie durch CI
  (CI nur auf `main` und PRs).
- **Wiki-Push unmittelbar nach dem Tag** (Martin, 16.11.): CC merged den Wiki-Branch `v0.5.1` lokal nach `master`,
  Martin pusht; die Release-Notes verlinken `Overlay-basics#on-a-narrow-overlay`, und das Wiki nennt dann holo-v3.
- [x] Martin (16.11.): `main` gepusht, CI grün (Run 43889583398, `da48270`), Tag `v0.5.1` auf `da48270`, Wiki gepusht.
- [x] **Release-Prüfung (CC):** Workflow „Release“ grün (Run 43890674127); v0.5.1 „Latest“, nicht Pre-release, Text =
  Release-Notes; holo-v3 bleibt Pre-release. Assets: ZIP **96.676.143 B (92,2 MiB)**, `.sha256` passt zum geladenen
  ZIP, `CargoPlan-0.5.1-full.nupkg` 96,7 MB, `-delta.nupkg` 10,7 MB, `releases.win.json` mit 0.5.1 voll und Delta und
  0.5.0 voll. Stichprobe im ZIP: `.portable`, `SC-CargoPlan.exe`, `Update.exe`, `current\` mit 108 Schiffen,
  `galaxy.json` mit `rsi-galaxy`, Umrisse `holo-v3`, `sq.version` 0.5.1, die App-DLL nennt `holo-v3` (UTF-16), nicht
  `holo-v2`. „about 90 MB“ in den Notes: 92,2 MiB, der Explorer zeigt ≈ 94.400 KB.
- Netz-Gate (Martin, Backlog „Erster Selbsttausch übers Netz“): v0.5.0-Portable aus Browser-Download → Notice
  `Update available · v0.5.1` → Update; Größe aus den `Velopack:`-Zeilen (≈ 90 MB voll); SmartScreen für `Update.exe` und
  den ersten Start; 0.5.1 läuft, Galaxy in der Auswahl, Holo-Cache-Notice einmal, Holo für Galaxy und Hull A.
- [x] **Netz-Gate bestanden** (Martin, 16.11., eigene Portable in `C:\Tools\SC-CargoPlan`, „funktioniert!“; Belege aus
  `cargoplan.log` und dem Profil): 14:59:08 `Update: 0.5.1 available`, Velopack „no local/base package … delta updates
  will be disabled“ → `CargoPlan-0.5.1-full.nupkg` (96,7 MB) in 4,2 s, Checksumme geprüft, neues `Update.exe`; 14:59:22
  `restarted by Update.exe as 0.5.1`, erster Frame 874 ms, `current\sq.version` 0.5.1. Holo-Cache: nur `holo\holo-v3`
  (`holo-v2` gelöscht, D95), neu geladen Manifest, C2 Hercules, Galaxy; Galaxy-Holo im Bild (Screenshot Settings,
  Download an). Nicht aus dem Log belegbar: SmartScreen (die Portable war kein frischer Browser-Download) und die
  einmalige Holo-Notice (Notices stehen nicht im Log) – Martin meldete nichts Auffälliges.
- [x] Nexus-Texte `holo-v2` → `holo-v3` (`docs/nexus/description.bbcode` Z. 15 und 81, `checklist.md` Z. 104).
- [x] `holo-v1` gelöscht (Martin, 16.11., nach dem Netz-Gate; entschieden gegen die Backlog-Frist 06.12.): Release
  und Tag weg, Manifest-URL 404; `holo-v2` (v0.3.0–v0.5.0) und `holo-v3` (v0.5.1) stehen, holo-v3-Manifest 200.
- [x] Nexus-Datei 0.5.1 hochladen (Martin, `docs/nexus/checklist.md`) – erledigt laut T12.6-Startprompt („Veröffentlicht
  ist v0.5.1 (Nexus und GitHub)“); seit 19.11. ersetzt durch 0.6.0 (T12.6a).

---

