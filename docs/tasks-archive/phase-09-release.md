# Archiv: Phase 9 – Release

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Phase 9 – Release (nach Phase 8)

**Warum:** T7.5a–c (README, Wiki, Medien) zeigen die 2D-Ansicht; nach D83 stimmen Begriffe und Bilder nicht mehr. Der
Release-Teil von T7.5 (T7.5d) zieht hierher. T7.5a–c bleiben unter ihrer Nummer.

### [x] T9.1 Textabgleich auf der 3D-Ansicht (31.10.2026, abgenommen 31.10.) – Branch `release/v0.1.0`
- Textdurchsicht auf durch D83 **falsch** gewordene Stellen: README, Wiki-Seiten (v. a. Ship-view, Preferences, Overlay-basics),
  `docs/settings-format.md`, Settings-Tooltips (Zoom-Grenzen = Zellgröße in der Zielebene, Kamera/Orbit/Pan, Reset, Abschnitt
  „Ship view“)
- Medien bleiben (D85), Hinweis in `docs/shots/SHOTLIST.md`, Auslöser zum Nachdrehen nur in der Backlog-Zeile
- Known issues aus Phase 8 (T8.7) ins Wiki (Troubleshooting) und in den Entwurf `docs/release-notes/v0.1.0.md`

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
- Medien inklusive Hero-GIF bleiben vom Stand vor D83 (D85, D83-Addendum); Auslöser-Wortlaut einmal im Backlog, SHOTLIST
  und D85 verweisen darauf.
- Neuer Branch `release/v0.1.0` ab `main`; `docs/release-v0.1.0` war vollständig in `main` und `origin/main` (9364ca0),
  lokal gelöscht, remote löscht Martin. Kein Merge nach T9.1: `--no-ff` nach der Abnahme der Notes in T9.2, vor dem Tag.
- Tooltip „Hologram opacity“ „behind the racks“ → „around the racks“ als eigener Commit (Text, kein Verhalten, kein Test
  pinnt ihn).
- Wiki `Ship-view`: der Satz zu langen Schiffen war schon vor D83 falsch („dimmed until you zoom out“): leere Racks langer
  Schiffe (`IsLong`: mehr als doppelt so lang wie breit) sind immer gedimmt (`ShipScene3D.cs:317`), der Start zeigt die
  beladenen Racks und den Bug (`CameraFit.cs:67`).
- README bleibt: Ansicht, Settings und Known issues stehen seit T7.5a im Wiki; Grep belegt, dass dort nichts falsch ist.
- Known issue „Planzeit bis ~4 s“ entfällt (B4, D66: Freelancer-Lastfall 2,3 ms). Retaliator: keine Datei in `ships/`,
  also „fehlt“, nicht „ohne Gittermaße“.

**Befund:**
- Downgrade geprüft am Code: rc.1 (6bbc115), rc.2 (1bfd3ba, beide am Commit ihrer Notes; die Tags gibt es nicht mehr) und
  rc.3 (Tag 01770ab) lesen Schema 2 mit `UnmappedMemberHandling.Disallow` und kennen weder `shipView` noch `shipOutline`.
  0.1.0 schreibt beide immer: **jede** von 0.1.0 gespeicherte `settings.json` ist für rc-Builds unlesbar (Defaults und
  Meldung, `.bak` erst beim nächsten Speichern), nicht erst eine mit `distance`/`target` (D83-Addendum T8.1).
- Netz: einziger `HttpClient` in `HoloSource.cs`, Basis `HoloRelease.BaseUri` (Release `holo-v1` dieses Repos), kein
  Aufruf von Fleetyards; sonst nur der Nexus-Link per Klick im Browser.
- Schiffe: 97 Dateien in `ships/` und ein Modul, der Loader lädt 98 Einträge ohne „Skipped“; README und Wiki stimmen.
- „New since rc.3“ aus `git log v0.1.0-rc.3..main` (318 Commits, 15 Merges): rc.3 war der Stand von T6.5 mit der Hull A
  allein; seitdem T7.0–T7.5, B1–B6, K1–K7, Sync-Gone, Sync-Net, Holo-Pump, T8.0–T8.7.

**Akzeptanz:**
- [x] Wiki: Ship-view, Preferences, Ships-and-cargo-grids, Troubleshooting (Known issues) folgen D83/D84 (Wiki `4d1dba0`,
  lokal; Push am Tag des Tags `v0.1.0`)
