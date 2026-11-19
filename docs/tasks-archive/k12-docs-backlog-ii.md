# Archiv: K12 Doku-Abschluss, Backlog II, F11

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## K12 Doku-Abschluss, Backlog II, F11 (19.11.2026) – Branch `docs/backlog-ii`

### [x] K12 Doku-Abschluss, Backlog II, F11 (19.11.2026, Freigabe Backlog-Chat 19.11.)

**Warum:** v0.6.0 ist draußen (19.11.), Phase 12 im Shipdata-Repo mit T12.6b geschlossen. Hier stehen noch überholte
Haken (R5 Nachlauf v0.4.0), der Phase-12-Abschluss und Doku, die Schiffsdatei-PRs in ein `ships/` des Shipdata-Repos
verspricht, das es nicht gibt (T12.6b Befund 4). Die SignPath Foundation hat am 19.11. abgelehnt. Aus den
Feedback-Chats kommen vier neue Punkte (B16, B17, F10, F11); F11 (Linux) ist nur Build und Doku und läuft hier mit.
**Nummer:** K11 bleibt für die TASKS.md-Archivierung reserviert (R5 Nachlauf v0.4.0), deshalb K12, obwohl K12 vorher
läuft. Reihenfolge danach (Martin): K11 → B16 → B17 → F10; F8 bleibt zurückgestellt.

Inhalt (Startprompt Backlog-Chat, 19.11.): (1) R5 Nachlauf schließen, (2) Phase 12 hier schließen, (3) Schiffsdatei-PRs
aus der App-Doku, (4) Code-Signing in README und Backlog, (5) Backlog II als Tasks anlegen (K11, B16, B17, F10), (6) F11
umsetzen: D-Eintrag „No Linux overlay port“, `CargoPlan.Cli` als `linux-x64`-Asset, README-Zeile.
**Nicht hier:** K11-Umsetzung, B16/B17/F10-Code, Release/Tag, Wiki-Push.

**Akzeptanz:**
- `grep -n '^### \[ \]' TASKS.md` zeigt nur K11, B16, B17, F10, F11-Rest (falls Scan offen) und die bewusst offenen Tasks.
- README ohne SignPath-Absatz; CONTRIBUTING, `ship-format.md` und Issue-Vorlage versprechen keinen Schiffsdatei-PR mehr.
- `build/publish.ps1` lokal: Tarball entsteht, `./CargoPlan.Cli --version` unter WSL liefert die Version; `ci.yml` grün.
- Alle Tests grün (App-Tests ohne Änderung erwartet).
- Commits `K12: …` bzw. `F11: …`, kein `Co-Authored-By`; Merge `--no-ff`, Martin pusht.

**Plan (19.11.2026, Branch `docs/backlog-ii` ab `main` `3f418d0`, Freigabe aus dem Backlog-Chat)**

**Ist-Stand vor dem Plan (Grep und Messung, nicht Gedächtnis):**
- Offene `### [ ]` heute: T9.3, R5 (v0.3.0, Z. 4779), T10.1, T10.6, R5 (v0.4.0), T12.6; dazu `#### [ ] T12.6b`. Der
  Auftrag nennt R5 v0.4.0 und T12.6b. Die anderen vier sind erledigt, aber nie abgehakt: T9.3 (Nexus-Seite
  `mods/52` seit 16.11.), T10.1 (T10.1a und T10.1b `[x]`), T10.6 (v0.4.0 veröffentlicht 13.11., Abschnitt „Release
  v0.4.0“), R5 v0.3.0 (Nexus und Ankündigungen von „Release v0.4.0“ R5 überholt). → **Rückfrage R1**.
