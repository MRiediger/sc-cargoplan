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

### [x] T3.5b Schiffsansicht (2.5D)
**Warum:** Die eigentliche Information.
**Präzisiert in der Planung:** Blick von vorne-backbord-oben (Bug links-unten, Backbord zum Betrachter, Projektion als reine Funktion `IsoProjection`); Painter nach Mittelpunkt-Tiefe, Gleichstand nach Rack-Index (Hull A: 4, 2, 3, 1); `FrameworkElement` mit `DrawingContext` (`ShipViewControl`) statt `UserControl`, Neuzeichnen nur bei Szene/Größe/Zoom/Pan; Segmente pro Placement entlang x ab Bug-Ende, proportional zu SCU, freier Rest als Umriss, überladen auf die Last normiert (`RackSegments`); Okabe-Ito ohne Schwarz + Weiß = 8 Farben, Zyklus ab Stop 9 mit Legendenzeile, Delivered dunkelgrau (`MissionPalette`); `IValueConverter` nur für das Legenden-Farbfeld; rote Rack-Kanten nur nach `Validate`, Hinweise und Verstöße als Text in der Legende; Rack-Nummer auf der Bugfläche im letzten Zeichendurchgang; Label pro Placement und Position als Tooltip beim Hover (`Rack 1 · port upper · 16 SCU Titanium (4×4)`, Hit-Test als reine Funktion); Legende nur `Stop · SCU`; Kopfzeile mit Schiffsname als Greiffläche, linkes Ziehen in der Ansicht = Pan, `DragMove` nur außerhalb; Rad-Zoom um den Cursor 0,5×–4×, Doppelklick = Fit, kein Persistieren; gesperrt ignoriert die Ansicht Rad/Ziehen/Hover; Szene als Record `ShipScene` aus `ShipViewBuilder` (kein `ShipViewModel`); `OverlayViewModel.PlanText` entfällt; Platzhalter-Plan jetzt `split_and_shared` (Riker liegt auf beiden Ebenen der Backbordseite); Fenster 420 × 460, Minimum 240 × 260; Core unverändert (D15). Präzisierung in der Planphase, ratifiziert durch Freigabe.
- `ShipView` als `UserControl`, zeichnet Racks als isometrische Quader aus `Rack.Bounds` im Rahmen `Ship.Bounds` (D14), nicht eine Reihe; Rack-Nummer sichtbar (D9), `Position` als Text, Füllgrad proportional
- Eine Farbe pro Stop, Palette ≥ 8 kontraststarke Farben, über `IValueConverter`
- Label pro Placement: „Riker · 12 SCU Titanium“
- Kein Planungscode im View oder ViewModel – nur Aufruf von Core

**Akzeptanz:** Fixture-Plan korrekt gezeichnet, Fenstergröße ändern skaliert sauber.

### [x] T3.5c Orbit der Schiffsansicht (Yaw + Pitch)
**Warum:** Schiffe tragen Container auch an der Unterseite (Spindeln rundum, Pods unter dem Rumpf); eine Kamera, die nur von oben schaut, zeigt diese Racks nie richtig. Der Spieler dreht das Schiff so, wie er gerade auf die Platte schaut, bis hin zum Blick von unten.
**Präzisiert in der Planung:** Kamera als Record `IsoProjection(yaw, pitch)`, orthografisch, Yaw = Azimut ab Bug Richtung Backbord, auf [0, 360) normiert, Pitch positiv = von oben, auf ±85° geklemmt (im Konstruktor, kein `with`); Default Yaw 45°, Pitch arctan(1/√2) ≈ 35,26° (nicht 30°) mit Streckung √(3/2), damit exakt die T3.5b-Projektion; sichtbare Flächen aus Normale · Blickrichtung < 0, Kantenflächen ausgeblendet; Bug-/Heckfläche nur mit dem End-Segment, Bodenfläche wie Deckfläche; Painter-Tiefe = Mittelpunkt · Blickrichtung, auf 9 Stellen gerundet, Gleichstand nach Rack-Nummer; Rack-Nummer auf der sichtbaren Fläche mit der größten projizierten Fläche (Gleichstand: oben/unten, Seiten, Enden); Szene und Projektion getrennt: `ShipScene` kamera-unabhängig aus `ShipViewBuilder` (VM, pro Plan), neuer reiner `SceneProjector` → `ProjectedScene` im Control, gecacht, neu nur bei Szenen- oder Kamerawechsel; Orbit in `ViewNavigation` neben Zoom/Pan (nicht `ViewTransform`), neuer Plan setzt nichts zurück; links ziehen = Orbit (0,5°/px, Greif-Metapher), rechts ziehen = Pan, Rad = Zoom, Doppelklick = Kamera + Zoom + Pan zurück; gesperrt wirkt nichts davon; Fit auf den projizierten Extent der aktuellen Kamera; Hit-Test unverändert auf den projizierten Polygonen; Backlog-Zeile „Camera side setting for ramp-loaded ships“ existierte nicht, nichts gestrichen; Core unverändert (D15). Präzisierung in der Planphase, ratifiziert durch Freigabe.
- Orbit um Hochachse und Neigung bis zum Blick von unten, orthografisch, kein Viewport3D
- Sichtbare Flächen, Zeichenreihenfolge und Nummernfläche hängen von der Kamera ab
- Maus: links drehen/neigen, rechts verschieben, Rad zoomen, Doppelklick zurücksetzen
- Kein Snap, keine Persistenz, keine Trägheit (Backlog)

**Akzeptanz:** `dotnet test` grün (Regression Default = T3.5b, Flächenwahl inkl. Boden, Painter in drei Lagen, Nummernfläche an mehreren Winkeln, Hit-Test von unten, Projektionszeit); `dotnet run`: einmal um die Hochachse, von oben bis unten neigen, keine Fläche springt, von unten liegen Rack 2 und 4 vorn; `--locked`: Ziehen ohne Wirkung; im Spiel stimmt die Nummer im Bild mit der Platte vor dir überein, auch unten.

### [x] T3.6 Abhaken
**Warum:** Am Ziel muss sichtbar sein, was schon weg ist.
**Präzisiert in der Planung:** Einheit = Stop, nicht Placement (eine Mission wird am Ziel komplett entladen, auch ein Split in einem Besuch), Toggle für Fehlklicks; `LoadPlanner.SetDelivered(plan, ship, destination, delivered)` neben `MovePlacement` statt `plan.WithDelivered` (Hinweise brauchen die Racks des Schiffs), unbekannte Destination → `ArgumentException` mit Namen; Hinweise ohne abgelieferte Placements, Kapazität in `Validate` zählt sie weiter (D10 unverändert, D16); Bedienung in der Legende (ganze Stop-Zeile ist eine Checkbox) und per Klick auf ein Segment in der Ansicht, beides `ToggleDeliveredCommand`; Klick = Loslassen im System-Drag-Rechteck, Orbit startet erst außerhalb (Schiff dreht beim Klick nicht), Doppelklick-Reset nur auf freier/leerer Fläche; erledigt = Farbfeld im Segmentgrau, Text grau und durchgestrichen, Reihenfolge bleibt, alle erledigt → „All stops delivered.“ in der Kopfzeile; Ansicht springt beim Abhaken nicht: Legende reserviert den Platz der Hinweise des Plans ohne Delivered (`ShipScene.ReservedHints`, reine Funktion des Plans); gesperrt nicht bedienbar durch Click-through, kein Code; Overlay bekommt `WS_EX_NOACTIVATE` + `WS_EX_APPWINDOW` (`NativeMethods.PreventActivation`, gemeinsam mit dem Lock-Fenster), damit der Haken am Terminal SC nicht den Vordergrund nimmt (D11-Addendum T3.6); `PlanTextRenderer` hängt „(delivered)“ an wie der Tooltip; Persistenz nach T3.8 verschoben (der Plan selbst wird erst dort gespeichert). Präzisierung in der Planphase, ratifiziert durch Freigabe.
- Klick auf Placement → `[RelayCommand] ToggleDelivered`, Darstellung ausgegraut/durchgestrichen
- „Stop done“-Button pro Ziel
- Plan-Zustand in `settings.json` persistieren (Absturz, Spielneustart)

**Akzeptanz:** Toggle funktioniert, ~~Zustand überlebt Neustart~~ (nach T3.8 verschoben, siehe oben).

### [x] T3.7 Globale Hotkeys
**Warum:** Im Flug muss das Spiel die Maus bekommen, beim Beladen das Overlay – per Taste, ohne zum Lock-Fenster zu greifen. **Das ist der Grund für .NET.**
**Präzisiert in der Planung:** keine Default-Hotkeys (ein registrierter Hotkey verschluckt die Kombination fürs Spiel, kein Default ist gegen SC-Bindings sicher), Konfiguration nur über `settings.json` (`hotkeys.toggleLock`/`toggleOverlay`, optional, kein `schemaVersion`-Sprung nach D13), keine Aufnahme-UI, kein Zahnrad; Indikator bleibt das Schloss-Glyph aus T3.2, keine Rahmenfarbe; reiner `HotkeyParser` (Modifier beliebig in Reihenfolge und Schreibweise, Pflicht Ctrl/Alt/Win – Shift allein reicht nicht, F12 abgelehnt weil von Windows für Debugger reserviert, immer `MOD_NOREPEAT`); `HotkeyService` hängt sich wie `OverlayBoundsSaver` an `SourceInitialized`/`Closed` des Overlays; Fehlschlag (`ERROR_HOTKEY_ALREADY_REGISTERED`) oder ungültiger Text → eigene Zeile `HotkeyNotice` (ein Speichern löscht `Notice`, die Hotkey-Meldung soll bleiben), der andere Hotkey bleibt aktiv; Ausblenden als `IsOverlayHidden` + `ToggleOverlayCommand`, beide Fenster rufen selbst `Hide()`/`Show()` (Win32 blendet owned windows nur beim Minimieren mit aus, nicht beim Verstecken), nichts wird zurückgesetzt; settings.json mit dem Relaxed-Encoder geschrieben, damit `+` nicht als `+` erscheint; Core unverändert (D17). Präzisierung in der Planphase, ratifiziert durch Freigabe.
- `Win32/NativeMethods.cs` um `RegisterHotKey`, `UnregisterHotKey`, `WM_HOTKEY` erweitern (Click-through-Aufrufe stehen dort seit T3.1), `[LibraryImport]`
- `HotkeyService`: registriert nach `SourceInitialized`, `HwndSource.AddHook` fängt `WM_HOTKEY`, gibt am Fensterende frei
- Hotkey 1 (~~Default `Ctrl+Alt+O`~~ kein Default, siehe oben): Overlay sichtbar/unsichtbar
- Hotkey 2 (~~Default `Ctrl+Alt+I`~~ kein Default): manueller Lock an/aus (wie T3.2), Indikator ~~(Rahmenfarbe)~~ (Schloss-Glyph)
- Beide in `settings.json` konfigurierbar

**Notizen:**
Kopfzeile (entschieden nach T3.2): Reiter links, Zahnrad + Schloss rechts; das Lock-Fenster überlappt die obere rechte Overlay-Ecke, die Kopfzeile reserviert diese Breite. Gesperrt blendet Reiter und Zahnrad aus, behält aber die Zeilenhöhe. Seit T3.5b gibt es die Kopfzeile mit dem Schiffsnamen (Greiffläche fürs Verschieben); die Schiffsansicht ist der erste Reiter („Ship“). Die Reiterleiste kommt mit dem zweiten Reiter (Schiffsauswahl, noch nicht nummeriert). Das Zahnrad kommt mit der Hotkey-Aufnahme-UI (Backlog, D17): die Hotkeys sind seit T3.7 die erste Einstellung, die ein Nutzer von Hand setzt, aber vorerst direkt in `settings.json`.

**Akzeptanz:** Spiel im Vordergrund, Hotkey schaltet um, gesperrt gehen Klicks ans Spiel. Martin kann erklären, was `WS_EX_TRANSPARENT` wirklich tut (Hit-Test-Weiterleitung, nicht Optik) und warum Hotkeys erst nach `SourceInitialized` registriert werden können.

T3.8 ist geteilt: a = Persistenz und leerer Zustand, b = Editor.

### [x] T3.8a Plan-Persistenz und leerer Zustand
**Warum:** Ein SC-Absturz zwischen zwei Stops darf Plan und Haken nicht löschen, sonst steht der Spieler am zweiten Terminal ohne Plan.
**Präzisiert in der Planung:** eigene Datei `%LocalAppData%\SC-CargoPlan\plan.json` statt settings.json (Arbeitszustand ≠ Einstellung, getrennt löschbar); gespeichert werden Eingabe und Ergebnis: Missionen als vollständige Missionsdatei (verschachtelt, `MissionFileDto` wiederverwendet), Placements mit `item` (über `CargoItemDto`) und Pflichtfeld `delivered`, Hinweise mitgespeichert statt neu abgeleitet (Planer unverändert), `shipId`, `schemaVersion` 1; Serialisierung in Core (`Plans/PlanFile`, `SavedPlan`, eigener STJ-Kontext, `PlanFormatException`); `SavedPlan` prüft im Konstruktor, dass die Placements die Missionen exakt abdecken, get-only gegen `with`; `PlanFile.Read` prüft zuerst das Schiff, dann die Racks, Überladung bleibt erlaubt (D10); unbekanntes und fremdes Schiff sind derselbe Fall (die App kennt nur `hull_a`); `EnsureBelongsTo` als Instanzmethode auf `LoadPlan` statt Extension, drei Aufrufer (`LoadPlanner`, `PlanTextRenderer`, `ShipViewBuilder`), die Backlog-Zeile dazu existierte nicht, nichts gestrichen; `PlanSaver` hört auf `PropertyChanged(Plan)`, 500 ms entprellt, `Closed` schreibt Ausstehendes, `Plan = null` schreibt nichts; atomares Schreiben als `AtomicFile`, gemeinsam mit settings.json; Saver hängt vor dem Start-Plan (jeder Start mit Plan schreibt einmal unverändert); kaputte Datei → leer + Notice, Datei bleibt bis zum nächsten Plan; leerer Zustand: Schiff mit leeren Racks, „No plan. Open the editor to enter missions.“; `SamplePlan` und Fixture-Link raus aus der App, als Test-Helfer `TestPlans` ins Testprojekt; `--plan <datei>` als Startschalter (gewinnt über plan.json, wird beim nächsten Speichern zu plan.json, ohne Pfad → Notice), Parsing als reine Funktion `StartOptions`; Notices als Liste mit Absender (`NoticeSource`), ersetzt `Notice` + `HotkeyNotice`; Gegenargument zur Entprellung in D18. Präzisierung in der Planphase, ratifiziert durch Freigabe (D18).
- plan.json mit Missionen, Placements inkl. `Delivered`, Hinweisen; gespeichert bei jeder Plan-Änderung
- Laden beim Start, kaputte Datei → leerer Zustand + Notice, Datei bleibt stehen
- Leerer Zustand statt Fixture
- `SamplePlan` und Fixture-Link in `CargoPlan.App.csproj` entfernt, `--plan` als Debug-Hilfe
- Notice-Liste pro Absender