- [x] `docs/settings-format.md` folgt D83/D84 (Enum-Wert `"none"` unverändert)
- [x] Tooltip „Hologram opacity“ folgt D83
- [x] SHOTLIST-Hinweis, D85 und D83-Addendum, Backlog-Zeile
- [x] Entwurf `docs/release-notes/v0.1.0.md` mit Highlights, Known issues (je mit Fundort), Credits
- [x] Grep-Prüfung (README, Wiki, `docs/*.md`, XAML ohne Treffer), Link-Prüfung (0 kaputt, Negativtest schlägt an),
  `dotnet build` 0 Warnungen, `dotnet test` grün (Core 1.497, Import 141, App 1.533)
- [x] Diff-Durchsicht durch Martin/Claude.ai (31.10.2026); `origin/docs/release-v0.1.0` von Martin gelöscht
- Kein Merge nach T9.1: `--no-ff` nach `main` nach der Abnahme der Notes in T9.2, vor dem Tag

### [x] T9.2 Release (= T7.5d) (31.10.2026, abgenommen 02.11.)
- Release-Notes `docs/release-notes/v0.1.0.md`, Tag `v0.1.0`, rc-Releases und -Tags löschen (D49)
- Notiz aus T9.1: `gh` liegt nicht im PATH (`C:\Program Files\GitHub CLI\gh.exe`); das Holo-Asset ist ein eigenes Release
  `holo-v1`, nicht am App-Release; Wiki-Push am Tag des Tags `v0.1.0`

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
- R1 Retaliator-Zeile bleibt ohne Grund (kein Beleg im Repo). R2 Schiffszahl bleibt 97: die Auswahl hat 98 Einträge,
  der 98. ist die Aurora Mk II mit TS-Modul (`ModuleDto.cs:30-34`, Variante, kein eigenes Schiff); Notes, README und Wiki
  sagen 97. Highlights bleiben wortgleich zur README; N1 Drop-Satz, N2 „if you choose“, N3 Link auf „Check the download“.
- Kein Versions-Bump: `release.yml` nimmt die Version aus dem Tag, `0.0.0-dev` in `Directory.Build.props` bleibt
  Platzhalter für Entwickler-Builds (D49).
- M6 (self-contained statt framework-dependent wie M5) **vor dem Tag** mit dem lokalen ZIP aus `publish.ps1 -Version 0.1.0`
  (gleiches Skript, gleiche Schalter wie CI); das veröffentlichte ZIP bekommt die Funktionsprüfung. Gate je Ziel: Median
  spät ≤ M5-Median + 2 pp, Vorschau p95 ≤ 16 ms.
- rc-Notes bleiben (D49-Addendum, keine neue D-Nummer). `release/v0.1.0` nach der Nachprüfung lokal und remote löschen.
- Wiki-Push entfällt: `origin/master` steht schon auf `ef6b9f4`.

**Befund (vor dem Tag):**
- Version überall `AssemblyInformationalVersion`: Settings-Fußzeile, CLI `--version`, Holo-User-Agent (ohne `+…`),
  Messlog-Kopf; kein Fenstertitel, kein Versionskopf in `cargoplan.log`.
- Remote: Releases `v0.1.0-rc.3` und `holo-v1` (beide Pre-release), Tags dieselben; `releases/latest` → `/releases`.
  README zeigt schon auf `releases/latest`.
- `holo-v1`: 120 Assets = 118 `.holo` + `manifest.json` (listet 118) + `NOTICE.md`; URL = `HoloRelease.BaseUri`.
- Daten liegen in `%LocalAppData%\SC-CargoPlan` (nicht `%AppData%`, dort nur die CT-DB).
- Link-Check der Notes (`linkcheck.py` aus T9.1): 0 kaputt, Negativtest mit zwei falschen Ankern schlägt an; extern
  200 außer Nexus (403 an curl, Bot-Schutz; Prüfung im Browser bei der Nachprüfung).
- Reproduzierbarkeit: Hash lokal ≠ CI erwartet (D49); verglichen werden Eintragsliste und `--version`.
- Merge `--no-ff` 6ae6444; Tests auf dem Merge-Commit = `main` vorher (Core 1.497, Import 141, App 1.533), Debug und
  Release 0 Warnungen. `publish.ps1 -Version 0.1.0`: ZIP 62,1 MB, 360 Dateien (die 358 aus T7.4 plus
  `ships/salvation.json` und `ships/modules/ts_module.json`), `CargoPlan.Cli --version` = `0.1.0+6ae6444…`.
