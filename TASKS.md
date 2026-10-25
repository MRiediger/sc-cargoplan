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

**Befund:** Alle README- und CONTRIBUTING-Links auf vorhandene Dateien und Anker geprüft. Community-Profil 42 % → 85 %; REST `issue_template` bleibt null und GraphQL `issueTemplates` leer, laut Sekundärquelle bei YAML-Issue-Forms immer so (devactivity.com, nicht GitHub-Doku); `config.yml` wird erkannt (Contact-Link da); Template-Chooser von Martin eingeloggt geprüft: drei Formulare, Contact-Link, funktioniert. Dependabot: App.Tests fehlte in allen alten PRs, weil der letzte volle Scan (16.10.) vor App.Tests (18.10.) lag, kein Fehler (Log: `CargoPlan.App.csproj` mit `"Error": null`). Push der neuen Konfiguration startet die Läufe sofort. Dependabot schloss #4, #8–#11 selbst, #6, #7, #13–#16 von Hand. #17 (checkout v7, setup-dotnet v6 in ci.yml **und** release.yml) und #18 (coverlet 10.1.0, Test.Sdk 18.11.0, xunit.runner.visualstudio 4.0.1 in allen drei Test-Projekten) grün auf beiden Runnern mit 564/791/93 wie main, squash-gemergt; lokal 0 Warnungen, 564/791/93. softprops bleibt v3.0.3 (neuestes Release). Private vulnerability reporting, Dependabot alerts und Dependabot security updates an (Martin). `docs/screenshots/first-start/smartscreen.png` (SmartScreen-Dialog der unsignierten EXE, englisch) ins Repo genommen, für den SmartScreen-Hinweis in T6.5.

**Screenshots für README und Nexus** (Martin, nach `docs/screenshots/`; vorhanden sind nur die T6.0-Bilder ohne Spiel im Hintergrund):
1. `overlay-ingame-elevator.png` – Overlay mit echtem Plan (3–4 Ziele, Legende, ein Ziel abgehakt) über dem Spiel am Frachtaufzug
2. `editor.png` – Editor mit mehreren Zeilen (zwei Ziele, ein handgetippter Kistenmix kursiv)
3. `settings.png` – Settings-Fenster mit Fußzeile (Version)
4. optional `overlay-drag.png` (Drag mit cyan Ghost); Nexus-Titelbild bei T6.6 nach Nexus-Format

PNG, kein Spielername/Org im Bild, Overlay außerhalb des CT-Capture-Bereichs.

### [x] T6.4b Bow marker in the ship view
**Warum:** Release-Blocker, Präzisierung in der T6.5-Planung: ohne Markierung ist Rack 1 nicht von Rack 4 zu unterscheiden, sobald der Spieler die Ansicht gedreht hat.
- Form, keine Beschriftung: Keil am Bug, Bug bleibt links in der Default-Kamera (D15)
- Keine Schiffsformat-Änderung, keine Settings-Option, Core unverändert
- Test: Keil am Bug-Ende in den vier Default-Yaw-Quadranten; Hit-Test und Drop-Target unverändert
- D-Eintrag mit Gegenargument (stumpfe Schiffe bekommen eine Nase)

**Präzisiert in der Planung:** Befund: `ShipScene` kennt keinen Rumpf, `Ship.Bounds` ist die Vereinigung der Rack-Boxen, gezeichnet als gestrichelter Rahmen aus Linien außerhalb des Painters (D21); Hit-Test und Drop-Target sehen ihn nicht. Die Racks reichen bis zur Bugkante, eine Verjüngung auf den letzten 10 % schnitte durch sie (und wären beim Hull A 0,4 Zellen). Daher Nase **vor** der Bugkante, Länge max(1 Zelle, 10 % der Länge), Spitze halbe Breite, volle Höhe, Linien wie der Rahmen (gestrichelt, `MissionPalette.Frame`), nicht gefüllt, kein Painter-Eingriff. Folge für T6.5: `src` ändert sich nach rc.1, geprüft wird am Prerelease `v0.1.0-rc.2`. Präzisierung in der Planphase, ratifiziert durch Freigabe (D52).

**Akzeptanz:** Keil am Bug in allen vier Quadranten (Test); Hit-Test unverändert (bestehende Tests grün, Klick auf die Nase trifft nichts); Martin hat die Sichtbarkeit im Spiel beurteilt.

**Befund (Linien):** `SceneProjector.BowWedge` als 8 Kanten, `Frame` 20 statt 12 Linien. In der Default-Kamera bleibt die Hull-A-Nase im alten Extent (v max 3,375 < 3,5; u min −4,98 > −5,20), Default-Zoom und Nummerngröße unverändert (`RackNumberSizeTests` grün ohne Änderung). Im Spiel kaum erkennbar (Martin): Stil des Rahmens, keine Fläche.

**Nachbesserung, präzisiert:** Nase als 5 gefüllte Flächen (Spitze, oben, unten, zwei Flanken), Füllung `MissionPalette.FreeEdge` (Alpha 0x50; freie Zellen selbst haben keine Füllung, daher deren Kantenfarbe als Fläche), Kanten `RackPen` durchgezogen; sichtbare Flächen über die Normalen. Painter-Befund: die x-Achse trennt Nase und Racks immer, der Yaw-Quadrant entscheidet (Bugseite → nach den Kisten, vor Rot/Cyan; sonst vor den Rack-Kanten); kein `DrawOrder`-Eintrag. Länge bleibt max(1 Zelle, 10 %). Präzisierung in der Planphase, ratifiziert durch Freigabe (D52-Addendum).

**Befund (Flächen):** `BowWedge` liefert `BowWedgeFace` (Fläche, Ecken, Normale nach außen), `ProjectedScene.BowWedge` die sichtbaren Flächen und `InFront`; `Frame` wieder 12 Linien, Extent umschließt alle Keil-Ecken. Tests: 5 Flächen mit Spitzenmaßen und Normalen nach außen (Hull A, 20-Zellen-Box); in vier Yaw-Quadranten Spitze bugwärts, `InFront` und sichtbare Spitze genau von der Bugseite; Klick auf die Nase trifft nichts. Tests 564/798/93 (+7 App gegenüber main), 0 Warnungen. Im Spiel (Martin): Fläche verdeckt die Container.

**Nachbesserung 2, präzisiert:** Füllung eigene Farbe `MissionPalette.BowWedgeFill`, Alpha 0x18. Schriftzug „Bow“ (UI-Text Englisch, Martin schrieb „Bug“) flach auf dem Nasenboden wie in Martins Skizze, Zeile quer zum Schiff, Buchstabenoberkanten achtern; mit der Nase gezeichnet. Die Rack-Nummer-Einstellungen gelten auch für ihn: Farbe und Umriss direkt, Schriftgröße relativ zum Default 27, gedeckelt (nie größer als der Nasenboden); Settings-Fenster „Rack numbers and bow label“, Umriss-Label „Outline around the rack numbers and the bow label“. Präzisierung in der Planphase, ratifiziert durch Freigabe (D52-Addendum 2).

**Befund (Schriftzug):** `FloorLabel.Lay` legt die Glyphen per affiner Matrix auf den projizierten Boden (`WedgeFloor`: Mitte, eine Zelle quer und bugwärts, Platz 70 % von Nasenlänge und Spitzenbreite), zentriert die Tinte (nicht den Zeilenkasten) und flacht in Bildschirmpixel ab: keine Transformation bleibt, der Umriss wird in Pixeln gezogen (für WPF nicht dokumentiert, ob `Geometry.Transform` den Stift mitskaliert; so egal). Tests: in vier Quadranten jeder Punkt des Schriftzugs im projizierten Nasenboden; Anteil 0,5 halbiert Breite und Höhe um die Bodenmitte, Anteil 2 = Anteil 1. Im Spiel: passt (Martin). Tests 564/803/93, 0 Warnungen. Ein erster Testlauf lief gegen den alten Build, weil das offene Overlay die EXE sperrte; nach dem Beenden neu gebaut. Danach im Spiel (Martin): Label größer als die Racknummern.

**Nachbesserung 3, präzisiert:** Größe nicht mehr als Anteil vom Default 27, sondern auf dem Bildschirm wie die Racknummern: entlang der Glyph-Hochachse (Schiffs-x auf dem Boden) ist ein Em die aktuelle `RackNumberSize.For` (D32 inkl. Schrumpfen), umgerechnet über die projizierte Länge einer Zelle in x bei Kamera und Zoom; Obergrenze bleibt der Platz auf dem Boden. „Gleich hoch“ heißt: Großbuchstabe B so hoch wie die Ziffer 1 bei gleicher Schrift und Größe (Rückfrage; wörtlich „B = RackNumberSize px“ wäre ~1,4× die Ziffern). Präzisierung in der Planphase, ratifiziert durch Freigabe (D52-Addendum 3).

**Befund (Größe):** `FloorLabel.Placement` liefert die Matrix (Lay flacht damit ab), `numberSize` einmal pro Frame für Nummern und Label. Tests: Yaw 45, Default-Zoom und 2×: B auf dem Bildschirm = Ziffer 1 bei `RackNumberSize` ±1 px (die Obergrenze greift dort also nicht); Boden mit 0,1 Zellen Platz quer → Label genau 0,1 Zellen breit. Anteil-Test entfällt. Tests 564/805/93, 0 Warnungen.

### [ ] T6.5 README final
**Warum:** Das README ist die Download-Seite. Was dort steht, muss der Build tun.
- Vorbedingung: Screenshots aus der T6.4-Liste liegen in `docs/screenshots/`
- Abschnitte: Was · Download · Erstes Mal · CT-Anbindung · Hotkeys · Known issues · Bug melden · Credits · Lizenz
- SmartScreen-Warnung bei der unsignierten EXE ehrlich dokumentieren (aus T6.3 hierher)
- Kein Ruckel-Hinweis mehr (in 7e94ac3 entfernt, D45)
- Jede Verhaltensaussage am Release-Build geprüft (das ZIP aus dem Prerelease `v0.1.0-rc.3`, nach T6.4b und dem Editor-Absturz-Fix `7f04dd7`)
- Danach Tag `v0.1.0` (Notes `docs/release-notes/v0.1.0.md`), dann die noch vorhandenen rc-Releases und -Tags löschen (D49). rc.3 ersetzt rc.2, das den Editor-Absturz noch enthält. rc.2 ersetzt rc.1: Prerelease aus `1bfd3ba` grün, Download passt zur `.sha256`, 260 Dateien unter `SC-CargoPlan/`, `CargoPlan.Cli --version` = `0.1.0-rc.2+1bfd3ba…`; Release und Tag `v0.1.0-rc.1` danach gelöscht (remote und lokal)

**Akzeptanz:** Alle Abschnitte vorhanden; jede Verhaltensaussage am Build nachvollzogen; Links funktionieren.

**Präzisiert in der Planung:** README in fester Hauler-Reihenfolge (Was · Download & Installation · Erstes Mal · ContractTracker · Am Aufzug · Settings · Known issues · Bug melden · Credits · Contributing/Lizenz), kurz; die Referenztiefe in die neue `docs/user-guide.md` mit gleicher Gliederung, README verlinkt pro Abschnitt. Hotkeys kein eigener README-Abschnitt (kurz in „Erstes Mal“, voll im Guide); Build-Abschnitt entfällt (Duplikat von CONTRIBUTING.md); kein Erec-Credit (Hull-A-Daten im Spiel ermittelt). Falsche Aussagen gestrichen, nicht umgeschrieben: „single Windows EXE“, „Screenshot: coming soon“, „Red edges … only a plan file edited by hand“ (seit T5.4a erzeugen Planer und Drag rote Stapelbefunde), „put crates on top of [delivered crates]“ (abgehakte Kisten sind Luft, `DropTarget.Resolve`), Reset nur durch Löschen von settings.json. Prüfung: CLI/Dateien/DB-Kopie durch CC am rc.3-ZIP, GUI-Aussagen am Code von `01770ab` (= rc.3) und per Checkliste durch Martin. Ohne Bilder (Martin): alle Bilder als Platzhalter-Kommentare `<!-- image: … -->`, auch die first-start-Bilder (zeigen das Overlay noch ohne Bug-Keil); die drei neuen PNGs bleiben uncommittet. Tag v0.1.0 erst nach dem Backlog (Martin). Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Befund (Prüfung je Abschnitt, Build rc.3 = `01770ab`):**
- **Was:** Problem und Ansatz aus CLAUDE.md, „separates Fenster, nichts im Spielprozess“ aus D13/D45 (keine Hooks). Hero-Bild Platzhalter.
- **Download & Installation:** rc.3-Release per `gh release download`: ZIP passt zur `.sha256`, 260 Dateien alle unter `SC-CargoPlan/`, `CargoPlan.Cli.exe --version` = `0.1.0-rc.3+01770ab…`. Der Einzeiler `$zip = Get-Item .\SC-CargoPlan-*.zip; (Get-FileHash $zip).Hash -eq (Get-Content "$zip.sha256").Split(' ')[0]` liefert unter Windows PowerShell 5.1.26100 `True`. SmartScreen-Text aus `smartscreen.png` („Windows protected your PC“, „More info“); „Run anyway“ und „einmal pro Download“ → Checkliste Martin. Self-contained (D46). Borderless aus D13.
- **Erstes Mal:** Leerzustand-Text „No plan. Open the editor to enter missions.“ und „Open editor“ im Code; keine Hotkey-Defaults (`hotkeys` null, D17; rc.2-Settings-Screenshot zeigt drei Mal „Not set“); Tastenregel aus `HotkeyParser` (A–Z, 0–9, F1–F24 ohne F12, Space, Pause, ScrollLock, Insert, Delete, Home, End, PageUp, PageDown); Tooltip am gesperrten Plan (`51d77c6`, Test) → Checkliste Martin.
- **ContractTracker:** Markierungstexte `4 SCU?`, `⚠ 1 of 3 destinations`, `⚠ duplicate?`, `⚠ doesn't fit (16 SCU, 8 free) – tick to drop`, Notice „ContractTracker database looks new – all contracts imported“ wörtlich im Code; Auto-Detect-Pfad und „N new contracts“ aus D44; Nexus-URL aus `ContractTrackerSectionViewModel.NexusUrl` (Nexus antwortet Skripten mit 403, im Browser prüfen).
- **Am Aufzug:** Abhaken, Drag, Split/Merge unverändert aus der README (in T3/T4/T5 im Spiel abgenommen); Stapelregel weich am Code: `CrateLayout` meldet „… in rack N lies on cargo for …, which is unloaded first“, die obere Kiste bekommt den roten Umriss (`CrateLayout.Faulty`), das Rack rote Kanten, die Zeile steht rot unter der Legende (`Scene.Violations`); kein Abbruch (D24-Addendum T5.4a). Abhaken außer Reihe: „has nothing below it“. → Checkliste Martin.
- **Settings:** Abschnitte und „Reset to defaults“ aus `SettingsWindow.xaml` (Window, Legend, Legend layout, Crates, Header, Buttons mit Reset; Tool windows, Hotkeys, ContractTracker ohne); Default Tool windows = `System`; `settings.json.bak` (D47); Fußzeile mit Version (T6.2).
- **Known issues:** aus dem Backlog, was Hauler merken: nur Hull A, unsigniert, kein Auto-Update (T7.2), Exclusive Fullscreen, Kamera nicht gespeichert (D15), Stapelbefunde bei ≥ 3 Stops (formbewusste Partition). Weggelassen: App-Icon, Editor-Undo, Hit-Test über der Legende (D33).
- **Bug melden:** Bug-Formular-URL `issues/new?template=bug.yml` leitet auf das GitHub-Login um (Konto nötig, im README genannt). `Copy-Item … .\contracts-copy.db` und `.\CargoPlan.Cli.exe contracts .\contracts-copy.db --raw` wörtlich im entpackten rc.3-Ordner ausgeführt: Exit 0, Vertrag und Rohtext. Kopie in den Programmordner statt auf den Desktop (der kann per OneDrive umgeleitet sein).
- **Credits:** Okabe-Ito-Quelle `https://jfly.uni-koeln.de/color/` liefert 200, Titel „Color Universal Design (CUD) / Colorblind Barrier Free“; acht Stop-Farben in `MissionPalette.Colors`.
- **Links:** Skript über README, user-guide.md, settings-format.md, CONTRIBUTING.md: alle relativen Dateien und Anker vorhanden (Negativtest mit falschem Anker und fehlender Datei schlägt an); `settings-format.md` zeigt jetzt auf `user-guide.md#hotkeys`. `releases/latest` leitet bis v0.1.0 auf `/releases` um.
- Tests 564/809/93, 0 Warnungen (XAML nur Kommentar).

**Offen:** Checkliste Martin am rc.3-ZIP (Run anyway und zweiter Start ohne Dialog; Plan-Tooltip; roter Stapelbefund mit Zeile unter der Legende; Drag einer Kiste auf die Stelle einer abgehakten); Bilder einsetzen; Notes `v0.1.0.md`, Tag `v0.1.0`, dann rc.3 löschen – nach dem Backlog.

### [ ] T6.6 Nexus Mods
**Warum:** Viele Hauler finden Tools auf Nexus, dort liegt auch ContractTracker.
- Nach dem GitHub-Tag, gleiches ZIP wie im GitHub-Release
- Permissions aus der MIT-Lizenz abgeleitet
- Credit für ContractTracker

**Akzeptanz:** Nexus-Seite online mit dem ZIP des GitHub-Releases (gleiche SHA256), Permissions passend zu MIT, CT-Credit sichtbar.

---

## Phase 7 – Schiffe vor Nexus (D53)

### [x] S7.0 Planer-Last und Geometrie-Korrektheit (Spike)
**Warum:** Vor weiteren Schiffen (T7.1) wissen, ob der Planer auf großen bzw. ungleichen Gittern trägt. Kein Code, keine Spike-Dateien im Repo.
- Synthetische Schiffe, eingebettet, nach dem Spike gelöscht: `spike_large` = 5 Racks à 8×4×4 (640 Zellen, je Rack 4 × 32er); `spike_uneven` = 2 Racks à 5×5×3 (jede Achse +1 gegenüber der 32er 4×4×2, geometrisch 1 × 32er pro Rack, rechnerisch 2)
- Messung: `CargoPlan.Cli plan <missions> --ship <id>` (Release-Build), `Stopwatch` um den Prozess, je 3 Läufe, Median; Baseline Hull A mit `example_missions.json` für den Prozessstart
- Lauf 3: 2 Ziele à 64 SCU (je 2 × 32er), damit jedes Ziel nach SCU genau in ein Rack passt und nur die Geometrie scheitern kann

**Befund:**

| Lauf | Schiff | Eingabe | Median (3×) | Exit-Code | Ausgang | Platziert / Eingabe |
|---|---|---|---|---|---|---|
| Baseline | hull_a | example_missions (3 Ziele, 30 SCU) | 100 ms | 0 | ok | 30 / 30 SCU |
| 1 | spike_large | 20 × 32 SCU auf 4 Ziele (Schiff 100 % voll) | 110 ms | 0 | ok, jedes Ziel über 2 Racks, Racks 2–4 geteilt (Warnungen korrekt) | 640 / 640 SCU |
| 2 | spike_large | 60 × 8 SCU auf 4 Ziele | 106 ms | 0 | ok, ein Rack pro Ziel, Rack 5 leer | 480 / 480 SCU |
| 3 | spike_uneven | 4 × 32 SCU auf 2 Ziele (128 / 150 SCU) | 92 ms | 1 | `PlanException`: „Crates for rack 1 don't fit its grid (5×5×3): 64/75 SCU.“ | 0 / 128 SCU, sauber abgelehnt |
| 4 | spike_uneven | Ziel 1: 2 × 32 SCU, Ziel 2: 40 × 1 SCU (104 / 150 SCU) | 91 ms | 1 | `PlanException`: „Crates for rack 1 don't fit its grid (5×5×3): 64/75 SCU.“ – lösbar mit je einem 32er pro Rack, 1er in den Rest | 0 / 104 SCU, zu Unrecht abgelehnt |
| 5 | spike_large | alle sieben Größen, 615 SCU (4 Ziele, 1–32 SCU) | 116 ms | 0 | ok, Ziele 1–3 je über 2 Racks, Racks 2–3 geteilt (Warnungen korrekt) | 615 / 615 SCU |

- Planerzeit netto ≈ 10–17 ms über der Hull-A-Baseline, Prozessstart ≈ 90–100 ms; alle 3 Läufe je Fall bit-gleich in der Ausgabe. **Bewertung: ≤ 1 s → ok.** Laufzeit ok, Korrektheit nicht: Lauf 4 lehnt eine lösbare Beladung ab (Partition nach SCU, beide 32er in Rack 1) → T7.0 vor T7.1.
- UI-Thread (nur nachgesehen): Editor-„Plan“ ruft `LoadPlanner.Plan` synchron im UI-Thread (`MissionEditorViewModel.Plan`); beim CT-Sync läuft nur das Lesen in `Task.Run`, `ApplyContractTracker` (→ `PlaceInto`) danach im UI-Thread. Bei ~10 ms unkritisch.
- Grenze des Spikes: nur 4 Ziele ≤ Racks. Mehr Ziele als Racks (geteilte Racks, `Partition`) wurden nicht gemessen. → Lastfall in T7.0
- Lauf 3 wie erwartet: D9 gilt nur für Hull A; der Planer verteilt nach SCU, die Geometrie prüft erst `CrateLayout.LayOut`. → T7.0.

### [x] T7.0 Formbewusste Partition – lösbare Beladungen nicht ablehnen (Auslöser: S7.0 Lauf 4)
Aus dem Backlog übernommen:
- Formbewusste `Partition` (Racks nach legbarer Anordnung in Routenreihenfolge, nicht nur nach SCU), falls Stapel-Befunde in echten Plänen häufig sind. Beleg aus dem Spike T5.4 (Brute-Force, abgebrochen): 3 Stops in einem Rack → 207 Aufteilungen ohne Anordnung bis Mischung 146/186; 4 Stops → 397 bis Mischung 65/170 (D24-Addendum, T5.4a).
- Geometrie bei Schiffen, deren Grid kein Vielfaches der Kistenmaße ist (D9 gilt nur für Hull A), sobald T7.1 ein solches Schiff bringt: `Plan` verteilt nach SCU und scheitert erst beim Legen, die Meldung („64/75 SCU“) klingt nach Platz und nennt nur Rack 1. `PlaceProblem` sagt es schon besser („no room for 8 SCU crates“). Beleg: S7.0, Lauf 3.

**Warnung:** Der Brute-Force-Ansatz wurde in T5.4 aus Kombinatorik-Gründen abgebrochen (siehe Beleg in der ersten Zeile).

**Akzeptanz:**
- Lauf 4 ergibt 104 / 104 SCU, Lauf 3 wirft weiter mit Exit-Code 1
- Hull-A-Pläne bleiben byte-gleich, bestehende Planer-Tests unverändert grün
- neuer Lastfall Hull A, 8 Ziele gemischt (mehrere Stops pro Rack), gemessen vor und nach dem Umbau, ≤ 1 s
- die neue Partition gilt auch in `PlaceInto` (CT-Sync)
- die Meldung bei wirklich unlösbaren Fällen ist in Hauler-Sprache (Richtung `PlaceProblem`)
- Testschiff `spike_uneven` als Fixture unter `tests/`, nicht unter `Ships/`