**Akzeptanz:** `dotnet test` grün; ohne plan.json leerer Zustand; `--plan` mit Fixture → Plan, Riker abhaken, per Kill beenden, Neustart ohne Schalter → Riker abgehakt; Müll in plan.json → leer + Notice, Datei unverändert; CLI-Ausgaben unverändert.

### [x] T3.8b Eingabe-Dialog
**Warum:** Dateneingabe ohne ContractTracker.
**Präzisiert in der Planung:** eine Zeile = eine Fracht, Stop-Reihenfolge = erstes Auftreten des Ziels (case-insensitive, getrimmt, erste Schreibweise gewinnt), schmale Stop-Spalte nur lesend; Editor ist ein normales, aktivierbares Fenster (kein `NOACTIVATE`, kein Click-through), `Owner` = Overlay und damit topmost, blendet sich mit dem Overlay-Hotkey **und bei Auto-Lock** aus (sonst fängt ein vergessener Editor im Flug den Traktorstrahl-Klick), `ShowActivated=False` + `Activate()` beim Öffnen, damit das Wieder-Einblenden SC den Fokus nicht nimmt; Öffnen per Stift in der Kopfzeile links vom Schloss (gesperrt `Hidden`) und per Button „Open editor“ im leeren Zustand, ein Editor gleichzeitig, kein Hotkey; startet vorbefüllt mit `SavedPlan.Missions`, sonst eine Leerzeile; neue Zeilen über die DataGrid-Leerzeile (Tab) oder Add, Kistengröße wie die Zeile darüber (erste: 1); „Clear rows“ leert den Entwurf für die nächste Route, der Plan bleibt bis „Plan“; leere Zeilen werden übersprungen; Ware ist Pflicht (sonst wäre der Export nicht lesbar); SCU als Text, geprüft in `MissionRows` (Regel doppelt zu `CargoItem`, Konsistenztest); Validierung inline (rote Zeile + Tooltip, erste Meldung in der Statuszeile, „Plan“ aus), `PlanException` in die Statuszeile statt MessageBox; Zeilen sind Entwurf bis „Plan“, Titel „unplanned changes“, Schließen verwirft; erfolgreiches „Plan“ schließt den Editor (`Planned`-Event, Fenster ruft `Close()`), `PlanException` lässt ihn offen; Legende: Stop-Zeile ohne Kästchen als randloser Button mit Hand-Cursor statt CheckBox, weiter per Klick abhakbar (D16-Addendum); neu planen setzt `Delivered` zurück, vorher Hinweis „Planning again clears delivered marks.“ ohne Rückfrage; Planer-Hinweise nur in der Overlay-Legende; Import über `MissionFileReader`, Export der aktuellen Zeilen über neuen `MissionFileWriter` (Core, prüft Lesbarkeit vor dem Schreiben) und `AtomicFile`; „Copy as text“ = `PlanTextRenderer` des gespeicherten Plans; Datei- und Clipboard-Fehler in die Statuszeile; Logik in `MissionRows` (rein) und `MissionEditorViewModel`, Dialoge und Clipboard im Code-behind; keine Schiffsauswahl, keine Editor-Position, kein Entwurfs-Autosave, kein Undo, `--plan` bleibt; Plan verwerfen ohne neuen Plan in den Backlog. Präzisierung in der Planphase, ratifiziert durch Freigabe (D19).
- `MissionEditorWindow`: `DataGrid` Destination / Commodity / SCU / Crate size, Zeilen hinzufügen/löschen, Reihenfolge per Pfeil
- „Plan“ ruft `LoadPlanner.Plan()`, Warnungen anzeigen, ~~`PlanException` als MessageBox~~ (Statuszeile, siehe oben)
- Import/Export als Missions-JSON (nutzt `MissionFileReader` aus T1.3)
- „Copy as text“ (nutzt `PlanTextRenderer`) – für Discord/Org-Chat
- Button „Open editor“ im leeren Zustand (Satz steht seit T3.8a)

**Akzeptanz:** Durchlauf Eingabe → Plan → Overlay → Abhaken ohne Terminal; Haken überleben Neustart (auch nach Absturz/Kill, seit T3.8a). **MVP fertig.**

### [x] T3.9 Drag-and-drop-Override
**Warum:** T2.3 im UI nutzbar.
**Präzisiert in der Planung:** Rechts-Ziehen auf Fracht = Placement verschieben, Rechts-Ziehen auf freier/leerer Fläche = Pan wie bisher, Links = Orbit/Klick überall (Orbit darf nicht davon abhängen, wo man anfasst); Modifier per A3 geprüft: `Keyboard.Modifiers` (`GetKeyState`) ohne Fokus unzuverlässig, `MK_SHIFT`/`MK_CONTROL` im wParam der Maus-Nachricht wären lesbar, verworfen wegen Hook und Taste an SC; Einheit = Placement, nicht Stop (Split-Stop teilweise verschiebbar); Delivered nicht ziehbar (Rechts-Ziehen darauf pannt); Ziel = Rack unter dem Cursor beim Loslassen über alle Flächen, gleiches/kein Rack = nichts; beim Ziehen Placement halbtransparent, Zielrack mit Cyan-Kante (`MissionPalette.DragTarget` + `DragTargetPen` statt `DragTargetBrush`), kein Ghost; Überladung erlaubt, rot nach `Validate`, kein Dialog; Rückgängig = zurückziehen, kein Undo-Stack; Gestenlogik als reine Tabelle `DragGesture.Resolve(button, down, moved, current)` plus `DraggedPlacement`, vom Control bei jeder Bewegung und beim Loslassen gefragt; Szene trägt `Placement` statt `Destination` und `RackId` pro Rack, `SceneHitTest.HitAt`/`PlacementAt`; `MovePlacementCommand` mit dem Record `Gesture.Move` als einzigem Parameter (generierte Commands nehmen höchstens einen); Szenenwechsel bricht einen Drag ab; Legenden-Sprung beim Move akzeptiert; gesperrt wirkungslos über `IsNavigationEnabled`; Verifikation an die Fixture angepasst (Rack 1 hat 16 SCU Titanium, Riker-Quartz nach Rack 1 = 20/16 rot; ohne Überladung Orison-Gold nach Rack 2 = 14/16); Entscheidung als D20 statt D21 (nächste freie Nummer, D19 war der letzte Eintrag); Core unverändert. Präzisierung in der Planphase, ratifiziert durch Freigabe (D20).
- Placement auf anderes Rack ziehen → `MovePlacement()`, `Validate()`-Warnungen, überladenes Rack rot

**Akzeptanz:** Funktioniert, Überladung sichtbar.

### [x] T3.10 SCU-Zellen in der Schiffsansicht
**Warum:** Im Spiel werden Kisten in Ebenen gestapelt; „12 SCU Riker“ als Ebene + Reihe ist beim Beladen greifbarer als ein Balkenanteil. Später soll ein Koordinator großer Schiffe allen dieselbe Füllrichtung zeigen.
**Präzisiert in der Planung:** `RackSegments` → `RackCells.Build(rack, placements, FillOrder)`, eine Zelle pro SCU in Plan-Reihenfolge, Rest frei; Füllreihenfolge Ebene (z) → Reihe (von der Mittellinie nach außen, kleineres |y| zuerst, Backbord/Steuerbord spiegelsymmetrisch) → Length innen in Füllrichtung (12 SCU = Ebene + Längsreihe, Richtungstest mit 10 SCU); `enum FillOrder { BowFirst, SternFirst }`, Default BowFirst als Konstante in `OverlayViewModel.FillOrder`, `ShipViewBuilder.Build` ohne Default; Speicherort der Richtung (plan.json vs. settings.json) bewusst offen (D21); Überladung anteilig nach SCU (größter Rest, jedes Placement ≥ 1 Zelle, damit es zurückgezogen werden kann) statt Überhang weglassen; Zelle als 0,9er-Quader, Painter über alle Zellen aller Racks nach Zellmittelpunkt-Tiefe (Tie: Rack-Nummer, Füllindex, `DrawOrder.CellsBackToFront`); normale Rack-Kanten vor den Zellen, Rot/Cyan und Nummern danach; Free-Zellen nur schwacher Umriss (`MissionPalette.FreeEdge` statt `Free`/`FreeBrush`); Hit-Test: Rack über seine Flächen (Free und Fugen = Drop-Ziel), Placement = vorderste Frachtzelle des Racks (Fracht hinter Free-Umrissen antwortet); `SegmentFill` → `CellFill`, `SceneSegment`/`SceneStrip` → `SceneCell`/`ProjectedCell`; Performance-Test 1.000 Zellen < 50 ms; Zoom skaliert die Zellen, Nummern/Linien unverändert; Core unverändert (D21). Präzisierung in der Planphase, ratifiziert durch Freigabe.
- Racks als Raster einzelner 1-SCU-Zellen, gefüllt in Missionsfarbe, frei als Umriss
- Füllrichtung als Parameter, beide Werte getestet

**Akzeptanz:** `dotnet test` grün; Rack 2 der Fixture zeigt Riker-, Shubin- und freie Zellen, Ebenen bleiben beim Orbit Ebenen; Abhaken, Klick zurück und Rechts-Ziehen funktionieren auf Zellen.

T3.11 ist zweigeteilt: a = Core, b = Kisten in der Ansicht. Je ein Plan, je eine Freigabe. Das frühere T3.11c (Auswahl und Gesten) folgt als T3.14, nach Kistenmix (T3.12) und Positionen (T3.13).

### [x] T3.11a Kistenweise verschieben (Core)
**Warum:** Am Aufzug entscheidet man kistenweise („die zwei Shubin-Kisten noch auf die andere Platte“) oder zielweise.
**Präzisiert in der Planung:** `MoveCrates(plan, ship, moves, targetRackId)` mit `(Placement Source, int Scu)`; Reihenfolge: voller Move ohne Partner bleibt am Index, Merge wächst am Index des Placements im Zielrack, nur ein Teil ohne Partner wird hinten angehängt; Merge-Schlüssel enthält `Delivered`, Zusammenlegen gilt für den ganzen Plan (auch Planer-Zwillinge aus doppelten Missionszeilen); Quellen gegen den Originalplan aufgelöst (Zwillinge = verschiedene Quellen, dieselbe Quelle zweimal = Fehler), Quelle im Zielrack = nichts; `MovePlacement` bleibt als Wrapper bis T3.11c und lehnt nun Delivered ab (UI erlaubt das seit D20 nicht); `MoveStop` eines abgelieferten Stops bewegt nichts, unbekanntes Ziel = Fehler; Test „MoveStop eines Splits“ mit split_required statt split_and_shared (dort hat Riker zwei Waren); Kistengrößen-Satz („Crate sizes are what you enter; the game only shows totals …“) in D22 und README, Editor-Default unverändert. Präzisierung in der Planphase, ratifiziert durch Freigabe (D22).
- `LoadPlanner.MoveCrates`: teilen, im Zielrack zusammenlegen, Delivered nie Quelle, Hinweise neu, Überladung erlaubt
- `MovePlacement` = `MoveCrates` mit vollem Umfang, eine Implementierung
- `LoadPlanner.MoveStop`: alle nicht abgelieferten Placements eines Ziels
- Invariante: pro Rack höchstens ein Placement je Ziel/Ware/Kistengröße; `SavedPlan`-Summen bleiben
- D22

**Akzeptanz:** `dotnet test` grün (Teilen 8→6+2, Merge, Rest 0, kein Vielfaches, mehrere Moves, Delivered, MoveStop eines Splits = ein Placement, SavedPlan nach Split + Merge); CLI-Ausgabe der Fixtures byte-gleich.

### [x] T3.11b Kisten in der Schiffsansicht
**Warum:** Kisten verschiebt man nur, wenn man sie sieht.
**Präzisiert in der Planung:** Blöcke in derselben Füllreihenfolge wie die Zellen (Ebene außen, Reihe ab Mittellinie, Length in Füllrichtung), Stapelachse bleibt z (D14); Block-Code kennt nur Grid-Positionen und ihren Rang, ein Wechsel der Stapelachse (Backlog `orientation`) ändert nur `Positions` und die Grid→Schiff-Abbildung; größte Kiste zuerst, bei gleicher Größe Plan-Reihenfolge (sonst fällt der Hull A bei Riker 4+4 + Shubin 8 in den Fallback); First-Fit = früheste erste Zelle, Tie früheste letzte; `CrateId(Placement, Number)` als Wert, stabil über Neuaufbau und Orbit, Planer-Zwillinge zählen weiter; Tooltip mit Ware und Position (`Shubin · Rack 2 · port lower · 8 SCU Agricium crate (1 of 1)`, abgeliefert + ` (delivered)`); `SceneHit(RackId, Placement?, CrateId?)`; rotes Rack ohne Blöcke, Überladung anteilig ohne Kisten-Id mit altem Placement-Tooltip, Formverstoß ohne Überladung zellweise mit Ids; Fallback pro Placement als Ganzes, getestet über ein künstliches 3×2×2-Rack; **Kiste = eigener Quader** (`SceneBox`, Inset 0,05 ringsum wie die Zelle, drei Flächen, keine Innenkanten), freie Zellen Einzelwürfel, keine Tönung; **Trennachsen-Painter** (D15-Backlog gezogen): Paare mit überlappendem Bildrechteck (Sweep über Bild-x), Richtung aus der Trennachse (`DrawOrder.Compare`, widersprechende Achsen = keine Kante), Kahn ab der Mittelpunkt-Tiefe-Reihenfolge, Zyklus → Mittelpunkt-Tiefe + `Debug.WriteLine`; derselbe Painter für die Racks, gezeichnet Rack für Rack; Painter geprüft mit unabhängigem Strahl-Orakel aus vier Kamerawinkeln, Zyklus mit Windrad; 1.000-Kisten-Test (1er) < 50 ms; verworfen nach Vergleichsbild: Zellen mit Innenkanten-Logik (Haarlinien) und zwei Tönungen pro Farbe; Entscheidung als D21-Addendum, D23 bleibt für c; `CrateMove`/`MoveCrates` erst in c; Core unverändert. Präzisierung in der Planphase, ratifiziert durch Freigabe (D21).
- `RackCells.Build` liefert pro Zelle eine Kisten-Id (Placement + laufende Nummer), Blockform aus `CrateSize` (D9), gelegt in Füllreihenfolge; passt ein Block nicht mehr → zellweise für dieses Placement
- Fuge nur zwischen Kisten, Tooltip pro Kiste (`Shubin · Rack 2 · 2 SCU crate (1 of 4)`)
- Hit-Test liefert die Kiste (`SceneHit` mit `CrateId?`); Überladung bleibt zellweise anteilig

