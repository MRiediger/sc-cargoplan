# Archiv: Release v0.3.0

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Release v0.3.0 (08.11.2026) – Branch `release/v0.3.0`

**Warum:** F1–F3, F7, 4a–4d gehen als ein Release raus; F8 ist zurückgestellt. Kein Release Candidate: Martin hat `main` an einem echten Hauling-Abend durchgespielt (CT-Sync, Plan, Laden, Abliefern, Schiffswechsel, Neustart mit gespeichertem Plan), ohne Befund; die 4d-Editoränderungen sind am Gate abgenommen. Ablauf R1 Texte → R2 Notes (Freigabe Claude.ai) → R3 Medien (Martin) → R4 Tag → R5 Nachlauf. Keine Funktionsänderung; ein Fehler wird ein Bugfix-Branch ab `main`.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. Branch `release/v0.3.0` statt `docs/release-v0.3.0`.
2. Wiki: keine neue Seite „Ship data“; in `Ships-and-cargo-grids` heißt der Abschnitt „Where the grids come from“ jetzt „Ship data“, alle anderen Anker bleiben. Einen „User Guide“ gibt es im Wiki nicht, der Stoff steht auf den Seiten mit dieser Rolle.
3. Kapazität (Hull B 512, Railen 640) nicht als Änderung in den Notes: seit v0.2.0 hat sich bei keinem Schiff `totalScu` geändert.
4. Keine Known-issue-Zeile zur Rack-Nummern-Verdeckung (Martin: nichts gesehen); 4b-Gate-Zeile abgehakt.
5. Ryekal nicht in den Credits; der Melder von #21/#22 heißt Reikal (GitHub).
6. Schema 6 braucht nur `rotation`; `modelKey` und die Anker kamen ohne Schema-Sprung (D94, D90).
7. Die drei Fleetyards-Schiffe haben auch ihre Gitter von Fleetyards, nicht nur die Hülle.
8. „Wie am Aufzug“ ist nur an der Hull A belegt; Hull B/C: Cargo-Pose des Spielmodells.
9. Rundung ≤ 0,4 Zellen gilt pro Achse; in den Notes in Nutzersprache („up to about half a meter“).
10. `ships/` im ZIP = 105 Schiffsdateien + `modules/ts_module.json`.
11. `staff-request.md` bleibt (Brief verschickt); nur `description.bbcode` und `checklist.md` auf 0.3.0.
12. K1: Notes um 4d (Changes, Fix „N SCU or smaller“), D96 und Nachträge D23/D36/D40/D69 in der Vollständigkeit, Testzahlen neu.
13. K2: Wiki `Plan-and-editor` mit Kistenformat (Anzahl × Größe, beide Eingabeformen, Summenfehler) und Max size per Dropdown/Tastatur; Editor-Screenshot D5 als dritte Aufnahme in R3.
14. Wiki-Zeile „A hologram look takes about 40–50 MB more memory“ gestrichen: Messung mit Martins echter `settings.json` > 10 min.

### [x] R1 Textabgleich (08.11.2026)

- README (Feature-Zeile mit Datenherkunft), `docs/ship-format.md`, `docs/settings-format.md` (D95-Regel), `CONTRIBUTING.md` („Correct ship data“), `ship-correction.yml`, `docs/nexus/description.bbcode` und `checklist.md`, `docs/shots/SHOTLIST.md`
- Wiki: Home, `_Footer`, Ships-and-cargo-grids, FAQ, Troubleshooting, Installation-and-first-start, Overlay-basics, Plan-and-editor, Ship-view, Legend-and-delivery-states, Quick-start

