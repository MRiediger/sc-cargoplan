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

### [ ] T3.13 Positionen
**Warum:** Platzhalter – Text folgt (D24).

### [ ] T3.14 Ziehen (Auswahl und Gesten)
**Warum:** Mehrere Kisten oder ein ganzes Ziel in einem Zug umplanen. (Früher T3.11c.)
- Auswahl = Menge von Kisten-Ids im `ShipViewControl`, nicht persistiert; Rechtsklick auf Kiste = umschalten, auf Leerfläche = leeren
- Rechts-Ziehen von einer Kiste = `MoveCrates` (Auswahl, wenn die Startkiste darin ist), von einer Legendenzeile = `MoveStop`
- Auswahlrahmen `MissionPalette.Selection`; gesperrt leert die Auswahl
- Entscheidungseintrag (nächste freie Nummer), README-Bedienung neu

**Akzeptanz:** `dotnet test` grün; Verifikation im Overlay und im Spiel am Aufzug (Task-Text T3.11).

Phase 3 ist mit T3.14 nummeriert komplett; offen bleiben nur Backlog-Punkte.

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
- Mapping skizzieren: Spalten → `MissionStop.Destination`, `MissionItem.Commodity`, `Scu`, `MaxCrateSize` (D23). Was fehlt?
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
- Kamera, Zoom und Pan der Schiffsansicht in `settings.json`, falls das Zurücksetzen beim Neustart nervt (D15).
- Orbit-Snap auf feste Winkel (z. B. 45°-Raster per Taste), falls freies Drehen schwer wiederholbar ist (D15).
- Hotkey-Aufnahme-UI (Tasten drücken statt tippen) und damit das Zahnrad in der Kopfzeile, sobald settings.json-Editieren Nutzer abschreckt (D17).
- Plan verwerfen ohne neuen Plan („Discard plan“), falls ein leeres Overlay nach der Route gewünscht wird; heute ersetzt nur „Plan“ den alten Plan (D19).
- Editor-Position in `settings.json`, Entwurfs-Autosave, Undo im Editor, falls der Zwei-Minuten-Durchlauf daran scheitert (D19).
- Füllrichtung der Zellen konfigurierbar; geteilt zwischen Koordinator und Haulern gehört sie in `plan.json`, als persönliche Vorliebe in `settings.json` (D21).
- Rückfall auf Segmente ab einer Zellenzahl, sobald ein großes Schiff (Hull C, ~4.600 Zellen) beim Orbit ruckelt (D21).
- Rack-Nummern und Linien beim Zoom konstant klein halten statt mitzuskalieren (D21).
- Kistengrößen als Blöcke und Zellbeschriftung in der Schiffsansicht (D21).