**Akzeptanz:** `dotnet test` grün (4+4+8 und 2+2+2+2 lückenlos, keine Überschneidung, Ids fortlaufend, Fallback bei 20 SCU, beide Füllrichtungen).

### [x] T3.12 Kistenmix aus den Vertragsdaten
**Warum:** Vertragstext und ContractTracker nennen pro Ziel und Ware nur SCU und maximale Kistengröße, nicht die Kistenmischung. Der Spieler soll genau diese zwei Zahlen eintragen; das Tool leitet die Kisten ab, wie der Aufzug sie liefert (größte erlaubte zuerst, Rest kleiner), und lässt sich übersteuern, wenn der Kiosk anders liefert.
**Präzisiert in der Planung:** „beim Lesen expandiert“ wird zu „der Reader liefert die Eingabe, `MissionItem.ToCargoItems()` expandiert vor dem Planen“ – sonst verlöre der Editor Max und Übersteuerung; dafür neue Eingabe-Typen `MissionItem`/`MissionStop` in Core, `MissionFileReader.Read`, `SavedPlan.Missions` und `OverlayViewModel.Missions` tragen sie, `LoadPlanner.Plan` bleibt auf `Stop`; `CrateMix.Sizes` als ausgeschriebene Liste (nicht aus dem Enum); `CrateMix.Problem` als eine Regel für Datei, `MissionItem` und Editor (keine D19-Doppelregel für Kisten); plan.json liest `missions` als rohes JSON (beide Schemata innen), `CargoItemDto` bleibt für Placements; `Invalid/unsupported_schema` von 2 auf 3, die alte Datei wird `crate_size_in_v2`; Übersteuerung gleich der Zerlegung wird nicht normalisiert, die Bearbeitung startet leer; Tooltip an der Kisten-Spalte; Editor-Fenster 660 breit. Präzisierung in der Planphase, ratifiziert durch Freigabe (D23).
- `CrateMix.Decompose(scu, max)` gierig über `CrateMix.Sizes`, 0 oder unbekannte Größe → `ArgumentException`
- Zerlegung ist Eingabe, nicht Modell: ein `CargoItem` pro Kistengröße, absteigend; Planer, Hinweise, Renderer unverändert
- Missions-JSON Schema 2 (`maxCrateSize`, optional `crates` als Übersteuerung), Schema 1 lesbar, Writer schreibt 2; plan.json ohne Schema-Sprung
- Editor: Max. Kiste (Default Zeile darüber, erste 4), Kisten als Text der Zerlegung, Klick = Übersteuerung, kursiv, Validierung inline
- D23, mission-format.md, plan-format.md, README „Entering missions“

**Akzeptanz:** `dotnet test` grün; CLI-Ausgabe aller v1-Fixtures byte-gleich; Editor: „Green Glade · Stims · 6 · max 4“ → „4+2“, Plan → eine 4er und eine 2er; „2+2+2“ → drei 2er; „2+2+3“ → Fehler; Export → `crates` nur bei der übersteuerten Zeile → Import identisch; v1-Datei → Plan wie vorher. Im Spiel: Vertrag nur aus dem Text eintragen, Kiosk vergleichen, unter 30 s für zwei Ziele.

**Offen:** Kiosk-Screenshot als Beleg für D23 nach `docs/images/kiosk-crate-mix.png`, eigener Commit, Link in D23 setzen.

T3.13 ist zweigeteilt: a = Core und Planformat, b = die Ansicht liest die Positionen. Je ein Plan, je eine Freigabe. Gesten erst in T3.14.

### [x] T3.13a Kisten mit Positionen, Layout in Core, plan.json Schema 2
**Warum:** Beim Beladen – und beim Koordinieren großer Schiffe – zählt, **wo** eine Kiste liegt; Drift zur Realität lässt sich nur mit gespeicherten Positionen korrigieren.
**Präzisiert in der Planung:** Kiste ist die primäre Einheit (`Crate`, `GridPosition`), `LoadPlan.Crates` ist die Wahrheit, `Placements` wird bei jedem Zugriff abgeleitet (kein gespeichertes Feld, weil `with` Initializer überspringt); Zwillinge aus gleichen Missionszeilen verschmelzen; `CrateLayout` in Core (Algorithmus aus `RackCells` plus Stützregel: jede Bodenzelle über der Platte braucht eine andere Kiste darunter, Delivered belegt und stützt); `LayOut` als ein Weg für Planer und Migration; `FillOrder` nach Core und in den Plan, `LoadPlanner.Plan` mit optionalem `fillOrder` (Default BowFirst); `MoveCrate` mit Zielzelle, `MoveStop`/`MovePlacement` als Re-Layout im Zielrack, `MoveCrates` mit Split/Merge entfällt; Ablehnung als `PlanException` (Platzgründe), Aufruferfehler bleiben `ArgumentException`, damit das Overlay nur Ablehnungen fängt; `Validate` meldet Überlappung, außerhalb/zu groß, fehlende Stütze, keine Überladungsmeldung mehr; plan.json Schema 2 (`fillOrder`, `crates` mit `position`), `warnings` bleibt gespeichert; Laden lehnt ab: unbekanntes Rack, Kiste außerhalb des Rasters, Kistenmenge ≠ Missionen; lädt und zeigt rot: Überlappung, fehlende Stütze; Schema 1 wird beim Laden über `LayOut` migriert, Haken bleiben, die App schreibt beim Start v2, überladenes v1-Rack = kaputte Datei; `SavedPlan` prüft die Kisten-Multimenge; App minimal: `FillOrder` aus Core, Rechts-Ziehen fängt die Ablehnung ab → `NoticeSource.Move` „Move refused: …“, nächster Move oder Plan löscht sie; `RackCells` zeichnet bis 13b weiter aus den abgeleiteten Placements; die App-Tests bauen das „überladene Rack“ jetzt als von Hand editierte Datei (`TestPlans.Overloaded`). Präzisierung in der Planphase, ratifiziert durch Freigabe (D24).
- `Crate`, `GridPosition`, `FillOrder` in Core; `LoadPlan.Crates`, abgeleitete `Placements`
- `CrateLayout.Place`/`LayOut`/`Problems` mit Stützregel; erschöpfender D9-Test
- `MoveCrate`, `MoveStop`, `MovePlacement` mit Ablehnung statt Überladung
- plan.json Schema 2, Migration aus Schema 1
- D24, plan-format.md, README

**Akzeptanz:** `dotnet test` grün (D9 mit Stützregel für alle Mischungen ≤ 16 aus 1/2/4/8/16, beide Füllrichtungen, beide Seiten); CLI-Ausgabe aller Fixtures byte-gleich; alte plan.json (v1) → App startet, Datei danach v2, Haken erhalten; Rechts-Ziehen in ein volles Rack → Hinweiszeile, nichts rot.

### [x] T3.13b Schiffsansicht zeigt gespeicherte Positionen
**Warum:** Die Ansicht soll zeigen, was im Plan steht, nicht eine eigene Zeichenregel.
**Präzisiert in der Planung:** `ShipViewBuilder` zeichnet `plan.CratesForRack(rack)` bei `Rack.Origin + Position` in `CrateSize.Dimensions`, Farbe über das abgeleitete Placement, freie Zellen = Rasterzellen ohne Kiste; `RackCells`, `RackCellsTests`, `CrateId`, `GridBox` gelöscht; `SceneBox.Crate`/`SceneHit.Crate` sind `Crate?`; Tooltip `(i of N)` = Index in Plan-Reihenfolge innerhalb des abgeleiteten Placements; Rack rot aus `Validate` wie bisher, zusätzlich `SceneBox.IsViolated` aus neuem `CrateLayout.Faulty` (Core, liest dieselbe private Befundliste wie `Problems`, Texte byte-gleich; nur für rote Racks aufgerufen), rot umrandet über allen Boxen; `ShipViewBuilder.Build` ohne `fillOrder`; Boxen nach Placement in Plan-Reihenfolge, dann freie Zellen (Painter sortiert unverändert, Reihenfolge-Tests auf Mengen umgestellt); überlappende Kisten beide gezeichnet und markiert; README ohne Task-Nummer. Präzisierung in der Planphase, ratifiziert durch Freigabe (D24).
- `ShipViewBuilder` nimmt `plan.Crates` (Position + Größe → Box in Schiffskoordinaten), kein eigenes Layout; `RackCells` und `RackCellsTests` entfallen
- `CrateId` = die Kiste selbst; Hit-Test, Tooltip (`(1 of N)` über das abgeleitete Placement), Painter unverändert
- Rechts-Ziehen bewegt weiter das abgeleitete Placement als Ganzes (Re-Layout seit 13a)

**Akzeptanz:** `dotnet test` grün; `--plan` mit split_and_shared zeigt dasselbe Bild wie nach T3.12; im Spiel Rack 1 mit 2 × 8er laden – liegen die Kisten, wo das Overlay sie zeigt? Wenn nein, ist das der Fall für T3.14.

T3.14 ist zweigeteilt: a = Einzelkiste auf Zelle und Legenden-Drag, b = Auswahl mehrerer Kisten. Ein Plan, je eine Session.

### [x] T3.14a Kiste auf Zelle ziehen, Ziel aus der Legende
**Warum:** Am Aufzug liegt eine Kiste woanders als geplant – der Spieler zieht sie im Overlay dorthin, wo sie wirklich ist, und der Plan stimmt wieder. (Früher T3.11c.) Seit D24 tragen Kisten Positionen; Moves lehnen ab statt zu überladen.
**Präzisiert in der Planung:** Zielzelle = sichtbare Fläche wie im Hit-Test (D21): Kiste auf der Sichtlinie → oben drauf über der getroffenen Zelle, Oberkante = Rasterhöhe → kein Ziel; sonst vorderste freie Zelle, das Ziel fällt in ihrer Säule auf Kiste oder Platte (`DropTarget.Resolve(rack, crates, camera, point)`, Strahl aus `IsoProjection.Unproject`, Slab-Schnitt); die erste Regel „letzte freie Zelle entlang der Sichtlinie“ in der Umsetzung verworfen (leeres Rack wird seitlich verlassen, äußere Bodenreihe unerreichbar); gezogene Kisten sind Luft; statt `CanPlace` eine Prüfung für Vorschau und Drop: `CrateLayout.NewProblem(rack, before, after)` aus den privaten `Findings`, `LoadPlanner.MoveProblem`, `MoveCrates(plan, ship, moves)` atomar mit `CrateMove`, Ablehnungstexte = Findings-Texte; `MoveCrate` Wrapper (Parametername `moves`), `LoadPlanner.MovePlacement`, `MovePlacementCommand`, `Gesture.Move` entfernt; `MoveStop` ins Rack, das das Ziel schon ganz hält = unverändert; Drop auf die eigene Position oder ohne Ziel = keine Geste; Geisterkiste cyan/rot (`MissionPalette.DropBrush`/`RefusedDropBrush`, `SceneProjector.ProjectBox`), Grund als Tooltip, Loslassen auf Rot → Notice; `CrateDrop` als ein Weg für Vorschau und VM; `ShipScene` trägt `Plan` und `Ship`; Legenden-Drag über `ShipViewControl.StartStopDrag`, Zielrack cyan, Kisten des Ziels blass; Verifikation korrigiert (8er-Shubin ist rasterhoch, Shubin passt in split_and_shared in kein anderes Rack, Erfolgsfall Legende mit eigenem Plan). Präzisierung in der Planphase, ratifiziert durch Freigabe (D25).
- Rechts-Ziehen einer Kiste auf die Zelle unter dem Cursor, Vorschau mit Grund
- Rechts-Ziehen einer Legendenzeile auf ein Rack = `MoveStop`
- Gültigkeit in Core, atomare Mehrfach-Moves
- D25, README-Bedienung neu

**Akzeptanz:** `dotnet test` grün; Verifikation im Overlay; im Spiel am Aufzug: eine Kiste liegt anders als geplant → im Overlay dorthin ziehen, weiterladen, am Ziel entladen – stimmt das Overlay die ganze Route über mit der Platte überein?

### [x] T3.14b Mehrere Kisten auswählen und gemeinsam ziehen
**Warum:** Mehrere Kisten in einem Zug umplanen.
**Präzisiert in der Planung:** in derselben Session wie 14a umgesetzt; `CrateMove.Shift(ship, crates, anchor, rackId, position)` in Core, Versatz in Schiffskoordinaten, Zielrack = Rack, das die neue Min-Ecke enthält, sonst null; `CrateDrop.Moves(ship, move)` liefert null → `CrateDrop.OutsideRacks` als einziger Text für Vorschau und Notice; `CrateSelection` als unveränderliche Klasse, gebunden per Referenz an die `ShipScene` (jede Plan-Änderung = neue Szene = leere Auswahl, ohne WPF getestet); `DragGesture.DraggedCrates` (angefasste Kiste zuerst) ersetzt `DraggedCrate`, `Resolve` bekommt die Auswahl; Rechtsklick auf Delivered behält die Auswahl; Geister für jede Kiste der Auswahl, bei „außerhalb“ nur der Anker rot; Auswahlkante Magenta `#FF00FF`, 2,5 px, nach den roten Kanten; Verifikation korrigiert (die zwei oberen Lorville-2er liegen mit einer Zelle Abstand in einer Reihe und passen so nicht nach Rack 2: stattdessen innerhalb von Rack 4 in die freie Reihe und die zwei Orison-Gold-1er nach Rack 2). Präzisierung in der Planphase, ratifiziert durch Freigabe (D25).
- Auswahl = Menge von Kisten, View-Zustand gebunden an die Szene (leer nach jeder Plan-Änderung), nicht persistiert; Rechtsklick auf Kiste = umschalten, auf Leerfläche = leeren, Delivered nicht auswählbar
- Ziehen einer ausgewählten Kiste bewegt die Auswahl, Versatz in Schiffskoordinaten (`CrateMove.Shift`), eine Kiste außerhalb aller Racks = ganzer Move ungültig
- Auswahlkante `MissionPalette.Selection`; gesperrt leert die Auswahl
- D25 abschließen, README-Auswahlabsatz

**Akzeptanz:** `dotnet test` grün; die zwei oberen Lorville-Tungsten in Rack 4 per Rechtsklick wählen und zusammen in die freie Reihe daneben ziehen, Versatz erhalten; die zwei Orison-Gold zusammen nach Rack 2; Rechtsklick auf Leerfläche leert; `--locked`: nichts geht.

