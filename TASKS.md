# TASKS – SC-CargoPlan

Regeln: **ein Task pro Session**, Reihenfolge einhalten. Jeder Task hat eine „Warum“-Zeile und prüfbare Akzeptanzkriterien. Erledigt → `[x]`.

Session-Start-Prompt für Claude Code:
> Lies CLAUDE.md und TASKS.md. Wir machen Task **Tx.y**. Fass den Task zusammen, dann los.

---

## Phase 0 – Fundament

### [x] T0.1 Solution-Scaffold
**Warum:** Projektgrenzen (Core ohne UI) müssen am Tag 1 stehen, später ist das nicht mehr zu trennen.
- `dotnet new sln`, Projekte wie in CLAUDE.md anlegen (`classlib`, `console`, `wpf`, `xunit`), Referenzen setzen: App → Core + Import, Import → Core, Cli → Core, Tests → jeweiliges Projekt
- `Directory.Build.props`: `<LangVersion>latest</LangVersion>`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- `.gitignore` (dotnet-Template), `.editorconfig` (dotnet-Defaults, 4 Spaces)
- `LICENSE` MIT, `README.md` englisch: what, why, download-link-Platzhalter, 3 Zeilen build
- `docs/decisions.md` mit den ersten Einträgen aus CLAUDE.md (D1 .NET 10, D2 WPF, D3 Records, D4 Single-File-EXE) – englisch, je Kontext / Entscheidung / Gegenargument / Konsequenz
- Ein Smoke-Test in `Core.Tests`

**Akzeptanz:** `dotnet build` ohne Warnung, `dotnet test` grün (1 Test), `git log` zeigt Initial-Commit. Martin kann erklären, was `Directory.Build.props` gegenüber Einstellungen pro `.csproj` bringt und warum Core kein `net10.0-windows` ist.

### [x] T0.2 CI-Workflow
**Warum:** Ab dem ersten PR von außen muss klar sein, ob er baut. Das kann nicht Martins Job sein.
**Warum Matrix:** Ursprünglich nur `windows-latest` geplant, ergänzt um `ubuntu-latest`. Der Linux-Lauf fängt Windows-Annahmen in Core/Import und den Tests früh ab (Pfadtrenner, Zeilenenden, Culture) und hält Core plattformneutral. Alternative „Ubuntu ohne App per Solution-Filter“ verworfen: zweiter Befehlspfad und eine Datei mehr für wenig Zusatznutzen.
- `.github/workflows/ci.yml`: Trigger `push` auf `main` + `pull_request`, Matrix `ubuntu-latest` + `windows-latest`, Schritte `setup-dotnet` (10.0.x) → `dotnet restore` → `dotnet build -c Release --no-restore` → `dotnet test -c Release --no-build`
- Concurrency-Gruppe pro Ref, laufender Lauf wird bei neuem Push abgebrochen
- Ubuntu baut die WPF-App über `EnableWindowsTargeting=true` (nur kompilieren, nicht ausführen)
- `.github/dependabot.yml`: `nuget` + `github-actions`, wöchentlich
- Badge in README

**Akzeptanz:** Workflow läuft auf beiden Runnern grün auf GitHub. Martin kann erklären, warum `windows-latest` zwingend ist (WPF läuft nur dort, Linux kompiliert nur gegen Reference Packs) und was der Ubuntu-Lauf zusätzlich absichert.

---

## Phase 1 – Datenmodell (Core)

### [x] T1.1 Core-Modelle
**Warum:** Der Planer braucht feste Typen, sonst diskutieren wir später über Dictionary-Keys.
Immutable `record`s in `CargoPlan.Core/Models/`:
- `enum CrateSize { One = 1, Two = 2, Four = 4, Eight = 8 }` (in T1.4 um 16/24/32 erweitert)
- `record Slot(string Id, IReadOnlySet<CrateSize> AllowedSizes)` (in T1.4 durch Grid + Position ersetzt)
- `record Rack(string Id, int Index, int CapacityScu, IReadOnlyList<Slot> Slots)` – `Index` 0 = Rack 1. Leere Slots = kapazitätsbasiert.
- `record Ship(string Id, string Name, int TotalScu, IReadOnlyList<Rack> Racks)` – Konstruktor-Validierung: Rack-Summe == `TotalScu`, sonst `ArgumentException`
- `record CargoItem(string Commodity, int Scu, CrateSize CrateSize)` – Validierung: `Scu % (int)CrateSize == 0`
- `record Stop(string Destination, int Order, IReadOnlyList<CargoItem> Items)` + `int TotalScu`
- `record Placement(string Destination, string RackId, CargoItem Item, bool Delivered = false)`
- `record LoadPlan(string ShipId, IReadOnlyList<Placement> Placements, IReadOnlyList<PlanWarning> Warnings)` + `PlacementsForRack(id)`, `PlacementsForStop(destination)` (Warnings seit T2.4 strukturiert, D7)

