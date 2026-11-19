# Archiv: Release v0.4.0

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Release v0.4.0 – Branch `release/v0.4.0`

**Warum:** Phase 10 (T10.1–T10.5, B11–B13) ersetzt ContractTracker durch den eigenen Scan. Ablauf wie v0.3.0: R0 Abend
→ R1 Texte → R2 Notes (Freigabe Claude.ai) → R3 Medien (Martin) → R4 Tag → R5 Nachlauf. Kein Produktcode im Branch; ein
Verhaltensfund wird ein eigener Fix-Task vor R4.

**Entscheidungen Martin (12.11.):** R0 erledigt, kein RC · Nexus startet mit **0.4.0**, kein Upload am 16.11. mit 0.3.1 ·
Hero-GIF neu (Martin nimmt auf, alle Scan-Shots auf der Hull A), CC liefert vorher die Shot-Liste · kein Credits-Abschnitt
in den Notes; die Lese-Engine (RapidOcrNet, PP-OCRv5, Apache-2.0) als Nebensatz im Scan-Highlight, der Dank an
ContractTracker als Halbsatz beim Wegfall.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. Nach einem neuen Scan heißt die Notice `Scanned #1 · From … · …` (`ScanNotices`); `Added #1` steht nur in `scans.log`.
2. Upgrade-Probe b aus Test-Fixtures (`Fixtures/Settings/v0.3.1_full.json`, `Fixtures/Plans/hurston_2026-10-25.json`, in
   der Scratch-Kopie eine Lieferung `loaded`, eine `delivered`): Martins Profil ist schon Schema 3/10.
3. ZIP-Größe in den Notes = veröffentlichtes ZIP (lokal + ≈ 2,1 MB CI-Kompression, wie v0.3.0/v0.3.1), Vergleich 64 MB.
4. Nach dem Tag ein `REL:`-Commit direkt auf `main` (CLAUDE.md ZIP-Zahl, Befund), Präzedenz `7010eff`, `1d3daa3`.
5. Wiki-Korrekturen auf dem Wiki-Branch `v0.4.0`, Merge nach `master` am Tag des Tags.
6. Known issues der Notes: die 6 Scan-Zeilen der README + Link auf die Wiki-Liste; die v0.3.x-Zeilen gelten weiter
   („blocks and gaps“ gilt für Scans genauso) und stehen im Wiki.
7. „Thanks for carrying the first **four** releases“ statt „three“: ContractTracker trug v0.1.0, v0.2.0, v0.3.0, v0.3.1.

### [x] R0 Hauling-Abend (12.11.2026)

**Befund (Martin):** 1,5 h auf `main` ohne ContractTracker, kein Fund, kein RC.

### [x] R1 Textabgleich (12.11.2026)

- `docs/v0.4.0-user-docs` gemergt (`--no-ff`, README, `outlines/NOTICE.md`, Nexus-Beschreibung und -Checkliste, `bug.yml`)
- Grep über README, `docs/` (ohne decisions, review, Release-Notes, `staff-request.md`), CONTRIBUTING, SECURITY,
  Templates, SHOTLIST, Nexus und Wiki-Branch `v0.4.0`; Stichproben gegen den Code; Link-Check

**Befund:**
- Korrigiert: Nexus `checklist.md` und `description.bbcode` 0.3.1 → 0.4.0; SHOTLIST H15 ohne „v0.3.0: *CT live*“; Wiki
  `Home` „Applies to v0.3.0“ (auf `master` nie auf 0.3.1 gezogen) → v0.4.0; Wiki `Plan-and-editor` „older files (schema
  1 to 3) still import“ war seit F5 falsch → „Schema 3 files still import; schema 1 and 2 are refused“; Versionsbeispiele
  in Wiki `Troubleshooting` und `Preferences` 0.1.0 → 0.4.0.
- Offen bis R3: SHOTLIST Zustandsabsatz und Hero-Maße (Z. 8, 28), D5-Zeile, Nexus-Checkliste Z. 49.
- Historie, bleibt: `ContractTracker` in Known issues (F3), Migrationssätzen (Wiki FAQ, Installation), Plan- und
  Settings-Format (Schema 2/9, CT-Felder); `holo-v1` in `holo-format.md`, `ship-format.md`, `settings-format.md` (die drei
  Fleetyards-Schiffe, Aufräumen alter Caches); Wiki „Coming from v0.1 or v0.2“, „`…+Space` … after v0.3.1“.