**Befund:**
- `docs/ship-format.md`: Hull-A-Beispiel und Korrektur-Diff hatten die Fleetyards-Origins (`[0, 1, 3]`), die Datei hat `[-9, 3, 1]`, 5 Zellen Spindel; Hull-B-Beispiel (y −4/−2) passte nicht mehr; „ship draft … has none“ für `position` gilt jetzt für fast alle Schiffe (99 von 105 ohne `position`, die Spieldateien nennen keine Seite); 8 Schiffe mit Rack-IDs außer der Reihe. Schema 6, `rotation`, `modelKey` + Alias, s und Refresh-Weg (`MOTHERLODE_API_KEY` oder `motherlode/api-key.txt`, gitignored, nie committet) standen schon da.
- Türlimits im Wiki um Corsair 24 ergänzt; alle anderen Zeilen stimmen mit `ships/` überein.
- Wiki „Rows: Lengthwise, bow to stern“ war für gedrehte Racks falsch; jetzt die Achsen des Racks wie der App-Tooltip.
- Known issue „Racks outside the hull“ (Railen, Caterpillar) gestrichen: am Bildvergleich (Draufsicht, `holo-shaded above`) liegen beide im Rumpf.
- Editor-Screenshots: `editor-rows.png` zeigt die alte cyanfarbene Markierung, den abgeschnittenen Kopf „Max“ und keine Layout-Zeile → D5 neu (R3). `delete-mission.gif`, `editor-import-plan.gif` zeigen vor allem das Overlay mit der alten Hülle, bleiben wie alle übrigen.
- Grep über README, `docs/` (ohne decisions), Nexus, Templates, SHOTLIST und Wiki nach `holo-v1`, `drafts`, `97 ships`, `0.2.0`, `fleetyardsSlug`, `Max crate`, `from Fleetyards`: jeder Rest ist Historie, betrifft die drei Fleetyards-Schiffe oder ist der Begriff „max crate size“.
- Link-Check (neues `linkcheck.py`, Markdown und `[url=…]`, Anker gegen Repo und Wiki-Kopie): 23 Dateien, interne Links 0 kaputt; Negativtest (falscher Anker, fehlende Datei) meldet 2. Extern: GitHub antwortete während des Laufs wechselnd 000/502/504; `holo-v2` (100 Assets), Repo und Discussions per `gh` bestätigt, Nexus 403 wie immer (Bot-Schutz). Zweiter Lauf nach R3, einzeln: alle 13 GitHub- und Fremd-URLs 200 (Nexus 403, im Browser zu prüfen).
- Bildvergleich `ShipPictureTests` gegen `main` (754921c, eigener Worktree): **1388 identisch, 0 verschieden, 0 fehlend**, Placement-Zahlen auf 0 von 106 verschieden. `git diff main -- src tests ships outlines build` leer.
- `dotnet build -c Release` 0 Warnungen; `dotnet test` im sauberen `main`-Worktree (Code gleich dem Branch): **Core 1818, App 1671, Import 164**, Summe 3653; `Category=Slow` 2 grün. (4d nannte Core 1819 aus der Arbeitskopie mit der ausgeschlossenen Scratch-Datei.)

### [x] R2 Release-Notes (08.11.2026)

- `docs/release-notes/v0.3.0.md` aus der Stoffsammlung, gegliedert: Highlights, Ship data, Changes, Before you update, Known issues, Credits