- **M6** (`m6.ps1`, Martin, 31.10.2026 16:54–17:04, SC vorn, Cursor frei; lokales ZIP, self-contained, Runtime 10.0.13;
  `--measure-frames <Ziel> --over-game`, 30 s je Lauf, drei Runden, Median der drei Läufe). Ein erster Durchgang um
  16:47 lief ohne SC (`empty` 14 % spät) und ist verworfen.

  | Ziel | M5 (T8.7) spät | M6 Läufe spät | M6 Median | Gate ≤ M5 + 2 pp |
  |---|---|---|---|---|
  | `empty` | 0,1 % | 1,0 / 0,3 / 0,3 | 0,3 % | ✓ |
  | `hull_c` | 0,2 % | 0,2 / 0,1 / 0,2 | 0,2 % | ✓ |
  | `hull_c_states` | 2,9 % | 0,4 / 0,7 / 0,4 | 0,4 % | ✓ |
  | `hull_c_drag` | 11,8 % | 11,9 / 11,5 / 12,1 | 11,9 % | ✓ |
  | `hull_a_outline` | 0,2 % | 0,2 / 0,2 / 0,1 | 0,2 % | ✓ |
  | Vorschau `hull_c_drag` p95 / max | 5,23 / 20,3 ms | p95 1,80 / 1,92 / 1,83, max 13,4 / 13,7 / 10,8 ms | 1,83 ms | ≤ 16 ms ✓ |

  path keys 25 / 27 / 26, „drag held yes“ in allen drei; Hit p95 ≤ 0,002 ms. Danach Tag `v0.1.0` (annotiert) auf 6ae6444.

**Befund (nach dem Push):**
- Release-Workflow grün (2 min 41 s), CI auf `main` grün; Release `v0.1.0` kein Pre-release, „Latest“,
  `releases/latest` → `tag/v0.1.0`; Assets ZIP 64,2 MB und `.sha256`; Download gegen `.sha256` OK, `--version` =
  `0.1.0+6ae6444…`, 360 Einträge, alle mit `/`.
- **Eintragsliste weicht in einem Namen ab:** das CI-ZIP bringt die Runtime **10.0.14** mit, das lokale (M6) 10.0.13
  (`mscordaccore_…`, `coreclr.dll`, `wpfgfx_cor3.dll`, `runtimeconfig.json`). Der Runner holt per `setup-dotnet 10.0.x`
  das neueste SDK, lokal SDK 10.0.402. Damit hält D49 „a local run gives the released artifact's content“ nur bei
  gleichem SDK-Stand. Self-contained mit 10.0.14 ist nicht über dem Spiel gemessen; M5 (10.0.14, framework-dependent)
  und M6 (self-contained, 10.0.13) sind beide grün. Martin bei der Abnahme: kein weiterer M6-Lauf, das veröffentlichte
  ZIP ist heruntergeladen und läuft.
- Veröffentlichtes ZIP (Runtime 10.0.14) nicht gemessen, M6 lief auf 10.0.13; siehe B7/D86.
- Nachprüfung (Martin, 02.11.2026): alle Punkte der Checkliste grün – Release-Seite (Titel, Latest, Notes, Assets,
  Nexus-Link im Browser), Hash, Start nach Wiki „Installation and first start“ mit frischem Profil, Version in der
  Fußzeile, Holo-Download aus `holo-v1`, Abschalten.

**Akzeptanz:**
- [x] Notes-Endfassung (R1, R2 unverändert; N1–N3), Link-Check 0 kaputt, Negativtest schlägt an
- [x] `dotnet build` 0 Warnungen, `dotnet test` grün, Zahlen = `main`
- [x] M6 über dem Spiel grün gegen M5
- [x] `git merge --no-ff` nach `main` (6ae6444), Tag `v0.1.0`, Release über `release.yml` grün, veröffentlichtes ZIP läuft
- [x] rc.3-Release und -Tag gelöscht (remote und lokal), `release/v0.1.0` gelöscht, dazu die gemergten Remote-Branches
  `feat/k2-plan-editor`, `feat/k3-planner-rules`, `feat/k5-data`, `feat/k6-elevator-flow`, `spike/s8-3d-finding`
  (Martin, 02.11.2026); remote bleiben die Releases `v0.1.0` (Latest) und `holo-v1`