**Präzisiert in der Planung (D54):** SCU-Partition zuerst, Fallback nur, wenn das Legen scheitert, nicht wenn schon die SCU-Partition scheitert (D6, Hull-A-Test „Crates don't fit*Orison*“). Fallback = First-Fit-Decreasing mit der Rackwahl von `PlaceInto` (D41), ohne Suche; Probe und Legen teilen `CrateLayout.RackSpace`. Fallback-Plan: Notiz „Racks not in route order – crates placed where they fit“ in jedem Rack mit Fracht, von `WithHints` mitgetragen, nicht beim `PlaceInto`-Fallback. Meldung: „{Ziel} doesn't fit: no room for {n} × {Größe} SCU crate(s) of {Ware} – {frei} SCU free, but no gap of that shape. Leave a stop out or take a ship with bigger racks.“ Hull-A-Lastfall mit 1–16 SCU, ohne 24/32 (passen in kein Hull-A-Rack). Stapelbewusste Partition bleibt Backlog. Nachtrag B1 (Befund Schritt 0): die exakte Stapelsuche hing auf 1.008 Zellen → Budget 30.000 Schritte, Notiz „Rack N: too many crates to rearrange – red crates lie on cargo that unloads earlier. Check the unload order by hand.“ (ersetzt nach der Umsetzung „stacking order not checked“: geprüft wird die Reihenfolge immer, nur die Umordnung entfällt). Commits: Golden → `RackSpace` → Budget → Fallback. Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Befund** (Release-CLI, je 3 Läufe, Median, gemessen an `0b7d62b`, Endstand `854d360` liegt wenige ms daneben; Spike-Schiffe aus S7.0 nachgebaut, auf dem alten Core bit-gleich zu S7.0 geprüft, nach der Messung gelöscht):

| Lauf | Schiff | Vorher | Nachher | Ausgang nachher |
|---|---|---|---|---|
| Hull-A-Lastfall (8 Ziele, 58 SCU) | hull_a | 106 ms, 58/58 | 107 ms, 58/58 | bit-gleich zu vorher |
| 1, 2, 5 | spike_large | 119 / 109 / 119 ms | 120 / 109 / 123 ms | bit-gleich zu S7.0 |
| 3 | spike_uneven | 90 ms, Exit 1 („64/75 SCU“) | 96 ms, Exit 1 | „Dest2 doesn't fit: no room for 2 × 32 SCU crates of Titanium – 22 SCU free, …“ |
| 4 | spike_uneven | 90 ms, Exit 1, 0/104 | 159 ms, Exit 0, **104/104** | Fallback-Notiz in beiden Racks |
| huge (1 Rack 12×12×7, 4 Ziele, alle 7 Größen, 958 SCU) | spike_huge | > 15 min (> 50 Mio. Schritte), abgebrochen | 877 ms, Exit 0, 958/958 | Notiz „too many crates to rearrange“ |

- Hull A braucht höchstens 2.859 Suchschritte (Exhaustive), ein Schritt kostet auf 1.008 Zellen ≈ 22 µs → Budget 30.000 (≈ 10 ×), Herleitung in D54 und am Code.
- Nachtrag T7.1c: Die Zusammensetzung der huge-Ladung wurde nicht festgehalten, `spike_huge` war gelöscht; die 877 ms sind nicht reproduzierbar. Nachbau mit denselben Eckdaten (Zusammensetzung im T7.1c-Befund): 577 ms mit der alten 32er, 636 ms mit 8×2×2.
- `PlaceInto` auf der Hull A platziert jetzt Missionen, die stopweise `Unplaced` blieben (12 + 4 SCU frei, Stops 4 + 4 und 8) – gewollt, D54.

### [x] T7.1a Schiffsordner statt eingebettet
Infrastruktur mit hull_a allein. D-Eintrag: Ordner `ships/` neben der Exe statt `EmbeddedResource`
(Hybrid abgelehnt: zwei Quellen, bei Bug-Reports unklar, welche aktiv war). Ablage im Repo-Root `ships/`,
Core kopiert ins Output. Laden: Ordner fehlt → Meldung und Ende, kein Crash; eine Datei kaputt → nur dieses
Schiff fehlt, Grund gesammelt (CLI gibt ihn aus, App zeigt ihn in T7.2); hull_a fehlt → wie Ordner fehlt.
Formatzusätze, optional, kein Schema-Bump: pro Rack `maxCrateSize` (Spiel-Limit, Fleetyards `maxContainerSize`,
Carrack 4×4×4 Zellen erlaubt 16, nicht 32; `CanHold` und `EnsureCratesFitShip` prüfen es), pro Schiff
`manufacturer` (Code wie „MISC“, für die Gruppierung in T7.2), `source`, `notes`. hull_a: `source: "in game"`.
publish.ps1 kopiert `ships/` ins ZIP, Prüfschritt + Negativtest wie D49. Tests: Ordner-Fälle, Carrack-Fall,
„alle Dateien in ships/ laden“. Lizenz: nur abgeleitete Dateien ins Repo.

**Präzisiert in der Planung (D56):** `ShipLoader.LoadAll` → `ShipCatalog` (Schiffe nach Id sortiert, `StringComparer.Ordinal`, plus eine Problemzeile je kaputter Datei); Ordner fehlt bzw. hull_a fehlt/kaputt → `ShipFolderException`, App: eine MessageBox, Exit-Code 1; CLI: „Skipped: …“ auf stderr, Exit 1. `Load(id)` bleibt case-sensitive (Lookup in der Dateiliste, nicht `File.Exists`). `CopyToOutputDirectory="IfDifferent"` statt `PreserveNewest` (von Git wiederhergestellte Dateien behalten die alte mtime). publish.ps1 kopiert nicht selbst: vor dem Umbau gemessen, `dotnet publish` legt Cores `ships/`-Items schon ab; das Skript prüft nur den ZIP-Eintrag. Zusätzlich `fleetyardsSlug` pro Schiff (T7.4), hull_a: `manufacturer` „MISC“, `fleetyardsSlug` „misc-hull-a“, kein `maxCrateSize` (Begründung in ship-format.md). Die Metadaten stehen schon im `Ship`-Modell, ohne Leser, ohne Ausgabeänderung. `maxCrateSize` prüfen `Rack.CanHold` (neu: `FitsGrid` für die Geometrie), `RackSpace.Add` (Plan, PlaceInto, Split, T7.0-Probe), die Findings (Moves) und als dritte Stelle der Merge (zwei 16er nebeneinander haben die Form einer 32er). Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Befund:**
- Aufrufer außerhalb der Tests vorher: 3 Stellen in 2 Dateien (App `Load`, CLI `ListShips` + `Load`).
- CLI `plan example_missions.json`: byte-gleich zum Stand vor T7.1a (Worktree von `2d30fab` gebaut). Kaputte `x.json` neben hull_a → „Skipped: Ship file 'x.json' is invalid: …“, Plan läuft, Exit 0; `--ship x` → zusätzlich „Ship 'x' not found. Available ships: hull_a.“, Exit 1. Ordner umbenannt → „Ships folder missing next to the exe – unzip the release again. Expected: …“, Exit 1 (auch für die Usage); `--version` braucht den Ordner nicht.
- App ohne `ships/`: ein Fenster „SC-CargoPlan“ mit derselben Meldung und OK, Exit-Code 1, kein Prozess bleibt. Mit Ordner: normaler Start, Schließen mit Exit 0.
- publish.ps1: 261 Dateien (vorher 260, + `ships/hull_a.json`), ZIP 58,7 MB. Negativtest: `ships/hull_a.json` umbenannt → Skript rot („… has no SC-CargoPlan/ships/hull_a.json …“), kein `.sha256`.
- Tests: Core 626 (608 nach dem Ordner-Commit, +18 für Formatzusätze und Carrack-Fall), App 809, Import 93, alle grün; `ships/` kommt auch transitiv in App.Tests und Import.Tests an.

### [x] T7.1b Fleetyards-Import
`tools/import-fleetyards.ps1`: `ships.json` (Export aus `sc-ship-outlines/fetch.ps1`, bleibt außerhalb des Repos)
→ Dateien in `ships/`. Scope: jedes Schiff mit Frachtgitter (erwartet 96). Abgleichmodus meldet Abweichungen
bei bestehenden Dateien, überschreibt nie Origin/stackAxis/Reihenfolge/Notizen. Erste Zahl: Hull A aus
Fleetyards gegen `hull_a.json`. Gegenprobe gegen Erecs Guide. Details im Planprompt.

**Präzisiert in der Planung (D53-, D56-Addendum):** Gegenprobe Guide entfällt (kein Guide-JSON, nur PDF/`guide.txt`). Fleetyards-Achsen: x quer (Vorzeichen: nach Steuerbord, Hull B/Starlancer stützen es, Pisces widerspricht), y längs, z hoch; Offset = Min-Ecke (Mittelpunkt-Lesart gemessen schlechter). Gitter: `length` = längere horizontale Seite, gedrehte Racks in `notes` (Backlog „Kisten drehbar“). Offsets nur, wenn alle Racks einen haben, keine überlappen und keins gedreht ist (5 Schiffe), mit `position` aus der Lage und Notiz „port/starboard from Fleetyards x sign, not checked in game“; sonst Standardreihe (Rack 1 am Bug, eine Zelle Abstand) **ohne** `position` – dafür `position` optional (gemessen: einzige Anzeige ist der Kisten-Tooltip). Rack-Ids `rack-<Fleetyards-Index>`. `maxCrateSize` an jedem importierten Rack; fehlt es in der Datei (hull_a), ist das keine Abweichung. `-Force` nur bei gleicher Rack-Id-Menge, schreibt auch `source`, verweigert hull_a. Exit 0/1/2, nicht geschriebene neue Schiffe zählen als 1. Testexport mit 4 synthetischen Schiffen (+ Überlappung), Skript läuft unter 5.1 und pwsh 7. Commits 0 (position optional) und 2a/2b (Ausgabe/Handkorrektur) getrennt. Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Befund** (Export 2026-10-22):
- 117 Schiffe, 96 mit Gitter, 21 ohne (übersprungen), 9 mit Offsets an allen Racks; 0 Id-Kollisionen, 0 Maße oder Offsets außerhalb des 1,25-m-Rasters, Kapazität = Zellen in 288/288 Racks, Racksumme = `cargo` in 96/96.
- Hull A gegen `hull_a.json`: Gitter 4×2×2, Kapazität 16/64 und Ebenenabstand (1 Zelle) gleich. Abweichend: Reihenfolge (Fleetyards je Seite unten zuerst = unsere 2, 1, 4, 3), Abstand Backbord–Steuerbord 3 statt 1 Zelle, `stackAxis` fehlt bei Fleetyards (Datei `±y`), `maxCrateSize` 16 bei Fleetyards, in der Datei bewusst weggelassen. Der Abgleich meldet hull_a als „same“.
- Offsets überlappen bei Carrack (9 Paare), Caterpillar (39), Hull C (2); Mittelpunkt-Lesart: Caterpillar 91, Starlancer MAX 2, 21 Origins vom Raster. Ursache: Racks mit identischem Offset und Maß (modul-lokal). → Standardreihe mit Notiz.
- Mit `length` = Fleetyards-y hätte der Planer in 67 Racks / 22 Schiffen Kisten abgelehnt, die das Spiel nimmt; mit der längeren Seite in 26 Racks / 6 Schiffen, alle wegen der 32er-Form (→ T7.1c).
- `-Create`: 95 Dateien, hull_a „same“; zweiter Abgleich: 96 same, Exit 0, idempotent. CLI `plan example_missions.json --ship` c2_hercules / caterpillar / hull_b: Exit 0.
- Fehler beim ersten echten Lauf: `$PSScriptRoot` ist in 5.1 in `param()`-Defaults leer → Default im Rumpf, Test ohne `-ShipsFolder` (`1d555f9`).
- Nachtrag Abnahme: `-Create` schrieb in einen Ordner ohne `hull_a.json` eine Fleetyards-Hull-A (ohne Spindelachsen). Jetzt „skipped … never written by the import“, kein Exit 1; Test `Create_never_writes_hull_a`.
- Tests: Core 732 (626 + 1 position + 10 Skript + 95 `Every_shipped_ship_loads`), App 810 (+1 Tooltip), Import 93, alle grün; `LoadAll().Problems` leer.

### [x] T7.1c 32-SCU-Kistenform – vor T7.2
Fleetyards gibt die 32er als 8×2×2 an (Reihe 8/16/24/32 = 2/4/6/8 lang, in sich konsistent), `CrateSize` als 4×4×2. Mit 4×4×2 nehmen 26 Racks in 6 Schiffen (Hull B, C1, Freelancer, Shiv, Starlancer MAX/TAC) keine 32er, obwohl ihre Datei 32 erlaubt. Spielprüfung durch Martin, dann D-Eintrag, `CrateSize`, T7.0-Fixtures, Golden-Tests prüfen.

**Präzisiert in der Planung (D57, Addenda D9/D27/D56):** Statt Spielprüfung zwei Quellen (Star-Citizen-Wiki 10 × 2,5 × 2,5 m, Fleetyards 8×2×2). Testschiff `uneven_long` 2 × 10×3×3 statt 9×3×3 (dort greift in Lauf 4 die Suchgrenze, der Test soll nur den Fallback prüfen); `spike_uneven` bleibt für die Loader-Tests. Würfel 4×4×4 → Säule 8×2×4 (`Column`), Max-Crate-Rack 8×2×2 mit Limit 16, `big_rack` 8×3×2. Standard-Split bleibt 16 + 16, Regel „halbieren, wenn die Hälfte eine Kistengröße ist, sonst größte passende plus Rest“ (D27-Addendum, Text von D27 unverändert). Footprint-Test → Invariante „jede Zerlegung passt“, die Prüfung in `SplitProblem` bleibt. Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Befund:**
- Stellen mit 4×4×2: Code nur `CrateSize.cs`; Doku `CLAUDE.md`, D9, `plan-format.md`, `ship-format.md` (Tabelle + Carrack-Beispiel); README/user-guide keine.
- Mit 8×2×2 vor dem Umbau der Tests 22 Core-Tests rot (9 Shape, 5 MaxCrateSize, 3 OrderSearchLimit, 2 PlaceInto, 2 SplitMerge, 1 Models), Hull-A-Goldens grün; dazu zwei Tests grün aus falschem Grund (`PlaceProblem_names…`, `CanHold…(32, false)`): keine 32er passte mehr.
- Racks in `ships/`, die 32 erlauben, aber keine fassen: 26 / 6 Schiffe → **0**. Fleetyards' `maxContainerSize` = größte Kiste, die das Gitter zulässt, in **284/284** Racks (4×4×2: 201) → `maxCrateSize` begrenzt in den Daten nichts (D56-Addendum).
- Jede Zerlegung jeder Kiste passt in ihre Grundfläche (32 SCU: 211 Zerlegungen, 0 abgelehnt). Merge prüft generisch (`BlockProblem`), keine Änderung.
- Lauf 4 auf 2 Racks je Größe: 8×3×3 5 ms, 9×3×3 66 ms **mit** Suchgrenze („too many crates to rearrange“, 81 Zellen, 32 + 40 × 1), 10×3×3 54 ms ohne; Lauf 3 „16 / 34 / 52 SCU free“.
- Stresslauf nachgebaut (spike_huge war gelöscht, Zusammensetzung jetzt festgehalten): 1 Rack 12×12×7, 4 Ziele, 958 SCU = 6×32, 6×24, 10×16, 20×8, 30×4, 40×2, 102×1 (Dest1/2 je 2×32, 2×24, 3×16, 5×8, 8×4, 10×2, 26×1; Dest3/4 je 1×32, 1×24, 2×16, 5×8, 7×4, 10×2, 25×1). Release-CLI, je 3 Läufe: 4×4×2 849/577/574 ms, Median **577 ms**; 8×2×2 892/636/621 ms, Median **636 ms**; beide 958/958, Exit 0, beide mit Suchgrenzen-Hinweis.
- Tests: Core 737 (732 − 1 Footprint + 6 Invariante), App 810, Import 93, alle grün; Exhaustive 2/2 grün (57 s).

### [x] T7.1d Suchbudget neu kalibrieren
Befund T7.1c: 9×3×3 mit 32 + 40×1 braucht 1,57 Mio. Schritte (509 ms) und wird mit 30.000 verworfen; 57 Racks in 26 Schiffen haben über 80 Zellen. Messreihe 30.000 / 300.000 / 1 Mio. / 2 Mio. / 4 Mio. (Release-CLI, je 3×, Median): spike_huge, 9×3×3, Hull-A-Lastfall, C2 small hold. Auswahlregel vorab: größtes Budget mit spike_huge ≤ 1,5 s. Determinismus bleibt (Schritte, nicht Zeit).

**Präzisiert in der Planung (D58):** Die Regel ergab 150.000, aber keiner der vier Fälle gewann dadurch etwas (9×3×3 erst ab 2 Mio. gelöst, spike_huge +0,67 s) → Budget nach Zellen skaliert: `MaxOrderSearchSteps(cells) = 150.000 × 1.008 / cells`, eigener D-Eintrag D58 statt D54-Addendum, Formel als Unit-Test. Limit-Tests: Jeder Plan am Limit kostet mit Skalierung etwa gleich viel (~1,3 s Release, ~8 s Debug), billiger wird kein Rack → Kategorie `Slow` (aus dem Standardlauf, eigener CI-Schritt); PlaceInto/MoveStop auf 8×4×4 (4 + 113 × 1), weil 8×2×4 mit 4 + 49 das Limit nicht mehr erreicht und eine feste 32er die Suche sofort beweisen lässt. Zeit-Assert (1 s) entfernt: Er hat die CI seit T7.0 rot gemacht (1.099 / 1.297 ms auf den Runnern). Zwischenmessungen 100.000 / 150.000 / 200.000 für die Auswahlregel. Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Befund:**
- Messtabelle in D58. Skaliert (Repo-Release-Build, Median): spike_huge 150.000 Schritte 1.307 ms mit Hinweis; 9×3×3 1,87 Mio. 863 ms **ohne** Hinweis; Hull A 9,45 Mio. 110 ms (kein Suchlauf); C2 small 700.000 132 ms (1 Suchlauf, 45 Schritte). Alle Ausgaben je Fall byte-gleich, Exit 0.
- Schrittkosten: ~5,7 µs auf 1.008 Zellen, ~0,3 µs auf 81. Scheinbare +31 ms bei Hull A/C2 in der Scratch-Kopie kamen vom Build-Ort (Temp-Ordner), nicht von der Formel: eine unveränderte HEAD-Kopie zeigt sie auch.
- Golden-Dateien seit `d93b8c6` unverändert, Exhaustive 2/2 grün (55 s). Slow 4/4 grün (Debug 8 / 8 / 1 / 1 s, Release 3 / 3 / 0,5 / 0,5 s).
- Tests: Core 738 im Standardlauf (5 Limit-Tests → 1 schnell + 4 Slow, + 4 Formelfälle), App 810, Import 93.

### [x] T7.2 Schiffsauswahl – Editor nach Hersteller gruppiert, Wahl in settings.json statt `App.ShipId`-const, Plan beim Schiffswechsel bzw. bei ShipId-Mismatch beim Laden (heute: ungefangene ArgumentException in `ShipViewBuilder`), Fallback auf hull_a bei fehlender Schiffsdatei

**Präzisiert in der Planung (D59):** Neu planen statt verwerfen/blockieren/Plan pro Schiff, in `LoadPlanner.Replan` (Komposition aus `Plan`, `RetryUnplaced`, `SetDelivered`, kein neuer Planer-Code): `Plan` in Stop-Reihenfolge, bei `PlanException` leerer Plan + `RetryUnplaced`, Haken ganzer Stops zurück; Altbesuch mit neuer Fracht kommt unabgehakt zurück. Splits bleiben (Split/Merge buchen in die Missionen um), daher Dialogtext „… manual moves are lost; split crates keep their sizes.“, mit Haken „N stops already ticked – ticks stay.“; ohne Missionen kein Dialog. Fremde plan.json (Schema 2/3) lädt ohne Gitterprüfung, `OverlayViewModel.ShowPlan` plant neu („Plan was made for X, replanned for Y.“), Schema 1 fremd bleibt abgewiesen. Eigene `NoticeSource.Ship`/`Replan` (PlanSaver löscht `Plan` bei jedem Speichern). `ship` optional ohne Bump, bleibt bei nicht ladbarem Schiff stehen; ohne `ship` nimmt der Start das Schiff der plan.json, wenn es lädt („Ship set to <name> from your plan“, nicht gespeichert; `PlanFile.ReadShipId`). `ShipCatalog.Problems` → `ShipProblem(ShipId, Message)`. Gruppierte ComboBox oben im Editor, wirkt sofort; Pfeile/Tippen/Mausrad öffnen in der geschlossenen Box erst die Liste (dort ändert nur Enter/Klick die Auswahl, dotnet/wpf `ComboBox.cs`), jede Auswahländerung ist eine Wahl. Kaputte Dateien als „<id> – <Grund>“ unter „Not loaded“, nicht wählbar. Module: eine Schiffskonfiguration ist eine Katalog-Id, kein `modules`-Feld (D59 Consequence). Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Befund:**
- Mismatch vor T7.2: **kein Crash**. `PlanStore.Load` fing die `PlanFormatException` („Plan not loaded … is for ship 'c2_hercules', not 'hull_a'“), der Sync pausierte, die Datei blieb; `ShipViewBuilder` erreichte kein fremder Plan (Tests `Plan_for_another_ship_gives_a_problem…`, `A_plan_for_another_ship_is_rejected`). Der Satz im Tasktitel stimmte nicht.
- Halter: ein `Ship` nur in `OverlayViewModel`; `App.ShipId` an 1 Stelle gelesen; Header-Bindung war `OneTime`.
- Größter Fixture-Plan 8 Missionen / 19 Kisten. Replan des Beispiels (3 Stopps) auf alle 96 Schiffe: zusammen < 2 s (Debug). Der 8-Stopp-Lastfall auf der **Freelancer** (Rack 9×2×3): **~4 s Release** (CLI 4.252 / 4.051 / 3.963 ms), 8 s Debug, Suchlimit (D58) – gleich beim Editor-Plan auf diesem Schiff → Test `Slow`, Messwert an der Backlog-Zeile „Planen in `Task.Run`“.
- App-Lauf (echte plan.json/settings.json gesichert und byte-gleich zurück): ohne `ship` + C2-Plan → Header „CRUS C2 Hercules“, „Ship set to … from your plan.“, `ship` nicht geschrieben; `ship: hull_a` + C2-Plan → neu geplant + Hinweis, plan.json danach hull_a; `ship: nope` → Hull A + „Ship 'nope' not loaded: not found – using MISC Hull A.“, `nope` bleibt. Editor per UI Automation: „C2“ tippen öffnet die Liste und hebt C2 Hercules hervor (Auswahl bleibt Hull A), Enter → Dialog; Nein → Hull A, nichts geschrieben; Ja → `ship: "c2_hercules"`, plan.json für die C2. CT-Sync lief beim Start auf dem jeweiligen Schiff weiter.
- Nebenbefund: ComboBox-Einträge hießen für Screenreader `ShipChoice { Id = … }` (Record-`ToString`) → `AutomationProperties.Name`. `FillOrderChoice` hat dieselbe Lücke (offen seit T4.7) → T7.3, Punkt 5.
- Nebenbefund Planer (vorher schon so, ohne Wechsel nachgestellt): echte CT-Captures per `PlaceInto` in den Hull-A-Plan legen Kisten auf früher entladene Stopps (rot, weiche Regel D24-Addendum) → Beleg an der Backlog-Zeile „Stapelbewusste Partition“.
- Befund für T7.3 (Screenshots im Scratchpad): Hull C 16 Racks als lange Diagonale, Kisten in Rack 1 kaum sichtbar, Zellraster dominiert, Bug-Keil winzig; Caterpillar 14 Racks in einer Reihe, Kisten klein, Racknummern über den Boxen.
- Tests: Core 848 (738 + 7 Replan + 96 Schiffe + 8 PlanFile − 1 gestrichener Fremdschiff-Fall), + 1 `Slow`; App 849 (810 + 39); Import 93; Exhaustive und Slow grün, Goldens unverändert.
- Nachtrag: Die Auswahl zog in T7.2b vom Editor in den Overlay-Header (Hauler-Feedback); der Editor zeigt nur noch „Ship: <Name>“.

### [x] T7.2b Schiffsauswahl im Overlay-Header (Präzisierung zu T7.2, Hauler-Feedback)

Martin hat die Auswahl im Editor nicht gefunden und am Schiffsnamen im Header gesucht. Die Auswahl zieht dorthin, keine neue Logik: `ShipChoices`, `ShipChangeQuestion`, `ChangeShip` bleiben, nur der Ort ändert sich. Gesperrt: Name als Text. Keine Tastatur (WS_EX_NOACTIVATE), Mausrad scrollt die Liste. Editor zeigt nur den Namen. Dialog gehört dem Overlay.

**Präzisiert in der Planung (D59-Addendum):** Liste auf der Fensterfarbe ohne Alpha (`WindowOpaqueBackgroundBrush`), weil der Header-Abschnitt keinen Hintergrund hat und die Liste über dem Spiel steht. Commit wie T7.2 portiert (SelectionChanged + Guard, Dialog nach dem Schließen); Korrektur zum Tasktext: T7.2 übernimmt bei SelectionChanged, „nur Klick“ entsteht, weil die Box nicht fokussierbar ist. „Lock blendet aus“ per DataTrigger auf `IsLocked` wie die Header-Buttons, vorgeführt per UI Automation statt Unit-Test. Der Editor erkennt einen Wechsel von außen an der Schiffs-Id (vorher `_ownChange`). Liste schließt beim Lock; kein DragMove bei offener Liste. User Guide: Name ist entsperrt kein Ziehgriff mehr. Spike um f) ergänzt (Fokus und Lock nach dem Dialog); bei Fehlschlag wäre die Bestätigung ein Zustand im Overlay geworden. Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Befund:**
- Spike über SC (Martin, Borderless): a) Liste über dem Spiel ja, b) SC behält den Fokus nach Auswahl ja, c) Mausrad scrollt ja, d) Auto-Lock schließt die Liste ja, e) Schloss schließt sie ja, f) nach dem Dialog Klick ins Spiel reicht (kein Alt+Tab), Auto-Lock geht, nichts hängt. → MessageBox bleibt; Inline-Bestätigung wäre größer (VM-Zustand, Layout, Lock-Regel).
- Ohne SC (UIA, temporäre Hotkeys): Popup-HWND `TOPMOST | NOACTIVATE | LAYERED | TOOLWINDOW`. Bei offener Liste schließt Schloss 5/5, Ausblenden 5/5, **Lock-Hotkey 0/5** → `ApplyLock` schließt die Liste explizit, danach 5/5.
- App-Lauf (plan.json/settings.json gesichert und byte-gleich zurück): Box „MISC Hull A“, 96 Einträge in 13 Gruppen, Vorauswahl Hull A; gesperrt Box weg, Name als Text. Mit offenem, ungeplantem Editor: Nein → Hull A, nichts geschrieben; Ja → Header und Editor „CRUS C2 Hercules“, Editor behält 7 Zeilen und „unplanned changes“, kein „changed outside“, `ship: "c2_hercules"`, plan.json für die C2.
- Sichtprüfung (Martin): Optik abgenommen; Bildlaufleiste der Liste auf Wunsch angeglichen: 8 px, keine Pfeile, Spur unsichtbar, Griff in Header-Farbe (35 %, unter der Maus 60 %), nur in der Schiffsliste.
- Tests: App 850 (849 − 5 Editor-Auswahl + 3 Editor + 2 Overlay + 1 Theme); der Test „Wechsel von außen ≠ changed outside“ vor der Id-Erkennung rot gesehen. Core 848, Import 93.
### [x] T7.2c Schiffsliste: Suche im Header und Gruppe „Recent“ (Martins Wunsch nach T7.2b)
96 Schiffe sind eine lange Liste. Zwei Hilfen, die sich ergänzen:

1. **Suche:** Bei offener Liste wird die Namenszeile im Header zum Eingabefeld (Platzhalter „Search ships…“, modern
   integriert, kein zweites Feld über der Liste). Jeder Tastendruck filtert nach Name und Hersteller, ohne Groß/klein.
   Esc leert bzw. schließt, Enter wählt bei genau einem Treffer. Danach Commit-Regel und Dialog wie T7.2b.
2. **Recent:** Gruppe „Recent“ ganz oben mit den letzten 3 gewählten Schiffen (ohne das aktuelle?), gespeichert über
   Neustarts. Braucht keine Tastatur und keinen Fokuswechsel, also auch baubar, wenn die Suche am Spike scheitert.

Knackpunkt Suche: Tastatur heißt Fokus. Das Overlay nimmt dem Spiel seit D11 (Addendum T3.6) nie den Fokus
(`WS_EX_NOACTIVATE`, nichts im Header fokussierbar). Beim Aufklappen holt sich das Overlay deshalb den Vordergrund
(wie der Editor beim Öffnen); solange die Liste offen ist, gehen Tasten nicht ans Spiel. Tippen ohne Fokuswechsel ginge
nur per globalem Tastatur-Hook: **ausgeschlossen** (CLAUDE.md: keine Hooks; für EAC ein Keylogger).

