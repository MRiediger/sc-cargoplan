# Archiv: Release v0.7.0

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 20.11.2026 (Release v0.7.0, R4); Index und offene Tasks stehen dort.

## Release v0.7.0 – Branch `release/v0.7.0`

**Warum:** Seit v0.6.0 (Tag auf `b2dc6d6`) liegen auf `main` (`f40109d`, CI grün, Run 44743066381) F11/K12 (Linux-CLI,
D115), K11, B16, B17, F10a (D116) und F10b (D117). plan.json 11 und Missions-JSON 5 plus neue Funktionen → **v0.7.0**,
nicht 0.6.1. Ablauf: R1 Textabgleich → R2 Notes (Freigabe Backlog-Chat, einzeln vor dem Tag) → R3 Merge, Tag (Martin),
Nachmessung → R4 Nachlauf. Kein Produktcode im Branch; ein Fund wird Fix-Branch ab `main` und verschiebt den Tag.
Erstes Release mit dem Linux-Asset (`release.yml` lädt `out/release/*`, seit K12 mit Tarball und `.sha256`).

**Befund vor dem Plan (20.11.2026):**
- `git log v0.6.0..main`: 61 Commits, davon 9 Merges. Code-Diff nur Core-Planer und -Formate, App-Editor (zwei
  Choices, Tooltip), Cli `Program.cs`, `publish.ps1`, `check-cli-tarball.ps1`, `ci.yml`, `release.yml`.
- Ship-data-Pin `ships-2026.11.1` = Latest im Shipdata-Repo → kein Pin-Bump, im Netz-Gate **keine** Ship-data-Zeile.
- Downgrade: v0.6.0 liest plan.json ≤ 10 und Missions-JSON ≤ 4; mit D106 legt v0.6.0 eine plan.json 11 beim ersten
  Speichern als `plan.json.unreadable-<date-time>` beiseite und startet leer. settings.json bleibt Schema 3 (F10a/F10b
  ändern die Settings nicht) → kein Settings-Satz.
- Wiki: `f10a-lanes` (mit B17 und F10b) ist schon in `master` und gepusht (`2b32efd`, 20.11., Martins Wunsch),
  `b17-section-block` damit auch. Offen nur `k12-ship-reports` (`06b4004`) und `b16-legend-order` (`0386c22`), beide per
  `git merge-tree` konfliktfrei. Remote steht noch `origin/f10a-lanes`. Veraltet: `Home.md:5` „Applies to v0.5.0“,
  `_Footer.md` v0.6.0, `Overlay-basics.md:63` Beispiel `v0.6.1`.
- Backlog: vier Wiki-Branch-Zeilen (k12, b16, b17, f10a), nicht drei.
- TASKS.md, F10b: eine kaputte Zeile `keine„keine Bahnen“ weglassen …` (Rest des in `698a87e` ersetzten Punkts „Offen
  für Martin“), keine Abnahme-Zeile (nur die Commit-Message).
- README `:34` „Releases after v0.6.0 carry …“; Nexus `checklist.md:25` `$version = '0.6.0'`, `description.bbcode:36`
  Beispiel 0.6.0; `summary.txt` ohne Version und Funktionen.
- Prüfskripte: `check-portable.ps1 -Zip -PublishDir -Version`, `check-cli-tarball.ps1 -Tarball -PublishDir -Version`;
  gegen die heruntergeladenen Dateien mit dem `PublishDir` des lokalen Packs auf dem Merge-Commit.

**Plan (20.11.2026):**

1. **R1 Textabgleich**
   - TASKS: F10b-Zeile ersetzen (K2); Backlog II wortgleich nach `docs/tasks-archive/backlog-ii.md` (Kopf wie K11),
     Index-Zeile.
   - README `:34` → ab v0.7.0, `ships --version` in die Klammer.
   - Nexus: `checklist.md` `$version = '0.7.0'`; `description.bbcode` Beispiel 0.7.0, eine Main-features-Zeile zum
     Layout (eine Seite zuerst), Linux-Hinweis (Kommandozeile ohne Overlay, separater Download auf GitHub).
   - Wiki: `k12-ship-reports` → `master`, dann `b16-legend-order` → `master` (je `--no-ff`, lokal); Branch `v0.7.0` ab
     `master`: Installation „Coming from v0.6.0“ (Formate, Downgrade-Falle D106, plan.json kopieren oder `--data-dir`),
     Home „Applies to v0.7.0“, `_Footer` v0.7.0, Overlay-basics-Beispiel `v0.7.1`. Konflikte zeigen, nicht lösen.
   - Grep über README, CONTRIBUTING, `docs/` (ohne decisions, review, release-notes, tasks-archive), Templates, Nexus,
     Wiki nach `0.6.0`, `v0.6`, Schema-Nummern; UI-Wortlaute (Sides, Side overflow, ihre Werte, die neuen Notices) per
     Grep aus `src/`; Link-Check.