- 0 Treffer: `62 MB`, `onContractGone`, `contracts --raw`, `syncContractTracker`/„Sync from ContractTracker“ außerhalb
  der Migration; „Ctrl, Alt or Win“ nur mit „unless it is an F key“.
- Code-Stichproben stimmen: `HotkeyParser` (Ctrl/Alt/Win außer F-Tasten, F12 und Space reserviert) = Wiki
  `Overlay-basics` („F1–F11 and F13–F24“) = Hinweis `HotkeysViewModel.Hint`; Default `AppSettings.DefaultScanKey` F3;
  `ScanNotices` (`Scanned #n`, `Updated #n · quantities changed`, `Already in plan (#n)`, `Scan #n not applied: …`, `No
  contract in frame`) und `⚠ n of m destinations` (`ShipViewBuilder`) wortgleich in Wiki und README; `--keep-scans`-Ordner
  `%LocalAppData%\SC-CargoPlan\scans` (`ScanPictures`, `AppFolders`) wie README und `bug.yml`.
- Link-Check (`linkcheck.py` aus v0.3.0): 25 Dateien, intern 0 kaputt (einziger Treffer der Platzhalter `media/<file>` im
  SHOTLIST-Konventionstext); Negativtest (falscher Anker, fehlende Datei) meldet 2; extern 20 URLs, alle 200.
- `git diff main -- src tests ships outlines build`: nur `outlines/NOTICE.md` (Textzeile „Not affiliated with …“ ohne
  ContractTracker, aus dem Doku-Branch).

### [x] R2 Release-Notes (13.11.2026)

- `docs/release-notes/v0.4.0.md`: Lead, Download-Absatz, Highlights, Changes, Before you update (settings, plan, kein
  Downgrade, Mission-Dateien, ContractTracker auf F3), Known issues (6 + Link), MIT-Schluss mit `models/NOTICE.md`

**Befund:**
- Vollständigkeit `git log v0.3.1..main --no-merges`: T10.1a–T10.5, B11–B13, K10 kommen vor, soweit sie das ZIP für
  Nutzer ändern. Nicht in den Notes: `--data-dir`, `--scan-probe`, `--measure-frames` (Gate-Werkzeuge), Plan-Schema 1–7
  abgelehnt (kein Release schrieb sie, v0.1.0 schrieb 8), Testumbau (B11, Timing-Kategorie), K10-Hygiene.
- Datenordner seit v0.3.1 gleich (`%LocalAppData%\SC-CargoPlan`, `git grep v0.3.1`), also kein Migrationssatz dazu.
- Zahlen: ZIP lokal 85.214.274 Byte (`publish.ps1 -Version 0.4.0` auf `0f8cd3f`) + 2,1 MB CI-Kompression ≈ 87 MB,
  v0.3.1 veröffentlicht 64.201.711 Byte; Speicher „about 90 MB less“ = D108 Gate 1 (217 vs 128 MB Working Set), „up to
  about 900 MB“ = D108 (773–892 MB nach 70 Scans), README wortgleich; `--keep-scans` 20 Bilder ≈ 2,6 MB = ≈ 50 MB (T10.5).
- Wiki-Lücke: „Coming from v0.3.x“ nannte weder `#1…#n` für CT-Verträge noch die Downgrade-Falle (D106); ergänzt,
  damit Notes und Wiki dasselbe sagen.
- Link-Check der Notes: 0 kaputt, 4 externe URLs 200.

- [x] Notes-Entwurf von Claude.ai freigegeben (Martin, 13.11.) mit K1: „Turn scanning off“ und „the reader doesn't load
  at all“ versprachen zu viel, der Editor-Button lädt den Reader nach (D108) → „Clear the scan key … doesn't load its
  contract reader at start … The editor's Scan button still works and loads the reader when you click it.“ und
  Known issue „… doesn't load at start.“; dieselbe Behauptung auch in README (Known issue, wortgleich) und Wiki
  (`Troubleshooting` Memory-Zeile, `Scanning-contracts` „Turn scanning off“) korrigiert. Kleinigkeit übernommen:
  `plan.json.unreadable-<date-time>` (D106: Datum und Uhrzeit). Nicht übernommen: „Nothing that is already on board
  moves“ → „Crates already loaded stay where they are“; im Highlight zum neuen Vertrag richtig und wortgleich mit dem
  Wiki „Scanning contracts“. Offen im Produkt: der Hinweis im Settings-Fenster sagt weiter „Clear it to turn scanning
  off: …; the editor's Scan button still works.“ – in sich stimmig, kein Fix im Release-Branch.