Zuerst prüfen, je 5 Versuche über SC (Borderless), Ergebnis vor dem Umbau melden:
- a) Feld hat nach dem Aufklappen per Klick sofort den Fokus (Tippen filtert ohne zweiten Klick).
- b) Nach Auswahl bzw. Esc bekommt SC den Fokus mit einem Klick ins Spiel zurück (kein Alt+Tab).
- c) Auto-Lock bzw. Lock-Hotkey, während das Feld den Fokus hat: Liste zu, Lock korrekt, nichts hängt.
- d) Tasten, die SC sonst bekommt (z. B. F, Tab), landen bei offener Liste nur im Feld.
Fällt a) oder b) durch: nur Recent bauen, Suche ins Backlog mit Befund.

Offen für die Planung: Wo Recent gespeichert wird (`settings.json`, neues optionales Feld ohne Schema-Bump wie `ship`,
D31) und ob das aktuelle Schiff darin erscheint. Filter und Recent-Reihenfolge als reine Funktionen neben `ShipChoices`
(Tests zuerst). D-Eintrag: das Overlay nimmt erstmals Tastatur (Gegenargument D11), D59-Addendum für Recent.
User Guide „Your ship“ ergänzen.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (D60, D59-Addendum T7.2c):** Recent als `recentShips` in
settings.json ohne Schema-Bump, geschrieben zusammen mit `ship` (aktuelles + 3, max. 4 Ids); Recent ohne das aktuelle
Schiff, mit vollem Namen, zusätzlich in der Herstellergruppe; leeres Feld zeigt den Platzhalter; Feldtext 2 px eingerückt;
kein automatisches Zurückgeben des Vordergrunds; Suche über Hersteller-Code und vollen Namen (Katalog kennt nur Codes);
Enter nur bei genau einem Schiff (Recent + Gruppe zählt einmal); Alt+F4 im Feld geschluckt; Look aus der Sichtprüfung im
Spike: Feld so breit wie die Liste und so hoch wie ein Eintrag, 1 px Rahmen in `ButtonsBorderBrush`, 2 px Abstand zur
Liste, Listenrahmen ebenfalls `ButtonsBorderBrush`.

**Befund:**
- Spike ohne SC (UIA, echte Klicks und Tasten gegen ein Stand-in-Fenster, das seine Tasten protokolliert, temporäre
  Hotkeys): a) Fokus im Feld nach dem Öffnen, Tippen filtert 5/5; b) nach Esc ein Klick ins Stand-in gibt ihm Vordergrund
  und Tasten zurück 5/5; c) Lock-Hotkey bei Fokus im Feld schließt die Liste und sperrt, Entsperren und neues Öffnen gehen
  5/5; d) F und Tab landen nur im Feld, Stand-in bekommt 0 Tasten 5/5. `Activate()` immer `True`.
- Spike über SC (Martin, Borderless): a–d grün.
- Nebenbefunde: Auto-Lock greift bei offener Suche nicht (SC nicht im Vordergrund, `AutoLockRule`); nach Schließen oder
  Sperren bleibt das Overlay im Vordergrund bis zum Klick ins Spiel.
- App-Lauf (plan.json/settings.json gesichert und byte-gleich zurück): Alt+F4 im Feld wirkungslos; **Pfeil runter schloss
  zunächst die Liste** (Fokus wandert aus dem Feld, die Box klappt bei Fokusverlust zu) → Pfeile, Bild auf/ab und Tab
  bleiben im Feld; Enter bei 3 Treffern nichts, bei einem Treffer Rückfrage (Plan mit Missionen), Ja → Hull C;
  settings.json danach `"recentShips": ["hull_c", "hull_a"]`; Liste zeigt „Recent: MISC Hull A“.
- Offen für Martin: Die Liste öffnet auf dem aktuellen Schiff, Recent steht dann oberhalb des sichtbaren Teils.
- Tests: App 877 (850 + 21 `ShipChoices` + 4 Settings + 2 Schiffswechsel; vor dem Code rot gesehen: Compile-Fehler zu
  `RecentShips`, `Matches`, `RecentGroup`, `Remember`, `SingleMatch`, `ChangeShip`/`Of` mit neuen Argumenten), Core 848,
  Import 93.

### [x] T7.3 Schiffsansicht bei großen Gittern – Hull C, C2: Racknummern (`RackNumberSize`: ein Wert für alle Racks), Fit, Legende
Befund aus T7.2 (Hull C, Caterpillar, C2; Screenshots im T7.2-Scratchpad):
1. Lange Reihe passt nicht ins Bild (Hull C 16, Caterpillar 14 Racks in der Diagonale, Kisten unkenntlich):
   Standardreihe beim Import in Zeilen statt einer Reihe, oder Kamera-Fit auf belegte Racks. Mit einem Schiff beginnen.
2. Zellraster dominiert auf großen Racks (Hull C 8×8×6): Linienstärke oder Raster erst ab Zoom.
3. Racknummern liegen über den Kisten (Caterpillar).
4. Bug-Keil winzig bei langen Schiffen: Mindestgröße in Bildschirmpixeln wie Racknummern (D32).
5. Fill-order-ComboBox liest sich für Screenreader als Record-ToString: wie ShipChoice beheben.

Je ein Schiff pro Problem: Hull C (1, 2, 4), Caterpillar (3), Starfarer als Normalfall-Kontrolle.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (D61, Addenda D15, D21, D32, D52):** Abnahme als
Sichtprüfung bei 800 × 600 px mit der 8-Stopp-Fixture je Schiff. 12 px Kistenkante hart für C2 und Starfarer, für Hull C
und Caterpillar Ziel; dort hart: alle belegten Racks im Standard-Fit mit lesbarer Nummer, Kisten zählbar nach ≤ 2
Mausrad-Stufen (Hull C mit Racks 1–8 = 71 Zellen in Reihe kommt bei 800 px höchstens auf 9,7 px). Hull A pixelgleich.
„Lang“ = länger als 2 × breit (geometrisch, nicht nach Fenstergröße); lang heißt Standard-Yaw 70°, Fit auf belegte Racks
+ Keil (gelieferte zählen, ein Tick fittet nicht neu), leere Racks gedimmt, Nummer über dem Rack mit Platz oben im Fit.
Für alle Schiffe: freie Zellen unter 12 px pro Zelle nur in Stapelebenen mit Kisten; „Bow“ schrumpft durch den Platz auf
dem Keilboden nur bis 12 px. Yaw 90 aus dem Plan fiel an Martins Zusatz durch (Caterpillar-Modul mit Kisten in allen vier
Tiefenreihen, Vergleich 90/70/60: bei 90 nicht zählbar); die Platzreserve oben kam, weil die Nummern bei schräger Reihe
abgeschnitten waren. FloorLabel-Tests stehen bei den bestehenden in `SceneProjectorTests` statt in einer neuen Datei; der
Test „Label verlässt nie den Boden“ (D52-Addendum 3) ist durch die Mindestgrößen-Regel ersetzt. Keine Datenänderung in
`ships/`, kein Planer-Code.

**Befund:**
- Planung per CLI: Hull C und Caterpillar belegen mit der 8-Stopp-Fixture Racks 1–8, C2 beide, Starfarer alle fünf
  (Caterpillar-Rack-Ids sind nicht nach Nummer sortiert).
- Messwerte 1-SCU-Kante x / z, Standard-Fit, 800 × 600 px bei 106 dpi (vorher, Diagonale, ganzes Schiff):
  C2 21,5 / 18,3 px (14,9), Starfarer 21,8 / 18,5 (14,3), Caterpillar 11,8 / 10,0 (6,6), nach 2 Stufen 17,0 / 14,4;
  Hull C 8,0 / 6,8 (3,8), nach 2 Stufen 11,6 / 9,8. Hull A 32,5 px, unverändert.
- Hull A: Screenshot vorher/nachher mit deckendem Hintergrund, im Fensterinneren 0 Pixel Unterschied (nur die äußeren
  3 px, wo sich der 1-DIP-Rahmen bei 106 dpi mit dem Desktop dahinter mischt).
- Tiefe (Caterpillar Rack 1, 16 × 1 + 4 × 2 SCU, eine volle Lage 6 × 4): bei Yaw 90 liegen die vier Reihen als Bänder
  übereinander, bei 70 und 60 sind alle Kisten zählbar. Martin: 70.
- FillOrder per UI Automation: vorher `FillOrderChoice { Value = BowFirst, Text = Bow first }`, nachher „Bow first“,
  „Stern first“.
- Sichtprüfung (Martin): abgenommen mit Yaw 70, Nasenlänge und Rastergrenze 12 unverändert.
- Legende: kein Befund.
- Nebenbefund: Scratch-Läufe der App überschreiben `settings.json.bak` (erstes Speichern pro Lauf); in T7.3 nicht
  gesichert, aus der unveränderten settings.json wiederhergestellt (gleiche Größe). plan.json und settings.json
  byte-gleich zurück.
- Tests: App 910 (877 − 1 ersetzter + 34), Core 848, Import 93; vor dem Code rot gesehen (31 Compile-Fehler, 2 FloorLabel-
  Asserts gegen den alten Code, 2 Compile-Fehler zu `Fit(…, top)`).
### [x] T7.4 Schiffsumriss aus `ship-outlines.json` (D55) – vor T7.5, vor dem Tag
- Erste Zahl: Orbit mit Apollo-Polygon (99 Punkte) bei laufendem SC, Messung wie D21. Ruckelt es → Phase 8.
- `ship-outlines.json` + `NOTICE.md` neben `ships/` im ZIP; Zuordnung über Schiffs-Id; ohne Eintrag bleibt der Keil.
- Polygon unter den Racks, Themefarbe, Bow-Label bleibt wie D52 (Platz ohne Keil klären).
- Sichtprüfung an 5 Schiffen im Overlay: Hull A, C2, Caterpillar, Reclaimer, 890 Jump (die drei letzten sind die zweideutigen).

**Präzisierung in der Planphase, ratifiziert durch Freigabe (D62, Addenda D52, D55, D61):** Gate-Polygon Hull D
(362 Punkte, das größte der Datei) statt Apollo; Gate = A/B mit Schwellen (Median + ≤ 1 ms, p95 ≤ 16 ms) plus Martins
Urteil. Ablage `outlines/` neben `ships/` (in `ships/` läse `ShipLoader` die Datei als Schiff), publish.ps1 prüft beide
Dateien. Zuordnung über `fleetyardsSlug` statt Schiffs-Id. Ladefehler → eine Notice-Zeile beim Start. Umriss als
skalierte Kulisse (Racks × 1,2 in der bindenden Achse, Mitte der Racks, Boden), kompakt ganz im Fit, lang belegte Racks
+ Bugzone; Hull-A-Schranke ≥ 24 px Kistenkante, sonst Lang-Regel auch kompakt. Falsch getracte Einträge fliegen aus der
Datei, kein neues Feld. Nach der Phase-A-Pause (Martin): Umriss ist Standard, mit Umriss **kein Keil und kein
„Bow“-Label**; Preferences-Sektion „Ship view“ mit „Ship outline“: Outline / None (`shipOutline` in settings.json); der
Keil mit Label bleibt stiller Fallback für Schiffe ohne Eintrag; Gate auf dem echten Code statt Spike. „Pixel-Diff 0“
als Screenshot-Vergleich, kein Golden-PNG-Test.

**Befund:**
- Daten: 117 Einträge, 11.429 Punkte; alle 96 Schiffe finden ihren Umriss. Größte: Hull D 362, Hull E 344 (ohne
  Schiffsdatei), RAFT 203.
- Gate: ohne SC Hull A ohne Umriss Median 0,76 ms, p95 1,24 ms pro `OnRender` (Release, 32 Renders/s beim Ziehen). In-game
  von Martin abgenommen; das Log des Gate-Skripts enthält keinen Orbit, die In-Game-ms sind nicht erfasst.
- Sichtprüfung (800 × 600 px, 106 dpi, Hull-A-Acht-Stopp-Plan je Schiff neu geplant, Kistenkante x, Keil → Umriss): Hull A
  32,5 → 27,7 px, C2 20,4 → 20,4, Caterpillar 11,8 → 11,7, Reclaimer 14,0 → 14,2, 890 Jump 12,9 → 13,0. Look abgenommen
  (Martin). Keil-Pfad bei allen fünf pixelgleich (Eintrag fehlt: 0 px); Ordner fehlt oder Datei kaputt: Notice-Zeile + Keil.
- Bugrichtung Reclaimer (Klaue unter dem Bug in der Seitenansicht) und 890 Jump (spitze Nase) gegen die Pipeline-Vorschau
  geprüft: richtig, nichts gestrichen.
- publish.ps1: ZIP 58,9 MB, 358 Dateien, beide Umriss-Dateien drin.
- Tests: App 953 (910 + 43), Core 862 (848 + 14), Import 93, Slow 5; vor dem Code rot gesehen (Compile-Fehler zu
  `ShipOutlineLoader`, `OutlinePoint`, `ShipOutlineMode`); ein eigener Testfehler (Breite 12,5 m aus dem Export statt
  12,0 m der Silhouette) korrigiert.
- Nebenbefund PowerShell: `$s` und `$S` sind dieselbe Variable; zwei Scratch-Skripte sind daran gescheitert, AppData danach
  byte-gleich zurück.
### [x] T7.4b Spike Holo-Ansicht (kein Prod-Code, kein Commit außer dem Befund)
- Holo als dritte Kulisse neben Keil und Umriss, vor dem Release. Quelle: Fleetyards `holo.gltf` aller 117 Schiffe
  (`sc-ship-outlines/raw/<slug>/`). Drei Gates mit Schwellen vorab; ein rotes Gate → Phase 8, alle grün → T7.4c.
- Gate 1 Lizenz (Martin), Gate 2 Netz und Cache (Release-Asset, eine Datei beim Schiffswechsel, Cache in
  `%LocalAppData%`, Download < 2 s, Cache-Hit < 50 ms), Gate 3 Rendern (Weg a `OnRender`, Weg b `Viewport3D`;
  Median-Zuwachs ≤ 1 ms, p95 ≤ 16 ms, RAM pro Mesh, Martins Urteil).

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** D63 statt D62 (D62 ist der Umriss). Dezimierer
`@gltf-transform/cli` 4.5.1 per npx, Version in `sc-ship-outlines/TOOLS.md`; danach stdlib-Python ins eigene Format.
Download per Range-Request auf das rc.3-Asset, Länge = größte dezimierte Datei, kein Upload vor Gate 1. Orbit per Timer
(12°/s, 30 s, ≥ 300 Frames, sonst ungültig) statt Hand-Ziehen. Budgets 5 k / 20 k / 50 k Dreiecke. Weg (a) in den
Varianten flat, shaded, edges, silhouette (kamera-abhängig, pro Frame; nur 5 k und 20 k). Gate 1 mit drei Zuständen
(grün / rot / offen, Frist Tag `v0.1.0`). Nach den ersten Läufen (Martin): Maßstab 1:1 (1 Zelle = 1,25 m) statt der
Umriss-Regel 1,2 × Racks, jedes Schiff mit eigenem Holo unter dem neu geplanten Acht-Stopp-Plan (D59), Fit zeigt das ganze
Holo; Zielbild Hidden-Line nach drei Referenzbildern (opak, nur sichtbare Kanten, drei Kantenstufen), echtes 3D statt
2D-Umriss; Variante hiddenline in beiden Wegen, `viewport3d-flat` für den flat-Look über Weg (b); SC-Lauf eingegrenzt auf
die drei Looks shaded / flat / hiddenline über Weg (b), 20 k, Hull A und Caterpillar.

**Befund:**

| Gate | Messung | Schwelle | Ergebnis |
|---|---|---|---|
| 1 Lizenz | Martin, 23.10.2026: „volle Freigabe erhalten“; Grundlage Mail von Marten (Fleetyards); CIG hat weder freigegeben noch untersagt | schriftlich | **grün** |
| 2 Download | Range auf rc.3 über `github.com` → 302 → `release-assets.githubusercontent.com`, Status 206, je 5 Läufe mit frischem `HttpClient`: 86 KB 167 ms, 311 KB 231 ms, 777 KB 228 ms Median; kältester Lauf 625 ms | < 2 s | **grün** |
| 2 Cache-Hit | Lesen + Parsen aus `%LocalAppData%`, 20 Läufe: 0,08 / 0,17 / 0,65 ms Median, max. 1,2 ms; Schreiben 0,2–1,2 ms | < 50 ms | **grün** |
| 2 Daten | siehe unten; Bug 5/5 richtig | – | mit Einschränkung |
| 3 Rendern | mit SC, Weg (b), 20 k: Δ +0,21–0,30 ms, p95 1,4–4,0 ms, 60 / 60 fps, SC-Bildrate unverändert (Martin) | ≤ 1 ms / ≤ 16 ms | **grün** |
| 3 RAM | mit SC, 20 k: shaded +15–22 MB, flat +16–24 MB, hiddenline +42–48 MB | Zahl | – |
| 3 Blick | Racks bleiben Blickfang (Martin); das `Viewport3D` liegt als Ebene hinter der Schiffsansicht, der Rumpf verdeckt nie Kisten | Urteil | **grün** |

- **Rohdaten:** alle 117 Draco-komprimiert (54 Text-glTF mit base64, 63 GLB unter `.gltf`), 773 MB, 0,28–9,35 Mio.
  Dreiecke (Hull D bis Ironclad Assault). Die Holos enthalten das Schiffsinnere (Tanks, Leitungen, Cockpit), Hull D auch das
  voll beladene Frachtgitter.
- **Dezimierung:** Draco → `copy` → nur `POSITION` (flache Normalen trennen Vertices, `weld` verschmilzt nur bitgleiche) →
  `weld` → `simplify --error 1 --lock-border false`. meshoptimizer erreicht 20 k und 50 k nur bei Hull A und Hull D; bei
  C2, Caterpillar und Ironclad bleibt er bei 181 k / 606 k / 994 k stehen (gesperrte Vertices nicht-mannigfaltiger
  Topologie, 1.476 Komponenten beim C2), bei 5 k auch Hull A (10 k) und Hull D (8 k). Rest per Vertex-Clustering
  (stdlib): erreicht jedes Budget, zerstört aber Kanten (C2 mit Splitterdreiecken, Caterpillar mit Löchern).
- **Größen** (eigenes Format `SCH1`, float32 + uint16/32): 5 k 56–86 KB, 20 k 246–311 KB, 50 k 541–777 KB.
- **Achsen-Check** (IoU der Draufsicht gegen das Umriss-Polygon): Bug 5/5 richtig bei jedem Budget; Spiegelung bei den
  asymmetrischen Schiffen eindeutig richtig (Caterpillar 0,58 gegen gespiegelt 0,31, Ironclad 0,80 gegen 0,66); Breite
  ±5 % verfehlt beim C2 (−7,6 %, Holo gegen Render) und bei Hull A 5 k (−13 %, Clustering).
- **Gate 3 ohne SC** (Release, 1:1, Hull A / C2 / Caterpillar / Ironclad): Weg (a) hält 60 fps nur bei 5 k (Δ +0,3–1,7 ms);
  ab 20 k 30 fps (flat, shaded, silhouette) bzw. 20 fps (edges), shaded bei 50 k p95 bis 31 ms. Hiddenline über (a)
  (Maler-Algorithmus, ein `DrawGeometry` pro Dreieck): schon 5 k nur 30 fps, 20 k 12 fps. Weg (b) bei allen Schiffen und
  Budgets 60 fps, Δ ≤ 0,44 ms; RAM bei 50 k bis +109 MB (hiddenline). `OnRender`-Zeit allein unterschätzt Weg (a): die Last
  trägt WPFs Render-Thread (silhouette 20 k: 2,6 ms in `OnRender`, Frames alle 50 ms).
- **Look:** wählbar (Martin): shaded, flat und hiddenline neben dem Umriss. Hiddenline-Kantenstufen (Knickwinkel 20° /
  35° / 60°) trennen auf dezimierten Meshes kaum (Hull A 20 k: 18.510 / 16.633 / 14.495 Linien); die Kantenstreifen von
  Weg (b) sind ungleich dick.
- **Maßstab 1:1:** die generierte Rack-Reihe des Caterpillar (D53) ist länger als der Rumpf und ragt hinten heraus.
- Spike-Code (Worktree `spike/t74b-holo`) gelöscht; Pipeline- und Messskripte und die Meshes lagen nur im Scratchpad der
  Sitzung, nicht im Repo, nichts davon wurde hochgeladen. AppData nach
  jeder Serie byte-gleich zurück; dazwischen hat Martin die App selbst benutzt (Overlay verschoben, Recent), dieser
  Stand bleibt.

**D63-Entwurf „Holo backdrop“ (für `decisions.md` in T7.4c):**
- **Context:** D62 draws a flat traced outline; the maintenance panel in the game shows a 3D hologram. Fleetyards serves a
  holo model (`holo.gltf`, Draco) for all 117 ships with cargo, CIG game models. T7.4b measured it.
- **Options:** backdrop wedge / outline / holo flat / holo shaded / holo hidden line; source repo / app ZIP / release
  asset downloaded on demand / Fleetyards API.
- **Decision:** Holos are used, as a choice next to the outline (shaded, flat, hidden line), drawn by a `Viewport3D`
  behind the ship view at true scale. A non-commercial fan project; Fleetyards does the same; CIG has neither allowed nor
  forbidden it. It holds until CIG or Fleetyards says stop. Holos come only as a release asset of their own, one file per
  ship, downloaded on a ship change and cached per ship and version; never from Fleetyards.
- **Counter-argument:** In Marten's words (Fleetyards): „Grauzone, keine offizielle Freigabe, nicht im Fansite-Kit.“
  Tolerance is an observation, not a right; a stop can come without warning. The overlay's first network access. The
  launch slips again, after D53 and D55, on 23 Oct 2026. Dependence on GitHub's release hosting. meshoptimizer can't
  reduce three of five meshes; clustering spoils their edges.
- **Consequence:** Holos only as a release asset of their own, not in the app ZIP, not in the repo, their own NOTICE. A
  stop means: delete the asset, the app shows the outline; no new release, no Nexus withdrawal. Setting "Download ship
  holograms", default on, off means no access; one sentence in README and Nexus description: "Ship holograms are an
  optional download; they derive from game models and may be withdrawn on request."

### [x] T7.4c-1 Holo-Daten (D63): Pipeline, Format, Release-Ordner – vor T7.5, vor dem Tag
- Pipeline in `sc-ship-outlines`: alle 117 dezimieren, Budget festlegen (20 k gemessen), Clustering-Kanten bei C2 /
  Caterpillar / Ironclad klären; Kantenstufen für hiddenline (Mindestlänge statt nur Knickwinkel).
- NOTICE mit Quelle und Datum; Hosting als Release-Asset; Format, auf das T7.4c-2 aufsetzt.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (D63):** T7.4c ist in T7.4c-1 (Daten) und T7.4c-2 (App)
geschnitten. Hosting: eigenes Daten-Release, Tag `holo-v1`, `--prerelease --latest=false` (die README verlinkt
`releases/latest`), dazu `manifest.json` mit SHA256 pro Datei; ein fehlender Slug heißt zurückgezogen, ein gescheiterter
Abruf behält den Cache. settings.json für T7.4c-2: `shipOutline` um `holo-shaded` / `holo-flat` / `holo-hiddenline`
erweitert, dazu `downloadShipHolograms`. Nicht saubere und abgelehnte Schiffe werden nicht veröffentlicht, der Grund
steht in `rejected.md` im Pipeline-Repo. Das Pipeline-Repo kommt unter Git, privates Remote (Martin legt es an und
pusht). Werkzeuge: meshoptimizer 1.3.0 direkt per API, numpy 2.5.3 und opencv-python-headless 5.0.0.93 in `.venv`,
Node 24.16.0 gepinnt (Martin nannte 22, installiert und LTS ist 24). Kanten kommen mit drei Stufen aus der Pipeline,
Innenleben fällt per Sichtbarkeit (100 Richtungen) vor jeder Dezimierung, Fenster bleiben. Trimmungen bei der Freigabe:
keine Tiefenkarten-Metrik, Remeshing ist Phase 8, Martin gibt pro Kontaktbogen frei. Am Kalibrier-Halt (Martin):
Methode Prune + Permissive ohne Sloppy (Vorbehalt in TOOLS.md und D63), Splitter nur relativ (≤ 1,5 × Hull A = 15,63 %),
Budget als Leiter 20 / 30 / 40 k, erste Stufe, die alle Kriterien erfüllt; Gate 3 in T7.4c-2 am größten ausgelieferten
Budget mit SC. Bei der Umsetzung: „Dreiecke ±10 %“ heißt höchstens 110 % der Stufe; Kontur im Hidden-Line nur entlang
glatter Kanten (< 30°), Teil des Formats; Hausdorff der Flächen statt der Randlinien (ein Loch, das nur in einer Maske
offen ist, zählte sonst über seinen eigenen Rand); Achswahl per IoU über beide Achsen; Streuteile fallen vor dem Skalieren.

**Befund:**

| Schritt | Messung | Ergebnis |
|---|---|---|
| Experiment (Hull A, Hull D, C2, Caterpillar, Ironclad Assault, 20 k) | M1 = Prune; M2 = + Permissive, Rest per Sloppy | Hull A, C2 sauber; Hull D bleibt bei 0,5 % Fehler bei 30,7 k stehen, Ironclad bei 47,8 k, Caterpillar verfehlt den Hausdorff; Permissive senkt Dreiecke bei gleichem Fehler (Ironclad 39,9 k), Sloppy erreicht das Budget nie und hebt den Hausdorff (Ironclad 0,83 → 1,28 %) |
| Cull, k an Hull A / Hull D | Löcher aus 30 neuen Richtungen bei k = 1 / 2 / 4 / 8 | Hull A 0,093 / 0,098 / 0,106 / 0,127 %, Hull D 0,032 / 0,047 / 0,083 / 0,167 %; k = 2; entfernt Hull A 62 %, Caterpillar 73 % der Dreiecke |
| Gegenprobe (Avenger Titan, RAFT, Polaris, Vulcan, Endeavor) | eingefrorene Methode und Schwellen | 3 sauber (20 k / 30 k / 30 k), RAFT und Polaris am Hausdorff: dasselbe Muster wie im Experiment |
| Determinismus | 5 Schiffe ab `flatten` neu | byte-gleich |
| Alle 117 | 195 min, 0 Abbrüche | 37 × 20 k, 21 × 30 k, 11 × 40 k sauber; 48 nicht sauber (Hausdorff 44, Splitter 7, IoU 2, F-Score 2, Dreiecke 1) |
| Achsen | IoU der Draufsicht gegen den Umriss | nach dem Fix min. 0,868 (300i), Gegenachse immer ≤ 0,77 |
| Gate (Martin, 24.10.2026) | 6 saubere, 4 nicht saubere Bögen | alle 117 freigegeben, `rejected.md` leer |
| Release-Ordner | `verify` | 117 Dateien + NOTICE + Manifest, 64,7 MB, 333–799 KB pro Schiff, 0 Probleme, `pipelineCommit` 2959336 (NOTICE-Satz nach Martins Wortlaut) |
| Upload (Martin, 24.10.2026) | `holo-v1` als Pre-Release, Immutable Releases aus; danach anonym mit `curl` | 119 Assets; `manifest.json` 200 nach einer Weiterleitung in 0,45 s, byte-gleich mit dem Ordner; `misc-hull-a.holo` SHA256 = Manifest; unbekannter Slug 404; `releases/latest` leitet auf `/releases`, rc.3 unverändert |

