# Archiv: Phase 11 – nach dem Launch (v0.5.0)

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Phase 11 – nach dem Launch (abgeschlossen 15.11.2026 mit v0.5.0)

### [x] S11.0 Velopack-Spike (13.11.2026, kein Prod-Code, abgenommen 13.11.)

**Branches:** Spike-Code auf `spike/s11-velopack` (Worktree `..\sc-cargoplan-s11`, zwei Commits: A = eigenes `Main` + Spike-Log,
B = Velopack; wird nach der Abnahme gelöscht), dieser Befund auf `spike/s11-velopack-finding`. Laufordner
`..\sc-cargoplan-s11-run` (Pakete, Feed, Logs, Belege in `evidence\`), außerhalb des Repos.
**Frage:** Kann sich das Portable-ZIP mit **Velopack** selbst aktualisieren, ohne Nutzerdaten, `ships\`, Cli, Startschalter,
Admin-Lauf und Startzeit zu gefährden? **Versionen:** NuGet `Velopack` **1.2.161** (eigenes `net10.0`-Target), `vpk`
**1.2.161** (`dotnet tool install -g vpk --version 1.2.161`); 1.3.x gibt es nur als Pre-Release.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. G2 im **zweiten Windows-Konto** (Windows Sandbox ist nicht installiert), Ergebnis über `C:\Users\Public\s11\`.
2. „Klick“ = automatischer Apply direkt nach dem Check (`CARGOPLAN_SPIKE_APPLY=1`), kein UI. Feed per
   `CARGOPLAN_SPIKE_UPDATES`, Notice über die Zeile `Outlines` (Wegwerf).
3. Spike-Log `spike-s11.log` in der Portable-Wurzel statt `AppLog`, das vor `--data-dir` aufs Profil zeigt. Jeder Lauf mit
   `--data-dir`, Profil-Hashes (`settings.json`, `.bak`, `plan.json`) nach jedem Gate verglichen.
4. K1: `%LocalAppData%\CargoPlan\` und `%Temp%` vor dem ersten Start und nach dem Apply belegt (G1, G5).
5. K2: G2 mit gültigen Dummy-Kopien aus `data\`, App-Start im Testkonto mit `--data-dir`, Existenz des Nachbarordners nach
   dem Uninstall.
6. E1/E2: Schreibrecht der Portable-Wurzel und Feed/Upload aus Doku und Quelltext 1.2.161.
7. G8 zusätzlich „Klick auf die Exe → erster Frame“, weil Velopack vor `Main` eine Stub-Kette schaltet.

**Befunde in der Durchführung:**
- Velopacks WPF-Sample (`Main(string[])` in `App`, `<StartupObject>…App</StartupObject>`) baut bei uns **nicht**: **CS0017**,
  weil WPF `Main()` in dieselbe Klasse generiert und `StartupObject` nur eine Klasse wählt. Lösung: eigene Klasse
  `Program` mit `[STAThread] Main`, `<StartupObject>CargoPlan.App.Program</StartupObject>`.
- `vpk pack` **überschreibt** im Ausgabeordner `CargoPlan-win-Portable.zip` und `-Setup.exe` mit der jüngsten Version.
  Der Feed wurde neu gebaut, Portable/Setup jeder Version sofort weggesichert.
- Zwei Builds desselben Codes ergeben **verschiedene nupkg** (Hash ungleich). Ein Delta muss gegen das **veröffentlichte**
  Full-nupkg der Vorversion gebaut werden.
- **G2 brauchte vier Läufe.** Lauf 1 wurde durch frühes Abmelden abgebrochen. Die Läufe 2 und 3 hingen, weil **Setup.exe
  1.2.161 abstürzt, sobald nach `--` App-Argumente folgen** (`--data-dir`): Rust-Panic in clap („Mismatch between definition
  and access of `EXE_ARGS`“, Setup-Log `--verbose`), danach läuft Setup ohne Fenster endlos und installiert nichts. Upstream
  [velopack#1039](https://github.com/velopack/velopack/issues/1039), geschlossen am 10.10.2026, in 1.2.161 noch vorhanden.
  Lauf 4 ohne App-Argumente bestand. Reste eines früheren Installs mit App-Start ohne `--data-dir` (`cargoplan.log`, Holo-Cache
  im Dummy-Ordner) hat Lauf 2 per Uninstall und Neuanlage entfernt; dieser Uninstall nannte im Velopack-Log ebenfalls nur
  `CargoPlan\` und den Temp-Ordner. Belege: `C:\Users\Public\s11\g2-result-145748.txt`, `g2-setup-*.log`,
  `g2-velopack_CargoPlan-*.log`.

**Gates:**

| Gate | Ergebnis | Beobachtung |
|---|---|---|
| G1 Portable aktualisiert sich selbst | **bestanden** | 0.9.0 → 0.9.1 im selben Ordner, ohne Setup, ohne Admin; `ApplyUpdatesAndRestart` startet die App neu (`build B`, `--data-dir` über `restartArgs`); Klick → neues `Main` 2,0 s, → erster Frame 2,8 s (Feed lokal) |
| G1/G5 Portable schreibt ins Profil (K1) | **ja, nur Log** | `%LocalAppData%\CargoPlan\` entsteht nicht; Velopack schreibt `%LocalAppData%\velopack\velopack_CargoPlan.log` (gemeinsamer Ordner aller Velopack-Apps), `%Temp%` nichts (`Temp\velopack` stammt von `vpk pack`) |
| G2 packId ≠ Datenordner | **bestanden** | Testkonto `cargotest`, `packId = CargoPlan`: `Setup.exe --silent` (1 s) und `Update.exe --uninstall --silent` lassen `%LocalAppData%\SC-CargoPlan\` stehen, `settings.json`/`plan.json` in allen Stufen byteidentisch; Uninstall entfernt nur `CargoPlan\`, `Temp\velopack_CargoPlan`, Verknüpfungen (Desktop, Startmenü) und den Uninstall-Eintrag; Setup im Silent-Modus startet die App nicht, nur den Install-Hook |
| G3 Daten, `ships\`, Cli | **bestanden** | `settings.json`/`plan.json` in `data\` byteidentisch; `current\ships\` 106 = Publish-Ordner; `current\CargoPlan.Cli.exe --version` = `0.9.1+e3db8dc`; Profil unverändert |
| G4 Hook-Reihenfolge | **bestanden** | `--veloapp-updated 0.9.1`: 113 ms, Exit 0, nur die Hook-Zeile, kein `after Run`, Profil unverändert (Start ohne `--data-dir`); Update.exe ruft vor dem Tausch die alte Exe mit `--veloapp-obsolete`, danach die neue mit `--veloapp-updated`, beide enden in `Run()`; über den Stub kommen `--locked` (`WS_EX_TRANSPARENT` gesetzt), `--plan` (plan.json übernommen), `--data-dir`, `--measure-frames empty --over-game` an, je ein `after Run`; kein `settings.json.bak` |
| G5 Admin-Lauf | **bestanden, UAC-Abfrage nicht beobachtbar** | alter und neuer Prozess `elevated=True`, Tausch ok; `ConsentPromptBehaviorAdmin = 0` auf diesem Rechner (Erhöhung ohne Abfrage), ob eine zweite Abfrage käme, ist nicht beobachtet; Update.exe und Neustart erben das Token |
| G6 Größen | gemessen | Tabelle unten; Portable +4,1 MB gegenüber dem heutigen ZIP, Delta 165 KB |
| G7 GitHub-Quelle | recherchiert | Stichpunkte unten |
| G8 Startzeit | **bestanden** | Main → erster Frame +12 / +17 ms (Median, zwei Reihen à 3); Klick → erster Frame +37 / +42 ms; Check-Ergebnis 14–16 ms **nach** dem ersten Frame, nie blockierend |

**G6 Größen** (Bytes aus `..\sc-cargoplan-s11-run`, MB dezimal wie in CLAUDE.md):

| Artefakt | Bytes | MB |
|---|---|---|
| Release-ZIP v0.4.0 (Referenz, lokal) | – | 85,2 |
| `publish.ps1`-ZIP 0.9.0 mit `Velopack.dll` | 85.329.674 | 85,3 |
| **Velopack Portable-ZIP** 0.9.0 | 89.473.215 | **89,5** |
| **Full-nupkg** 0.9.1 | 89.517.239 | **89,5** |
| **Delta-nupkg** 0.9.0 → 0.9.1 (Textmarker + Version; 11 von 379 Dateien gepatcht) | 164.606 | **0,16** |
| Setup.exe 0.9.0 (nur G2) | 97.075.367 | 97,1 |

- Velopack bringt unter .NET 10 nur `Velopack.dll` (279 KB) mit. Portable = App + `Update.exe` (4,0 MB) + Stub (0,45 MB),
  ohne `*.pdb` und `createdump.exe` (Default-`--exclude` von `vpk`).
- Pack-Zeit je Version 5–7 s.

**G8 Startzeit** (Release, warm, gleicher Datenordner; A = eigenes `Main` ohne Velopack, B = 0.9.0-Portable mit `Run()` und
Check gegen den lokalen Feed, der 0.9.1 findet):

| Messung | A heute | B Velopack | Δ |
|---|---|---|---|
| Main → erster Frame, Reihe 1 (Median aus 3) | 785 ms | 802 ms | +17 ms |
| Main → erster Frame, Reihe 2 (Median aus 3) | 778 ms | 790 ms | +12 ms |
| Klick → erster Frame, Reihe 1 (B über den Stub) | 821 ms | 863 ms | +42 ms |
| Klick → erster Frame, Reihe 2 | 815 ms | 852 ms | +37 ms |
| Check fertig nach dem ersten Frame | – | 14–16 ms | – |

- `Run()` selbst kostet 30–50 ms (`Run took …`), überlappt aber mit dem WPF-Start. Netto bleiben +12 bis +17 ms.
- Die Stub-Kette (`SC-CargoPlan.exe` → `Update.exe start` → `current\CargoPlan.App.exe`) kostet weitere ≈ 25 ms vor `Main`.
- Kaltstart (erster Lauf nach dem Build): A 1371 ms, B 1333 ms Klick → Frame, im Rauschen.

**G7/E2 GitHub-Quelle** (Quelltext `src/lib-csharp/Sources/GithubSource.cs`, `GitBase.cs`, `HttpClientFileDownloader.cs`, Tag
1.2.161; `vpk … --help`):
- `new GithubSource(repoUrl, accessToken: null, prerelease: false)`: `prerelease: false` filtert nach GitHubs
  **Release-Flag** `prerelease`, nicht nach dem Tag-Namen. RCs müssen als Pre-Release veröffentlicht werden.
- **Ein API-Aufruf je Check:** `GET /repos/{owner}/{repo}/releases?per_page=10&page=1`, sortiert nach `published_at`. Es zählen
  nur die **10 zuletzt veröffentlichten Releases**, Holo- und Pre-Releases eingeschlossen. Kommen mehr als 10 Nicht-App-Releases
  nach dem letzten App-Release, findet der Check nichts.
- Je Release wird das Asset `releases.{channel}.json` gesucht, Channel unter Windows per Default `win` →
  **`releases.win.json`**. Fehlt es (`holo-v1`, `holo-v2`, v0.1.0–v0.4.0), wirft `GetAssetUrlFromName`, `GitBase` fängt und
  **überspringt das Release**.
- Die Version kommt aus `releases.win.json` (`vpk pack -v`), **nicht aus dem Tag**. Tag `v0.5.0` mit `-v 0.5.0` passt.
- Unauthentifiziert: 60 Anfragen/h je IP, ein Check = 1 API-Anfrage. JSON und Pakete laden über `browser_download_url` und
  zählen nicht. User-Agent `Velopack/1.2.161`, keine Nutzerdaten. Die `.betaId` (Staging-GUID in `packages\`) nutzt `GitBase` nicht.
- **403/Rate-Limit/offline:** `EnsureSuccessStatusCode` → `HttpRequestException` aus `CheckForUpdatesAsync()`. Die Methode
  hat **keinen CancellationToken**, der Download-Timeout steht per Default auf 30 min.
- Ein vollständiger Feed je GitHub-Release: **`releases.win.json` + Full-nupkg** (Pflicht), Delta-nupkg (optional),
  `CargoPlan-win-Portable.zip` (für Menschen), `Setup.exe`, `RELEASES` (Squirrel-Altformat), `assets.win.json` (Liste für
  `vpk upload`).
- `vpk upload github --repoUrl --token --tag --releaseName --pre --merge --publish` lädt `assets.win.json` hoch;
  `vpk download github --repoUrl [--pre]` holt das letzte Release in den Ausgabeordner (Basis für das Delta).

**E1 Schreibrecht der Portable-Wurzel** (`WindowsVelopackLocator.cs`, `apply_windows_impl.rs`):
- Schreibbar: `packages\` neben `current\` (beobachtet: `packages\CargoPlan-0.9.1-full.nupkg`, `.betaId`, `.velopack_lock`,
  `VelopackTemp\`).
- **Nicht schreibbar** (`C:\Program Files\…`, Share): Rückfall auf **`%LocalAppData%\{packId}\packages`** plus Kopie von
  `Update.exe` dort, **auch bei Portable**. Den Tausch startet Update.exe dann **selbst erhöht (UAC)** und wartet bis zu 10 min.
  Läuft der Prozess schon als Admin und die Wurzel ist trotzdem nicht schreibbar: Abbruch „The update cannot continue.“

**Designfolgen für T11.1–T11.3:**
- **`packId = CargoPlan`**, nie `SC-CargoPlan` und später nie mehr ändern (neue packId = neue App, alte Installationen finden
  kein Update). Begründung aus dem Quelltext:
  - Setup löscht beim Install eine nicht leere `%LocalAppData%\{packId}` (Rückfrage, mit `--silent` automatisch Ja),
    Uninstall leert sie (`remove_dir_contents`).
  - Portable legt bei nicht schreibbarer Wurzel `packages\` dorthin.
  - Das gilt **für jedes Paket**, nicht nur für Setup.exe.
- **Ort von `Run()`:** eigene Klasse `Program` mit `[STAThread] Main`, `<StartupObject>CargoPlan.App.Program</StartupObject>`.
  `VelopackApp.Build().Run()` als erste Zeile, dann `new App()`, `InitializeComponent()`, `Run()`. `StartOptions` ignoriert
  `--veloapp-*` schon heute. Fast-Callbacks haben 15 s (Update) bzw. 30 s (Install/Uninstall), wir brauchen keinen.
- **Portable statt Setup:**
  - Layout neu: Wurzel mit `SC-CargoPlan.exe` (Stub, Name aus `--packTitle`), `Update.exe`, `.portable`, `current\`, nach dem
    ersten Update `packages\`. Der Hauler startet den Stub.
  - Das Release-ZIP wird das Velopack-Portable-ZIP (+4,1 MB). `publish.ps1` bleibt der Publish-Schritt, seine ZIP-Prüfung
    (ships, outlines, models) muss dann `current\…` im Portable prüfen.
  - Ohne `.pdb` haben Crash-Stacks im `cargoplan.log` keine Zeilennummern mehr. Ob per `--exclude` mitliefern, entscheidet T11.1.
- **Erstes Update ist immer voll** (89,5 MB): Das Portable-ZIP hat kein Basis-nupkg („no local/base package … delta updates will
  be disabled“). Danach liegt das Full-nupkg in `packages\`, und Deltas greifen (165 KB im Spike).
- **Release-Workflow (T11.3):** `vpk download github` (letztes stabiles Release) → `vpk pack` → `vpk upload github --tag v… --merge`
  oder unser Asset-Upload mit derselben Dateiliste. Ohne den Download-Schritt gibt es kein Delta, ein neu gebautes nupkg passt
  nicht byteweise. Holo-Releases zählen zu den 10 abgefragten.
- **Pre-Releases:** `prerelease: false`, RC-Tags als GitHub-Pre-Release veröffentlichen. Alte Releases ≤ v0.4.0 haben keinen
  Feed: **v0.5.0 holt jeder Hauler einmal von Hand**, erst ab dann aktualisiert es sich selbst.
- **Check (T11.2):** nach dem ersten Frame auf dem Pool-Thread mit eigenem Timeout (z. B. `Task.WaitAsync`). Fehler und 403
  nur ins Log, Notice nur bei „Update da“. Das ist der **zweite Netzzugriff** neben `HoloSource` und braucht laut CLAUDE.md einen
  eigenen D-Eintrag und einen Schalter in settings.json (Schema-Bump mit Migration).
- **Apply (T11.2):** `ApplyUpdatesAndRestart` endet sofort per `Environment.Exit`, ohne `Closed`-Handler. Vorher Settings- und
  Plan-Flush, oder `WaitExitThenApplyUpdates` + `Shutdown()`. `restartArgs` = eigene Startargumente (`--data-dir`, `--locked`).
  Update.exe beendet vor dem Tausch **jeden Prozess aus dem Ordner** (auch eine laufende `CargoPlan.Cli.exe`).
- **Notice-Fälle (T11.2):** Wurzel nicht schreibbar → UAC-Abfrage beim Tausch, bei Ablehnung Fehler; schon Admin + nicht
  schreibbar → Tausch unmöglich, Text „folder not writable, download the ZIP“.

- **Setup.exe nicht mit App-Argumenten aufrufen** (velopack#1039 in 1.2.161). Falls Setup je angeboten wird: ohne `--`,
  oder erst mit einer Velopack-Version, in der der Fix nachweislich steckt.

**Offene Risiken:**
- Update.exe meldete beide Male „Failed to wait for process … Zugriff verweigert … Continuing“ und tauschte trotzdem sauber,
  weil `ApplyUpdatesAndRestart` schon beendet hatte. Bei einer langsam beendenden App (Flush vor dem Exit) würde Force-stop
  sie töten. In T11.2 mit `WaitExitThenApplyUpdates` prüfen.
- UAC mit Abfrage (G5) und SmartScreen für Stub und `Update.exe` aus einem heruntergeladenen ZIP sind nicht geprüft.
- Velopack setzt die AppUserModelID `velopack.CargoPlan`: Die Taskleisten-Gruppierung ändert sich gegenüber v0.4.0.
- Alles neben der Exe geht beim Tausch verloren (`measure-frames.log`, lokal geänderte `ships\`-Dateien). Ships kommen laut
  `docs/ship-format.md` nur über das Repo, Risiko klein und heute beim neuen ZIP gleich.

**Empfehlung:** Weiter mit **Velopack-Portable**: Der Selbsttausch, die Daten, `ships\`, Cli, Startschalter und Admin-Lauf
halten, und der Start wird nur um 12–17 ms (Klick: 37–42 ms) langsamer. Setup.exe bringt Hauler nichts, was Portable nicht
kann, ist 8 MB größer, berührt mit Install/Uninstall `%LocalAppData%\{packId}` und hängt in 1.2.161 bei App-Argumenten. Ein Rückfall auf „Notice mit Download-Link“
ist nicht nötig, bleibt aber der Weg für Wurzeln ohne Schreibrecht.

### [x] T11.0 Doku – D109, Phase 11 neu, CLAUDE.md A14 (13.11.2026, `2db4063`, abgenommen mit T11.1)

Branch `feat/p11-auto-update`, erster Commit. **D109** „CargoPlan updates itself via Velopack (portable)“ mit Martins
Entscheidungen E1–E10 aus dem Prompt (Velopack 1.2.161 gepinnt, Portable-ZIP ohne Setup.exe, `packId = CargoPlan`,
`GithubSource` ohne Pre-Releases, ein Check pro Start nach dem ersten Frame mit 10 s, kein Check für Dev-Builds und bei
`checkOnStartup = false`, Skip = diese Version, stiller Fehlschlag, Download erst auf Klick, `--updates-source`).
Addenda an D63 (zweiter Netzzugriff) und D105 (`updates` ohne Schema-Schritt). CLAUDE.md: A14 „Velopack zuerst“, Zeile
„Netz“ nennt zwei Zugriffe.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. Kein Schema-Bump: `updates` ist ein optionaler Abschnitt nach D31 (Martins Antwort auf die Rückfrage), D105-Addendum
   statt Migration.
2. PDBs gehen ins Velopack-Paket (Martins Antwort), Umsetzung T11.3.
3. Das 10-Releases-Fenster von `GithubSource` ist aus dem Quelltext 1.2.161 belegt (D109): Pre-Releases und Releases
   ohne `releases.win.json` zählen **vor** dem Filter mit. Regel in T11.3.
4. CLAUDE.md-Zeile „Netz“ nennt jetzt zwei Zugriffe (der Prompt nannte nur A14).

### [x] T11.1 Integration – eigenes `Main`, `UpdateGate`, `UpdateService`, `updates` in settings.json, Notice als Text (13.11.2026, `cff9c7b`, abgenommen 13.11.)

Alles in `CargoPlan.App`, Core unverändert. Check nach dem ersten Frame, Ergebnis nur als Text `Update available: v…`
(`NoticeSource.AppUpdate`), kein Download.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. Kein Schema-Bump (T11.0 Punkt 1). Gate 5 dazu angepasst.
2. `NuGet.Versioning` kommt nicht mit Velopack (1.2.161 hat für `net10.0` keine Abhängigkeiten und bringt einen eigenen
   `Velopack.SemanticVersion`). `UpdateGate` parst selbst: `System.Version` für `x.y.z`, `-…` = Pre-Release, `+…` fällt
   weg, bei gleicher Zahl stable > Pre-Release. Urteilt Velopack anders, gilt Velopack (D109).
3. `UpdateGate` hat zwei reine Funktionen: `ShouldCheck` vor dem Netz, `Offer` danach. Mit `checkOnStartup = false`
   entsteht kein `UpdateManager`: kein Netz, kein Velopack-Log, keine Logzeile.
4. Die Quelle heißt `NoticeSource.AppUpdate` (es gibt schon `HoloUpdate`) und steht nach `HoloUpdate`. Die endgültige
   Lage entscheidet T11.2.
5. Dauerhafte Logzeile mit der Zeit vom Prozessstart bis zum ersten Frame, für das Startzeit-Gate. Die Basislinie `main`
   misst ein Wegwerf-Worktree mit nur dieser Zeile.
6. Gate-Laufordner neu (`..\sc-cargoplan-p11-run`), der aus S11.0 ist gelöscht.
7. `SetAutoApplyOnStartup(false)`: Ein liegengebliebenes Paket heißt, dass etwas schiefging (UAC abgelehnt, Wurzel nicht
   schreibbar, App beim Download beendet). Ein stiller Tausch mit Neustart oder eine UAC-Abfrage bei jedem Start wäre
   schlimmer als ein erneutes Angebot.
8. (Freigabe E1) `UpdateManager` entsteht auf dem Pool. Ein `CancellationTokenSource` cancelt bei `Closed`/`Exit`,
   `SetNotice` nur ohne `Dispatcher.HasShutdownStarted`. Cancel = eine Logzeile „cancelled“, keine Notice.

**Befunde in der Umsetzung:**
- Ein leerer Feed-Ordner wirft in Velopack nicht, `CheckForUpdatesAsync` liefert null: Logzeile „nothing newer than
  0.9.0“ statt „check failed“. Ebenso GitHub heute, weil noch kein Release `releases.win.json` hat.
- Start aus einem Ordner ohne Velopack-Paket (Publish-Ordner, heutiges ZIP): eine Zeile „no check: not a Velopack
  package“, kein Check. Dev-Build: `VelopackApp.Run()` läuft ohne Paket durch, keine Update-Zeile.
- Punkt 6 belegt: `current\sq.version` sagt `0.9.0`, `CargoPlan.App.dll` und `CargoPlan.Cli --version` sagen
  `0.9.0+2db4063…`. Ohne `+sha` gleich; weicht es ab, schreibt `UpdateService` eine eigene Zeile (T11.3 prüft es).
- `vpk pack` lässt die PDBs weg (0 in `current\`): die T11.3-Notiz gilt.
- `HoloRelease.UserAgentVersion` liest jetzt `AppVersion.Current` (eine Stelle statt zwei, A1).

**Gate-Anleitung (Martin)** – alles im Laufordner `..\sc-cargoplan-p11-run` (CC hat jeden Schritt einmal ohne SC laufen
lassen, Ergebnis jeweils dahinter). Inhalt: `portable-0.9.0\` (entpacktes Velopack-Portable, Stub `SC-CargoPlan.exe`),
`releases\` (Feed mit 0.9.0, 0.9.1 voll und Delta), `empty-feed\`, `pub-main\` (`main` + nur die Startzeile, Basislinie),
`gate-run.ps1` (startet, wartet 8 s, nimmt das Overlay als PNG auf, schließt, zeigt das Log), `measure-start.ps1`.
Jeder Lauf hat einen eigenen `--data-dir` im Laufordner; das Profil bleibt unberührt. Velopack schreibt trotzdem
`%LocalAppData%\velopack\velopack_CargoPlan.log` (D109).

```powershell
cd ..\sc-cargoplan-p11-run
$exe = "$PWD\portable-0.9.0\SC-CargoPlan.exe"
```

1. **Notice mit SC im Vordergrund.** SC starten, in den Vordergrund, dann aus einer zweiten Konsole:
   `.\gate-run.ps1 -Exe $exe -DataDir "$PWD\data-g1" -Arguments @('--updates-source', "$PWD\releases") -Name g1`.
   Erwartet: Overlay erscheint, danach unten links amber `Update available: v0.9.1`; Log `Update: 0.9.1 available
   (installed 0.9.0)` nach `Start: overlay first frame …`. *CC: Zeile 19–92 ms nach dem Frame, Notice im Bild (`g1.png`).*
   **Startzeit:** `.\measure-start.ps1` (SC im Vordergrund lassen, ≈ 2 min, Finger weg von Maus und Tastatur). Erwartet:
   Δ B − A ≤ 50 ms im Median. *CC ohne SC: Prozessstart → Frame +9 / +21 ms, Klick → Frame +33 / +48 ms (A 629 / 621,
   B 662 / 669 ms). Klick liegt knapp an der Grenze.*
2. **Skip und Aus.**
   `.\gate-run.ps1 -Exe $exe -DataDir "$PWD\data-g2a" -Arguments @('--updates-source', "$PWD\releases") -Settings '{ "schemaVersion": 3, "updates": { "checkOnStartup": true, "skippedVersion": "0.9.1" } }' -Name g2a`
   → keine Notice, Log `Update: 0.9.1 skipped (updates.skippedVersion)`.
   `.\gate-run.ps1 -Exe $exe -DataDir "$PWD\data-g2b" -Arguments @('--updates-source', "$PWD\releases") -Settings '{ "schemaVersion": 3, "updates": { "checkOnStartup": false } }' -Name g2b`
   → keine Notice, **keine** Zeile `Update:`. *CC: beides so, `g2a.png` ohne Notice.*
3. **Dev-Build** aus Visual Studio (F5, Debug) oder `dotnet run --project src/CargoPlan.App -- --data-dir <Ordner>
   --updates-source <…\releases>` → keine Notice, keine Zeile `Update:`. *CC (`bin\Debug`): so.*
4. **Quelle nicht erreichbar.**
   `.\gate-run.ps1 -Exe $exe -DataDir "$PWD\data-g4" -Arguments @('--updates-source', "$PWD\empty-feed") -Name g4`
   → keine Notice, eine Zeile (`Update: nothing newer than 0.9.0`, s. Befund). Dann **ohne Netz** (WLAN aus oder
   Kabel ab) ohne Schalter: `.\gate-run.ps1 -Exe $exe -DataDir "$PWD\data-g4b" -Name g4b` → keine Notice, eine Zeile
   `Update: check failed: HttpRequestException: …`. *CC: leerer Ordner so; offline nicht geprüft (kein Netz-Aus ohne
   Admin), mit Netz ohne Schalter `Update: nothing newer than 0.9.0` (echte GitHub-Anfrage, 0,6 s).*
5. **settings.json von `main`.** Eine echte v0.4.0-Datei (z. B. Kopie der eigenen aus dem Profil) nach
   `data-g5\settings.json`, dann `Start-Process $exe -ArgumentList '--data-dir', "$PWD\data-g5"`. Erwartet: keine
   Settings-Notice, Datei nach dem Start unverändert, kein `.bak`. Overlay verschieben → `settings.json` enthält
   `"updates": { "checkOnStartup": true, "skippedVersion": null }`, `settings.json.bak` = die alte Datei; noch einmal
   verschieben → `.bak` bleibt gleich. *CC (mit der Datei aus `main`s Fixture `t10.3_scan`, Verschieben per
   `SetWindowPos`): so.*

**Abnahme (Martin, 13.11.2026): alle Gates bestanden.**
- [x] Gate 1 Notice nach dem Overlay, SC im Vordergrund; Startzeit Δ ≤ 50 ms (Martins Zahlen nicht notiert; CC ohne SC
  s. oben)
- [x] Gate 2 `skippedVersion` → keine Notice, Zeile „skipped“; `checkOnStartup: false` → keine Notice, keine Zeile
- [x] Gate 3 Dev-Build → keine Notice
- [x] Gate 4 leerer Feed und ohne Netz → keine Notice, eine Zeile
- [x] Gate 5 settings.json von `main` lädt, `updates` und `.bak` beim ersten Speichern
- [x] Build ohne Warnungen, alle Tests grün (Core 1772, Import 280, Ocr 34, App 1888), `dotnet format` sauber
- Nächster Schritt: T11.2 mit eigenem Prompt. Merge `--no-ff` erst am Ende der Phase.

### [x] T11.2 Notice mit Update und Skip, Abschnitt „Updates“ im Settings-Fenster, Download + Apply (13.11.2026, `370306b`, `295a7a8`, abgenommen 14.11.)

- Notice `Update available: v… · [Update] [Skip]` (Optik = Martin). Skip schreibt `updates.skippedVersion`.
  Settings-Abschnitt „Updates“: `checkOnStartup`, übersprungene Version zurücksetzen.
- Apply über `WaitExitThenApplyUpdates` + `Shutdown()` statt `ApplyUpdatesAndRestart` (endet per `Environment.Exit`
  ohne `Closed`), damit `Closed` Settings und Plan flusht. Velopacks Wartegrenze (60 s) im Plan nennen, ebenso das
  Risiko Force-stop (S11.0). `restartArgs` = eigene Startargumente (`--data-dir`, `--locked`, `--updates-source`).
- Notice-Fälle: Wurzel nicht schreibbar → UAC-Abfrage beim Tausch, bei Ablehnung Fehlertext; schon Admin und nicht
  schreibbar → „folder not writable, download the ZIP“.
- Prüfen: Verwendet `DownloadUpdatesAsync` ein Paket aus `packages\` wieder (T11.1, `SetAutoApplyOnStartup(false)`)?
  Kann das Velopack-Log per `VelopackApp.SetLogger` in `cargoplan.log`?
- **Gate-Zeile UAC (Freigabe E3):** Auf Martins Rechner gibt es keine UAC-Abfrage (`ConsentPromptBehaviorAdmin = 0`),
  G5 hat den Hauler-Fall nicht abgedeckt. Testkonto `cargotest` **als Standardnutzer neu anlegen, nicht als Admin**
  (das alte ist gelöscht). Dort das Portable-Update einmal unter `C:\Program Files\…` (nicht schreibbar, UAC-Abfrage
  erwartet) und einmal unter `%UserProfile%\…` (schreibbar, keine Abfrage) durchspielen.

**Umsetzung (13.11.2026)**, alles in `CargoPlan.App`, Core unverändert, D109-Addendum „T11.2“. Martins Entscheidungen
U1–U12 aus dem Prompt.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. Die Update-Zeile ist ein **eigenes Element** unter den Notices (`OverlayViewModel.Update`, `UpdateViewModel`), kein
   Listeneintrag; `NoticeSource.AppUpdate` entfällt. Die Notices bleiben Text, ihre acht Testdateien unberührt; ein
   XAML-Test hält ihr Template fest.
2. **UAC wird zugelassen** (Martins Antwort): Nicht schreibbar und nicht erhöht → App schließt, `Update.exe` fragt.
   `folder not writable` nur, wenn die App schon erhöht läuft und trotzdem nicht schreiben kann (Prüfung vor dem
   Download, App läuft weiter). Gate 5b entsprechend.
3. „cancelled“ zeigt die **neu gestartete alte App**: `Update.exe` startet sie nach einem gescheiterten Tausch mit
   denselben Argumenten (`apply.rs` 57–61); erkannt an `OnRestarted` + liegendem neuerem Paket.
4. `restartArgs` **ohne `--plan <pfad>`** (Martins Antwort).
5. `no network` auch für einen fehlenden lokalen Feed (Gate 6); HTTP-Antworten (403, 404) → `see cargoplan.log`.
6. Check now zeigt eine Statuszeile im Abschnitt; ein Fund setzt zusätzlich die Overlay-Zeile.
7. `silent: false`: Velopacks Fortschrittsfenster beim Tausch.
8. `SetLogger` schreibt Velopacks Zeilen ab Information als `Velopack:` nach `cargoplan.log`, ab dem Setzen des
   Datenordners; Velopacks eigene Datei bleibt.
9. (Freigabe E1) Nach einem UAC-Tausch startet der **nicht erhöhte** Eltern-`Update.exe` die App: Sie läuft nicht
   erhöht. Die Startzeile endet dauerhaft mit `, elevated: yes|no`.
10. (Freigabe E2) Vor `Shutdown()` wartet das Apply auf das Ende eines modalen Dialogs (`ComponentDispatcher.IsThreadModal`),
    dann schließen Editor und Settings mit ihrem eigenen `Close`. Kein Fenster behandelt `Closing`. Der Editor-Entwurf
    geht wie beim Exit (D19).
11. (Freigabe E3) Ein neueres Angebot ersetzt `Failed`, `Downloading`/`Applying` bleiben unberührt.

**Befunde in der Umsetzung:**
- `AsyncRelayCommand` überwacht seine Aufgabe in einem `async void` (`MonitorTask`); xUnit wartet auf jedes `async void`
  eines Tests. Ein Test, der einen Download offen lässt, hält den ganzen Lauf an (erst als „Testhost abgestürzt“
  gemeldet, Exitcode 143 = mein `timeout`). Die Tests schließen ihn darum ab (`Finish`).
- `SimpleFileSource` kopiert ohne Blick auf den Token: Ein Download bei schon schließender App kam als „fertig“ zurück.
  `UpdateService` prüft den Token jetzt vor und nach dem Download selbst.
- `AcquireLockFailedException` hat in 1.2.161 nur `internal`-Konstruktoren; der Test baut sie per Reflection wie
  Velopack selbst.
- UIA: Das Lock-Fenster hängt als Kind unter dem Overlay (owned window). Toggle einer Checkbox per UIA löst ihren
  Command nicht aus (`OnToggle`, nicht `OnClick`): Skip geht per Maus und Tastatur nicht, per Screenreader-Toggle nicht.
  Im Settings-Fenster heißen die Hotkey-Buttons und der Updates-Button beide „Clear“.
- Paket-Wiederverwendung belegt: Retry nach gescheitertem Tausch schreibt `0.9.1 is downloaded already`, kein Download.

**Gate-Anleitung (Martin)** – Laufordner `..\sc-cargoplan-p11-run`, neu gebaut aus dem T11.2-Stand mit
`.\build-gate.ps1` (0.9.0 und 0.9.1 publish + `vpk pack`, Full 89,5 MB, Delta 121 KB, `portable-0.9.0\` entpackt).
Helfer: `click-update.ps1 -Show` zeigt die Update-Zeile per UIA. CC hat jeden Schritt ohne SC laufen lassen (Klicks per
UIA bzw. eine Maus-Klick-Simulation), Ergebnis kursiv dahinter.

```powershell
cd ..\sc-cargoplan-p11-run
$gate = "$env:UserProfile\CargoPlan-gate"
```

1. **Voller Durchlauf, SC im Vordergrund.** `Copy-Item -Recurse .\portable-0.9.0 $gate`, dann
   `Start-Process "$gate\SC-CargoPlan.exe" -ArgumentList '--locked', '--data-dir', "$PWD\data-m1", '--updates-source', "$PWD\releases"`.
   Overlay erscheint gesperrt mit amber `Update available · v0.9.1`, **ohne** Button und Checkbox. Entsperren (Schloss),
   im Editor einen Plan anlegen, einen Stopp abhaken, die Ansicht drehen, das Overlay verschieben, dann **Update** →
   `Downloading update · … %` (lokal kurz) → `Installing update · restarting` → Overlay weg, Velopack-Fenster → 0.9.1
   startet **gesperrt**, Plan, Haken, Kamera und Position wie vor dem Klick. `Get-Content .\data-m1\cargoplan.log`:
   `Update: applying 0.9.1 … restarting with [--locked --data-dir … --updates-source …]`, zweite Zeile
   `Start: … elevated: no`, `Update: restarted by Update.exe as 0.9.1`, `Velopack:`-Zeilen dazwischen.
   *CC (ohne SC, Lock per UIA gelöst, verschoben per `SetWindowPos` auf 321,123 px): so; neue PID mit genau diesen
   Argumenten aus `current\CargoPlan.App.exe`, Version `0.9.1+…`, `settings.json` `left 290,7 / top 111,4` (= 321,123 px
   bei 106 % DPI). `%LocalAppData%\CargoPlan` entsteht nicht.*
2. **Lock.** In 1 enthalten (gesperrt nur Text, entsperrt Controls). Zusätzlich: SC hat die Maus (Auto-Lock) → Controls
   weg, Text bleibt; Maus frei → Controls zurück. *CC: gesperrt fehlen Button und Checkbox im UIA-Baum, entsperrt da;
   Auto-Lock ohne SC nicht prüfbar.*
3. **Skip.** `$gate` löschen, neu kopieren (es ist jetzt 0.9.1), Start ohne `--locked` mit `data-m3`. **Skip this
   version** anhaken → Zeile weg, `.\data-m3\settings.json` hat `"skippedVersion": "0.9.1"` (nach ½ s). Overlay schließen,
   neu starten → keine Zeile, Log `0.9.1 skipped`. Zahnrad → **Updates**: `Skipped version: v0.9.1 [Clear]` → **Clear**
   (Zeile verschwindet) → **Check now** → Status `Update available: v0.9.1`, Overlay-Zeile wieder da; `settings.json`
   `"skippedVersion": null`. *CC: so (Skip per Mausklick-Simulation, Clear/Check now per UIA).*
4. **Dev-Build.** `dotnet run --project src/CargoPlan.App -- --data-dir <voller Pfad> --updates-source <Laufordner>\releases`
   → Zahnrad → Updates → **Check now** → `Dev build – updates disabled`, keine Overlay-Zeile. *CC (`bin\Debug`): so.*
5. **Standardnutzer `cargotest`** (neu, kein Admin, UAC an). Vorher als Martin: `New-Item -ItemType Directory C:\Users\Public\p11`,
   `Copy-Item -Recurse .\portable-0.9.0, .\releases C:\Users\Public\p11\` (das Profil `MartinRiediger` kann `cargotest`
   nicht lesen).
   - a. Als `cargotest`: `Copy-Item -Recurse C:\Users\Public\p11\portable-0.9.0 $env:UserProfile\CargoPlan`, Start mit
     `--data-dir $env:UserProfile\cg-a --updates-source C:\Users\Public\p11\releases` → **Update** → keine UAC-Abfrage,
     0.9.1 startet.
   - b. Als Martin (Admin): `Copy-Item -Recurse C:\Users\Public\p11\portable-0.9.0 'C:\Program Files\CargoPlan-gate'`. Als
     `cargotest`: `'C:\Program Files\CargoPlan-gate\SC-CargoPlan.exe'` mit `--data-dir $env:UserProfile\cg-b
     --updates-source C:\Users\Public\p11\releases` → **Update** → Download (nach `%LocalAppData%\CargoPlan\packages`
     von `cargotest`) → App schließt → UAC fragt nach Admin-Anmeldedaten → **Nein** → die alte App kommt mit denselben
     Argumenten zurück: `Update failed · cancelled` + **Retry**. **Retry** → wieder UAC (Log `0.9.1 is downloaded already`,
     kein Download) → diesmal **Martins Anmeldedaten** → 0.9.1 startet; Startzeile `elevated: no` (E1),
     `%LocalAppData%\velopack\velopack_CargoPlan.log` von `cargotest`: `Elevated process has run successfully.`
     *CC: nicht ausführbar (anderes Konto, UAC). Nachgestellt ohne UAC: neueres Paket in `packages\` +
     `VELOPACK_RESTART=true` → `Update failed · see cargoplan.log` (Wurzel schreibbar) + Retry → `0.9.1 is downloaded
     already` → Tausch → 0.9.1; der Startcheck überschrieb den Fehler nicht.*
   - c. Optional, als Martin: `icacls $gate /deny '*S-1-5-32-544:(OI)(CI)(W)'` auf eine frische Kopie, Stub per
     „Als Administrator ausführen“ → **Update** → sofort `Update failed · folder not writable`, App läuft weiter, kein
     Download. Danach `icacls $gate /remove:d '*S-1-5-32-544'`. *CC: nicht ausführbar (Erhöhung); Unit-Test deckt es.*
6. **Netz weg.** `Copy-Item -Recurse .\releases .\releases-m6`, Start mit `--data-dir "$PWD\data-m6" --updates-source
   "$PWD\releases-m6"`, nach der Zeile `Rename-Item .\releases-m6 releases-m6-gone` → **Update** → `Update failed · no
   network`, **Retry** → dasselbe. Mit GitHub-Quelle erst ab v0.5.0 möglich (heute hat kein Release einen Feed).
   *CC: so, je eine Logzeile `download failed (no network): Exception: The file … does not exist.`*
7. **Startzeit.** `.\measure-start.ps1`, SC im Vordergrund, ≈ 2 min Finger weg. B (Portable mit T11.2) gegen das T11.1-Gate
   vergleichen. *CC ohne SC: B Klick → Frame 669 / 670 ms (T11.1: 662 / 669), Prozessstart → Frame 640 / 642 ms;
   A `main` 617 / 616 ms (T11.1: 629 / 621), daher Δ Klick +52 / +54 ms statt +33 / +48.*

**Offen fürs Gate (Optik = Martin):** Umbruch bei schmalem Overlay (Vorschlag: Controls in eine zweite Zeile),
`Hidden` vs. `Collapsed` im Lock, Lage des Abschnitts „Updates“ (jetzt der letzte), Texte von Check now.

**Abnahme (Martin, 13./14.11.2026):**
- [x] Optik am echten Element: „passt wunderbar“. Umbruch, `Hidden`, Lage des Abschnitts und Texte bleiben wie gebaut.
- [x] Gate 1 voller Durchlauf, Neustart mit gleichen Argumenten, Plan, Haken, Kamera und Position nach dem Neustart
  (zweiter Lauf; `plan.json` und `settings.json` lagen vor dem Klick da).
  **Offener Befund, erster Lauf (13.11., 18:17–18:18):** Die Instanz speicherte nichts, weder Plan noch Position;
  `data-m1` enthielt danach nur `cargoplan.log` (Ordner beim Start angelegt, nicht nachträglich geleert), kein
  `holo\`. Nicht das Update (gespeichert wird 0,5 s nach jeder Änderung), im zweiten Lauf und in CCs Nachbau (gesperrt,
  `--plan`, verschoben) nicht wiederholbar. Tritt es wieder auf: vor dem Update-Klick `Get-ChildItem` des Datenordners.
- [x] Gate 2 Lock und Auto-Lock
- [x] Gate 3 Skip, Neustart, Clear, Check now
- [x] Gate 4 Dev-Build
- [x] Gate 5a `cargotest` im eigenen Profil: keine UAC, 0.9.1.
- [x] Gate 5b `cargotest` unter `Program Files`: UAC abgelehnt → alte App mit `Update failed · cancelled` (13 s nach dem
  Klick); Retry → `0.9.1 is downloaded already`, UAC angenommen → `Elevated process has run successfully.`, 0.9.1 mit
  `elevated: no`. 5c nicht gelaufen (optional, Unit-Test).
- [x] Gate 6 Netz weg
- [x] Gate 7 Startzeit (mit SC): Prozessstart → Frame A 660 / 657, B 666 / 684 ms (Δ +6 / +27); Klick → Frame A 664 / 661,
  B 730 / 747 ms (Δ +66 / +86). Der Klick-Anteil über 50 ms ist der Velopack-Stub (B Klick − Prozess ≈ 63 ms mit SC,
  29 ms ohne), seit T11.1 unverändert; T11.1-Zahlen mit SC nicht notiert. **Akzeptiert (Martin, 14.11.):** bekannte
  Kosten von Velopack-Portable, kein eigener Punkt.
- Gate-Ordner aufgeräumt (`Program Files\CargoPlan-gate`, `Public\p11`, `CargoPlan-gate`); `cargotest` bleibt.
- Nächster Schritt: T11.3 mit eigenem Prompt. Merge `--no-ff` erst am Ende der Phase.
- [x] Build ohne Warnungen, alle Tests grün (Core 1772, Import 280, Ocr 34, App 1958), `dotnet format` sauber

### [x] T11.3 `release.yml` mit Velopack (14.11.2026, `51b5ed4`, `a079af1`, `2c79dab`, Gate-Fixes `6c9e667`, `e7d06bb`, abgenommen 15.11.)

- `vpk download github` (letztes stabiles Release, Basis für das Delta) → `publish.ps1 -Version` → `vpk pack -u CargoPlan
  -v <dieselbe Version>` (Prüfschritt: `sq.version` = Assembly-Version ohne `+sha`) → Upload von `releases.win.json`,
  Full- und Delta-nupkg, Portable-ZIP, SHA256. `vpk` 1.2.161 gepinnt, Dependabot-Gruppe Velopack + `vpk`.
- PDBs ins Paket (T11.0 Punkt 2): `--exclude` so setzen, dass `*.pdb` bleiben; Syntax gegen die `vpk`-Doku prüfen.
- Die ZIP-Prüfung von `publish.ps1` (ships, outlines, models) prüft im Portable-ZIP unter `current\`.
- **Release-Hygiene (D109, Fenster aus dem Quelltext):** `GithubSource` liest nur die erste API-Seite mit 10 Releases,
  Pre-Releases und Releases ohne `releases.win.json` (Holo) zählen vor dem Filter mit. Regel: Unter den 10 neuesten
  GitHub-Releases steht immer das letzte stabile App-Release. Darum rc-Pre-Releases nach dem stable Tag löschen,
  höchstens 2 Holo-Releases gleichzeitig, nie mehr als 9 Pre- oder Nicht-App-Releases nach dem letzten stable Release.
  Steht auch in `CONTRIBUTING.md` („Release“). Heute 7 Releases (v0.1.0–v0.4.0, holo-v1, holo-v2), mit rc.1 + rc.2 sind
  es 9.
- CLAUDE.md: Zeile „Distribution“ (Portable-Layout, Stub `SC-CargoPlan.exe`, `current\`, ZIP-Größe).

**Umsetzung (14.11.2026):** Martins Entscheidungen R1–R12 aus dem Prompt, D109-Addendum „T11.3“, D49-Addendum. Belegt
aus `vpk … -H` 1.2.161 und dem Quelltext am Tag 1.2.161 (`GitBase.cs`, `UpdateManager.cs`, `PackageBuilder.cs`,
`_GitRelease.cs`, `_DownloadCommandRunner.cs`).
- `build/publish.ps1 -Version <v> [-DeltaBase github|<full.nupkg>]`: Publish wie bisher, `vpk` in der Version der
  `Velopack`-PackageReference nach `out\tools\vpk-<v>\`, Delta-Basis, `vpk pack`, Release-Dateien nach `out\release\`,
  dann `build/check-portable.ps1`, SHA256.
- `release.yml`: `publish.ps1 -Version $env:VERSION -DeltaBase github` mit `VPK_TOKEN` nur in diesem Schritt, softprops
  lädt `out/release/*`. `ci.yml`: unter Windows `publish.ps1 -Version 0.0.1-ci` ohne Basis und ohne Upload.
  `dependabot.yml`: nur ein Kommentar (Velopack ist schon in der Gruppe `nuget`).
- App: `--updates-prerelease` (`StartOptions`, `UpdateService.CreateSource`), Logzeile je Check.
- Doku: `docs/update-testing.md` (neu, Entwickler), `CONTRIBUTING.md` (Publish, Release mit Hygiene), CLAUDE.md
  (Distribution, Netz, Struktur, Befehle), `docs/release-notes/v0.5.0-rc.1.md` und `-rc.2.md`.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. Delta-Basis: stable-Tag → neuestes **stable** Release (R7), rc-Tag → neuestes Release **inkl. Pre-Releases** (`--pre`),
   sonst bekäme ein rc nie ein Delta. Gate 2 prüft „Delta erzeugt“ im rc.2-Log, der Download ist **voll** (frisch
   entpacktes Portable hat keine Basis in `packages\`). Den Delta-Download übers Netz prüft T11.5.
2. Gate 4 über einen **Draft-PR** `feat/p11-auto-update → main`: `ci.yml` läuft nur auf `main` und PRs, und
   `workflow_dispatch` löst GitHub nur aus dem Default-Branch aus.
3. actionlint einmal als offizielles Binary im Scratchpad (nicht im Repo, nicht in der CI).
4. Upload über die bestehende softprops-Action statt `vpk upload github` (Namen aus `assets.win.json`, kein `.sha256`,
   Draft, Body aus dem Paket). Der Basis-Eintrag im hochgeladenen Feed ist harmlos: Ziel ist das höchste Full.
5. `vpk` per `--tool-path`, weder global noch als Manifest; die Version liest das Skript per `-getItem:PackageReference`.
6. `dependabot.yml` funktional unverändert, nur ein Kommentar.
7. Layout-Prüfung als eigenes Skript `build/check-portable.ps1`, damit sie sich an einem manipulierten ZIP provozieren lässt.
8. Release-Dateien in `out\release\`. Delta-Basis, `RELEASES` und `assets.win.json` kommen nicht ins Release.
9. Der Wurzelordner `SC-CargoPlan/` entfällt (D49-Addendum): Velopacks ZIP hat 4 Einträge, es wird nicht neu gezippt.
10. rc-Release-Notes vorab committet (Muster v0.1.0-rc.N). rc.1 und rc.2 auf **denselben Commit**: Die Version
    unterscheidet sich, also ist das Delta nicht leer.
11. `--updates-prerelease` mit `--updates-source` ist ein Problem, der Schalter fällt weg.
12. Die Layout-Prüfung vergleicht die ganze Dateimenge `current\` mit dem Publish-Ordner (bekannt: `createdump.exe` fehlt,
    `sq.version` kommt hinzu, bestätigt im ersten Lauf).
13. (Freigabe E1) In D109 und `docs/update-testing.md`: „Deltas helfen nur, wer genau eine Version zurückliegt; alle
    anderen laden voll.“ Delta-Ketten werden nicht gebaut.
14. (Freigabe E2) Kommentar über `--exclude "(?!)"` in `publish.ps1`: Default `.*\.pdb`, ein nicht leerer Wert ersetzt
    ihn, leer zählt als nicht gesetzt. Die PDB-Zählung in `check-portable.ps1` ist der Wächter.

**Befunde in der Umsetzung:**
- **`--runtime win-x64`** (nicht im Plan): Ohne den Schalter warnt `vpk` „No architecture specified with --runtime,
  defaulting to x86“ und baut **Stub und `Update.exe` als x86** (PE-Machine `014C`, auch schon in S11.0 und T11.2, wo es
  lief). Mit `win-x64` sind beide x64 (`8664`), `sq.version` sagt `<rid>win-x64</rid>`, ZIP +0,35 MB. Gate 2 ist der
  erste Lauf mit x64-Binaries. **Zur Abnahme:** zurück auf x86 ist eine Zeile.
- **`-Version` ist Pflicht** (nicht im Plan): `vpk` lehnt `0.0.0-dev` ab („Invalid package version '0.0.0-dev', it
  must be >= 0.0.1“), gefunden im lokalen Nachlauf des CI-Wegs. Eine zweite Paketversion neben der Build-Version hätte zwei
  Versionen für ein ZIP bedeutet. CI packt `0.0.1-ci`. Der MSBuild-Rückfall auf die Version ist aus `publish.ps1` raus.
- `vpk download github` ohne Treffer: „Found 0 release(s) in remote file“, „No full / applicable release was found to
  download. Aborting.“, Exit 0; das Skript warnt „No delta base found“ und packt ohne Delta (geprüft mit `-Version
  0.9.2-rc.1 -DeltaBase github`, also auch über den `--pre`-Pfad).
- `sq.version` ist ein Nuspec-XML (`id`, `version`, `mainExe`, `rid`). Die Layout-Prüfung liest packId, Version und
  Main-Exe daraus (R3 als Wächter).
- `pwsh` 7 ist lokal nicht installiert; alle lokalen Läufe unter Windows PowerShell 5.1. Den `pwsh`-Lauf belegen Gate 1
  und Gate 4.
- `--updates-prerelease` im echten Paket (`0.0.1-ci`-Portable, `--data-dir` im Scratchpad):
  `Update: checking GitHub with pre-releases (--updates-prerelease)` → `Velopack: … No remote full releases found.` →
  `Update: nothing newer than 0.0.1-ci`.
- actionlint 1.7.12 (Checksumme geprüft) über `ci.yml` und `release.yml`: keine Meldung, Exit 0.
- Build ohne Warnungen, alle Tests grün (Core 1772, Import 280, Ocr 34, App 1965 = +7 für `--updates-prerelease`),
  `dotnet format` sauber.

**Paketgrößen** (lokal, Bytes / MB dezimal):

| Artefakt | Bytes | MB |
|---|---|---|
| Portable-ZIP 0.9.0 (x86-Stub, erster Lauf) | 89.898.845 | 89,9 |
| **Portable-ZIP 0.9.0** (x64) | 90.251.603 | **90,3** |
| **Full-nupkg 0.9.1** | 90.295.611 | **90,3** |
| **Delta-nupkg 0.9.0 → 0.9.1** (16 von 384 Dateien gepatcht) | 168.785 | **0,17** |
| Publish-Ordner (382 Dateien) | – | 185,0 |
| Release-ZIP v0.4.0 (Referenz) | – | 85,2 |

**Provozierte Fehler** (`check-portable.ps1` auf ZIP-Kopien von `0.9.2-rc.1`):
- Ohne `current/ships/100i.json`: `current\ships has 105 files, the publish folder 106` und `not in current\:
  ships\100i.json`.
- Ohne alle 5 PDBs: `not in current\: CargoPlan.App.pdb, CargoPlan.Cli.pdb, CargoPlan.Core.pdb, CargoPlan.Import.pdb,
  CargoPlan.Ocr.pdb` und `current\ has 0 .pdb files, the publish folder 5`.

**Gate-Anleitung (Martin).** Voraussetzung: T11.3 gepusht. Tag auf den letzten T11.3-Commit (der die rc-Notes enthält).
CC hat Gate 1, 2 und 4 nicht ausführen können (Tags und PR sind Martins); lokal liefen die Schritte dahinter jeweils einmal
(oben). Laufordner z. B. `..\sc-cargoplan-p11-run\t113\`, jeder Start mit eigenem `--data-dir`.

```powershell
$gh = "$env:ProgramFiles\GitHub CLI\gh.exe"
```

1. **Pre-Release-Probe.** `git tag v0.5.0-rc.1; git push origin v0.5.0-rc.1` → Workflow „Release“ grün
   (`& $gh run list -L 3`). Im Log von „Publish and pack“: `No full / applicable release was found` + Warnung
   `No delta base found`, `Layout check: SC-CargoPlan-v0.5.0-rc.1-win-x64.zip passed (…)`. Release ist **Pre-release** mit
   genau `SC-CargoPlan-v0.5.0-rc.1-win-x64.zip`, `.zip.sha256`, `CargoPlan-0.5.0-rc.1-full.nupkg`, `releases.win.json`
   (`& $gh release view v0.5.0-rc.1`).
2. **Update übers Netz, SC im Vordergrund.** ZIP herunterladen, in einen leeren Ordner entpacken (z. B.
   `$env:UserProfile\CargoPlan-rc`), `SC-CargoPlan.exe --updates-prerelease --data-dir <voller Pfad>\data-g2` → keine
   Update-Zeile, Log `checking GitHub with pre-releases`, `nothing newer than 0.5.0-rc.1`. Overlay schließen.
   `git tag v0.5.0-rc.2` **auf denselben Commit**, pushen → Workflow grün, im Log `Delta base:
   CargoPlan-0.5.0-rc.1-full.nupkg` und `Delta: CargoPlan-0.5.0-rc.2-delta.nupkg, <Bytes>`; das Release hat zusätzlich
   das Delta, sein `releases.win.json` nennt auch rc.1. App neu starten (gleiche Argumente) → `Update available ·
   v0.5.0-rc.2` → **Update** → Download **voll** (≈ 90 MB; Größe aus `%LocalAppData%\velopack\velopack_CargoPlan.log`
   bzw. den `Velopack:`-Zeilen notieren, dort auch „no local/base package … delta updates will be disabled“) → Neustart
   als rc.2 (`current\CargoPlan.Cli.exe --version` = `0.5.0-rc.2+…`), Log `restarted by Update.exe as 0.5.0-rc.2`.
3. **Stable sieht kein rc.** Die rc.1-ZIP noch einmal frisch entpacken, **ohne** `--updates-prerelease` starten (eigener
   `--data-dir`) → keine Update-Zeile, Log `nothing newer than 0.5.0-rc.1`.
4. **CI mit Pack-Schritt.** Draft-PR `feat/p11-auto-update → main` öffnen
   (`& $gh pr create --draft --base main --head feat/p11-auto-update --title "Phase 11: auto-update" --body "Draft for CI (T11.3 gate 4); merged --no-ff at the end of the phase."`)
   → „Build & test (windows-latest)“ grün inkl. „Publish and pack“ (`Layout check: SC-CargoPlan-v0.0.1-ci-win-x64.zip
   passed`), Ubuntu grün.
5. **Hygiene einmal durchexerzieren (R10).** `& $gh release delete v0.5.0-rc.2 --cleanup-tag -y`,
   `& $gh release delete v0.5.0-rc.1 --cleanup-tag -y`, `git tag -d v0.5.0-rc.1 v0.5.0-rc.2`, `git fetch --prune --prune-tags`;
   `& $gh release list` zeigt wieder 7. Die Notes-Dateien bleiben (D49-Addendum T9.2).

**Befunde im Gate (zwei Fixes, je ein eigener Commit):**
- **`6c9e667` – Versionstest scheiterte am ersten Tag nach T11.1.** `AppVersionTests.A_build_without_a_tag_is_a_dev_build`
  (T11.1) erwartete fest `0.0.0-dev`; `release.yml` testet aber den Build mit `-p:Version=<tag>`. Erster rc.1-Lauf rot im
  Schritt „Test“, kein Release, Tag neu gesetzt. Der Test nimmt jetzt die Version seines eigenen Builds (`-p:Version` gilt für
  alle Projekte). Lokal mit der Testfolge von `release.yml` belegt (Release-Build mit `-p:Version=0.5.0-rc.1`, `dotnet test
  --no-build`).
- **`e7d06bb` – `--updates-prerelease` erreichte das Angebot nicht.** Velopack fand rc.2 („Found newer remote release
  available (0.5.0-rc.1 -> 0.5.0-rc.2)“), danach verwarf `UpdateGate.Offer` es als `0.5.0-rc.2 is a pre-release` (eigenes Netz
  aus T11.1 für ein rc ohne GitHub-Flag). Der Schalter hob nur den Filter von `GithubSource` auf; der Grep in T11.3 suchte den
  Schalter, nicht den Zweck „pre-release“ (A1). `Offer(…, allowPreRelease)`, `UpdateService` reicht den Schalter durch, +4 Tests
  (allein und über einen lokalen Feed mit Velopacks `UpdateManager`). Fix nach Martins Freigabe; die installierte rc.1 hatte
  den Fehler, darum rc.1 und rc.2 gelöscht und beide auf `e7d06bb` neu getaggt.
- Nach dem Neustart steht `checking GitHub with pre-releases` zweimal: Die Zeile kommt je erzeugtem `UpdateManager`, und die
  Prüfung auf einen gescheiterten Tausch erzeugt auch einen. Kosmetisch, bleibt (Abnahme).

**Abnahme (Martin, 15.11.2026):**
- [x] Gate 1 rc.1 (`e7d06bb`): Workflow grün unter `pwsh` 7, Tests grün, „No delta base found“ als Warnung, Layout-Prüfung
  grün (382 Dateien in `current\`, 106 Schiffsdateien, 5 PDBs), Pre-release mit 4 Dateien, ZIP 90.251.162 Byte.
- [x] Gate 2 rc.1 → rc.2 über GitHub, SC im Vordergrund: rc.1 allein `nothing newer`; rc.2 mit `Delta base:
  CargoPlan-0.5.0-rc.1-full.nupkg`, Delta **169.079 Byte** (16 von 384 Dateien), Feed nennt rc.2 Full, rc.2 Delta und rc.1
  (harmlos, belegt). In der App `0.5.0-rc.2 available` → Download **voll** („no local/base package … delta updates will be
  disabled“, `CargoPlan-0.5.0-rc.2-full.nupkg` 90.295.173 Byte in 4,2 s) → Neustart mit `--updates-prerelease --data-dir …`
  als `0.5.0-rc.2+e7d06bb…`, danach liegt das rc.2-Full in `packages\` (Delta-Basis für T11.5).
- [x] Gate 3 frische rc.1 ohne Schalter: keine Zeile, nur `nothing newer than 0.5.0-rc.1`, keine Pre-Release-Zeile (R8).
- [x] Gate 4 Draft-PR #24: CI grün, Windows inkl. „Publish and pack“ (`Layout check: SC-CargoPlan-v0.0.1-ci-win-x64.zip
  passed`), Ubuntu grün.
- [x] Gate 5 rc.1/rc.2 samt Tags gelöscht (remote und lokal), Test-Installationen entfernt, wieder 7 Releases.
- [x] Offene Punkte wie gebaut abgenommen: `--runtime win-x64` (Stub und `Update.exe` x64), `-Version` Pflicht,
  `--updates-prerelease` mit `--updates-source` bleibt ein Problem (Präzisierung 11), doppelte Logzeile bleibt.
- [x] Build ohne Warnungen, alle Tests grün (Core 1772, Import 280, Ocr 34, App 1969), `dotnet format` sauber.
- Nächster Schritt: T11.4 mit eigenem Prompt. Merge `--no-ff` erst am Ende der Phase; der Draft-PR #24 schließt sich dabei.

### [x] T11.4 Migration und Texte (15.11.2026, `2e1bc2b`, `7ee7e6a`, `2f387f9`, Wiki `6ea3281` auf `v0.5.0`, abgenommen 15.11.)

Release-Notes v0.5.0: v0.5.0 einmal von Hand laden, danach aktualisiert es sich selbst; neues Layout (Stub + `current\`);
Settings-Downgrade auf v0.4.0 (D105-Addendum); Taskleisten-Gruppierung; Velopack-Log im Profil. README, Wiki
(Installation, Update, „where data lives“), Nexus-Offenlegung des zweiten Netzzugriffs.

Notizen aus T11.2 für die Texte:
- **„Im Auto-Lock musst du zum Klicken entsperren“**: Die Update-Zeile zeigt im Lock nur den Text; Update, Retry und
  Skip erscheinen erst entsperrt (U2). Solange SC die Maus hat, ist das Overlay auto-gesperrt.
- Ein offener Entwurf im Editor (Zeilen ohne Plan) geht beim Update verloren wie beim Exit (D19).
- Unter `Program Files` fragt das Update per UAC; nach dem Tausch läuft die App wieder normal, nicht als Admin.
  Abgelehnt: Die alte Version startet neu mit `Update failed · cancelled` und Retry.
- Velopack zeigt beim Tausch ein eigenes Fortschrittsfenster (`silent: false`).

Notizen aus T11.3 – Ablauf für heutige v0.4.0-Nutzer (Vorlage, von Martin bestätigt):
1. v0.5.0 einmal von Hand laden: ≤ v0.4.0 hat keinen Feed.
2. Entpacken: **kein Wurzelordner mehr**, 4 Einträge (`SC-CargoPlan.exe`, `Update.exe`, `.portable`, `current\`). In einen
   **neuen** Ordner entpacken, nicht über v0.4.0, sonst bleiben alte Dateien in der Wurzel liegen. „Alle extrahieren“
   schlägt einen Ordner mit dem ZIP-Namen vor.
3. **`SC-CargoPlan.exe`** starten (Stub), nicht `current\CargoPlan.App.exe`. Startschalter gehen durch den Stub
   (`SC-CargoPlan.exe --keep-scans`). Die Cli liegt jetzt unter `current\CargoPlan.Cli.exe`. Angeheftete Verknüpfungen
   zeigen noch auf den alten Ordner, und die Taskleiste gruppiert neu (AUMID `velopack.CargoPlan`). SmartScreen beim
   Stub prüft T11.5.
4. Die Daten bleiben in `%LocalAppData%\SC-CargoPlan\`. `settings.json` bekommt beim ersten Speichern `updates`, die alte
   Datei landet als `.bak` (T11.1 Gate 5). Zurück auf v0.4.0 geht laut D105-Addendum.
5. Der erste Check nach dem ersten Frame findet v0.5.0 = sich selbst und schreibt „nothing newer“. Velopack legt sein Log
   in `%LocalAppData%\velopack\` an.
6. Bei v0.5.1 erscheint die Zeile. Update lädt **voll** (≈ 90 MB, keine Basis), tauscht und startet neu. Ab dann liegt
   das Full in `packages\`, und ab v0.5.2 gibt es Deltas, aber nur für alle, die genau eine Version zurückliegen (D109).
7. Den alten v0.4.0-Ordner löscht der Hauler von Hand.

**Umsetzung (15.11.2026):** reine Text- und Doku-Session, kein App-Code, keine Workflow-Änderung. Martins Entscheidungen
M1–M10 aus dem Prompt; Plan freigegeben mit den Ergänzungen E1–E3. Jede Verhaltensaussage aus D109 (+ Addenda T11.2,
T11.3), dem D105-Addendum T11.0, den Gate-Protokollen S11.0–T11.3 und den UI-Texten im Code (`UpdateViewModel.cs`,
`UpdateFailure.cs`, `UpdateService.cs`, `SettingsWindow.xaml`, `OverlayWindow.xaml`).
- Repo: `docs/release-notes/v0.5.0.md` (neu, Entwurf), `README.md` (Install, neu *Updates*, `--keep-scans` am Stub),
  `docs/nexus/description.bbcode` (*Network access* mit beiden Zugriffen, Known issues, Installation, Version),
  `docs/nexus/checklist.md` (0.5.0, Satz zur Staff-Anfrage), `docs/settings-format.md` (Skip „this version only“, Clear,
  Check now; Holo-Schalter ≠ „kein Netz“), `.github/ISSUE_TEMPLATE/bug.yml` (Pflichtfeld Install-Weg, Velopack-Log),
  `docs/holo-format.md`, `docs/plan-format.md` (je ein Satz), `docs/shots/t114-updates.md` (neu, U1/U2),
  `docs/shots/SHOTLIST.md` (Verweis, Präfix `updates`).
- Wiki-Klon, Branch `v0.5.0` (nicht gepusht): `Updates.md` (neu), `Installation-and-first-start.md` (Install, Ordnerinhalt,
  Update, *Coming from v0.4.0 or older*, Uninstall, Daten-Tabelle), `Troubleshooting.md` (Known issues, Report a bug),
  `Preferences.md` (*Updates*), `Overlay-basics.md`, `Scanning-contracts.md`, `FAQ.md`, `Ships-and-cargo-grids.md`,
  `Home.md`, `_Sidebar.md`, `_Footer.md` („Applies to v0.5.0“).
- D109 ohne Addendum: keine Lücke. Die 89,5 MB im Gegenargument sind der S11.0-Stand, das T11.3-Addendum nennt 90,3 MB.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. Wiki-Seitennamen aus dem Klon: Settings = `Preferences`, Known issues = `Troubleshooting#known-issues`, Installation =
   `Installation-and-first-start`; neue Seite `Updates`.
2. Größe **„about 90 MB“** statt „≈ 89 MB“: Full rc.2 90.295.173 Byte, ZIP rc.1 90.251.162 Byte (T11.3 Gate 1/2).
3. Downgrade: `.bak` hält die v0.4.0-Datei nur nach dem **ersten** v0.5.0-Lauf (`.bak` = Stand vor der ersten Änderung
   des Laufs). Text: vor dem Zurückgehen den `"updates": { … }`-Block am Ende von `settings.json` samt dem Komma am
   Ende der Zeile davor löschen (`updates` ist der letzte Key, ohne das Komma wäre die Datei kein JSON mehr).
   Abgeleitet aus dem D105-Addendum, belegt erst durch Gate 2b (E2).
4. `docs/settings-format.md` hatte den Abschnitt `updates` schon; nur Ergänzungen (Skip „this version only“, Clear,
   Check now behält die übersprungene Version).
5. Weitere veraltete Stellen mitgezogen: README Known issues (`SC-CargoPlan.exe --keep-scans`), Wiki `Overlay-basics`
   (Startschalter am Stub), `Scanning-contracts` (Stub, `current\CargoPlan.Cli.exe`), `FAQ` (zwei Netzzugriffe),
   `Troubleshooting` („No auto-update“ raus), `Preferences` (*Updates*), `docs/holo-format.md` („only network access“),
   `docs/plan-format.md` (`--plan` am Stub), Bug-Template („sync failed“ → „Update failed“). In der Umsetzung zusätzlich
   gefunden: Wiki `Ships-and-cargo-grids` und `docs/settings-format.md` sagten „Download ship holograms off = no network
   access at all“, jetzt „no hologram downloads“ mit Verweis auf den Update-Schalter.
6. Wiki auf Branch `v0.5.0` im Klon (Muster v0.4.0), Merge + Push in T11.5; Home und Footer schon auf v0.5.0.
7. Shotlist `docs/shots/t114-updates.md` plus Verweiszeile in `SHOTLIST.md`, Präfix `updates`, IDs U1/U2.
8. `docs/nexus/staff-request.md` bleibt (abgeschickt, Geschichte); die Checkliste sagt, dass die Anfrage nur den
   Holo-Download nannte und die Beschreibung den Update-Check offenlegt.
9. SmartScreen-Satz am Stub unbelegt übernommen; Reihenfolge in T11.5 (E1).
10. v0.3.x-Nutzer: ein Satz verweist zusätzlich auf „Before you update“ von v0.4.0.
11. README-Zeile „Not affiliated with Motherlode, Fleetyards or CIG“ bleibt wörtlich (M5 zitierte sie anders).
12. Vollständigkeit per `git log v0.4.0..HEAD` (Tabelle unten). Der Dependabot-Bump SDK 10.0.404 (`ed2b9f6`) liegt nur
    auf `main` und kommt mit dem Merge; nicht in den Notes (Runtime-Patch, frühere Notes nennen ihn nie).

**Known issues v0.5.0** (RN = Release-Notes, Wiki = `Troubleshooting#known-issues`):

| # | Kern | Wo | Quelle |
|---|---|---|---|
| 1 | Zurück auf v0.4.0 nach dem ersten Speichern → Defaults + Notice; vorher `updates`-Block löschen | RN, Wiki, Installation | D105-Addendum T11.0, T11.1 Gate 5, Gate 2b (E2) |
| 2 | Ordner unter `Program Files` → UAC; danach normal, nicht als Admin; abgelehnt → alte Version mit `Update failed · cancelled` + Retry ohne neuen Download | RN, Wiki, Nexus | D109-Addendum T11.2 „UAC“, T11.2 Gate 5b |
| 3 | Erstes Update nach dem Umstieg voll (about 90 MB), Deltas danach und nur eine Version zurück | RN, Wiki | D109-Addendum T11.3 „Delta base“, T11.3 Gate 2 |
| 4 | Angeheftete Verknüpfungen zeigen auf den alten Ordner | RN, Wiki, Installation | T11.4-Notiz aus T11.3, Punkt 3 |
| 5 | Taskleiste gruppiert neu | RN, Wiki, Installation | D109 Gegenargument (AUMID `velopack.CargoPlan`), S11.0 Risiken |
| 6 | Velopack-Log `%LocalAppData%\velopack\velopack_CargoPlan.log`, auch mit `--data-dir` | RN, Wiki | D109 Gegenargument, S11.0 G1/G5 |
| 7 | SC als Admin → CargoPlan als Admin (bleibt); Update startet wie gestartet, nach UAC-Tausch nicht erhöht | RN, Wiki | Wiki (bestand), S11.0 G5, D109-Addendum T11.2 „UAC“ |
| 8 | Im Lock (auch Auto-Lock) nur Text; zum Klicken entsperren | RN (What's new), Wiki | D109-Addendum T11.2 „Line“ (U2), T11.4-Notiz aus T11.2 |
| 9 | Ungeplante Editor-Zeilen gehen beim Update verloren wie beim Exit | RN, Wiki | D109-Addendum T11.2 „Apply“ (D19) |
| 10 | Unsigned: Windows warnt beim ersten Start eines heruntergeladenen ZIPs | Wiki, Nexus | v0.4.0-Text, E1 |
| 11 | Eigene Änderungen im Programmordner (z. B. `current\ships`) ersetzt ein Update | Wiki | D109 Gegenargument („Everything else in `current\` is lost“) |

**Vollständigkeit** (`git log v0.4.0..HEAD --oneline`, 19 Commits):

| Commits | Inhalt | In den Notes |
|---|---|---|
| `0642b98`, `864c6d1` | REL v0.4.0 nachgemessen, R5 | – (Doku) |
| `5a65d44`, `063ffc5`, `ab412fa` | S11.0 Spike-Befund | – (kein Prod-Code) |
| `2db4063` | T11.0 D109, Tasks, A14 | – (Doku) |
| `cff9c7b`, `ec8542d` | T11.1 Check nach dem ersten Frame | What's new (Check, Netz, Schalter) |
| `370306b`, `295a7a8`, `6166f36`, `db245b4` | T11.2 Update-Zeile, Skip, Retry, Download + Apply, Settings *Updates* | What's new, Known issues 2, 8, 9 |
| `2a9bb10` | CLAUDE.md DoD | – (Repo-Regel) |
| `51b5ed4`, `a079af1`, `2c79dab`, `6c9e667`, `e7d06bb`, `293ec04` | T11.3 Pack, Release, `--updates-prerelease`, Versionstest | Changes (Layout, Cli, Größe), Known issue 3; `--updates-prerelease` nur Entwickler (`docs/update-testing.md`) |

**Gate-Anleitung (Martin).**

1. **Texte lesen:** `docs/release-notes/v0.5.0.md`, `README.md`, Wiki `Updates.md` und `Installation-and-first-start.md`
   (im Klon, Branch `v0.5.0`), `docs/nexus/description.bbcode`. Freigabe oder Korrekturen.
2. **Anleitung wörtlich befolgen** (SC muss nicht laufen). Vorher Profil und alten Ordner sichern:
   ```powershell
   $bk = "$env:UserProfile\cargoplan-t114-backup"
   Copy-Item -Recurse "$env:LocalAppData\SC-CargoPlan" "$bk\profile"
   Copy-Item -Recurse <dein v0.4.0-Ordner> "$bk\v0.4.0"
   ./build/publish.ps1 -Version 0.5.0-gate.1
   Copy-Item out\release\SC-CargoPlan-v0.5.0-gate.1-win-x64.zip "$env:UserProfile\Downloads\"
   ```
   Dann nur noch nach dem Block „Upgrading from v0.4.0 or older“ der Release-Notes (bzw. README *Install*): Extract All
   in einen neuen Ordner, `SC-CargoPlan.exe`, Settings und Plan da, das Overlay einmal verschieben (damit `updates`
   geschrieben wird), schließen, den alten Ordner löschen. Jede Stelle, an der du nachdenken musst, ist ein Textfehler.
   Ein lokal gebautes ZIP trägt keine Download-Markierung, SmartScreen zeigt sich hier also nicht (prüft T11.5, E1).
3. **Gate 2b (Pflicht, E2):** belegt Known issue 1, ohne das Profil anzufassen.
   ```powershell
   $d = "$env:UserProfile\cargoplan-t114-2b"; New-Item -ItemType Directory $d | Out-Null
   Copy-Item "$env:LocalAppData\SC-CargoPlan\settings.json" $d     # v0.5.0-Datei mit "updates"
   Start-Process "$bk\v0.4.0\CargoPlan.App.exe" -ArgumentList '--data-dir', $d
   ```
   a. Erwartet: amber `Settings not loaded, using defaults (…)`. **Nichts ändern**, Overlay schließen.
   b. In `$d\settings.json` den Block `"updates": { … }` am Dateiende (vier Zeilen) **samt dem Komma am Ende der Zeile
   davor** löschen (Text der Known issue wörtlich befolgen), gleicher Start → **keine** Notice, Overlay an der
   verschobenen Stelle, dein Schiff. Schließen. Scheitert b, wird Known issue 1 umgeschrieben.
4. **Screenshots** nach `docs/shots/t114-updates.md` (U1, U2) in `..\sc-cargoplan.wiki\media\`.
5. **Stichprobe** `git log v0.4.0..HEAD --oneline` gegen die Notes (Tabelle oben).
6. Danach `$bk`, `$d` und den Gate-Ordner aufräumen; das Profil bleibt, wie Schritt 2 es hinterlässt (v0.5.0-Format,
   `.bak` = deine v0.4.0-Datei).

**Entwurf Phase-11-Lehren** (Übernahme in „Lehren“ mit T11.5):
- **Fremd-Framework zuerst als Spike mit Gates am echten Layout.** S11.0 fand vor dem ersten Prod-Commit CS0017, den
  Setup-Hang (velopack#1039), das Löschverhalten je packId und das 10er-Fenster von `GithubSource`.
- **Defaults fremder CLIs: „leer“ kann „nicht gesetzt“ heißen.** `vpk --exclude ""` ließ die PDBs weg (Default
  `.*\.pdb`); `(?!)` matcht nie, und die PDB-Zählung in `check-portable.ps1` ist der Wächter, einmal provoziert.
- **UAC nur im Standardkonto prüfen.** Im Admin-Konto mit `ConsentPromptBehaviorAdmin = 0` kam keine Abfrage (S11.0 G5);
  erst `cargotest` als Standardnutzer zeigte Abfrage, Ablehnung und `cancelled` (T11.2 Gate 5b).
- **A1-Grep nach dem Zweck, nicht nach dem Schalter** (E3). `e7d06bb`: `--updates-prerelease` hob nur einen von zwei
  Pre-Release-Filtern auf (`GithubSource`, nicht `UpdateGate.Offer`); der Grep nach dem Schalternamen fand den zweiten
  nicht, einer nach „pre-release“ hätte ihn gefunden.

**Abnahme (Martin, 15.11.2026):**
- [x] Gate 1 Texte freigegeben. Korrektur aus dem Gate: Downgrade-Satz „and the comma at the end of the line before it“
  (Release-Notes, Wiki Troubleshooting und Installation), weil `updates` der letzte Key ist.
- [x] Gate 2 Umstieg nach der Anleitung (lokales `0.5.0-gate.1`-ZIP): neuer Ordner, `SC-CargoPlan.exe`, Settings und Plan
  da, alter Ordner gelöscht.
- [x] Gate 2b (E2) v0.4.0 mit der v0.5.0-`settings.json`: mit `updates` amber Notice und Defaults; Block samt Komma
  gelöscht → keine Notice, Einstellungen geladen. Known issue 1 belegt.
- [x] Gate 3 U1 `updates-line.png` und U2 `updates-settings.png` im Wiki-`media\`. Martins Entscheidung zum Bild: U1 ohne
  Plan, U2 ohne Check-now-Status; `docs/shots/t114-updates.md` beschreibt die Bilder so.
- [x] Gate 4 Stichprobe `git log v0.4.0..HEAD --oneline` gegen die Notes passt.
- [x] Build ohne Warnungen, alle Tests grün (Core 1772, Import 280, Ocr 34, App 1969).
- Nächster Schritt: T11.5 mit eigenem Prompt (Merge, Tag, Wiki-Push, offene Punkte unten).

### [x] T11.5 Release v0.5.0 (15.11.2026, Merge `00bba94`, Tag `v0.5.0`, Wiki `7debb75`)

Tag, CI-Release, ZIP gemessen, Start aus dem heruntergeladenen ZIP (SmartScreen für Stub und `Update.exe`). Der erste
echte Selbsttausch ist v0.5.0 → v0.5.1.

Offene Punkte aus T11.4:
- Merge `--no-ff` `feat/p11-auto-update` → `main` (schließt Draft-PR #24). Bringt den SDK-Bump 10.0.404 von `main`
  (`ed2b9f6`): SDK vorher installieren (B7).
- Tag `v0.5.0` auf den Merge, `docs/release-notes/v0.5.0.md` ist die Beschreibung.
- **SmartScreen (E1):** Stub und `Update.exe` aus dem Browser-Download prüfen, dazu der erste Start nach einem
  Selbsttausch. Der Satz „More info → Run anyway“ bleibt als sicherer Default zum Tag. Zeigt die Probe keine Warnung:
  `gh release edit v0.5.0 --notes-file docs/release-notes/v0.5.0.md` nach der Korrektur, die Datei mit einem Folge-Commit
  nachziehen (README, Wiki, Nexus ebenso). **Kein zweiter Tag.**
- ZIP messen; „about 90 MB“ in Notes, Wiki und README prüfen.
- Wiki-Branch `v0.5.0` nach `master` mergen und pushen, mit dem Tag.
- Nexus-Upload 0.5.0 nach `docs/nexus/checklist.md` (Staff-Frist 16.11.2026).
- Martin entscheidet: Nexus-Staff nachträglich über den Update-Check informieren ja/nein (P8).
- Erster echter Selbsttausch v0.5.0 → v0.5.1, dabei der Delta-Download übers Netz (T11.3 Präzisierung 1).
- rc-Hygiene erledigt (T11.3 Gate 5: rc.1/rc.2 samt Tags gelöscht, 7 Releases).
- Phase-11-Lehren aus T11.4 nach „Lehren“ übernehmen.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. build/test/format und der Pack-Lauf laufen auf dem **lokalen Merge-Commit** (SDK 10.0.404), nicht auf dem Branch
   (10.0.403): nur so sind sie die Probe dessen, was Tag und Runner bauen, und die Basis für den Runtime-Vergleich (B7).
   Die Dokuprüfung bleibt vor dem Merge auf dem Branch.
2. CC merged den Wiki-Branch `v0.5.0` → `master` lokal mit `--no-ff` (Muster v0.4.0), erst nach der Erstnutzer-Probe,
   Martin pusht.
3. D109 bekommt ein Addendum „Status (T11.5)“ statt eines neuen Status-Felds.
4. SmartScreen für `Update.exe` und den ersten Start nach einem Selbsttausch lässt sich erst mit v0.5.1 prüfen → Backlog.
5. (Freigabe E1) Fehlerpfad, falls `release.yml` am Tag scheitert: unten in der Anleitung und als Satz in
   `CONTRIBUTING.md` („Release“).

**Vorprüfung (15.11.2026, Branch `3e96df1`):**
- `git log v0.4.0..HEAD`: 23 Commits; die 19 der T11.4-Tabelle plus die 4 T11.4-Commits (reine Doku). Unter `src/` seit
  v0.4.0 nur Phase-11-Code; `HoloRelease.cs` ist ein Refactor (User-Agent-Version aus `AppVersion`), nicht sichtbar.
- `release.yml` verlangt `docs/release-notes/v0.5.0.md` (Zeile 36, 74): vorhanden; Tag ohne `-` → kein Pre-Release.
- T11.0–T11.4 `[x]` mit Abnahme, D109 mit Addenda T11.2 und T11.3. „about 90 MB“ einheitlich in Notes und Wiki
  (README nennt keine Größe).

**Merge und Probe (CC, lokal, `00bba94`, nicht gepusht):** `dotnet --version` = 10.0.404.
- Build: `0 Warnung(en)`, `0 Fehler`. `dotnet format --verify-no-changes`: Exit 0.
- Test (sauberer Worktree von `00bba94`):
  `Bestanden! : Fehler: 0, erfolgreich: 280, … gesamt: 280 - CargoPlan.Import.Tests.dll`,
  `… erfolgreich: 34, … gesamt: 34 - CargoPlan.Ocr.Tests.dll`,
  `… erfolgreich: 1968, … gesamt: 1968 - CargoPlan.App.Tests.dll`,
  `… erfolgreich: 1770, … gesamt: 1770 - CargoPlan.Core.Tests.dll`.
  Die Zahlen aus T11.3/T11.4 (Core 1772, App 1969) enthielten drei gitignorte lokale Scratch-Tests (2 Core, 1 App);
  `main` bringt keine Teständerung mit.
- `./build/publish.ps1 -Version 0.5.0 -DeltaBase github`: `WARNUNG: No delta base found; packing without a delta.`,
  `Layout check: SC-CargoPlan-v0.5.0-win-x64.zip passed (382 files in current\, 106 ship files, 5 PDBs, version 0.5.0).`
  Release-Dateien: ZIP 90.261.861, Full-nupkg 90.305.872, `releases.win.json` 252, `.sha256` 98 Byte.
- Lokale B7-Referenz: Runtime `Microsoft.NETCore.App` und `Microsoft.WindowsDesktop.App` **10.0.15**
  (`CargoPlan.App.runtimeconfig.json`), `coreclr.dll` 10.0.1526.53008, Cli `0.5.0+00bba94…`.

**Release geprüft (CC, 15.11.2026, nach Martins Push von `main` und Tag `v0.5.0`):**

| Prüfung | Soll | Ist |
|---|---|---|
| CI auf `main` (Merge-Push) | grün | Run 43741126840 success |
| Workflow „Release“ | grün | Run 43741150620 success, 2 min 41 s |
| Tag | annotiert, auf dem Merge | `tag`, `00bba94` |
| Release | kein Pre-Release, kein Draft, „Latest“ | `isPrerelease false`, `isDraft false`, Latest, 17:29:28 UTC |
| Body | = `docs/release-notes/v0.5.0.md` | identisch (4.493 Zeichen, Vergleich über die API) |
| Assets | ZIP, `.zip.sha256`, Full-nupkg, `releases.win.json`; kein Delta, keine Setup.exe, kein `RELEASES`, kein `assets.win.json` | genau diese 4: ZIP 90.261.314, nupkg 90.305.325, Feed 252, `.sha256` 98 Byte |
| SDK im Runner | 10.0.404 | `dotnet-install: Installed version is 10.0.404` |
| Delta | Warnung, kein Fehler | `No full / applicable release was found`, `##[warning]No delta base found; packing without a delta.` |
| Layout-Prüfung | grün | `passed (382 files in current\, 106 ship files, 5 PDBs, version 0.5.0)` |
| SHA256 ZIP | = `.sha256` | `820eb82d…ad187`, gleich |
| `releases.win.json` | genau ein Full `0.5.0`, `FileName` = Asset | ein Eintrag `Full` `0.5.0` `CargoPlan-0.5.0-full.nupkg`, Size und SHA256 = Asset |
| ZIP-Wurzel | `SC-CargoPlan.exe`, `Update.exe`, `.portable`, `current\` | genau diese, 382 Dateien in `current\` |
| Runtime (B7) | = lokales Pack mit 10.0.404 | beide `Microsoft.NETCore.App` / `WindowsDesktop.App` **10.0.15**, `coreclr.dll` und `PresentationFramework.dll` 10.0.1526.53008 |
| Cli-Version | `0.5.0+00bba94…` | `0.5.0+00bba9416301217703ab4dafc06f5a5447898288`, lokal gleich |
| Größe | „about 90 MB“ | 90,3 MB (ZIP lokal 547 Byte größer: Zeitstempel im Archiv) |
| Hygiene | letztes stable unter den 10 neuesten | 8 Releases, v0.5.0 an der Spitze |

**B7-Lücke geschlossen:** Die Runtime im CI-ZIP ist dieselbe wie im lokalen Pack mit demselben SDK.

**Anleitung (Martin).**

1. `git push` auf `main` → PR #24 schließt sich, `ci.yml` läuft auf `main` (Windows inkl. „Publish and pack“, Ubuntu).
   Erst bei grün weiter (`& "$env:ProgramFiles\GitHub CLI\gh.exe" run list -L 3`).
2. Tag: `git tag -a v0.5.0 -m "v0.5.0"; git push origin v0.5.0`. Danach Workflow „Release“ abwarten, „Workflow durch“ an CC.
   **Scheitert `release.yml`** (Pack, Layout-Prüfung, Upload): Release-Entwurf und Tag löschen
   (`gh release delete v0.5.0 --yes`, `git push --delete origin v0.5.0`, `git tag -d v0.5.0`), Ursache auf `main` beheben,
   **denselben Tag** neu setzen, solange kein Asset öffentlich war. War das Release sichtbar und könnte jemand geladen
   haben, ist ein Fix eine neue Version (v0.5.1).
3. **Erstnutzer-Probe** (nach CCs Release-Prüfung), genau wie ein v0.4.0-Nutzer:
   1. ZIP **im Browser** von der Release-Seite laden (nicht per `gh`/`curl`, sonst fehlt das Mark-of-the-Web).
   2. Explorer → Rechtsklick → „Alle extrahieren“ in einen neuen Ordner unter `%UserProfile%`.
   3. `SC-CargoPlan.exe` starten. **SmartScreen beobachten:** erscheint „Der Computer wurde durch Windows geschützt“,
      Wortlaut und Weg („Weitere Informationen → Trotzdem ausführen“) notieren. Overlay schließen, zweiter Start: kommt
      die Meldung wieder?
   4. Settings und Plan aus `%LocalAppData%\SC-CargoPlan` sind da; `cargoplan.log` zeigt `Update: nothing newer than
      0.5.0`; Settings → Updates → **Check now** → `Up to date`.
   5. Schalter *Check for updates on startup* aus → Overlay schließen, neu starten → keine `Update:`-Check-Zeile im Log.
      Schalter wieder an.
   6. Ergebnis an CC: SmartScreen ja/nein, Wortlaut, zweiter Start.
4. Danach merged CC den Wiki-Branch lokal; du pushst das Wiki (`git push` in `..\sc-cargoplan.wiki`), rufst die Seite
   „Updates“ im Browser auf (Bilder?) und lädst nach `docs/nexus/checklist.md` zu Nexus hoch (*Network access* in der
   Beschreibung, bevor die Datei live geht). Veröffentlichen nach Stand der Staff-Anfrage; T11.5 wartet nicht darauf.

**Erstnutzer-Probe (Martin, 15.11.2026):** bestanden. ZIP im Browser geladen, „Alle extrahieren“, `SC-CargoPlan.exe`:
**SmartScreen beim ersten Start ja, beim zweiten nicht mehr.** Das deckt sich mit den Texten zum Tag (Release-Notes
Schritt 3, README, Nexus „More info → Run anyway“, Wiki „Windows asks once per download“ mit Bild, Known issue
„Unsigned“): keine Korrektur, kein `gh release edit`. Settings und Plan da, Startcheck `nothing newer`, Check now
`Up to date`, Schalter aus → keine Check-Zeile, wieder an.

**Wiki:** CC hat `v0.5.0` lokal mit `--no-ff` nach `master` gemergt (`7debb75`), nach der Probe; Martin pusht.

**Phasenabschluss (CC):** Lehren und Backlog unten, `CONTRIBUTING.md` „Release“ um den Fehlerpfad (E1), D109-Addendum
„Status (T11.5)“.

**Aufräumen (nur gemeldet, nichts gelöscht):** `..\sc-cargoplan-p11-run`, `%UserProfile%\CargoPlan-gate`, der
Probe-Ordner aus der Erstnutzer-Probe, `out\tools\vpk-*` (bleibt, wird wiederverwendet), Testkonto `cargotest` (Martin
entfernt es), Branch `feat/p11-auto-update` lokal und remote (gemergt in `00bba94`).

**Abnahme (Martin, 15.11.2026):**
- [x] `main` gepusht, CI grün (Run 43741126840), PR #24 geschlossen.
- [x] Tag `v0.5.0` gepusht, Workflow „Release“ grün; Release-Prüfung oben ohne Befund, B7-Lücke geschlossen.
- [x] Erstnutzer-Probe bestanden, SmartScreen wie dokumentiert.
- [x] Build ohne Warnungen, alle Tests grün (Core 1770, Import 280, Ocr 34, App 1968; sauberer Worktree), `dotnet format`
  sauber.

---