2. **R2 Release-Notes** `docs/release-notes/v0.7.0.md` (Aufbau wie v0.6.0, eigener Commit, einzeln zur Freigabe):
   Updating · Layout: one side first (Sides, Side overflow; Bahnen auf flachen Racks ab 4 breit; Racks nach Seite auf
   Schiffen mit Racks links und rechts; Wait bei Plan/Re-plan, ein Scan legt, wenn Platz ist – F10a-P12; Sektionen
   belegen ganze Reihen – P16) · Legend (B16; Editor ↑/↓ verschiebt gescannte Missionen nicht mehr) · Sections (B17) ·
   Linux (CLI-Tarball, `plan`, `scan <png>`, `ships --version`; kein Overlay, kein Update-Kanal, Schiffsdaten des
   Release) · Files (plan.json 11, Missions-JSON 5, alte laden mit Defaults) · Known issues (Downgrade auf v0.6.0 nach
   D106, unsigniert, gültige v0.6.0-KIs) · Schiffsdaten-Fehler als Issue · Fuß wie v0.6.0. Keine Namen von
   Linux-Nutzern. Vollständigkeitstabelle `git log v0.6.0..main` hier im Befund.
3. **R3 Merge, Tag, Nachmessung** (nach Notes-Freigabe): Draft-PR nur für CI → grün → Merge `--no-ff` lokal → auf dem
   Merge-Commit `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` im sauberen Worktree,
   `./build/publish.ps1 -Version 0.7.0 -DeltaBase github` (Portable + Linux-Tarball, beide Prüfungen) → Martin pusht
   `main` → CI grün → **Martin taggt `v0.7.0`** → `release.yml` → Release-Prüfung (CC) als Tabelle: Latest, Body =
   Notes, 7 Assets (ZIP, `.sha256`, Full-, Delta-nupkg, `releases.win.json`, Linux-Tarball, `.sha256`), SHA beider,
   Feed 0.7.0 voll + Delta, `check-portable.ps1` und `check-cli-tarball.ps1` gegen die heruntergeladenen Dateien,
   Runtime und Cli-Version = lokales Pack (B7), Größen → CLAUDE.md als `REL:`-Commit nach dem Tag. Dann merged CC Wiki
   `v0.7.0` → `master` (`--no-ff`), Martin pusht.
4. **R4 Nachlauf:** Netz-Gate (Martin); WSL-Probe des Tarballs; `release/v0.7.0` lokal und remote löschen;
   Wiki-Branches `k12-ship-reports`, `b16-legend-order`, `b17-section-block`, `f10a-lanes` (+ `origin/f10a-lanes`),
   `v0.7.0` löschen; die vier Backlog-Zeilen „Wiki-Branch …“ streichen; Nexus-Upload (Martin, optional); Nachricht an
   den Linux-Nutzer (Martin); dieser Abschnitt ins Archiv (`release-v0.7.0.md`, Index-Zeile).

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:**
- P1 Wiki: `f10a-lanes` und `b17-section-block` sind schon in `master` (gepusht 20.11.); gemergt werden nur
  `k12-ship-reports` → `b16-legend-order`. Das Wiki beschreibt Sides seit 20.11. vor dem Release – nicht rückholbar.
- P2 „Coming from v0.6.0“, Home, Footer und Overlay-basics-Beispiel auf eigenem Wiki-Branch `v0.7.0`, Merge am Tag
  (Muster T12.6a).
- P3 Vier Backlog-Zeilen statt drei (b17 dazu); Löschen inkl. `origin/f10a-lanes`.
- P4 Kein Pin-Bump (Pin = Latest), Netz-Gate ohne Ship-data-Zeile.
- P5 Downgrade-Satz nach D106 (beiseitegelegt statt überschrieben), kein Settings-Satz (Schema 3 unverändert).
- P6 `summary.txt` bleibt unverändert.
- P7 Draft-PR nur für CI vor dem Merge (Lehre V5/T12.6a).
- P8 Sieben Release-Assets statt fünf; beide Prüfskripte gegen die Downloads.