- **Schiffsdatei-PR-Versprechen** (Grep `pull request|PR|ships/` in Doku, Vorlagen, Wiki, Nexus): mehr Stellen als im
  Auftrag – README Z. 58 („A pull request with a corrected ship file goes to sc-cargoplan-shipdata“); CONTRIBUTING
  „Correct ship data“ (Z. 69: „a hand fix goes there“) **und** „Contribute a ship“ (Z. 73–79: Datei + PR-Checkliste);
  `ship-correction.yml`; **`ship.yml`** („A pull request with the file is even better“); `ship-format.md` Einleitung
  (Z. 8–9), „Measuring a ship“ (Z. 307 „name the source in your PR“), „The three Fleetyards ships“ samt Anker (Z. 318,
  362), „Checklist for a new ship PR“ (Z. 441–457); Wiki „Ships and cargo grids“ Z. 140–142 („a pull request there with
  the corrected file is even better“). Nexus-Texte: kein Treffer.
- **Türlimits:** CONTRIBUTING Z. 69 („A door limit is ours to fix“) und `ship-correction.yml` („Door limits are ours“)
  versprechen eine Korrektur, für die es keinen Weg mehr gibt: `import-motherlode.ps1` (Shipdata) übernimmt
  `maxCrateSize` aus der Datei des letzten Release (Z. 346), `overrides.json` kennt kein Türlimit. Die App-Doku
  verspricht deshalb nur „melden“; der Befund geht ans Shipdata-Repo (unten „Übergabe“).
- README Z. 32–33: SignPath-Absatz. Backlog-Zeile „Setup.exe und/oder Code-Signing“ mit Stand 19.11. „beantragt“.
- Letzte D-Nummer D114 → **D115**.
- Wiki `master` ist 2 Commits vor `origin/master` (`cba829a`, Merge `5a1ab1d` „Nexus Mods link“): nicht gepusht. Nur
  zur Info, kein Teil von K12.

**F11-Spike (19.11., Wegwerf im Scratchpad, nichts im Repo):**
- `dotnet publish src/CargoPlan.Cli -c Release -r linux-x64 --self-contained true` unter Windows: 9 s, 325 Dateien,
  138 MB. Drin: `libonnxruntime.so` (`Microsoft.ML.OnnxRuntime` 1.32.0, `runtimes/linux-x64`), `libSkiaSharp.so`
  (`SkiaSharp.NativeAssets.Linux.NoDependencies` 3.119.1, kommt schon heute transitiv über RapidOcrNet), `models/v5/`
  (4 Dateien), `ship-data/` (110 Schiffsdateien). **Kein Paket neu.**
- WSL Ubuntu 24.04.2: `--version` → `0.6.0+3f418d0…`; `ships --version` → `ships-2026.11.1 (baseline) …/ship-data`;
  `plan tests/…/example_missions.json` Exit 0; `scan` auf einer 3440×1440-Aufnahme aus `sc-ocr-samples/set` Exit 0,
  1 103 ms (Windows 1 557 ms). **Ausgabe Linux = Windows byte-gleich** (nach CRLF → LF): `plan` 166 B, `scan` 1 980 B.
- **Festlegung: `scan <png>` läuft unter Linux** und gehört zum Linux-Build, ohne „Windows-only“-Vermerk. Die Aufnahme
  macht der Spieler selbst (Screenshot des Spielfensters); die GDI-Capture ist App, nicht Cli.
- **Befund Packen:** Windows-`tar.exe` (bsdtar 3.8.9) aus einem Ordner schreibt `CargoPlan.Cli` als `rw-rw-rw-` →
  unter Linux `Permission denied`. pwsh 7 (`System.Formats.Tar`) ist lokal nicht installiert, `publish.ps1` läuft hier
  unter Windows PowerShell 5.1. **Gemessen:** bsdtar liest eine **mtree-Liste** als Eingabe (`#mtree`, je Eintrag `type`,
  `mode`, `uid=0 gid=0`), `tar -czf <out> @<liste>` → `-rwxr-xr-x … CargoPlan.Cli`, 66 358 181 B (63,3 MiB), 4,1 s; in
  WSL entpackt laufen `--version`, `ships --version` und `scan` ohne `chmod`.
- Voraussetzung unter Linux: x64, glibc, `libicu` (jede .NET-App ohne `InvariantGlobalization`); WSL hat `libicu74`.