**Akzeptanz:** `ModelsTests.cs` deckt jede Validierung positiv und negativ ab, ≥ 8 Tests. Martin kann erklären, warum `record` statt `class` (Value-Equality, `with`-Ausdrücke) und warum `IReadOnlyList` statt `List` in der öffentlichen API.

### [x] T1.2 Hull-A-Layout als JSON + Loader
**Warum:** Schiffe sind Daten. Community-PRs für neue Schiffe sollen eine JSON-Datei sein, kein C#.
- `Core/Ships/hull_a.json`: 4 Racks à 16 SCU, Slots leer; als `EmbeddedResource` im `.csproj`
- `ShipLoader.Load(string shipId)`, `ShipLoader.ListShips()` – lesen aus `Assembly.GetManifestResourceStream`
- Deserialisierung mit `System.Text.Json` in DTOs, dann in die validierten Records mappen. Warum: STJ kann die Konstruktor-Validierung nicht sauber auslösen, DTO → Record trennt Format von Domäne.
- `docs/ship-format.md`: Format kurz dokumentiert, damit Contributor Schiffe beisteuern können

**Akzeptanz:** `ShipLoader.Load("hull_a")` liefert validiertes `Ship`, defekte JSON (Summe 60) wirft, Test vorhanden. Martin kann erklären, was eine `EmbeddedResource` ist und warum nicht „Datei neben der EXE“.

### [x] T1.3 Mission-Fixtures
**Warum:** Realistische Testdaten, nicht „2 SCU Sand“.
- `Core.Tests/Fixtures/example_missions.json`: 3 Stops, Covalex-typisch (Riker 12 SCU Titanium 4er, Shubin 10 SCU Quartz 2er, Orison 8 SCU Agricium 8er), gesamt < 64
- `overflow_missions.json`: gesamt > 64
- `split_required.json`: ein Stop mit 20 SCU
- `MissionFileReader` in Core (wird auch von CLI und später vom Dialog-Export genutzt)
- xUnit `ClassFixture` oder Helper, der die Dateien als `IReadOnlyList<Stop>` liefert

**Akzeptanz:** Alle drei Dateien parsen fehlerfrei.

### [x] T1.4 Rack-Raster, Position, Kistengrößen
**Warum:** Ohne echtes Raster kann der Planer nicht prüfen, ob eine Kiste physisch passt.
**Präzisiert in der Planung:** Umfang erweitert (Slot-Konzept ersetzt statt ergänzt, Position, Kistengrößen 16/24/32, Form-Vorcheck im Planer, Rück-Sweep „vorne/hinten“), Präzisierung in der Planphase, ratifiziert durch Freigabe. Grid ist Pflicht, `schemaVersion` 2. Kein 3D-Packer (D9).
- Spieldaten Hull A: 4 Platten, 2 Seiten × 2 Ebenen, je 4 × 2 × 2 Zellen (Length × Width × Stack, 1 Zelle = 1 SCU)
- `Rack` bekommt `Grid` (`Dimensions`) und `Position` (`RackSide`, `RackLevel`); Validierung `Grid.Volume == CapacityScu`. `Slot` entfällt.
- Kistenmaße als Tabelle in Core (`CrateSize.Dimensions`), neue Größen 16/24/32; `Rack.CanHold(size)` mit fester Orientierung
- `LoadPlanner`: einziger Zusatz ist der Vorcheck „jede Kiste passt in mindestens ein Rack“, sonst `PlanException`
- `hull_a.json`, `docs/ship-format.md` (inkl. „Measuring a ship“, „Adding a crate size“), D9
- Sprache: „vorne/hinten“ → Rack-Reihenfolge bzw. Rack 1 … 4

