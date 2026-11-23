# SC-CargoPlan

Open-Source Ladeplan-Overlay für Star Citizen Hauling (Alpha 4.10.x). Windows-Desktop-App, .NET / WPF.
**Ziel: möglichst viele Hauler nutzen es.** Reichweite und Einfachheit schlagen Eleganz.

## Wer hier arbeitet

Martin, 20+ Jahre IT (PowerShell, M365, Power Platform, Netzwerk). C# ist neu, PowerShell-Hintergrund hilft (gleiche .NET-Typen, ähnliche Syntax). Will den Code verstehen, nicht nur ausführen.

## Arbeitsweise (verbindlich)

- Wir sind per Du. Prägnant, direkt, keine Höflichkeitsfloskeln.
- **Bei jedem Schritt und jedem Befehl in einem Satz erklären, warum er nötig ist.**
- **Unterhaltung Deutsch. Code, Kommentare, Commits, README, UI-Texte: Englisch.** Warum: Open Source mit internationaler Zielgruppe.
- Moderne C#-Syntax (file-scoped namespaces, `record`, Pattern Matching, Collection Expressions, Primary Constructors), knappe Kommentare.
- Bei Mehrdeutigkeit **nachfragen statt raten**.
- Fachbegriffe korrigieren, wenn Martin sie falsch verwendet. Wo C# anders tickt als PowerShell, kurz darauf hinweisen.
- **Ein Task aus `TASKS.md` pro Session.** Vorher Task vorlesen, nachher Akzeptanzkriterien einzeln abhaken.
- **Tests vor Implementierung** bei allem mit Logik (Planer, Modelle, Reader).
- Keine neuen NuGet-Pakete ohne kurze Begründung. Weniger Pakete = kleinere EXE = weniger Angriffsfläche.
- **Session-Branches mit `git merge --no-ff` nach `main` mergen**, erst nach Martins Abnahme. Warum: Die Session bleibt als ein Merge-Commit sichtbar und lässt sich als Ganzes zurücknehmen (Anlass: B1–B5 wurden fast-forwarded).

## Das Problem

Hauling-Aufträge für Covalex sind oft **1 Abholort → n Ziele**. Jeder Container ist intern an Mission + Ziel gebunden. Der Frachtaufzug am Ziel nimmt nur passende Container und **verweigert die ganze Übergabe**, sobald ein fremder Container auf der Plattform liegt. Das Spiel zeigt nicht, welcher Container wohin gehört.

## Der Ansatz

**Nicht erkennen, sondern festlegen.** Vor dem Beladen wird bestimmt, welches Ziel in welchen Schiffsabschnitt kommt. Der Spieler lädt nach Plan, das Overlay hält die Zuordnung fest, am Ziel wird der Block abgehakt.

- **Input:** pro Mission: Ziel → Ware → SCU → maximale Kistengröße (1/2/4/8/16/24/32 SCU), wie im Vertrag; Kistenmix abgeleitet (gierig, größte zuerst), pro Item übersteuerbar (D23)
- **Verarbeitung:** Bin-Packing ohne Reihenfolge (D64): Ziel zusammenhalten, ein Ziel pro Rack, keine Kiste auf fremder Fracht, wenn vermeidbar. Die Route entscheidet der Spieler am Terminal; die App sagt nur, was auf wem liegt. Zellreihenfolge wählbar (`fillRows`, D88); optional Sektionen pro Ziel mit Lücken als Durchgang nach dem Planer (`grouping`, `sectionGap`, D89); optional seitenweise: Rackbreite in zwei Bahnen, erst eine Seite voll (`sides`, `sideOverflow`, D116); auf Schiffen mit Racks links und rechts erst alle Racks einer Seite, Mitte zuletzt (Seitengruppen vor D64, D117).
- **Output:** halbtransparentes Always-on-top-Overlay mit **Click-through als Modus** (Lock, D11), Schiff schematisch, farbige Blöcke pro Ziel mit Label („Riker · 12 SCU Titanium“), abhakbar.

