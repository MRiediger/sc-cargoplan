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

---

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

**Akzeptanz:**
- [ ] Notes vom Backlog-Chat freigegeben; `git log v0.6.0..main` ohne unerwähnten nutzer-sichtbaren Commit
- [ ] **Netz-Gate (Martin, SC vorn):** v0.6.0-Portable in eigenem Ordner oder mit `--data-dir` (Backup von
  `%LocalAppData%\SC-CargoPlan` vorher) → Start → Velopack bietet 0.7.0 → Update → Neustart → Plan geladen, plan.json
  Schema 11, Editor zeigt Sides und Side overflow, Legende chronologisch, Settings 0.7.0, keine Ship-data-Zeile
- [ ] Linux-Asset auf der Release-Seite; unter WSL entpackt `./CargoPlan.Cli --version` = `0.7.0+…`
- [ ] Wiki `master` nach dem Tag gepusht, Branches weg
- [ ] Commits `R0.7:` bzw. `REL:`, kein `Co-Authored-By`; Merge `--no-ff`, Martin pusht und taggt

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
- Wiki-Branch `k12-ship-reports` (lokal, `06b4004`: „Ships and cargo grids“ ohne Schiffsdatei-PR, K12) nach `master`
  mergen (`--no-ff`, CC), Martin pusht. **Auslöser: der nächste Tag.**
- Wiki-Branch `b16-legend-order` (lokal, `0386c22`: „Legend and delivery states“, Missionen in Scan-Reihenfolge, B16)
  nach `master` mergen (`--no-ff`, CC), Martin pusht. **Auslöser: der nächste Tag.**
- Wiki-Branch `b17-section-block` (lokal, `1009927`: „Plan and editor“, ein Scan schließt den Block seines Ziels, B17)
  nach `master` mergen (`--no-ff`, CC), Martin pusht. **Auslöser: der nächste Tag.**
- Wiki-Branch `f10a-lanes` (lokal, `ea9252a` auf `b17-section-block`: „Plan and editor“, Layout mit Sides und Side
  overflow, Missions-JSON Schema 5, F10a) statt `b17-section-block` nach `master` mergen (`--no-ff`, CC; enthält B17),
  Martin pusht. **Auslöser: der nächste Tag** (plan.json 11 und Missions-JSON 5 kommen erst mit dem Release).
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