- **Nicht sauber** heißt bei 43 der 47 vor dem Achs-Fix vermessenen (die Syulen kam erst danach dazu): schmale Anbauten
  (Läufe, Antennen, Türmchen) fehlen, höchstens 2–6,4 px breit auf 512 px; die Flächen decken sich (IoU meist
  0,97–0,99). Mehr fehlt bei Hull D und E (Ladung im Modell, 10–16 px), Orion (12,8 px) und Kraken (16 px); die Hull C
  trägt ihre Ladung ebenfalls, ihr fehlen 6 px.
- **Nach dem vollen Lauf gefunden**, für die Metriken unsichtbar (sie vergleichen mit einer Referenz in denselben
  Achsen): fünf Schiffe, die breiter als lang sind (Syulen 16 × 28 m, Clipper, Corsair, Railen, Aurora Mk II), lagen
  quer; die Cutter Scout hat 2.450 Streuteile über das Vierfache ihrer Länge, das Schiff kam auf ein Drittel. Fix:
  Streuteile raus, Achse per IoU über beide Achsen. Ein Zwischenschritt (Achse nach Fleetyards-Seitenverhältnis) drehte
  Mule und Reliant falsch (die Reliant fliegt mit senkrechten Flügeln) und ist verworfen. 9 Schiffe neu, 108 byte-gleich.
  Seitdem zeigt der Kontaktbogen den Umriss rot über der Draufsicht.
- **Kantenstufen:** L1 881–3.000, L2 2.886–15.000, L3 fast immer an der Kappe 15.000. Mit jeder Knickkante als Kontur
  sahen die drei Stufen gleich aus (Hull A 20 k: 18.604 Konturkanten); seit der Regel „Kontur nur < 30°“ 435.
- **Größtes Mesh:** Constellation Centaurus 50.782 Dreiecke (kommt innerhalb 0,5 % nicht unter 40 k), dann Ironclad
  Assault 40.025. Fünf Gruppen von Varianten teilen ein Modell und sind byte-gleich (z. B. Aurora Mk I CL/ES/LN/LX/MR).
- **Pipeline-Repo** `sc-ship-outlines`: 35 Python-Tests (`unittest`), vor dem Code rot gesehen
  (`ModuleNotFoundError: holo_mesh`, später je ein Assert- oder Attributfehler pro neuem Test); Commits 1cead41..2959336.
  Kein Upload: Der Release-Ordner liegt in `sc-ship-outlines/holo/holo-v1`, hochgeladen wird mit Martins OK.
- `docs/holo-format.md` neu (SCH2, Hidden-Line-Regel, Manifest). sc-cargoplan sonst unverändert: kein Code, kein
  App-Lauf, AppData unberührt.

### [x] T7.4c-2 Holo-Kulisse in der App (D63) – vor T7.5, vor dem Tag
- Aus T7.4b: Look wählbar (shaded, flat, hiddenline) neben dem Umriss, Keil bleibt Fallback, über `Viewport3D` hinter der
  Schiffsansicht, Maßstab 1:1, ganzes Holo im Fit.
- Leser in Core nach `docs/holo-format.md` (WPF-frei, Tests vor dem Code); flat trennt Vertices pro Dreieck, shaded an den
  Stufe-3-Kanten, `BackMaterial` gegen Lücken; Kontur nur entlang Kanten < 30°.
- Download: nur HTTPS von `releases/download/holo-v1/…`, nur beim Schiffswechsel (und für das Startschiff), Timeout ≤ 5 s,
  jeder Fehler endet still im Umriss (eine Notice beim ersten Fehler pro Lauf), Schalter „Download ship holograms“
  (Default an, aus = kein Zugriff); `manifest.json` einmal pro Lauf: fehlender Slug = zurückgezogen (Cache-Datei weg),
  anderer Hash = neu laden, Abruf gescheitert = Cache bleibt. Cache in `%LocalAppData%` pro Schiff und Datenversion.
- Settings: `shipOutline` um `holo-shaded` / `holo-flat` / `holo-hiddenline`, `downloadShipHolograms` (optional, kein
  Schema-Bump, D31), Sektion „Ship view“ (D62).
- Offen: Lage in z (Spike: Mitte der Racks), Racks außerhalb des Rumpfs (Caterpillar), Ladung im Holo (Hull C/D/E),
  RAM bei hiddenline (+42–48 MB bei 20 k gemessen), Hidden-Line-Weg (b) mit Linienbreite unabhängig vom Fit.
- Gate 3 mit dem echten Code (A/B gegen den Umriss, mit SC, Martin) am größten Mesh (Centaurus, 50,8 k): Δ ≤ 1 ms,
  p95 ≤ 16 ms. AppData vor Scratch-Läufen sichern und per Hash zurück.
- Ein Satz in README und User Guide: „Ship holograms are an optional download; they derive from game models and may be
  withdrawn on request.“
**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Gate-3-Schiff ist die Centaurus mit 50,8 k (nicht ein
40-k-Schiff); über 40 k liegt nur sie, als Override im Pipeline-Repo festgehalten (`overrides.json`, `run-all.ps1`
meldet jedes Schiff über 110 % ohne Eintrag, `TOOLS.md`; T7.4c-1-Nachtrag a406e95, Doppellauf byte-gleich), die
Ironclad Assault (40.025) liegt in der 110-%-Toleranz. Schalter aus = kein Netz, gecachte Holos bleiben sichtbar
(„Already downloaded holograms stay.“); Rückzug = Stopp der Verbreitung. Manifest jünger als 24 h = kein Abruf, sonst
ein Versuch pro Lauf; Rückzug nur durch ein Manifest von jetzt oder aus den 24 h; Kill-Switch = leere `ships`-Liste.
Jede 2xx-Antwort, Länge und SHA256 entscheiden. User-Agent ohne Commit-Hash. z: Mitte auf Mitte. Fit zeigt das ganze
Holo. Linien als kamerazugewandte Streifen. Liste heißt „Backdrop“. Umriss-Vergleich per Scratch-Harness. Beim Bau:
`Holo` der VM nur bei Holo-Look und passendem Schiff, Mesh im Speicher für Look-Wechsel, `ShipViewChanged`-Event der
Session; nach dem Look-Gate (Martin) Deckkraft als Regler `holoOpacity` (0,1–1, Standard 0,4) im Deckkraft-Commit;
danach zwei Farben (Martin: Variante C von drei) `holoHullColor` (Rumpf bei shaded/flat) und `holoEdgeColor` (Kanten bei
hidden line), je ein Farbfeld ohne Alpha-Regler unter dem Deckkraft-Regler, immer deckend.

**Befund:**

| Schritt | Messung | Ergebnis |
|---|---|---|
| Leser gegen holo-v1 | alle 117 Dateien, Länge und SHA256 gegen das Manifest | 117 / 117 |
| Aufbereitung Centaurus | `long`-Hash (a ^ b) kollidierte | 2 s → 18–35 ms (Fibonacci-Mischung) |
| Umriss-Modus | Overlay offscreen, Hull A, C2, Caterpillar, Centaurus, vor und nach T7.4c-2 | byte-gleich |
| Kamera | WPF-Projektion gegen `IsoProjection` + `ViewTransform`, vier Kameras | 1e-6 |
| Download echt | Manifest + Hull A kalt / Centaurus / Cache | 1,2 s / 413 ms / 6 ms |
| Gate 3 mit SC (Martin) | Centaurus, hiddenline, Orbit 12°/s, je 30 s | Zoom 1: Δ +0,12 ms, p95 3,31 ms; Zoom 3: Δ +4,10 ms, p95 11,07 ms, im Spiel flüssig → grün nach Martins Urteil; shaded/flat 1,56/1,55 ms; RAM hiddenline +41 MB |
| Look-Gate (Martin) | Farben, z-Lage, „Backdrop“, Deckkraft | grün, Deckkraft 0,4 als Regler |

- Die Kosten bei Zoom hängen an der Kantenzahl, nicht an den Dreiecken (fast jedes Schiff hat 15.000 Kanten auf
  Stufe 3; Streifenbau Hull A 2,09 ms, Centaurus 1,80 ms): Die vorab geplante Rot-Regel hätte das falsche Schiff getroffen.
- Tests: 2.159 grün (Core 954, Import 119, App 1.086), mit synthetischem SCH2 und Fake-Handler; zwei absichtliche
  Fehler im Download-Code fängt die Suite. Kein neues NuGet-Paket. AppData in keinem Scratch-Lauf berührt.
- Nach dem Doku-Commit (Martin): Preferences schloss die App. Ursache: die Grenzen des Deckkraft-Reglers als `int` per
  `x:Static` an `Slider.Minimum` (ein `double`), WPF wandelt dort nicht um. Kein Test öffnete das Settings-Fenster;
  neu `SettingsWindowXamlTests` (jede statische Reglergrenze ein `double`, vor dem Fix rot), dazu das echte Fenster
  offscreen geöffnet.
- Commits ab4acbb, de96e92, 31541bf, 8c7a123 und dieser; Pipeline a406e95. Martin pusht.

### [ ] T7.4d Mission und Abholort sichtbar machen (D68) – vor T7.5
- Problem aus dem Spiel: Kisten kommen pro Mission und Abholort hoch, das Overlay färbt pro Ziel; zwei Missionen zum selben
  Ziel oder eine Mission mit drei Abholorten sind nicht zu unterscheiden.
- Modell: `pickup` optional am Item, jede Kiste kennt ihr Item; Merge nur bei gleichem Item, Split erbt.
- Ansicht ohne Planer-Code: Infozeile bei genau einer markierten Kiste, Auswahl rahmt die Mission und dimmt den Rest,
  Legende mit Kopfzeile pro Abholort; Haken pro Stop (D16), Legende „aus“ blendet Kopfzeilen mit aus (D33).
- Sichtprüfung (Martin): Hull A, zwei Missionen nach Riker aus Lorville und Everus Harbor, gleiche Ware und Kistengröße;
  Drei-Orte-Mission am Abholort B.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** ein Item mit einer Liste von Abholorten ohne Mengen; Feld
`pickup`, Editor-Spalte „Pickup“ zwischen Stop und Source, die CT-Spalte „Source“ bleibt; Legende mit einer Kopfzeile pro
Ortsliste („From A, B, C“, gleiche Orte in beliebiger Reihenfolge und Schreibweise = eine Liste), darunter jedes Ziel
einmal; Infozeile mit „ / “; ohne Abholort im ganzen Plan keine Kopfzeile, sonst Kopfzeile ohne Text für Items ohne
Abholort; `Crate.Item` = Index in `Mission.Items`, Bump auf plan.json Schema 4 mit Migration aus 3, Missionsdatei bleibt
Schema 2 (`pickup` optional); Merge nur im selben Item, auch ohne Abholort; Hull-A-Goldens neu erzeugt, Diff nur
`schemaVersion` und `item`; Infozeile über der Schiffsansicht ohne eigenen Platz (kein Neu-Fitten beim Markieren);
neue Zeile erbt den Abholort; Dimmen 0,4 wie leere Racks (D61), Rahmenfarbe Look-Gate (Vorschlag weiß, 1,5 px);
CT liest `Contracts.PickupLocation` (Fingerprint, Split an „/“, OCR-Text unverändert, nicht im Duplikat-Schlüssel);
D-Eintrag ist D68 (D64 war vergeben).

**Befund:**

| Frage | Ergebnis |
|---|---|
| Wo gab es einen Abholort? | Nirgends: Editor-„Source“ ist die CT-Capture, Missionsdatei, plan.json, `Stop`, `Mission.Contract` ohne Feld |
| Kannte die Kiste ihr Item? | Nein, nur die Mission (Warteschlange pro Ziel + Ware + Größe) → Schema 4 |
| CT-Teilmengen pro Abholort? | Nein: `PickupLocation` ein Freitext pro Zeile, Fixtures „Collect Waste from …“ viermal ohne SCU, nur „0/9 SCU“ |
| Goldens | 10 Dateien, bis auf `schemaVersion` und `item` identisch, Racks und Positionen unverändert |
| Overlay offscreen (Hull A, acht Stops, ohne Auswahl) | byte-gleich zu 924a6f1, nach Commit 2 und 3 |
| Echte CT-DB (Kopie) | Fingerprint mit `PickupLocation` passt, Kandidaten werden gelesen |
| Tests | 2.231 grün (Core 992, Import 128, App 1.111), Slow grün; kein neues NuGet-Paket |

- Befund für die Sichtprüfung: Getippte Zeilen eines Ziels sind eine Mission (D42-Addendum). Lorville und Everus Harbor
  im Editor getippt rahmen deshalb gemeinsam; die Infozeile trennt sie, CT-Captures sind eigene Missionen.
- Offen: Sichtprüfung und Look-Gate Rahmen (Martin).
- Commits 6012d2a (Modell, Migration, CT, Editor), c03f7a4 (Infozeile, Rahmen), 2d9b5b8 (Legende), 4af8b51 (Doku).

**Nachtrag nach Martins erstem Blick (Testplan volle Hull A, zwei Missionen):** Infozeile „from <Ort> · <Ware> · to <Ziel>“;
Haken neben „From <Ort>“ bei mehr als einer Mission an Bord; Editor-Buttonzeile bricht um. **Präzisierung in der
Planphase, ratifiziert durch Freigabe:** der Haken ist eine echte Auswahl (D25), nur an Kopfzeilen mit Abholort, ohne
abgehakte Kisten, gesetzt solange alle Kisten des Orts markiert sind; „from“/„to“ klein; 4 px unter jedem Button, damit
umbrochene Zeilen Abstand halten. Geprüft: Mauspfad (`OnClick`) offscreen markiert 6 Kisten, nimmt sie heraus,
markiert erneut, das Binding bleibt, leere Auswahl nimmt den Haken weg (der Automation-Toggle führt den Befehl nicht
aus, deshalb der Mauspfad); Editor bei 520 px zweizeilig, bei 1.100 px einzeilig; Overlay ohne Abholort weiter
byte-gleich zu 924a6f1. Tests 2.236 grün (App 1.116). Commits 91af570 und dieser.

**Ersetzt durch B6 (D69):** Legende nach Mission statt nach Abholort-Liste, Checkbox an jeder Zeile, Infozeile mit `from ?`.

### [ ] T7.5 README/Guide/Screenshots – Schiffsliste, Known issue „Entwurf“, Credit Erec, Bild-Platzhalter (`<!-- image: … -->`) durch echte Bilder ersetzen, Ship-Template wird „Ship correction“
- Known issue (aus T7.2, D59; T7.2b: Wechsel jetzt im Overlay): „On ships with large racks and many small crates, Plan or a ship change can take a few seconds. The overlay doesn’t respond meanwhile.“
- Reihenfolge (23.10.2026): Umriss vor Doku, T7.4 und T7.5 getauscht (D55-Addendum). Die Screenshots zeigen den Endzustand, Doku zuletzt vermeidet einen dritten Tausch. Präzisierung in der Planphase, ratifiziert durch Freigabe.

→ Tag `v0.1.0` → T6.6 Nexus

---

## Bugfix B1–B6 (24./25.10.2026) – eingeschoben vor T7.4c, Branch `fix/plan-rules` (B6 nach T7.4d, Branch neu ab main)

**Warum:** Spieltest am Aufzug (Covalex, Hull A, 7 Stops): rote „… which is unloaded first“-Notices zu einer Reihenfolge, die der Spieler nie festgelegt hat; „Move refused … has nothing below it“ statt einer gültigen Lage; keine Kapazitätsanzeige; ein Ziel als zwei Stops („Thundering Express“ / „… Station“). Martins Vorgaben: Die Reihenfolge der Ziele entscheidet der Spieler, nie die App; ist eine Zuordnung möglich, wird sie angenommen, nicht abgelehnt. Merge nach `main` erst nach Martins Spieltest am Aufzug mit der Covalex-Route (nach Normalisierung 5 Stops).
**Präzisiert in der Planung (Session-Start):** T7.4/T7.4b sind erledigt, eingeschoben wird vor T7.4c. D10 ist seit D24 abgelöst, und abgehakte Kisten geben ihre Zellen seit T5.4 frei (D24-Addendum): Der D10-Punkt aus B3 entfällt samt D-Nummer. D63 ist für die Holos reserviert, neue Einträge ab D64. `decisions.md` bleibt Englisch wie D1–D62. Commits ohne Co-Authored-By, Martin pusht. Präzisierung in der Planphase, ratifiziert durch Freigabe.

### [x] B1 Zielnamen normalisieren (24.10.2026)
**Diagnose (CT-DB read-only, `immutable=1`):** Capture 9–12 ist ein Capture mit vier Objectives; beide Varianten kommen aus demselben Rohtext, nicht aus zwei Feldern. Jede Lieferzeile bricht vor „Station“ um; „to Melodic Fieids“ (l als i gelesen) ist kein Zeichenpräfix von „Melodic Fields Station“, der Listeneintrag „Freight alevator at Thundering Express Station“ zählte nicht (2 von 3). Über Captures hinweg setzte nichts „X“ und „X Station“ gleich.
**Präzisiert in der Planung:** Parser und Suffix-Netz (Martins Wahl auf Rückfrage); Notice auch im Sync-Pfad, gelöscht beim nächsten Sync mit Übernahme ohne Merge (K1); Schlüssel erst falten, dann gefaltetes „Station“ wortweise abstreifen (K3); Inhaltsschlüssel der Duplikate geprüft (K2: Missionen kommen und gehen über CT-Ids, nach `Unify` ein Name pro Ort, keine Änderung an `ContractSync`). Präzisierung in der Planphase, ratifiziert durch Freigabe (D40-Addendum B1).
- Parser: Ergänzung in der `AnchorForm` (I/L/1/| und O/0 gleich), „elevator“ mit verlesenem oder fehlendem ersten Buchstaben
- `DestinationNames` (Import): ein Ort = gleicher Schlüssel, längerer Name gewinnt; angewandt in Reader, Sync (`Unify` vor `ContractSync.Apply`), Editor-Zeilen und `ShowPlan` (alte plan.json einmalig, Notice „Stops merged: A → B“)
- `LoadPlanner.RenameStop` (Core): mechanisch, Missionen und Kisten gemeinsam, nichts bewegt, Haken bleiben

**Akzeptanz:** Tests grün (die vier Screenshot-Namen, Capture 9–12 als Fixture → 3 Stops, Martins plan.json vom 23.10. als Fixture → „Melodic Fieids“ gemergt, Positionen und Haken gleich; Unify + Sync derselben Erfassung → keine Mission weg oder neu, nur Namen; Editor-Zeilen „X“ und „X Station“ → ein Stop). CLI `contracts` gegen eine DB-Kopie: 9–12 ergibt 3 Stops ohne „⚠ 2 of 3 destinations“. App-Start mit der eigenen plan.json (Notice „Stops merged: …“, beim zweiten Start keine) und Namensvarianten im Spieltest: Martin, 25.10.

### [x] B2 Stop-Reihenfolge aus dem Planer entfernen (D64; Golden-Files bewusst neu) (24.10.2026)
**Warum:** Der Planer unterstellte an drei Stellen eine Route: Kistenzeile in Stopp-Reihenfolge (D5/D6: Stopp 1 in Rack 1, nur Nachbarn teilen), Stapelregel „nur auf eigener oder späterer Fracht“ (D24-Addendum), `UnloadOrder` für Legende und Stapelregel (D41). Am Aufzug kamen rote „which is unloaded first“-Befunde zu einer Reihenfolge, die der Spieler nie festgelegt hat. Martins Vorgabe: Die Reihenfolge entscheidet der Spieler am Terminal; keine Notice darf eine Route unterstellen.
**Präzisiert in der Planung:** Prio 2 = wenigste Ziele im vollsten Rack, dann wenigste geteilte Racks, dann Quadratsumme („wenigste Racks“ entfällt); Prio 3 wählt auch zwischen in Prio 1+2 gleichwertigen Zuordnungen, Schrittbudget = D58-Konstante pro Plan; Tie-Break SCU absteigend, dann Name ordinal, gleicher Schlüssel für Layout und Fallback; Legende und Farben in Eingabereihenfolge; Messfall Hull A = Martins plan.json vom 24.10. mit „Thur“ → Thundering Express Station und „Melg“ → Melodic Fields Station zusammengelegt (4 Ziele, 55 SCU); Item-Schnitt = Warenzeile in > 1 Rack; kein Schema-Bump, Hinweise beim Lesen neu abgeleitet; `FallbackNote` ersatzlos; Säulen-Schranke als Beweis ohne Suche und als Schranke in der Suche; Kapazitätsmeldung ohne Stoppnamen; `TestPlans` der App bleibt beim Planer (Layout byte-identisch). Bei der Freigabe ergänzt (E1–E3): Zahl der `sits on`-Paare vorher/nachher in Commit und D64, „Prio 2 vor Prio 3“ als Entscheidung; D64-Gegenargumente Fragmentierung (→ B3/B4) und Historienabhängigkeit von `PlaceInto`; Knotenzahlen hier festgehalten. Präzisierung in der Planphase, ratifiziert durch Freigabe.
- `RackAssignment` (Core, neu): exakte Suche über Ziele ohne Reihenfolge, Knotenbudget 1.000.000; `CrateLayout`: Regel „nur auf eigener Fracht“ über die Stützkette, `MustStack`, `StackedOn`, `StackingBudget`; Hinweis `Rack N: crate for X sits on cargo for Y`; `UnloadOrder` entfernt
- App: Legende aus `Route`, Rot nur für Blockierendes

**Messbefehle** (vorher/nachher, Release, ein Aufwärmlauf, 3 Läufe, Median per `Measure-Command`, inkl. ~1,2 s Start von `dotnet run`; auch für B3/B4):
- Freelancer-Lastfall: `dotnet run -c Release --project src/CargoPlan.Cli -- plan tests/CargoPlan.Core.Tests/Fixtures/hull_a_eight_stops.json --ship freelancer` → **5.277 → 1.401 ms**
- Hull-A-Fall (Hurston 24.10.): `dotnet run -c Release --project src/CargoPlan.Cli -- plan tests/CargoPlan.Core.Tests/Fixtures/hull_a_hurston_2026-10-24.json` → **1.279 → 1.395 ms** (+0,1 s: Abwägung der Gleichstände und Layout-Suchen; unter einer Sekunde Planung)

**Knoten der Zuordnungssuche (E3, Budget 1.000.000):** example_missions 20, split_and_shared 40, split_required 68, crate_mix 25, hull_a_eight_stops 98, Hurston 24.10. 1.312, Freelancer-Lastfall 67, Hull C 53, C2 50, Stresslast T7.1c 25. Höchstens 0,13 % des Budgets: keine Notiz für ein erschöpftes Knotenbudget.

**Akzeptanz:**
- [x] Ordnungsinvarianz: alle Permutationen (über 6 Ziele: 50 feste) aller Fixtures inkl. Freelancer und uneven_long → gleiche Kisten-Lagen und `StackedOn`-Paare (`OrderFreePlanTests`; vor der Umsetzung 15 von 21 rot)
- [x] Nie fremde Fracht, wenn vermeidbar: Paarung ohne Hinweis gewinnt, erzwungener Fall → Hinweis, `Validate` leer; Exhaustive-Prüfung auf die neue Regel umgestellt, grün (8,5 min statt ~1 min)
- [x] `Validate` meldet für frühere Reihenfolge-Befunde nichts mehr; Rot nur für Überlappung, außerhalb, schwebend
- [x] Sync-Nachlegen: neuer Vertrag landet im Rack des Ziels, wo nichts auf fremder Fracht landet
- [x] Alter plan.json (8-Stopp-Plan vor B2 mit beiden alten Notizen) lädt ohne Replan, Positionen und Haken gleich, Notizen weg, Hinweise neu; Hurston-Plan vom 23.10. (B1-Fixture) grün
- [x] Golden-Files: crate_mix und hull_a_eight_stops neu, example_missions, split_and_shared, split_required byte-identisch; 8-Stopp: Seraphim Rack 4 → 3, Fremdauflage-Paare 4 (Rack 4) → 4 (1 in Rack 3, 3 in Rack 4)
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (Core 878, App 957, Import 118); Slow und Exhaustive grün
- [x] App-Start mit Martins plan.json: keine roten Reihenfolge-Befunde, Positionen und Haken gleich (Screenshot 24.10.)
- [x] Nachtrag nach der Abnahme (Martin): keine Hinweiszeilen unter der Legende, die Farben zeigen Teilen und Stapeln; Core behält die Hinweise für CLI und plan.json (D64-Addendum, D16-Addendum). Präzisierung in der Planphase, ratifiziert durch Freigabe.
- [x] Spieltest am Aufzug, keine roten Reihenfolge-Befunde (Martin, 25.10.)
- Offen: `PlaceInto` und `MoveStop` haben keinen Test mehr, der ihre Suchgrenzen-Notiz erreicht: Mit der Säulen-Schranke erschöpft kein gefundener Fall das Budget (die Stresslast legt in 246 Schritten). Abgedeckt sind das Aufgeben (`Lay` mit 1 Schritt) und das Weitertragen der Notiz.