- Nach B14 (Claude.ai): `plan.json`-Zeile „… when v0.4.0 loads it, and an amber line says so once; …“; Notes, Wiki
  (`Installation-and-first-start`) und Produkt sagen jetzt dasselbe.

### [x] R3 Medien (Martin nimmt auf, CC schneidet und prüft) (13.11.2026)

- E1 Hero, E2 `quickstart-capture-sync.gif`, D10 `prefs-hotkeys.png` (Scan-Zeile), D5 `editor-rows.png` (Scan-Button),
  E6 `legend-markings.png` (`#n`); Bildvergleich `ShipPictureTests` gegen `main` (→ R4, mit den Tests)

**Befund:**
- Vorab-Durchsicht der alten Shots: `prefs-window.png` (D7) zeigte den CT-Abschnitt mit Datenbankpfad und die
  Sync-Zeile → must, neu; `editor-import-plan.gif` (D6) und `delete-mission.gif` (D12) zeigen den Knopf „Sync from
  ContractTracker“, D12 CT-Nummern `#91–96` → bleiben (nice, Martin).
- Aufnahmen Martin (13.11.), alle auf der Hull A: Rohexporte `hero_full.gif` (18,0 s) und
  `quickstart-capture-sync-full.gif` (16,5 s, direkte Fortsetzung mit Vertrag #2), je 1367 × 796 px, 14 fps,
  185/168 MB; liegen in `Documents\Coding\Dotnet\sc-cargoplan-raw\v0.4.0`, nie im Repo.
- Erster Versuch (verworfen, vor dem Push mit dem Ergebnis zu einem Commit zusammengefasst): beide hintereinander mit `mpdecimate` + gifsicle 1.97 `--lossy=100`, 5,98 MB, 34,5 s.
  Martin: „sieht Mist aus“ (Schlieren im Hintergrund und im Overlay) → Fables Rezept (Session 08.11.): zwei Durchgänge
  `fps=8,scale=720:-1,hqdn3d=3:2:9:6`, 96 Farben `stats_mode=diff`, `dither=none`, Ziel < 5 MB. Beide hintereinander
  ergeben 8,6 MB (720) bzw. 6,9 MB (640), also **Hero = Vertrag #1** (Angebot → `Scanned #1` → annehmen →
  `Updated #1 · quantities changed`), **E2 = Fortsetzung mit #2**, gleiches Rezept: Hero **4.368.497 Byte**, 720 × 419,
  144 Frames, 18,0 s; E2 **3.928.600 Byte**, 132 Frames, 16,5 s. gifsicle wird nicht gebraucht.
- Farbe: hellblaue Kisten werden gräulich (gemessen 78,142,178 → 127,136,148). Ursache: die eine Palette des GIFs hat
  neben dem grauen Hintergrund keinen Platz für die kleinen Kistenflächen; 128/160 Farben, `stats_mode=full`, Palette je
  Frame, `format=yuv444p` und ein gewichteter Kistenausschnitt in der Palettenberechnung ändern es nicht genug. Martin:
  „mit leichtem Farbverlust kann ich leben“. Orange, Amber und Text bleiben.
- Sichtprüfung: Standbilder Anfang / Notice / Ende beider GIFs, Legende lesbar, keine Namen/Chat.
- D5 aus `Pictures\Screenshots\Screenshot 2026-11-13 075129.png` übernommen (Scan-Button, kein Sync-Knopf); D7, D10 von
  Martin (D7-Fußzeile `0.0.0-dev+400a3e7…`, wie das alte Bild aus einem Dev-Build).
- `legend-markings.png` steht im Wiki zweimal (E5 Zustände, E6 Markierungen); Martins neues Bild zeigte die
  Testplan-Legende ohne beides → altes Bild zurück (Martin), neu **E7** `legend-scan-headers.png` aus dem letzten
  E2-Frame (Köpfe `#1`/`#2`, `Updated #2`) unter „Mission headers“.
- Doku-Fehler: Wiki `Legend-and-delivery-states` und `Plan-and-editor` sagten, Pläne von vor v0.4.0 zeigten ihre
  CT-Ids (`#47–48`, `#37–38`); seit D107 werden sie `#1…#n` → korrigiert.