**Freigabe 20.11.2026 (Backlog-Chat):** P1–P8 ratifiziert. **K1:** keine Ko-fi-Zeile in den Notes (nicht im ZIP, README
reicht). **K2:** F10b-Zeile wie vorgeschlagen ersetzen, Abnahme 20.11.2026, dann Backlog II archivieren. R1 los; die
Notes kommen als eigener Commit einzeln zur Freigabe.

**Befund R1 Textabgleich (20.11.2026):**
- **TASKS:** F10b-Zeile ersetzt (K2); Backlog II → `docs/tasks-archive/backlog-ii.md` (`dfbef05`), Block gegen den
  committeten Text per `diff`: 768 Zeilen gleich, einzige Abweichung die K2-Zeile (1 → 2 Zeilen); 6 × `### [x]`;
  Index-Zeile „Backlog II (K11, B16, B17, F10a, F10b)“. Kein Verweis auf „Backlog II“ außerhalb von TASKS.md und Archiv.
- **README / Nexus** (`45eca1c`): Linux-Zeile „From v0.7.0, each release carries …“ mit `ships --version` (D115; im
  `cli-linux`-Job von `ci.yml` geprüft, `scan` in K12 per Hand byte-gleich); **Präzisierung beim Schreiben:** eine
  Feature-Zeile „Optionally one side first …“ in README **und** Nexus-Beschreibung (die Nexus-Liste spiegelt die README
  wortgleich, `checklist.md`); Nexus: Linux-Hinweis unter Requirements, Beispiel 0.7.0; `checklist.md` `$version` und
  Beispiel `:43` auf 0.7.0; `summary.txt` unverändert (P6).
- **Wiki** (lokal, nichts gepusht): `master` + `k12-ship-reports` (`0d31aec`) + `b16-legend-order` (`200fa61`), je
  `--no-ff`, ohne Konflikt; `master` jetzt 4 vor `origin/master`. Branch `v0.7.0` (`eec87c4`): Installation „Coming from
  v0.6.0“ (Schema 11 nach dem Laden, None/Cross over für alte Pläne, Missions-JSON 5, Legende, Rückweg nach D106 mit
  dem v0.6.0-Wortlaut aus dem Tag, `--data-dir` über die Stub-Exe wie `update-testing.md:41`), Verweis im Abschnitt
  Update; Troubleshooting Known issues „Going back to v0.6.0“ (**Präzisierung beim Schreiben:** dieselbe Falle steht dort
  wie die von v0.5.x); Home „Applies to v0.7.0“ (stand auf v0.5.0), `_Footer` v0.7.0, Overlay-basics `v0.7.1`.
- **Grep** `0.6.x`, `v0.6`, Schema 10/4 über README, CONTRIBUTING, SECURITY, `.github`, `docs/` (ohne decisions, review,
  release-notes, tasks-archive), Nexus, Wiki: übrig nur Geschichte („from v0.6.0 on“ in README, `ship-format.md`,
  `holo-format.md`, `checklist.md:16/98`, Wiki Updates/Troubleshooting) und richtige Spannen (`plan-format.md`
  „v0.6.x and older refuses a schema 11 file“, „Schema 10 (v0.4.0 to v0.6.x)“).
- **Wortlaute aus `src/`:** `Sides` None/Port first/Starboard first, `Side overflow` Cross over/Wait
  (`MissionEditorViewModel.cs:206-227`), Notices `RackSections.cs:532-538` (`has a gap – loaded crates`, `lanes full –
  a mission waits (side overflow off)`, `crosses sides – side overflow off`, `sides full – a mission waits (side
  overflow off)`, Bahn-Hinweise) = Wiki „Plan and editor“; v0.6.0-Meldung `Plan not loaded (…). The next save keeps the
  file aside, it is never overwritten.` (`git grep v0.6.0`, `PlanStore.cs:49`); Settings seit v0.6.0 unverändert
  (`git diff v0.6.0..main` über beide Settings-Ordner leer).
- **Link-Check:** Wiki 125 interne Links, 0 kaputt (Gegenprobe mit zwei eingebauten Fehlern: 4 gemeldet); Notes 11
  externe URLs, alle 200, inkl. D115-Anker.

**Befund R2 Notes (20.11.2026):** `docs/release-notes/v0.7.0.md`, eigener Commit, zur Freigabe im Backlog-Chat.
Vollständigkeit `git log --no-merges v0.6.0..main` (52 Commits):