**Befund:**
- Vollständigkeit `git log v0.2.0..main --no-merges`: F2/F1, F7, F3, 4a, 4b, 4c, 4d kommen vor. Stoffsammlung ergänzt um 4a (TASKS 4a, „Material“), 4b (Hüllen, Cargo-Pose, Cyclone) und 4d; die F3-Zeile „No shipped ship uses it yet“ war seit 4a falsch. Nicht in den Notes, weil ohne Wirkung aufs ZIP: Testkopien, Bildharness, Reclaimer-Frame-Ziel, Doku- und TASKS-Commits.
- Gespeicherter Corsair-Plan mit 32er-Kisten: `PlanFileDto.UnderCap` gibt ihm die Kappe `Saved`, die Overlay plant ihn beim Laden neu und meldet „Plan was made for other racks, re-planned for …“. **Beobachtet** (Scratch-Test, nicht committet, nach dem Muster `DoorLimitOverlayTests.A_plan_file_split_under_another_cap_is_planned_anew_and_says_so`): Plan auf `git show v0.2.0:ships/corsair.json` (Riker 64 SCU, max 32 → `32+32`), `PlanFile.Write`, `PlanFile.Read` mit der heutigen `ships/corsair.json` → Kappe `Saved` 32; `OverlayViewModel.ShowPlan` → `24+24+16`, Notices „Plan was made for other racks, re-planned for DRAK Corsair.“ und „crates capped to 24 SCU (door limit)“.
- Downgrade auf 0.2.0: Plan-Schema 9, Settings-Schema 2 und Missions-Schema 4 seit v0.2.0 unverändert (`git diff v0.2.0..main` leer in `Core/Plans`, `App/Settings`, `Core/Missions`); 0.2.0 lädt `holo-v1` neu, weil 0.3.0 den Ordner löscht. Eigene Schiffsdateien mit `rotation` oder `modelKey` lehnt 0.2.0 ab.
- Keine Paket- oder SDK-Änderung seit v0.2.0.

**Abnahme:**
- [x] Notes-Entwurf von Claude.ai freigegeben (Martin, 08.11.) mit zwei Textänderungen: „DRAK Corsair“ → „Corsair“, „thanks Reikal“ nur bei #22

### [x] R3 Medien (Martin) (08.11.2026)

- E1 Hero-GIF neu (Hull A am Aufzug: Plan → Laden → Abhaken), H15 `docs/images/ships-hull-b.png` (Hull B im Holo, gedrehte Racks), D5 `editor-rows.png` (Editor nach 4d); SHOTLIST beschreibt alle drei
- Danach CC: README- und Wiki-Verweise, Nexus-Bildliste, Größenregeln

**Befund:**
- Hero: Hull A in der Hülle aus dem Spielmodell, Ablauf CT-Capture → Kisten erscheinen → Abhaken; 21 s, 640 × 312, 8 fps, 4.794.582 Byte (alt 6.198.644). Länger als die 8 s der SHOTLIST und mit der Amber-Zeile „Plan discarded – …“ im Bild; Martin: bleibt so. SHOTLIST folgt der Aufnahme.
- H15: erste Datei zeigte die Hull A (Kopfzeile `MISC Hull A · 29/64`), von Martin ersetzt; jetzt `MISC Hull B · 9/512`, Holo schattiert, schräge Racks beidseits der Spindel, CT-Plan statt Testplan.
- D5: Max size, `3×4`/`2×4`/`3×2` kursiv, Layout-Zeile, dunkle Markierung mit Teal-Balken.
- Doppelte Dateien gelöscht (Martin): `docs/images/editor-rows.png`, Wiki `media/hero.gif`, Wiki `media/ships-hull-b.png` (alte Hull-A-Datei). Das Wiki zeigt Hero und Hull B per Raw-URL wie bisher den Hero.
- README: Hull-B-Bild unter den Features; Nexus-Bildliste 11 Bilder (Hull B und Editor auf 2 und 3). Link-Check intern 0 kaputt.
- Commits `1f0c9bc` (App), Wiki `2b1dbb8`.

### [x] R4 Merge, Tag, Nachmessung (08.11.2026)

- Vor dem Merge: Build, Tests, Bildvergleich, `publish.ps1 -Version 0.3.0`, `gh run list -L 3`, externe Links
- `publish.ps1 -Version 0.3.0` lokal (vor den Branch-Commits): ZIP 62.121.133 Byte (62,1 MB), 368 Einträge (v0.2.0: 360, plus 8 neue Schiffe), `ships/` 106 JSON = 105 + `modules/ts_module.json`, Runtime 10.0.14, `CargoPlan.Cli --version` `0.3.0+754921c…`
- Martin: Abnahme → `--no-ff` nach `main`, Martin pusht `main` und Tag `v0.3.0`
- Veröffentlichtes ZIP nachmessen (Tabelle): SHA256, Größe, Runtime, Einträge, `ships/` 105 + 1 Modul, `--version`, Settings-Fußzeile