### [x] T9.3 Nexus-Mods-Seite (= T6.6) – Branch `docs/nexus`

**Abgehakt in K12 (19.11.2026, R1):** Nexus-Seite seit 16.11.2026 online mit v0.5.0,
<https://www.nexusmods.com/starcitizen/mods/52>; v0.6.0 per T12.6a, README nennt Nexus unter Install. Die offenen Punkte
unten sind damit erledigt oder gegenstandslos.
- Texte und Checkliste versioniert in `docs/nexus/`: `summary.txt`, `description.bbcode`, `checklist.md`,
  `staff-request.md`; die Beschreibung aus README, `docs/release-notes/v0.1.0.md` und Wiki abgeleitet
- Upload = exakt das GitHub-Asset `SC-CargoPlan-v0.1.0-win-x64.zip` (gleicher SHA256), Version `0.1.0`
- D87, `CONTRIBUTING.md` Abschnitt „Release“, README-Link nach dem Launch
- Martin legt die Seite an (Account, Upload, Klicks); CC trägt URL und Datum nach

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
- Nexus-Regeln gelesen statt angenommen: Netzzugriff nur, wenn „crucial“, der Holo-Download ist optional → vor dem
  ersten Upload Anfrage an den Nexus-Support (`staff-request.md`), Upload nach Zustimmung oder nach Fristablauf
  16.11.2026 ohne Antwort (14 Tage statt offenes Warten, D87-Addendum); eine Ablehnung stoppt T9.3, kein
  Nexus-Sonder-Build.
- Anfrage, Abschnitt ContractTracker mit Belegen, ohne Wertung des Autors: Direktnachricht an den Autor am 20.10.2026,
  keine Antwort; Discord-Link auf mods/34 ungültig; optional, read-only auf die lokale Datei, kein Netz; bei Einwand
  des Autors gehen die CT-Bezüge von der Nexus-Seite. Silhouetten-Absatz nennt die schriftliche Zustimmung von
  Fleetyards (Torlek, 23.10.2026).
- Netzzugriff in der Anfrage genau wie im Code: Default-Look Outline, dann kein Request; erst ein Hologramm-Look lädt
  per GET aus `holo-v1`, danach je Start und Schiffswechsel das Manifest höchstens einmal pro Lauf und nicht, solange
  das gecachte jünger als 24 h ist; Schalter aus → kein Request. Code-Links auf `fd75313`.
- `outlines/` im ZIP (aus Renders von CIG-Schiffen, nicht MIT, D55), Befund außerhalb des Auftrags: offengelegt in
  Anfrage, Beschreibung und Permissions; Asset use „ask/credit“, Martin entscheidet an der Maske.
- Der CT-Autor wird nicht erneut gefragt; die Beschreibung sagt „only reads its database“.
- KI-Tags „AI-Generated Content“ und „AI Media“, dazu ein Satz unter Shout outs.
- Keine Donation Points, kein Text dazu. CT nicht im Requirements-Feld (Nexus kennt kein „optional“).
- Kategorie Miscellaneous (die einzige für Star Citizen), Tags Utilities for Players, Quality of Life, User Interface.
- Maske nach Martins Screenshots: fünf Schritte General → Media → Files → Requirements → Permissions, Summary
  350 Zeichen, Beschreibung als BBCode über den [ ]-Button; `description.bbcode` in den fünf Vorlagen-Blöcken
  (Description, Installation instructions, Main features, Requirements, Shout outs), kein `[font]`.
- Checkliste versionsneutral (`$version`), Hash in den Befund statt in die Checkliste; Größenvergleich in Byte, MB und
  MiB; nach Publish Hash und Größe des Nexus-Downloads.
- `holo-v1` in der Beschreibung nur als Name, kein Link (Regel gegen Links auf nicht hostbare Dateien).
- Hero-GIF als Bild 1; abgelehnt oder ohne Bewegung → Einzelbild per ffmpeg nach `docs/nexus/hero.png`.
- README nach dem Launch: Nexus als Klammerzusatz in Install Schritt 1, GitHub bleibt erste Quelle.