- Hero-Bildunterschrift in README, Wiki-Home und Nexus-Liste: „F3 on a contract in the mobiGlas: its crates appear in
  the racks of the MISC Hull A“ (statt „next to the cargo elevator“); Nexus-Rückfall `hero.png` bei 7,5 s per `select`
  (ein Sprung mit `-ss` in ein GIF mit `diff_mode=rectangle` kann Teilbilder liefern).
- SHOTLIST: Zustand, Rezept (Fable, mit Preis Farbe), D5/D7/D10/E1/E2 neu beschrieben, E7; 31 Shots, 15 must.
- Link-Check: README, Nexus-Checkliste, Wiki intern 0 kaputt; SHOTLIST online: 2 externe URLs 200 (neu der
  gifsicle-Link), einziger Treffer wieder der Platzhalter `media/<file>`; jedes referenzierte Wiki-Bild vorhanden,
  keins unbenutzt.

### [x] R4 Vor dem Tag, Merge, Tag, Nachmessung (13.11.2026)

- `publish.ps1 -Version 0.4.0` (ZIP ≈ 85 MB lokal, `models/v5/` 4 Dateien, `models/NOTICE.md`, keine SQLite-DLLs), Probe a
  (leeres Profil) und b (v0.3.1-Profil), `dotnet test` im sauberen Worktree, `gh run list -L 3`
- Martin: Abnahme → `--no-ff` nach `main`, Tag `v0.4.0`; veröffentlichtes ZIP nachmessen

**Befund, erster Lauf (13.11., Stand `4652ae3`):** ZIP 85.214.246 Byte, Tests grün; die Upgrade-Probe b fand die
verschwundene Plan-Notice → Stopp, **B14** vor dem Tag (Regel des Tasks: kein Produktcode im Release-Branch). Der erste
Probe-b-Lauf verlor „loaded“: die Settings-Fixture nennt die Hull B, der Plan war für die Hull A, also neu geplant
(gewollt, `plan-format.md` „Planned anew … no longer loaded“); Probe b danach aus einem Schema-9-Plan auf den heutigen
Racks (`probe-b2-src`: v0.3.1-Settings + Plan mit 3 Kisten `loaded`, 6 `delivered`).

**Befund, zweiter Lauf (13.11., Stand `d39dffe`, mit B14 aus `main` `6426211`):**

| Prüfung | Soll | Ist | |
|---|---|---|---|
| `publish.ps1 -Version 0.4.0` | ≈ 85 MB, 0 Warnungen | 85.214.432 Byte, 0 Warnungen | ✓ |
| SHA256 | gleich `.sha256` | `167ABA8F…9F7FB0D4`, gleich | ✓ |
| Einträge | wie v0.4.0-Entwurf | 381 (v0.3.1: 368, + OCR, − SQLite) | ✓ |
| `models/` | `v5/` 4 Dateien, NOTICE | 4 + `NOTICE.md` + `onnxruntime-ThirdPartyNotices.txt` | ✓ |
| `ships/`, `outlines/` | 105 + 1 Modul, 2 Dateien | 106 JSON; `ship-outlines.json`, `NOTICE.md` | ✓ |
| SQLite | keine | keine `*sqlite*` | ✓ |
| Runtime, Version | 10.0.14, `0.4.0+…` | 10.0.14 beide; `0.4.0+d39dffe…` | ✓ |
| `dotnet test`, sauberer Worktree | grün | Core 1770, App 1823, Import 280, Ocr 34 | ✓ |
| Probe a, leeres Profil (ZIP-Exe) | F3 ab Werk, kein Schreiben | Reader wärmt beim Start (nur mit Scan-Taste), nur `cargoplan.log`; Hull A leer, „No plan …“ | ✓ |
| Probe b, v0.3.1-Profil, Start + Verschieben | beide Hinweise bleiben, Marken bleiben | Amber-Zeile „hotkey Ctrl+Alt+O … removed …“ und „4 contracts … #1–#4 now.“ nach dem Verschieben; 3 `loaded`, 6 `delivered`, Schema 10; `.bak` = Schema 2 mit `Ctrl+Alt+O` | ✓ |
| Probe b, zweiter Start | keine Hinweise | keine Amber-Zeile, Log-Zeile der Konvertierung einmal | ✓ |
| `ShipPictureTests` gegen `main` (`6426211`, eigener Worktree, 98 Hologramme aus `sc-ship-outlines/holo/holo-v2`) | 0 verschieden | **1388 identisch, 0 verschieden, 0 fehlend**, Placement-Zahlen auf 0 von 106 verschieden; `git diff main -- src tests ships build` leer, `outlines/` nur die NOTICE-Textzeile | ✓ |
| Probe a über dem Spiel (Martin, 13.11.) | Hotkeys-Zeile F3 + Hinweis, Scan → `Scanned #1`, `scans.log` mit `Added #1`, Fußzeile 0.4.0 | „funktioniert“; `cargoplan.log` des Laufs nur „OCR ready … 129 → 216 MB“ (die Scans stehen in `scans.log`) | ✓ |