**Routenplaner (Phase 13, D118, D119):** eine Schicht über den F3-Scans, der Klassik-Ablauf bleibt. Reiterleiste „Cargo | Plan“ unter der Kopfzeile (D120; „Plan“ ist der Routes-Reiter, im Code bleibt „Routes“); bei gewähltem Reiter „Plan“ kommt ein Angebot per F3 nicht in die Ladung, sonst wie heute, die Wahl steht in settings.json (`routes.tab`); jeder lesbare Scan schreibt Beobachtungen je Template nach `routes.json` neben plan.json (U3), plan.json bleibt Schema 11.
Hauling-Templates und Stationspositionen kommen aus dem Data-Release von `sc-cargoplan-shipdata` (Abschnitt `hauling` im Manifest, `docs/hauling-data-format.md`); fehlen sie, gibt es den Reiter ohne Templates. Erster Inhalt: die Hafen-Matrix (Pickup × Dropoff, T13.2); in den lokalen Stellar-Blöcken steht der Hub des Boards für die leere Pool-Seite (D121).
Kein Netzzugriff zur Laufzeit dafür, keine API-Calls; der Planer kennt keine Verfügbarkeit, Rewards nur aus Scan oder Template. Design: `docs/design/cargo-routes.md`.

## Domänenfakten

- **MISC Hull A:** 64 SCU, Spindel mit **4 Platten à 16 SCU**, getrennt beladbar (bestätigt). Rack = Ziel ist umsetzbar.
- **Lage:** 2 Seiten × 2 Ebenen, **nicht vorne→hinten**. Rack 1 = Backbord oben, Rack 2 = Backbord unten, Rack 3 = Steuerbord oben, Rack 4 = Steuerbord unten. Rack-Reihenfolge = Planungsreihenfolge, unabhängig von der Lage. Gestapelt wird **von der Spindel nach außen** (Rack 1/2 `+y`, Rack 3/4 `-y`); jedes Rack nennt seine Stapelachse im Schiffs-JSON (D26).
- **Raster:** jede Platte 4 × 2 × 2 Zellen (Length entlang der Spindel × Width quer × Stack weg von der Platte), 1 Zelle = 1 SCU = 1,25 m. Kisten in fester Orientierung: 1 = 1×1×1, 2 = 2×1×1, 4 = 2×2×1, 8 = 2×2×2, 16 = 4×2×2, 24 = 6×2×2, 32 = 8×2×2 (D57). Jede Mischung ≤ 16 passt in eine Platte, deshalb **kein 3D-Packer** (D9).
- Später: Cutlass Black, RAFT, Community-Beiträge für weitere Schiffe.
- Begriffe: **Rack** = Schiffsabschnitt (Platte), **Grid** = Zellraster eines Racks, **Cell** = 1 SCU, **Position** = Seite + Ebene (Spieler-Sprache), **Origin** = Min-Ecke des Racks in Schiffskoordinaten (x Bug, y Backbord, z oben, in Zellen; D14; Nullpunkt = der Datei, Holo und Umriss liegen unabhängig davon, D90), **Rotation** = optionale Drehung des Racks um seine Mitte (`[yaw, pitch, roll]` in Grad, D91), **gerade** = wo das Rack vor der Drehung liegt: dort rechnen Core und die Boxen der Szene ganzzahlig, nur `RackTransform` in der App kennt die Drehung, **Stop** = Zielhafen in Reihenfolge, **Placement** = Fracht → Rack.
- Star Citizen nutzt Easy Anti-Cheat. **Externes Fenster = unkritisch. Nichts in den Spielprozess injizieren, keine Hooks.**

## Technische Entscheidungen