**Umsetzung (Reihenfolge = Commits):**

1. **`K12: plan`** – dieser Abschnitt.
2. **`K12: close R5 and phase 12`** (TASKS.md):
   - R5 (v0.4.0): „Nexus mit 0.4.0“ `[x]` – Nexus-Seite am 16.11. mit v0.5.0 veröffentlicht; der v0.6.0-Upload liegt in
     der Nexus-Quarantäne, Martin lässt ihn liegen: Nexus ist nur Schaufenster, Updates laufen über GitHub/Velopack.
     „Ankündigungen“ `[x]` – v0.4.0-Ankündigung entfallen, v0.5.0/v0.6.0 folgten; Ankündigungen laufen außerhalb von
     TASKS.md. „Übergabe an K11“ → Verweis auf K11 in „Backlog II“. R5 `[x]`.
   - T12.6b `[x]` mit Verweis: Shipdata-Branch `docs/p12-close`, Commits `8bb42d5`/`b1e4d29`/`f4467d1`/`20fe78d`, Merge
     19.11.2026; SD13 und SD3-Addendum dort. T12.6 `[x]`. Phase-12-Überschrift „(gestartet 16.11.2026, abgeschlossen
     19.11.2026, D111)“.
   - Backlog „Modelle für 135c, 600i EE, F7C“: Stand 19.11. – F7C erledigt, 135c-Transform angekündigt
     (Shipdata-Backlog), **600i EE angefragt** (Discord 19.11.); Auslöser: Sciirofs Antwort. Zeilen Cyclone und
     135c-Transform: Zusatz „→ Shipdata-Backlog“.
   - Nach R1: T9.3, R5 (v0.3.0), T10.1, T10.6 `[x]` mit je einem Vermerk (Fundstelle oben).