**Befund (nach dem Push):**
- Merge `--no-ff` `140be1f` (CC, nach Martins Abnahme), Inhalt gleich `release/v0.4.0`. Martin: `main` gepusht, CI grün
  (2 min 32 s), Tag `v0.4.0` annotiert auf `140be1f`, Release-Workflow grün; Wiki `v0.4.0` → `master` gepusht
  (`19732be`).
- Release `v0.4.0`: kein Pre-release, kein Draft, `releases/latest` → `v0.4.0`, veröffentlicht 13.11.2026 11:38 UTC.
  Notes-Text gleich `docs/release-notes/v0.4.0.md` (nur eine Leerzeile am Ende mehr).

| Prüfung | Soll | Veröffentlichtes ZIP | |
|---|---|---|---|
| SHA256 gegen `.sha256` | gleich | `064EDEEE…08823B87`, gleich | ✓ |
| Größe | ≈ 87 MB (lokal + ≈ 2,1 MB CI-Kompression), Notes „about 87 MB“ | 87.633.602 Byte (lokal 85.214.432, +2,4 MB) | ✓ |
| Einträge | wie lokal | 381, Eintragsliste gleich dem lokalen `publish.ps1 -Version 0.4.0` | ✓ |
| Inhalt | 106 Schiffs-JSON, `models/v5/` 4, keine SQLite | genau so | ✓ |
| `CargoPlan.Cli --version` | `0.4.0+140be1f…` | `0.4.0+140be1ffa5f9e3ed5dfa5bcd8d39f0ae3ceb55b7` | ✓ |
| Runtime | 10.0.14 | `Microsoft.NETCore.App` und `WindowsDesktop.App` 10.0.14 | ✓ |
| Start aus dem heruntergeladenen ZIP (SmartScreen-Weg) | Martin | – | offen |

### [x] R5 Nachlauf (geschlossen in K12, 19.11.2026)

- [x] Wiki `v0.4.0` → `master` (Martin, 13.11., `19732be`)
- [x] `REL:`-Commit: CLAUDE.md ZIP-Größe, R4-Befund nach dem Push (`0642b98`, Martin gepusht)
- [x] Martin (13.11.): Start aus dem heruntergeladenen ZIP (SmartScreen „More info → Run anyway“), Release-Seite im
  Browser
- [x] Branches lokal gelöscht (CC, `git branch -d`): `release/v0.4.0`, `docs/v0.4.0-user-docs`, `fix/b14-plan-notice`,
  Wiki `v0.4.0`
- [x] Remote gelöscht (Martin, 13.11.): `docs/v0.4.0-user-docs`, `feat/t10-5-scan-settings`, Wiki `v0.4.0`; remote
  bleiben `main` und der alte `release/v0.3.0`
- [x] Nexus mit 0.4.0 nach `docs/nexus/checklist.md`, frühestens 16.11. (Staff-Frist, T9.3) – Nexus-Seite am 16.11. mit
  v0.5.0 veröffentlicht; der v0.6.0-Upload liegt in der Nexus-Quarantäne, Martin lässt ihn liegen: Nexus ist nur
  Schaufenster, Updates laufen über GitHub/Velopack (K12)
- [x] Ankündigungen (Entwürfe Claude.ai, Martin verschickt) – v0.4.0-Ankündigung entfallen, v0.5.0/v0.6.0 folgten;
  Ankündigungen laufen außerhalb von TASKS.md (K12)
- [x] Rohaufnahmen `Documents\Coding\Dotnet\sc-cargoplan-raw\v0.4.0` (354 MB, `hero_full.gif`,
  `quickstart-capture-sync-full.gif`): bleiben (Martin), außerhalb des Repos
- Übergabe an K11 (TASKS.md-Archivierung) → Task K11 in „Backlog II (19.11.2026)“