| Was | Entscheidung | Warum |
|---|---|---|
| Runtime | **.NET 10** (aktuelles LTS), SDK exakt gepinnt per `global.json` (`rollForward: disable`), Updates per Dependabot `dotnet-sdk` (D86) | Support bis 2028. .NET 8 läuft Nov 2026 aus. Self-contained: die Runtime des SDK liegt im ZIP, lokal = CI nur mit demselben SDK. |
| UI | **WPF** | Rahmenlose transparente Fenster seit 15 Jahren stabil, Win32-Handle direkt greifbar. WinUI 3 kann Transparenz schlecht, Avalonia bringt Cross-Platform, das wir nicht brauchen. |
| Schiffsansicht | **Ein `Viewport3D`**, `PerspectiveCamera` (FOV 40°), Text (Rack-Nummern, Tooltips) in einer 2D-Overlay-Ebene, `CameraMath` als einzige Projektion für Bild, Overlay und Hit-Test (D83, Phase 8) | Ortho pumpt beim Drehen (S8.0); eine Szene und eine Kamera statt zwei Zeichnern im Abgleich. |
| MVVM | **CommunityToolkit.Mvvm** | Source-Generatoren sparen Boilerplate (`[ObservableProperty]`, `[RelayCommand]`). Einziges UI-Paket. |
| JSON | `System.Text.Json` | Stdlib, kein Newtonsoft. |
| OCR | **RapidOcrNet** (PP-OCRv5 latin) auf ONNX Runtime 1.31.1 (explizit), SkiaSharp transitiv 3.x, in `CargoPlan.Ocr` (`net10.0`, ohne Windows-API); Modelle aus dem Paket als `None`-Items nach `models/v5/` neben der Exe, Lizenzen in `models/NOTICE.md` (D99) | Eigenes Lesen statt ContractTracker (A11-Exit, S10.0): 100 % der Lieferfelder bei 21:9, offline ≈ 1,2 s je Vertrag. Kein Download, kein Netz. Aufnahme: GDI `BitBlt` nur aufs ROI unter Per-Monitor-DPI im Capture-Thread, eigene Fenster über dem ROI für die Aufnahme per DWM-Cloak verborgen, je Owner-Kette nur der oberste (D100, B12); Engine wärmt nach dem ersten Frame vor, nur mit Scan-Taste, sonst beim ersten Scan (`ContractScanner`, D108). |
| Tests | **xUnit** + `AwesomeAssertions` | Standard im .NET-Ökosystem. AwesomeAssertions ist der Apache-2.0-Fork von FluentAssertions (ab v8 kommerziell lizenziert). |
| Click-through | Win32 `SetWindowLongPtrW` schaltet `WS_EX_TRANSPARENT` via `[LibraryImport]`, HWND aus `WindowInteropHelper`; `WS_EX_LAYERED` setzt WPF selbst (D11) | Keine Lib nötig. |
| Hotkeys | **Gepollt:** `GetAsyncKeyState` auf einem `DispatcherTimer` (33 ms), Flanke + exakte Modifier (`HotkeyPoller`), scharf nur mit SC oder CargoPlan vorne (D97); Ctrl/Alt/Win Pflicht außer bei F-Tasten, einziger Default Scan = F3 (D104) | `RegisterHotKey` kommt mit SC vorne nie an; kein Hook (EAC), keine Lib. Die Taste erreicht auch das Spiel. |
| Netz | **Drei Netzzugriffe, nur GET:** (1) `HoloSource` (App) lädt Schiffs-Hologramme per BCL-`HttpClient` aus dem Data-Release des **aktiven** Stands, `sc-cargoplan-shipdata/releases/download/<dataVersion>/<file>`; Liste, Länge und SHA aus dem `holo`-Abschnitt seines Manifests (lokal, kein Manifest-GET), `holo: null` oder unbekanntes Format = Umriss, 404 = Umriss ohne Notice; Schalter `downloadShipHolograms` (D63, D114); der Cache `holo\<dataVersion>\` behält nur den aktiven Stand (D95). (2) `UpdateService` (App) fragt einmal pro Start über Velopacks `GithubSource` die Release-Liste ab, ohne Pre-Releases (außer mit Dev-Schalter `--updates-prerelease`), Schalter `updates.checkOnStartup`; geladen wird erst auf Klick (D109). **Seit v0.6.0:** (3) `ShipDataSource` (App) fragt einmal pro Start nach dem Update-Check `…/releases/latest/download/manifest.json` aus `MRiediger/sc-cargoplan-shipdata` ab, Schalter `shipData.checkOnStartup`; auf Klick `ships.zip` aus dem Data-Release `ships-*`, geprüft und atomar nach `ships\<tag>\` im Datenordner (`ShipDataStore`, Core), übernommen per Neustart; auf demselben Klick die zwei Hauling-Dateien des Release, geprüft, ein Fehler kostet die Schiffe nicht (D112-Addendum T13.2); Dev-Build nur mit `--ship-data-source` (D111, D112) | Hologramme stammen aus CIG-Modellen und müssen zurückziehbar sein: eigenes Daten-Release statt ZIP oder Repo. Jeder weitere Netzzugriff braucht einen eigenen D-Eintrag. Nicht die App, sondern der Build: `tools/fetch-ship-data.ps1` lädt einmal den gepinnten Data-Release als Baseline (D113). |
| Distribution | `build/publish.ps1 -Version` → App + Cli self-contained `win-x64` in **einem Ordner** (`out/publish/SC-CargoPlan`, 201 MiB, 388 Dateien inkl. `ship-data/` (gepinnter Data-Release, D113) und `models/`; Stand v0.7.0), dann `vpk pack` (Version aus der `Velopack`-PackageReference, `out/tools/`) → **Velopack-Portable-ZIP** `SC-CargoPlan-v<v>-win-x64.zip` mit SHA256 (Stub `SC-CargoPlan.exe`, `Update.exe`, App in `current\`, kein Wurzelordner; 0.9.x lokal 90,3 MB, v0.4.0 85,2 MB, v0.5.1 92,2 MB, v0.6.0 92,3 MiB, Delta von v0.5.1 0,85 MB, v0.7.0 92,3 MiB, Delta von v0.6.0 0,65 MB; mit den Hauling-Dateien (T13.2) +0,18 MB) + Feed (`releases.win.json`, Full-, Delta-nupkg) in `out/release/`, Layout-Prüfung `check-portable.ps1`; kein Single-File, kein pubxml, keine Setup.exe, Satellites nur `en` (D46, D48, D49, D50, D99, D109). Dazu die **Linux-CLI** `SC-CargoPlan-Cli-v<v>-linux-x64.tar.gz` mit SHA256: Cli self-contained `linux-x64` nach `out/publish/linux/SC-CargoPlan-Cli`, gepackt mit Windows-bsdtar über eine mtree-Liste (sonst fehlt `+x`), geprüft von `check-cli-tarball.ps1`; kein Overlay, kein Velopack, kein Datenupdate (66,4 MB, 326 Dateien, Stand v0.7.0, erstes Release mit Tarball, D115) | Nutzer sollen nichts installieren müssen; Hauler haben .NET 8, nicht 10. WPF kann nicht getrimmt werden. |
| CI/CD | GitHub Actions, Matrix `ubuntu-latest` + `windows-latest` (App-Tests und `dotnet format` nur unter Windows, D12), Release bei Git-Tag; Job `cli-linux` startet den Linux-CLI-Tarball des Windows-Jobs auf `ubuntu-latest` (Artefakt 1 Tag, `upload-artifact@v7`/`download-artifact@v8`, D115) | Niemand baut von Hand; Ubuntu deckt Pfadtrenner und Kultur für Core, Import, Cli ab. |
| Lizenz | **MIT** | Maximale Verbreitung. |

**Kernregel:** `CargoPlan.Core` referenziert **nichts** aus WPF oder Windows-APIs. UI und Import referenzieren Core, nie umgekehrt. Warum: Planer bleibt testbar und tauschbar. Ebenso enthält `CargoPlan.Ocr` keine Windows-API: Bildschirmaufnahme und Fenstersuche (Win32) gehören in die App (`App/Win32`), Ocr bekommt nur das Bild (D99).

## Projektstruktur (Soll)

```
sc-cargoplan/
├── CLAUDE.md · TASKS.md · README.md · LICENSE · CONTRIBUTING.md · SECURITY.md
├── SC-CargoPlan.sln
├── Directory.Build.props          # gemeinsame Settings: LangVersion, Nullable, TreatWarningsAsErrors, Version, Metadaten
├── build/
│   ├── publish.ps1                # Publish nach out/publish, vpk pack, Release-Dateien nach out/release (D48, D49, D109)
│   ├── check-portable.ps1         # Layout-Prüfung des Portable-ZIPs: current\ = Publish-Ordner, ships, PDBs, Version (D109)
│   └── check-cli-tarball.ps1      # Prüfung des Linux-CLI-Tarballs: Dateien = Publish-Ordner, Modi, NOTICE, Version, Pin (D115)
├── build/ship-data.json           # Pin des Data-Release (`ships-YYYY.MM[.N]`), Bump je App-Release (D113)
├── .ship-data/                    # gitignored: der gepinnte Release, vom Build geholt und geprüft; `current` nennt den Ordner (D113)
├── tools/fetch-ship-data.ps1      # holt den Pin aus sc-cargoplan-shipdata, prüft jede Datei gegen das Manifest (PS 5.1 + 7); der Build ruft es einmal (D113)
├── src/
│   ├── CargoPlan.Core/            # net10.0 – Modelle, Planer, Ship-Loader (liest ships/ und outlines.json des aktiven Datenstands; Baseline = `ship-data\` neben der Exe mit Manifest, `ActiveShipData.ReadBaseline`, D113); csproj-Target holt die Baseline beim Build, Holo-Leser SCH2 (D63), `ShipData/`: Tag, Manifest-Leser (inkl. `holo`-Abschnitt → `HoloOffer`, D114), `ShipDataStore` (aktiver Stand, Install, Aufräumen; kein Netz, D112); `Routing/`: Titel-Enums, Beobachtungen + `routes.json` (T13.1), Hauling-Loader (`HaulingFiles`, Templates + Positionen des aktiven Stands, Manifest-Abschnitt `hauling` → `HaulingOffer`), `BoardHubs` (D121), `PlaceNames` (Ordnung Hubs → L-Stationen je Planet → Rest, Kurznamen `HUR-L1`), `RouteFilter` (Default Stanton/stellar), `PortMatrix` (T13.2); Soll: Board, Matching, Ranking (D118, D119)
│   ├── CargoPlan.Import/          # net10.0 – Contracts/: Vertragstext → `ScannedContract` mit Inhaltsschlüssel (D101); `DestinationNames` (B1)
│   ├── CargoPlan.Ocr/             # net10.0 – Vertrag aus einem Bild lesen: ROI, up2-gray, RapidOcrNet, Lesereihenfolge (D99); keine Windows-API; NOTICE.md → models/NOTICE.md
│   ├── CargoPlan.Cli/             # net10.0 – Plan im Terminal ausgeben (Debugging ohne UI), `scan <png>`, `ships --version`
│   └── CargoPlan.App/             # net10.0-windows, WPF – Overlay, ViewModels, Settings (Schema 3, Migration D105), Win32-Interop (inkl. Fenstersuche und Bildschirmaufnahme), Scan (`Scanning/`: Aufnahme D100, `ScanIntake` mit zweiter Lesung bei Verdacht und `scans.log` D103, `ScanRunner` für Hotkey und Editor-Button mit Notice je Ergebnis D104, `ScanPictures` für `--keep-scans` D108), Holo-Download aus dem Data-Release des aktiven Stands und Cache `holo\<dataVersion>\` (`HoloSource`, Netzzugriff 1, D63, D114), `Program.Main` mit `VelopackApp.Run()` zuerst und `Updates/` (Check nach dem ersten Frame, `UpdateGate` ohne Velopack, Netzzugriff 2, D109), `ShipData/` (`ShipDataGate`, `ShipDataSource`, Netzzugriff 3, Neustart nach dem Install, D112)
├── tests/
│   ├── CargoPlan.Core.Tests/      # Fixtures/, FrozenShips/ (A10)
│   ├── CargoPlan.Import.Tests/    # Fixtures/RawText: echte Vertragstexte (CIG-Material nur als Text)
│   ├── CargoPlan.App.Tests/       # läuft nur unter Windows (D12), WPF nur über Sta.Run (A12)
│   └── CargoPlan.Ocr.Tests/       # läuft nur unter Windows (D12-Muster, Schrift der Testbilder)
├── docs/
│   ├── decisions.md               # D1…, nummeriert, mit Addenda
│   ├── tasks-archive/             # Geschichte: abgeschlossene Phasen, Releases, Sessions aus TASKS.md, wortgleich (K11); Index und Lehren in TASKS.md
│   ├── design/                    # Entwürfe vor einer Phase, Stand ihres Datums (cargo-routes.md: Phase 13); D-Einträge gehen vor
│   ├── mission-format.md · plan-format.md · settings-format.md · ship-format.md · holo-format.md · hauling-data-format.md · routes-format.md
│   ├── update-testing.md          # Entwickler: lokaler Feed, --updates-prerelease, Release-Dateien, 10er-Fenster (D109)
│   ├── release-notes/             # je Release, Geschichte: nie nachträglich ändern
│   ├── review/                    # Repo-Durchsichten (2026-11-pre-p10)
│   ├── nexus/                     # Nexus-Seite, Checkliste, Staff-Anfrage
│   ├── shots/                     # SHOTLIST.md + Pläne für Screenshots
│   └── images/
└── .github/
    ├── dependabot.yml             # nuget + github-actions, je eine Gruppe, monatlich (D51)
    ├── PULL_REQUEST_TEMPLATE.md
    ├── ISSUE_TEMPLATE/            # bug, ship, feature + config (keine leeren Issues, Fragen → Discussions)
    └── workflows/
        ├── ci.yml                 # build + test bei jedem Push/PR
        └── release.yml            # publish + GitHub Release bei Tag v*