**Akzeptanz:** Layout lädt mit Grid und Position, Validierungstest (Grid ≠ Kapazität wirft), Kistenmaße-Test pro Größe, 24er auf der Hull A wirft `PlanException`, CLI-Ausgabe der Fixtures unverändert.

---

## Phase 2 – Verteilalgorithmus (Core, kein UI)

### [x] T2.1 `LoadPlanner.Plan()` – Grundfall
**Warum:** Der Kern. Alles andere ist Anzeige.
`static LoadPlan Plan(Ship ship, IReadOnlyList<Stop> stops)` – reine Funktion.
Regeln in Priorität:
1. Stops nach `Order`. Erster Stop → Rack 1, letzter → höchster Rack-Index (Rack-Reihenfolge).
2. Ein Stop belegt möglichst **genau ein Rack**.
3. Teilen sich Stops ein Rack, dann nur **in der Route benachbarte** (1+2, nie 1+3).
4. Freie Racks bleiben leer.

Tests zuerst (`LoadPlannerTests.cs`):
- 3 Stops ≤ 16 → 3 Racks, Reihenfolge korrekt
- 2 × 8 SCU + 1 × 16 SCU → 3 racks used
- 4 × 16 SCU → alle Racks
- leere Liste → leerer Plan

**Akzeptanz:** Tests grün. Martin kann den Algorithmus in 5 Sätzen ohne Code erklären. Core hat **keine** Referenz auf App/Import.

### [x] T2.2 Grenzfälle
**Warum:** Im Spiel passiert genau das.
**Präzisiert in der Planung:** Split nur, wenn kein Plan ohne Split existiert; Rangfolge in `docs/decisions.md` D6. Racks im Spieler-Text ab 1 gezählt.
- Stop > Rack-Kapazität: auf **benachbarte** Racks splitten, Warnung („Riker spans racks 2+3“)
- Gesamt > Schiff: `PlanException` mit fehlenden SCU und betroffenem Stop. Kein stiller Teilplan.
- Items eines Stops nie trennen, wenn vermeidbar

**Akzeptanz:** Tests mit `split_required.json` und `overflow_missions.json`, `Assert.Throws<PlanException>`.

### [x] T2.3 Manuelles Override
**Warum:** Der Planer irrt, der Spieler weiß es besser.
**Präzisiert in der Planung:** `Validate` prüft auch die Kistenform (`Rack.CanHold`), `MovePlacement` bekommt das `Ship` und berechnet die Hinweise (Shared/Split) neu, `Validate` liefert nur Verstöße (Kapazität, Form). Präzisierung in der Planphase, ratifiziert durch Freigabe (D10).
- `LoadPlan MovePlacement(LoadPlan plan, Ship ship, Placement p, string targetRackId)` – neuer Plan via `with`, Original unverändert, Hinweise neu abgeleitet, Überladung nie verboten
- `IReadOnlyList<PlanWarning> Validate(LoadPlan plan, Ship ship)` – Kapazitäts- und Formverstöße als Warnungen mit Rack (Präzisierung in der Planphase von T2.4, ratifiziert durch Freigabe; D7)

**Akzeptanz:** Tests: Original unverändert, Überladung erzeugt Warnung.

### [x] T2.4 CLI
**Warum:** Planer im Terminal prüfen, bevor eine Zeile XAML existiert.
**Präzisiert in der Planung:** vor T2.3 gezogen. Warnungen strukturiert als `PlanWarning(RackId, Message)` (D7), Racks im Text ab 1, Kistenangabe als Anzahl×Größe, Warnungen pro Rack und unten dedupliziert. Manuelles Parsing (D8).
- `CargoPlan.Cli`: `plan <missions.json> [--ship hull_a]`, `System.CommandLine` oder manuelles Parsing (Begründung wählen)
- `PlanTextRenderer` in **Core** (wird später auch für „Copy as text“ im Overlay genutzt):

```
[Rack 1 · 16/16]  Riker  12 SCU Titanium (3×4)
                  Riker   4 SCU Titanium (1×4)
[Rack 2 ·  0/16]  -
```