### [x] B3 Verschieben annehmen statt ablehnen (vier Stufen, D65) (24.10.2026, E3 25.10.)
**Warum:** Am Aufzug kam „Move refused … has nothing below it“, obwohl Platz war: Core prüfte einen Drop nur an genau der Zielzelle. `DropTarget` ließ nur die Säule unter dem Cursor fallen, nicht den Fußabdruck; eine unter einer anderen weggezogene Kiste ließ die obere schweben; `MoveStop` lehnte ein Rack ab, das um seine Fracht herum voll war. Martins Vorgabe: Ist eine Zuordnung möglich, wird der Move angenommen; abgelehnt wird nur, wenn die Fracht nirgends passt.
**Präzisiert in der Planung:** Stufe 3b „Fremde Fracht verdrängen“ vor Stufe 4 (Martins Wahl auf Rückfrage): wenigste SCU des fremden Ziels im Zielrack zuerst, größte Kiste zuerst, eine Ebene (verdrängte Kisten nur um liegende Fracht herum, D41-Reihenfolge, Quellrack normaler Kandidat), sonst Stufe 4 mit Quellrack als letztem Kandidaten; Notices „Moved k crates for X to rack M to make room“ und „No room in rack N – placed in rack M“; Auswahl atomar (Notice nennt nur Kisten ohne Platz), Legenden-Drag teilweise mit „No room in rack N – k crates stay in rack M“, die Asymmetrie in D65 benannt (E1); Stufe 2 nach Grid-Schritten, dann Nähe zur eigenen Fracht, dann Füllrang; Gruppe erst als Block durch 1–3b, dann kistenweise durch 1–4; ohne Zellabsicht (Shift null) ist Stufe 2 das Legen um die liegende Fracht; Vorschau-Schlüssel aus Zielrack, Zelle, Anker und Landezellen inkl. Shift null (E2); Geister für jede Kiste, die den Platz wechselt; `Pending` als cyane Rack-Kante mit „Rack N will be rearranged“; Messung als eigene Kategorie `Timing` (von Hand, nicht in CI, druckt eine Tabelle) statt `Slow`; Pending-Entscheidung nach Martins nativem Release-Lauf, nahe 16 ms → Pending (E3); Screenshot-Fall nachgebaut (4er Pressurized Ice für Green Glade Station auf den 2er von Faithful Dream Station in Rack 2, halb über einer Lücke neben Melodic Fields Station); User-Guide-Abschnitt heißt „Changing the plan“. Präzisierung in der Planphase, ratifiziert durch Freigabe.
- `MoveResolution` (Core, neu): Stufen 1 Gravity, 2 freie Lage, 3 Re-Layout, 3b Verdrängen, 4 Ausweichen, 5 Ablehnung; Quellrack-Gravity; `MoveOutcome`/`MoveStage`; `LoadPlanner.ResolveMove`/`ResolveStopMove` mit `upTo`; `MoveCrates`/`MoveStop` als Wrapper, `MoveProblem` entfernt
- App: `CrateDrop.Outcome`/`StopOutcome`/`KeyOf`/`Changed`/`Text`/`PreviewStage`; Vorschau-Geister, Legenden-Vorschau, Notices ohne Präfix; `CrateDrop.OutsideRacks` entfernt

**Messwerte** (Release, Median aus 3 nach Aufwärmlauf, Entwickler-VM; `dotnet test tests/CargoPlan.Core.Tests -c Release -p:RunSettingsFilePath= --filter Category=Timing --logger "console;verbosity=detailed"`):

| Fall | Gravity | Freie Lage | Re-Layout | Verdrängen | Ausweichen | Legende | Vorschau max |
|---|---|---|---|---|---|---|---|
| Freelancer, 8 Stopps | 0,19 ms | 0,24 ms | 0,62 ms (4 Kisten: 0,56) | 0,31 ms | 0,29 ms | abgelehnt 0,16 ms | 0,22 ms |
| Hull A, Hurston 24.10. | 0,03 ms | 0,03 ms | 0,05 ms (4 Kisten: 0,04) | 0,09 ms (4 Kisten: 0,10) | 0,23 ms | verdrängt 0,19–0,96 ms | 0,03 ms |
| Stresslast + Dock | 2,65 ms | 2,72 ms | 39,6 ms (4 Kisten: 41,0) | – | 4 Kisten: 8,17 ms | verdrängt 158 ms | 6,21 ms |

Entscheidung (Regel): Stufe 1–2 laufen in jedem Frame (überall unter 16 ms). Stufe 3–4 liegen in der VM bei Freelancer und Hull A unter 1 ms, auf dem Stress-Rack bei 40–158 ms. Nach Regel E3 entscheidet Martins nativer Lauf: liegt ein Fall nahe an oder über 16 ms, wird `CrateDrop.PreviewStage` auf `FreeSpot` gesetzt (Vorschau `Pending`), sonst bleibt die volle Vorschau. Martins Wert (nativ, 25.10., Tabelle im Abschluss unten): echte Schiffe höchstens 0,68 ms, nur die Stresslast (kein Schiff) 31–43 ms. **Entscheidung Martin: volle Vorschau, `PreviewStage` bleibt `Refused`; maßgeblich sind echte Schiffe, die Stresslast ist ein Lasttest.** Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Akzeptanz:**
- [x] Je Stufe ein Core-Test, der genau diese Stufe erzwingt (`MoveStage` geprüft), bei Stufe 1–2 bewegt sich keine fremde Kiste; vor der Umsetzung rot (Screenshot-Fall „… Green Glade Station in rack 2 has nothing below it“, weggezogene Kiste, Drop in die Luft, Hurston-Plan „… Everus Harbor in rack 1 has nothing below it“, die übrigen als Compile-Fehler)
- [x] Quellrack-Gravity nach Einzel- und Mehrfach-Drag
- [x] Auswahl: Translation erhalten (Fall, nächste freie Lage), Shift null → Erfolg, Aufteilung über Racks mit „Riker spans racks 2+4“, Teilerfolg ganz abgelehnt
- [x] Legenden-Drag: ganzes Ziel ins Rack, volles Rack → Verdrängung, Ziel größer als das Rack → Rest bleibt mit Notice, keine Kiste passt → abgelehnt
- [x] Einzelkiste nie geteilt: 4er bei 6 freien SCU in Stücken → „no room for 4 SCU crate“
- [x] Vorschau-Äquivalenz: jede Zielzelle jedes Racks für einen 1er und einen 4er, `upTo: FreeSpot` gleich vollem Ergebnis, wo Stufe ≤ 2, sonst `Pending`; Determinismus; Vorschau-Schlüssel (E2): andere Versatzlage bei gleicher Zelle → neuer Schlüssel
- [x] Regressionen: Shift-Hybrid und `MoveCrates_moves_crates_that_carry_each_other_together` grün; acht Core- und drei App-Tests, die Ablehnungen erwarteten, erwarten jetzt den aufgelösten Move; echte Ablehnung im VM-Test mit vollem Hull A
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (Core 902, App 960, Import 119); Slow grün; Timing läuft (25 s)
- [x] Martins nativer Timing-Lauf → `PreviewStage` bleibt volle Vorschau (E3, 25.10., siehe oben)
- [x] Look von Geistern, verdrängten Kisten und `Pending`-Kante (Taste-Gate Martin, abgenommen 24.10.)
- [x] Spieltest am Aufzug (Martin, 25.10.)
- Beobachtung: Eine Kiste, die unter eigener Fracht liegt, verliert beim Ziehen ihr Loch an die nachrutschende Kiste; findet sie dann nirgends Platz, wird abgelehnt (der Plan bleibt, die Kiste also liegen). Hurston-Fall: Green-Glade-4er unter einem eigenen 1er auf ein Rack voller Green Glade.
### [x] B4 Knopf „Re-plan“ im Editor (24.10.2026)
**Warum:** Zwei Aufträge geladen, ein dritter passt kapazitiv, aber nicht mehr in die gewachsene Anordnung (Fragmentierung, D64-Gegenargument): `PlaceInto` legt ihn als wartend ab, eine Mission ohne Kisten lässt sich nicht ziehen. Martins Vorgabe: Der Spieler entscheidet aktiv, alles neu sortieren zu lassen, und nimmt das Umladen in Kauf; die B3-Stufen bleiben konservativ, Re-plan ist die bewusste, destruktive Aktion.

**Messwerte vor der Umsetzung (E1)** – Planzeit ganzer Prozesse, Median aus 5 nach Aufwärmlauf, Entwickler-VM; zwei Messwege mit verschiedener Konfiguration:

| Fall | B2: `dotnet run -c Release` (inkl. Build-Check, ~1,2 s Start) | Release-Apphost `CargoPlan.Cli.exe` direkt (Build 24.10. 06:57, Stand B2) |
|---|---|---|
| `--version` (nur Start) | – | 38 ms |
| example_missions | – | 144 ms |
| split_and_shared | – | 149 ms |
| split_required | – | 147 ms |
| crate_mix | – | 147 ms |
| Hull A acht Stopps | – | 168 ms |
| Hull A Hurston 24.10. | 1.395 ms | 160 ms |
| Freelancer acht Stopps | 1.401 ms | 159 ms |

Der Unterschied ist der Start von `dotnet run` (MSBuild-Prüfung, Host), nicht das Planen; der Apphost enthält noch Start, JIT und das Laden der 96 Schiffe. Verbindlich für die Entscheidung ist `ReplanTimingTests` (im Prozess, Release, Median aus 3 nach Aufwärmlauf); Gate: liegt ein realistischer Fall (alle Fixtures außer Stresslast) über ~100 ms → Stopp, `Task.Run`-Variante vorlegen.

**`ReplanTimingTests`** (verbindlich; Release, im Prozess, `dotnet test tests/CargoPlan.Core.Tests -c Release -p:RunSettingsFilePath= --filter Category=Timing --logger "console;verbosity=detailed"`; erster Lauf nach dem Plan, von dem er ausgeht, dann Median aus 3):

| Fall | erster Lauf | Median |
|---|---|---|
| example_missions | 7,0 ms | 0,4 ms |
| split_and_shared | 1,0 ms | 0,7 ms |
| split_required | 0,3 ms | 0,3 ms |
| crate_mix | 0,4 ms | 0,4 ms |
| Hull A acht Stopps | 6,3 ms | 3,2 ms |
| Hull A Hurston 24.10. | 2,5 ms | 2,1 ms |
| Freelancer acht Stopps | 2,3 ms | 2,3 ms |
| Stresslast 12×12×7 (kein Schiff) | 186 ms | 80,2 ms |

Entscheidung (Regel): Kein realistischer Fall kommt über 7 ms, also läuft Re-plan synchron auf dem UI-Thread, ohne Sperre und ohne Versionszähler. Die Backlog-Zeile „Planen in `Task.Run`“ ist damit geschlossen.

**Präzisiert in der Planung:** `Replan` plante abgehakte Stopps mit (sie belegten Platz) und verlor Teil-Haken (D41). Deshalb dispatcht es jetzt auf `ReplanInPlace` (eigenes Schiff, neu: abgehakte Kisten bleiben liegen und belegen nichts) und `ReplanForShip` (D59, unverändert). Keine API-Änderung (E3). Kaskade: alle Missionen; sonst die Fracht an Bord neu und die Wartenden danach (`RetryUnplaced`); sonst bleibt die Anordnung. N zählt nur Kisten, die vorher an Bord waren (Multimenge, Haken sind kein Move). `Plan unchanged` heißt: nichts bewegt und nichts neu platziert. Bleibt eine Mission wartend, hängt „– X still doesn't fit“ an (E2). Füllrichtung kommt aus dem Plan, nicht aus dem Editor-Entwurf. Notice-Quelle ist `Move` (verschwindet beim nächsten Move, Haken oder Plan). Alle Hotkeys prüfen `CanExecute` vor `Execute`: Die Toolkit-Kommandos tun das nicht selbst, ein Button-Binding schon. Bei der Freigabe ergänzt: E1 (Messtabelle mit Build-Konfiguration vor der Umsetzung, `ReplanTimingTests` verbindlich), E2, E3. Nach der ersten Umsetzung (Martin): Re-plan zusätzlich als Header-Knopf ↻ (E72C Refresh) zwischen Merge und Stift; jede Ausführung fragt vorher („niemals so“); alle Bestätigungen erscheinen mittig im aktuellen Fenster statt mittig auf dem Monitor. Das ist ein eigener `ConfirmDialog` (Fluent, topmost, `CenterOwner`), auch für den Schiffswechsel; vorausgewählt ist No bei Re-plan, Yes beim Schiffswechsel. Gegen einen CBT-Hook, der die MessageBox verschiebt, spricht „keine Hooks“. Präzisierung in der Planphase, ratifiziert durch Freigabe.
- Core: `LoadPlanner.Replan` → `ReplanInPlace`/`ReplanForShip`, neu `LoadPlanner.MovedCrates`
- App: `OverlayViewModel.ReplanCommand` mit `ConfirmReplan` (Standardantwort Nein), Header-Knopf ↻ mit Tooltip, `ConfirmDialog` (neu, Besitzer = aktives Fenster, sonst Overlay; auch Schiffswechsel), Editor-Knopf „Re-plan“ zwischen „Import…“ und „Sync from ContractTracker“ als eigene Änderung (Entwurf bleibt Entwurf), Hotkey `replan` (`HotkeyAction`, `HotkeySettings`, Settings-Fenster), `CanExecute`-Prüfung in `HotkeyService`

**Akzeptanz:**
- [x] `Replan` ≡ frischer `Plan`: sechs Fixtures, nach `PlaceInto` gewachsen, Kisten und Hinweise gleich; Füllrichtung und Watermark bleiben (`ReplanInPlaceTests`)
- [x] Haken bleiben: abgehakter Stopp mit Rack und Lage unverändert, der Rest wie ein frischer Plan ohne ihn; Teil-Haken (D41) bleibt; abgehakte Zellen nimmt eine wartende Mission (Hull A voll + Shubin abgehakt → Orison platziert)
- [x] Übersteuerungen: geteilte Kiste (T3.15) und getippte Kistenliste (T3.12, crate_mix) behalten ihre Größen
- [x] Notice-Zählung: `MovedCrates` 1 bei einem Move, 0 ohne; Haken und neue Kisten zählen nicht; kanonischer Plan → 0 und dieselbe Missionsliste; Texte (Singular/Plural, Suffix für Wartende) als Theorie
- [x] Szenario Hull A: zwei Aufträge mit je zwei Zielen à 8 SCU (ein Ziel pro Rack), dritter 16 SCU → „doesn't fit (no room for 16 SCU crates)“, `RetryUnplaced` ändert nichts; nach Re-plan alles platziert, `Validate` leer
- [x] Kaskade: wartende Mission ohne Platz → Rest neu gelegt, sie wartet weiter, Notice „… – Orison still doesn't fit“
- [x] Sync nach Re-plan: dieselben Captures → keine Mission neu oder weg, Kisten gleich, keine Konflikte
- [x] Knopf-Zustand: ohne Plan aus, alles abgehakt aus (mit `CanExecuteChanged`); Editor folgt dem Overlay; Entwurf bleibt dirty ohne „changed outside“, sauberer Editor zeigt die Zeilen neu
- [x] Hotkey: Aufnahme, Clear = `null`, Sync-Kombination abgelehnt, Session speichert `replan`, Round-Trip in settings.json
- [x] Vor der Umsetzung rot: Core 8 von 18 (Haken, Missionsliste, `MovedCrates`), App als Compile-Fehler; das Szenario war schon grün (das alte `Replan` plante auf dem eigenen Schiff frisch) und bleibt Akzeptanztest
- [x] App-Lauf (UI Automation, AppData gesichert und wiederhergestellt, Auto-Sync für den Lauf aus): Szenario geladen → Legende „Everus Harbor · 16 SCU · ⚠ doesn't fit (no room for 16 SCU crates)“; Re-plan im Editor → „Plan rebuilt – 4 crates moved“, Warnung weg, plan.json ohne Wartende; Hotkey Ctrl+Alt+F9 → „Plan unchanged“
- [x] Header-Knopf ↻ zwischen Merge und Stift, gesperrt mit Tooltip „Re-plan: no cargo on board“
- [x] Bestätigung: Header, Editor und Hotkey fragen; No ändert nichts und schreibt keine Notice; ohne Fenster antwortet das ViewModel Nein (Tests); im App-Lauf ist No fokussiert
- [x] Dialog mittig im aktuellen Fenster (UIA, AppData je Lauf frisch gesichert und per Hash zurückgespielt): Header 0/0 px zum Overlay-Mittelpunkt, Editor 0/0 px zum Editor (der an den Monitorrand geschoben war), Hotkey 0/0 px zum Overlay; Screenshots zeigen den Dialog deckend über dem Fenster
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (Core 919, App 983, Import 119); Slow grün; Timing läuft
- [x] Look und Platz von Header-Knopf und Dialog (Taste-Gate Martin, abgenommen 24.10.)
- [x] Spieltest am Aufzug inkl. Re-plan mit wartendem Auftrag (Martin, 25.10.)
- Nicht Teil der Abschluss-Checkliste, daher nicht belegt: ob der Hotkey-Dialog nach vorn kommt, wenn Star Citizen den Fokus hat, und ob der Schiffswechsel-Dialog mittig im Overlay steht (nicht per UIA geprüft).

### [x] B5 Kapazitätsanzeige (Kopfzeile, Rack-Tooltip) (24.10.2026)
**Warum:** Am Aufzug war weder die Maximal- noch die Restkapazität sichtbar, fürs Schiff nicht und pro Rack nicht; nur die Legende zeigte SCU je Ziel. Martins Vorgabe: beides im Overlay, Schiff und Rack.
**Präzisiert in der Planung:** Kopfzeile immer im kompakten Format `56/64 · 8 free` bzw. `56/64 · 8 free · 16 waiting`, kein Formatwechsel (ersetzt `56 / 64 SCU · 8 free` aus dem Auftrag); reicht die Breite nicht, kürzt der Schiffsname mit Ellipse, die Kapazität nie; über einer Kiste deren Tooltip plus Rack-Zeile, über dem übrigen Rack nur die Rack-Zeile; `waiting` zählt SCU; ohne Plan `0/64 · 64 free`; `LoadPlan.ScuOnBoard` in Core (D24-Regel an einer Stelle, auch für `PlaceProblem`); Lock-Prüfung als Test über die XAML des Overlays (keine Fenster-Tests im Projekt). Bei der Freigabe ergänzt (E1): Gate zusätzlich am Schiff mit dem kleinsten Rack-Grid, Befund an Martin, kein eigener Fix. Im Gate gefunden und behoben: Der Schiffsname brach in zwei Zeilen um („MISC“ über „Hull A“) statt zu kürzen, weil der Header-Stil umbricht; beide Namensfelder jetzt `NoWrap`. Nach dem Gate (Martin): nur Racknummern, die Zahl `3 · 12/16` dahinter ist zu viel; sie fliegt samt Schalter `crates.showRackLoad` ganz raus (Martins Wahl auf Rückfrage, statt Default aus), settings.json bleibt unverändert. Präzisierung in der Planphase, ratifiziert durch Freigabe.
- Core: `LoadPlan.ScuOnBoard(rackId?)`
- App: `ShipScene.WaitingScu`/`CapacityText`, `RackLoad` (Tooltip) an `SceneRack` und `ProjectedRack`, `SceneHitTest.ToolTipAt` übers ganze Rack, Kopfzeile `CapacityText` ohne `IsLocked`-Trigger, Schiffsname `NoWrap`

**Gate-Messwerte der entfernten Zahl hinter der Racknummer** (Grundlage für Martins Entscheidung; echtes Overlay offscreen, Default-Look, Zoom 1, 420 × 460 px, Entwickler-VM; Harness im Scratchpad, nicht im Repo):

| Fall | Label | Breite Label | Breite Rack | Verhältnis | Überlappung |
|---|---|---|---|---|---|
| Hull A, Hurston 24.10. (27 px Nummer, 27,2 px/Zelle) | `1 · 16/16` | 111,8 px | 141,1 px | 0,79× | keine |
| Syulen (6 Racks à 1 Zelle, Nummern über den Racks) | `1 · 1/1` | 80,8 px | 81,3 px | 0,99× | keine |
| Aurora Mk II / Mule / Reliant Tana / UTV (1 Zelle) | `1 · 1/1` | 80,8 px | 279–323 px | 0,25–0,29× | keine |

Kleinstes Rack-Grid: fünf Schiffe mit 1 Zelle gleichauf, alle gemessen. Kopfzeile: `55/64 · 9 free` 64 px, mit `· 16 waiting` 129 px. Vollständig sichtbar ab 310 px Overlay-Breite, mit `waiting` ab 370 px; bei der Mindestbreite 240 px bleiben links der sechs Header-Knöpfe ~10 px, die Kapazität wird unter ihnen abgeschnitten (Befund an Martin). Im Lock-Modus rückt die Kapazität 16 px nach links (der Name verliert den Chevron).

**Akzeptanz:**
- [x] Kapazitätswerte aus dem Plan: Max, geladen, frei, wartend; abgehakte Kisten zählen nicht; nach Haken, Move und Re-plan aktuell (`ShipViewBuilderTests`, `OverlayViewModelTests`, `ReplanCommandTests`: Fragmentierung `32/64 · 32 free · 16 waiting` → nach Re-plan `48/64 · 16 free`)
- [x] Rack-Tooltip `Rack 3 · 14 / 16 SCU`, auch bei leerem Rack und über freien Zellen; über einer Kiste als zweite Zeile (`SceneHitTestTests`)
- [x] Racknummer zeigt nur die Nummer (Gate-Bild Hull A nach dem Rückbau); kein `showRackLoad` mehr in Code, Settings-Fenster und settings-format.md
- [x] Lock-Modus: kein `IsLocked`-Trigger am Kapazitätstext und an seinen Vorfahren, auch nicht über `{StaticResource}`-Stile (`OverlayHeaderXamlTests`); im Gate-Bild gesperrt sichtbar
- [x] Name kürzt statt umzubrechen, Kapazität nie gekürzt (XAML-Test, Gate-Bilder 420 px mit `waiting`)
- [x] Vor der Umsetzung rot (Compile-Fehler); der `NoWrap`-Test kam mit dem Fix aus dem Gate
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (Core 920, App 997, Import 119); Slow grün
- [x] Look von Kopfzeile und Tooltip; Umgang mit Overlays unter 310 px (Taste-Gate Martin, abgenommen 24.10.)
- [x] Spieltest am Aufzug, Kapazität in der Kopfzeile (Martin, 25.10.)

### [x] B6 Legende nach Mission, From/To, Auswahl-Checkbox (D69) (25.10.2026)
**Warum:** Screenshot mit Martins plan.json vom 25.10.: Die Legende aus T7.4d (D68) gruppierte nach Abholort-Liste; vier Zeilen ohne Überschrift, Rohtext als Überschrift, kein „To“, Checkbox an der Überschrift statt an den Zeilen, ein Ziel eine Zeile je Liste mit einem Haken pro Stop. Kisten kommen pro Mission hoch, der Spieler lädt und hakt pro Mission ab.

**Befund:**

| # | Screenshot | Ursache |
|---|---|---|
| 1 | Vier Zeilen ohne Überschrift | Missionen 1–3 (ct 1–4) vor T7.4d importiert, ohne `pickup`; `LegendLines` legte sie unter eine Überschrift `""` |
| 2 | Rohtext als Überschrift | ContractTracker selbst speicherte in `PickupLocation` von ct 38 „Everus Harbor. - Freight elevator at Green Glade Station at Hurston's L1 Lagrange point“ (DB read-only, `immutable=1`); D68 übernahm es wörtlich |
| 3 | „Everus Harbor · 8 SCU“ als Ziel | Kein Fehler: ct 4 = Waste nach Everus Harbor (8 SCU, max 1 SCU, Größe aus dem Text), Muster aller Waste-Fixtures; Rohtext nicht prüfbar (ct 1–4 in CT gelöscht, `.bak` leer), Lesart von Martin akzeptiert |
| 4 | Kein „To“, Checkbox an der Überschrift | So gebaut in T7.4d (Checkbox nur an `From`-Zeilen bei > 1 Mission) |
| 5 | „Stims · to High Course Station“ unter der Kopfzeile | Die Infozeile aus T7.4d bei einer markierten Kiste; bleibt, jetzt mit `from ?` |

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Überschriften nur, wenn irgendeine Mission einen Abholort hat (sonst flach, Zeilen trotzdem `To` und je Mission + Ziel); Kopf-Checkbox wählt die ganze Mission; zwei Abhak-Einheiten mit zwei Zwecken (Kistenklick in der Ansicht = ganzer Stop, Legendenzeile = Mission + Ziel); Abholort-Bereinigung beim Import und einmalig beim Laden (Notice `Pickups cleaned: …`, nur CT-Missionen); Regel „Herkunft nie als Ziel“ und Test „ct 4: kein Stop Everus Harbor“ entfallen (Befund 3); Missionen nach erstem Auftreten entlang der Route, bei Gleichstand Missionsreihenfolge; Legenden-Drag atomar wie eine Auswahl, die Teilverschiebung (D65/E1) fällt weg; D69 statt D68 (vergeben); Infozeile bleibt klein `from <Ort> · <Ware> · to <Ziel>`, ohne Abholort `from ?`; E1: Klick auf eine teilweise Checkbox wählt alles (`null → true → false`); E2: Stufe 3b bleibt im Gruppenlauf; Branch `fix/plan-rules` neu ab main (B1–B5 waren gemergt).
- Core: `LoadPlanner.SetDelivered` mit Mission, `LoadPlanner.ResolveMoveToRack` (Kisten ohne Zelle durch `MoveResolution.Crates`); `MoveStop`, `ResolveStopMove`, `MoveResolution.Stop` entfernt
- Import: `ContractText.NormalizePickup` (Listenstrich, Überschriften, dann `NormalizeDestination`), `PickupNames.Parse`/`Clean`
- App: `ShipViewBuilder.LegendLines` je Mission + Ziel, `LegendEntry` mit `Mission`, `Crates`, `MissionCrates`, `Grouped`, Label `To …`; `ToggleLineCommand`, `ToggleCratesCommand`, `MoveLegendCommand`; `SelectionFocus.State`, `LegendCheckedConverter` (ersetzt `PickupCheckedConverter`), `MissionPalette.CheckBoxBrush` (grau); `Gesture.MoveLegend`, `StartLegendDrag`; Notice-Quelle `Pickups`
- Fixture: Martins plan.json vom 25.10. (`hurston_2026-10-25`, Import, in die App-Tests verlinkt)