3. **`K12: ship reports are issues, not ship file PRs`** – eine Linie überall (Englisch, sinngemäß): *The ship files are
   rebuilt each month from the game files by way of Motherlode; a hand-edited file would be overwritten by the next run,
   so ship files take no pull requests. Open an issue here or in sc-cargoplan-shipdata; we report grid errors to
   Motherlode (cryon.rocks), and the next data release fixes them for everyone.*
   - README Z. 55–59: PR-Satz raus.
   - CONTRIBUTING „Correct ship data“: Absatz 2 auf die Linie; Türlimits „report them the same way“. „Contribute a ship“
     → **„Missing a ship?“**: jedes Schiff kommt aus Motherlodes Katalog; ein Schiff, das dort fehlt, kommt über eine
     *Ship request* mit Messwerten, wir geben sie an Motherlode weiter. Keine PR-Checkliste.
   - `ship-correction.yml`: PR-Satz und Link `#correcting-a-rack` raus, Türlimit wie oben. `ship.yml`: PR-Satz raus,
     Messanleitung (`#measuring-a-ship`) bleibt.
   - `ship-format.md`: Einleitung („A ship correction or a new ship goes to the pipeline's repository“ → als Issue);
     „Measuring a ship“: „in your issue“ statt „in your PR“; **„The three Fleetyards ships“ → „The two Fleetyards
     ships“** (135c, 600i EE; F7C seit `ships-2026.11.1` aus den Spieldateien), Anker in Z. 318 mit; „A change to one of
     the three is hand work“ → beide bleiben, bis Motherlode ihre Grids hat; **„Checklist for a new ship PR“ entfällt**
     (kein Link darauf, Grep). „Correcting a rack“ bleibt als Formatbeispiel, was eine `origin`-Änderung im Bild macht.
   - Wiki (`..\sc-cargoplan.wiki`, lokaler Branch `k12-ship-reports` ab `master`): „Ships and cargo grids“ Z. 140–142
     raus, Türlimit wie oben. Kein Merge, kein Push – mit dem nächsten Tag; Zeile im Backlog „Wiki `k12-ship-reports`
     mit dem nächsten Release mergen“.
4. **`K12: code signing stays off`** – README Z. 32–33 raus, Schritt 3 bleibt. Backlog „Setup.exe und/oder
   Code-Signing“: Stand – SignPath Foundation abgelehnt 19.11.2026 (Kriterien: Stars/Forks/Contributors, externe
   Artikel, Reddit/YouTube-Referenzen); Martins Entscheidung: unsigniert bleiben, keine bezahlte Signatur. **Auslöser
   neu:** (a) erster Nutzer-Report, dass SmartScreen ihn abhält, oder (b) genug Sichtbarkeit für einen SignPath-Reapply.
   Liste „dann nachziehen“ bleibt (SignPath-Zeile der README entfällt dort). D109 unverändert.
5. **`K12: backlog II tasks`** – neuer Abschnitt **„## Backlog II (19.11.2026)“** nach diesem, je Task Warum-Zeile und
   prüfbare Akzeptanz, Text wie im Startprompt: **K11** TASKS.md-Archivierung, **B16** Legende: Missionsreihenfolge,
   **B17** Sektionen: Lücke bei Rest-Einzelkiste, **F10** Seitenweise Planung; F8 bleibt zurückgestellt (Zeile
   unverändert), **F11** steht dort als `[x]` mit Verweis hierher. **A1:** die Backlog-Zeile „Zweiseitige Sektionen
   (Backbord-/Steuerbord-Hälfte). Auslöser: ein Schiff braucht es.“ ist der Vorläufer von F10 → „→ F10“; die Zeilen
   „Sektionsreihenfolge per Legenden-Drag“ und „Optionale Spieler-Reihenfolge“ betreffen Stopps, nicht Missionsnummern
   (B16), und bleiben.
6. **`F11: D115 no Linux overlay port`** – `docs/decisions.md` D115. Kontext: Anfrage nach Linux; das Overlay ist WPF
   plus Win32 (Click-through per Fensterstil `WS_EX_TRANSPARENT` D11, Hotkey-Polling `GetAsyncKeyState` D97,
   Cursor-Erkennung `GetCursorInfo`, GDI-Capture fürs Scannen D100). Entscheidung: kein Overlay-Port; `CargoPlan.Cli`
   (`plan`, `scan <png>`, `ships --version`) als `linux-x64`-Asset. Begründung kopierfähig: unter Wayland gibt es für
   Click-through und globales Tastatur-Polling keine Entsprechung, ein Port wäre ein zweites Projekt. Gegenargument:
   Linux-Spieler bleiben ohne Overlay, und Avalonia hätte das am Anfang gekonnt (D2). Konsequenz: Tarball je Release,
   gebaut von `publish.ps1`, ohne Velopack (kein Selbst-Update, Linux-Nutzer laden neu). CLAUDE.md: Zeilen
   „Distribution“ und „CI/CD“, Befehl `publish.ps1`.
7. **`F11: Linux CLI tarball`** – `build/publish.ps1`, Reihenfolge Cli → App für `win-x64` bleibt, danach:
   - `dotnet publish src/CargoPlan.Cli -c Release -r linux-x64 --self-contained true -o out/publish/linux/SC-CargoPlan-Cli
     -p:Version=<v>`, `LICENSE` dazu (MIT: Lizenz mit jeder Kopie).
   - mtree-Liste (Ordner 0755, Dateien 0644, `CargoPlan.Cli` und `createdump` 0755, uid/gid 0), dann
     `& "$env:SystemRoot\System32\tar.exe" -czf out/release/SC-CargoPlan-Cli-v<v>-linux-x64.tar.gz @<liste>` – voller
     Pfad, weil auf dem Runner Gits GNU tar im PATH stehen kann, und das liest kein mtree. Wurzelordner im Tarball
     `SC-CargoPlan-Cli/` (Linux-Brauch: ein Tarball entpackt nicht ins aktuelle Verzeichnis; Gegenargument: der
     Windows-ZIP hat keinen, D109 – aber dort entscheidet Velopacks Layout). `.sha256` wie beim ZIP. Quellen als
     Kommentar: libarchive `mtree(5)`, `bsdtar(1)` (A3).
   - Prüfung im Skript nach dem Packen (`tar -tvzf`): `SC-CargoPlan-Cli/CargoPlan.Cli` mit `rwxr-xr-x`,
     `ship-data/manifest.json`, 4 Dateien in `models/v5/`, `LICENSE`; einmal provoziert rot (Modus 0644), Lehre T11.3.
   - `release.yml`: keine Änderung nötig – `files: out/release/*` lädt den Tarball mit; Velopack liest nur
     `releases.win.json`. Kommentar am Schritt ergänzen. CONTRIBUTING „Release“ (Z. 49, 57): Tarball nennen.
8. **`F11: CI checks the Linux CLI`** – `ci.yml` nach **Rückfrage R2**.
9. **`F11: README Linux line`** – unter Install, z. B.: *Linux: no overlay ([D115](docs/decisions.md)). Releases after
   v0.6.0 carry the command-line planner, `SC-CargoPlan-Cli-v<version>-linux-x64.tar.gz` (`plan`, `scan <screenshot>`;
   x64, glibc, libicu).* „after v0.6.0“, weil das Asset erst mit dem nächsten Tag erscheint.
10. **Probe:** `./build/publish.ps1 -Version 0.6.1-k12` (ohne Delta) → Layout-Prüfung grün, Tarball mit `rwxr-xr-x`; WSL:
    `--version`, `ships --version`, `plan`, und `scan` über **alle** PNGs aus `sc-ocr-samples/set` Linux gegen Windows
    byte-gleich (Zahl N/N in TASKS.md). `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` im sauberen
    Worktree (Lehre T11.4). Draft-PR nur für CI (Lehre V5), grün → Merge `--no-ff` lokal, Martin pusht.

**Übergabe an die nächste Shipdata-Session (dort nicht angefasst, ein Repo pro Task):** Türlimit von Hand hat keinen Weg
mehr (`maxCrateSize` aus dem letzten Release, kein Override-Feld). Anlass: erster Türlimit-Report.

**Rückfragen:**
- **R1 Alte offene Haken:** T9.3, R5 (v0.3.0), T10.1, T10.6 mit je einem Vermerk auf `[x]`? Empfehlung **ja** – sonst
  zeigt der Akzeptanz-Grep vier erledigte Tasks als offen, und K11 archiviert sie ohnehin. Gegenargument: nicht im
  Auftrag.
- **R2 Linux-CLI in der CI.** Der Auftrag sagt „im Ubuntu-Job mitbauen“. `publish.ps1` läuft dort nicht (`vpk.exe`,
  `win-x64`, bsdtar mit mtree), und das Release-Asset entsteht auf `windows-latest`.
  - **(a) Empfehlung:** der Windows-Job packt wie das Release (`publish.ps1`) und reicht den Tarball per
    `actions/upload-artifact` (Aufbewahrung 1 Tag) an einen neuen Job auf `ubuntu-latest`, der ihn mit
    `actions/download-artifact` holt, entpackt und `--version`, `ships --version`, `plan` startet. Prüft genau das Asset,
    das Nutzer bekommen, Modus eingeschlossen. Gegenargument: zwei neue Actions (GitHub-eigen, gepinnt, Dependabot-Gruppe
    `github-actions`), ein Job mehr, 63 MiB Artefakt je CI-Lauf.
  - (b) wörtlich: der Ubuntu-Job ruft selbst `dotnet publish -r linux-x64` und startet die CLI. Keine neue Action, aber
    eine zweite Publish-Zeile neben `publish.ps1` (Lehre „ein Pack-Skript“), und der Tarball samt Modus bleibt ungeprüft.
  - (c) nur die Listing-Prüfung in `publish.ps1` (Windows-Job), Ubuntu unverändert. Billig, aber kein Start unter Linux
    in der CI.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. Schiffsdatei-PR-Linie auch für README Z. 58, CONTRIBUTING „Contribute a ship“, `ship.yml` und „Checklist for a new
   ship PR“ – dieselbe Zusage an mehr Stellen als im Auftrag.
2. Türlimit-Sätze in CONTRIBUTING und `ship-correction.yml` auf „melden“; Befund an die Shipdata-Session.
3. Wiki-Änderung als lokaler Branch `k12-ship-reports`, Merge und Push mit dem nächsten Tag.
4. `scan` gehört zum Linux-Build (Spike: byte-gleich zu Windows), kein „F11-Rest“.
5. Tarball per bsdtar + mtree (Spike), Wurzelordner `SC-CargoPlan-Cli/`, mit `.sha256` und Listing-Prüfung im Skript.
6. `release.yml` bleibt unverändert bis auf einen Kommentar (`out/release/*` nimmt den Tarball mit).
7. F11 steht in „Backlog II“ als `[x]` mit Verweis auf K12, damit alle vier Feedback-Punkte an einem Ort stehen.

**Freigabe (Backlog-Chat, 19.11.2026):** R1 ja, alle vier mit Vermerk auf `[x]`. R2 **(a)**. Dazu:
- **E1** NOTICE wie im Windows-ZIP neben `LICENSE` in den Tarball, Listing-Prüfung erweitern.
- **E2** D115 und README-Zeile: die Linux-CLI hat kein App- und kein Daten-Update, `ships --version` nennt die Baseline
  des Release.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (aus E1/E2):**
8. **E1:** Der Windows-ZIP hat keine NOTICE in der Wurzel; `check-portable.ps1` verlangt `ship-data\NOTICE.md` und
   `models\NOTICE.md` neben `LICENSE`. Beide liegen im Linux-Publish schon (Spike). Die Listing-Prüfung des Tarballs
   verlangt deshalb dieselbe Liste wie `check-portable.ps1` (`LICENSE`, beide NOTICE, Manifest, `hull_a.json`,
   `ts_module.json`, `outlines.json`, vier Modelldateien), die nativen Bibliotheken unter Linux-Namen
   (`libonnxruntime.so`, `libSkiaSharp.so`) und `CargoPlan.Cli` mit `rwxr-xr-x`.
9. **E2:** Ein Datenupdate lädt nur das Overlay herunter; die Linux-CLI liest einen geladenen Stand nur, wenn er im
   Profil liegt (`~/.local/share/SC-CargoPlan/ships`), und den schreibt unter Linux nichts. Neuere Schiffsdaten heißt
   also: das nächste Tarball laden.

**Wiki (19.11.):** Martin hat `master` gepusht (`5a1ab1d`); `k12-ship-reports` setzt darauf auf.

**Umsetzung (CC, 19.11.2026):** Commits `bcd674c` (R5, Phase 12, R1), `f2f1669` (Schiffsdatei-PRs), `7e58081`
(Code-Signing), `b80966d` (Backlog II), `b157566` (D115), `750b44e` (Tarball), `9de2e2e` (CI), `91fdf2e` (README); Wiki
lokal `k12-ship-reports` `06b4004` (nicht gemergt, Backlog-Zeile „nächster Tag“).

**Messwerte:**
- `./build/publish.ps1 -Version 0.6.1-k12` lokal (PS 5.1): 24 s gesamt; `Layout check: … passed (388 files in current\,
  110 ship files of ships-2026.11.1, 5 PDBs, version 0.6.1-k12)`; `Tarball check: SC-CargoPlan-Cli-v0.6.1-k12-linux-x64.tar.gz
  passed (326 files in SC-CargoPlan-Cli/, 110 ship files of ships-2026.11.1, CargoPlan.Cli executable, version
  0.6.1-k12)`. Tarball **66 360 872 B**, `.sha256` 111 B; Windows-ZIP unverändert 96 761 307 B.
- Tarball-Prüfung **dreimal provoziert rot:** (1) Ordner per bsdtar ohne mtree gepackt → jede Datei `-rw-rw-rw-`,
  `CargoPlan.Cli` nicht ausführbar; (2) mtree ohne `models/NOTICE.md` → „missing“ und „not in the tarball“; (3) `-Version
  0.6.2` → „CargoPlan.Cli.dll has the version '0.6.1-k12+…', not 0.6.2“.
- **WSL Ubuntu 24.04.2, Tarball aus `out/release`:** ein Wurzelordner `SC-CargoPlan-Cli/`, `CargoPlan.Cli` `-rwxr-xr-x`
  ohne `chmod`; `--version` → `0.6.1-k12+b157566…`; `ships --version` → `ships-2026.11.1 (baseline) …`; `plan
  example_missions.json` = Windows (166 B); **`scan` über alle 42 Aufnahmen aus `sc-ocr-samples/set`: 42/42 Exit 0, 42/42
  byte-gleich zu `CargoPlan.Cli.exe` desselben Builds** (65 259 B nach CRLF → LF; Linux 64 s, Windows 87 s).
- Sauberer Worktree auf `91fdf2e`, SDK 10.0.404: Build `0 Warnung(en)`, `0 Fehler`; `dotnet format --verify-no-changes`
  Exit 0; Tests `Bestanden! … erfolgreich: 1913, … gesamt: 1913 - CargoPlan.Core.Tests.dll`, `… 280 … Import`, `… 34 …
  Ocr`, `… 2079 … CargoPlan.App.Tests.dll` – dieselben Zahlen wie beim v0.6.0-Merge, App-Tests unverändert.
- Draft-PR [#30](https://github.com/MRiediger/sc-cargoplan/pull/30) nur für CI, Run 44519400677: `Build & test
  (ubuntu-latest)` 1 min 54 s, `Build & test (windows-latest)` 3 min 17 s (darin `Tarball check: … passed (326 files …,
  version 0.0.1-ci)`, Artefakt `cli-linux-x64`), **`Linux CLI from the release tarball` 12 s**: `-rwxr-xr-x … CargoPlan.Cli`,
  `version: 0.0.1-ci+7f4f0c5…`, `ships: ships-2026.11.1 (baseline) …`, Plan Rack 1–4.

**Präzisierungen beim Umsetzen:**
- Eigenes Skript `build/check-cli-tarball.ps1` statt einer Prüfung in `publish.ps1`: wie `check-portable.ps1` einzeln
  gegen einen von Hand gepackten Tarball aufrufbar (so wurde es rot provoziert). Die Version liest es aus der
  `ProductVersion` von `CargoPlan.Cli.dll`, weil Linux-Code unter Windows nicht startet.
- `ship-format.md`: „Correcting a rack“ heißt jetzt „Moving a rack“ (Formatbeispiel für Testdateien und
  `--ship-data-source`), CONTRIBUTING „Contribute a ship“ → „Missing a ship?“; kein Link zeigte auf die alten Anker (Grep).
- `upload-artifact@v7` packt per Default in ein ZIP (`archive: true`), `download-artifact@v8` entpackt es; die Modi trägt
  der Tarball. `jq` ist im Runner-Image (`Ubuntu2404-Readme.md`: jq 1.7), als Quelle im YAML.

**Akzeptanz:**
- [x] `grep -n '^### \[ \]' TASKS.md` zeigt nur K11, B16, B17, F10 (F11 ohne Rest: `scan` läuft unter Linux)
- [x] README ohne SignPath-Absatz; README, CONTRIBUTING, `ship-format.md`, `ship-correction.yml` und `ship.yml`
  versprechen keinen Schiffsdatei-PR mehr (Grep `pull request` nur noch mit „no pull requests“)
- [x] `build/publish.ps1` lokal: Tarball entsteht, `./CargoPlan.Cli --version` unter WSL liefert die Version; `ci.yml`
  grün inkl. Start unter Ubuntu (Run 44519400677)
- [x] Alle Tests grün, App-Tests unverändert (2079)
- [x] Commits `K12: …` bzw. `F11: …`, kein `Co-Authored-By`; Merge `--no-ff` (CC), Martin pusht

---