**Befund:**
- File Submission Guidelines gelesen 02.11.2026 (Stand 27.10.2026). Nexus-Seiten (Wiki, News, Mod-Seiten) antworten
  Skripten mit 403; Kategorien, Tags und Requirements aus der öffentlichen GraphQL-API (`api.nexusmods.com/v2/graphql`,
  Star Citizen = Spiel 2246): 34 Mods, alle Miscellaneous; `ModRequirement` hat `notes`, kein Optional-Flag; CT
  (Mod 34): Autor Yuiki, Uploader uctv. Ein SC-Mod mit KI-Übersetzung trägt bereits „AI-Generated Content“.
- BBCode-Syntax nur aus einer Sekundärquelle (BUTR/Converter.MarkdownToBBCodeNM); die Vorschau im Editor ist
  Checklist-Schritt.
- Asset `SC-CargoPlan-v0.1.0-win-x64.zip`: 64.164.865 Byte (64,2 MB / 61,2 MiB), SHA256
  `ad8330f9a579a935fb3bba43d3c8291e908b4f882ff71e687560c352f8786a97`. Die „63,6 MB“ aus der Freigabe passen zu keiner
  der beiden Einheiten.
- Netzcode: einziger `HttpClient` in `HoloSource.cs:67` (Grep über `src`).
- Link-Check `description.bbcode` (Skript nach `linkcheck.py`, T9.1, um `[url=…]` erweitert): 13 Links, 0 kaputt,
  Wiki-Anker gegen die lokale Wiki-Kopie, Nexus-Link im Browser; Negativtest (falscher Anker, tote URL) meldet 2.
  Code-Links der Anfrage (7) und Guidelines: 200.
- Quellentreue (Skript: jeder Satz wörtlich in README, Notes oder Wiki?): ohne Quelle nur die Überschriften, die
  D87-Sätze (Files tab, `.sha256` des GitHub-Release, Source code, Bugs auf GitHub, KI-Satz), der D63-Satz
  (`decisions.md:714`) und FAQ:11 ohne die Klammer „(see …)“. `summary.txt` = README:5-6, 167 Zeichen.
- CT-Discord (02.11.2026): die Beschreibung von mods/34 (GraphQL-API, zuletzt geändert 02.06.2026) enthält zweimal
  `discord.gg/9RNUXAdVn`; Martin im Browser „Ungültig oder abgelaufen“, Discord-API `invites/9RNUXAdVn` → 404
  „Unknown Invite“ (Code 10006). Read-only belegt: `ContractTrackerReader.cs:108` (`SqliteOpenMode.ReadOnly`), Link
  auf `fd75313`.
- Quellen im Repo nennen es anders: `contracttracker-schema.md:7` „asked of the author in the Nexus Discord on
  2026-10-20 09:26“ (Anfrage sagt neutral „by direct message“); Fleetyards-Zustimmung steht in T7.4b Gate 1 als
  „Mail von Marten (Fleetyards)“, D63 zitiert Marten mit „Grauzone, keine offizielle Freigabe“. Martin, 02.11.2026:
  Torlek ist Marten; die Zustimmung gilt der Nutzung aller Inhalte der Fleetyards-API, also auch den Silhouetten
  (aus den Renders der API, `outlines/NOTICE.md`).

**Akzeptanz:**
- [x] `docs/nexus/*`, D87, CONTRIBUTING, TASKS; Link-Check 0 kaputt, Negativtest schlägt an
- [x] Anfrage an den Nexus-Support gesendet und beantwortet (Datum, Wortlaut) oder Frist 16.11.2026 ohne Antwort
  abgelaufen – Frist 16.11. erreicht, Seite an dem Tag veröffentlicht (K12)
- [x] Nexus-Seite online mit dem ZIP des GitHub-Releases (gleicher SHA256 und gleiche Größe des Nexus-Downloads),
  Permissions passend zu MIT, CT-Credit sichtbar; URL und Datum – `mods/52`, 16.11.2026 (v0.5.0); CT-Credit
  gegenstandslos, ContractTracker ist seit v0.4.0 raus (`description.bbcode` ohne Treffer)
- [x] README: Nexus als zweite Quelle – Install, Schritt 1 (Merge `6899470`)
- [x] `git merge --no-ff` nach `main` – `5c7f7cb` „Merge docs/nexus“

### ~~[ ] T9.4 Fleetyards-Verlinkung~~ gegenstandslos seit D94 (gestrichen in K10)

---