**Akzeptanz:**
- [x] Jede Mission eine Überschrift, auch ohne Abholort (`From ?`), nie Rohtext; Martins Plan: Missionen 1, 2, 4, 3 mit `From ?`, `From ?`, `From Everus Harbor`, `From ?` (`LegendByMissionTests`)
- [x] ct 4: Waste-Zeile `To Everus Harbor · 8 SCU` unter `From ?` (ersetzt den Test „kein Stop Everus Harbor“); CT-Pickup von ct 38 → `Everus Harbor`, Laden bereinigt einmal mit Notice, zweiter Lauf ohne (`PickupNamesTests`, `ContractTrackerReaderTests`)
- [x] Zeilen mit `To`, Summe je Mission + Ziel (High Course Station 7 und 5), Farbe je Ziel über Gruppen gleich, Reihenfolge nach erstem Auftreten, ohne jeden Abholort keine Überschrift
- [x] Checkbox: setzen/aufheben, Vereinigung, Kopf = ganze Mission, Spiegelung (an/aus/teilweise), E1-Zyklus, abgehakte Kisten nie in der Auswahl; Checkbox-Kommando hakt nicht ab, Zeilenklick hakt nur Mission + Ziel ab, Kistenklick weiter den ganzen Stop
- [x] Legenden-Drag Zeile/Überschrift → `Gesture.MoveLegend` mit genau diesen Kisten; volles Zielrack → Verdrängung (E2); nirgends Platz → nichts bewegt (Core)
- [x] Infozeile `from ? · …` ohne Abholort; Tooltip unverändert (`SceneHitTestTests` grün)
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (Core 996, Import 141, App 1.130); Slow grün
- Rot vor der Umsetzung: Commit 1 als Compile-Fehler in allen drei Testprojekten; in Commit 2 nur die umgezogenen Core-Tests, die App-Tests für Checkbox und Drag kamen nach dem Code (Lehre „erst rot sehen“ nicht ganz eingehalten).
- Nach dem ersten Blick (Martin): Checkboxen grau statt Magenta (`#B0B0B0`, Erledigt-Text `#8C8C8C`, abgehakte Zeilen ohne Kästchen). Präzisierung in der Planphase, ratifiziert durch Freigabe.
- [x] Look der grauen Checkbox und Einrückung der Zeilen (Taste-Gate Martin, abgenommen 25.10.)
- [x] Spieltest am Aufzug, Legende nach Mission mit Checkboxen, Infozeile from/to (Martin, 25.10.)
- Commits 1fec5d6 (Legende nach Mission, Bereinigung), d3422f9 (Checkbox, Drag), f227851 (Doku), 9de6d83 (Checkbox grau).

### Abschluss der Bugfix-Session (25.10.2026)
**Messwerte, eine Konfiguration:** Martins Maschine (nativ, keine VM), Release, im Prozess, ein Lauf von `dotnet test tests/CargoPlan.Core.Tests -c Release -p:RunSettingsFilePath= --filter Category=Timing --logger "console;verbosity=detailed"` auf dem Stand von B6 (9de6d83); Median aus 3 nach Aufwärmlauf. Planen (B2) = Re-plan ohne Haken, das ist ein frischer `Plan` derselben Missionen (D66); „erster“ = erster Lauf nach dem Plan, von dem er ausgeht. Die B2-Prozesszeiten oben (`dotnet run`, inkl. ~1,2 s Start) und die VM-Werte von B3/B4 bleiben als Historie stehen; sie sind ein anderer Messweg.

| Fall | Planen/Re-plan erster | Median | Gravity | Freie Lage | Re-Layout | Verdrängen | Ausweichen | Legende | Vorschau max |
|---|---|---|---|---|---|---|---|---|---|
| example_missions | 11,5 ms | 0,5 ms | | | | | | | |
| split_and_shared | 1,2 ms | 1,0 ms | | | | | | | |
| split_required | 5,5 ms | 0,3 ms | | | | | | | |
| crate_mix | 0,5 ms | 0,5 ms | | | | | | | |
| Hull A acht Stopps | 3,5 ms | 3,6 ms | | | | | | | |
| Hull A Hurston 24.10. | 2,7 ms | 2,9 ms | 0,03 ms | 0,03 ms | 0,05 ms (4 Kisten: 0,04) | 0,09 ms (4 Kisten: 0,11) | 0,23 ms | 0,14–0,61 ms | 0,03 ms |
| Freelancer acht Stopps | 2,6 ms | 2,7 ms | 0,19 ms | 0,23 ms | 0,68 ms (4 Kisten: 0,60) | 0,33 ms | 0,30 ms | 0,27–0,28 ms | 0,23 ms |
| Stresslast (kein Schiff) | 264,9 ms | 95,3 ms | 2,86 ms | 2,87 ms | 39,4 ms (4 Kisten: 43,5) | – | 4 Kisten: 7,97 ms | 31,4 ms | 6,31 ms |

Legende = Legenden-Drag seit B6 (Zeile auf ein Rack, atomar, `ResolveMoveToRack`); die Spalte „Planen“ der Stresslast misst den Stress-Plan ohne Dock, die Gesten den mit Dock. E3: echte Schiffe höchstens 0,68 ms, volle Vorschau bleibt (B3).

- [x] TASKS B1–B6 abgehakt mit Datum, Messtabelle in einer Konfiguration, Backlog-Zeilen (Spieler-Reihenfolge, Undo für Drags/Re-plan, B1-Restfall Capture 21–25, Waste-Abholorte) vorhanden
- [x] README und User Guide gegen D64–D69 gelesen: kein „unloaded first“, keine Ablehnung bei belegter Zelle; „nothing moves“ nur für „passt nirgends“ (D65). README: zwei Abhak-Einheiten, Checkboxen, Kapazität in der Kopfzeile ergänzt. `crates.showRackLoad` steht nirgends mehr (Doku, Settings-Fenster, Code, Tests)
- [x] Veralteter Assert `PlaceIntoTests` („which is unloaded first“, grün nur, weil `Validate` leer war) → `BeEmpty()`
- [x] Spieltest mit echter Covalex-Route (Martin, 25.10.); er lief vor dem Publish-Lauf unten, der Publish-Build selbst war nicht im Spiel
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (Core 996, Import 141, App 1.130); Slow grün; Exhaustive grün (8 min 18 s)
- [x] `build/publish.ps1`: Ordner mit 358 Dateien inkl. `ships/` und `outlines/`, ZIP 61,9 MB mit SHA256
- CLAUDE.md: Regel „Session-Branches mit `--no-ff` mergen“.

---

## Backlog-Session (vor dem Release)

Fünf Sessions aus dem Backlog vor dem Tag, Reihenfolge **K3 → K2 → K1 → K4 → K5 → T7.5 → Tag v0.1.0**. Je Session ein Branch ab
main, Merge `--no-ff`. Die übernommenen Backlog-Zeilen sind unten durchgestrichen und verweisen hierher.

| Session | Inhalt |
|---|---|
| K3 Planer-Regeln | Türlimit pro Rack, Kisten-Gier 90° |
| K2 Plan/Editor | Discard plan (Sync-Baseline zurück), Undo/Redo-Stack, `fillOrder` in Missions-JSON Schema 3 (Import/Export), Haken-Erhalt beim Schiffswechsel |
| K1 Schiffsansicht | Zoom-Grenzen absolut in px/Zelle + Preference, Kamera/Zoom/Pan persistieren, Reset-Symbol in der Kopfzeile, Hit-Test auf die Ansichtsfläche |
| K4 Look | Checkbox- und Chevron-Farbe/Deckkraft, Stop-Paletten-Presets, Look-Presets + Export/Import |
| K5 Daten | `offset`/`rotation` aus Fleetyards → `origin`/`stackAxis` im Import; Achsen an der Hull A kalibrieren; offen: innerer/äußerer Hull-C-Block ein Gitter oder zwei. Fortune und Moth (Gitter geändert, neue Racks), Salvation (neu) aus dem Index vom 25.10. Aurora-Frachtmodul, App-Icon |

Reihenfolge innerhalb von K3: K3.1 → K3.3 → K3.2 (Nachtrag 25.10.: `limits.min.capacity` kam dazu).

Aus dem Index vom 25.10. für K5 festgehalten: `cargoHolds[].offset`/`rotation` liegen für Hull A, Hull C, Ironclad Assault u. a. vor (Hull C: zwei Kreuze bei `y ∈ {0, 11.25}`, je vier Arme aus innen 10×10×7,5 m = 8×8×6 Zellen = 384 SCU und außen 5×10×7,5 m = 4×8×6 = 192 SCU; die vertikalen Arme tragen `rotation: 90`).

### [ ] K3.1 Türlimit pro Rack (D70) (25.10.2026)
**Warum:** Spielfakt (Fleetyards-Betreiber, 23.10.): Manche Laderäume haben eine Tür, die kleiner ist als das Gitter. Clipper: Gitter 3×2×2 nimmt eine 8, durch die Tür passen nur Kisten bis 2 SCU.

**Ist-Zustand vor K3.1 (A1):** `maxCrateSize` pro Rack gab es seit D56 (`Rack.CanHold` in Layout, Moves, Merge, `PlaceInto`), aus Fleetyards `maxContainerSize`, in 284/284 Racks gleich dem Gitter (D57), also wirkungslos. Plan-Schema war schon 4 (D68), K3.2 hebt es auf 5. `Partition` heißt seit B2 `RackAssignment`.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 `maxCrateSize` ist das Türlimit, redundante Werte raus aus `ships/`; P2 Schiffs-Schema 4 → 5 als Marker für den Bedeutungswechsel (Schema 4 wird gleich gelesen); P3 Kappung der abgeleiteten Mischung durch Tür **und** Gitter (größte Kiste, die irgendein Rack nimmt); P4 im Import-Skript kein TODO (Nachtrag 25.10.: das Feld ist `limits.max.capacity`, `$DoorLimitField` entfiel); P6 eine 1 auf einem Gitter, das mehr nimmt, bleibt ungeprüft und wird nicht geschrieben (Martin: ein fehlendes Limit kostet eine Kiste, ein falsches sperrt das Rack); P5 Drag auf eine zu kleine Tür bleibt bei D65 (Stufe 4 mit Grund statt Ablehnung); K1 `SavedPlan.CrateCap` ohne Default, Tests ohne Schiff nehmen `CrateCap.None`; K2 jeder Text aus `CrateCap.Text`, Reason-Regel eigens getestet; K3 Kiosk-Gate. Regel 3 („weiß das Modell, ob abgeleitet?“): ja, `MissionItem.Crates == null`, kein neues Flag. Datenschritt vor dem Planer-Commit, damit seine Gleichheit allein gemessen wird. Eine Ablehnung „(door limit …)“ wird nicht gebaut: Eine gezogene Kiste passt immer zurück in ihr Rack.
- Core: `CrateCap` (`Of(Ship)`, `Of(Rack)`, `Text`, `Notice`, `Limit`), `CapReason` (`None`, `Door`, `LargestRack`, `Saved`), `Rack.DoorRefuses`; `MissionItem.CrateListUnder(cap)`, `CappedBy`, `ToCargoItems(cap)`, `WithCrateList(crates, cap)` (ersetzt `CrateList`); `MissionStop.ToStop(cap)`/`ToStops(cap)`; `SavedPlan(…, CrateCap, …)`, `CratesCapped`, `FromStops(…, cap)`; `LoadPlanner`: Neuplanung nimmt `CrateCap.Of(ship)`, Arbeit am Plan `saved.CrateCap`, `Replan` in place nur bei gleicher Kappung, `PlaceProblem` „no rack accepts …“; `MoveResolution` überspringt ein Zielrack mit zu kleiner Tür; `PlanFileDto.UnderCap` (Rückfall auf die größte passende Kappung); `ShipLoader` liest 4 und 5
- Daten/Import: 283 redundante `maxCrateSize` entfernt, alle Schiffe Schema 5; `import-fleetyards.ps1` liest `limits.max.capacity` (über 32 auf 32 geklemmt, nur unter der Gitterkiste geschrieben, eine 1 ungeprüft), `maxContainerSize` gar nicht mehr, Walkway-Reihenfolge aus dem wirksamen Maximum, `-Force` ergänzt fehlende Türen und überschreibt keine; aus dem Index vom 25.10. (`fetch.ps1`, 118 Schiffe, 97 mit Gitter, 293 Holds alle mit `limits`) tragen 14 Schiffe eine Tür: Clipper, 300i, 315p, Apollo Medivac/Triage, Cutlass Black, Idris P, Reclaimer, Shiv (2), Nomad (8), 400i, Idris M, Starfarer, Starfarer Gemini (16), `notes` nennen `limits.max.capacity`
- App: `NoticeSource.CrateCap` aus `SavedPlan.CratesCapped`, `ShowPlan` plant Dateien unter fremder Kappung neu (`RacksReplannedText`), Editor-Spalte Crates gekappt und folgt dem Schiffswechsel
- Tests: `CrateCapTests`, `DoorLimitTests`, `DoorLimitOverlayTests`, `MaxCrateSizeTests` (Verdrängen nie hinter eine zu kleine Tür, mit Gegenprobe), Schiffs-Schema 4/5/6, `A_shipped_max_crate_size_is_below_what_the_grid_takes`, `A_shipped_file_is_schema_5`; `Uncapped` (Core- und App-Tests)

**Zahl vorher/nachher:**

| Messung | Vorher | Nachher |
|---|---|---|
| Tests (Core / Import / App) | 996 / 141 / 1.130 | 1.210 / 141 / 1.134 |
| Golden-Files und Fixtures geändert | erwartet 0 | 0 |
| 4 Fixtures × 96 Schiffe, Plan-Datei-Hash, nur Datenbereinigung (alter Code) | 384 | 381 gleich, 3 Clipper (vorher und nachher abgelehnt, jetzt mit Tür) |
| dieselben 384 Fälle mit Kappung | 384 | jeder vorher geplante Fall byte-gleich; 149 abgelehnte bleiben abgelehnt mit anderem Grund (meist Kapazität); 2 jetzt geplant (600i Executive/Explorer, crate_mix) |
| dieselben 384 Fälle nach den Türen aus dem Index vom 25.10. | 384 | 370 gleich; 14 geändert, alle auf Schiffen mit neuer Tür; 4 davon jetzt abgelehnt (`example_missions` ist Schema 1, also gesetzte 4er, die eine Tür 2 nicht nimmt) |
| `ReplanTimingTests` Median (Release, diese Maschine) | example 0,5 · split_and_shared 1,1 · split_required 0,6 · crate_mix 0,5 · Hull A acht Stopps 4,4 · Hurston 3,1 · Freelancer 3,1 · Stresslast 238,3 ms | 0,6 · 0,8 · 0,4 · 0,6 · 5,6 · 2,9 · 3,0 · 103,2 ms (im Rauschen; die Stresslast schwankt zwischen Läufen um mehr als das Doppelte) |

**Ungeprüfte Türen** (`limits.max.capacity = 1` auf einem Gitter, das mehr nimmt; nicht geschrieben, Gitterkiste in Klammern): 100i rack-0 (2) · 125a rack-0 (2) · 135c rack-0 (2) · Cutlass Black rack-1 (2) · Cutlass Blue rack-0, -1 (2) · Cutlass Red rack-0, -1 (2) · F7C Hornet Mk II rack-0 (2) · Golem OX rack-0 (32) · Hermes rack-0, -1 (32) · Idris M rack-3, -4 (2) · Idris P rack-7, -8 (2) · Intrepid rack-0, -1 (2) · Ironclad rack-0 bis -3 (32), rack-4 bis -8 (8) · Ironclad Assault rack-0, -1 (32) · Paladin rack-0 (2) · Prowler Utility rack-0, -1 (16). 32 Racks in 16 Schiffen, dazu Fortune und Salvation (K5). Frage an Torlek (Fleetyards): „Ist `limits.max = 1` ein Default oder eingetragen – und kommt `limits` aus Starbreaker oder von Hand?“ Die Antwort entscheidet die Liste auf einmal.

Vier Tests nutzten einen 32-SCU-Vertrag auf der Hull A als „passt nicht“; der wird jetzt zu 16ern gekappt und passt, sie nehmen 72 SCU.

**Akzeptanz:**
- [x] Clipper (`ships/clipper.json`, Gitter 3×2×2, Tür 2): 8-SCU-Vertrag → vier 2er, Notice `crates capped to 2 SCU (door limit)` (`DoorLimitTests`, `DoorLimitOverlayTests`, CLI `plan … --ship clipper`: `8 SCU Titanium (4×2)`, Hull A `(1×8)`)
- [x] Gesetzte 4er → `Plan` lehnt ab mit `(door limit 2 SCU)`, `PlaceInto` lässt warten, `PlaceProblem` = `doesn't fit (no rack accepts 4 SCU crates, door limit 2 SCU)`
- [x] Schiffswechsel leitet abgeleitete Mischungen neu ab, gesetzte bleiben; Split/Merge zurück lässt die Mischung abgeleitet
- [x] Drag auf ein Rack mit zu kleiner Tür: Stufe 4 mit `Rack 1: door limit 16 SCU – placed in rack 2`, Vorschau zeigt es (P5)
- [x] Verdrängen (3b) landet nie hinter einer Tür, die die Kiste abweist (mit Gegenprobe ohne Tür)
- [x] Migration Schiffs-Schema 4 → 5 ohne Feld und mit Feld; Schema 6 abgelehnt
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (Core 1.213, Import 141, App 1.134); Slow grün; Exhaustive grün (13 min 10 s, anfangs parallel zu anderen Tests)
- [ ] Antwort von Torlek zur 1 → Liste der ungeprüften Türen übernehmen oder streichen
- [ ] Gate Spiel-Build (Martin): Clipper laden, CT-Sync mit einem Vertrag über 2 SCU Kistengröße, Notice und Legende prüfen
- [ ] Gate Kiosk (Martin, K3): am Terminal bestätigen, dass der Kiosk bei einem Vertrag mit Maximum 8 auch 2er anbietet; widerlegt → Kappung nur bei Türlimit, P3 zurück (Folge-Commit), Ergebnis in D70
- Commits f3c4e63 (Daten, Import), f815210 (Core-Tests rot), 69ee7da (Core), 538b6c9 (App-Tests rot), 72a6115 (App), 25ac668 (Import aus `limits.max.capacity`), 84b2cc3 (Türen aus dem Index vom 25.10.), Doku-Commit.

### [ ] K3.3 Untergrenze `minCrateSize` pro Rack (D70-Addendum) (25.10.2026)
**Warum:** Fleetyards gibt neben `limits.max` auch `limits.min.capacity`; > 1 nur bei Hull C (32, die Spindel nimmt nur 32er), MPUV Tractor (16), CSV-SM (4). Ihre Gitter nehmen die Mindestkiste ungedreht (8×4×6/8×8×6, 4×2×2, 2×2×1).

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** `CrateCap.Min` = kleinste Kiste, die ein Rack nimmt (Martin); K1 `CrateCap.Accepts` statt Grenzen, gierige Zerlegung nur über angenommene Größen, Rest ohne angenommene Größe → Mission wartet, kein Hochkappen; K2 Neuplanung vergleicht die ganze Menge (Bitmaske, Record-Gleichheit). Ablehnung eines Moves mit Grund nicht gebaut (unerreichbar, wie bei der Tür); Split unter das Minimum wird abgelehnt. Texte `takes 32 SCU crates only` (mit „crates“, wie die übrigen Texte).
- Core: `Rack.MinCrateSize`, `CanHold` mit Untergrenze, `LimitRefuses`; `CrateCap` als Größenmenge (`Accepts`, `Min`, `Max`, `RackText`, `ShipText`, `Refusal`); `CrateMix.Decompose(scu, max, cap)`; `RackAssignment`-Zwillinge mit gleichem Minimum; `PlanFileDto.UnderCap` verwirft eine Kappung, die eine Kiste an Bord nicht nimmt; `RackDto.MinCrateSize`
- Import: `limits.min.capacity` > 1 (0 = keins, über 32 geklemmt, über dem Maximum Fehler), Abgleich und `-Force` wie bei der Tür; Hull C, MPUV Tractor, CSV-SM per `-Force` aus dem Index vom 25.10.
- Tests: `MinCrateSizeTests`, `MinCrateSizeOverlayTests`, `CrateCapTests` (Hull C, Lückenschiff), Testschiffe `min_crate_size*`, `Exactly_three_shipped_ships_have_a_minimum`, `A_shipped_minimum_is_one_the_rack_takes`, Import-Tests für `limits.min`

**Zahl vorher/nachher:**

| Messung | Vorher | Nachher |
|---|---|---|
| Tests (Core / Import / App) | 1.213 / 141 / 1.134 | 1.327 / 141 / 1.135 |
| Golden-Files und Fixtures geändert | erwartet 0 | 0 |
| Dateien mit `minCrateSize` | erwartet `csv_sm`, `hull_c`, `mpuv_tractor` | genau diese drei |
| 4 Fixtures × 96 Schiffe gegen 84b2cc3 | 384 | 372 gleich; 12 geändert, alle auf den drei Schiffen (Hull C lehnt alle vier Fixtures ab, keine besteht nur aus 32ern) |

**Akzeptanz:**
- [x] Hull C: 64 SCU max 32 plant zwei 32er; 16 max 16 wartet, `Plan` lehnt ab mit `(ship takes 32 SCU crates only)`, `PlaceProblem` = `doesn't fit (ship takes 32 SCU crates only)`; gesetzte `[16, 16]` und 40 max 32 warten genauso
- [x] Lückenschiff (Racks 32–32 neben 1–2): eine 8 passt nirgends, 40 max 32 → 32 + 4 × 2 (K1)
- [x] Schiffswechsel Hull A → Hull C → Hull A: 16er wartet, dann plant er wieder
- [x] Split einer 32 auf der Hull C abgelehnt; Move auf ein Rack mit Minimum → Stufe 4 mit `Rack 1: takes 16 SCU crates only – placed in rack 2`
- [x] Plan-Datei mit einer 16 auf der Hull C → neu geplant mit `Plan was made for other racks …`, Mission wartet (K2, Core und Overlay)
- [x] Schiffsdatei ohne Feld = jede Größe; Minimum über Maximum oder Gitter lädt nicht
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün; Slow grün
- [ ] Gate Overlay (Martin, kein Spieltest): Hull C wählen, 16er-Vertrag per CT-Sync → Mission in der Legende wartend mit `doesn't fit (ship takes 32 SCU crates only)`
- Commits 32b5dd2 (Tests rot), f1fb3ef (Core, Import), c32de68 (Daten), Doku-Commit.

### [x] K3.2 Kisten-Gier 90° (D71) (25.10.2026)
**Warum:** Spielfakt: Kisten drehen sich im Gitter um 90° um die Stapelachse, kippen nicht. Betroffen nur ungleiche Grundflächen (2, 16, 24, 32). Bisher gab es eine Form pro Größe (D9), T7.1b drehte dafür Racks.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** (1) „21 Racks“ sind 21 Schiffe mit 56 Racks (Liste aus den `notes`); (2) der Hull-C-Außenblock ist kein Beweis: bei Fleetyards x 5 × y 10 × z 7,5 m = length 8 × width 4 × stack 6, die Lage der Datei, ein 32er passt ungedreht („4×8×6“ war x×y×z gelesen); Beweis stattdessen Perseus Rack 1 und Freelancer Racks 2/3 (Martin: beide); (3) eine Kiste außerhalb des heutigen Racks wird neu geplant statt abgelehnt, unabhängig vom Schema, mit eigenem Grund `CapReason.RacksChanged` (K1); (4) Mausrad lässt sich nicht pollen, Spike vor der Geste (K2); (5) `CheckItems` verglich mit `SupportedSchemaVersion`. Dazu: ungedreht zuerst, gedreht nur wo ungedreht scheitert (Suche: zweiter Durchgang nur, wenn die erste ohne Lösung und ohne Aufgeben endet); Merge braucht einen Quader, nicht gleiche Lagen der Teile; `CrateMove.Turn` relativ (Standard: Lage behalten).
- Daten/Import: `import-fleetyards.ps1` schreibt `length` = Fleetyards y, `width` = x, kein „turned“ mehr; mit Offsets an allen Racks ohne Überlappung jetzt Fleetyards-Positionen bei `-Create`. **Zurückgedreht** (Racks in Klammern, Nummern der Datei): 600i Executive Edition (1, 2, 5, 6, 7), 600i Explorer (1, 2, 5, 6, 7), 600i Touring (1, 2), Carrack (2, 5, 8), Caterpillar (5, 6, 8, 10, 12, 14), Cutter (1), F7C Hornet Mk II (1), Freelancer, DUR, MAX, MIS (je 2, 3), Idris M (1, 2), Idris P (1–6), Mustang Alpha (1), Perseus (1), Reclaimer (5, 7, 9, 10, 11, 12), Reliant Kore (1, 2), SRV (1), Starfarer, Starfarer Gemini (je 2, 4), Zeus Mk II CL (2, 3). Abgleich gegen den Index vom 25.10.: 94 same, abweichend nur Fortune, Moth, Salvation (K5).
- Core: `CrateSize.Shape(rotated)`, `Turns`; `Crate.Rotated` (quadratisch + gedreht wirft, auch per `with`), `Crate.Shape`; `Pose`; `Rack.CellBox(Crate)`, `CellBox(position, shape)`; `FitsGrid` und Minimum in beiden Lagen; `RackSpace.Add`, `CrateLayout.Apart` (zweiter Durchgang), `Laying.Poses`, `RackLoad`, `MoveResolution.Intent.Rotated`, `Board.Put(Pose)`; Split/Merge (`CrateLayout.Block`); Plan-Schema 5 (`crates[].rotated` nur bei true, `UnrotatedSchemaVersion = 4`, `CheckRotated`); `PlanFileDto.Checked` liefert `RacksChanged`, `UnderCap(…, racksChanged)`, `CrateCap.Because`
- App: Zeichnen über `rack.CellBox(crate)` (Kisten, Geister, `DropTarget`); Rad im Einzel-Drag dreht statt Zoom (`DragGesture.CanTurn`, `Resolve(…, turned)`, `Gesture.MoveCrates.Turn`, `CrateDrop.Moves`/`Outcome`/`Changed`, `ShipViewControl.Turn`), keine neue P/Invoke
- Tests: `CrateRotationTests` (Core), `CrateTurnTests` und `A_turned_crate_is_drawn_as_it_lies_and_its_tooltip_stays` (App), `Schema_2_with_a_crate_outside_its_rack_reads_as_racks_changed`; `ReplanTimingTests.Search_counts_of_every_fixture`; geändert: `Place_never_lets_a_crate_float` (Schachbrett, ein gedrehter 2er passte quer), Schema-6-Beispiel, Layout-Tests lesen `Pose`

**Spike K2** (Martin, Scratch-Probe, SC vorne, Probe nicht im Vordergrund): während eines Rechts-Captures `WM_MOUSEWHEEL` 38 / 37, Raw Input (`RIDEV_INPUTSINK`) ebenso 38 / 37 → Rad über `WM_MOUSEWHEEL`, kein Raw Input.