| Commit | Inhalt | In den Notes |
|---|---|---|
| `1ce6871`, `ef2d896`, `340f600`, `ad5deab`, `8c7771e`, `9d5cb8d` | T12.6a: Release-Prüfung, Netz-Gate, Nexus-Link in README | nein (betrifft v0.6.0) |
| `7901350`, `3c80298` | README-Absatz SignPath, Backlog | nein (von `7e58081` zurückgenommen); Known issue „Unsigned“ |
| `e789000`, `c3e1aa5`, `17e6bee`, `3f418d0` | Ko-fi in README und `FUNDING.yml` | nein (K1: nicht im ZIP, README reicht) |
| `167d590`, `bcd674c`, `b80966d`, `8df22a2` | K12: Plan, R5/Phase 12 zu, Backlog II, Protokoll | nein (TASKS.md) |
| `f2f1669` | Schiffsdaten-Fehler als Issue, keine Schiffsdatei-PRs | Ship data |
| `7e58081` | Code-Signing bleibt aus | Known issue „Unsigned“ |
| `b157566`, `750b44e`, `9de2e2e`, `91fdf2e` | F11: D115, Tarball, CI-Start, README | Linux |
| `2e99d50`, `da2c6d6`, `3d9bbec`, `8d62b37`, `d649a0c` | K11 Archiv | nein (TASKS.md, Verweise) |
| `0821dad`, `9f396a3`, `93b9bb5`, `c12ff0b` | B16 Legende in Scan-Reihenfolge | Legend |
| `a0f7073`, `df46792`, `1e3c749`, `0d41c4a` | B17 ein Scan schließt den Block | Blocks per destination, Known issue „A scan can move planned crates“ |
| `c930d9a`, `e426c98`, `c5e3117` | F10a Plan, Befund, Abnahme | nein (TASKS.md) |
| `309ccd8`, `9ec973f` | F10a Bahnen in Zellreihenfolge, Plänen, Sektionen, Scans | Layout (Halves of a rack, Wait, ganze Reihen) |
| `9abcf8d` | plan.json 11, Missions-JSON 5 | Files, Known issue „Going back to v0.6.0“ |
| `97d2cd8` | Editor: Sides, Side overflow | Layout |
| `1d98ba5`, `92a41f8` | D116/D117, Addenda, Formatdoku | indirekt (Quelle der Texte) |
| `16a3257`, `249a184` | F10b Plan, Befund | nein (TASKS.md) |
| `49ac5d1` | `hull_b` eingefroren, rote Tests | nein (Tests) |
| `3d66ebb`, `cfd6157`, `825b83c` | F10b Seitengruppen, Erfassen, Warten | Layout (Racks by side, Wait) |
| `794b430` | Tooltip „Sides“ | indirekt (Layout) |
| `698a87e` | keine Bahn-Hinweise auf Schiffen mit Seiten | nein (Hinweise, die nie released waren) |

**Präzisierungen beim Schreiben:**
1. Abschnitt „Ship data“ mit „unverändert `2026.11.1`“ (Pin = Latest, P4) und dem Meldeweg aus K12 statt eigenem
   Doku-Abschnitt.
2. Known issue „A scan can move planned crates“ (B17-Gegenargument zu D41): „loaded“ abhaken schützt.
3. Known issues aus v0.6.0 gekürzt auf die weiter gültigen; die Downgrade-Sätze für v0.5.x/v0.4.0 stehen im Wiki
   (Link „More, with workarounds“).
4. Linux-Größe „about 66 MB“ aus K12 (66,4 MB); Nachmessung in R3.

**Notes freigegeben (Backlog-Chat, 20.11.2026)** mit K1–K4:
- **K1** Reihen-Beispiel generisch: „once the first side has no free row left, the next contract goes to the other side,
  although single cells on the first may still be free“ (das RAFT-Beispiel 40 + 40 hing am Kistenmix).
- **K2** D115-Anker geprüft: GitHubs gerendertes `decisions.md` (`gh api … contents … application/vnd.github.html`)
  hat `id="user-content-d115--no-linux-overlay-port-the-cli-ships-for-linux-x64-f11-k12"` = Link. Unverändert.