**Akzeptanz:** CLI gibt Plan aus, Exit-Code ≠ 0 bei `PlanException`. Renderer hat Unit-Test.

---

## Phase 3 – Overlay (WPF)

### [x] T3.1 Fenster-Grundgerüst + MVVM
**Warum:** Erst beweisen, dass ein transparentes Fenster über dem Spiel sichtbar bleibt. WPF ohne MVVM wird nach drei Dialogen unwartbar; Contributor erwarten es.
**Präzisiert in der Planung:** alte T3.2 (MVVM) eingegliedert, Win32-Basis für Click-through aus der Hotkey-Task (jetzt T3.7) vorgezogen, Click-through als Modus `IsLocked` mit Default „interaktiv“ (D11), `ShowActivated=False`, `ShowInTaskbar=True`, Startschalter `--locked`, Platzhalter-Plan aus der Fixture, App-Tests nur unter Windows (D12). Positions-Persistenz und Topmost-Fallback nach T3.4. Präzisierung in der Planphase, ratifiziert durch Freigabe.
- `OverlayWindow.xaml`: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`, `Topmost=True`, `ShowActivated=False`, `ShowInTaskbar=True`, `ResizeMode=CanResizeWithGrip`
- Halbtransparenter Hintergrund-Border (`#A0141414`, Ressource in `App.xaml`), Fenster per `DragMove()` verschiebbar
- `CommunityToolkit.Mvvm`; `OverlayViewModel` mit `[ObservableProperty] LoadPlan? Plan`, `[ObservableProperty] bool IsLocked`, `Ship Ship`, `PlanText` über `PlanTextRenderer`
- `App.xaml.cs` baut ViewModel und Fenster zusammen (kein DI-Container, Begründung im Kommentar); `--locked` startet gesperrt
- `Win32/NativeMethods.cs`: `GetWindowLongPtrW`/`SetWindowLongPtrW` per `[LibraryImport]`, `GWL_EXSTYLE`, `WS_EX_LAYERED`, `WS_EX_TRANSPARENT`. Gesperrt setzt `WS_EX_TRANSPARENT`, entsperrt entfernt nur dieses Bit (`WS_EX_LAYERED` gehört WPF). Angewendet ab `SourceInitialized` und bei jeder Änderung von `IsLocked`
- `CargoPlan.App.Tests` (`net10.0-windows`): ViewModel und Platzhalter-Plan, ohne Dispatcher

**Akzeptanz:** `dotnet run --project src/CargoPlan.App` zeigt ein verschiebbares, transparentes Fenster mit dem Plan-Text über Star Citizen (Borderless) und nimmt beim Start keinen Fokus. Klick auf den dunklen Border-Bereich: mit `-- --locked` geht er ans Spiel; ohne `--locked` bleibt derselbe Klick im Overlay (Fenster wird aktiv). Erst das Paar beweist den Schalter, weil WPF Klicks auf voll transparenten Pixeln ohnehin durchlässt. App-Tests grün unter Windows, Ubuntu-CI grün (App-Tests übersprungen), Core unverändert. Martin kann erklären, warum `AllowsTransparency` Software-Rendering erzwingt und warum das hier egal ist, was der Source-Generator aus `[ObservableProperty]` erzeugt und warum der Lock erst ab `SourceInitialized` gesetzt werden kann.

### [x] T3.2 Lock-Fenster (manuell)
**Warum:** Ein gesperrtes Overlay ist nicht mehr klickbar. Der Spieler braucht einen Schalter, der selbst nie click-through ist.
**Präzisiert in der Planung:** ein ViewModel für beide Fenster, `[RelayCommand] ToggleLock`; `LockWindow` owned vom Overlay, `ShowActivated=False`, `ShowInTaskbar=False`, `WS_EX_NOACTIVATE` und nicht fokussierbarer Button, damit SC den Vordergrund behält (Ergänzung zu D11); Position innen in der oberen rechten Overlay-Ecke, aber als eigenes Fenster (ein Button im gesperrten Overlay wäre nicht klickbar, D11), ohne eigenen Hintergrund, in den virtuellen Desktop geklemmt, als reine Funktion `LockWindowPlacement.Compute`; Zustand per Segoe-MDL2-Glyph, Akzentfarbe und Tooltip; README beschreibt das Lock statt „Neustart“. Präzisierung in der Planphase, ratifiziert durch Freigabe.
- Kleines separates Fenster (rahmenlos, Topmost, nie `WS_EX_TRANSPARENT`) mit Lock-Button, schaltet den manuellen Lock und damit `OverlayViewModel.IsLocked`; Zustand sichtbar
- `--locked` bleibt als Startzustand