```

## Befehle

```powershell
dotnet build                                   # alles kompilieren
dotnet test                                    # alle Tests
dotnet test tests/CargoPlan.Core.Tests -p:RunSettingsFilePath= --filter Category=Exhaustive   # Layout-Vollprüfung (~9 min), nicht im Standardlauf
dotnet test tests/CargoPlan.Core.Tests -p:RunSettingsFilePath= --filter Category=Slow         # große Pläne (Stresslast, Freelancer; < 1 s seit D64), nicht im Standardlauf, CI eigener Schritt
dotnet test tests/CargoPlan.Core.Tests -c Release -p:RunSettingsFilePath= --filter Category=Timing --logger "console;verbosity=detailed"   # Kosten je Verschiebe-Stufe als Tabelle (D65), nur von Hand
dotnet test tests/CargoPlan.App.Tests -c Release -p:RunSettingsFilePath= --filter Category=Timing --logger "console;verbosity=detailed"    # Speicher je Schnappschuss, Undo/Redo-Dauer (D74), Renderzeit Schraffur (D29), Hit-Test auf gedrehten Racks (D91), nur von Hand
dotnet test tests/CargoPlan.App.Tests -c Release -p:RunSettingsFilePath= --filter FullyQualifiedName~ShipPictureTests     # jedes Schiff pixelweise gegen eine Baseline, auch mit Ladung (~3,5 min, 1381 Bilder inkl. Draufsicht für den Kontaktabzug, D90, D91, D92), nur von Hand; Env: CARGOPLAN_PICTURES_OUT, CARGOPLAN_HOLO_FOLDER, optional CARGOPLAN_PICTURES_BASELINE, CARGOPLAN_PICTURES_EXPECTED, CARGOPLAN_SHIP_DATA (anderer Datenstand im Download-Layout statt der Baseline, D113)
dotnet run --project src/CargoPlan.App         # Overlay starten
dotnet run --project src/CargoPlan.App -- --plan tests/CargoPlan.Core.Tests/Fixtures/Plans/split_and_shared.json
dotnet run --project src/CargoPlan.Cli -- plan tests/CargoPlan.Core.Tests/Fixtures/example_missions.json
dotnet run --project src/CargoPlan.App -- --data-dir <voller Pfad>   # alle Dateien des Laufs (Settings, Plan, Logs, Holo-Cache) in diesem Ordner statt im Profil; für Gate- und Testläufe (T10.4a)
dotnet run --project src/CargoPlan.Cli -- scan <screenshot.png>   # Vertrag aus einem Fenster-Screenshot lesen: Rohtext, dann geparster Vertrag mit Inhaltsschlüssel nach stdout, Stufenzeiten nach stderr (D99, D101)
dotnet test tests/CargoPlan.Import.Tests -c Release -p:RunSettingsFilePath= --filter FullyQualifiedName~OcrSampleTests   # echte Aufnahmen aus S10.0 durch OCR und Parser gegen truth.json: Lieferungen, Pickup, Max, Reward, Ansicht, Schlüssel je Paar (~1 min), nur von Hand; Env: CARGOPLAN_OCR_SAMPLES (sc-ocr-samples, CIG-Material, nie ins Repo), CARGOPLAN_OCR_OUT
dotnet test tests/CargoPlan.Import.Tests -c Release -p:RunSettingsFilePath= --filter FullyQualifiedName~ScanProbeLogTests --logger "console;verbosity=detailed"   # jeden Rohtext eines scan-probe.log durch den Parser, strukturelle Asserts, Zahlen je Schlüssel im Output (T10.2a), nur von Hand; Env: CARGOPLAN_SCAN_PROBE_LOG (CIG-Material, nie ins Repo)
dotnet test tests/CargoPlan.Ocr.Tests -c Release -p:RunSettingsFilePath= --filter FullyQualifiedName~OcrMemoryTests --logger "console;verbosity=detailed"   # Speicher über 100 Lesevorgänge, nur von Hand; Env: CARGOPLAN_OCR_SAMPLES
dotnet run --project src/CargoPlan.App -c Release -- --scan-probe [N] [--scan-probe-images]   # Overlay + Scan-Probe: F8 = 1 Scan, F9 = N Scans (Default 100); scan-probe.log + scan-probe\*.png neben cargoplan.log (CIG-Material, nie ins Repo); Env optional CARGOPLAN_SC_CAPTURE_PROCESS = anderes Fenster für einen Probelauf ohne SC
dotnet run --project src/CargoPlan.App -c Release -- --keep-scans   # Overlay wie immer, jede Aufnahme von F3/Scan-Button als PNG nach scans\ neben plan.json (die letzten 20, Name Zeit-Ansicht-Ergebnis, auch bei „No contract in frame“), Name am Ende der Ergebniszeile in scans.log; für Bug-Reports anderer Auflösungen (D108, CIG-Material, nie ins Repo)
dotnet run --project src/CargoPlan.App -c Release -- --measure-frames <ziel> --over-game [--scan-load]   # Frame-Zeit über dem Spiel (D83) mit vorgewärmter Engine; Ziel `routes` = Hafen-Matrix des Plan-Reiters, scrollt statt Kamera (T13.2); --scan-load scannt während der 30 s in Schleife; measure-frames.log neben der Exe
dotnet test tests/CargoPlan.App.Tests -c Release -p:RunSettingsFilePath= --filter FullyQualifiedName~OverlayCaptureTests   # eigenes Fenster über dem ROI je Capture-Variante (D100), SC muss laufen, nur von Hand; Env: CARGOPLAN_SC_CAPTURE (Ausgabeordner außerhalb des Repos), optional CARGOPLAN_SC_CAPTURE_PROCESS
./tools/fetch-ship-data.ps1 -ReadableSchemaVersions 4,5,6   # gepinnten Data-Release nach .ship-data/<tag>/ holen und prüfen; macht der Build selbst, wenn er fehlt (D113); Racks, Umrisse, Anker baut die Pipeline (sc-cargoplan-shipdata)
./build/publish.ps1 -Version 0.1.0 [-DeltaBase github|<full.nupkg>]   # App + Cli nach out/publish/SC-CargoPlan, vpk pack, Portable-ZIP + SHA256 + Feed nach out/release, Layout-Prüfung (D48, D49, D109), dann Linux-CLI-Tarball + SHA256 mit Prüfung (D115); Reihenfolge im Skript nicht ändern
dotnet run --project src/CargoPlan.App -- --updates-source <voller Pfad> | --updates-prerelease   # Update-Check gegen einen lokalen Feed bzw. mit GitHub-Pre-Releases (D109), nur für Gates; docs/update-testing.md
dotnet run --project src/CargoPlan.App -- --data-dir <voller Pfad> --ship-data-source <voller Pfad | https://github.com/<owner>/<repo>>   # Schiffsdaten-Check gegen einen Ordner (manifest.json + ships.zip) oder ein anderes Repo; Dev-Build prüft nur so (D112); docs/update-testing.md
dotnet run --project src/CargoPlan.Cli -- ships --version [--data-dir <voller Pfad>]   # aktiver Schiffsdatenstand: Tag, baseline/downloaded, Ordner (T12.3)
dotnet run --project src/CargoPlan.Cli -- --version
```

## Anti-Patterns – wovor Claude Code sich schützen soll

| # | Stichwort | Regel |
|---|---|---|
| A1 | **Grep-first** | Vor jedem neuen Typ, jeder Methode, jeder Datei: `Grep` nach dem Namen **und nach Synonymen des Zwecks** (`Render`, `Format`, `Print`…). Treffer ⇒ Ist-Zustand melden und stoppen, nicht daneben neu bauen. Der Compiler fängt nur exakte Doppelnamen im selben Namespace. |
| A2 | Rück-Sweep | Neue Hausregel gelernt ⇒ Grep über den Bestand als eigener Mini-Task, nicht nur in künftigen Code anwenden. |
| A3 | Confident Hallucination | P/Invoke-Signaturen, NuGet-APIs, Attribute (`[LibraryImport]`, `[ObservableProperty]`, STJ-Generatoren) **gegen aktuelle Doku prüfen**, Quelle als Kommentar an die Stelle. Gedächtnis reicht nicht. |
| A4 | YAGNI / Minimal zuerst | Kein Interface ohne zweite Implementierung, kein DI-Container ohne Bedarf, keine Abstraktion „für später“. Erst die kleinste lauffähige Version, dann erweitern. |
| A5 | Sycophancy | Jede Architekturentscheidung mit mindestens einem Gegenargument. Martin ist C#-Einsteiger und kann nur abwägen, was er sieht. |
| A6 | Defensiv nur an Grenzen | Grenzen: JSON-Laden, SQLite-Reader, Win32-Calls, Nutzereingaben. Innen (Core) sind Records validiert – kein Null-Check-Teppich. |
| A7 | Plan-Mode-Stopp | Task berührt ≥ 3 Dateien ⇒ erst Plan vorlegen, dann auf wörtliche **„Freigabe“** warten. Ein Neustart des Plan Mode hebt eine ausstehende Freigabe nicht auf. |
| A8 | Stille Annahmen | Fragen statt raten. |
| A9 | **Renderer-Flicken** | Kommt ein neues Darstellungselement, wird die Renderarchitektur neu entschieden – nicht das Element in die bestehende eingepasst. Zwei Renderer für ein Bild sind ein Provisorium, kein Entwurf (Anlass: Holo-Kulisse hinter dem 2D-Painter, D83). |
| A10 | **Fixture-Kopplung** | Mechanik-Tests (Treffertest, Drag, Szene, Kamera, Planer, Importskripte) laden Schiffe aus dem Test-Ordner (`tests/CargoPlan.Core.Tests/FrozenShips`, `TestPlans.HullA()`, `FrozenShips.Load`), **nie aus den ausgelieferten Daten** (`ship-data\`, D113). Datentests prüfen die Baseline selbst. Sonst bricht der nächste Datenimport UI-Tests, ohne dass die App einen Fehler hat (Anlass: D92, 70 Tests). |
| A11 | **Fremdquelle mit Exit-Plan** | Eine fremde Datenquelle ohne Lizenz und ohne Ansprechpartner kommt nur mit Exit-Plan ins Projekt (Anlass: ContractTracker, closed source, Autor nicht erreichbar; Ausbau quer durch Planer und Plan-Schema, S10.0). |
| A12 | **WPF in Tests** | WPF-Elemente in Tests nur über `Sta.Run`. Der Testprozess rendert nur in Software (`Sta.RenderInSoftware`, D98), kein Test schaltet das zurück oder setzt `RenderMode` pro Fenster. Eine GPU macht lokal sonst andere Tests als CI (Anlass: B11, Deadlock in `d3d9` in 5 von 10 Läufen). |
| A13 | **Notice-Zeile mit mehreren Schreibern** | Wer eine Notice-Quelle setzt oder löscht, grept alle Schreiber dieser Quelle (`SetNotice(NoticeSource.X`) und testet ihr Zusammenspiel: Fehler dürfen verschwinden, Hinweise müssen stehen bleiben. Billiger als jede Upgrade-Probe (Anlass: B14, B13 baute das Löschen nach dem Speichern, T10.4b das Setzen des Konvertierungshinweises, ohne gemeinsamen Test; die Notice stand 500 ms). |
| A14 | **Velopack zuerst** | `VelopackApp.Build().Run()` ist die erste Zeile in `Program.Main`. Code davor läuft bei jedem Install- und Update-Hook (`--veloapp-*`) erneut, und Velopack beendet den Prozess in diesen Fällen innerhalb von `Run()` (D109, S11.0 G4). |

## NIEMALS

- `catch { }` oder `catch (Exception) { }` ohne Logging/Rethrow (stille Fehler)
- `.Result` / `.Wait()` / `GetAwaiter().GetResult()` im UI-Thread (Deadlock in WPF)
- `async void` außer bei Event-Handlern
- Magic Numbers für Win32 – jede Konstante benannt (`WS_EX_TRANSPARENT = 0x20`)
- `Console.WriteLine` in Core/App (nur in `CargoPlan.Cli`)
- Hardcoded Pfade, Hotkeys, Farben – gehört in `settings.json` oder eine zentrale Konstanten-Datei
- `TODO` ohne Issue-Nummer
- Planungslogik in Views oder ViewModels
- Einen Parameter streichen, ohne jede Aufrufstelle anzusehen: ein übrig gebliebenes int-Literal `0` wandelt sich still in jedes Enum (T10.4b: 33 Watermark-Aufrufe wären in `FillRows` gerutscht)

## IMMER

- `Nullable` enabled, Warnungen sind Fehler (steht in `Directory.Build.props`)
- Records immutable, Änderung via `with`
- Externe Library-Aufrufe gegen aktuelle Doku verifiziert (A3)
- Entscheidungen mit Alternativen in `docs/decisions.md` festhalten – nummeriert, je 5–10 Zeilen: Kontext, Entscheidung, Gegenargument, Konsequenz

## Kommunikation mit Martin

- Deutsch, prägnant, Markdown mit fetten Schlüsselbegriffen, Trade-offs statt nur Lösungen
- **Abweichungen vom Task-Text in der Planphase nie als „Martins Entscheidung“ etikettieren.** Korrektes Label: „Präzisierung in der Planphase, ratifiziert durch Freigabe“ (nie „Abweichung“), auch im Commit – **auch für Präzisierungen, die Martin selbst gewählt hat** (Startprompt, Antwort auf eine Rückfrage): in Plan, TASKS.md und Commit-Body ein Label für alle Punkte.
- Niemals ungefragt Framework-Wechsel, „neuere Library“ oder Cross-Platform vorschlagen – Stack ist gesetzt, Änderung nur per Eintrag in `docs/decisions.md` nach Diskussion.

## Definition of Done (jeder Task)

1. Akzeptanzkriterien aus `TASKS.md` erfüllt und einzeln vorgeführt.
2. Unmittelbar vor der Fertig-Meldung `dotnet build` und `dotnet test`
   komplett ausführen und die Zusammenfassung der Ausgabe wörtlich zitieren
   (Warnungen: 0, Tests: bestanden/gesamt). Keine Fertig-Meldung ohne
   diesen Nachweis aus demselben Durchlauf.
3. `dotnet build` ohne Warnungen (`TreatWarningsAsErrors`), `dotnet test` grün.
4. Martin hat jede neue Datei einmal erklärt bekommen (Zweck, Kernidee, nicht Zeile für Zeile).
5. Task in `TASKS.md` auf `[x]`, Commit mit Task-Prefix (`T2.1: ...`), englische Message.