### [x] T3.13c Stapelachse pro Rack (Schiffsschema 4)
**Warum:** Der Hull A stapelt im Spiel von der Spindel nach außen, das Overlay zeichnete nach D14 nach oben. Die seit T3.10 offene Prüfung im Spiel (2 × 8er in Rack 1) konnte so nicht gelingen. Nachgeschoben nach T3.14, die Nummer gehört zu den Positionen (T3.13).
**Präzisiert in der Planung:** Schiffs-JSON Schema 4, pro Rack `stackAxis` `"+x" | "-x" | "+y" | "-y" | "+z" | "-z"` (Pflicht, case-sensitiv, expliziter Switch wie `side`/`level`, unbekannt → Fehler mit Rack-Nummer und Wert), v3 abgelehnt; Achsrahmen: ±z → Length x, Width y · ±y → Length x, Width z · ±x → Length z, Width y; negative Achse zählt von der Max-Seite, `Origin` bleibt Min-Ecke; Hull A Rack 1/2 `+y`, Rack 3/4 `-y`, gleiche `Bounds`; eine Abbildung in Core: `Rack.CellBox(GridPosition, CrateSize)` und `Rack.ToGrid(ShipBox)` (kein `ToShip(GridPosition)`/`ToGrid(ShipPoint)`, niemand bräuchte sie); `Rack`-Konstruktor mit `StackAxis = PlusZ` als Default (alte Konvention, Test-Racks unverändert); `ShipViewBuilder` (ohne `ToShip`/`BoxOf`), Ghost in `ShipViewControl` und `DropTarget` nutzen `CellBox`, die Säulenregel bleibt im Grid; `CrateLayout.Ranks` füllt Width-Reihen in Grid-Reihenfolge statt „Mittellinie zuerst“ über `Origin.Y` (Steuerbord-Layouts neuer Pläne ändern sich, gespeicherte Positionen bleiben gültig, kein Planformat-Bump); `CrateMove.Shift` als Hybrid: Auswahl in einem Rack → Grid-Offsets zum Anker (Stapel bleibt Stapel, auch Backbord → Steuerbord), über mehrere Racks → Box-Translation in Schiffskoordinaten, Ziel = erstes Rack mit `ToGrid(box) != null`; Core-Tests geändert, wo die Regel sich ändert (Reihenfolge, `Shift`) und wo Steuerbord-Positionen aus dem alten Layout fest eingetragen waren (gespiegelt w → 1 − w); App-Tests auf die neue Abbildung umgerechnet, Szenarien gleich. Präzisierung in der Planphase, ratifiziert durch Freigabe (D26).
- `StackAxis`, `Rack.CellBox`/`ToGrid`/`Bounds` aus einem Achsrahmen
- Schiffsschema 4, `hull_a.json`, Test-Schiffe
- `CrateLayout`-Reihenfolge, `CrateMove.Shift`-Hybrid
- App: Builder, Ghost, `DropTarget`
- D26, Nachträge D14/D21/D25, ship-format.md

**Akzeptanz:** `dotnet test` grün; CLI-Ausgabe aller Fixtures byte-gleich; `--plan` mit split_and_shared: Hull-A-Kisten stapeln von der Spindel nach außen (Orbit von der Seite); Auswahl aus einem Rack nach Steuerbord bleibt ein Stapel. Im Spiel: 2 × 8er in Rack 1 – liegen beide nebeneinander von der Spindel weg, wie im Overlay?