**Akzeptanz:** Im Spiel: Lock an → Klicks auf das Overlay gehen ans Spiel, der Lock-Button bleibt klickbar; Lock aus → Overlay wieder bedienbar, ohne Neustart.

### [x] T3.3 Auto-Lock über Cursor-Zustand
**Warum:** Im Flug ist der Cursor versteckt und folgt dem Blick; ein offenes Overlay fängt dann Klicks ab, die dem Spiel gehören (Tractor Beam, D11).
**Präzisiert in der Planung:** Cursor-Spike vor dem Code (SC versteckt per `ShowCursor`, `flags=0`; Tabelle im D11-Addendum); `IsLocked` abgeleitet aus `IsManuallyLocked || IsAutoLocked`, `ToggleLock` und `--locked` nur manuell; reine Funktion `AutoLockRule.ShouldLock` mit Konstante `GameProcessName`, nicht konfigurierbar; `CursorStateMonitor` mit `DispatcherTimer` 150 ms, kein Hook; Prozessname nur bei HWND-Wechsel, verschwundener Prozess = „nicht SC“; `GetCursorInfo`-Fehler wirft, außer `ERROR_ACCESS_DENIED` (sicherer Desktop); Lock-Fenster bei Auto-Lock ebenfalls click-through, Click-through-Umschaltung zentral in `NativeMethods.SetClickThrough`; Tooltip bleibt zweiwertig. Präzisierung in der Planphase, ratifiziert durch Freigabe.
- Polling: `StarCitizen.exe` im Vordergrund UND `GetCursorInfo` ohne `CURSOR_SHOWING` → Auto-Lock
- `IsLocked = Manual || Auto`: der manuelle Lock erzwingt „gesperrt“, hebt den Auto-Lock aber nicht auf. Manuell entsperrt heißt nicht „nie sperren“, sonst fängt das Overlay beim nächsten Tractor Beam wieder die Kiste ab
- Nur lesende Win32-Abfragen, nichts im Spielprozess (EAC)

**Akzeptanz:** Im Spiel: Cursor versteckt → Overlay gesperrt; Cursor sichtbar (z. B. mobiGlas) → entsperrt; anderes Programm im Vordergrund → entsperrt; manueller Lock bleibt in allen Fällen gesperrt.

### [x] T3.4 Fenster-Settings
**Warum:** Position und Größe sollen einen Neustart überleben; `Topmost` verliert gegen Borderless-Spiele gelegentlich.
**Präzisiert in der Planung:** `%LocalAppData%` statt `%APPDATA%` (Program Files nicht beschreibbar, Fensterposition gehört zum Monitor-Layout dieses Rechners); Modell im App-Projekt, Core unverändert; Speichern bei Änderung, 500 ms entprellt, `Closed` schreibt nur Ausstehendes, nur bei `WindowState.Normal`, Zuhören erst ab `ContentRendered`; atomar über `.tmp` + `File.Move`; Wiederherstellung per reiner Funktion `WindowBoundsRestore.Clamp` gegen den virtuellen Desktop mit `MinWidth=200`/`MinHeight=100` aus dem XAML; kaputte Datei oder unbekannte `schemaVersion` → Defaults, Datei bleibt bis zur nächsten Bewegung unangetastet; Hinweise (Laden/Schreiben fehlgeschlagen) als Zeile im Overlay (`OverlayViewModel.Notice`); kein Zahnrad; Topmost-Fallback gestrichen (T3.1-Test im Spiel: Overlay bleibt über Borderless), Backlog-Zeile unten (D13). Präzisierung in der Planphase, ratifiziert durch Freigabe.
- Position/Größe in `%APPDATA%\SC-CargoPlan\settings.json` (STJ), beim Start laden
- **Bekanntes Problem:** `Topmost` verliert gegen Borderless-Spiele gelegentlich. Fallback: `SetWindowPos(HWND_TOPMOST)` per Timer alle 2 s neu setzen, als Setting abschaltbar.