- **K3** „saves the file as schema 11 right after loading“ stimmt, unverändert: `App.ComposeStartPlan` → `ShowPlan` →
  `Step` setzt `Saved`, `PlanSaver` schreibt nach 500 ms Debounce ohne Änderung des Spielers; belegt von
  `PlanSaverTests.A_plan_from_before_v0_4_0_keeps_its_notice_after_the_first_save` (Start, `Flush`, Datei enthält
  `"schemaVersion": 11`). Der v0.6.0-Satz „the first change keeps the file …“ bleibt ebenfalls: ohne lesbaren Plan
  ruft v0.6.0 kein `ShowPlan`, erst eine Änderung speichert.
- **K4** Schiffszahl in den Notes „110 ships (one of them a cargo module configuration)“. README, Nexus und Wiki sagen
  weiter „109 ships and one cargo module configuration“ (Einheitlichkeit aus T12.6a), nicht angefasst.

**R3 Draft-PR, Merge, Probe (CC, 20.11.2026):**
- Draft-PR [#31](https://github.com/MRiediger/sc-cargoplan/pull/31) nur für CI: Run 44747078793 grün auf
  `ubuntu-latest`, `windows-latest` und „Linux CLI from the release tarball“.
- Merge `--no-ff` lokal → **`9df5faa`**, Inhalt gleich `release/v0.7.0`. Darauf im sauberen Worktree, SDK 10.0.404:
  Build `0 Warnung(en)`, `0 Fehler`; `dotnet format --verify-no-changes` Exit 0;
  `Bestanden! … erfolgreich: 2122, … gesamt: 2122 - CargoPlan.Core.Tests.dll`, `… 2090 … CargoPlan.App.Tests.dll`,
  `… 280 … Import`, `… 34 … Ocr`.
- `./build/publish.ps1 -Version 0.7.0 -DeltaBase github`: `Delta base: CargoPlan-0.6.0-full.nupkg`, 16 Dateien
  gepatcht, 374 gleich, Delta 658 055 B; `Layout check: … passed (388 files in current\, 110 ship files of
  ships-2026.11.1, 5 PDBs, version 0.7.0)`; `Tarball check: … passed (326 files in SC-CargoPlan-Cli/, 110 ship files
  of ships-2026.11.1, CargoPlan.Cli executable, version 0.7.0)`. ZIP 96 791 812 B, Tarball 66 381 648 B. B7-Referenz:
  Runtime 10.0.15 (beide), `coreclr.dll` 10.0.1526.53008, Cli `0.7.0+9df5faa…`.
- Martin (20.11.): `main` gepusht, CI grün (Run 44748145744), Tag `v0.7.0` auf `9df5faa`. Dieses Protokoll kommt als
  Folge-Commit nach dem Tag.

**Release-Prüfung (CC, 20.11.2026)**, alle Assets frisch in einen leeren Ordner geladen:

| Prüfung | Soll | Ist |
|---|---|---|
| CI auf `main` (Merge-Push) | grün | Run 44748145744 success |
| Workflow „Release“ | grün | Run 44749731575 success, 2 min 55 s |
| Tag | auf dem Merge | `9df5faa`, leichtgewichtig (wie `v0.5.1`, `v0.6.0`) |
| Release | Latest, kein Pre-Release, kein Draft | `isPrerelease false`, `isDraft false`, `releases/latest` = `v0.7.0`, 14:05:46 UTC |
| Body | = `docs/release-notes/v0.7.0.md` | identisch ohne CRLF, eine Leerzeile mehr am Ende (wie v0.4.0) |
| Assets | 7 (P8) | ZIP 96 791 336, `.sha256` 98, Full 96 835 347, Delta 648 426, Feed 732, Tarball 66 381 450, `.sha256` 107 |
| SDK im Runner | 10.0.404 | `.NET Core SDK with version '10.0.404' is already installed` |
| Delta | 0.6.0 → 0.7.0 | `Delta base: CargoPlan-0.6.0-full.nupkg`, 648 426 B |
| SHA256 ZIP / Tarball | = `.sha256` | `e497b2f0…` gleich, `06fe22b6…` gleich |
| `releases.win.json` | 0.7.0 voll + Delta, Size/SHA1 = Asset | beide gleich; dazu `Full 0.6.0` 96 804 720 = Asset von v0.6.0 (T12.6a) |
| `check-portable.ps1` gegen den Download | passed | `passed (388 files in current\, 110 ship files of ships-2026.11.1, 5 PDBs, version 0.7.0)` |
| `check-cli-tarball.ps1` gegen den Download | passed | `passed (326 files in SC-CargoPlan-Cli/, 110 ship files of ships-2026.11.1, CargoPlan.Cli executable, version 0.7.0)` |
| ZIP-Wurzel | `SC-CargoPlan.exe`, `Update.exe`, `.portable`, `current\` | genau diese |
| Runtime (B7) | = lokales Pack | 10.0.15 beide, `coreclr.dll` 10.0.1526.53008 |
| Cli-Version | `0.7.0+9df5faa…` | `0.7.0+9df5faa9d6f91ecc1cfe49d0e8ecb7cdbee650b7`, lokal gleich; `ships --version` mit leerem Datenordner `ships-2026.11.1 (baseline)` |
| Linux unter WSL (Ubuntu 24.04) | entpackt, startet ohne `chmod` | `CargoPlan.Cli` `-rwxr-xr-x`, `--version` `0.7.0+9df5faa…`, `ships --version` `ships-2026.11.1 (baseline)` |
| Größe | Notes „about 90 MB“, Linux „about 66 MB“ | ZIP 92,3 MiB, Tarball 66,4 MB; Delta von v0.6.0 0,65 MB → CLAUDE.md |

**Wiki:** CC hat `v0.7.0` lokal mit `--no-ff` nach `master` gemergt (`4731161`); Martin hat gepusht.

**Netz-Gate (Martin, 20.11., `C:\Tools\SC-CargoPlan`, „bestanden“; Belege aus `cargoplan.log` und dem Profil):**
14:13:02 `Update: 0.7.0 available (installed 0.6.0)` → 14:13:16 `Velopack: Downloading delta 0.7.0`, `All delta packages
downloaded and verified`, `Applying 1 patches to CargoPlan-0.6.0-full.nupkg` → 14:13:25 `Update: restarted by
Update.exe as 0.7.0`; `Ship data: in use: ships-2026.11.1 (baseline) …, 110 ships`, 14:13:27 `Ship data: nothing newer
than ships-2026.11.1` → keine Ship-data-Zeile. `plan.json` im Profil: `"schemaVersion": 11`. Editor (Sides, Side
overflow), Legende und Settings 0.7.0 laut Martin; Notices stehen nicht im Log.

**R4 Nachlauf (CC, 20.11.2026):**
- `release/v0.7.0` lokal und remote gelöscht; PR #31 hat GitHub mit dem Push von `main` selbst auf *merged* gesetzt.
- Wiki-Branches lokal gelöscht (`git branch -d`, alle gemergt): `k12-ship-reports`, `b16-legend-order`,
  `b17-section-block`, `f10a-lanes`, `v0.7.0`; remote `f10a-lanes` gelöscht. Beide Repos haben nur noch `main` bzw.
  `master`.
- Die vier Backlog-Zeilen „Wiki-Branch …“ wortgleich nach `docs/tasks-archive/backlog-done.md` (neue Überschrift
  „Umzug am 20.11.2026 (Release v0.7.0)“, je eine Erledigt-Zeile), wie K11 mit erledigten Backlog-Punkten verfuhr
  (Präzisierung beim Schreiben: „streichen“ ohne Inhaltsverlust).
- Nexus-Upload (optional) und die Nachricht an den Linux-Nutzer macht Martin; beide laufen außerhalb von TASKS.md (wie
  die Ankündigungen seit K12).
- Dieser Abschnitt geht ins Archiv (`docs/tasks-archive/release-v0.7.0.md`).

**Akzeptanz:**
- [x] Notes vom Backlog-Chat freigegeben (20.11., K1–K4); `git log v0.6.0..main` ohne unerwähnten nutzer-sichtbaren
  Commit (Tabelle R2)
- [x] **Netz-Gate (Martin, SC vorn, 20.11., bestanden, Delta übers Netz):** v0.6.0-Portable in eigenem Ordner oder mit `--data-dir` (Backup von
  `%LocalAppData%\SC-CargoPlan` vorher) → Start → Velopack bietet 0.7.0 → Update → Neustart → Plan geladen, plan.json
  Schema 11, Editor zeigt Sides und Side overflow, Legende chronologisch, Settings 0.7.0, keine Ship-data-Zeile
- [x] Linux-Asset auf der Release-Seite; unter WSL entpackt `./CargoPlan.Cli --version` = `0.7.0+…` (CC, 20.11.,
  Tabelle oben)
- [x] Wiki `master` nach dem Tag gepusht (`4731161`), Branches weg
- [x] Commits `R0.7:` bzw. `REL:`, kein `Co-Authored-By`; Merge `--no-ff` (`9df5faa`), Martin pusht und taggt

---