**Zahl vorher/nachher:**

| Messung | Vorher | Nach Rückbau, ohne Drehung | Nachher |
|---|---|---|---|
| Tests (Core / Import / App) | 1.327 / 141 / 1.135 | – | 1.358 / 141 / 1.145 |
| Golden-Files geändert | erwartet nur `schemaVersion` | – | Hull-A-Goldens nur `schemaVersion` |
| 4 Fixtures × 96 Schiffe, geplant | 176 / 384 | 168 (53 Fälle geändert, alle auf den 21 Schiffen) | 176, dieselben Fälle; 42 Hashes anders als vorher, alle auf 16 Schiffen (Taurus `crate_mix`: Riker 6 SCU in 2ern allein im 3×2×1-Rack) |
| Suchknoten / Layout-Schritte (Release) | – | Hull A Hurston 1.312 / 10, Stresslast 25 / 246, Freelancer keine Zuordnung | gleich; Freelancer 67 / 0 |
| `ReplanTimingTests` Median (Release) | – | example 0,4 · split_and_shared 0,7 · split_required 0,4 · crate_mix 0,5 · Hull A acht Stopps 3,7 · Hurston 2,3 · Freelancer abgelehnt · Perseus 2,6 · Stresslast 79,6 ms | 0,6 · 0,9 · 0,4 · 0,5 · ~6,7 (dreimal gemessen: zweiter `FirstFit` bei jeder fehlschlagenden Passprobe) · 2,5 · 2,5 · 2,8 · 87–119 ms (Rauschen) |

D58 bleibt unberührt (kein Budget-Problem, keine offene Frage). Rot nach dem Rückbau, vor der Drehung (erwartet): `OrderFreePlanTests` hull_a_eight_stops/freelancer, `A_shipped_max_crate_size_is_below_what_the_grid_takes` idris_p/idris_m, Slow `ReplanTests` Freelancer, Timing `MoveResolutionTimingTests.Eight_stops_on_the_Freelancer`.

**Akzeptanz:**
- [x] Rückbau der 56 Racks vor den roten Tests, Import ohne Drehung, Abgleich „same“
- [x] 3×2×1-Rack nimmt drei 2er (einer gedreht); Perseus Rack 1 drei 32er gedreht; Freelancer 66 SCU in 2ern; Hull A dreht nie
- [x] Drehung respektiert Tür und Minimum (`CanHold`)
- [x] Plan-Schema 4 → 5: Schema 4 liest ungedreht, `rotated` in 4 kaputt, 6 abgelehnt; alter Leser (`0892454`) lehnt 5 ab: „schemaVersion 5 is not supported (expected 1 or 2 or 3 or 4)“
- [x] Kiste außerhalb des heutigen Racks (Schema 2, 4, 5) → `RacksChanged`, neu geplant, ganz abgehakte Stopps behalten den Haken; Schema 5 innerhalb unverändert
- [x] Split/Merge mit gedrehten Kisten; B2-Property-Test grün
- [x] Rad dreht nur im Einzelkisten-Drag, Vorschau = Loslassen, Mehrfachauswahl und Legende behalten die Lagen
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün; Slow grün; Exhaustive grün (8 min 34 s)
- [x] Gates Publish-Build (Martin, 25.10., bestanden): Hull A 2er ziehen, drehen, loslassen, Neustart → Lage bleibt; im Spiel mit SC vorne dreht das Rad; Freelancer 2er-Vertrag → Racks 2/3 gedreht; Perseus 32er-Vertrag → drei gedrehte 32er; Mehrfachauswahl mit gedrehtem 2er → Lagen bleiben; alte Freelancer-`plan.json` → „Plan was made for other racks …“
- [x] Gate Traktorstrahl (Martin, 25.10., bestanden): 2er hochkant an eine Hull-A-Platte geht im Spiel; `CanTurn` bleibt für jedes Rack
- Commits 04669b3 (Rückbau), 3c8a52b (Core-Tests rot), 748a3ed (Core), 52f6475 (Zeichnen), c4af6ad (App-Tests rot), b34c8be (Rad), b173294 (Doku), Abnahme-Commit; Merge `--no-ff` nach main.

Reihenfolge innerhalb von K2 (Branch `feat/k2-plan-editor`): K2.1 Füllrichtung → K2.2 Haken-Erhalt → K2.3 Discard plan → K2.4 Undo/Redo zuletzt, weil der Stack alle anderen Aktionen umschließt. Schema-Stand vor K2: Schiff 5, Plan 5, Mission 2.

### [x] K2.1 Füllrichtung in Missionsdateien (D72) (25.10.2026)
**Warum:** Die Füllrichtung stand nur in `plan.json` (D24, T4.5); ein Koordinator, der die Route als Missionsdatei teilt, konnte sie nicht mitgeben (Guide: „exported mission files don't carry it“).

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 `MissionFileReader.Read` liefert `MissionFile(Stops, FillOrder?)`, `MissionFileWriter.Write(stream, stops, fillOrder)` mit Pflichtparameter (kein zweiter Lesepfad); P2 Wörter und Switch aus `PlanFileDto` nach `FillOrderJson` verschoben, nicht kopiert; P3 `fillOrder` in Schema 2 abgelehnt, unbekannter Wert mit der Meldung des Plans, Schema 3 ohne Feld gültig; P4 eingebettete Missionen in Plan-Schema 1/2 bleiben Mission 1|2 (bestehender Test `inner_schema`); P5 CLI `plan` nimmt die Richtung aus der Datei, sonst Bug zuerst; P6 Import mit Feld setzt den Entwurf (wirkt erst mit Plan), ohne Feld bleibt die Richtung, Export schreibt die des Entwurfs; P7 `fillOrder` direkt nach `schemaVersion`; P8 eigener Eintrag D72, D24-Addendum.
- Core: `MissionFile`, `FillOrderJson` (`Parse`, `Text`), `MissionFileDto(SchemaVersion, Stops, FillOrder?)` mit `ToMissionFile`, `JsonPropertyOrder` auf `Stops`; `MissionFileReader.CurrentSchemaVersion = 3`, `NoFillOrderSchemaVersion = 2`, `EmbeddedSchemaVersions`; `PlanFileDto`/`PlanFileV2Dto` über `FillOrderJson`
- App: `MissionEditorViewModel.Import` setzt `FillOrder` aus der Datei, `Export` übergibt sie
- Daten/Tests: Fixtures `fill_order_stern.json` (Writer-Format), `schema3_without_fill_order.json`, `Invalid/fill_order_in_v2.json`, `Invalid/unknown_fill_order.json`; `Invalid/unsupported_schema` 3 → 4; Reader-, Writer- und Editor-Tests; `MissionFixtures.LoadFile`/`Text`

**Zahl vorher/nachher:**

| Messung | Vorher | Nachher |
|---|---|---|
| Tests (Core / Import / App) | 1.358 / 141 / 1.145 | 1.372 / 141 / 1.151 |
| Golden-Files und Plan-Fixtures geändert | erwartet 0 | 0 |
| Missions-Fixtures geändert | erwartet nur `Invalid/unsupported_schema` | nur diese (3 → 4) |
| Schema-3-Datei lesen → schreiben | – | byte-gleich (Core bis auf Zeilenenden; Editor Import → Export byte-gleich) |
| Alter Leser auf Schema 3 | – | „schemaVersion 3 is not supported (expected 1 or 2)“ |

**Akzeptanz:**
- [x] Schema 3 mit `fillOrder` lädt mit Richtung, ohne Feld und Schema 1/2 ohne (`MissionFileReaderTests`)
- [x] Import mit Feld setzt die Richtung als ungeplante Änderung, ohne Feld bleibt sie; Clear rows behält sie (`MissionEditorViewModelTests`)
- [x] Export schreibt die Richtung immer, nach `schemaVersion`; Rundreise byte-gleich (`MissionFileWriterTests`, Editor)
- [x] Stern-first-Datei importiert und geplant → Kisten wie `SternFirst`
- [x] `fillOrder` in Schema 2 und unbekannter Wert → Fehlermeldung mit Grund; Schema 4 → „expected 1 or 2 or 3“
- [x] Goldens unverändert; `dotnet build` ohne Warnungen; `dotnet test` grün
- Die CLI-Textausgabe nennt Racks, keine Zellen: Mit und ohne Feld sieht sie gleich aus, P5 ist dort nicht sichtbar.
- Commits 5b39915 (Fixtures), 7008222 (Core-Tests rot), eb76cd5 (Core), af87047 (App-Tests rot), 1f7c98f (App), Doku-Commit.

### [x] K2.2 Haken-Erhalt beim Schiffswechsel und bei `RacksChanged` (D75) (25.10.2026)
**Warum:** Offene Zeile aus D59: Auf einem anderen Schiff kamen nur ganz abgehakte Stopps abgehakt zurück, Legendenzeilen und der D41-Fall nicht, und abgehakte Fracht belegte beim Planen Platz; seit K3.2 galt das auch für `RacksChanged`. Regel: Was abgeliefert ist, bleibt abgeliefert.

**Ist-Zustand vor K2.2 (A1):** Der Haken liegt seit D24 pro Kiste (`Crate.Delivered`), `SetDelivered` hakt ganze Lieferungen ab (Stopp oder Mission × Ziel). Auf dem eigenen Schiff war der D41-Fall damit schon gelöst (neue Fracht = neue Mission); der Verlust lag nur in `ReplanForShip`, über das Schiffswechsel, `RacksChanged` und fremde Kappung laufen.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 Einheit = Lieferung (Mission × Ziel, die Legendenzeile), ganz abgehakt = alle Kisten abgehakt; abgelieferte Lieferungen verlassen die Mission vor dem Planen, ohne Haken bleibt der Weg der alte; P2 Variante **B** (Martin): abgelieferte Fracht war nie auf dem neuen Schiff, wird nicht gezeichnet, nur die Legende zeigt den Haken; Kennzeichen **Plan-Schema 6** mit `crates[].offShip` (Martin, statt einer Platzhalter-Position); Zurückhaken legt ein wie `PlaceInto` (Martin, statt Ablehnen); P3 Off-Ship-Kisten werden von Rack-, Gitter- und `Validate`-Prüfung übergangen (eine gesetzte 16 auf dem Clipper bleibt abgehakt); P4 bekannte Grenze: eine Mission, deren Rest auf dem neuen Schiff wartet, verliert den Haken ihrer Lieferung; P5 die Frage zählt Lieferungen; P6 Addenda zu D59, D41, D24 statt eigenem D-Eintrag – bei der Abnahme auf Martins Wunsch eigener Eintrag **D75** (nach D73/D74, ohne Umnummerierung), D59 behält einen Verweis.
- Core: `Crate.NoRack`, `Crate.OffShip`, `Crate.OffShipOf`; `ReplanForShip` mit `Undelivered`, `PlanOrPlace`; `LayStop` aus `StopByStop` herausgelöst, genutzt von `BackOnBoard` (Zurückhaken); `SetDelivered` legt Off-Ship-Kisten ein; `Validate` und `PlanFileDto.Checked` übergehen sie; Plan-Schema 6 (`PlanFile.NoOffShipSchemaVersion = 5`, `CrateDto.OffShip`, `CheckOffShip`); `CheckRotated` nennt Schema 5 statt des aktuellen
- App: `ShipChangeQuestion`/`TicksStayText` zählen Lieferungen („2 deliveries already ticked – ticks stay.“); Zeichnung, Legende und Text brauchen nichts, Off-Ship-Kisten liegen in keinem Rack
- Tests: `ShipChangeTickTests` (16), `ReplanTimingTests.Ship_change_with_and_without_ticks`, App `ShipChangeTests`/`LegendByMissionTests` (+4, zwei Texte geändert); Schema-Tests auf 6, `Schema_7_is_refused`

**Zahl vorher/nachher:**

| Messung | Vorher | Nachher |
|---|---|---|
| Tests (Core / Import / App) | 1.372 / 141 / 1.151 | 1.388 / 141 / 1.155 |
| Golden-Files geändert | erwartet nur `schemaVersion` (Schema 6) | nur `schemaVersion` 5 → 6, Rest byte-gleich |
| `ReplanTests`, `ReplanInPlaceTests`, B2-Property-Test | grün | unverändert grün; Slow grün |
| `ReplanTimingTests` Median (Release) | example 0,4 · split_and_shared 0,8 · split_required 0,3 · crate_mix 0,5 · Hull A acht Stopps 6,2 · Hurston 2,3 · Freelancer 2,5 · Perseus 2,9 · Stresslast 82,5 ms | 0,5 · 0,8 · 0,3 · 0,5 · 6,5 · 2,2 · 2,6 · 2,9 · 76,1 ms; Suchzähler gleich |
| Schiffswechsel acht Stopps (Release, Median) | Hull A → Freelancer 1,1 / mit zwei Stopps abgehakt 1,3; zurück 3,3 / 3,4 ms | 1,2 / 1,0; zurück 3,1 / 0,9 ms |

**Akzeptanz:**
- [x] Abgehakter Stopp und abgehakte Legendenzeile bleiben über Schiffswechsel hin und zurück abgehakt; die andere Mission desselben Ziels bleibt an Bord
- [x] D41-Fall: Stopp abgehakt, Sync legt neue Mission dorthin nach → nach dem Wechsel nur die alte Lieferung abgehakt
- [x] Abgehakte Fracht belegt nichts: die Fracht an Bord liegt wie ein Plan ohne sie; 72 SCU auf der C2 mit 32 abgehakt passen ganz auf die Hull A
- [x] `RacksChanged` behält Zeilenhaken; Sync und Re-plan auf dem neuen Schiff lassen Off-Ship-Kisten stehen
- [x] Zurückhaken legt in freie Zellen, sonst nichts bewegt, Legende behält die Reihenfolge; ohne Platz abgelehnt
- [x] Plan-Schema 6 schreibt `offShip` nur bei true; Schema 5 mit `offShip`, ohne `delivered` oder mit Rack → Fehler; Schema 7 abgelehnt
- [x] Overlay: Kisten eines abgehakten Stopps auf dem neuen Schiff nicht gezeichnet; Frage zählt Lieferungen
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün; Slow grün
- [x] Gate (Martin, 25.10., bestanden): Hull A, einen Stopp und eine Legendenzeile abhaken, auf die Freelancer wechseln → beide Haken bleiben, deren Kisten fehlen in der Ansicht, Kapazität ohne sie; zurück auf die Hull A → gleich; einen Haken zurücknehmen → Kisten erscheinen
- Commits 16257ed (Core-Tests rot), da24cf0 (Core), 97bf144 (App-Tests rot), 69fdc76 (App), Doku-Commit.

### [x] K2.3 Discard plan (D73) (25.10.2026)
**Warum:** Nur „Plan“ ersetzte bisher einen Plan (D19); nach einer Route blieb der alte stehen. Martins Entscheidung: Variante a – Plan leeren **und** Sync-Baseline zurück, der nächste Sync legt alle CT-Verträge wieder nach; kein Merken verworfener Verträge.

**Ist-Zustand vor K2.3 (A1):** Die Sync-Baseline ist der `Watermark` im `SavedPlan`. Ohne Plan legt der Sync einen neuen mit Bug zuerst an, der Editor startet mit Bug zuerst; `HasPlan` war `Plan is not null`.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 leer heißt leerer Plan (keine Missionen, Kisten, Haken, Watermark 0, alte Füllrichtung, Schiff des Overlays), nicht `null`; P2 `HasPlan` = hat Missionen (leerer Plan zeigt den leeren Zustand wie T3.8a, auch nach Löschen der letzten Mission); P3 `LoadPlanner.Discard`; P4 Kommando `DiscardPlan` im Overlay-VM, Notice statt Frage („Plan discarded – the next sync brings every ContractTracker contract back.“); P5 Editor-Knopf „Discard plan“ neben Re-plan, eigene Änderung wie Re-plan; P6 Hotkey `discardPlan`, Default null, settings.json Schema 2 bleibt; P7 laufender Watcher legt beim nächsten Signal nach (gewollt, im Guide); P8 eigener Eintrag D73, Addenda D19, D44.
- Core: `LoadPlanner.Discard`
- App: `OverlayViewModel.DiscardPlanCommand`, `DiscardedText`, `HasPlan`; `MissionEditorViewModel.DiscardPlanCommand`, `HasPlan` vom Overlay; `HotkeyAction.DiscardPlan`, `HotkeySettings.DiscardPlan`, `HotkeyService`, `SettingsSession`, `HotkeysViewModel.Discard`, `HotkeyEditor.DiscardLabel`, Zeile im Settings-Fenster, Knopf im Editor
- Tests: `DiscardTests` (Core, gegen `ContractSync.Apply`), `DiscardPlanCommandTests` (App), Hotkey- und Settings-Tests

**Zahl vorher/nachher:**

| Messung | Vorher | Nachher |
|---|---|---|
| Tests (Core / Import / App) | 1.388 / 141 / 1.155 | 1.391 / 141 / 1.165 |
| Golden-Files geändert | erwartet 0 | 0 |
| `ContractSync.Apply` mit Verträgen 36–38 unter Watermark 41 | ohne Discard: nichts | nach Discard: alle drei, Watermark 38, Richtung bleibt |

**Akzeptanz:**
- [x] Discard leert Missionen, Kisten, Haken; Watermark 0; Füllrichtung und Schiff bleiben (Core und Overlay)
- [x] Nächster Sync nach Discard bringt alle Verträge zurück
- [x] Leerer Zustand („No plan…“), Notice, kein Dialog; Kommando ohne Missionen aus
- [x] Editor-Knopf folgt dem Overlay; saubere Zeilen → eine leere Zeile, Entwurf bleibt, kein „changed outside“, Richtung bleibt
- [x] Hotkey `discardPlan`: kein Default, Kollision abgelehnt, von Session und Store geschrieben
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, 25.10., bestanden): Plan mit CT-Verträgen → Discard → leer, Notice; Sync → alles wieder da. Editor-Plan „Stern first“ → Discard → leer, Editor zeigt weiter „Stern first“, auch nach Neustart. Hotkey belegen und auslösen. Blick auf den Notice-Text (Geschmack)
- Commits 4b3f6bb (Core-Tests rot), 14c5996 (Core), 57cca8b (App-Tests rot), 6d2b53b (App), Doku-Commit.

### [x] K2.4 Undo/Redo (D74) (25.10.2026)
**Warum:** Seit D65 bewegt ein Drop mehr als die gezogene Kiste, Re-plan (D66) und Discard (D73) ändern den ganzen Plan; Zurückziehen half nur in Stufe 1–2. Martins Entscheidung: Snapshot-Stack mit Redo, nicht persistiert.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Sync (Martin, gegen das Leeren der Stacks): Stacks bleiben, Undo/Redo = Snapshot wiederherstellen und sofort inkrementell syncen, Sync kein Schritt; Haken pro Lieferung auf den wiederhergestellten Plan übertragen; Schiffswechsel leert beide Stacks mit Notice (Martin). P1 Mission löschen (Editor, Legende) ist ein Schritt; P2 Resync ohne Datenbank-Lesen: Captures des aktuellen Plans, die weder Snapshot noch Referenz kannten, kommen per `Apply` wieder herein, Entfernen mit `_present`; P3 Haken: nur was sich seit dem Schritt geändert hat (sonst verlöre Undo von „Plan“ die alten Haken, D19), Schlüssel Vertrag statt Missions-Id; P4 Eintrag = Ziel + Referenz; P5 `MaxSteps` 100 nach Messung; P6 ↶ ↷ links vom Zahnrad, nur mit Schritt, beim Lock versteckt; P7 Hotkeys `undo`/`redo`; P8 `ContractSync.Resync`, `LoadPlanner.CarryTicks`, `PlanHistory`; P9 eigener Eintrag D74, Addenda D59, D65, D66.
- Core: `ContractSync.Resync`, `LoadPlanner.CarryTicks`
- App: `PlanHistory`; `OverlayViewModel.Step`, `UndoCommand`, `RedoCommand`, `CanUndo`, `CanRedo`, `HistoryClearedByShipChangeText`; Kopfzeilen-Knöpfe; Hotkeys `undo`/`redo` (`HotkeyAction`, `HotkeySettings`, `HotkeyService`, `SettingsSession`, `HotkeysViewModel`, Settings-Fenster)
- Tests: `UndoRestoreTests` (Core, 10), `UndoRedoTests` (App), `UndoTimingTests` (App, Timing, von Hand; App-Tests haben jetzt `default.runsettings` wie Core), Kopfzeilen-XAML, Hotkey- und Settings-Tests

**Zahl vorher/nachher:**

| Messung | Vorher | Nachher |
|---|---|---|
| Tests (Core / Import / App) | 1.391 / 141 / 1.165 | 1.401 / 141 / 1.182 |
| Golden-Files geändert | erwartet 0 | 0 |
| Speicher ganzer Plan (Hull A acht Stopps / Hurston 25.10. / C2 acht Stopps) | 8,6 / 10,5 / 8,2 KB | – (Snapshots teilen Unverändertes) |
| Zuwachs je Move | 2,5 / 1,9 / 1,7 KB | – |
| Plan anzeigen (Validierung + Szene) | 0,4 / 0,4 / 0,6 ms | – |
| Erstes Undo nach Move, Sync und Haken (mit Resync) / Redo | – | 0,85–0,99 / 0,44–0,48 · 0,88–0,91 / 0,38–0,39 · 1,18–1,19 / 0,53 ms (Ziel < 16 ms) |

**Akzeptanz:**
- [x] Drei Moves, dreimal Undo, zweimal Redo, neuer Move → Redo weg (`UndoRedoTests`)
- [x] Re-plan → Undo stellt die alte Anordnung her; Discard → Undo bringt den Plan zurück
- [x] Move → Haken → Undo → Haken steht; → Redo → Haken steht
- [x] Sync nach Move → Undo behält den nachgelegten Vertrag; Ticks und Syncs sind keine Schritte
- [x] Plan im Editor, Mission löschen, Split sind Schritte; Schiffswechsel leert beide Stacks mit Notice
- [x] `MaxSteps` 100; Kopfzeile ↶ ↷ links vom Zahnrad, nur mit Schritt, beim Lock versteckt; Hotkeys ohne Default
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, 25.10., bestanden): Hull A, drei Moves, dreimal Undo, zweimal Redo, neuer Move → ↷ weg; Re-plan → Undo; Discard → Undo; Move → Haken → Undo → Haken steht; Sync dazwischen → Undo behält den Vertrag; Schiffswechsel → Knöpfe weg, Notice; Neustart → Knöpfe weg. Blick auf die Kopfzeile (Geschmack: Knöpfe, Kürzung des Schiffsnamens)
- Commits 6095b57 (Messung vorher), 11e4ecd (Core-Tests rot), bfb31c0 (Core), 966b534 (App-Tests rot), 01fb78b (App), Doku-Commit.

### Abschluss der K2-Session (25.10.2026)
- Alle vier Tasks von Martin abgenommen (Gates K2.2–K2.4 bestanden, K2.1 ohne Gate). K2.2 bekam bei der Abnahme einen eigenen Eintrag (D75).
- Tests: Core 1.358 → 1.401, Import 141 → 141, App 1.145 → 1.182; Slow grün; Exhaustive grün (8 min 7 s).
- Merge `--no-ff` nach main, Push durch Martin.

**Übergabe für den K1-Startprompt:**
- **Schema-Stand:** Schiff 5, Plan **6** (`crates[].offShip`, K2.2), Mission **3** (`fillOrder`, K2.1), settings.json 2 (neue optionale Hotkeys `discardPlan`, `undo`, `redo`). Eine App von vor K2 lehnt jede neue `plan.json` und jede exportierte Missionsdatei ab.
- **Geänderte Goldens:** nur die zehn Hull-A-Goldens, nur `schemaVersion` 5 → 6 (K2.2); sonst byte-gleich. Missions-Fixture `Invalid/unsupported_schema` 3 → 4.
- **Neu für K1 relevant:** `HasPlan` heißt „hat Missionen“ (K2.3); jede Plan-Änderung des Spielers läuft über `OverlayViewModel.Step` (K2.4) – K1-Aktionen an der Ansicht (Zoom, Kamera, Pan) sind keine Schritte. Die Kopfzeile trägt mit Verlauf zwei Knöpfe mehr (↶ ↷), der Schiffsname wird früher gekürzt (D67). App-Tests haben jetzt `default.runsettings` (Kategorie Timing nur von Hand).
- **Offen aus K3:** Antwort von Torlek zu `limits.max = 1` (K3.1) und damit die Liste der ungeprüften Türen (32 Racks in 16 Schiffen); Budget-Befund aus K3.2: D58 bleibt unberührt, keine offene Frage; Rad-Routing (D71): Star Citizen sieht das Rad während eines Drags mit, und es hängt an Windows' „scroll inactive windows“ – im Spiel nicht weiter geprüft.
- **Offen aus K2:** eine Mission, deren Rest auf dem neuen Schiff wartet, verliert den Haken ihrer abgelieferten Lieferung (D75); der Resync nach Undo liest die Datenbank nicht, ein seit dem letzten Lesen in CT gelöschter Vertrag geht erst beim nächsten echten Sync (D74); Undo-Verlauf nicht persistiert.

Reihenfolge innerhalb von K1 (Branch `feat/k1-ship-view`): K1.1 Zoom-Grenzen → K1.2 Kamera-Persistenz → K1.3 Reset-Symbol → K1.4 Hit-Test. K1.1 zuerst, weil K1.2 den Zoomwert speichert. Nur App, Core unberührt; Schemas bleiben (Schiff 5, Plan 6, Mission 3, settings 2 mit neuen optionalen Keys).

**Präzisierung in der Planphase, ratifiziert durch Freigabe (für K1.1–K1.4):** P1 Einheit DIPs (WPF-Pixel), wie Raster und Nummern; P2 Untergrenze bleibt 0,5 × Fit, nur die Obergrenze wird absolut (die 4 px des Briefs entfallen); P3 Zoomzeile im Abschnitt Ship view, nicht Crates; P4 Kamera als absolute Rahmung (px/Zelle + Pan in Zellen), live und gespeichert dieselbe Darstellung; P5 `pxPerCell`/`panX`/`panY` optional, nur gedreht = nur die Kamera, beim Start wird gefittet; P6 Pan vom schiffsfesten Anker (projizierte Mitte von `Ship.Bounds`), nie vom Fit der Beladung (Korrektur bei der Freigabe); P7 Reset-Symbol direkt links vom Zahnrad, rechts von ↶ ↷; P8 Hit-Test-Naht erst ohne Prüfung (rot per Assert), dann mit; P9 keine Frame-Zeit-Messung (gleiche Geometrie, nur andere Skala; das volle Raster zeichnet die Hull C schon bei 4 × Fit).