**Akzeptanz:** Position überlebt Neustart (auch nach Absturz/Kill), gespeicherte Position außerhalb des Bildschirms landet sichtbar am Rand, kaputte Datei → Defaults ohne Überschreiben. ~~Fallback per Setting abschaltbar~~ (gestrichen, siehe oben).

### [x] T3.5a Rack-Geometrie im Schiffs-JSON (Core)
**Warum:** Die Schiffsansicht soll Racks als Quader im Raum zeichnen, für jedes Schiff mit JSON, ohne Sonderfall. Seite/Ebene beschreibt Cutlass-Bay oder RAFT-Pods nicht.
**Präzisiert in der Planung:** neue Typen `ShipPoint`/`ShipBox` (`Dimensions` erlaubt keine Null/Negativwerte); `Rack.Origin` Pflicht im Konstruktor, `Rack.Bounds` öffentlich (Overlap-Test, Ansicht); Überlappungsprüfung in `ShipDto.ToShip` neben der Doppel-ID-Prüfung; `origin` mit ≠ 3 Werten → Meldung mit Rack-ID; leeres `racks` war schon abgelehnt (`no_racks`), daher wirft `Ship.Bounds` nie nach dem Laden; Test-Schiffe mit mehreren Racks überlappungsfrei; `stack` als Zeichenkonvention (Length/Width/Stack → x/y/z), physische Stapelrichtung nicht modelliert; Verweis auf Community-Katalog mit Lizenzhinweis in ship-format.md. Präzisierung in der Planphase, ratifiziert durch Freigabe (D14).
- Koordinatensystem rechtshändig, x = Bug, y = Backbord, z = oben, 1 = 1 Zelle; Grid-Achsen fest auf x/y/z, keine Rotation
- `origin: [x, y, z]` = Min-Ecke, Pflicht ab `schemaVersion: 3`; v2 wird mit Version und Erwartung abgelehnt
- Loader lehnt überlappende Racks ab, Meldung nennt beide Rack-Nummern (1-basiert)
- `Rack.Position` bleibt (Spieler-Sprache), Konsistenztest Position ↔ `origin` für Hull A
- `Ship.Bounds` (Min/Max über alle Racks)
- Hull A schematisch: Rack 1 `[0, 1, 3]`, Rack 2 `[0, 1, 0]`, Rack 3 `[0, -2, 3]`, Rack 4 `[0, -2, 0]`
- Keine Änderung an `LoadPlanner`, `PlanTextRenderer`, CLI, Import

**Akzeptanz:** `dotnet test` grün (Overlap-Meldung mit Rack-Nummern, v2 abgelehnt, `Bounds` Hull A = x 0..4, y -2..3, z 0..5, Konsistenztest); CLI-Ausgabe der Fixtures byte-gleich zu vorher; Hull A mit zwei Racks auf `[0, 1, 0]` → Meldung „Rack 2 … Rack 4 … overlap“, kein Stacktrace.

### [ ] T3.5b Schiffsansicht (2.5D)
**Warum:** Die eigentliche Information.
- `ShipView` als `UserControl`, zeichnet Racks als isometrische Quader aus `Rack.Bounds` im Rahmen `Ship.Bounds` (D14), nicht eine Reihe; Rack-Nummer sichtbar (D9), `Position` als Text, Füllgrad proportional
- Eine Farbe pro Stop, Palette ≥ 8 kontraststarke Farben, über `IValueConverter`
- Label pro Placement: „Riker · 12 SCU Titanium“
- Kein Planungscode im View oder ViewModel – nur Aufruf von Core

**Akzeptanz:** Fixture-Plan korrekt gezeichnet, Fenstergröße ändern skaliert sauber.

### [ ] T3.6 Abhaken
**Warum:** Am Ziel muss sichtbar sein, was schon weg ist.
- Klick auf Placement → `[RelayCommand] ToggleDelivered`, Darstellung ausgegraut/durchgestrichen
- „Stop done“-Button pro Ziel
- Plan-Zustand in `settings.json` persistieren (Absturz, Spielneustart)