**Befund (nach dem Push):**
- Merge `--no-ff` 23b2644 (CC, nach Martins Abnahme), Inhalt gleich `release/v0.3.0`. Martin: `main` gepusht, CI grün (2 min 25 s), Tag `v0.3.0` annotiert auf 23b2644, Release-Workflow grün (2 min 39 s); Wiki gepusht.
- Release `v0.3.0`: kein Pre-release, kein Draft, `releases/latest` → `v0.3.0`, veröffentlicht 08.11.2026 13:36 UTC.

| Prüfung | Soll | Veröffentlichtes ZIP | |
|---|---|---|---|
| SHA256 gegen `.sha256` | gleich | `47271f8f…84f83`, gleich | ✓ |
| Größe | ca. 64 MB | 64.201.672 Byte (64,2 MB / 61,2 MiB) | ✓ |
| Runtime | 10.0.14 (SDK aus `global.json`) | `Microsoft.NETCore.App` und `WindowsDesktop.App` 10.0.14 | ✓ |
| Einträge | wie lokal | 368, alle mit `/`; Eintragsliste gleich dem lokalen `publish.ps1 -Version 0.3.0` | ✓ |
| `ships/` | 105 + 1 Modul | 106 JSON, davon `modules/ts_module.json` | ✓ |
| `outlines/` | Katalog und NOTICE | `ship-outlines.json`, `NOTICE.md` | ✓ |
| `CargoPlan.Cli --version` | `0.3.0+23b2644…` | `0.3.0+23b264432f2a94e4c6b71c59a03f3b2a2258492c` | ✓ |
| Settings-Fußzeile | `SC-CargoPlan 0.3.0+…` | Martin | offen |

- Wiki live: Anker `Ships-and-cargo-grids#ship-data`, `#ship-holograms`, `#wrong-grid-how-to-report-it`, `#door-limits`, `Troubleshooting#known-issues`, `Plan-and-editor#layout`, `#the-crate-mix` vorhanden; Fußzeile „Applies to SC-CargoPlan v0.3.0 · Not affiliated with Motherlode, …“; `hero.gif` und `ships-hull-b.png` per Raw-URL 200 in voller Größe.
- Lokales ZIP vor den Branch-Commits: 62.121.133 Byte; das CI-ZIP ist 2,1 MB größer mit gleicher Eintragsliste (Kompression; v0.2.0: lokal 62,1, CI 64,2 MB, ebenso).
- `release/v0.3.0` lokal gelöscht (nie gepusht).

### [x] R5 Nachlauf

**Abgehakt in K12 (19.11.2026, R1):** überholt – Nexus und Ankündigungen liefen mit „Release v0.4.0“ R5 und später
(Nexus-Seite 16.11. mit v0.5.0), die Backlog-Zeilen stehen im Backlog.

- [x] Wiki-Push nach dem Release (Martin, 08.11.), Anker-Check gegen das Live-Wiki (R4-Befund)
- [ ] Martin: Settings-Fußzeile 0.3.0, Release-Seite im Browser (Notes, Assets, Nexus-Link)
- Nexus-Upload am 16.11. mit ~~0.3.0~~ ~~0.3.1~~ (Release v0.3.1) nach `docs/nexus/checklist.md` (T9.3) – **überholt
  (12.11.): Nexus startet mit 0.4.0**, siehe „Release v0.4.0“ R5
- Ankündigungen (Entwürfe Claude.ai, Martin verschickt): Ryekal (Reddit), Sciirof, Torlek, Spectrum-Post
- Backlog-Zeilen „holo-v1-Release entfernen“ (Auslöser: v0.3.0 vier Wochen stabil) und „Modelle bei Sciirof anfragen“ mit Datum; nächster Chat „Feedback II“