### [x] K1.1 Zoom-Grenzen absolut in Pixel pro Zelle (D76) (25.10.2026)
**Warum:** Auf großen Schiffen (C2, Hull C) reichte der maximale Zoom nicht, um einzelne Kisten zu treffen und zu lesen (Martin).

**Ist-Zustand vor K1.1 (A1):** Zoom war ein Faktor 0,5–4 auf den Fit (`ViewNavigation.MinZoom`/`MaxZoom`), Pan in Pixeln um die Bildschirmecke; Hull C fittet bei 800 × 600 auf 8,0 px/Zelle (D61) → höchstens 32, mit Hologramm weniger; Hull A im Standard-Overlay ≈ 30 → 120. Der Radschritt war schon ein Faktor (1,2 pro Raste). Die Rastergrenze borgte die Mindest-Schriftgröße der Nummern (12).
- App: `ViewBasis` (Fit, Mitte der Fit-Fläche, Anker), `ZoomLimits` (unten 0,5 × Fit, oben `shipView.maxZoomPxPerCell`, Fit immer erreichbar), `Framing` (px/Zelle, Pan in Zellen vom Anker, `OfFit`), `ViewNavigation(Camera, Framing?)` mit `On`, `ZoomAt`, `PanBy`, `Clamped`, `ZoomOn`; `ProjectedScene.Anchor`; `FreeCellGrid.MinCellSize = 12` als eigener Wert; `ShipViewControl.MaxPxPerCell` (kleinerer Wert schrumpft eine Rahmung sofort um ihre Mitte)
- Settings: `ShipViewSettings` (`shipView.maxZoomPxPerCell`, Default 160, 40–240, geklemmt mit Notice), `SettingsSession.ChangeMaxZoom`/`MaxZoom`, `OverlayViewModel.MaxZoomPxPerCell`, `SettingsViewModel.MaxZoom` mit Slider „Max zoom per cell“ im Abschnitt Ship view, Reset dort
- Tests: `ViewTransformTests` (Navigationsteil neu), `SceneProjectorTests` (Anker), Store-, Session- und SettingsViewModel-Tests

**Akzeptanz:**
- [x] Obergrenze absolut, gleich bei Fenster 400 × 300 und 1200 × 900; Untergrenze 0,5 × Fit; Fit über dem Maximum bleibt Obergrenze
- [x] Punkt unter dem Cursor bleibt beim Zoomen; am Limit bewegt sich nichts; Zoomschritt pro Raste ein Faktor (1,2)
- [x] Kleinere Preference schrumpft die Ansicht sofort um ihre Mitte
- [x] Rahmung hängt am Anker, nicht am Fit-Extent (breiterer Extent → gleiches Bild); Anker unabhängig von der Beladung
- [x] Rastergrenze 12 px/Zelle unverändert; Zoom beim Einzelkisten-Drag weiter aus (K3.2, unverändert)
- [x] settings.json ohne `shipView` → 160 ohne Notice; Rundreise; 20 → 40 und 500 → 240 mit Notice
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (App 1.182 → 1.203)
- [x] Gate Publish-Build (Martin, 25.10., abgenommen): Hull C hineinzoomen, bis eine 32er lesbar den Tooltip trifft; Hull A fühlt sich unverändert an; Preference ändern wirkt sofort
- Commits 8dd6803 (Tests rot), bbb9314 (App), Doku-Commit.

### [x] K1.2 Kamera-Persistenz pro Schiff (D76) (25.10.2026)
**Warum:** Backlog D15: Drehen, Zoom und Pan gingen bei jedem Neustart verloren, auf großen Schiffen nervt das mehr.

**Ist-Zustand vor K1.2 (A1):** Die Navigation lebte nur im Control (`_navigation`), der Schiffswechsel setzte sie dort zurück (`OnSceneChanged`). Die Recent-Liste hält höchstens 4 Ids (`ShipChoices.Remember`) und liefert die Obergrenze. Kein Muster „VM bittet das Control“; die Auswahl läuft zweiseitig per `SetCurrentValue`.
- Settings: `ShipCamera(Yaw, Pitch, PxPerCell?, PanX?, PanY?)`, `ShipViewSettings.Cameras` (Einträge ohne Kamera oder Id fallen an der Dateigrenze weg), `SettingsSession.ChangeCameras` (500 ms Ruhe, `Flush` beim Beenden)
- App: `ViewNavigation.From`/`ToCamera` (Rahmung nur mit allen drei Werten); `OverlayViewModel.Cameras` (Start aus settings.json) und `Camera` (des aktuellen Schiffs, nur Recent ∪ aktuelles Schiff behalten, Schiffswechsel meldet die Kamera vor der Szene, kein `Step`); `ShipViewControl.Camera` zweiseitig, jede Navigation schreibt, Doppelklick schreibt null, Navigation aus Kamera + Szene abgeleitet (Reihenfolge der Bindings egal); Composition Root leitet `Cameras` an die Session
- Tests: `ShipCameraTests` (13 Methoden, davon der Anker-Fall an der Caterpillar: Kamera speichern, Kiste in ein Rack weit achtern, Kamera laden → Rack 1 an derselben Bildschirmstelle), Store- und Session-Tests

**Akzeptanz:**
- [x] Kamera pro Schiff: Start liest sie, Wechsel hin und zurück bringt jeweils die eigene, Reset entfernt den Eintrag
- [x] Nur Recent ∪ aktuelles Schiff behalten Kameras; gleiche Kamera noch einmal meldet nichts; Kamera ist kein Schritt
- [x] Nur gedreht = keine Rahmung = Fit beim nächsten Start; andere Fenstergröße: gleiche px/Zelle, derselbe Punkt in der Mitte (K1.1-Test)
- [x] Gespeicherte Ansicht bleibt am selben Rack, wenn ein Sync ein Rack weiter außen belädt
- [x] settings.json: Rundreise, `null`-Einträge und leere Ids fallen weg; `ChangeCameras` entprellt, Max-Zoom und Kameras überschreiben einander nicht
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (App 1.203 → 1.223)
- [x] Gate Publish-Build (Martin, 25.10., abgenommen): Hull C drehen, zoomen, verschieben, beenden, starten → gleiche Ansicht; Schiff wechseln und zurück → gleich; Reset → Fit, Neustart → Fit
- Commits a2c05dc (Tests rot), a6b059f (App), Doku-Commit.

### [x] K1.3 Reset-Symbol in der Kopfzeile + Hotkey `resetView` (D15-Addendum) (25.10.2026)
**Warum:** Backlog: der Reset per Doppelklick ist versteckt.

**Ist-Zustand vor K1.3 (A1):** Kopfzeile von rechts: Exit, Zahnrad, ↷, ↶ (nur mit Verlauf), Stift, Re-plan …; das Schloss ist ein eigenes Fenster. `HeaderButtonStyle` blendet gesperrt aus. Hotkey-Muster K2.3/K2.4 (`HotkeyAction`, `HotkeySettings`, `HotkeyService`, `SettingsSession`, `HotkeysViewModel`, `HotkeyEditor`, Settings-Zeile).
- App: `OverlayViewModel.ResetViewCommand` (Kamera null, auch gesperrt); Button rechts neben dem Zahnrad, Tooltip „Reset view“, Glyph E9A6 FitPage (gegen die Segoe-Doku geprüft, am echten Element gewählt, Martin delegierte die Wahl); ↶ ↷ eine Stelle weiter links; Hotkey `resetView` ohne Default in allen sieben Bausteinen
- Tests: Kopfzeilen-XAML (Position, Stil, kein Ausblenden außer gesperrt, Glyph; ↶ ↷ verschoben), `ShipCameraTests` (Reset, gesperrt), Hotkey- und Session-Tests

**Akzeptanz:**
- [x] Symbol rechts neben dem Zahnrad, immer da, gesperrt ausgeblendet (Stil); springt nicht, wenn ↶ ↷ kommen und gehen
- [x] Reset entfernt die Kamera des Schiffs (Fit, auch nach Neustart); wirkt gesperrt; Doppelklick bleibt
- [x] Hotkey `resetView`: kein Default, Kollision abgelehnt, von Session und Store geschrieben
- [x] Screenshot Debug-Build: Symbol sichtbar zwischen Stift und Zahnrad (AppData vorher gesichert, danach hash-gleich)
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (App 1.223 → 1.229)
- [x] Gate (Martin, 25.10., abgenommen): Symbol sichtbar, Klick fittet, im Lock-Modus weg; Hotkey nach Aufnahme wirkt im Spiel
- Commits d0b1823 (Tests rot), d49c021 (App), Doku-Commit.

### [x] K1.4 Hit-Test auf die Ansichtsfläche begrenzen (D33-Addendum) (25.10.2026)
**Warum:** Gezeichnet wurde mit `ClipToBounds`, getroffen nicht: ein Drop über der Legende konnte ein beim Hineinzoomen abgeschnittenes, unsichtbares Rack treffen (D33).

**Ist-Zustand vor K1.4 (A1):** `HitAt`, `DestinationAt` und der Hover-Tooltip invertierten jeden Punkt, auch außerhalb von `RenderSize`; mit gefangener Maus kommen Punkte über Legende und Kopfzeile an. `DropAt` läuft nur mit einem Treffer, `DragGesture.Resolve`/`ResolveLegend` machen ohne Treffer `None`.
- App: `SceneHitTest.HitAt`/`DestinationAt`/`ToolTipAt(scene, transform, viewport, screen)`, außerhalb des Rechtecks (Ränder eingeschlossen) kein Treffer; das Control nutzt nur noch diese (Hover, Rechtsklick, Drop-Ziel, Legenden-Drag, Doppelklick)
- Tests: `SceneHitTestTests` (+6: Szene allein trifft das unsichtbare Rack, außerhalb kein Treffer, Legendenzeile über der Legende bewegt nichts, innerhalb wie die Szene an Mitte und beiden Ecken)

**Zahl vorher/nachher:**

| Messung | Vorher | Nachher |
|---|---|---|
| Hull A, Rack 1 unter eine 400 × 300-Ansicht gezoomt, Punkt (200, 350) über der Legende | Treffer `rack-0`, Legendenzeile → `MoveLegend` | kein Treffer, `None` |
| `SceneHitTestTests` mit der Naht ohne Prüfung (b0b6fc7) / mit Prüfung | 2 von 28 rot | 28 von 28 grün |
| Tests (Core / Import / App) | 1.401 / 141 / 1.182 (vor K1) | 1.401 / 141 / 1.235 |

**Akzeptanz:**
- [x] Rot per Assert gesehen (Naht als reine Weiterleitung), dann grün
- [x] Außerhalb der Fläche weder Treffer noch Ziel noch Tooltip; ein Drop dort bewegt nichts, die Vorschau zeigt keinen Geist und kein Ziel
- [x] Innerhalb der Fläche, Ränder eingeschlossen, unverändert
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, 25.10., abgenommen): Hull C hineinzoomen, ein Rack aus dem Bild schieben, Kiste über die Legende an diese Stelle ziehen → kein Drop
- Commits b0b6fc7 (Naht, Tests rot), d3d9be8 (Prüfung), Doku-Commit.

### Abschluss der K1-Session (25.10.2026)
- Alle vier Tasks von Martin abgenommen (25.10.2026, Publish-Build), Gates K1.1–K1.4 bestanden.
- Merge `--no-ff` nach main, Push durch Martin.
- Tests: Core 1.401 → 1.401, Import 141 → 141, App 1.182 → 1.235; Slow grün; Exhaustive nicht nötig (Core unberührt).
- Keine Frame-Zeit-Messung (P9): gleiche Geometrie, nur andere Skala.

**Übergabe für den K4-Startprompt:**
- **Schema-Stand:** Schiff 5, Plan 6, Mission 3, settings.json 2. **Neue optionale Keys (K1):** `shipView.maxZoomPxPerCell` (Default 160, 40–240), `shipView.cameras` (Katalog-Id → `yaw`, `pitch`, `pxPerCell`, `panX`, `panY`), `hotkeys.resetView`. `shipView` ist ein Objekt, die älteren Ship-view-Keys (`shipOutline`, `holo*`) liegen flach; der Ship-view-Reset setzt den Zoom zurück, nie die Kameras.
- **Hotkey-Liste:** `toggleLock`, `toggleOverlay`, `syncContractTracker`, `replan`, `discardPlan`, `undo`, `redo`, `resetView` – alle ohne Default (D17).
- **Für K4 relevant:** Die Kopfzeile trägt jetzt rechts neben dem Zahnrad das Reset-Symbol (E9A6), ↶ ↷ links davon; Look-Presets dürfen `shipView.cameras` nicht mitnehmen (Zustand, nicht Look). Kamera-Änderungen sind kein `Step`.
- **Für K5 relevant:** Gespeicherte Kameras sind schiffsfest (Anker = projizierte Mitte von `Ship.Bounds`); ändert K5 die Origins (`offset`/`rotation`), verschieben sich gespeicherte Ansichten – Datenupdate, kein Bug (D76).
- **Offen:** Antwort von Torlek zu `limits.max = 1` und damit die `unverified`-Liste der Türen (32 Racks in 16 Schiffen, K3.1); Rastergrenze 12 px/Zelle bleibt, „höher, falls das volle C2-Raster im Spiel stört“ ist Beobachtung, kein Task.

---

## Phase 8 – nach dem Launch

### [ ] T8.1 Auto-Update – **Velopack** evaluieren (ersetzt T6.2-Publish durch Installer mit Delta-Updates)
### [ ] T8.2 winget-Manifest – `winget install SC-CargoPlan`
### [ ] T8.3 Eigenes OCR – **Windows.Media.Ocr** (in Windows eingebaut, kein Paket, keine 100 MB Modelle). Machbarkeitsstudie als eigener Task.

---

## Lehren

Regeln aus Fehlern, die durchgerutscht sind. Gelten für jeden Task.

- **Vor jedem Push `gh run list -L 3`.** Rote Läufe erst klären, dann pushen. Anlass T7.1d: Die CI war seit T7.0 rot (1-s-Zeit-Assert in `OrderSearchLimitTests`, 1.099 / 1.297 ms auf den Runnern), unbemerkt über mehrere Pushes.
- **Tests vor der Implementierung heißt: erst rot sehen.** Neue Tests vor dem Code laufen lassen und das Scheitern (Compile- oder Assert-Fehler) festhalten, dann implementieren. Anlass T7.2: Die Core-Tests (Replan, PlanFile, Katalog) wurden vor dem Code geschrieben, aber erst danach ausgeführt; in der App lief es richtig.

---

## Backlog (nur bei Bedarf)

Kein Plan, sondern Reaktion: wird erst zum Task, wenn der Anlass eintritt.

- `CODE_OF_CONDUCT.md`, sobald es Contributors gibt; Community Standards mahnt es an, ein Solo-Repo braucht es vor dem ersten Hauler nicht (T6.4).
- ~~App-Icon (`.ico` mit 16/32/48/256 px, `<ApplicationIcon>` im App-csproj), sobald ein Icon vorliegt; aus T6.2 (D48).~~ → K5
- Topmost-Fallback (`SetWindowPos(HWND_TOPMOST)` per Timer) nur bei Issue-Report mit Repro. Im T3.1-Test blieb das Overlay über Borderless-SC (D13).
- ~~Rack `orientation` (physische Stapelrichtung, Rotation pro Rack), sobald ein Schiff es braucht (D14).~~ Stapelrichtung erledigt in T3.13c (`stackAxis`, D26); freie Rotation pro Rack erst, wenn ein Schiff sie braucht.
- Viewport3D-Renderer auf denselben Geometriedaten, falls 2.5D nicht reicht (D14).
- ~~Ship mesh outline (Schiffsumriss): erst nach Klärung von Asset-Pipeline, Rechtslage bei Spieldaten und Render-Kosten im Layered Window (D14).~~ Übernommen als T7.4 (D55).
- ~~Umriss in 3D wie im Maintenance Panel (Prisma aus Draufsicht + Höhe, grober Rumpf aus Drauf- und Seitenansicht, oder Holo-Modelle), falls der flache Umriss im Spiel nicht reicht (D62).~~ Holo-Modelle übernommen als T7.4b (Spike) und T7.4c.
- ~~Kamera, Zoom und Pan der Schiffsansicht in `settings.json`, falls das Zurücksetzen beim Neustart nervt (D15).~~ → K1
- Orbit-Snap auf feste Winkel (z. B. 45°-Raster per Taste), falls freies Drehen schwer wiederholbar ist (D15).
- ~~Plan verwerfen ohne neuen Plan („Discard plan“), falls ein leeres Overlay nach der Route gewünscht wird; heute ersetzt nur „Plan“ den alten Plan (D19).~~ → K2
- Editor-Position in `settings.json`, Entwurfs-Autosave, Undo im Editor, falls der Zwei-Minuten-Durchlauf daran scheitert (D19). Undo-Teil → K2.
- ~~Rückfall auf Segmente ab einer Zellenzahl, sobald ein großes Schiff (Hull C, ~4.600 Zellen) beim Orbit ruckelt (D21).~~ Gestrichen: erledigt mit T7.3.
- ~~Spindelmuster für Hull B/C wie bei Hull A statt der generierten Reihe (Daten, T7.1b-Nachtrag), sobald die echte Anordnung
  bekannt ist (D61).~~ → K5
- ~~Nasenlänge des Bug-Keils auf die Schiffsbreite deckeln (Hull C 14 → 6 Zellen, ~10 % mehr Maßstab), falls lange Schiffe
  im Spiel zu klein wirken (D61).~~ Gestrichen: der Keil ist seit T7.4 nur noch Fallback ohne Umriss.
- ~~Rastergrenze über 12 px (z. B. 20), falls das volle Raster der C2 (18 px pro Zelle) im Spiel stört (D61).~~ → K1
- ~~Kistengrößen als Blöcke und Zellbeschriftung in der Schiffsansicht (D21).~~ Gestrichen: erledigt durch T3.11b.
- Backdrop-Blur hinter dem Fenster (Spike: DWM vs. AllowsTransparency) (D28). Auslöser: Hauler-Report „Legende trotz Halo unlesbar“.
- ~~Alternative Stop-Paletten, nur geprüft farbfehlsicht-tauglich (D29).~~ → K4
- ~~Legenden-Checkboxen: Farbe und Deckkraft in den Preferences wählbar (Abschnitt Legend; heute fest grau `#B0B0B0`, `MissionPalette.CheckBoxBrush`, D69). Auslöser: Taste-Gate B6.~~ → K4
- ~~Theme-Presets/Export (A4).~~ → K4
- ~~Füllrichtung in Mission-Dateien (Export/Import), falls Koordinatoren sie mit der Route teilen wollen; heute nur über plan.json (Core-Formatwechsel, D24-Addendum).~~ → K2
- ~~Formbewusste `Partition` (Racks nach legbarer Anordnung in Routenreihenfolge, nicht nur nach SCU), falls Stapel-Befunde in echten Plänen häufig sind. Beleg aus dem Spike T5.4 (Brute-Force, abgebrochen): 3 Stops in einem Rack → 207 Aufteilungen ohne Anordnung bis Mischung 146/186; 4 Stops → 397 bis Mischung 65/170 (D24-Addendum, T5.4a).~~ → T7.0
- ~~Geometrie bei Schiffen, deren Grid kein Vielfaches der Kistenmaße ist (D9 gilt nur für Hull A), sobald T7.1 ein solches Schiff bringt: `Plan` verteilt nach SCU und scheitert erst beim Legen, die Meldung („64/75 SCU“) klingt nach Platz und nennt nur Rack 1. `PlaceProblem` sagt es schon besser („no room for 8 SCU crates“). Beleg: S7.0, Lauf 3.~~ → T7.0
- ~~Stapelbewusste Partition: Racks so verteilen, dass keine Stapel-Befunde entstehen (Rest der übernommenen T7.0-Zeile), falls Stapel-Befunde in echten Plänen häufig sind (D54). Beleg T7.2: echte CT-Captures (High Course Station, 11 SCU) per `PlaceInto` in den Hull-A-Plan split_and_shared → 3 rote Kisten auf Riker/Orison/Lorville.~~ Erledigt in B2 (D64): Prio 3 wählt auch zwischen Zuordnungen, `PlaceInto` nimmt das Rack ohne Fremdauflage.
- ~~Ein Ziel pro Rack auf Mehrrack-Schiffen, wenn die Last es zulässt (Partition nach SCU füllt heute Rack 1 mit allen Zielen, C2: sechs Ziele in Rack 1). Auslöser: Hauler eines Mehrrack-Schiffs meldet, dass er ein Ziel pro Rack will. Planer-Änderung, D6-Addendum. Beleg T7.2: Legende „Rack 1 shared by Riker, Shubin, High Course Station, Melodic Fields Station, Faithful Dream Station, Everus Harbor – identify crates by count“; auf einem 480-Zellen-Rack hilft „by count“ nicht mehr.~~ Erledigt in B2 (D64): Prio 2 „wenigste Ziele im vollsten Rack, dann wenigste geteilte Racks“.
- ~~Frachtmodule (Aurora-Heckmodul, Retaliator, Caterpillar `module_01–04`) als zusätzliche Katalogeinträge aus einer Datei, ohne Änderung an Plan-Schema oder Auswahl (D59). Auslöser: erstes Schiff in `ships/` mit im Spiel wechselbarem Frachtmodul.~~ → K5
- Starbreaker (Fleetyards-Extractor, Rust) als Quelle für die Gitter von Frachtmodulen. Auslöser: zweites Schiff mit Frachtmodul.
- ~~Haken eines Altbesuchs beim Schiffswechsel erhalten: ein abgehakter Stopp, der danach neue Fracht bekam (D41), kommt nach `Replan` auf einem anderen Schiff unabgehakt zurück und belegt Platz (D59); auf dem eigenen Schiff erledigt in B4 (D66). Auslöser: Meldung eines Haulers.~~ → K2
- Sync Ships in der App (Fleetyards-API), falls sich Frachtgitter häufiger ändern als Releases erscheinen oder Hauler zwischen Releases fehlende Schiffe melden. Voraussetzungen: D-Eintrag Netzwerkzugriff des Overlays, Lizenzfrage für Laufzeitabrufe, Merge-Logik aus import-fleetyards.ps1 nach C# (T7.1b).
- Abholorte von Waste-Verträgen aus den Zeilen „Collect Waste from …“ des Rohtexts lesen (die Stationen), statt nur aus ContractTrackers `PickupLocation`, das einen davon zerrissen nennt (B6, D69). Auslöser: Hauler will Waste-Missionen nach Abholort unterscheiden.
- ~~Kisten horizontal drehbar (Gier 90°) im Planer, statt Racks beim Import zu drehen (T7.1b: `length` = längere Seite, Rack in `notes` genannt). Auslöser: ein Schiff, dessen Rack nur mit gedrehten Kisten so voll wird wie im Spiel, oder ein Hauler-Report über abgelehnte Kisten, die im Spiel passen.~~ → K3.2
- `CrateLayout.InOrder` beschleunigen (`StartingAt` ohne LINQ, Pruning), Auslöser: ein echter Plan mit der Notiz „too many crates to rearrange“ (D54). Erwartet bei Racks ab ~80 Zellen mit vielen 1-SCU-Kisten (T7.1c: 9×3×3, 32 + 40 × 1).
- ~~Planen in `Task.Run` statt im UI-Thread (`MissionEditorViewModel.Plan`, CT-Sync, Schiffswechsel), Auslöser: ein gemeldetes Einfrieren (D54) oder der erste Hauler-Report zur Planungsdauer. Messwert T7.2: 8-Stopp-Lastfall auf der Freelancer ~4 s Release (Suchlimit, D58), beim Editor-Plan wie beim Wechsel (`ReplanTests`, `Slow`). Ein UI-Task (Knopf gesperrt, „Planning…“), kein Planer-Task; kann als erster nach dem Launch kommen. Bis dahin Known issue (T7.5).~~ Geschlossen in B4 (D66): seit B2 plant jedes Fixture im Prozess in höchstens 3,2 ms (Median, erster Lauf 7 ms, Freelancer-Lastfall 2,3 ms, `ReplanTimingTests`); nur die Stresslast (kein Schiff) braucht 80 ms. Wiederöffnen bei einem gemeldeten Einfrieren.
- Optionale Spieler-Reihenfolge (Legende per Drag sortieren) → Stapelhinweise relativ zur Spieler-Reihenfolge („Shubin liegt auf Riker, Riker kommt zuerst“). Auslöser: Hauler wollen wieder wissen, welche Kiste zuerst runter muss (D64).
- B1-Restfall: abgeschnittenes Fragment ohne Ergänzung. Capture 21–25 (24.10.): „to Thur“ bricht um, OCR liest „Thundering“ als „Thur…“, „Melg“ entsprechend; die Ergänzung aus der Drop-off-Liste greift nicht, weil das Fragment kein Präfix ist. Auslöser: weitere Fälle in echten Captures.
- ~~Hit-Test der Schiffsansicht auf ihre Fläche begrenzen: gezeichnet wird mit `ClipToBounds`, getroffen nicht, also kann ein Drop über der Legende ein beim Hineinzoomen abgeschnittenes, unsichtbares Rack treffen (D33).~~ → K1
- ~~Undo für Drags und Re-plan: ab Stufe 3 (D65) ist Zurückziehen kein Undo mehr, ein Re-plan (B4) verwirft Moves. Auslöser: ein Hauler verliert eine Lage durch einen Drop.~~ → K2
- Mengen pro Abholort („collect 3 from A, 2 from B“) statt einer Ortsliste ohne Mengen (D68). Auslöser: CT oder das Spiel
  nennen die Teilmengen.
- Kisten anderer Missionen ausblenden statt dimmen (D68). Auslöser: Martin oder ein Hauler will es nach dem Spieltest.
- Abholort ändern ohne Re-plan (heute braucht jede Editor-Änderung „Plan“, D19). Auslöser: ein Hauler trägt Abholorte erst
  nach dem Beladen nach und verliert dabei Moves.
- Mehrsprachigkeit, falls nicht-englische Hauler danach fragen. Beginnt beim Parser (deutsche CT-Captures als Fixtures), nicht bei `.resx` (D53).