**Akzeptanz:** Toggle funktioniert, Zustand überlebt Neustart.

### [ ] T3.7 Globale Hotkeys
**Warum:** Im Flug muss das Spiel die Maus bekommen, beim Beladen das Overlay – per Taste, ohne zum Lock-Fenster zu greifen. **Das ist der Grund für .NET.**
- `Win32/NativeMethods.cs` um `RegisterHotKey`, `UnregisterHotKey`, `WM_HOTKEY` erweitern (Click-through-Aufrufe stehen dort seit T3.1), `[LibraryImport]`
- `HotkeyService`: registriert nach `SourceInitialized`, `HwndSource.AddHook` fängt `WM_HOTKEY`, gibt am Fensterende frei
- Hotkey 1 (Default `Ctrl+Alt+O`): Overlay sichtbar/unsichtbar
- Hotkey 2 (Default `Ctrl+Alt+I`): manueller Lock an/aus (wie T3.2), Indikator (Rahmenfarbe)
- Beide in `settings.json` konfigurierbar

**Notizen:**
Kopfzeile (entschieden nach T3.2): Reiter links, Zahnrad + Schloss rechts; das Lock-Fenster überlappt die obere rechte Overlay-Ecke, die Kopfzeile reserviert diese Breite. Gesperrt blendet Reiter und Zahnrad aus, behält aber die Zeilenhöhe. Die Reiterleiste kommt mit dem ersten zweiten Reiter (Schiffsauswahl, noch nicht nummeriert), das Zahnrad mit der ersten Einstellung, die ein Nutzer von Hand setzt (nicht T3.4: Position/Größe speichern sich selbst, D13).

**Akzeptanz:** Spiel im Vordergrund, Hotkey schaltet um, gesperrt gehen Klicks ans Spiel. Martin kann erklären, was `WS_EX_TRANSPARENT` wirklich tut (Hit-Test-Weiterleitung, nicht Optik) und warum Hotkeys erst nach `SourceInitialized` registriert werden können.

### [ ] T3.8 Eingabe-Dialog
**Warum:** Dateneingabe ohne ContractTracker.
- `MissionEditorWindow`: `DataGrid` Destination / Commodity / SCU / Crate size, Zeilen hinzufügen/löschen, Reihenfolge per Pfeil
- „Plan“ ruft `LoadPlanner.Plan()`, Warnungen anzeigen, `PlanException` als MessageBox
- Import/Export als Missions-JSON (nutzt `MissionFileReader` aus T1.3)
- „Copy as text“ (nutzt `PlanTextRenderer`) – für Discord/Org-Chat
- Platzhalter-Plan aus T3.1 (`SamplePlan`, Fixture-Link in `CargoPlan.App.csproj`) entfernen

**Akzeptanz:** Durchlauf Eingabe → Plan → Overlay → Abhaken ohne Terminal. **MVP fertig.**

### [ ] T3.9 Drag-and-drop-Override
**Warum:** T2.3 im UI nutzbar.
- Placement auf anderes Rack ziehen → `MovePlacement()`, `Validate()`-Warnungen, überladenes Rack rot

**Akzeptanz:** Funktioniert, Überladung sichtbar.

---

## Phase 4 – Release (vor dem Import, weil Reichweite vorne steht)