### [x] T3.15 Kisten splitten und mergen
**Warum:** Der Kiosk liefert manchmal andere Kisten als die Zerlegung (D23), z. B. 2 × 2er statt 1 × 4er. Der Spieler splittet oder merged im Overlay, und der Plan folgt dem Spiel.
**Präzisiert in der Planung:** Split legt die Teile in den Fußabdruck der alten Kiste (`CrateLayout.PlaceWithin`, größte zuerst), passt es nicht (32 → 24 + 8) → `PlanException`; Default-Split ohne Dialog (`CrateMix.DefaultSplit`: die zwei größten Teile, 32 → 16 + 16), 1er nicht splittbar, `parts` bleibt API; Merge nur, wenn die Vereinigung genau der Quader der neuen Größe ist (`CrateLayout.BlockProblem`: Überlappung, „not adjacent“, „don't form a block“, falsche Maße), Ergebnis an der Min-Ecke; Merge nicht über die Max-Kistengröße des Vertrags und nur innerhalb einer Missionszeile (SCU pro Zeile ist Vertragsdatum); die erste passende Zeile zieht mit (`MissionItem.WithCrateList`, absteigend sortiert, gleich der Zerlegung → `null`); Aufruferfehler `ArgumentException` (fremdes Schiff, Kiste nicht im Plan oder doppelt, Delivered, < 2 Kisten), Spielregeln `PlanException`; `SplitProblem` zusätzlich zu `MergeProblem` für den Tooltip; Teile an den Index der alten Kiste, gemergte Kiste an den kleinsten Index, Split → Merge = Original; Auswahl als TwoWay-Dependency-Property `ShipViewControl.Selection` ↔ `OverlayViewModel.Selection`, `CrateSelection.Of` für die Ergebniskisten (neue minus alte); Buttons Cut `E8C6` / Link `E71B` links vom Stift, gemeinsamer `HeaderButtonStyle`, beim Lock ausgeblendet, `ToolTipService.ShowOnDisabled`, `IconButtonStyle` mit Opacity 0.4 bei Disabled; Notice „Split refused: …“/„Merge refused: …“; Verifikation korrigiert (Tooltip nach Split `(1 of 2)` für einen 2er und `(x of 3)` für die 4er statt `(1 of 5)`: der Zähler läuft im abgeleiteten Placement, das nach Kistengröße trennt; „not adjacent“ mit Riker-2ern nach zwei Splits, weil Lorville-2er in der Schema-1-Datei Max 2 haben und an der Max-Grenze scheitern). Präzisierung in der Planphase, ratifiziert durch Freigabe (D27).
- `LoadPlanner.SplitCrate`/`SplitProblem`, `MergeCrates`/`MergeProblem` auf `SavedPlan`
- Footprint-Regel, Merge-Formregel, Mission zieht mit
- Split-/Merge-Buttons in der Kopfzeile, Auswahl bleibt auf den Ergebniskisten
- D27, Nachträge D23/D25, README „When the kiosk delivers other crates“, plan-format.md

**Akzeptanz:** `dotnet test` grün; CLI-Ausgabe aller Fixtures byte-gleich; split_and_shared: Riker-Titanium-4er splitten → zwei 2er im Fußabdruck, Legende „Riker · 20“, beide gewählt → Merge → wieder 4er; Editor zeigt nach Split „4+4+4+2+2“ kursiv; zwei getrennte 2er → Merge grau mit „not adjacent“. Im Spiel am Kiosk: abweichende Mischung in unter 15 s korrigiert.

Phase 3 ist mit T3.15 abgeschlossen; offen bleiben nur Backlog-Punkte.

---

## Phase 4 – Settings & Konfiguration

Regel für alle Tasks dieser Phase: **Core bleibt unverändert**, alles in `CargoPlan.App`. Grundlage: D28 (Halo statt Backdrop-Blur), D29 (Stop-Farben fest), D30 (Apply-on-change). Reihenfolge der Umsetzung = Reihenfolge hier, nicht die Nummer.

Einstellbare Werte:
- **Fenster:** Transparenz, Hintergrund, Rahmen
- **Legende:** Schriftgröße, Schriftfarbe, Halo
- **Kisten:** Rack-Nummern-Schriftgröße und -farbe, Hintergrund-Transparenz, Rahmen-Transparenz, Halo
- **Kopfzeile/Reiter:** Schriftfarbe, Halo
- **Buttons:** Hintergrund, Rahmen, Halo, Eckenradius

### [x] T4.1 Settings-Modell Schema 2 + Theme-Ressourcen
**Warum:** Jede Einstellung braucht erst einen Platz in settings.json und einen Weg in die Oberfläche; das Settings-Fenster (T4.2) baut darauf auf.
**Präzisiert in der Planung:** Top-Level `overlay`, `hotkeys`, `appearance` (`window`, `header`, `legend`, `crates`, `buttons`); `legend` an/aus + Position kommt mit T4.3, Rack-Nummern-Schriftgröße mit T4.6, beide ohne Schema-Bump; Transparenz = Alpha der Farbe (`#AARRGGBB`), Kisten-Alpha als Zahl 0–255 (Farben fest, D29); Wert außerhalb des Bereichs → Clamp + Notice, ungültige Farbe → Default dieses Werts + Notice, unbekannte Keys/falscher Typ weiter strikt (ganze Datei, D13); klickende Hintergründe mindestens Alpha 1; Halo pro Abschnitt `enabled`/`color`/`size`, Text weich (`DropShadowEffect`), Rack-Nummern scharfer Umriss; Button-Rahmen nur für Glyph-Buttons, „Open editor“ behält seinen Umriss in Legendenfarbe; `legend.fontSize` `null` = Windows-Nachrichtenschrift (auf dem Dev-Rechner 11, nicht 12); Schema 1 wird erst beim nächsten Speichern als 2 geschrieben, dann vollständig; Ressourcen aus Code (`ThemeResources.Apply`), nicht als Literale in App.xaml; Halo aus = Key entfernt (Spike: `null` per Indexer liefert den Property-Default, nicht `null`); positionale Records statt `init`-Properties (Spike: Source-Gen in .NET 10 setzt fehlende `init`-Properties auf null/0); Zahnrad bleibt in T4.2. Pixelvergleich alt/neu ohne Datei: 0 abweichende Pixel. Präzisierung in der Planphase, ratifiziert durch Freigabe (D31).
- settings.json `schemaVersion` 2 mit `appearance`, `legend`, `hotkeys`; Fensterposition und -größe bleiben wie in T3.4
- Theme-Werte als Ressourcen in `App.xaml`, Views binden per `DynamicResource` (Grundlage für die Live-Vorschau in T4.2)
- Noch ohne UI: alle Werte wirken bereits aus settings.json
- Schema 1 bleibt lesbar, Defaults für Neues; kaputte Datei → Defaults + Notice (D13)

**Akzeptanz:** `dotnet test` grün (Schema 1 → Defaults für Neues, Schema-2-Rundlauf, kaputte Datei → Defaults + Notice, Datei unverändert); Wert in settings.json ändern → nach Neustart sichtbar; Core unverändert.

### [x] T4.6 Rack-Nummern und Linienstärken beim Zoom konstant
**Warum:** Rack-Nummern und Linien wachsen heute mit dem Zoom mit (Backlog aus D21). Eine Schriftgröße als Einstellung wäre damit bei jedem Zoom eine andere. Voraussetzung für die Rack-Nummern-Schriftgröße als Einstellung.
**Präzisiert in der Planung:** Linien waren schon konstant (Zoom skaliert die Punkte, nicht die Zeichnung), also keine Code-Änderung bei den Pens; nur die Rack-Nummer wuchs. `crates.rackNumberFontSize` Default 27 (alte Größe bei Zoom 1 im Standardfenster), Bereich 8–48; passt die Nummer nicht mehr aufs Rack, schrumpft sie bis zur kürzesten Rack-Seite, nicht unter 12 (D32). Die Backlog-Zeile stand nur in D21 und ist dort per Addendum erledigt. Präzisierung in der Planphase, ratifiziert durch Freigabe.
- Rack-Nummern in fester Bildschirmgröße, Linienstärken (Kanten, rote Kanten, Drop-Ziel, Auswahl) fest; Kisten und Zellen skalieren weiter
- Backlog-Zeile entfällt
- Danach `crates.rackNumberFontSize` in settings.json (optional, kein Schema-Bump, D31), Key `CratesRackNumberFontSize`

**Akzeptanz:** `dotnet test` grün; Zoom 0,5× bis 4×: Nummern und Linien bleiben gleich groß, Kisten skalieren; Hit-Test und Drop-Ziel unverändert.

### [x] T4.3 Legende an/aus + Position
**Warum:** Je nach Fensterform und Platz neben dem Kiosk steht die Legende besser oben oder unten, links, mittig oder rechts. Wer die Farben kennt, blendet sie aus.
**Präzisiert in der Planung:** `legend.position` als String mit sechs Namen (`bottom-left` …, kleingeschrieben), geprüft in `AppearanceRules` wie eine Farbe: ungültig → `bottom-left` + kurze Notice („edge-alignment, e.g. bottom-right“), kein Enum-Konverter, der die ganze Datei kippen würde; left/center/right verschiebt einen Block in Inhaltsbreite, innen linksbündig (Swatches in einer Spalte), die Hint-Reserve misst mit; oben: Kopfzeile → Notices → Block → Ansicht, Notices immer am Außenrand in voller Breite, nicht im Block; Live-Umschaltung über Style-Trigger auf `OverlayViewModel.IsLegendVisible`/`LegendPosition`, nicht DynamicResource; Legenden-Drag ohne Code-Änderung (die Ansicht arbeitet in eigenen Koordinaten); „aus“ nimmt den Legenden-Drag mit, ein Stop zieht dann über die Auswahl um (README/settings-format); Akzeptanz „Legende aus + kaputte settings.json“ als Wertfehler bei `visible: false` vorgeführt, weil eine ganz kaputte Datei Defaults lädt und die Legende damit ohnehin an ist; Prüfung offscreen per `RenderTargetBitmap`: bottom-left 0 abweichende Pixel, kein Sprung beim Abhaken in allen zwölf Layouts. Präzisierung in der Planphase, ratifiziert durch Freigabe (D33).
- Legende an/aus und Position (top/bottom × left/center/right) in settings.json, Top-Level `legend` (optional, kein Schema-Bump, D31)
- Layout-Umbau des Overlays; der reservierte Hinweisplatz (D16) bleibt, die Ansicht springt beim Abhaken nicht
- „aus“ blendet nur die Stop-Zeilen aus; Hinweise und Notices bleiben sichtbar (an der Legendenposition), sonst sähe niemand die D13-Notice „kaputte settings.json → Defaults“
- Abhaken (D16) und Legenden-Drag (D25) in jeder Position; ausgeblendet bleibt Abhaken per Klick in der Ansicht

**Akzeptanz:** alle sechs Positionen und „aus“ per settings.json; Abhaken und Legenden-Drag in jeder Position; Ansicht springt beim Abhaken nicht; Legende aus + kaputte settings.json → Notice sichtbar; Core unverändert.

### [x] T4.2a Zahnrad + Settings-Fenster (Window, Legend, Legend layout)
**Warum:** settings.json von Hand zu bearbeiten schreckt Hauler ab (D17). Die Lesbarkeit über hellem und dunklem Hintergrund stellt man im Spiel ein, nicht im Texteditor.
**Präzisiert in der Planung:** Schnitt in T4.2a/T4.2b nach Abschnitten, jeder Teil für sich nutzbar; `SettingsSession` als einzige Besitzerin von `AppSettings` (Start und Live über dieselbe Apply-Methode, `ChangeLook` normalisiert wie das Laden und speichert entprellt, Bounds über die Session statt eigener Kopie im `OverlayBoundsSaver`); `SettingsViewModel` WPF-frei, liefert Appearance + Legend-Layout statt ganzem `AppSettings` (sonst veraltete Bounds); Farbe = Hex `#RRGGBB` + Alpha-Regler + Swatch, ungültig → Feld markiert (Hintergrund, weil der System-Rahmen bei Fokus blau wird), kein Apply, keine Notice; Reglergrenzen aus `AppearanceRules`; „Use Windows text size“ = `null`; Legend layout eigener Abschnitt mit eigenem Reset; Fenster im Systemlook, eine Instanz, Esc/X schließen, neben dem Overlay, Position nicht gespeichert; Zahnrad rechts neben dem Stift am Schloss; Rettungsweg Tooltip + README statt Code; kein Hotkey-Platzhalter. Präzisierung in der Planphase, ratifiziert durch Freigabe (D34).
- Zahnrad in der Kopfzeile, verhält sich beim Lock wie der Stift: eine Regel für alle Kopfzeilen-Buttons
- Settings-Fenster: aktivierbar, Topmost, bei Auto-Lock ausgeblendet wie der Editor (D19), eins gleichzeitig
- Abschnitte Window / Legend / Legend layout
- Live-Vorschau: jede Änderung wirkt sofort und wird gespeichert, kein OK/Cancel (D30); „Reset to defaults“ pro Abschnitt

**Akzeptanz:** jeden Wert der drei Abschnitte im Fenster ändern → Overlay ändert sich sofort, Neustart behält ihn; Reset pro Abschnitt; Auto-Lock blendet das Fenster aus und wieder ein; Core unverändert.

### [x] T4.2c Color-Picker im Settings-Fenster
**Warum:** Hex-Codes kennt kaum ein Hauler; eine Farbe sucht man mit der Maus, während man das Overlay über der Szene sieht.
**Präzisiert in der Planung:** eigenes Popup statt Paket oder Windows-Farbdialog (der wäre modal, ohne Live-Vorschau); Swatch als Toggle, Popup mit Sättigung/Helligkeit-Fläche (`SaturationValuePad`) und Farbton-Leiste, Alpha bleibt eigener Regler; `Hsv` WPF-frei; der Picker schreibt `Hex`, also ein Weg zum Overlay; Grau behält den Farbton, Schwarz die Sättigung; Esc schließt bei offenem Picker nur den Picker, sonst das Fenster (Gegenprobe ComboBox); der Picker schließt immer, wenn das Fenster unsichtbar wird (Auto-Lock = Tractor-Beam-Griff). Präzisierung in der Planphase, ratifiziert durch Freigabe (D34-Addendum).
- Klick auf den Swatch öffnet den Picker, Ziehen ändert das Overlay live
- Gilt für jede Farbe im Fenster, auch die aus T4.2b

**Akzeptanz:** Farbe per Picker wählen → Overlay ändert sich beim Ziehen, Hex-Feld folgt, Neustart behält sie; Esc mit offenem Picker schließt nur den Picker; Auto-Lock schließt den Picker; Core unverändert.

### [x] T4.2b Settings-Fenster: Header, Crates, Buttons
**Warum:** Der Rest der Werte aus T4.1 gehört ins selbe Fenster, sonst bleibt für Kopfzeile, Kisten und Buttons der Texteditor.
**Präzisiert in der Planung:** Reihenfolge Window → Legend → Legend layout → Crates → Header → Buttons (am Aufzug ist die Kisten-Füllung der nächste Regler nach Transparenz und Legende); jeder Halo nennt, was er umgibt (`HaloEditor.Label`), bei den Kisten „Outline around the rack numbers“; Kisten-Alpha in Prozent wie die Farben (`ColourField.Percent`), gespeichert 0–255; Kopfzeilenfarbe „Text and glyphs“ mit Hinweis auf den Rettungsweg. Präzisierung in der Planphase, ratifiziert durch Freigabe (D34-Addendum). Beim Laufzeit-Check gefunden und mit behoben: der Swatch aus T4.2c war 0 × 0 groß (der Button zentriert seinen Inhalt), jetzt gestreckt.
- Abschnitte Header (Text colour, Halo), Crates (Fill/Edge alpha 0–255, Rack number colour, Rack number size 8–48, Halo/Umriss), Buttons (Background Alpha ≥ 1, Border colour/width, Corner radius, Halo) mit `ColourField`/`HaloEditor` aus T4.2a
- „Reset to defaults“ pro Abschnitt, Tests nach dem Muster von T4.2a
- README/settings-format: „still set in the file“ entfällt

**Akzeptanz:** jeden Wert der drei Abschnitte im Fenster ändern → Overlay ändert sich sofort, Neustart behält ihn; Reset pro Abschnitt; Core unverändert.

### [x] T4.4 Hotkey-Aufnahme
**Warum:** Hotkeys sind seit T3.7 nur über settings.json setzbar (D17). Tasten drücken ist schneller als tippen, und ein Tippfehler macht die ganze Datei ungültig.
**Präzisiert in der Planung:** Aufnahme über KeyDown des eigenen fokussierten Fensters (`RecordedKey`, kein Hook); unsere Hotkeys während der Aufnahme ausgesetzt, sonst kämen ihre Tasten nie an; `HotkeyParser.TryCreate` teilt Regeln und Meldungen mit `TryParse`; belegte Kombination (eigene andere Aktion per Name, anderes Programm per `RegisterHotKey`) wird abgelehnt und nicht gespeichert, der alte Hotkey bleibt aktiv; Meldung am Feld statt Notice; „Clear“ schreibt `null`, `null` und fehlend gelten gleich; Hinweis „Ctrl, Alt or Win required. Win combinations may be taken by Windows itself.“; Esc bricht nur die Aufnahme ab, Fokusverlust/Ausblenden/Schließen ebenso; kein „Reset to defaults“ (Default = kein Hotkey); eigener Abschnitt ganz unten, eigener DataContext. Präzisierung in der Planphase, ratifiziert durch Freigabe (D35).
- Abschnitt Hotkeys im Settings-Fenster: je Aktion (Lock, Overlay) Aufnahme per Tastendruck, Löschen möglich
- Regeln wie `HotkeyParser` (Ctrl/Alt/Win Pflicht, F12 abgelehnt), weiterhin keine Defaults (D17)
- Belegte Kombination → Konfliktmeldung aus T3.7

**Akzeptanz:** Hotkey im Fenster setzen → wirkt sofort ohne Neustart, steht in settings.json; belegte Kombination → Meldung, der andere Hotkey bleibt aktiv; Shift+L und F12 werden abgelehnt.

### [x] T4.5 Füllrichtung im Editor
**Warum:** Ein Koordinator soll allen dieselbe Füllrichtung zeigen (T3.10). Seit D24 steht sie im Plan (`fillOrder`), wählbar ist sie aber noch nicht.
**Präzisiert in der Planung:** ComboBox „Fill order:“ links vom Plan-Button (derselbe Begriff wie der Key `fillOrder`), Einträge „Bow first“/„Stern first“ (`FillOrderChoice`); Änderung markiert „unplanned changes“, wirkt erst mit Plan; Clear rows und Import behalten die Richtung; Startwert aus dem aktuellen Plan, ohne Plan Bug zuerst; Mission-Dateien tragen die Richtung nicht (Backlog, Core-Format). Präzisierung in der Planphase, ratifiziert durch Freigabe (D24-Addendum).
- Auswahl Bug zuerst / Heck zuerst im Editor, wirkt beim nächsten „Plan“
- Gespeichert im Plan (D24), nicht in settings.json
- Core unverändert: `LoadPlanner.Plan` nimmt `fillOrder` schon

**Akzeptanz:** „Heck zuerst“ → Plan → Kisten beginnen am Heck; Neustart behält die Richtung (plan.json); der Editor startet mit der Richtung des aktuellen Plans.

### [x] T4.7a Werkzeugfenster-Theme: Fluent-Spike + Musterseite
**Warum:** Settings-Fenster, Editor und Picker stehen im hellen Systemlook (D34) und blenden über dem dunklen Spiel. Drei feste Modi light/dark/system; das Overlay bleibt `appearance` (D34).
**Präzisiert in der Planung:** zuerst Fluent-Spike (`Window.ThemeMode`, nur auf Werkzeugfenstern, `Application.ThemeMode` bleibt `None`), Timebox halbe Session; Weiche F (Fluent) / E (eigene Templates) nach sieben Kriterien: 1 Overlay und Lock-Fenster unverändert, Gear-Tooltip im Systemlook; 2 jedes verwendete Control in light und dark lesbar (DataGrid, ComboBox-Popup, ScrollBar, ToolTip, Kontextmenü, Picker-Popup); 3 `System` folgt live, Titelleiste mit; 4 Settings-Fenster bei 400 px ohne Layoutbruch; 5 Akzent überschreibbar, `WPF0001` lokal per `#pragma`; 6 Markierungsfarben (ungültig, Problem, Warnung) folgen dem Modus; 7 **Martins Blick im Spiel** („Overlay oder Windows?“). **Die Session hält nach dem Spike an**, erst nach Martins Urteil geht es mit E oder F weiter. Weg E: Palette als C#-Record ohne Defaults (Compiler erzwingt gleiche Keys), Templates in einem pro Werkzeugfenster gemergten `ToolStyles.xaml` (nicht App.xaml, sonst trifft es Overlay-Tooltips), `WM_SETTINGCHANGE` am HwndSource mit Neulesen von `AppsUseLightTheme`, DWM-Titelleiste (`DWMWA_USE_IMMERSIVE_DARK_MODE` = 20, Fehler ignoriert), High Contrast → Systemlook, Kontrast-Test ≥ 4,5:1; reicht die Session nicht, Schnitt in 4.7a/4.7a2 (ScrollBar, DataGrid, ContextMenu, Popup). Präzisierung in der Planphase, ratifiziert durch Freigabe.
**Präzisiert nach dem Spike (Weg F, Akzent Teal fest):** `Settings/ToolWindowTheme` (Enum System/Light/Dark, WPF-frei; Parsen erst 4.7b); `Theme/ToolWindowThemer.Apply` setzt `ThemeMode` pro Fenster (einzige `WPF0001`-Stelle) und Teal über die sieben `SystemColors.AccentColor*Key`; Muster für eigene Styles: `Apply` vor `InitializeComponent`, `BasedOn` Fluent, eigene Styles eine Ebene unter dem Fenster (sonst kein Base), DataGrid-Zeilen `BasedOn` `DefaultDataGridRowStyle`; `HueSliderStyle` in App.xaml; deaktivierte Slider/TextBox mit Opacity 0.4; Popup über `FlyoutBackground`; Markierungen über `SystemFillColor*`; Fluent kann Zeilen nicht tönen, eine ungültige Zeile bekommt deshalb einen 1-px-Rahmen in `SystemFillColorCriticalBrush` (statt roter Schrift); Musterseite öffnet neben dem Overlay statt allein (Gate 1 und 7). Präzisierung in der Planphase, ratifiziert durch Freigabe (D36).
- Musterseite `--theme-sample`, nur im Debug-Build: jedes Control der Werkzeugfenster, Moduswahl light/dark/system live
- Spike-Ergebnis pro Kriterium in D36

**Akzeptanz:** Kriterien 1–6 belegt; Martin nimmt die Musterseite in beiden Modi im Spiel ab; `--theme-sample` fehlt im Release-Build; Core unverändert.

### [x] T4.7b Werkzeugfenster-Theme: Setting + Verdrahtung
**Warum:** Erst mit Setting und Verdrahtung sieht der Spieler den Modus in Settings-Fenster, Editor und Picker.
**Präzisiert in der Planung:** Top-Level `"toolWindowTheme"` (light|dark|system, klein), optional, kein Schema-Bump; `null` und fehlend = `system` ohne Notice (wie `legend: null`, T4.3), ungültiger String → `system` + Notice wie `legend.position`; nicht unter `appearance`, damit „Reset to defaults“ ihn nicht mitnimmt; `Settings/ToolWindowTheme` WPF-frei (Names/TryParse/Resolve), die WPF-Seite heißt `ToolWindowThemer`; Abschnitt „Tool windows“ nach Buttons, vor Hotkeys, ComboBox „Follow Windows / Light / Dark“, Apply-on-change (D30), kein Reset. Präzisierung in der Planphase, ratifiziert durch Freigabe.
**Präzisiert in der Umsetzungsplanung:** kein `Resolve` (nur Weg E brauchte es, Fluent folgt Windows selbst); `OverlayWindow` schaltet die offenen Werkzeugfenster live um, weil es beide besitzt, neue Fenster bekommen den Modus im Konstruktor; die Musterseite schaltet nur noch sich selbst (sonst kämpft sie gegen das Setting); `AppSettings.ToolWindowTheme` ist ein String, geparst über `ToolWindowThemes` (Names/TryParse/Parse/ToName) neben dem Enum. Präzisierung in der Planphase, ratifiziert durch Freigabe (D36).
**Präzisiert nach der Abnahme:** Text im Dark-Modus grau (`#D0D0D0`, nach Augenmaß aus drei Kandidaten) statt Fluents reinem Weiß; dafür kommt `Resolve` zurück (Registry wie Fluent) und `SystemEvents.UserPreferenceChanged` hält „Follow Windows“ live; Fokusrahmen bleibt weiß, High Contrast unangetastet. Präzisierung in der Planphase, ratifiziert durch Freigabe (D36, Nachtrag).
- `SettingsSession.ChangeToolWindowTheme`, entprellt gespeichert wie Hotkeys
- Settings-Fenster, Editor und Picker-Popup folgen dem Modus; Datei-Dialoge und MessageBox bleiben System
- settings-format.md, README, D36

**Akzeptanz:** Modus im Fenster wechseln → Settings, Editor und Picker folgen sofort, Neustart behält ihn; „system“ folgt dem Windows-Wechsel live; Reset eines Abschnitts lässt den Modus stehen; `"toolWindowTheme": "blue"` → system + Notice, `null` → system ohne Notice; Overlay unverändert; Core unverändert.

---

## Phase 5 – Import aus ContractTracker V2 (vor dem Release, weil die meisten Hauler CT parallel laufen lassen und keine Missionsdateien schreiben)

Umnummeriert mit D37: der Import war Phase 6, der Release Phase 5. T5.1 und T6.1 sind neu.

### [x] T5.1 Exit-Button und feste Notices
**Warum:** Beenden geht heute nur über die Taskleiste. Notices wandern mit der Legendenposition (D33) und schieben oben die Ansicht; mit dem CT-Import kommen mehr davon.
**Präzisiert in der Planung:** Exit ruft `Close()` auf dem Overlay, denselben Weg wie die Taskleiste; die vorhandenen `Closed`-Handler (`OverlayBoundsSaver`, `SettingsSession.Flush`, `PlanSaver`) schreiben Ausstehendes, kein zweiter Speicherpfad, kein `Shutdown()`. Exit verhält sich wie das Zahnrad (`HeaderButtonStyle`, gesperrt `Hidden`), nicht wie das Schloss, das als eigenes Fenster immer sichtbar bleibt. Glyph E8BB ChromeClose, Tooltip „Exit“, ganz rechts in der Kopfzeile, Zahnrad links daneben. Notices werden gestapelt: eigenes Kind des äußeren DockPanels, unterste Zeile, volle Breite; eine untere Legende steht darüber, niemand weicht seitlich aus. Die README-Anleitung für ein unsichtbares Theme zeigt jetzt auf den zweiten Button von rechts. Präzisierung in der Planphase, ratifiziert durch Freigabe (Addenda zu D33 und D34).
- Exit-Button in der Kopfzeile zwischen Zahnrad und Schloss, Lock-Verhalten wie die anderen Header-Buttons (nicht fokussierbar, gesperrt ausgeblendet)
- Beenden ohne Dialog, schreibt Ausstehendes wie `overlay.Closed` (Settings, Plan)
- Notices immer fix unten links, unabhängig von `legend.position` (Addendum zu D33)

**Akzeptanz:** Haken setzen, sofort Exit → Neustart zeigt den Haken; gesperrt ist der Button nicht sichtbar; Legende `top-right` → Notice steht unten links; Core unverändert.

### [x] T5.2 Schema erkunden
**Warum:** Erst schauen, dann Code. ContractTracker ist closed source; ohne Fingerabdruck liest ein CT-Update still falsch.
**Präzisiert in der Planung:** Befund als D38 (Tabelle Frage → Befund → Konsequenz für T5.3/T5.4, nur Konzepte, keine Tabellen- oder Spaltennamen, damit D38 ein CT-Update übersteht) und in `docs/contracttracker-schema.md` (Pfad, Pragmas, Fingerabdruck, Mapping, offene Fragen; nur dort stehen Namen, nur diese Datei fällt bei einem Nein von Yuiki). Dazu die Fixture `tests/CargoPlan.Import.Tests/Fixtures/ct_schema.sql`, Testdaten, kein Produktcode. Zusätzlich beobachtet: Lebenszyklus F3/F4/F5, Löschen und Erledigen in der Hauptapp, Edit per Location-Mapping, Dateien bei laufendem und beendetem CT, Schreibmuster pro F3 (WAL-Commit-Frames). Werkzeug `sqlite3` per winget, kein NuGet. Lizenzfrage war schon gestellt, nur der Stand wird notiert. Präzisierung in der Planphase, ratifiziert durch Freigabe (D38).
- DB-Pfad finden (`%APPDATA%`/`%LOCALAPPDATA%`), in `docs/contracttracker-schema.md` notieren
- Kopie anlegen, an der Kopie `sqlite3 copy.db .schema` und `PRAGMA user_version`
- Schema-Fingerabdruck definieren: Tabellen + Spalten, die wir lesen
- Prüfen, ob die maximale Kistengröße in der DB steht (D23 nimmt das an)
- Mapping skizzieren: Spalten → `MissionStop.Destination`, `MissionItem.Commodity`, `Scu`, `MaxCrateSize` (D23), Pickup. Was fehlt?
- Lizenzfrage an Yuiki über den Nexus-Discord: dürfen wir die DB lesen und das dokumentieren?
- Doku minimal: nur die gelesenen Spalten

**Akzeptanz:** Doku vorhanden (Pfad, `user_version`, Fingerabdruck, Mapping, offene Fragen), Lizenzfrage gestellt und Stand notiert. Kein Code.

### [x] T5.3 Read-only-Reader und Rohtext-Parser
**Warum:** Ziele, Waren, SCU und max. Kistengröße automatisch, ohne die DB des laufenden Trackers zu stören. CT liefert Capture und OCR; die Zerlegung des Vertragstexts machen wir selbst (D38-Addendum: der Rohtext ist vollständig, CTs Parser verliert Ziele).
**Vor der Planung D38 + Addendum und D39 lesen.** Kein Auto-Sync in den Plan; der Reader liefert **Import-Kandidaten** für die Durchsicht im Editor (T5.4).
**Präzisiert in der Planung:** Gruppierung über lückenlose Ids statt gleicher Capture-Zeit (die zwei Zeilen einer Erfassung lagen 0,36 ms auseinander); der Parser liest nur zwischen `DETAILS` und `ACCEPT OFFER`, weil die Listenfragmente vor `DETAILS` stehen, nicht danach; Akzeptanz ergänzt. Präzisierung in der Planphase, ratifiziert durch Freigabe (D39).
**Präzisiert in der Planung (Umsetzung):** Normalisierung von Ware und Zielen an einer Stelle im Parser, auch für den Kindzeilen-Fallback; T5.4 vergleicht nur mit `OrdinalIgnoreCase` (D40). Ziel = Stationsname, abgeschnitten vor „ at …“/„ above …“; abgeschnittene Ziele (auch mitten im Wort, „to Thunde“) werden per Zeichen-Präfix aus der Drop-off-Liste vervollständigt, nur bei genau einem Treffer; ein übriges Fragment nimmt das eine längere Lieferziel desselben Captures, sonst bleibt es (bekannte Grenze, Capture 28 → „Thundering Express“); Pick-up-Liste und Prosa vervollständigen nicht (D40-Addendum). Anker `DETAILS`/`ACCEPT OFFER` unscharf (O/0, I/L/1), beide optional: die Accepted-Ansicht (37/38 beobachtet) endet mit `TRACK`/`ABANDON`, `DETAILS` steht in beiden (schema.md, „Raw text layouts“). Fingerabdruck um Capture-Zeit und Rohtext erweitert; `Pooling=False`, Busy-Retry 5 s über das Command-Timeout von Microsoft.Data.Sqlite; fehlende Datei → nichts gelesen, keine Notice (der Hinweis gehört T5.5). Ein Capture mit mehreren Waren ist ein Kandidat mit mehreren Items. CLI-Befehl `contracts <db> [--since <id>] [--raw]` für den echten Lauf und als Bug-Report-Weg (`--raw` hängt den Rohtext an). Präzisierung in der Planphase, ratifiziert durch Freigabe (D40).
- `CargoPlan.Import`: Reader liest Vertragszeilen mit Id, Capture-Zeit, Rohtext und Kindzeilen; Fingerabdruck (schema.md) vor dem Lesen prüfen, Abweichung → nichts lesen, Notice „ContractTracker schema not supported – enter missions in the editor or use Import…“ (der Text zitiert den Editor-Knopf, nicht umgekehrt)
- Connection-String `Mode=ReadOnly`, Tracker schreibt parallel (WAL); Lock → Retry, keine Notice. Eigener Lesezugriff legt `-wal`/`-shm` an (D38) – dokumentiert, kein Aufräumen
- **Gruppierung:** Zeilen mit byteweise gleichem Rohtext und lückenlos aufeinanderfolgenden CT-Ids = eine Erfassung = ein Kandidat (D38: eine Transaktion pro Capture); Schlüssel des Kandidaten sind seine CT-Ids (AUTOINCREMENT, nie wiederverwendet). Kein Inhaltsschlüssel – Pickup ist unbrauchbar, gleiche Inhalte sind nicht unterscheidbar (D38)
- **Parser auf den Rohtext:** Ware, Ziele mit SCU aus den „Deliver … to …“-Zeilen und der Drop-off-Liste; tolerant gegen OCR-Fehler (`Dellver`, `O/3`, `LS` für `L5`); liest nur zwischen `DETAILS` und `ACCEPT OFFER`: Listenfragmente (`· Stims (3 SCU)`, abgeschnittene Zeilen) stehen davor im Kopfblock, danach nur Ziffernrauschen (D38-Addendum). Kindzeilen nur als Fallback, wenn der Parser kein Ziel findet
- **Max. Kistengröße** aus zwei beobachteten Phrasen („Max size will be N SCU.“, „none of the containers are bigger than N SCU.“, Zeilenumbruch innerhalb erlaubt); fehlt sie → Default größte Kiste, die in die SCU passt, Kandidat trägt `crateSizeAssumed = true`
- **Kandidat** (`ContractTrackerCandidate`, nur in `.Import`): Mission in Eingabetypen (`MissionStop`/`MissionItem`, D23) + CT-Ids + Capture-Zeit + Hinweise für die Durchsicht: `crateSizeAssumed`, `usedFallbackRows`, Zielanzahl laut Parser vs. laut Drop-off-Liste (Abweichung = unvollständig)
- `ReadSince(lastId)`: nur Zeilen mit Id > Wasserstand; der Wasserstand selbst gehört zum Plan (T5.4)
- Tests: Fixture-DB `ct_schema.sql` (nur gelesene Spalten) + Rohtext-Fixtures aus den echten Captures (anonymisiert, ohne Datum), darunter absichtlich: OCR-Fehler, Listenfragmente, 2+1-Split, fehlende Größenphrase, Fallback-Fall. Parser ohne DB testbar
- Offen (schema.md): deutscher Spiel-Client nicht beobachtet; ob CT Mappings beim nächsten Capture anwendet

**Akzeptanz:** Tests grün: Fingerabdruck-Abweichung → nichts gelesen + Notice; 2+1-Split → ein Kandidat mit zwei Ids; 1-Ziel-Capture mit `Dellver`/`O0/5` → drei Ziele; Listenfragmente vor `DETAILS` ergeben keine Ware; beide Größenphrasen erkannt, fehlende → `crateSizeAssumed`; kein Parser-Ziel → Kindzeilen + `usedFallbackRows`; `ReadSince` liefert nur Ids > Wasserstand; Parser-Tests ohne DB; Accepted-Capture 37/38 mit Umbruch mitten im Wort → „Thundering Express Station“ (Namen vollständig). Echter Lauf gegen eine Kopie von Martins DB liefert plausible Kandidaten.

### [x] T5.3b Parser-Härtung aus dem Spieltest
**Warum:** Spieltest 2026-10-21: 47 „to Everus Harbor aboy“ und 48 „… abov“ ergeben Zielnamen mit abgerissenem „above“; „Max size willb 1 SCU“ ergibt `?`; 47 und 48 sind zwei echte Verträge (Müll nach Everus Harbor, andere Belohnung), aber `⚠ duplicate?` – bei Einzelziel-Verträgen zum selben Ort mit gleicher Ware (Covalex-Alltag) ist die Teilmengen-Regel ohne Belohnung immer falsch-positiv; unser Overlay stand in CTs Capture-Region und steht im Rohtext.
**Präzisiert in der Planung:** Größenphrase erlaubt bis zu zwei Wörter zwischen „size“ und Zahl (das Muster mit einem Wort trifft „will be“ nicht); Ziffernkorrektur O→0, I/l/|→1 nur in der Zahl der Größenphrase und nur, wenn eine gültige Kistengröße herauskommt (in der Lieferzeile gibt es keine); Schlusswort = Zeichen-Präfix von „above“/„at“ ab zwei Zeichen oder, ab vier Zeichen, ein Zeichen Abweichung von „abov“/„above“, danach gilt der Name als abgeschlossen; Prosa-Name endet vor „ at“, „ above“ oder „.“, Umbrüche erlaubt; der bestehende Test „Pick-up-Liste und Prosa vervollständigen nicht“ wird aufgeteilt, weil „taken to …“ jetzt vervollständigt; Belohnung nur, wenn ihre Zeile ganz Zahl ist (OCR „5O,250“ → unbekannt, nicht 5); die Belohnung wird im Plan gespeichert (`ContractSourceDto`, optional, Schema bleibt 3), sonst wären 47/48 nach dem Neustart wieder markiert. Präzisierung in der Planphase, ratifiziert durch Freigabe (D39/D40-Addenda).
- **Abgerissenes Schlusswort:** Lieferzeile ohne Abschluss, die auf ein Präfix von „above“/„at“ oder eine Zeichenabweichung davon endet (`abov`, `aboy`, `ab`) → Wort entfernt
- **Größenphrase worttolerant:** `Max size <Wort(e)> N SCU`, `bigger than N SCU`, mit Ziffernkorrektur
- **Vervollständigung aus der Prosa,** nur mit „to“: „to a freight elevator at <Name>“ liefert Zielnamen für Zeichen-Präfix-Treffer; „from a freight elevator at …“ nie (Pickup); Verb egal. Reihenfolge: Drop-off-Liste → Prosa → Fragment-Merge (D40-Addendum)
- **Belohnung** aus „Reward“ am Textanfang → `ParsedContract.Reward` → Kandidat → `ContractSource.Reward` (Core, ein Feld). Duplikat = Teilmenge **und** Belohnung gleich oder mindestens eine unbekannt (D39-Addendum)
- **Overlay-Text im Rohtext:** README-Satz im Abschnitt ContractTracker, Overlay außerhalb von CTs Capture-Region (F6)
- Fixtures aus den echten Rohtexten (anonymisiert, ohne Datum): Id 47, Id 48, Silicon-Capture mit Overlay-Text
- Nachtrag: CLI `contracts` hängt ` · reward 50,250` an die Kopfzeile, wenn die Belohnung bekannt ist; sie entscheidet über Duplikate, ein Bug-Report braucht sie

**Akzeptanz:** Tests grün: 47 → „Everus Harbor“, max. 1 SCU; 48 → „Everus Harbor“, Größe weiter `?`; Silicon → genau ein Ziel mit 7 SCU, max. 1 SCU, keine Overlay-Zeile als Ware oder Ziel; „to“-Prosa vervollständigt, „from“-Prosa nicht; 47 vs. 48 nicht markiert, Retry mit gleicher Belohnung markiert; Belohnung übersteht Speichern und Laden. Echter Lauf gegen eine Kopie von Martins DB zeigt 47/48 als „Everus Harbor“. Core-Änderung nur Belohnung (Feld + Vergleich).

### [x] T5.4a Stapelregel nach Zielreihenfolge
**Warum:** Eine Kiste, auf der entlang der Stapelachse eine andere liegt, lässt sich im Spiel nicht entnehmen (Martin, Spike T5.4). `CrateLayout.Place` legt nach Größe, nicht nach Route: Im vollen Hull A (A 20, B 12, C 16, D 16 SCU, max. 4) liegt B in Rack 2 auf A, das zuerst abgeladen wird. Mit A in 2er-Kisten liegt A auf B: das ist das Soll, die obere Kiste geht zuerst runter. Der Plan ist heute schon falsch, mit oder ohne CT – **Release-Blocker**. Vor T5.4, weil `PlaceInto` (D41) `CrateLayout.Place` nutzt und die Regel erbt: eine Regel, eine Stelle.
**Präzisiert in der Planung:** Regel weich – Verstoß = Befund, nie Abbruch (Spike: ab drei Stops in einem Rack gibt es Mischungen ohne regelkonforme Anordnung, z. B. 8/4/1 SCU); `Place` legt wie bisher, nur bei Verstoß sucht es im Rack exakt nach einer Anordnung in Routenreihenfolge, sonst bleibt das erste Layout. Zwei Befundklassen: blockierend (Platz, außerhalb, Überlappung, Stütze) lehnt Moves ab, Reihenfolge wird nur angezeigt – Drag und `MoveStop` legen und markieren. Reihenfolge = `LoadPlan.Route` (erstes Auftreten in den Kisten, wie Legende/Palette), kein Formatwechsel. `Partition` unverändert (formbewusst → Backlog). Gespeicherte Pläne mit falscher Stapelung: rot, keine Neuanordnung. Abgehakte Kisten freigeben → T5.4. Exhaustive-Test (`Category=Exhaustive`, Brute-Force-Referenz) nicht im Standardlauf (`default.runsettings`), einmal in der DoD. Präzisierung in der Planphase, ratifiziert durch Freigabe (D24-Addendum).
- **Regel (D24-Addendum):** Auf einer Kiste darf nur eine Kiste desselben oder eines früheren Stops liegen, nie ein späterer Stop auf einem früheren. Reihenfolge = Stop-Reihenfolge des Plans (Legende/Route). Eine Regel in `CrateLayout`, genutzt von `Place` und den Prüfungen
- Gilt für `Plan`, `MoveStop`, Drag (D25) und `Validate`; Verstoß → Kiste rot, Befund in der Legende
- Bestehende Planer-Tests mit geteilten Racks prüfen: Layouts ändern sich, wo heute ein späterer Stop auf einem früheren liegt

**Akzeptanz:** Tests grün: A 20 / B 12 / C 16 / D 16 in 4ern → A liegt außen auf B, nie B auf A; vier Stops à 12 (4er) + 16 SCU in 8ern → geht auf, weil frühere Stops auf späteren liegen dürfen; Drag einer Kiste eines späteren Stops auf eine eines früheren → gelegt, Kiste rot, Befund in der Legende; bestehende Planer-Tests grün oder bewusst angepasst und begründet. Im Overlay zeigt der erste Plan Rack 2 mit A außen auf B.

### [x] T5.4 Auto-Apply mit Markierungen + inkrementell platzieren
**Warum:** Hauler drücken F3 am Terminal und schauen aufs Overlay, den Editor öffnen sie nur bei Fehlern (D39-Addendum). CT-Importe sind je Capture lückenhaft (D38), deshalb sichtbar markieren statt still übernehmen. Hauler nehmen Aufträge nacheinander an; neu planen löscht Positionen und Haken (T3.8b).
**Präzisiert in der Planung:** Neu gefasst nach dem D39-Addendum (Revision des Prüforts von D39, ratifiziert durch Freigabe): Markierung in der Legende statt Pflichtschritt im Editor; jede Erfassung eine eigene Mission, Duplikate nur markiert; Entfernen einer Mission bewegt keine anderen Kisten. Präzisierung in der Planphase, ratifiziert durch Freigabe.
**Präzisiert in der Planung (Umsetzung):** Zwei Commits, T5.4b Core + Import (ohne WPF testbar), T5.4c App.
- **Erster Sync:** Wasserstand 0 → alles aus CT, auch bei migriertem Schema-2-Plan.
- **Unplatzierte Missionen:** Retry bei jedem Sync, nicht nach einem Haken.
- **Duplikat:** Teilmenge der (Ziel, Ware)-Paare statt Überlappung (Beleg 36 vs. 37/38), abgeleitet statt gespeichert; kein Marker für den Kindzeilen-Fallback.
- **Abgehakte Kisten:** grau bis überbaut, kein Drop-Ziel; Haken unter einer liegenden Kiste → rot, nicht abgelehnt.
- **Schema 3:** Stops unter der Mission statt Id am Item, Stop-Sicht abgeleitet.
- **Reihenfolge:** Legende und Stapelregel nach `UnloadOrder` (erste Kiste an Bord), `Route` nur für die Farben; Korrektur bei der Freigabe.
- **„In CT gelöscht“** = alle Ids der Erfassung fehlen; „nicht gelesen“ ist nie „gelöscht“.
- **Sync pausiert** bei kaputtem plan.json.
- **Fehlerfälle:** Fehlende DB ohne Notice, Datei ohne SQLite-DB mit Notice.
- **Delete mission:** Statuszeile „plan updated“.
- **README:** „retry before you load“.

Präzisierung in der Planphase, ratifiziert durch Freigabe (D24/D39/D41/D42-Addenda).
- `FileSystemWatcher` auf DB + `-wal` übernimmt Kandidaten seit dem Wasserstand **direkt** in den Plan; **Wasserstand = höchste übernommene Id**, steigt bei Übernahme, gehört zum Plan. „Diff, don't react“ (D38) bleibt: Arbeit nur, wenn Zeilen über dem Wasserstand existieren oder eine Plan-Id in CT fehlt; Mapping-Write, Checkpoint und unser eigener Lesezugriff (`-wal`/`-shm`) ergeben nichts
- **Legende** zeigt die Hinweise pro Stop: Größe angenommen (`4 SCU?`), Ziele unvollständig (`⚠ 1 of 3 destinations`), Fallback auf Kindzeilen, `⚠ duplicate?`
- **Jede Erfassung eine eigene Mission, nichts wird ersetzt:** gleiche Ware + überlappende Ziele mit einer Mission ohne Haken → `⚠ duplicate?`, keine Notice; verschwindet beim ersten Haken auf einer der beiden. Retry nach OCR-Fehler → Spieler löscht die falsche Mission im Editor
- **In CT gelöscht:** alle Kisten der Mission abgehakt → Mission raus; sonst Notice, Plan bleibt. Dafür prüft der Reader, welche Plan-Ids in CT noch existieren (Erweiterung hier, T5.3 bleibt wie formuliert)
- Übernommene Missionen behalten ihre **CT-Ids im Plan** (nötig für die Löschnotiz). Plan-Format prüfen; ändert es sich, eigener Eintrag in decisions.md
- Einzige Core-Änderung der Phase, erste seit T3.15: Mission in den bestehenden Plan legen, ohne andere zu verschieben; Positionen und Haken bleiben. Spike erledigt: D41 (inkrementell platzieren), D42 (Missions-Identität, Schema 3); setzt T5.4a voraus. Hier, nicht in T5.4a: abgehakte Kisten geben ihre Zellen frei und tragen nicht mehr; Zurückhaken bei neu belegten Zellen abgelehnt mit Grund (D24-Addendum)
- **Mission entfernen** (in CT gelöscht, im Editor gelöscht) nimmt nur ihre Kisten heraus, andere bewegen sich nicht, Haken bleiben
- Knopf „Sync from ContractTracker“ im Editor neben „Import…“ bleibt als manueller Weg: übernimmt Kandidaten seit dem Wasserstand wie der Watcher. „Sync“, nicht „Load“: „Import…“ daneben ersetzt den Plan, diese Aktion legt inkrementell nach
- Entfällt (D39): Reset-Erkennung, Multimenge, Debounce-Logik auf Inhalt; „verschwindet = erfüllt“ nur in der engen Form oben

**Akzeptanz:** Tests grün (inkrementell ohne Bewegung liegender Kisten, Haken bleiben, Wasserstand steigt bei Übernahme, Dateievent ohne neue Zeilen → nichts; Duplikat-Markierung nur ohne Haken; Entfernen bewegt keine anderen Kisten; Entfernen nach CT-Löschung nur bei vollständig abgehakt); im Spiel: zweiten Auftrag erfassen → ohne Klick im Overlay, Kisten und Haken des ersten unverändert; Capture ohne Größenphrase → `?` in der Legende; F4 vor dem ersten Haken → zweite Mission mit `⚠ duplicate?`, erste unverändert, keine Notice; F4 nach dem ersten Haken → beide im Plan, keine Markierung; falsche Mission im Editor löschen → weg, übrige Kisten und Haken unverändert; in CT löschen bei vollständig abgehakt → Mission weg; bei offenen Kisten → Notice, Plan bleibt.

### [ ] T5.5 Settings-Abschnitt „ContractTracker“
**Warum:** Nicht jede Installation liegt am Standardpfad, nicht jeder will Erfassungen ohne Zutun im Plan, und wer CT nicht hat, soll erfahren, woher.
**Präzisiert in der Planung:** Schalter wieder „Auto-sync“ an/aus (D39-Addendum: Übernahme ohne Pflichtschritt im Editor); der Hotkey übernimmt direkt, statt den Editor zu öffnen; Abschnitt auch ohne CT sichtbar. Präzisierung in der Planphase, ratifiziert durch Freigabe.
**Präzisiert in der Planung (Umsetzung):** Schema bleibt 2 (optionaler Abschnitt `contractTracker` mit `path`/`autoSync`, `hotkeys.syncContractTracker`); Pfad, der kein voller Pfad ist → Autodetektion + Notice, fehlende Datei bleibt stehen. Pfad nur als Anzeige mit „Browse…“ und „Detect automatically“; Status beim Öffnen und bei Änderung geprüft, nicht live. Auto-sync aus ändert den Plan nie (auch nicht nach CT-Löschung), die Notice zählt Erfassungen; wieder an → Sync nach dem Debounce. Fehlende DB → Notice nur bei Knopf/Hotkey, der Watcher bleibt stumm. Hotkey-Doppelprüfung über alle drei Aktionen. **Neue DB** (Neuinstallation, Update legt die Datei neu an, anderer Pfad): höchste je vergebene Id (`sqlite_sequence`) < Wasserstand → ab 0 lesen, Wasserstand zurück, Notice „ContractTracker database looks new – all contracts imported“; nicht `MAX(Id)`, das beim Löschen des neuesten Vertrags sinkt und alle älteren doppelt importieren würde. Präzisierung in der Planphase, ratifiziert durch Freigabe (D44).
- Abschnitt „ContractTracker“ im Settings-Fenster: Pfad (Autodetektion + manuell), „Auto-sync“ an/aus. Aus → Watcher zeigt nur Notice „N new contracts“, Übernahme per Knopf oder Hotkey
- CT nicht gefunden → Abschnitt bleibt sichtbar mit Hinweis „ContractTracker not found – set path or install“ und Link zur Nexus-Seite von ContractTracker (URL bei der Umsetzung prüfen, A3)
- „Sync from ContractTracker“ im Editor (aus T5.4) bleibt der manuelle Weg, auch bei Auto-sync aus
- Optionaler Hotkey „Sync from ContractTracker“ im Hotkeys-Abschnitt, Default `null` (keine Default-Hotkeys, D17): übernimmt direkt wie der Knopf, öffnet nicht den Editor
- **Neue DB:** Ids beginnen wieder bei 1 → ohne Guard nie wieder ein Import, still. Reader erkennt, Core setzt den Wasserstand zurück

**Akzeptanz:** Autodetektion findet Martins DB; manueller Pfad wirkt ohne Neustart und überlebt ihn; Auto-sync aus → neuer CT-Vertrag erzeugt nur die Notice „N new contracts“ und steht nicht im Plan, Knopf oder Hotkey übernimmt ihn; Hotkey gesetzt → übernimmt ohne Editor, ohne Eintrag kein Hotkey; CT fehlt oder Pfad ungültig → Abschnitt sichtbar mit Hinweis und Nexus-Link. Tests grün: Plan mit Wasserstand 57, DB mit Ids 1–3 → drei Kandidaten, Wasserstand 3; neuesten Vertrag in CT löschen → kein Neuimport.

---

## Phase 6 – Release

### [x] T6.0 Erster Start (Spike)
**Warum:** Wer nach dem Download ein leeres Overlay sieht und nicht weiterweiß, schließt es wieder. Vor dem Release prüfen, was ein Hauler ohne README vorfindet.
- Drei Zustände durchspielen: (1) kein Plan, kein CT; (2) kein Plan, CT vorhanden; (3) Plan vorhanden, CT-DB gelöscht
- Screenshots je Zustand in `docs/screenshots/first-start/`
- Ergebnis: Liste „Blocker / Backlog“ (Blocker → Task vor dem Release, Rest → Backlog)
- Kein Code

**Akzeptanz:** Ein Hauler kommt ohne README vom leeren Overlay zu Editor und Settings; Screenshots und Liste liegen vor.

**Befund:** Zustände A/B/C ohne Blocker, Leerzustand und Settings selbsterklärend, Auto-Detect funktioniert, Sync ~1 s. Screenshots in `docs/screenshots/first-start/`.

### [x] T6.1 settings.json sichern
**Warum:** Release-Vorbedingung. Das erste Speichern überschreibt eine kaputte oder von Hand geänderte settings.json (D34), ohne Weg zurück.
- `settings.json` einmal pro Prozesslaufzeit, vor dem ersten Schreiben, als `settings.json.bak` sichern

**Präzisiert in der Planung:** Flag „already backed up“ in `SettingsSession` (einziger Besitzer pro Lauf, D34), nicht statisch; die Kopie macht `SettingsStore.BackUp` (`File.Copy`, overwrite, byte-gleich auch bei Nicht-JSON). Keine settings.json → kein Backup, Flag trotzdem gesetzt. Flag erst nach erfolgreicher Kopie: scheitert sie, wird nicht geschrieben und der nächste Save versucht es erneut. Lade-Hinweis bei kaputter Datei: „The next save overwrites {path}, keeping the old file as settings.json.bak.“; README und settings-format.md nennen die `.bak`. Präzisierung in der Planphase, ratifiziert durch Freigabe (D47).

**Akzeptanz:** settings.json von Hand ändern (auch kaputt), Overlay bewegen → `settings.json.bak` enthält den alten Inhalt byte-gleich; weiteres Speichern im selben Lauf ändert `settings.json.bak` nicht; Core unverändert.

### [x] T6.2 Publish-Profil
**Warum:** Entpacken, Doppelklick, läuft. Sonst nutzt es niemand.
- Ein Profil (D46): `src/CargoPlan.App/Properties/PublishProfiles/win-x64-selfcontained.pubxml`, `SelfContained=true`, `RuntimeIdentifier=win-x64`; kein `PublishSingleFile`, kein `IncludeNativeLibrariesForSelfExtract`, kein zweites Profil
- App und Cli in denselben Publish-Ordner, daraus ein ZIP
- Version sichtbar in der Fußzeile des Settings-Fensters und per `CargoPlan.Cli --version`, beide aus `AssemblyInformationalVersion`
- App-Icon, Assembly-Metadaten (Version, Author, Repo-URL)

**Präzisiert in der Planung:** Statt `win-x64-selfcontained.pubxml` ist `build/publish.ps1` das einzige Profil: ein pubxml gilt pro Projekt, zwei Projekte brauchen ohnehin zwei Aufrufe; Cli zuerst, App zuletzt (zwei gleichnamige Dateien unterscheiden sich, die der App müssen gewinnen), Ordner vorher leeren, `LICENSE` hinein, PDBs bleiben drin. `Directory.Build.props`: `Version` 0.0.0-dev, `Authors`/`Company` MRiediger wie LICENSE, `Product`, `Copyright`, `RepositoryUrl`; kein `PackageLicenseExpression` (wirkt nur bei `dotnet pack`). Version mit vollem Commit-Hash: Fußzeile „SC-CargoPlan 0.0.0-dev+<sha>“ außerhalb des Scrollbereichs, markierbar, Trenner oben, keine zweite Schriftgröße; `--version` gibt nur die Version aus. App-Icon in den Backlog (keine `.ico` im Repo). ZIP-Wurzelordner entscheidet T6.3. Präzisierung in der Planphase, ratifiziert durch Freigabe (D48).

**Akzeptanz:** Ein ZIP; `CargoPlan.App.exe` startet nach dem Entpacken; ZIP < 80 MB; Settings und CLI zeigen dieselbe Version.

**Befund:** Ordner 143,2 MB in 416 Dateien (260 im Wurzelordner, 13 Sprachordner à 12), ZIP (`Compress-Archive`) 62,0 MB. Aus dem entpackten ZIP: Overlay startet, Fußzeile und `CargoPlan.Cli --version` zeigen `0.0.0-dev+2290a0a…`, `plan` und `contracts` (Kopie der CT-DB) laufen. Kollisionscheck (App und Cli getrennt publiziert, SHA256 verglichen): 196 gleichnamige Dateien, 2 unterschiedlich (`WindowsBase.dll`, `SQLitePCLRaw.provider.e_sqlite3.dll`), daher die Reihenfolge Cli → App (D48).

### [x] T6.3 Release-Workflow
**Warum:** Tag setzen → Download steht. Kein manuelles Hochladen.
- Kein MinVer/GitVersion: `Directory.Build.props` setzt `<Version>0.0.0-dev</Version>`; CI setzt `-p:Version` aus dem Tag (ohne „v“) und bricht ab, wenn der Rest kein SemVer ist
- `.github/workflows/release.yml`: Trigger `push` auf `tags: v*`, `build/publish.ps1` aus T6.2 (D48), `softprops/action-gh-release` hängt ein ZIP + SHA256 an
- Release-Notes als Body-Datei, von Hand geschrieben, nicht `generate_release_notes`

**Präzisiert in der Planung:** Erster Schritt prüft den Tag (SemVer 2.0 ohne Build-Metadaten, das SDK hängt `+<sha>` selbst an) und verlangt `docs/release-notes/v<version>.md`, sonst Abbruch vor dem Build. Prerelease-Suffix → `prerelease: true`. `-p:Version` schon beim Build, Test ohne Neubau, dann `publish.ps1 -Version`. `publish.ps1` schreibt `out/publish/SC-CargoPlan/`, das ZIP `SC-CargoPlan-v<version>-win-x64.zip` mit Wurzelordner `SC-CargoPlan/` (Einträge mit `/`, auch unter Windows PowerShell 5.1) und eine `.sha256` im `sha256sum`-Format; ohne `-Version` bleibt 0.0.0-dev. `softprops/action-gh-release` v3.0.3 auf den Commit gepinnt. `SatelliteResourceLanguages` = `en` (D50). Akzeptanz mit `v0.1.0-rc.1` statt `v0.1.0`; `v0.1.0` erst nach T6.5, rc.1 bleibt bis dahin als Prerelease stehen (Prüfbuild für T6.5), dann Release und Tag löschen. Präzisierung in der Planphase, ratifiziert durch Freigabe (D49, D50).

**Akzeptanz:** Tag `v0.1.0-rc.1` pushen → Prerelease mit ZIP + SHA256 und den handgeschriebenen Notes, Version 0.1.0-rc.1 in Settings und CLI; Tag `v0.1` → Workflow bricht ab, kein Release.

**Befund (lokal):** Ordner 134,9 MB in 260 Dateien, keine Unterordner (vorher 143,2 MB / 416), ZIP 58,7 MB (vorher 62,0), ein Wurzelordner, alle Einträge mit `/`, `.sha256` passt zu `Get-FileHash`. `publish.ps1 -Version 0.1.0-rc.1` → `CargoPlan.Cli --version` aus dem entpackten ZIP zeigt `0.1.0-rc.1+9796bfb…`. Tag-Regex: `v0.1.0`, `v0.1.0-rc.1` ✓; `v0.1`, `v01.0.0`, `v0.1.0-rc.01`, `v0.1.0+build`, `v0.1.0-` ✗. Kollisionscheck nach D50 unverändert: 196 gleichnamig, 2 unterschiedlich, Vereinigung 259 + `LICENSE` = 260.

**Befund (CI):** CI auf `6bbc115` grün (ubuntu, windows). Tag `v0.1.0-rc.1` → Release-Lauf grün, [Prerelease](https://github.com/MRiediger/sc-cargoplan/releases/tag/v0.1.0-rc.1) mit ZIP + `.sha256`, Body = Notes-Datei; heruntergeladenes ZIP passt zur `.sha256`, 260 Einträge unter `SC-CargoPlan/`, gleiche Namen und entpackte Größe (134,9 MB) wie lokal; `CargoPlan.Cli --version` zeigt `0.1.0-rc.1+6bbc115…`, die Fußzeile des Settings-Fensters „SC-CargoPlan 0.1.0-rc.1+6bbc115…“ (von Martin geprüft). Das CI-ZIP ist 60,7 MB statt lokal 58,7 MB: gleicher Inhalt, nur anders komprimiert (vermutlich .NET auf dem Runner gegenüber .NET Framework unter Windows PowerShell 5.1, nicht untersucht), also nicht byte-gleich. Tag `v0.1` → Lauf [39652136154](https://github.com/MRiediger/sc-cargoplan/actions/runs/39652136154) scheitert in „Version from tag“ („Tag 'v0.1' is not v<SemVer> …“), Rest übersprungen, kein Release; Tag lokal und remote gelöscht. **Der rote Lauf in der Actions-Historie ist dieser absichtliche Negativtest, kein Fehler.**

### [x] T6.4 Repo-Hygiene für Contributor
**Warum:** Erster externer PR soll nicht an Unklarheit scheitern.
- `CONTRIBUTING.md` (englisch): build, test, Schiff beisteuern, PR-Regeln
- Issue-Templates: Bug, Ship request, Feature; Bug verlangt die Version und die Ausgabe von `contracts --raw`
- `docs/screenshots/` mit Overlay über dem Spiel

**Präzisiert in der Planung:** CONTRIBUTING mit den Befehlen aus ci.yml, Publish nur über `build/publish.ps1` (D48), Schiff → ship-format.md mit `stackAxis` und Quelle. Issue Forms: Bug mit Pflichtfeldern Version (`input`, aus der Settings-Fußzeile), Schritte, erwartet, beobachtet, Screenshot (`upload`), SC-Version, Fenstermodus; `contracts --raw` optional mit „required if missions are involved“ (Issue Forms kennen kein bedingtes Pflichtfeld). Ship fragt zusätzlich Position und Rack-Reihenfolge ab, Label `ship` neu. Feature: Problem zuerst. `config.yml`: keine leeren Issues, Fragen → Discussions. Dazu PR-Template (vier Checkboxen aus den PR-Regeln) und `SECURITY.md` (Private vulnerability reporting), README-Abschnitt „Contributing“ mit den Links. Dependabot je Ökosystem eine Gruppe, monatlich (D51), alte PRs geschlossen, D49-Addendum. Code of Conduct → Backlog. Screenshots: T6.4 schließt mit der Liste, die Bilder macht Martin, Vorbedingung von T6.5. Präzisierung in der Planphase, ratifiziert durch Freigabe (D51).

**Akzeptanz:** Dateien vorhanden, Links aus README funktionieren.

**Befund:** Alle README- und CONTRIBUTING-Links auf vorhandene Dateien und Anker geprüft. Community-Profil 42 % → 85 %; REST `issue_template` bleibt null und GraphQL `issueTemplates` leer, laut Sekundärquelle bei YAML-Issue-Forms immer so (devactivity.com, nicht GitHub-Doku); `config.yml` wird erkannt (Contact-Link da); Template-Chooser von Martin eingeloggt geprüft: drei Formulare, Contact-Link, funktioniert. Dependabot: App.Tests fehlte in allen alten PRs, weil der letzte volle Scan (16.10.) vor App.Tests (18.10.) lag, kein Fehler (Log: `CargoPlan.App.csproj` mit `"Error": null`). Push der neuen Konfiguration startet die Läufe sofort. Dependabot schloss #4, #8–#11 selbst, #6, #7, #13–#16 von Hand. #17 (checkout v7, setup-dotnet v6 in ci.yml **und** release.yml) und #18 (coverlet 10.1.0, Test.Sdk 18.11.0, xunit.runner.visualstudio 4.0.1 in allen drei Test-Projekten) grün auf beiden Runnern mit 564/791/93 wie main, squash-gemergt; lokal 0 Warnungen, 564/791/93. softprops bleibt v3.0.3 (neuestes Release). Private vulnerability reporting, Dependabot alerts und Dependabot security updates an (Martin). `docs/screenshots/first-start/smartscreen.png` (SmartScreen-Dialog der unsignierten EXE, deutsches Windows) ins Repo genommen, für den SmartScreen-Hinweis in T6.5.

**Screenshots für README und Nexus** (Martin, nach `docs/screenshots/`; vorhanden sind nur die T6.0-Bilder ohne Spiel im Hintergrund):
1. `overlay-ingame-elevator.png` – Overlay mit echtem Plan (3–4 Ziele, Legende, ein Ziel abgehakt) über dem Spiel am Frachtaufzug
2. `editor.png` – Editor mit mehreren Zeilen (zwei Ziele, ein handgetippter Kistenmix kursiv)
3. `settings.png` – Settings-Fenster mit Fußzeile (Version)
4. optional `overlay-drag.png` (Drag mit cyan Ghost); Nexus-Titelbild bei T6.6 nach Nexus-Format

PNG, kein Spielername/Org im Bild, Overlay außerhalb des CT-Capture-Bereichs.

### [ ] T6.5 README final
**Warum:** Das README ist die Download-Seite. Was dort steht, muss der Build tun.
- Vorbedingung: Screenshots aus der T6.4-Liste liegen in `docs/screenshots/`
- Abschnitte: Was · Download · Erstes Mal · CT-Anbindung · Hotkeys · Known issues · Bug melden · Credits · Lizenz
- SmartScreen-Warnung bei der unsignierten EXE ehrlich dokumentieren (aus T6.3 hierher)
- Kein Ruckel-Hinweis mehr (in 7e94ac3 entfernt, D45)
- Jede Verhaltensaussage am Release-Build geprüft (das ZIP aus dem Prerelease `v0.1.0-rc.1`)
- Danach Tag `v0.1.0` (Notes `docs/release-notes/v0.1.0.md`), dann Release und Tag `v0.1.0-rc.1` löschen (D49)

**Akzeptanz:** Alle Abschnitte vorhanden; jede Verhaltensaussage am Build nachvollzogen; Links funktionieren.

### [ ] T6.6 Nexus Mods
**Warum:** Viele Hauler finden Tools auf Nexus, dort liegt auch ContractTracker.
- Nach dem GitHub-Tag, gleiches ZIP wie im GitHub-Release
- Permissions aus der MIT-Lizenz abgeleitet
- Credit für ContractTracker

**Akzeptanz:** Nexus-Seite online mit dem ZIP des GitHub-Releases (gleiche SHA256), Permissions passend zu MIT, CT-Credit sichtbar.

---

## Phase 7 – Ausbau (optional, Reihenfolge frei)

### [ ] T7.1 Weitere Schiffe – `cutlass_black.json`, `raft.json`, Schiffsauswahl im Editor
### [ ] T7.2 Auto-Update – **Velopack** evaluieren (ersetzt T6.2-Publish durch Installer mit Delta-Updates)
### [ ] T7.3 winget-Manifest – `winget install SC-CargoPlan`
### [ ] T7.4 Eigenes OCR – **Windows.Media.Ocr** (in Windows eingebaut, kein Paket, keine 100 MB Modelle). Machbarkeitsstudie als eigener Task.
### [ ] T7.5 Lokalisierung – `.resx`, Deutsch als zweite Sprache

---

## Backlog (nur bei Bedarf)

Kein Plan, sondern Reaktion: wird erst zum Task, wenn der Anlass eintritt.

- `CODE_OF_CONDUCT.md`, sobald es Contributors gibt; Community Standards mahnt es an, ein Solo-Repo braucht es vor dem ersten Hauler nicht (T6.4).
- App-Icon (`.ico` mit 16/32/48/256 px, `<ApplicationIcon>` im App-csproj), sobald ein Icon vorliegt; aus T6.2 (D48).
- Topmost-Fallback (`SetWindowPos(HWND_TOPMOST)` per Timer) nur bei Issue-Report mit Repro. Im T3.1-Test blieb das Overlay über Borderless-SC (D13).
- ~~Rack `orientation` (physische Stapelrichtung, Rotation pro Rack), sobald ein Schiff es braucht (D14).~~ Stapelrichtung erledigt in T3.13c (`stackAxis`, D26); freie Rotation pro Rack erst, wenn ein Schiff sie braucht.
- Viewport3D-Renderer auf denselben Geometriedaten, falls 2.5D nicht reicht (D14).
- Ship mesh outline (Schiffsumriss): erst nach Klärung von Asset-Pipeline, Rechtslage bei Spieldaten und Render-Kosten im Layered Window (D14).
- Kamera, Zoom und Pan der Schiffsansicht in `settings.json`, falls das Zurücksetzen beim Neustart nervt (D15).
- Orbit-Snap auf feste Winkel (z. B. 45°-Raster per Taste), falls freies Drehen schwer wiederholbar ist (D15).
- Plan verwerfen ohne neuen Plan („Discard plan“), falls ein leeres Overlay nach der Route gewünscht wird; heute ersetzt nur „Plan“ den alten Plan (D19).
- Editor-Position in `settings.json`, Entwurfs-Autosave, Undo im Editor, falls der Zwei-Minuten-Durchlauf daran scheitert (D19).
- Rückfall auf Segmente ab einer Zellenzahl, sobald ein großes Schiff (Hull C, ~4.600 Zellen) beim Orbit ruckelt (D21).
- Kistengrößen als Blöcke und Zellbeschriftung in der Schiffsansicht (D21).
- Backdrop-Blur hinter dem Fenster (Spike: DWM vs. AllowsTransparency) (D28).
- Alternative Stop-Paletten, nur geprüft farbfehlsicht-tauglich (D29).
- Theme-Presets/Export (A4).
- Füllrichtung in Mission-Dateien (Export/Import), falls Koordinatoren sie mit der Route teilen wollen; heute nur über plan.json (Core-Formatwechsel, D24-Addendum).
- Formbewusste `Partition` (Racks nach legbarer Anordnung in Routenreihenfolge, nicht nur nach SCU), falls Stapel-Befunde in echten Plänen häufig sind. Beleg aus dem Spike T5.4 (Brute-Force, abgebrochen): 3 Stops in einem Rack → 207 Aufteilungen ohne Anordnung bis Mischung 146/186; 4 Stops → 397 bis Mischung 65/170 (D24-Addendum, T5.4a).
- Hit-Test der Schiffsansicht auf ihre Fläche begrenzen: gezeichnet wird mit `ClipToBounds`, getroffen nicht, also kann ein Drop über der Legende ein beim Hineinzoomen abgeschnittenes, unsichtbares Rack treffen (D33).