### [ ] T4.1 Publish-Profil
**Warum:** Eine EXE, Doppelklick, läuft. Sonst nutzt es niemand.
- `src/CargoPlan.App/Properties/PublishProfiles/win-x64-selfcontained.pubxml`: `SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, `RuntimeIdentifier=win-x64`
- Zweites Profil framework-dependent (klein, braucht .NET Desktop Runtime)
- App-Icon, Assembly-Metadaten (Version, Author, Repo-URL)

**Akzeptanz:** Beide Publish-Läufe liefern eine startfähige EXE. Self-contained < 80 MB. Martin kann erklären, warum WPF nicht getrimmt werden kann.

### [ ] T4.2 Release-Workflow
**Warum:** Tag setzen → Download steht. Kein manuelles Hochladen.
- `.github/workflows/release.yml`: Trigger `push` auf `tags: v*`, publish beider Profile, `softprops/action-gh-release` hängt beide EXEs + SHA256 an
- `MinVer` oder `GitVersion` für Versionsnummer aus Tag (eines wählen, begründen)
- README: Download-Abschnitt, Hinweis zum SmartScreen-Warnhinweis bei unsignierten EXEs (ehrlich dokumentieren)

**Akzeptanz:** `git tag v0.1.0 && git push --tags` erzeugt Release mit beiden Dateien. Martin kann erklären, was SmartScreen prüft und warum Code-Signing-Zertifikate das Problem lösen würden.

### [ ] T4.3 Repo-Hygiene für Contributor
**Warum:** Erster externer PR soll nicht an Unklarheit scheitern.
- `CONTRIBUTING.md` (englisch): build, test, Schiff beisteuern, PR-Regeln
- Issue-Templates: Bug, Ship request, Feature
- `docs/screenshots/` mit Overlay über dem Spiel

**Akzeptanz:** Dateien vorhanden, Links aus README funktionieren.

---

## Phase 5 – Import aus ContractTracker V2

### [ ] T5.1 Schema erkunden
**Warum:** Erst schauen, dann Code.
- DB-Pfad finden (`%APPDATA%`/`%LOCALAPPDATA%`), in `docs/contracttracker-schema.md` notieren
- Kopie anlegen, `sqlite3 copy.db .schema`, Tabellen/Spalten dokumentieren
- Mapping skizzieren: Spalten → `Stop.Destination`, `CargoItem.Commodity`, `Scu`, `CrateSize`. Was fehlt?
- Lizenz des Trackers prüfen: dürfen wir die DB lesen und das dokumentieren?

**Akzeptanz:** Doku vorhanden, offene Fragen gelistet. Kein Code.

### [ ] T5.2 Read-only-Reader
**Warum:** Ziele, Waren, SCU automatisch.
- `CargoPlan.Import/ContractTrackerReader.cs`: `IReadOnlyList<Stop> ReadActiveMissions(string dbPath)`
- Connection-String `Mode=ReadOnly`, Tracker schreibt parallel
- Test gegen Mini-SQLite-Fixture mit nachgebautem Schema (In-Memory oder Temp-Datei)

**Akzeptanz:** Tests grün, echter Lauf gegen Martins DB liefert plausible Stops. Martin kann erklären, was `Mode=ReadOnly` verhindert.

### [ ] T5.3 Integration
**Warum:** Knopf statt Tipparbeit.
- Button „Load from ContractTracker“ im Editor, Pfad-Autodetektion mit manuellem Fallback
- Optional Auto-Refresh, nur bei offenem Editor

**Akzeptanz:** Auftrag annehmen → Tracker erfasst → ein Klick → Plan steht.

---

## Phase 6 – Ausbau (optional, Reihenfolge frei)

### [ ] T6.1 Weitere Schiffe – `cutlass_black.json`, `raft.json`, Schiffsauswahl im Editor
### [ ] T6.2 Auto-Update – **Velopack** evaluieren (ersetzt T4.1-Publish durch Installer mit Delta-Updates)
### [ ] T6.3 winget-Manifest – `winget install SC-CargoPlan`
### [ ] T6.4 Eigenes OCR – **Windows.Media.Ocr** (in Windows eingebaut, kein Paket, keine 100 MB Modelle). Machbarkeitsstudie als eigener Task.
### [ ] T6.5 Lokalisierung – `.resx`, Deutsch als zweite Sprache

---

## Backlog (nur bei Bedarf)

Kein Plan, sondern Reaktion: wird erst zum Task, wenn der Anlass eintritt.

- Topmost-Fallback (`SetWindowPos(HWND_TOPMOST)` per Timer) nur bei Issue-Report mit Repro. Im T3.1-Test blieb das Overlay über Borderless-SC (D13).
- Rack `orientation` (physische Stapelrichtung, Rotation pro Rack), sobald ein Schiff es braucht (D14).
- Viewport3D-Renderer auf denselben Geometriedaten, falls 2.5D nicht reicht (D14).
- Ship mesh outline (Schiffsumriss): erst nach Klärung von Asset-Pipeline, Rechtslage bei Spieldaten und Render-Kosten im Layered Window (D14).
