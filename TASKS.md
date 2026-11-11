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

### [x] T5.5 Settings-Abschnitt „ContractTracker“ (erledigt, Häkchen nachgetragen in K10; Commits b67aa54, b2b33e2)
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

### [x] T6.5 README final (erledigt, Häkchen nachgetragen in K10; Commits 1bfd3ba…c0aa7da, Bilder und Tag mit T7.5/T9.2)
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

### ~~[ ] T6.6 Nexus Mods~~ → T9.3 (dort weitergeführt, „= T6.6“; gestrichen in K10)
**Warum:** Viele Hauler finden Tools auf Nexus, dort liegt auch ContractTracker.
- Nach dem GitHub-Tag, gleiches ZIP wie im GitHub-Release
- Permissions aus der MIT-Lizenz abgeleitet
- Credit für ContractTracker

**Akzeptanz:** Nexus-Seite online mit dem ZIP des GitHub-Releases (gleiche SHA256), Permissions passend zu MIT, CT-Credit sichtbar.

→ Umgesetzt als T9.3 (Phase 9, D87).

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

### [x] T7.4d Mission und Abholort sichtbar machen (D68) – vor T7.5 (erledigt, Häkchen nachgetragen in K10; Commits 6012d2a…91af570)
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

### T7.5 README, Wiki, Medien – in fünf Schritten (27.10.2026)
**Warum:** Release-Doku vor dem Tag `v0.1.0`. Die README wird eine Bildschirmseite, die Nutzerdoku zieht aus
`docs/user-guide.md` ins GitHub-Wiki (`sc-cargoplan.wiki.git`, lokal neben dem Hauptrepo), gegliedert nach Nutzerfragen.
Branch `docs/release-v0.1.0`.
- Reihenfolge (23.10.2026): Umriss vor Doku, T7.4 und T7.5 getauscht (D55-Addendum). Die Screenshots zeigen den
  Endzustand, Doku zuletzt vermeidet einen dritten Tausch. Präzisierung in der Planphase, ratifiziert durch Freigabe.
- Alte Unterpunkte von T7.5: Schiffsliste → Seite `Ships-and-cargo-grids` ohne Liste aller Schiffe (Verweis auf die Auswahl
  im Overlay und `ships/`); Known issue „Entwurf“ → `Troubleshooting`; Bild-Platzhalter → T7.5c; Known issue
  Planungsdauer entfällt (geschlossen in B4, D66); Credit Erec entfällt (Abgleich mit dem Guide fiel weg, D53-Addendum
  T7.1b); Ship-Template „Ship correction“ → T7.5c. Präzisierung in der Planphase, ratifiziert durch Freigabe.

#### [x] T7.5a README und Wiki-Texte (Claude Code) (27.10.2026, abgenommen 28.10.)
**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Wiki-Seiten Englisch nach dem Seitenplan (12 Seiten +
`_Sidebar`, `_Footer`); Shotlist und Testplan bleiben dauerhaft im Hauptrepo unter `docs/shots/` (Nachdrehen bei jedem
Release), nicht im Wiki; Shotlist mit Prio must/nice; FAQ mit der Frage zu Spiel und EAC, nur was die App technisch tut;
durchgehend „cargo elevator“; README „Windows 10/11 x64 (tested on Windows 11)“; die Re-plan-, Farb- und
Header-Tooltip-Texte zitiert das Wiki in der Zielfassung von T7.5a2.
- README: Satz, Hero-Platzhalter, 6 Features, Install in 3 Schritten + Borderless, Requirements, Documentation,
  Credits & disclaimer (Fleetyards, ContractTracker, Paletten, „Not affiliated …“, MIT und die zwei NOTICE-Dateien).
  Settings, Rettungsweg, Startschalter, Known issues und Bug-Meldung ziehen ins Wiki, Build bleibt in `CONTRIBUTING.md`.
- Wiki: Home, Installation-and-first-start, Quick-start-at-the-cargo-elevator, Overlay-basics, Plan-and-editor,
  Ship-view, Legend-and-delivery-states, Preferences, Ships-and-cargo-grids, ContractTracker-integration,
  Troubleshooting, FAQ; Medien als `![…](media/<datei>)` + `<!-- SHOT: <Id> -->`, Hero per Raw-URL aus `docs/images/`
- `docs/shots/SHOTLIST.md` (31 Medien: 14 GIF, 17 PNG; 14 must) und `docs/shots/testplan.json` (Hull A, drei Missionen
  aus Lorville, Rack 4 frei; per `CargoPlan.Cli plan` geprüft)
- Medien-Test (27.10., Testseite gepusht und wieder entfernt durch Martin): `![…](media/test.png)` rendert, GitHub leitet
  `/wiki/media/…` per 302 auf `raw.githubusercontent.com/wiki/MRiediger/sc-cargoplan/media/…` (200 `image/png`); die
  Raw-URL des Hauptrepos (`…/main/docs/images/…`) liefert 200, Seitenlink `[Home](Home)` geht. `curl` ohne
  Browser-User-Agent bekommt statt der Seite eine Umleitung auf Home: Prüfungen mit `-A`.
- Wiki lokal committet (`37be77f`), nicht gepusht; Hauptrepo `066cd89` (Branch von Martin gepusht, `main` unverändert)

#### [x] T7.5a2 Text-Fixes vor den Aufnahmen (Claude Code, eigener Plan) (27.10.2026, abgenommen 28.10.)
**Warum:** Sichtbare Texte, die sonst in den Aufnahmen von T7.5b einfrieren. Gleicher Branch, eigener Commit.
- Re-plan-Texte (Header- und Editor-Tooltip aus einer Quelle, Dialog, Schiffswechsel) nennen, dass geladene Fracht
  liegen bleibt; durchgehend „Re-plan“ statt „Replan“/„replanned“
- UI-Texte durchgehend amerikanisch („color“), passend zu den Settings-Keys
- Tooltip unter Settings › Header nennt alle Glyphen
- **Reset to defaults** in Ship view lässt `downloadShipHolograms` unverändert
- „Row {n}“ bei sortierter Ansicht und „Re-plan found no better layout“ bei `PlanException` zuerst nachstellen: bestätigt
  → Fix, sonst Zeile streichen
- Verifikation: jeder Zieltext, den das Wiki zitiert, per Grep gegen `src/` wörtlich gleich; weicht ein String vom Plan
  ab, wird die Wiki-Stelle im selben Commit nachgezogen

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** `ThemeSampleWindow.xaml` (nur Debug) ebenfalls „color“;
Bug 7 mit Regressionstest ohne Sortierung und einem mit getippter Zeile hinter den CT-Zeilen (Sortierung nach Mission);
der Reset-Knopf in Ship view bekommt den Tooltip „Reset the view settings to defaults. Download ship holograms stays as it
is.“ (fängt das Gegenargument des D63-Addendums in der UI auf); roter Commit und Fix werden gemeinsam gepusht.

**Befund:**
- Rot vor dem Fix: 14 von 1.435 App-Tests (Re-plan-Tooltip und -Frage, Editor-Tooltip per `x:Static`, „Re-plan for“ und
  „re-planned for“, „color“ im Settings-Fenster, im Theme-Muster und in den Farb-Notices, Header-Tooltip, Ship-view-Reset
  samt Tooltip, „Row n“ sortiert nach Ziel und nach Mission). Grün schon vorher: „Row n“ ohne Sortierung.
- **Bug 7 bestätigt und behoben:** „Row n“ und die erste ungültige Zeile folgen der Grid-Reihenfolge (`RowsView`, ohne den
  Platzhalter der neuen Zeile); `MissionRows.RowProblem` formatiert für beide Wege, ein Sortierwechsel meldet `Problem`
  und `PlanToolTip` neu.
- **Bug 8 nicht bestätigt, gestrichen:** Core-Test `Replan_lays_the_rest_of_a_partly_loaded_mission_around_the_loaded_cargo_without_giving_up`
  (Rest 16 + 8 einer teilweise geladenen Mission, Schiff voll) war schon vor dem Fix grün, `PlanKeeping` wirft dort nicht.
  Der `PlanException`-Zweig behält einen Plan, der schon alles an Bord hält; „Re-plan found no better layout“ stimmt dann.
- Neu `VisibleTextXamlTests` (vier Fenster: kein „colour“, kein „replan“ in sichtbaren Texten). Im C#-Code bleibt
  „colour“ nur in Bezeichnern und Kommentaren.
- Doku: `settings-format.md` (Glyphenliste :94, Ship-view-Reset), `plan-format.md` („re-planned for“), D63-Addendum;
  Wiki `Preferences` (Reset-Tooltip). Alle Zieltexte, die das Wiki zitiert, wörtlich in `src/`.
- Tests: Core 1.466 → 1.467, Import 141, App 1.424 → 1.435; 0 Warnungen.

#### [x] T7.5b Aufnahmen nach Shotlist (Martin) (28.10.2026)
- `docs/shots/SHOTLIST.md`, zuerst alle must-Shots
- Ergebnis: alle 31 Shots plus `delete-mission.gif` (D12, nice); `prefs-color-picker` und `shipview-hologram` als GIF statt
  PNG; `legend-states.png` = `legend-ct-markings.png` mit Absicht (zeigt beide Zustände und eine `?`-Markierung).

#### [x] T7.5c Platzhalter ersetzen, Wiki pushen (Claude Code) (28.10.2026, abgenommen 28.10.; Push und Wiki-Sperre nach dem Merge)
- Medien ins Wiki-Repo `media/`, `hero.gif` nach `docs/images/`; Größen (≤ 3 MB) und Links prüfen; fehlende nice-Shots:
  Platzhalter- und SHOT-Zeile entfernen, Text bleibt
- `docs/user-guide.md` löschen; Bruchstelle `docs/settings-format.md:70` (Link auf `user-guide.md#hotkeys`) aufs Wiki
  umstellen
- Doku-Abweichungen aus T7.5a: `settings-format.md` („Example with every default“ mit Werten statt `null`; die
  Glyphenliste :94 erledigt T7.5a2), `bug.yml` (DB-Kopie auf den Desktop statt in den Programmordner), `ship-format.md:7-8` und :63
- Ship-Template: Korrektur-Template bzw. Umbenennung „Ship correction“, vor dem Tag; Wiki-Link auf `template=` nachziehen
- **Reihenfolge:** erst Hauptrepo mergen (`--no-ff`) und pushen, damit `hero.gif` auf `main` liegt; dann Wiki pushen;
  dann Wiki-Editing auf Collaborators beschränken (Repo-Settings)

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** `hero.gif` vor dem Merge auf ≤ 5 MB (bleibt dauerhaft in der
Git-Historie), Ergebnis zur Sichtprüfung; eigenes Template `ship-correction.yml` („Ship correction“), „Ship request“ bleibt
für fehlende Schiffe; Known issue Hull-C-Hologramm gestrichen; `docs/screenshots/` gelöscht, D1 verweist aufs Wiki; D12
`delete-mission.gif` mit Prio nice; `bug.yml` folgt der Wiki-Anleitung (CT-DB in den Programmordner); die
`<!-- SHOT: … -->`-Zeilen bleiben stehen (verbinden jedes Bild mit der Shotlist beim Nachdrehen).

**Befund:**
- Medien: ScreenToGif-Export mit Spielhintergrund 532 MB (2176 × 1241, 22 s): bewegter Hintergrund, „Unveränderte Pixel“
  greift nicht. Mit ffmpeg (Ausschnitt Vertragsziele + Overlay, 720 px, 128 Farben, kein Dithering):
  `quickstart-capture-sync.gif` 10 s / 8 fps = 10,1 MB (Ausnahme, nur Wiki), `hero.gif` 8 s / 6 fps = **6,2 MB**. ≤ 5 MB
  ging ohne gifsicle (nicht per winget) nur mit 64 oder 96 Farben, dabei verschwinden Grün und Blau der Stopfarben;
  Martin nimmt 6,2 MB in Kauf. Stopfarben pixelgleich zum 10-MB-Stand. `shipview-orbit-zoom.gif` 5,9 → 2,6 MB,
  `shipview-hologram.gif` 10,4 → 2,9 MB; Parameter in den Konventionen der Shotlist.
- Doku: `settings-format.md` (Default-Beispiel `overlay`/`hotkeys` `null`, Link aufs Wiki), `ship-format.md:7-8`, :63,
  `bug.yml`, `ship.yml` (Hinweis auf Ship correction), neu `ship-correction.yml`; `docs/user-guide.md` und
  `docs/screenshots/` gelöscht, kein Verweis mehr darauf.
- Check-Skript (Scratchpad): Seiten- und Ankerlinks, jede eingebundene Datei vorhanden, jede Datei in `media/` eingebunden,
  ≤ 3 MB außer den zwei Ausnahmen, Shotlist 1:1 mit Typ und Datei (32 Shots: 17 GIF, 15 PNG; 14 must); Negativtest
  schlägt an. Tests Core 1.467, Import 141, App 1.435, 0 Warnungen.

#### [x] T7.5d Release (Martin + Claude Code) → T9.2 (D83), erledigt mit T9.2
- Release-Notes `docs/release-notes/v0.1.0.md`, Tag `v0.1.0`, rc-Releases und -Tags löschen (D49)

→ T6.6 Nexus und Fleetyards-Verlinkung, seit D83 in Phase 9 (T9.3, T9.4)

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

Fünf Sessions aus dem Backlog vor dem Tag, Reihenfolge **K3 → K2 → K1 → K4 → K5 → T7.5 → Tag v0.1.0**; K6 (Aufzug-Flow) eingeschoben vor T7.5, abgeschlossen 26.10.2026. Je Session ein Branch ab
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

### [x] K3.1 Türlimit pro Rack (D70) (25.10.2026) (erledigt, Häkchen nachgetragen in K10; Commits 69ee7da…2839b27)
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
- [ ] Antwort von Torlek zur 1 → Liste der ungeprüften Türen übernehmen oder streichen – bleibt offen (Martin, 26.10.), Known issue für T7.5; Stand nach K5: 36 Racks in 18 Schiffen
- [x] Gate Spiel-Build (Martin, 26.10., bestanden): Clipper laden, CT-Sync mit einem Vertrag über 2 SCU Kistengröße, Notice und Legende prüfen
- [x] ~~Gate Kiosk (Martin, K3): am Terminal bestätigen, dass der Kiosk bei einem Vertrag mit Maximum 8 auch 2er anbietet; widerlegt → Kappung nur bei Türlimit, P3 zurück (Folge-Commit), Ergebnis in D70~~ Entfällt (Martin, 26.10.): Der Frachtaufzug gibt aus, was er will, der Spieler wählt keine Kistengrößen; P3 bleibt, Notiz in D70
- Commits f3c4e63 (Daten, Import), f815210 (Core-Tests rot), 69ee7da (Core), 538b6c9 (App-Tests rot), 72a6115 (App), 25ac668 (Import aus `limits.max.capacity`), 84b2cc3 (Türen aus dem Index vom 25.10.), Doku-Commit.

### [x] K3.3 Untergrenze `minCrateSize` pro Rack (D70-Addendum) (25.10.2026) (erledigt, Häkchen nachgetragen in K10; Commits 32b5dd2…0892454)
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
- [ ] Gate Overlay (Martin, kein Spieltest): Hull C wählen, 16er-Vertrag per CT-Sync → Mission in der Legende wartend mit `doesn't fit (ship takes 32 SCU crates only)` – offen (Martin, 26.10.): ContractTracker liefert ihm keine Mission mit 16er-Kisten; der Fall ist durch `MinCrateSizeOverlayTests` abgedeckt
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

Reihenfolge innerhalb von K4 (Branch `feat/k4-look`): K4.1 Checkbox/Haken → K4.2 Stop-Paletten → K4.3 Look-Presets mit
Export/Import. K4.3 zuletzt, weil es exportiert, was K4.1 und K4.2 an Keys anlegen. Nur App, Core unberührt; Schemas
bleiben (Schiff 5, Plan 6, Mission 3, settings 2 mit neuen optionalen Keys); neu ist die Look-Datei mit Schema 1.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (für K4.1–K4.3):** P1 „Chevron“ ist der Haken in der
Checkbox, der Strich für „teilweise“ nimmt die Haken-Farbe; P2 zwei Keys statt vier (`checkBoxColor`, `checkMarkColor`,
Deckkraft = Alpha, D31) unter `appearance.legend`; P3 eine Auswahlfarbe der Checkbox gibt es nicht (angehakt = Haken-Farbe),
die Kisten-Magenta bleibt eine eigene Quelle; P4 Paletten Okabe-Ito 8 (unverändert), Tol Bright 6 ohne Grau `#BBBBBB`
(Korrektur K1 bei der Freigabe: Grau heißt im Overlay „erledigt“), Tol Muted 7 ohne Indigo `#332288` und Wein `#882255`
(auf dem dunklen Overlay unsichtbar, wie Okabe-Schwarz in D15), IBM entfällt (keine Primärquelle, Orange näher am Problemrot
als heute); P4b der Test prüft Nähe statt Gleichheit: ΔE76 ≥ 33,6 (heutiges Minimum Vermillion ↔ Problemrot) gegen
Problemrot und Delivered-Grau; P5 Key `appearance.crates.stopPalette`, ComboBox im Abschnitt Crates; P6 dunkler Unterstrich
unter Problem-, Auswahl- und DragTarget-Rand in jeder Palette; P7 `customStopColors` → Backlog; P8 Tooltip unverändert,
Ansicht und Legende holen ihre Pinsel aus einer Funktion; P9 `downloadShipHolograms` gehört nicht zum Look (Netzschalter, D63).

### [x] K4.1 Checkbox- und Haken-Farbe in den Preferences (D69-Addendum) (25.10.2026)
**Warum:** Backlog D69, Taste-Gate B6: Martin will Farbe und Deckkraft der Legenden-Checkboxen wählen, Kasten und Haken getrennt.

**Ist-Zustand vor K4.1 (A1):** `LegendCheckBoxStyle` (OverlayWindow.xaml) band Rahmen, Haken und Strich an `x:Static MissionPalette.CheckBoxBrush` (`#B0B0B0`); kein Test prüfte die Farbe. Einen Chevron zum Auf-/Zuklappen gibt es in der Legende nicht (B6 rückt nur ein); gemeint ist der Haken.
- Settings: `LegendAppearance(…, CheckBoxColor, CheckMarkColor)`, Default `#FFB0B0B0`, `AppearanceRules` prüft beide wie jede Farbe (Alpha 0 erlaubt)
- Theme: `LegendCheckBoxBrush`, `LegendCheckMarkBrush`; Template per DynamicResource (Rahmen / Haken und Strich); `MissionPalette.CheckBoxBrush` entfällt
- Fenster: Abschnitt Legend mit „Check box“ und „Check mark“ (`ColourField`, Picker, Alpha in Prozent), Reset Legend deckt beide ab
- Tests: `AppearanceRulesTests`, `ThemeResourcesTests`, `SettingsViewModelTests`, `SettingsStoreTests` (Rundreise), neu `OverlayLegendXamlTests`

**Akzeptanz:**
- [x] Rot gesehen (Compile-Fehler, 1a43821), dann grün
- [x] Defaults = altes Grau, ohne Datei sieht die Legende aus wie vorher; ungültige Farbe → Grau mit Notice; Alpha 0 erlaubt
- [x] Kasten und Haken getrennt einstellbar, jede Änderung wendet genau ihren Wert an; Reset Legend stellt beide auf Grau
- [x] Template bindet beide Keys per DynamicResource (XAML-Test)
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (App 1.235 → 1.241)
- [x] Gate (Martin, Publish-Build, 25.10., abgenommen): Werte ändern wirkt sofort in der Legende; Haken und Kasten unabhängig; Reset Legend stellt Grau her; Neustart behält die Werte
- Commits 1a43821 (Tests rot), b7c8d98 (App), 7b12355 (Doku).

### [x] K4.2 Stop-Paletten als Preset-Auswahl, Schraffur bei Wiederholung (D29-, D25-Addendum) (25.10.2026)
**Warum:** D29 hielt die Stop-Farben fest auf Okabe-Ito; Martin will eine Auswahl aus geprüften, farbfehlsicht-tauglichen Paletten (Stufe 2), und mehr Ziele als Farben sollen unterscheidbar bleiben.

**Ist-Zustand vor K4.2 (A1):** `MissionPalette.Colors` (8) und `Assign` mit `% Colors.Count` (die Runde ging verloren); Index → Pinsel an zwei Stellen (`ShipViewControl.Paint`, `PaletteBrushConverter` mit festem Alpha `0xD0`); Problem- und Auswahlrand 2,5 px ohne Absicherung; Muster nur als `SwatchCheckerBrush`. Eine neue Szene leert die Auswahl (D25), daher läuft die Palette nicht über die Szene.
- Settings: `StopPaletteName` + `StopPaletteNames` (WPF-frei, Namen `okabeIto`, `tolBright`, `tolMuted`), `CrateAppearance.StopPalette`, `AppearanceRules` (unbekannt → `okabeIto` mit Notice)
- ShipView: `StopPalettes` (drei Paletten mit Quelle), `StopPalette` (`ColorOf`, `HatchOf`, `RepeatNotice`, `Text`), `StopHatch`, `StopBrushes` (eine Funktion Ordnungszahl → Pinsel, gekachelter `DrawingBrush` in absoluten DIPs, Strich schwarz/weiß nach WCAG-Kontrast), `PaletteBrushConverter` als MultiBinding, neu `PaletteNoticeConverter`; `MissionPalette.Assign` ohne Modulo, `Colors`/`Brush`/`Repeats`/`RepeatNotice` entfallen; `ShipScene.StopCount` statt `PaletteRepeats`; `ShipViewControl.StopPalette` und dunkler Unterstrich (`EdgeUnderStroke`) unter Problem-, Auswahl- und Geisterrand
- App: `OverlayViewModel.StopPalette`, gesetzt von `SettingsSession.ApplyLook`; ComboBox „Stop colors“ im Abschnitt Crates (`StopPaletteChoice`)
- Tests: `StopPalettesTests` (Werte gegen Quelle, ΔE76 ≥ 33,6 gegen Problemrot und Delivered-Grau), `StopBrushesTests`, `PaletteNoticeConverterTests`, `PaletteBrushConverterTests`, `MissionPaletteTests`, Rules/Store/VM/Session, XAML-Tests; `StopHatchTimingTests` (Kategorie Timing)

**Zahl vorher/nachher:**

| Messung | Ergebnis |
|---|---|
| ΔE76 nächste Farbe zu Problemrot `FF2A2A` (Okabe / Tol Bright / Tol Muted) | 33,6 / 43,1 / 55,7 |
| ΔE76 nächste Farbe zu Delivered `5A5A5A` | 42,2 / 34,0 / 42,1 (Okabe-Weiß 61,8, Tol-Sand 62,4) |
| ΔE76 nächste Farbe zu Auswahl-Magenta / DragTarget-Cyan | 78,0 / 68,3 / 59,4 — 42,9 / 33,2 / 34,1 (Unterstrich) |
| Renderzeit C2, 16 Ziele, 64 Kisten, 800 × 600, Release, Median aus 30, drei Läufe | 16 Farben ohne Muster 24,7–25,9 ms; Okabe-Ito mit 8 schraffierten Zielen 25,8–26,3 ms |
| Standard-Look Hull A, 8 Ziele, offscreen gegen b7c8d98 | ohne Auswahl pixelgleich; mit Auswahl 4.420 Pixel anders, alle an den ausgewählten Kisten (Unterstrich) |
| Tests (Core / Import / App) | 1.401 / 141 / 1.241 → 1.401 / 141 / 1.281 |

**Akzeptanz:**
- [x] Rot gesehen (Compile-Fehler, 2ec27f9), dann grün
- [x] Drei Paletten, Werte gleich der Quelle minus dokumentierte Streichungen (Tol Bright ohne Grau, Tol Muted ohne Indigo und Wein); IBM entfällt
- [x] Jede Farbe jeder Palette ΔE76 ≥ 33,6 gegen Problemrot und Delivered-Grau (Test)
- [x] Zuordnung Ziel → Ordnungszahl bleibt beim Wechsel, nur Farbe und Muster der Zahl ändern sich; Szene und Auswahl bleiben (Session-Test)
- [x] Mehr Ziele als Farben: Schraffur „/“, dann „\“, in Ansicht und Legende aus derselben Funktion; Hinweiszeile nennt die Palette
- [x] Erledigte Kisten grau ohne Muster, Auswahl Magenta, Problem Rot, beide mit dunklem Unterstrich
- [x] Renderkosten der Schraffur gemessen (+0,4 bis 1,3 ms auf ~25 ms)
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (App 1.241 → 1.281)
- [x] Gate (Martin, Publish-Build, 25.10., abgenommen): Hull A mit 8 Zielen, Palette wechseln → alle Kisten färben sofort um; C2 oder Hull C mit mehr Zielen als Farben → Schraffur sichtbar und in der Legende identisch; Problemkiste und Auswahl in jeder Palette erkennbar
- [x] Taste-Gate (Martin, 25.10., abgenommen): Kachel 6 DIP, Strich 1,5 DIP, Unterstrich Schwarz `A0` 4,5 px; dazu der Wunsch, den Auswahlrand einstellbar zu machen → K4.4
- Commits 2ec27f9 (Tests rot), 0ea24d0 (App), 23a450a (Doku).

### [x] K4.3 Look-Presets und Export/Import (D77) (25.10.2026)
**Warum:** Backlog A4: Hauler tauschen Looks, Martin lädt für die T7.5-Screenshots ein definiertes Preset.

**Ist-Zustand vor K4.3 (A1):** Keine eingebetteten Ressourcen (Schiffe und Umrisse sind kopierte Dateien), keine Look-Gleichheit; `SettingsViewModel` konnte von außen nicht neu laden, `Batch` unterdrückt die eigenen Callbacks ganz; `SettingsSession.Save` überschreibt die Zeile `NoticeSource.Settings` nach jedem Speichern.
- Settings: `Look` (Record nur aus Skalaren und Records, `Of`, `Default`), `LookFile` (`Read` mit Schema-Prüfung vor den Keys, „fehlend = unverändert“ pro Key und Abschnitt, `Write` mit allen Keys; DTOs im `SettingsJsonContext`), `AppearanceRules.Normalize(Look)`, `LookPresets` (`Looks/*.cargoplan-look.json` eingebettet, `LogicalName looks/<datei>`, `WithCulture=false`)
- App: `SettingsViewModel.Look`, `SelectedPreset`, `PresetChoices`, `ImportLook`, `ExportLook`, `ApplyLook` (ein Batch, eigene Callbacks nur bei Änderung, Download-Schalter nie); `PresetChoice`; `NoticeSource.Look`; Kopfzeile im Settings-Fenster (Preset, Export…, Import…) außerhalb des Scrollbereichs, Dialoge im Code-Behind; Notice wird beim Schließen des Fensters gelöscht
- Tests: `LookFileTests`, `LookPresetsTests` (Default = Code = Export, Vollständigkeit, eingebettete Bytes = Repo-Datei), `SettingsViewModelTests` (Preset-Box, Import, Export, Notice), `SettingsSessionTests` (Import ändert nur den Look), `SettingsWindowXamlTests` (Kopfzeile)

**Akzeptanz:**
- [x] Rot gesehen (Compile-Fehler, 73910e3 und 47636aa), dann grün
- [x] Look-Grenze: Hotkeys, ContractTracker, Fensterposition, Kameras, Schiff, Recent und Download-Schalter bleiben beim Import unverändert (Session-Test)
- [x] Look-Datei Schema 1, Rundreise byte-gleich; fehlende Keys/Abschnitte unverändert; `cameras`, `hotkeys`, Tippfehler, falscher Typ → abgelehnt, nichts geändert; Schema 2 → „schemaVersion 2 is not supported (expected 1).“
- [x] Presets Default, High contrast, Minimal eingebettet; Default = Code-Defaults = Export
- [x] Preset-Box folgt jeder Änderung („Custom“), Preset wirkt sofort mit Notice „Preset applied: …“, Import „Look imported: …“, Fehler „Look not imported: …“
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (App 1.281 → 1.320)
- [x] High contrast und Minimal: von Martin abgenommen (25.10.), die Entwürfe bleiben die Presets
- [x] Gate (Martin, Publish-Build, 25.10., abgenommen): Preset wechseln → Overlay ändert sich sofort; Export → Datei (Name ohne Endung getippt: bekommt sie `.cargoplan-look.json`?); einen Wert ändern → „Custom“; Import der Datei → zurück; Import einer Datei mit `schemaVersion: 2` → Fehlermeldung, nichts geändert; Hotkeys und CT-Pfad nach Import unverändert
- Commits 73910e3 (Tests Datei rot), 0c79ccb (Datei, Presets), 47636aa (Tests Fenster rot), 8df8135 (Fenster, Import, Export), Doku-Commit.

### [x] K4.4 Auswahlrand einstellbar: Farbe, Breite, Kontur (D25-Addendum) (25.10.2026)
**Warum:** Martin beim Look-Gate K4.2: Farbe, Breite und Kontur der markierten Kisten sollen in den Preferences einstellbar sein.

**Ist-Zustand vor K4.4 (A1):** `ShipViewControl.SelectionPen` fest (`MissionPalette.Selection` Magenta, 2,5 px), darunter der feste Unterstrich aus K4.2 (`EdgeUnderPen`, Schwarz `A0`, 4,5 px), gemeinsam mit Problem- und Geisterrand.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** „markierte Crates“ = per Rechtsklick ausgewählte Kisten (Magenta-Rand); „Kontur“ = der dunkle Unterstrich, als Halo-Objekt (an/aus, Farbe, Breite je Seite); Problem- und Geisterrand behalten den festen Unterstrich (Zustände, keine Wahl); Ort Abschnitt Crates mit dessen Reset.
- Settings: `CrateAppearance.SelectionColor` (`#FFFF00FF`), `SelectionWidth` (2,5, 1–8), `SelectionOutline` (an, `#A0000000`, 1); `AppearanceRules` prüft sie wie die übrigen Kistenwerte
- Theme/Ansicht: `CratesSelectionPen`, `CratesSelectionOutlinePen` (Breite + 2 × Größe, runde Ecken, aus = Key entfernt); `ShipViewControl.SelectionPen`/`SelectionOutlinePen` per DynamicResource; `MissionPalette.Selection` entfällt
- Fenster: „Selection“ (Farbe mit Alpha), „Selection width“ (halbe Pixel), „Contour around the selection edge“ im Abschnitt Crates
- Presets: neu geschrieben, die drei Keys mit Defaults, alle anderen Werte unverändert (Martins Exporte von High contrast und Minimal sind byte-gleich mit den Entwürfen, Zeilenenden ausgenommen)
- Tests: Rules, Theme, VM, Store, XAML, `MissionPaletteTests`

**Akzeptanz:**
- [x] Rot gesehen (Compile-Fehler, 7b5e9d5), dann grün
- [x] Defaults zeichnen wie K4.2 (gleiche Stifte, gleiche Reihenfolge; nicht neu gerendert)
- [x] Farbe, Breite, Kontur einzeln einstellbar, Kontur abschaltbar; Werte außerhalb geklemmt, ungültige Farbe → Magenta mit Notice; Reset Crates stellt alle drei zurück
- [x] Look-Dateien tragen die drei Keys; Drift-Schutz der Default-Datei grün
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (App 1.320 → 1.328)
- [x] Gate (Martin, Publish-Build, 25.10., abgenommen): Kisten auswählen, Farbe, Breite und Kontur ändern → wirkt sofort; Kontur aus → nur der Rand; Reset Crates → Magenta über dunkler Kontur; Neustart behält die Werte
- Commits 7b5e9d5 (Tests rot), 17b1d60 (App), a14df49 (Doku).

### Abschluss der K4-Session (25.10.2026)
- Alle vier Tasks von Martin abgenommen (25.10.2026, Publish-Build): K4.1–K4.3 (e2cdf9e), K4.4 aus Martins Wunsch beim Look-Gate K4.2.
- Merge `--no-ff` nach main, Push durch Martin.
- Tests: Core 1.401 → 1.401, Import 141 → 141, App 1.235 → 1.328; Core unberührt, Exhaustive nicht nötig.

**Übergabe für den K5-Startprompt:**
- **Schema-Stand:** Schiff 5, Plan 6, Mission 3, settings.json 2. Neue optionale Keys (K4): `appearance.legend.checkBoxColor`, `appearance.legend.checkMarkColor`, `appearance.crates.stopPalette` (`okabeIto`, `tolBright`, `tolMuted`), `appearance.crates.selectionColor`, `selectionWidth`, `selectionOutline`. Neu: Look-Datei `*.cargoplan-look.json` Schema 1 (D77).
- **Look-Keys:** `appearance` komplett, `legend`, `toolWindowTheme`, `shipOutline`, `holoOpacity`, `holoHullColor`, `holoEdgeColor`, `shipView.maxZoomPxPerCell`. Nicht: Hotkeys, ContractTracker, Bounds, Kameras, Schiff, Recent, `downloadShipHolograms`.
- **Preset-Namen (für T7.5-Screenshots):** „Default“, „High contrast“, „Minimal“ (Dateien `default`, `high-contrast`, `minimal`). Ein neuer Settings-Key mit Default zwingt `Looks/default.cargoplan-look.json` nachzuziehen (Test `The_default_file_is_what_export_writes_for_the_defaults`); neue Look-Keys müssen auch in `Look`, `LookFileContent` und `SettingsViewModel.ApplyLook`.
- **Für K5 relevant:** Stop-Farben laufen über `OverlayViewModel.StopPalette`, nicht über die Szene; `MissionPalette.Assign` liefert Ordnungszahlen ohne Modulo.
- **Offen:** Antwort von Torlek zu `limits.max = 1` und die `unverified`-Liste der Türen (32 Racks in 16 Schiffen); Fortune, Moth (Gitter geändert, neue Racks) und Salvation (neu) aus dem Index vom 25.10.; `offset`/`rotation` → `origin`/`stackAxis` im Import; Aurora-Frachtmodul; App-Icon; Befund `MissionFrame` weiß = Okabe-Weiß (Backlog).

Reihenfolge innerhalb von K5 (Branch `feat/k5-data` ab `fa8276d`): K5.1 `fetch.ps1` → K5.2 Import `offset`/`rotation` +
voller Abgleich → K5.3 Aurora-Frachtmodul → K5.4 App-Icon (unabhängig, vorgezogen, sobald die Datei da war). Schemas
bleiben (Schiff 5, Plan 6, Mission 3, settings 2, Look 1).

### [x] K5.1 `fetch.ps1`: vollständiger Index, prüfbar (D53-Addendum) (26.10.2026)
**Warum:** Befund 26.10.: Die Javelin steht auf der Fleetyards-Website, aber nicht in der App; Verdacht: `fetch.ps1` liest nur die erste Seite von `/v1/models`.

**Ist-Zustand vor K5.1 (A1):** `fetch.ps1` (Pipeline-Repo `sc-ship-outlines`) las schon seitenweise (`perPage=100`, bis 20 Seiten, Abbruch bei leerer Seite). **Die Javelin stand im Index** (`cargo 5400`, `cargoHolds: []`) und fiel erst im Import als „no cargo grid“ heraus, wie 20 andere. Der Index vom 25.10. (118 / 97) war im Pipeline-Repo nicht committet; 117 / 96 ist der Index vom 22.10.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** K5.1 wird von „alle Seiten lesen“ zu „Vollständigkeit prüfbar machen“. `ships.json` bleibt ein JSON-Array ohne Kopf-Feld (drei Leser: `trace.py`, `bundle.py`, Import); Datum und Zählung stehen hier, das Exportdatum liest der Import weiter aus dem Datei-Zeitstempel. Filter `cargo > 0` bleibt (ohne Fracht kein Gitter), Filter Top-View bleibt (`trace.py` braucht `top.png`), wird aber gemeldet.
- Pipeline: `1af1c83` Index vom 25.10. gesichert (Zahl vorher); `6be0795` Seitengrenze 50 → Abbruch statt still, doppelter Slug → Abbruch, Fracht ohne Top-View → Warnung, Seitengrößen und Schlusszeile; `ef83dda` Korrektur nach Martins erstem Lauf (Abbruch auf Seite 4 mit `slug` = null): eine leere Seite kam als Hülle mit leerem `items`, die Wahrheitsprüfung nahm die Hülle als Modell (das alte Skript schleppte solche Hüllen still bis Seite 20 mit); jetzt nach dem Feld `items` statt nach seinem Wahrheitswert, `$null`/`''` = leere Seite, Modell ohne `slug` → Abbruch; `52a5d3f` Index vom 26.10.

**Zahl vorher/nachher:**

| Messung | Index 25.10. (`1af1c83`) | Index 26.10. (`52a5d3f`) |
|---|---|---|
| Modelle bei Fleetyards | – (nicht gezählt) | 248 (Seiten 100 / 100 / 48, Seite 4 leer) |
| mit Fracht + Top-View / mit Gitter | 118 / 97 | 118 / 97; 0 ohne Top-View verworfen |
| Slugs hinzugekommen / verschwunden | – | keine / keine |
| Schiffe mit geänderten `cargoHolds` | – | 0 (byte-gleich nach Normalisierung) |
| Schiffe mit `offset` an allen Holds | 9 | 9: C8 Pisces, C8X, Carrack, Caterpillar, Hull A, Hull B, Hull C, Starlancer MAX, TAC |
| Import-Abgleich (`import-fleetyards.ps1`, schreibt nichts) | 94 same, Fortune/Moth different, Salvation new | gleich; 21 ohne Gitter (u. a. Javelin, Hull D/E, Kraken, Odin), Exit 1 |

**Befund Rumpfmaße:** `length`/`beam`/`height` änderten sich bei 18 Schiffen (Avenger Titan/Renegade, C8/C8X, MPUV Cargo, Mustang Alpha, Nomad, Hull A 30 → 23,42 m, 325a, 350r, sechs Aurora Mk I, Zeus Mk II CL/ES). `ship-outlines.json` und `holo-v1` sind gegen die Maße vom 22.10. erzeugt und bleiben, wie sie sind (Pipeline-Arbeit). Auslöser für eine Neuerzeugung: Das K5.2-Holo-Gate zeigt Racks außerhalb des Rumpfs.

**Akzeptanz:**
- [x] `fetch.ps1` liest alle Seiten und bricht bei unvollständigem Index ab, statt still weiterzulaufen
- [x] Filter `cargo > 0` und Top-View im Kopfkommentar begründet, Verworfene werden gemeldet (0)
- [x] Zahl vorher gesichert, alt/neu verglichen, keine Schiffe verschwunden (also nichts zu melden oder zu löschen)
- [x] Gate (Martin, 26.10.): `fetch.ps1` nativ ohne Ausnahme, Seitengrößen 100 / 100 / 48 / 0, Schlusszeile; Stichprobe im Index: Clipper `limits.max` 2, Hull C `limits.min`/`max` 32 / 32
- Commits Pipeline `1af1c83`, `6be0795`, `ef83dda`, `52a5d3f`; Doku-Commit hier.

### [x] K5.2 Import: `offset`/`rotation` → `origin`/`stackAxis`, voller Abgleich (D78) (26.10.2026)
**Warum:** Fleetyards gibt für 9 Schiffe Lage und Drehung der Laderäume; der Import nutzte sie nur halb, die Hull C stand als Reihe von 16 Racks statt als Kreuze um die Spindel, und Fortune, Moth und Salvation aus dem Index vom 25.10. fehlten.

**Ist-Zustand vor K5.2 (A1):** `import-fleetyards.ps1` las `offset` seit K3.2 nur bei `-Create` (Min-Ecke, `origin = (Fy.y, −(Fy.x + Breite), Fy.z)`), ohne `rotation`; die Hull C überlappte so und bekam eine generierte Reihe. Der Abgleich verglich keine Lage, `-Force` verweigerte geänderte Rack-Sätze. Die Hull A war schematisch (1 Zelle zwischen den Platten, D14). Core plant ohne `Origin`/`StackAxis` (Planning-Namespace per Grep).

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 die Hull A nimmt den Fleetyards-Spalt (3 Zellen), der Spalt ist kein Kalibrierkriterium; P2 Gate fürs Vorzeichen ist die Starlancer MAX (die Hull B ist vorne/hinten gleich und kann es nicht zeigen); P3 Hull C behält 8×8×6/8×4×6, die Stapelachse folgt der Box (waagerechte Arme z, senkrechte y); P4 Carrack und Caterpillar-Nase bleiben generiert; P5 Ironclad Assault hat kein `offset`, kein Gate; P6 eigene Entscheidung D78. Dazu: Gate L bestanden (Vorzeichen bleibt, Fy y+ = Bug); Kistenkante der Hull A unter 24 px akzeptiert (D62-Addendum); beim gleichen Rack-Satz bleibt die Zuordnung über die Id (wie bisher, `Force_rewrites_the_grid_data_and_keeps_the_hand_work`), Id **und** Gitter nur bei geändertem Satz; `-Force` nennt Türen und Minima aus dem Export in den `notes` wie `-Create` (Befund Moth); die Salvation hat noch keinen Umriss (Bug-Keil, `NotTracedYet`); die Hull C ist kein langes Schiff mehr (D61-Addendum).
- Import: `rotation` 90/270 tauscht x- und z-Ausdehnung der Box (um Fy y), andere Werte → Abbruch; Box in Schiffszellen (Helfer statt Inline-Formel); Überlappung auf den gedrehten Boxen; Stapelachse per Port von `Rack.FrameOf` (`Get-Frame`, `Get-StackAxis`: die der Datei, sonst die erste passende); keine Position für Racks auf der Schiffsmitte; Abgleich der Lage bis auf eine gemeinsame Verschiebung (`Get-FileBox`, `Get-ArrangementText`), `hull_a` eingeschlossen; `-Force` legt Fleetyards-Schiffe (`Set-Arrangement`), geänderter Rack-Satz über `Update-Ship` (zugeordnete Racks behalten alles, neue hinter dem letzten großen, verschwundene gemeldet, nichts zuordenbar → neu); Generierte Reihe als `Get-RowOrigins`
- Daten: D1 `hull_a.json` Steuerbord y −4..−2 (von Hand, `notes`); D2 `-Force hull_c` (zwei Kreuze, `notes` zur Stapelachse von Hand); D3 `-Force fortune`, `-Force moth`, `-Create` (Salvation)
- Tests: `ImportFleetyardsScriptTests` (Drehung, `bad_rotation`, Lage gleich bei Verschiebung, Lage abweichend, `-Force` mit Achswahl, geänderter Rack-Satz, Neu-Erzeugung, Hull-A-Kalibrierung mit Gegenprobe, Tür-Hinweis), Fixtures `rotated.json`, `bad_rotation.json`, `with_hull_a.json` (vier echte Holds); `Hull_c_racks_form_two_crosses`; `A_ship_not_traced_yet_has_no_outline`; 13 Erwartungen an die Hull-A-Geometrie (3 Core, 10 App, `TestPlans.HullABeforeK52` für die zwei, die an der alten Geometrie gemessen wurden); `IsoProjectionTests` Hull C nicht lang

**Zahl vorher/nachher:**

| Messung | Vorher | Nachher |
|---|---|---|
| Tests (Core / Import / App) | 1.401 / 141 / 1.328 | 1.418 / 141 / 1.328 |
| Gleichheitslauf 4 Fixtures × alle Schiffe (gegen `c75cd5f`) | 384 Fälle, 176 geplant, 96 Schiffe | nach D1 und D2 je 384/384 byte-gleich; nach D3 388 Fälle, 178 geplant, 97 Schiffe: 380 gleich, Fortune 3 weiter abgelehnt (Kapazität 4 → 16 im Grund), Moth 4 geändert (2 jetzt geplant, 2 auf den neuen Racks), Salvation 4 neu |
| Import-Abgleich gegen den Index vom 26.10. | 94 same, Fortune/Moth different, Salvation new, Exit 1 | 97 same, Exit 0; Hull A same inkl. Lage |
| Schiffe mit Fleetyards-Lage | 5 (C8, C8X, Hull B, Starlancer MAX/TAC) | 7 (+ Hull A von Hand, Hull C) |
| Kistenkante Hull A, Default-Fit mit Umriss | 27,7 px (T7.4) | ~23,4 px (Skala × 0,844; 400×302: × 0,755), akzeptiert |
| rote Tests durch Geometrie | – | nach D1 13 (alle Geometrie, Stopp-Regel 20 nicht erreicht), nach D2 1 (Hull C nicht mehr lang), nach D3 1 (Salvation ohne Umriss) |

**Schiffe mit `offset`** (Index 26.10.): C8 Pisces, C8X, Carrack, Caterpillar, Hull A, Hull B, Hull C, Starlancer MAX, TAC; `rotation` 90 nur Hull C (8), 270 nur Carrack (9) und Caterpillar-Nase (2). **Geänderte Rack-Sätze:** Fortune 1 → 3 Racks (4 → 16 SCU), Moth 1 → 3 (32 → 224 SCU, neue Racks mit Tür 8); Salvation neu (6 SCU). **`unverified`-Stand:** 32 Racks in 16 Schiffen (K3.1) + Fortune rack-0 bis -2, Salvation rack-0 = 36 Racks in 18 Schiffen.

**Release-Notes-Stoff (T7.5):** Gespeicherte Ansichten der Hull A und Hull C sitzen nach dem Update anders (neue Rack-Lage, Datenupdate, D76); die Hull C zeigt ihre zwei Kreuze, die Hull A den echten Spindelabstand. Known issue: Bei der Hull C liegen die Racks innerhalb der Frachtblöcke des Hologramms, nicht deckungsgleich; das Raster ist das logische des Spiels, das Hologramm zeigt Rahmen und Abstände (D78, Martin 26.10.: Fleetyards bleibt).

**Befund beim Gate (26.10.):** Martin sah die Hull-C-Racks „zu eng und zu tief“ gegenüber dem Holo. Gemessen am Holo-Cache: Arm quer 10 m statt 7,5, radial 8,75 → 29 m statt 5 → 20, Blöcke längs ~12,5 m statt 10; das Holo ist auf Fleetyards' Länge 125 m skaliert (D63; die Rohmodelle haben keine einheitliche Einheit, ein Vergleich mit ihnen belegt nichts). Daneben: das **Hull-A-Holo ist 28 % zu groß** (`holo-v1` auf Fleetyards' alte Länge 30 m skaliert, Modell 23,4 m) → K5.2b.

**Akzeptanz:**
- [x] Kalibrierung: der Abgleich findet die ausgelieferte Hull A „same“ inkl. Lage; die Hull A von vorher (1 Zelle Spalt) meldet er als abweichend
- [x] Drehung 90/270 nach der Tabelle, andere Werte brechen mit Wert im Text ab
- [x] Hull C: zwei Kreuze, acht Arme je Kreuz, Gitter unverändert, Gleichheitslauf gleich
- [x] `-Force` mit geändertem Rack-Satz behält, was zuordenbar ist (Fortune, Moth); Salvation per `-Create`
- [x] Gleichheitslauf: Abweichungen nur auf Fortune, Moth, Salvation, jede benannt
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün; Slow grün; Exhaustive nicht nötig (Core-Code unberührt)
- [x] Gate L (Martin, 26.10.): Starlancer MAX über dem Holo stimmt, Belly-Racks vorn, Bucht hinten
- [x] Gate (Martin, Publish-Build, 26.10., bestanden): Hull C zeigt zwei Kreuze mit je acht Armen über dem Holo; Hull A gleiche Seiten/Ebenen mit breiterem Spalt; Fortune und Moth planen mit den neuen Gittern; Salvation wählbar
- Commits 48107db (D1), db183d3 (Tests rot), 215d3d9 (Import), a5f3e4f (D2), c704561 (Grenz-Hinweise), a05691d (D3), Doku-Commit.

### [x] K5.2b Holos und Umrisse aus dem Index vom 26.10. (D63-, D55-Addendum) (26.10.2026)
**Warum:** Befund beim K5.2-Gate: Das Hull-A-Holo ist 28 % zu groß. `holo-v1` und die Umrisse sind auf die Längen vom 22.10. skaliert, Fleetyards hat seitdem 18 Längen geändert; die Salvation hat weder Holo noch Umriss.

**Ist-Zustand vor K5.2b (A1):** Pipeline `sc-ship-outlines`: `trace.py` skaliert den Umriss mit Fleetyards' Länge, `holo_mesh.py flatten` liest die Länge aus `ship-outlines.json`; `trace.py`/`sheet.py` liefen mit nicht festgehaltenen cv2/numpy-Versionen (die Systemumgebung hat kein cv2). Die App lädt eine Holo-Datei neu, sobald ihr SHA256 vom Manifest abweicht, und holt das Manifest höchstens einmal am Tag.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Martin: jetzt, vor K5.3; Versionen für `trace.py` heute festschreiben statt nur benennen; Zahl je Umriss (Punkte, Fläche / Länge²). Assets im selben Tag `holo-v1` ersetzen (`--clobber`), kein `holo-v2`. Beim Lauf gefunden: `flatten` liest die Länge aus dem Bündel, also erst bündeln, dann Holos (die 325a lief vor dem neuen Bündel und wurde wiederholt); `TOOLS.md` nennt die Reihenfolge.
- Pipeline: `b3da084` (19 Umrisse in `.venv` neu getraced, Bündel, `TOOLS.md`: Pins, Reihenfolge, Zahlen); Holos 18 × `-Redo flatten` + Salvation komplett; Release-Ordner neu (`generatedAt` 2026-10-26)
- Repo: `outlines/ship-outlines.json` (byte-gleiche Kopie, SHA256 `C6ADEEC4…`), `NotTracedYet` leer, `Hull_a_outline_points_to_the_bow_and_is_in_metres` erwartet 23,42 × 9,38 m (vorher 30 × 12, × 0,781)

**Zahl vorher/nachher:**

| Messung | Vorher | Nachher |
|---|---|---|
| Umrisse, Länge unverändert (99), in `.venv` neu getraced | – | 99 byte-gleich (Zeilenenden ausgenommen) |
| Umrisse, Länge geändert (18) | – | 16 gleiche Punktzahl; Fläche / Länge² höchstens 0,13 % anders; C8 49 → 39, C8X 51 → 46 Punkte (Untergrenze 0,08 m in `trace.py`) |
| Holos (18): Budget-Stufe / Dreiecke | holo-v1 | alle gleiche Stufe; Dreiecke höchstens 0,2 % anders (C8X 19.811 → 19.850); 325a/350r `slivers` wie in holo-v1 |
| Salvation | weder Holo noch Umriss | Holo 20k (19.931 Dreiecke, alle Kriterien), Umriss 76 Punkte |
| Release-Ordner | 117 | 118: 18 ersetzt, 1 neu, 99 byte-gleich, `verify` ohne Befund |
| Tests (Core / Import / App) | 1.418 / 141 / 1.328 | gleich |

**Akzeptanz:**
- [x] Kontaktbögen: Holos (Pipeline-Bögen „clean“/„not clean“), Umrisse gegen Render mit Bugpunkt (Scratch-Bogen)
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, 26.10., bestanden): Kontaktbögen; Salvation-Bug-Richtung
- [x] Upload (Martin, 26.10.; Manifest und Hull-A-Holo auf GitHub byte-gleich mit dem Release-Ordner): `upload-holo-v1.ps1` (Holos, dann Manifest); danach Hull A, Avenger Titan, C8, Salvation im Holo-Modus in richtiger Größe zu den Racks
- Commits Pipeline 1af1c83..b3da084, hier Daten-/Doku-Commit.

### [x] K5.2c Umriss mindestens maßstabsgetreu, Ausschnitt wie mit Keil (D62-Addendum) (26.10.2026)
**Warum:** Martin beim Gate (Aurora, Salvation): „outline passt nicht“. Nach D62 schrumpft der Umriss auf ~1,2 × Rack-Größe; bei 93 von 97 Schiffen ist der Rumpf kleiner als echt (Redeemer 6 %).

**Ist-Zustand vor K5.2c (A1):** `OutlinePlacement.Place` Faktor = 1,2 × max(Racks / Umriss) je Achse; `SceneProjector.FitExtent` kompakt mit Umriss = Racks + ganzer Umriss, lang = geladene Racks + Bugzone bis zur Umrissspitze (`OutlinePlacement.Tip`).

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Umriss nie kleiner als das Schiff (Martin); Ausschnitt Racks + Bugzone (Martin), dann nach der Stopp-Regel kurze Bugzone = Keilnase (Martin); Schwelle: kein Hauler-Schiff (Hull A, Cutlass Black, Freelancer, C2, Hull C, RAFT) schlechter als heute und keins neu unter dem heutigen Hull-A-Wert im Default-Overlay; der heutige Wert ist 19,4 px, nicht 23,4 (das war die Hochrechnung auf 800 × 600); C2 und Hull C lagen schon vorher darunter → Backlog (Martin). Liste der Schiffe, die den D62-Zweig behalten (Martin): Hull A (echt), Caterpillar, Syulen, Railen (generiert; nur die Railen-Reihe ist länger als der Rumpf, 53 gegen 40 Zellen) → kein Befund für den Reihen-Generator.
- App: `OutlinePlacement.Place` Faktor = max(1,2 × Racks/Umriss, 1); `FitExtent` mit Umriss wie mit Keil (`Bow` nimmt die Keilecken), `OutlinePlacement.Tip` entfällt
- Tests: `OutlinePlacementTests` (kleine Racks → Maßstab, winziges Rack → Maßstab, Racks füllen den Rumpf → 1,2), `SceneProjectorTests` (Umriss-Fit = Keil-Fit, kompakt und lang); `The_tip_is_the_bowmost_point` entfällt

**Zahl vorher/nachher** (Kistenkante entlang x, Default-Overlay 400 × 302, Default-Kamera, leerer Plan; Wegwerf-Test):

| Schiff | vorher (D62) | Plan: Bugzone bis Spitze | nachher: kurze Bugzone |
|---|---|---|---|
| Hull A | 19,4 | 22,2 | 27,2 |
| Cutlass Black | 31,9 | 19,9 | 35,4 |
| Freelancer | 19,9 | 17,4 | 23,0 |
| C2 | 12,1 | 7,6 | 13,2 |
| Hull C | 4,4 | 4,2 | 5,1 |
| RAFT | 20,7 | 15,9 | 22,2 |
| Redeemer / Aurora MR / Salvation | 90,5 / 106,8 / 80,5 | 20,7 / 43,0 / 42,8 | 84,7 / 91,8 / 76,3 |
| Tests App | 1.328 | – | 1.330 |

**Akzeptanz:**
- [x] Umriss nie kleiner als das Schiff; D62-Rand nur, wo die Racks den Rumpf füllen
- [x] Mit Umriss passt die Ansicht wie mit Keil; kein Hauler-Schiff kleiner als vorher
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, Publish-Build, Look, 26.10., abgenommen): Aurora, Salvation, Redeemer, Cutlass Black, Hull A, C2 mit Umriss; Rumpf in echter Größe, Racks lesbar, Bug erkennbar
- Commits bcb45fb (Tests rot), 1e157d9 (App), Doku-Commit.

### [x] K5.2d Herauszoomen bis zum ganzen Umriss (D76-Addendum) (26.10.2026)
**Warum:** Martin nach K5.2c (Constellation Mk IV Phoenix): immer so weit herauszoomen können, dass der ganze Umriss zu sehen ist, wie bei der Hull C.

**Ist-Zustand vor K5.2d (A1):** `ZoomLimits.Lower(fit) = fit.Scale × 0,5` (D76); seit K5.2c zeigt der Fit nur Racks + kurze Bugzone, der maßstabsgetreue Umriss reicht weit darüber hinaus.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Untergrenze = min(halber Fit, Skala der ganzen Szene); `ViewBasis.WholeScale` aus `ProjectedScene.Extent` (gleicher Rand, gleicher Raum oben); `ZoomLimits` nimmt die Basis; kein automatisches Zentrieren (A4).
- App: `ViewBasis.WholeScale`, `ViewBasis.Of(…, whole)`, `ZoomLimits.Lower/Upper/Clamp(ViewBasis)`, `ShipViewControl.Basis` übergibt `scene.Extent`
- Tests: `Zooming_out_reaches_the_whole_scene_where_half_the_fit_would_not`, `A_whole_scene_within_half_the_fit_leaves_the_lower_limit_at_half_the_fit`; `A_fit_above_the_maximum_stays_the_upper_limit` ruft `Upper(basis)`

**Akzeptanz:**
- [x] Herauszoomen endet beim halben Fit oder, wo die Szene größer ist, dort, wo sie ganz passt
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (App 1.331: +2, der Wegwerf-Test aus K5.2c ist gelöscht)
- [x] Gate (Martin, 26.10., bestanden): Phoenix, Redeemer, Aurora mit Umriss ganz herausgezoomt → ganzer Rumpf im Fenster (ggf. nach Verschieben); Hull A, Hull C wie bisher
- Commits 46a7088 (Tests rot), 5e1e582 (App), Doku-Commit.

### [x] K5.3 Aurora Mk II mit TS-Modul (D59-Addendum) (26.10.2026)
**Warum:** D59 sieht Frachtmodule als zusätzliche Katalogeinträge vor; Fleetyards kennt keine Module. Erstes Modul: die TS-Modul-Plattform der Aurora Mk II.

**Ist-Zustand vor K5.3 (A1):** `ShipLoader.LoadAll` liest nur `ships/*.json`, Unterordner ignoriert; Core kopiert nur `ships/*.json`; App und CLI arbeiten mit Katalog-Ids (D59); `aurora_mk_ii.json` = 2 × 1 SCU, generierte Reihe.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Daten von Martin (Spiel, 26.10.): Plattform 3 × 2 × 1 Zellen (Länge entlang x), Kisten bis 2 SCU, Stapelachse +z, am Heck hinter dem 2-SCU-Raum auf der Mittellinie, fährt nach unten aus; nur Aurora Mk II; 8 SCU gesamt; Name „TS module“. Eigene Moduldatei mit Modul-Schema 1, kein Schiffsschema-Bump; Lage schematisch (x −6..−3, y 0..2, z −1..0); `maxCrateSize: 2` als Tür.
- Daten: `ships/modules/ts_module.json`
- Core: `ModuleDto` (`Check`, `ConfigurationId`, `On`), `ShipJsonContext` + `ModuleDto`, `ShipLoader.ModulesFolderName`, `LoadAll` liest `modules/` (eine Konfiguration je Basis, Fehler als `ShipProblem`, sortiert), `LoadFile` → `ReadFile` + `ToShip`; csproj kopiert `ships/modules/*.json`; `publish.ps1` prüft `ships/modules/ts_module.json` im ZIP
- Tests: `ShipLoaderTests` (Eintrag je Basis, fehlende Basis, kaputte Datei, Modul-Schema 2, Überlappung, kein Ordner; `LoadAll_loads_every_shipped_file_without_problems` nennt die Modul-Einträge), `ShipModuleTests` (ausgelieferte Konfiguration, Basis unverändert, 6 SCU in 2ern ohne Modul abgelehnt, mit Modul auf der Plattform)

**Zahl vorher/nachher:**

| Messung | Vorher | Nachher |
|---|---|---|
| Tests (Core / Import / App) | 1.418 / 141 / 1.331 | 1.428 / 141 / 1.331; Slow grün |
| Katalogeinträge | 97 | 98 (+ `aurora_mk_ii_ts_module`) |
| Gleichheitslauf (gegen nach D3) | 388 | 388 gleich, 4 neue Zeilen für die Konfiguration (alle abgelehnt: Fixtures über 8 SCU oder gesetzte 4er gegen Tür 2) |

**Akzeptanz:**
- [x] Konfiguration „RSI Aurora Mk II · TS module“ mit 8 SCU, Plattform als Rack 3 mit Tür 2; Basis unverändert
- [x] Kaputtes Modul kostet nur seine Einträge; ohne `modules/` alles wie bisher
- [x] Plan mit und ohne Modul (6 SCU in 2ern)
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün; Slow grün; Exhaustive nicht nötig (Planer unberührt)
- [x] Gate (Martin, Publish-Build, 26.10., bestanden mit Korrektur): „RSI Aurora Mk II · TS module“ wählen → Kopfzeile 8 SCU; Plan über 2 SCU legt ins Modul; Modul passt. Korrektur: Rack 2 der Mk II liegt direkt über Rack 1, nicht dahinter (`aurora_mk_ii.json` Origin `[0, 0, 1]` statt `[-2, 0, 0]`, Handwert in `notes`); Modul unverändert, damit seine Lage zum Holo bleibt; Gleichheitslauf 392/392, Import-Abgleich `same`
- [x] Zweite Korrektur (Martin, 26.10.): „2 direkt über der 1“ hieß in der Draufsicht daneben, nicht gestapelt. Rack 1 (`cargo_left`) `[0, 0, 0]` bleibt, Rack 2 (`cargo_right`) `[0, 0, 1]` → `[0, -1, 0]` (Steuerbord daneben); Modul `[-6, 0, -1]` → `[-6, -1, -1]` (y-Mitte = Mitte der beiden Plätze, x hinter dem Bay, z Rumpfunterseite bleiben); Rack 3 vorher geprüft: Modul 3 × 2 × 1, 6 SCU, Tür 2 (nicht 2 × 2 × 2), Rack 1/2 Index 1 × 1 × 1, limits 1–1; 8 SCU; Gleichheitslauf 392/392 (09dfbe9)
- [x] Dritte Korrektur (Martin, 26.10.): Rack 1 und 2 eine Zelle tiefer, auf die Höhe des Moduls: `[0, 0, -1]`, `[0, -1, -1]` (z −1..0 wie Rack 3); Gleichheitslauf 392/392
- [x] Gate (Martin, 26.10., bestanden nach K5.3b): Holo an, Plattform hinter dem Bay unter dem Heck, die zwei Mk-II-Plätze nebeneinander, Bug nach vorn
- Commits 6a05575 (Tests rot), fb3e49c (Core, Daten), fd07438 (Doku), Korrektur-Commit.

### [x] K5.3b `holoOffset` in der Schiffsdatei (D63-Addendum) (26.10.2026)
**Warum:** Martin beim Holo-Gate der Aurora Mk II: Racks liegen eine Zelle zu hoch im Rumpf. Das Holo sitzt mittig auf den Racks (D63), Racks verschieben hilft nicht, das Holo wandert mit.

**Ist-Zustand vor K5.3b (A1):** `HoloPlacement.Place(mesh, racks)` legt die Mitte des Holos auf die Mitte von `Ship.Bounds` in allen drei Achsen; kein Versatz in Daten oder Code.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** optionales Feld `holoOffset: [x, y, z]` in ganzen Zellen auf Schiffsebene, kein Schema-Bump (optional wie `position`, D56); Modul-Konfigurationen erben es; `-Force` schreibt es zurück, setzt es nie. Dazu Martin: `holoOffset` korrigiert die Mittellage des Holos gegenüber den Racks, keine Positionsangabe des Schiffs; wer einen Wert setzt, hat ihn am Holo-Gate gesehen, Quelle und Datum in `notes`; braucht ein Schiff einmal x/y, gilt das für Umriss und Holo gleichermaßen und das Feld wird erweitert, kein zweites; heute braucht das kein Schiff, kein Code dafür.
- Core: `ShipDto.HoloOffset` (drei Werte) → `Ship.HoloOffset` (`ShipPoint?`)
- App: `HoloPlacement.Place(mesh, racks, shift)`, `PreparedHolo` übergibt `ship.HoloOffset`
- Import: `Format-Ship` schreibt `holoOffset`
- Daten: `aurora_mk_ii.json` `"holoOffset": [0, 0, 1]`, Quelle und Datum in `notes`
- Tests: `ShipLoaderTests` (Lesen, Modul erbt, ohne Feld keins, zwei Werte = Problem), `ShipModuleTests` (Aurora mit und ohne Modul), `HoloViewTests` (Versatz um ganze Zellen), `ImportFleetyardsScriptTests` (`-Force` behält es)

**Zahl vorher/nachher:** Tests Core 1.428 → 1.433, App 1.331 → 1.332, Import 141; Gleichheitslauf 392/392 byte-gleich; Import-Abgleich 97 same.

**Akzeptanz:**
- [x] Holo der Aurora Mk II (mit und ohne Modul) eine Zelle höher, Racks und Planung unverändert
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, 26.10., abgenommen): Aurora Mk II und Aurora Mk II · TS module mit Holo, Racks tiefer im Rumpf; andere Schiffe unverändert
- Commits e88229e (Tests rot), 54f0bed (Core, App, Import, Daten), Doku-Commit.

### [x] K5.4 App-Icon (D48-Addendum) (26.10.2026)
**Warum:** Seit T6.2 im Backlog (D48): Exe, Taskleiste und Werkzeugfenster zeigten das Windows-Standardsymbol.

**Ist-Zustand vor K5.4 (A1):** kein `.ico`, kein `<ApplicationIcon>`, kein `Window.Icon` im App-Projekt; `docs/images/` leer.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Martin lieferte das Logo (Variante A „flat“, `.ico` mit 16/24/32/48/64/128/256 px als PNG-Frames, SVG-Quelle) und gab K5.4 ohne eigenen Plan frei („direkt einbauen“). Nur `<ApplicationIcon>`, kein `Window.Icon` je Fenster: ein Fenster ohne eigenes Icon nimmt das Assembly-Icon (Doku `Window.Icon`, Remarks). Das Logo ist Martins Eigenwerk ohne fremdes Material → MIT wie das Repo, kein `NOTICE.md`. Keine Prüfung in `publish.ps1`: das Icon ist eine Win32-Ressource der Exe, keine Datei im ZIP.
- App: `src/CargoPlan.App/cargoplan.ico` (byte-gleich mit Martins Datei, SHA256 `3C13B3A1…`), `<ApplicationIcon>` im csproj
- Doku: `docs/images/icon-256.png` (README, Nexus-Seite in T7.5), `docs/images/icon.svg` (Quelle, Kommentare übersetzt)

**Akzeptanz:**
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (Core 1.401, Import 141, App 1.328, unverändert)
- [x] Icon aus der gebauten `CargoPlan.App.exe` zurückgelesen (`Icon.ExtractAssociatedIcon`): das Logo
- [x] Gate (Martin, Publish-Build, 26.10., bestanden): Icon in Explorer, Taskleiste, Alt+Tab und den Titelleisten von Editor und Settings; ZIP entpackt → Exe zeigt es

### Abschluss der K5-Session (26.10.2026)
- Alle Tasks von Martin abgenommen (26.10.2026): K5.1, K5.2, K5.3, K5.4 aus dem Startprompt; K5.2b (Holos und Umrisse), K5.2c (Umriss maßstabsgetreu), K5.2d (Herauszoomen bis zum Rumpf) und K5.3b (`holoOffset`) aus Befunden an den Gates.
- K3 nebenbei: Clipper-Gate bestanden, Kiosk-Gate entfällt (D70-Addendum); offen bleiben die Antwort von Torlek (K3.1) und das Hull-C-Overlay-Gate (K3.3, keine 16er-Mission verfügbar).
- Merge `--no-ff` nach main, Push durch Martin. Pipeline-Repo `sc-ship-outlines`: 1af1c83..b3da084 (Push durch Martin); `holo-v1` am 26.10. neu hochgeladen (Martin, auf GitHub geprüft).
- Tests: Core 1.401 → 1.433, Import 141 → 141, App 1.328 → 1.332; Slow grün; Exhaustive nicht nötig (Planer unberührt).
- Entscheidungen: D78 neu; Addenda zu D14, D48, D53, D55, D59, D61, D62 (zwei), D63 (zwei), D70, D76.

**Übergabe für T7.5:**
- **Schema-Stand:** Schiff 5 (neu optional `holoOffset`), Plan 6, Mission 3, settings.json 2, Look 1; neu: Moduldatei `ships/modules/*.json` mit eigenem Schema 1 (D59).
- **Schiffszahl:** 97 Schiffsdateien + 1 Modul-Konfiguration = 98 Einträge in der Auswahl. Index vom 26.10.: 248 Modelle bei Fleetyards, 118 mit Fracht, 97 mit Gitter; 21 mit Fracht, aber ohne Gitter (u. a. Javelin, Hull D/E, Kraken), fehlen bewusst.
- **Preset-Namen (K4):** „Default“, „High contrast“, „Minimal“.
- **Release-Notes-Stoff:** Hull C als zwei Kreuze, Hull A mit echtem Spindelabstand; Fortune und Moth mit neuen Racks, Salvation neu, Aurora Mk II · TS module neu (8 SCU); der Umriss hat echte Größe, die Ansicht öffnet auf Racks und Bug, Herauszoomen zeigt den ganzen Rumpf; Holos von 18 Schiffen neu skaliert (Hull A war 28 % zu groß), Salvation-Holo neu; gespeicherte Ansichten von Hull A, Hull C und Aurora Mk II sitzen nach dem Update anders (Datenupdate, D76); App-Icon.
- **Known issues:** `limits.max = 1` ungeprüft, 36 Racks in 18 Schiffen ohne Türlimit (Torlek); bei der Hull C liegen die Racks innerhalb der Holo-Frachtblöcke (logisches Raster gegen Modell, D78); Kisten großer Schiffe im Default-Overlay klein (C2 13,2 px, Hull C 5,1 px); die Anordnung von 90 Schiffen ist generiert (D53), die Railen-Reihe ragt aus dem Rumpf; Backbord/Steuerbord stammen aus Fleetyards-Namen (D78).
- **Für die Screenshots:** nach K5 neu aufnehmen; Umriss in echter Größe und neuer Ausschnitt ändern das Bild aller Schiffe außer Hull A, Caterpillar, Syulen, Railen.
- **Offen:** Antwort von Torlek (K3.1); Hull-C-Overlay-Gate (K3.3); Starbreaker für das zweite Modulschiff (Backlog).

### Nachtrag nach dem K5-Merge (26.10.2026, Branch `fix/k5-followup`)
- **CI:** Der K5-Merge (2922698) scheiterte auf `windows-latest` an `A_rack_of_1000_crates_projects_within_50_ms` (60 ms auf dem geteilten Runner, alles andere grün); der rote K4-Merge war ein .NET-Download-Ausfall (HTTP 409). Der Test misst jetzt fünfmal und prüft den schnellsten Lauf (Martin), 4f38666.

### [x] K5.5 Maßstab beim Drehen festhalten (D76-Addendum) (26.10.2026)
**Warum:** Martin: Beim Drehen mit Holo pumpt die Größe. Gemessen (Wegwerf-Test, Default-Overlay, Holo als Box in Fleetyards-Maßen): Fit-Skala über eine Runde schwankt bei Hull A 10 %, C2 25 %, Aurora Mk II 32 %, Hull C 39 %.

**Ist-Zustand vor K5.5 (A1):** Ohne Rahmen passt die Ansicht bei jedem Winkel neu ein (D61, D76, `Orbiting_the_fit_keeps_fitting`); `PanBy` macht den Fit schon zum Rahmen, `OrbitBy` nicht.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Maßstab beim Drehen festhalten (Martin, statt drehfestem Fit oder Lassen), in allen Modi; danach folgt die Ansicht Fenster und Ladung nicht mehr von selbst bis zum Reset; der Rahmen wird pro Schiff gespeichert (K1.2).
- App: `ViewNavigation.OrbitBy(yaw, pitch, basis)` macht aus dem Fit beim ersten Schritt den Rahmen (`Framing.OfFit`), `ShipViewControl` übergibt `Basis(scene)`
- Tests: `Orbiting_the_fit_fixes_its_scale` (ersetzt `Orbiting_the_fit_keeps_fitting`), `The_first_orbit_step_draws_the_fit_it_starts_from`, `Further_orbit_steps_keep_the_scale_whatever_the_fit_would_be`

**Zahl vorher/nachher:** Tests App 1.332 → 1.334, Core 1.433, Import 141.

**Akzeptanz:**
- [x] Drehen aus dem Fit hält den Maßstab, der erste Schritt springt nicht; ein gesetzter Rahmen bleibt
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, Publish-Build vom Branch, 26.10., bestanden; der erste Test lief auf der Publish-Version vom 25.10. ohne K5.5): Aurora Mk II und Hull C mit Holo drehen, ohne vorher zu zoomen → Größe bleibt ruhig; Reset bringt den Fit zurück
- Commits d814e50 (Tests rot), 9a0eef6 (App), Doku-Commit.

### K6 Aufzug-Flow (26.10.2026) – eingeschoben vor T7.5, Branch `feat/k6-elevator-flow`

Auslöser: Martins Abschlusstest am Frachtaufzug. Mission 1 annehmen → Sync → laden → Mission 2 annehmen → **Plan** legt
Mission 1 um, obwohl sie an Bord ist (korrekt nach D19, aber ohne Schutz für Geladenes); am Aufzug muss pro Kiste ohne
Unlock ablesbar sein, was wohin gehört. Martins Ladeweise: alle Missionen laden, dann pro Mission entnehmen
(first-in last-out). Reihenfolge K6.1 → K6.2 → K6.3 → K6.4 → K6.5; je Task ein Plan zur Freigabe. Plan-Schema 6 → 7
in K6.4 (`loaded`), K6.5 legt `manual` ebenfalls in 7 ab.

### [x] K6.1 Legendenzeile vollständig (D69-Addendum) (26.10.2026)
**Warum:** Am Aufzug wird eine Kiste über Ware + Größe + Ziel zugeordnet; die Zeile zeigte nur `To <Ziel> · <SCU> SCU`,
die Überschrift keine Mission.

**Ist-Zustand vor K6.1 (A1):** `ShipViewBuilder.LegendLines` → `LegendEntry.Label` (`To X · n SCU · <Markierungen>`),
Überschrift nur bei Pickup; `HeaderRow`/`LineRow` waren horizontale StackPanels, also nie gekürzt, nur abgeschnitten;
keine eigene Panel-Klasse. `ContractSync` trägt `ContractSource.ContractIds` = CTs `Contracts.Id` (AUTOINCREMENT, eine
Aufnahme mit zwei Waren = zwei aufeinanderfolgende Ids); im Spieltext steht keine Id, der Aufzug zeigt sie nicht.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 `To Green Glade Station · 5 SCU (4+1) · Pressurized Ice`,
Mischung aus den Kisten des Plans (abgelieferte zählen mit), eine einzelne Kiste ohne Klammer; Wiederholungen immer als
`Anzahl×Größe` (`2×4+1`); P2 mehrere Waren je mit eigener Mischung: `12 SCU (8+2) Titanium, (2) Gold`; P3 Markierungen
am Ende, nie gekürzt, die D16-Reserve nur noch dort; P4 Überschrift `From <Abholort> · #47` (CT `Contracts.Id`,
Bereich `#47–48`, Lücken `#47, 49`), Editor-Mission ohne Id, CT-Missionen bekommen auch ohne Pickup eine Überschrift;
P5 Kürzung: Ware zuerst, dann Ziel bzw. Abholort, nie SCU/Mischung/Id/Markierungen, Tooltip mit Volltext; E1 wartende
Missionen (D41) zeigen die Mischung aus ihrer Kistenliste (`CrateListUnder(saved.CrateCap)`); E2 Nachweis der B6-Gesten
nach dem Umbau (XAML-Test + Offscreen-Hit-Test); E3 K6.4 hängt „geladen“ als weiteres Rang-0-Segment an, ohne Panel-Umbau.
- App: `LegendRowPanel` (Attached Property `Rank`, kürzt Rang 1 anteilig, dann Rang 2), `LegendSegment` (Text, Rang,
  Reserve), `LineGoods`, `LegendText` (`Quantity`, `Mix`, `Goods`, `ContractIds`, `Appended`); `LegendEntry` mit
  `Segments`, `HeaderSegments`, `HeaderLabel`, `Goods`, `ContractIds`; `ShipViewBuilder` setzt Waren, Id und die erweiterte
  Überschriften-Regel, `Unplaced` die Mischung; XAML: Zeilen und Überschrift als DockPanel + ItemsControl über den Segmenten,
  `LegendSegmentTemplate`/`LegendSegmentTextStyle` (Grau + Durchstreichen über den `Tag` der Zeile), Tooltips
- Tests: `LegendLineTests` (19 Fälle), `LegendRowPanelTests` (7), `OverlayLegendXamlTests` (+6 Fälle, u. a.
  `The_B6_gestures_stay_on_their_elements`); bestehende `Label`-Erwartungen auf das neue Format

**Zahl vorher/nachher:** Tests App 1.334 → 1.366, Core 1.433, Import 141 (unverändert). UI-Task, keine Planer-Zahl.

**Offscreen-Protokoll (echtes Overlay, Hurston-Plan vom 25.10., Default-Look; Harness im Scratchpad, nicht im Repo):**
- 420 px: alle Zeilen vollständig, z. B. `From Everus Harbor · #37–38`, `To Everus Harbor · 8 SCU (8×1) · Waste`
- 270 px: nur die Waren kürzen; 240 px: Waren weg, Zielnamen mit Ellipse; SCU, Mischung, Markierungen bleiben
- Hit-Test (E2): Klick auf Zeilentext → `Button#LineButton` → Zeile abgehakt, Auswahl leer; Zeilen-Checkbox →
  `CheckBox#LineCheck` (außerhalb des Buttons) → 2 von 2 Kisten ausgewählt, Haken 3 → 3; Kopf-Checkbox →
  `CheckBox#MissionCheck` → Mission 4 (6 Kisten) zur Auswahl dazu
- Nebenbefund, nicht K6.1: das Abhaken der unteren Mission lässt Kisten darüber schweben (rote Zeilen) – das Absinken
  aus K6.4

**Akzeptanz:**
- [x] Zeile `To <Ziel> · <SCU> SCU (<Mischung>) · <Ware>`, Mischung aus dem Plan, nach Split der tatsächliche Stand
- [x] Überschrift `From <Abholort> · #<CT-Id>`, ohne CT keine Id
- [x] Kürzung Ware → Ziel, nie SCU/Mischung; Tooltip mit Volltext
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, Publish-Build vom Branch, im Lock am Aufzug, 26.10., bestanden): Zeile lesbar, Mischung = Kiosk, `#Id` = CT-Fenster
- Commits cd54dfb (Tests rot), 0a75daf (App), Doku-Commit.

### [x] K6.2 Markierung überlebt den Lock (D25-Addendum) (26.10.2026)
**Warum:** Am Aufzug ist der Lock der Normalzustand und die Markierung das, was man dort braucht; der Lock räumte sie.

**Ist-Zustand vor K6.2 (A1):** Einzige Stelle `ShipViewControl.IsNavigationEnabled` → `Pick(CrateSelection.Empty)`,
Grund im Code: „a selection left over must not move with the next drag“ (D25: „goes … with the lock“, User Guide).
Kein Drag-Grund: ein laufender Drag endet beim Lock schon über `ReleaseMouseCapture` → `OnLostMouseCapture` → `_drag = null`,
ohne `FinishDrag`. Lücke: ein Drag ohne Capture (`CaptureMouse` gescheitert) bekam kein Ende. Das XAML blendet im Lock
nur Kopfzeilen-Buttons und Schiffsauswahl aus.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** `Pick(Empty)` entfällt; ein Drag ohne Capture wird im
Lock verworfen (kein Move); `IsDragging` als öffentliche Abfrage für den Test statt Reflection; jede Planänderung
(Sync, Haken, Undo) leert die Auswahl weiterhin (D25), eigener Punkt, falls es am Aufzug stört.
- App: `ShipViewControl.IsNavigationEnabled` ohne Demarkieren, verwirft einen Drag ohne Capture; `IsDragging`
- Tests: `ShipViewLockTests` (3): Lock und Unlock behalten die Auswahl, Lock beendet einen Drag ohne Move.
  Erst nur `IsDragging` ergänzt und rot gesehen: Auswahl durch `Empty` ersetzt, `IsDragging` nach dem Lock `True`

**Zahl vorher/nachher:** Tests App 1.366 → 1.369, Core 1.433, Import 141. UI-Task, keine Planer-Zahl.

**Offscreen-Protokoll (echtes Overlay, Hurston-Plan; Harness im Scratchpad):** Zeilen-Checkbox Mission 1 und
Kopf-Checkbox Mission 4 per Mauspfad → 9 Kisten ausgewählt; gesperrt: 9, dieselben Checkboxen gesetzt, Magenta-Rand
und Dimmen sichtbar, Kopfzeilen-Buttons weg; wieder entsperrt: 9, unverändert. Lock während eines Drags mit Capture
nicht automatisiert (bräuchte echte Mausbewegung) → Gate.

**Akzeptanz:**
- [x] Lock räumt die Auswahl nicht; Auswahlfarbe und Legenden-Checkboxen im Lock sichtbar; Unlock übernimmt sie
- [x] Laufender Drag beim Lock: kein Move, Auswahl bleibt
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, Publish-Build vom Branch, 26.10., bestanden): markieren, Lock → sichtbar; Unlock → noch da; Lock
  während eines Drags (Hotkey) → kein Move, Auswahl da
- Commits cf04c63 (Tests rot), c7d2f4f (App), Doku-Commit.
### [x] K6.3 Editor sortierbar nach Mission und Ziel (D19-Addendum) (26.10.2026)
**Warum:** Am Aufzug sucht man die Zeilen eines Vertrags oder einer Station; der Editor zeigte nur die Reihenfolge des
ersten Auftretens.

**Ist-Zustand vor K6.3 (A1):** `CanUserSortColumns="False"` („it would reorder the view, not the route“); Plan liest
`MissionEditorViewModel.Rows` (Route = erstes Auftreten, Missionsgruppen in `MissionRows.ToMissions`), nie die Ansicht;
↑/↓ verschieben in `Rows`. Keine Spalte „Mission“, nur „Source“ (`ct 37–38`, `MissionText.Source`, auch in der Notice
zu gelöschten Verträgen). A1-Befund aus K6.1: `LegendText.ContractIds` war eine zweite Kopie dieser Bereichsregel.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 „Source“ heißt „Mission“ und zeigt `#37–38` wie die
Legende, eine Formatfunktion `MissionText.Source` für Editor, Legende und Notice (`Riker (#36)`), `LegendText.ContractIds`
entfällt, Läufe überall (`#36–37, 40`); P2 Klick-Zyklus auf → ab → Standard, nicht persistiert, eine Spalte zugleich;
P3 Mission: erste CT-Id numerisch, getippte Zeilen dahinter nach Ziel; E1 getippte Zeilen in **beiden** Richtungen hinter
den CT-Zeilen, die Richtung dreht nur innerhalb der Blöcke; P4 Ziel ohne Groß-/Kleinschreibung; P5 Gleichstand in
Standard-Reihenfolge; P6 Leerzeilen unten; P7 ↑/↓ gesperrt, solange sortiert ist (Tooltip); P8 eine im sortierten
Zustand geänderte Zeile springt beim Bestätigen an ihren Platz.
- App: `Editor/MissionRowOrder` (`IComparer`, `EditorSortColumn`, `EditorSort`), `MissionRow.ContractId`;
  `MissionEditorViewModel.RowsView` (`ListCollectionView`), `Sort`, `SortBy`, `CanMoveUp/Down` mit `Sort is null`;
  Fenster: `ItemsSource="{Binding RowsView}"`, `OnSorting` (bestätigt offene Bearbeitung, ruft `SortBy`, setzt den Pfeil),
  nur Mission und Destination mit `SortMemberPath`; `MissionText.Source` mit `#` und Läufen, `ShipViewBuilder` nutzt es
- Tests: `MissionRowOrderTests` (7 Fälle, u. a. `By_mission_descending_typed_rows_stay_behind_the_contracts`),
  `MissionEditorViewModelTests` (+6, u. a. `Plan_from_a_sorted_view_is_the_plan_from_the_standard_order`),
  `MissionEditorXamlTests` (3, neu; `MissionEditorWindow.xaml` als eingebettete Ressource), `MissionTextTests` (+3, die
  Id-Fälle aus `LegendLineTests` zogen hierher); `ct …`-Erwartungen auf `#…`

**Zahl vorher/nachher:** Tests App 1.369 → 1.384, Core 1.433, Import 141. UI-Task, keine Planer-Zahl; Planer-Invarianz
über den Test „sortiert = unsortiert“.

**Offscreen-Protokoll (echtes Editorfenster, Hurston-Plan, Dark; Harness im Scratchpad):** Klick auf „Mission“ →
`#1, #2–3 …, #4, #37–38 …`, Pfeil auf; zweiter → `#37–38 … #4, #2–3 …, #1`, Pfeil ab; dritter → Standard, kein Pfeil;
„Destination“ auf/ab alphabetisch; Klick während offener Zellbearbeitung → bestätigt, keine Ausnahme; ↑/↓ gesperrt;
Plan aus sortierter Ansicht → Legende zeilengleich mit Plan aus der Standard-Reihenfolge.
- Nebenbefund, nicht K6.3: die Crates-Spalte des Editors schreibt `1+1+1+1+1+1+1+1`, die Legende `8×1` (K6.1); die
  Spalte ist zugleich das Eingabeformat (`2+2+2`).

**Akzeptanz:**
- [x] Spaltenköpfe Mission und Destination klickbar, auf/ab, nicht persistiert; Standard bleibt erstes Auftreten
- [x] Sortierung ändert nichts am Plan und an der Legende; Entwurfszeilen sortieren mit, eine leere Zeile bleibt unten
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, 26.10., bestanden): sortieren, Plan drücken → Plan unverändert zur unsortierten Eingabe
- Commits e23c056 (Tests rot), 5a78e83 (App), Doku-Commit.
### [x] K6.4 Zustand „geladen“ (Plan-Schema 7, D79) (26.10.2026)
**Warum:** Martins Abschlusstest: Mission 1 annehmen → Sync → laden → Mission 2 annehmen → **Plan** legt Mission 1 um,
obwohl sie an Bord ist. Ladeweise: alle Missionen laden, dann pro Mission abgeben (first-in last-out).

**Ist-Zustand vor K6.4 (A1, zwei Explore-Berichte):** kein Konzept „feste Kiste“, einziges Hindernis ist `occupied` von
`CrateLayout.Lay`; `Plan` (`RackAssignment`) und `ReplanInPlace` legen alles Unabgehakte neu; Stufe 3 legt das Zielrack
ganz neu, 3b verdrängt fremde Kisten; `Board.Settle` weicht seitlich aus oder legt neu; Haken, `RemoveMission` und Sync
lassen Kisten darüber rot schweben (der Task-Text „wie bisher neu gelegt“ stimmte nicht); der Sync entfernt eine
verschwundene Mission nur ohne Kisten an Bord; der Editor-Plan löscht alle Haken (D19) und nummeriert getippte Missionen
neu; Undo trägt Haken per `CarryTicks` nach (D74); Plan-Schema 6.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 `Crate.Loaded`, nie zusammen mit abgeliefert; P2
Schema 7 `crates[].loaded` nur bei `true`, 6 → 7, Ausschlüsse `delivered`/`offShip`, Goldens nur `schemaVersion`; P3
Lieferung über erste CT-Id × Ziel bzw. bei getippten Missionen das Ziel, überlebt nur mit gleichen Kisten (Ware × Größe),
sonst geplant mit Notice; P4 Editor-Plan und Re-plan halten Geladenes **und Abgeliefertes** (Martin, ändert D19), der
Rest wird nach D41-Regel um das Geladene gelegt, ohne Geladenes der heutige Weg; P5 Moves: Stufe 3 um Geladenes herum,
3b nie Geladenes, Spieler darf ziehen, Split/Merge erhalten den Zustand; P6 Absinken: nach jeder Abgabe sinkt alles
Schwebende an Bord gerade, **geladen und geplant** (Martin), halb aufliegend bleibt es rot; P7 Zurückhaken immer nach
geplant, an alter Stelle oder neu eingelegt; P8 Sync entfernt eine verschwundene Mission, wenn nichts von ihr geplant an
Bord ist; P9 Schiffswechsel/`RacksChanged` löscht geladen mit Notice; P10 Zustandswechsel sind Undo-Schritte, Restore aus
dem Snapshot, `CarryTicks` entfällt; P11 Legendenzyklus geplant → geladen → abgeliefert → geplant, Kistenklick bleibt B6;
P12 Look: heller Rahmen, ` ▣` als festes Segment, Kopfzeile `· 40 loaded`. Ergänzungen bei der Freigabe: **E1** teilweise
geladene Mission ohne Platz → Ablehnung als Fallback (Aufwand „Warteeinheit Lieferung“ ≈ 20 Stellen + Formatfeld, Backlog),
ihr Rest zuerst gelegt; **E2** Re-plan mit Geladenem behält das Layout, wenn mehr warten würde („Re-plan found no better
layout“), Backlog `RackAssignment` mit Hindernissen; **E3** Lieferung ohne Kisten an Bord ist nicht ladbar („not placed
yet“); **E4** Tests Split → Editor-Plan bleibt geladen, Dreierstapel sinkt von unten.
- Core: `Crate.Loaded`/`AsLoaded`/`AsDelivered`/`AsPlanned`; `CrateDto.Loaded`, `PlanFile.SupportedSchemaVersion = 7`,
  `NotLoadedSchemaVersion`, `PlanFileDto.CheckLoaded`; `LoadPlanner.SetLoaded`, `Settled`, `SettledCrates`, `PlanKeeping`
  (+ `KeptDeliveries`, `DeliveryKey`, `Without`), `SetDelivered` mit Absinken und Neueinlegen, `RemoveMission` mit Absinken,
  `ReplanInPlace` mit E2, `Replan` löscht geladen für ein anderes Schiff, `CarryTicks` entfernt; `MoveResolution` (Stufe 3
  um Geladenes, 3b ohne, `Settle` nur gerade für Geladenes); `ContractSync` (geplant an Bord statt an Bord)
- App: `LegendEntry.Loaded`/`LoadedMark`, `CellFill.Loaded`, `MissionPalette.LoadedEdge`, Tooltip ` (loaded)`,
  `CapacityText` mit `· N loaded`; `OverlayViewModel`: Zyklus in `ToggleLine`, `Tick` als `Step`, `Restored` nur noch
  `Resync`, Notices `SettledText`, `LoadedClearedText`, `ReplanNoBetterText`; Editor-Plan über `PlanKeeping`,
  `ClearsDelivered` entfernt
- Tests: `LoadedStateTests` (Core, 32), `LoadedOverlayTests` (App, 12); umgestellt: Schema-Zahlen, zwei Zurückhak-Tests
  (P7), schwebend → sinkt (P6), `RemoveMission` (P6), fünf `CarryTicks`-Tests entfernt, Undo „Haken ist ein Schritt“,
  Editor „Abgeliefertes bleibt“, Legendentests klicken zweimal zum Abhaken
- Übergang: Auf dem Core-Commit `cb7ab02` scheitern zwei alte App-Tests (Haken-Übertrag, Ablehnung beim Zurückhaken), bis
  `1876e98` sie umstellt; gebaut hat jeder Commit.

**Zahl vorher/nachher (Release, `8d17ed2` gegen K6.4):** Gleichheitslauf 392/392 Hashes gleich; Suchzähler gleich, gave up
0; Replan-Mediane im Rauschen (Hull A acht 6,3 → 7,4 ms, Stresslast 76,6 → 87,0 ms); neu „erster Stop geladen“: 0,1–0,8 ms
je Fixture, Stresslast 36,8 ms, nichts wartend, altes Layout nie behalten, gave up 0; Slow grün; Goldens nur
`schemaVersion`. Tests Core 1.433 → 1.460, App 1.384 → 1.396, Import 141.

**Offscreen-Protokoll (echtes Overlay, Hurston-Plan; Harness im Scratchpad):** Mission 4 geladen, Waste abgeliefert →
geladene Kisten mit weißem Rahmen, Zeilen enden auf ` ▣`, Waste grau durchgestrichen; Kopfzeile `28/64 · 36 free · 10
loaded`. **Befund fürs Gate:** bei 420 px mit sichtbarem ↶ verschwindet der Schiffsname und die Kapazität läuft unter die
Schere (D67-Regel verletzt); `▣` ist klein.

**Akzeptanz:**
- [x] Geladene Kisten bewegt der Planer nie (Plan, Re-plan, Sync, 3b, fremder Legenden-Drag); Spieler-Drag, Split/Merge erhalten
- [x] Absinken nach Sync, Abhaken, Löschen; Dreierstapel von unten; Notice
- [x] Editor-Plan und Re-plan halten Geladenes und Abgeliefertes; E1-Ablehnung, E2-Schutz
- [x] Schiffswechsel löscht geladen mit Notice; Undo/Redo über Zustandswechsel; Schema 7 mit Migration und Ausschlüssen
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün; Zahl vorher/nachher
- [x] Look-Gate (Martin, 26.10.): Rahmen und `▣` passen; Kopfzeile kurz `· 10 ▣` statt `· 10 loaded`, Tooltip mit
  Volltext (`CapacityToolTip`), danach bei 420 px mit ↶ vollständig vor der Schere (Name weicht, D67)
- [x] Gate (Martin, der Originalfall, 26.10., bestanden): Mission 1 → Sync → laden → „geladen“ → Mission 2 → **Plan** → Mission 1 liegt, Mission 2
  drumherum; Re-plan gleich; geladene Kiste ziehen geht; Mission 1 abhaken → grau, Kisten darüber sinken; Neustart →
  Zustände da; Dreierzyklus ohne Nachdenken, Fehlklick per Undo zurück
- Commits 6d27157, 24977c3 (Tests rot Core), cb7ab02 (Core), 1876e98 (Tests rot App), 5f420b7 (App), 36bf04e (Doku);
  Look-Gate: 2311b20 (Tests rot), 52c6960 (Kopfzeile), Doku-Commit. Tests App 1.396 → 1.397.
### [x] K6.5 CT-Missionen im Editor editierbar (D44-Addendum) (26.10.2026)
**Warum:** ContractTracker vergisst manchmal Ziele; als getippte Zeile nachgetragen wurden sie eine eigene Mission ohne CT-Id.

**Ist-Zustand vor K6.5 (A1):** CT-Zeilen waren schon editierbar und blieben nach Plan in ihrer CT-Mission
(`MissionRows.ToMissions`); der Sync liest eine Aufnahme nur einmal (Wasserstand), überschreibt also nie; es fehlte nur das
Hinzufügen (Add = getippte Mission) und eine Markierung. `MissionItemDto` ist für Plan- und Missionsdateien dasselbe.
„CT liefert später doch“ gibt es nur als Neuaufnahme (F4), die eine eigene Mission mit `⚠ duplicate?` wird.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 Button „Add to mission“ (Martin) fügt unter der
ausgewählten CT-Zeile eine `manual`-Zeile in deren Mission ein, Add bleibt getippt; P2 eine geänderte CT-Zeile wird `manual`
(im Zeilen-Handler, Laden markiert nichts); P3 Spalte „Mission“ zeigt `#37–38 ✎` (Martin), Legende unverändert; P4 Sync wie
heute (Martin): Neuaufnahme bleibt eigene Mission mit Duplikat-Marke, die Regel „… now also in ContractTracker“ entfällt,
B1 benennt auch `manual`-Stopps um, sie bleiben `manual`; P5 verschwindet die Mission, gehen alle Zeilen, Notice `Gone from
ContractTracker: #37, with 1 row added by hand`; P6 `manual` im Plan-Schema 7 (kein zweiter Bump), nur bei `true`,
Fehler in Schema 6 und in Missionsdateien, Export schreibt es nie (geprüft); P7 ein nachgetragenes Ziel an einer
geladenen Mission ist deren ungeladener Rest (K6.4, E1).
- Core: `MissionItem.Manual`, `MissionItemDto.Manual`, `FromMissionItem/FromMissionStop(…, withManual)`,
  `MissionFileDto.ToMissionStops` lehnt `manual` ab, `PlanFileDto.CheckManual`
- App: `MissionRow.Manual`/`MissionLabel`/`ManualMark`, `MissionRows` (Laden, `ToMissions` trägt `manual` in Captures),
  `AddToMissionCommand`, Markieren in `OnRowChanged`, `OverlayViewModel.GoneWithManualText`, Spalte und Button im XAML
- Tests: `ManualItemTests` (Core, 6), `ManualRowsTests` (App, 8), `MissionEditorXamlTests` (+1, Spaltenbindung)

**Zahl vorher/nachher:** `ContractSync`- und Golden-Tests 37 grün → 37 grün (Goldens byte-gleich), Sync-/CT-Tests der
App 58 grün → grün; Planer unberührt, Gleichheitslauf nach K6.4 392/392. Tests Core 1.460 → 1.466, App 1.397 → 1.406.

**Akzeptanz:**
- [x] CT-Zeilen editierbar, Ziel per „Add to mission“ in derselben Mission; hinzugefügte und geänderte Zeilen `manual`
- [x] Sync: `manual`-Zeilen bleiben, Idempotenz, Umbenennen ohne Dublette; Mission weg → alle Zeilen weg mit Notice
- [x] Format: `manual` in Schema 7, Missions-JSON trägt es nicht
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, 26.10., bestanden): CT-Mission mit fehlendem Ziel → Ziel nachtragen → Plan → Legende unter derselben
  CT-Id → Sync → Ziel bleibt → Vertrag in CT abgeben → Mission samt Handzeile weg
- Commits a18b6ed (Tests rot Core), cd884e8 (Core), 61b66db (Tests rot App), 15e194a (App), 1a8f2b9 (Doku).

### Abschluss der K6-Session (26.10.2026)
- Alle Tasks von Martin abgenommen (26.10.2026): K6.1 Legendenzeile, K6.2 Markierung im Lock, K6.3 Editor-Sortierung,
  K6.4 Zustand „geladen“ (mit Look-Gate: Kopfzeile `· 10 ▣` und Tooltip), K6.5 CT-Missionen editierbar.
- Merge `--no-ff` nach main, Push durch Martin.
- Tests: Core 1.433 → 1.466, Import 141 → 141, App 1.334 → 1.406; Slow grün; Exhaustive grün (2/2, 8 min 14 s, auf
  1a8f2b9); Gleichheitslauf 392/392 vor und nach K6.4.
- Entscheidungen: D79 neu; Addenda zu D19 (K6.3, K6.4), D24, D25, D41, D44, D65, D67 (zwei), D69 (zwei), D74.
- Pannen, behoben (alle vor dem Push): BOM in einer Commit-Message (K6.2, neu geschrieben); untracked
  `ScratchEveryShipBaseline.cs` versehentlich im roten K6.4-Commit (per Amend entfernt); App ohne `CarryTicks` im Core-Commit
  (per Amend nachgezogen); `LegendText.ContractIds` als Kopie von `MissionText.Source` (A1, in K6.3 zusammengeführt).

**Übergabe für T7.5:**
- **Schema-Stand:** Schiff 5, **Plan 7** (neu `crates[].loaded`, `missions[].stops[].items[].manual`; Schema 6 wird gelesen),
  Mission 3 (lehnt `manual` ab), settings.json 2, Look 1, Moduldatei 1.
- **Release-Notes-Stoff:** Legendenzeile mit Kistenmischung und Ware, Überschrift mit ContractTracker-`#Id`; Markierung
  bleibt im Lock; Editor sortierbar nach Mission und Ziel, Spalte „Source“ heißt „Mission“; Zustand „geladen“ (Legende
  geplant → geladen → abgeliefert, Plan und Re-plan halten Geladenes, Kisten sinken ab, Abgeliefertes überlebt den
  Editor-Plan, jeder Klick ist per Undo zurücknehmbar); „Add to mission“ für vergessene Ziele.
- **Known issues:** Schiffswechsel löscht „geladen“ (Zurückwechseln stellt es nicht her); das Raten am Aufzug, welche Kiste
  zu welcher Mission gehört, bleibt spielseitig (die Legende reduziert es auf Ware + Größe + Ziel); die `#Id` ist die von
  ContractTracker, nicht vom Spiel; eine teilweise geladene Mission ohne Platz für ihren Rest lässt Plan ablehnen (Backlog
  „Warteeinheit Lieferung“); mit Geladenem legt der Planer nach D41 statt exakt (Backlog „RackAssignment mit Hindernissen“).
- **Für die Screenshots:** Legende hat neue Zeilen und Überschriften; geladene Kisten mit weißem Rahmen.
- **Offen:** Nebenbefund K6.3 (Crates-Spalte im Editor `1+1+…`, Legende `8×1`); aus K5 weiter Torlek (K3.1), Hull-C-Gate (K3.3).

### [x] K7 Scrollbare Legende (D80) (26.10.2026) – Branch `feat/scrollable-legend`
**Warum:** Martins Screenshot: viele Aufträge sprengen den Legendenbereich; der angedockte Block bekommt nur die Resthöhe,
der Rest wird abgeschnitten, ausgerechnet die „⚠ doesn't fit“-Zeile.

**Ist-Zustand vor K7 (A1):** Kein ScrollViewer in der Legende; einziger Scrollbalken im Overlay ist der schlanke der
Schiffsliste (lokaler Style im ComboBox-Template); kein Multiplikations-Converter. Legenden-Layout = `LegendSettings`
(top level, Teil der Look-Dateien, eigener Reset).

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 Grenze als Setting `legend.maxHeightShare` (Anteil an der
Overlay-Höhe, 0,2–0,8, Default 0,5, Slider in % unter „Legend layout“, Name nach Rückfrage); P2 nur die Zielzeilen
scrollen, „doesn't fit“, Verstöße und Paletten-Hinweis bleiben darunter; P3 im Lock kein Scrollen, Hinweis im User Guide.
- App: `LegendSettings.MaxHeightShare`, `AppearanceRules.Min/Max/DefaultLegendMaxHeightShare` + Klemmen mit Problemzeile,
  `OverlayViewModel.LegendMaxHeightShare` (Session setzt ihn), `SettingsViewModel.LegendMaxHeightSharePercent` +
  `BuildLayout()`, `LegendMaxHeightConverter`, XAML: `StopLinesScroll` (horizontal Disabled, nicht fokussierbar, MaxHeight
  = `OverlayContent.ActualHeight` × Anteil), `SlimScrollBarStyle` für Schiffsliste und Legende; die drei Looks nennen den Schlüssel
- Tests: `AppearanceRulesTests` (+5), `SettingsStoreTests` (+3), `SettingsViewModelTests` (+2), `SettingsSessionTests` (+1),
  `LegendMaxHeightConverterTests` (2), `OverlayLegendXamlTests` (+5)

**Zahl vorher/nachher:** Tests App 1.406 → 1.424, Core 1.466, Import 141 (unverändert). UI-Task, keine Planer-Zahl.

**Offscreen-Protokoll (echtes Overlay, Wegwerf-Harness im Scratchpad, nicht im Repo):**
- Martins Plan und Look (480 × 619, Schrift 9): 12 Zeilen, 180 px bei Grenze 299 px, kein Balken
- 3 Zeilen, Default (420 × 460): 44 px, kein Balken; 20 Zeilen: 220 von 293 px, Balken, Schiffsansicht 169 px,
  Paletten-Hinweis darunter; oben rechts mit 30 %: 132 px, Schiffsansicht 257 px; ausgeblendet: Schiffsansicht 389 px
- 30 Zeilen + 2 wartende Missionen in Martins Look: 299 von 360 px, beide „doesn't fit“-Zeilen und Hinweis darunter sichtbar
- Linksdruck auf den Scrollbereich erreicht DragMove; Treffer auf eine Zeile → `LineButton`

**Akzeptanz:**
- [x] Zielzeilen scrollen ab dem eingestellten Anteil, Schiffsansicht behält den Rest
- [x] „doesn't fit“, Verstöße und Paletten-Hinweis scrollen nie weg
- [x] `legend.maxHeightShare` in settings.json, Look-Dateien und Settings-Fenster, geklemmt mit Problemzeile
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün
- [x] Gate (Martin, Look, 26.10., bestanden): Balkenfarbe und -breite, Default 50 %, Verhalten mit vielen Aufträgen im Spiel
- Commits 49cfe18 (Tests rot), 3c4d39b (App), 813bc00 (Doku).

## Bugfix Sync-Gone (D81) (29.10.2026) – Branch `fix/sync-gone-keeps-plan`, Blocker für v0.1.0

### [x] S1 Verschwundene CT-Verträge bleiben im Plan (D81, Plan-Schema 8) (abgenommen 29.10.2026)
**Warum (Brief von Claude.ai als Reviewer):** Der Sync behandelte einen in CT verschwundenen Vertrag als erledigt und nahm
ihn aus dem Plan. CT verliert Verträge aus vielen Gründen (OCR, Neustart, Löschen in CT, im Spiel abgebrochen mit Fracht
an Bord); Datenverlust im Plan darf nie die Folge eines fremden Tools sein.

**Ist-Zustand vor S1 (A1, zwei Explore-Berichte), drei Punkte weichen vom Brief ab:** (1) Entfernt wurde nur, wenn nichts
geplant an Bord war: ganz geladen (Kisten weg, darüber Absinken), ganz abgeliefert oder wartend (für immer weg, der
Wasserstand stand schon darüber); mit geplanten Kisten blieb die Mission mit der Notice „Deleted in ContractTracker, crates
still on board“. (2) D80 war vergeben (K7), der Eintrag ist D81. (3) Einen Schutz „alle gleichzeitig weg“ gab es nicht
(D39 hatte ihn gestrichen), eine neue Datenbank machte jede Aufnahme zu gone; einen Inhaltsschlüssel aus T5.4 gibt es
nicht (D38/D42), F4 ist eine neue Zeile mit neuer Id.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 Marke am Vertrag `ContractSource.Gone`
(`missions[].contract.gone`), damit Editor-Plan, Umbenennen und Schiffswechsel sie ohne eigenes Feld tragen; P2 Plan-Schema
7 → 8, `gone` nur bei `true`, in Schema 7 ein Fehler, Goldens nur `schemaVersion`; P3 `remove` in zwei Stufen (Martin:
Abgeliefertes darf gehen; Korrektur **K1** bei der Freigabe): beim Verschwinden gehen geplante und abgelieferte
Lieferungen, geladene bleiben mit `gone`, eine wartende Mission geht ganz; später geht eine gone-Mission nur ganz und nur,
wenn nichts mehr geplant oder geladen ist, nie rückwirkend Geplantes (Reset-Schutz und Umschalten keep → remove halten);
P3b (Korrektur **K2** bei der Freigabe) eine neue Datenbank hängt die alten Ids ab (`ContractSource.Detached`, nur mit
`gone`): angezeigt, nie mehr verglichen, nicht in `KnownIds`, eigener Lieferschlüssel in `PlanKeeping`; P4 Rückkehr nur
über dieselbe Id (Martin), F4 bleibt Duplikat; P5 wartende gone-Missionen werden nicht neu platziert (Martin), der
Editor-Plan legt sie; P6 Schutz neu gebaut: alle gelisteten Verträge in einem Read weg (mindestens zwei) oder neue
Datenbank → nichts entfernt; P7 die Konflikt-Notice entfällt, Ersatz `· gone` in Legende und Editor plus einmalige
Sync-Notice; P8 Notices `Contract #47 gone from ContractTracker – kept in plan`, `… – removed 2 planned, 1 delivered,
kept 1 loaded`, `… – removed, it was waiting`, `Contract #47 back in ContractTracker`, `All contracts gone from
ContractTracker at once – kept in plan`; P9 Reset im Abschnitt setzt Auto-sync und gone zurück, der Pfad behält „Detect
automatically“; P10 Branch ab main f3f9996, `docs/t75c-followup` unberührt.

**Beim Bauen hinzugekommen, ratifiziert durch Abnahme (Martin, 29.10.2026):** E1 eine neue CT-Datenbank leert die
Undo-Historie (`Undo history cleared by new ContractTracker database`): die Schnappschüsse tragen die alten Ids nicht
abgehängt, ein Resync würde sie mit den neuen verwechseln (wie D74 beim Schiffswechsel); E2 `ContractSync.Quiet` ersetzt
die frühe Rückkehr „alle bekannten Ids da“, die eine Rückkehr nicht erkannte; folglich geht eine gone-Mission, die man
unter `remove` später abhakt, erst beim nächsten Sync mit Neuigkeit oder per Button (D43: ein Read ohne Neues ändert
nichts); E3 der Sync liefert einen Bericht (`ContractSync.Sync` → `SyncResult`, `GoneReport`) statt eines nachträglichen
Vergleichs `Changes(before, after)`; E4 das Label steht über der ComboBox (die 110-px-Zeile hätte es vierzeilig
umgebrochen); E5 „Known issues“ gibt es nur im Wiki (Troubleshooting), dort ergänzt, README unverändert.
- Core: `ContractSource.Gone`/`Detached`/`AsDetached`, `ContractSourceDto`, `PlanFile.SupportedSchemaVersion = 8`,
  `NotGoneSchemaVersion`, `PlanFileDto.CheckGone`, `ContractGonePolicy`, `SyncResult`, `GoneReport`, `ContractSync.Sync`
  (Apply = Sync.Saved), `Resync(…, onGone)`, `Quiet`, `Conflicts` entfernt, `LoadPlanner.RemoveDeliveries` (nutzt `Without`),
  `RetryUnplaced` ohne gone, `DeliveryKey` für abgehängte negativ
- App: `OverlayViewModel.OnContractGone`, `GoneText`, `AllGoneAtOnceText`, `HistoryClearedByDatabaseText`, `ClearHistory`,
  `KnownIds` ohne abgehängte; `NoticeSource.DeletedContracts` und `UpdateDeletedContracts` entfernt; `LegendEntry.Gone`
  (festes Segment ` · gone` nach der Id), `UnplacedEntry` mit `gone`, `MissionText.GoneText`, `MissionRow.Gone`/`GoneMark`;
  `ContractTrackerSettings.OnContractGone`/`GonePolicy`, `ContractGonePolicies`, `AppearanceRules.NormalizeContractTracker`,
  `ContractGoneChoice`, `ContractTrackerSectionViewModel.OnContractGone`/`GoneChoices`/`ResetCommand`, XAML, `App.xaml.cs`
- Tests: `ContractGoneTests` (Core, 23), `GoneOverlayTests` (App, 14), `SyncTimingTests` (Timing); umgestellt:
  `ContractSyncTests`, `LoadedStateTests`, `UndoRestoreTests`, `ReplanInPlaceTests`, Schema-Zahlen, `OverlayViewModelTests`,
  `ManualRowsTests`, `LoadedOverlayTests`; neu in `LegendLineTests`, `SettingsStoreTests`,
  `ContractTrackerSectionViewModelTests`, `SettingsWindowXamlTests`
- Übergang: Auf dem Core-Commit 9eca83f scheitern drei alte App-Tests (altes Entfernen), bis 1469c81 sie umstellt; gebaut
  hat jeder Commit.

**Zahl vorher/nachher (nur Sync-Pfad, Release, Median aus 5, `SyncTimingTests` auf dem Hurston-Plan vom 25.10., Hull A):**
Watcher-Read ohne Neues 0,01 → 0,00 ms, Button ohne Neues 0,05 → 0,05, zwei weg 0,07 → 1,22 (jetzt markiert: neue
Szene), zwei weg und alles abgeliefert 0,55 → 0,68; neu: remove 0,84, remove alles abgeliefert 0,54, Watcher-Read mit zwei
schon gone 0,00, zwei zurück 0,73; weit unter 16 ms (K2.4). Goldens nur `schemaVersion`, Slow grün. Tests Core
1.467 → 1.492, App 1.435 → 1.458, Import 141.

**Nebenbefund (nicht behoben):** `ContractTrackerSettings.DatabasePath` (berechnet) wird in settings.json mitgeschrieben
(`"databasePath": …`), beim Lesen ignoriert; das neue `GonePolicy` trägt `[JsonIgnore]`.

**Akzeptanz:**
- [x] keep: verschwundener Vertrag bleibt byte-gleich bis auf `gone`; Kisten, Zustände, Positionen, Haken unverändert
- [x] remove: geplant und abgeliefert weg, geladen bleibt mit Flag, gemischte Mission nur das Ungeladene weg; später nur
  ganz und ohne Geplantes/Geladenes (K1); wartend weg
- [x] Rückkehr entfernt das Flag, idempotent; alle auf einmal / neue Datenbank entfernen nichts, auch der zweite Sync nicht
- [x] Neue Datenbank: alte Ids abgehängt, neuer Vertrag #1 ist neu (K2); Undo-Historie geleert (E1, ratifiziert)
- [x] Schema 8 mit Migration 7 → 8 (Golden), `gone` in 7 und `detached` ohne `gone` abgelehnt
- [x] Undo nach markierendem Sync: Flag wieder da; Sync bleibt kein Schritt
- [x] Legende `· gone` fest nach der Id, Editor `#47 · gone`, Sortierung unverändert, Editor-Plan behält die Marke
- [x] `contractTracker.onContractGone` in settings.json, Settings-Fenster und Reset; unbekannter Wert → keep mit Problemzeile
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün; Zahl vorher/nachher
- [x] Look-Gate (Martin, 29.10.): `· gone` in der Kopfzeile bei 420 px; Zeile und Reset im Abschnitt ContractTracker
- [x] Gate (Martin, mit ContractTracker, 29.10.; Absturz bei `remove` gefunden und behoben, s. u.): Vertrag annehmen → Sync → laden → in CT löschen → bleibt mit `· gone`, Kisten unverändert;
  mit remove: abgeben → abhaken → Sync → weg
- Gate-Befund (Martin, 29.10.): `remove` mit geladenen Stopps 2 und 3 stürzte ab (Kisten behielten die Item-Indizes der
  gestrichenen Stopps, D68); `RemoveDeliveries` nummeriert sie neu, Regressionstest. Tests Core 1.491 → 1.492.
- Commits 1e264df (Tests rot Core), 9eca83f (Core), a406540 (Tests rot App), 1469c81 (App), af83a2f (Label), 9acfc6b
  (Doku), 8d64da5 (Test rot Absturz), 2b24418 (Fix); Wiki d486ad5.
- Offen (nicht Teil von S1): einmalige Notice bei teilweise gelöschten CT-Zeilen einer Aufnahme; ~~Auffangnetz, damit ein
  Fehler im Sync die App nicht beendet~~ (erledigt: Sync-Net, D82); Aufnahmen D3/D4 (Abschnitt ContractTracker) neu.

## Bugfix Sync-Net (D82) (29.10.2026) – Branch `fix/sync-safety-net`, eigener Merge nach Sync-Gone

### [x] N1 Auffangnetz Sync: ein Fehler kostet weder Plan noch App (D82) (abgenommen 29.10.2026)
**Warum:** Jede unerwartete Ausnahme im CT-Sync lief bis an den Dispatcher; das Overlay verschwand ohne Meldung, es gab
kein Log. Ein Fehler, der bei jedem Datei-Event wiederkehrt, konnte schleifen.

**Brief (Martin):** Catch nur im Timer-Tick-Handler, darunter wirft alles weiter; im Catch Plan unverändert, Notice mit
Typ und Meldung, Stacktrace ins Log; gleiche Meldung nur einmal bis zum nächsten Erfolg; nach 5 Fehlschlägen in Folge
Pause („Sync paused – check ContractTracker file“), Wiederaufnahme bei Änderung der CT-Datei oder manuellem Sync, Zähler
bei Erfolg auf 0; `DispatcherUnhandledException` nur loggen und Crash-Dialog, `Handled` bleibt false.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 der eine Catch sitzt in `RunSyncGuardedAsync`, das Tick
und Button/Hotkey aufrufen; P2 manueller Sync hebt die Pause nur bei Erfolg auf; P3 Log `%LocalAppData%\SC-CargoPlan\cargoplan.log`,
ab 1 MB nach `.log.1` (`File.Move` mit overwrite), `AppLog` wirft nie (Rückfall `Debug.WriteLine`); P4 `Saved`-Setter:
Szene bauen → Feld → `Scene` → `OnPropertyChanged(Saved)`, ohne `SetProperty`, Early-Return mit gleichem Vergleich (K1,
K2: kein Aufrufer setzt einen gleichen Plan zum Neuaufbau); P5 der ganze Tick-Körper im try, da
`ContractTrackerFileState.Of` werfen kann (K3); Pause-Stand = Dateistand vor dem fehlgeschlagenen Lesen, der Catch
fasst das Dateisystem nicht an; kein Timer-Neustart im finally (der Timer entprellt nur, Watcher-Events starten ihn);
P6 `_lastFailure = null` beim Pausieren; P7 `AppDomain.UnhandledException` nur loggen, ohne Doppeleintrag; P8
`SyncAsync` meldet, ob es gelesen oder nur eingereiht hat, ein eingereihter Aufruf zählt weder als Erfolg noch als
Fehlschlag; P9 Bug-Vorlage fragt nach `cargoplan.log`; Test für einen werfenden Szenenaufbau entfällt (kein Hook).

**Akzeptanz:**
- [x] Tick mit geworfener Ausnahme: Plan unverändert, App lebt, Notice, Log mit Stacktrace (Test; von Hand am Overlay)
- [x] Gleiche Meldung nur einmal bis zum nächsten Erfolg (Test)
- [x] Fünfter Fehlschlag pausiert, unveränderte Datei bleibt pausiert, CT schreibt → neuer Versuch (Test)
- [x] Erfolg setzt den Zähler zurück (Test)
- [x] Manueller Sync hebt die Pause nur bei Erfolg auf (Test)
- [x] Crash-Dialog: Meldung mit OK, App endet, genau ein Eintrag „Unhandled“ im Log (von Hand)
- [x] `AppLog`: ganzer Eintrag, Rotation, wirft nie (Tests); Build 0 Warnungen, Tests App 1.458 → 1.466, Core 1.492, Import 141

- Nicht von Hand geprüft: Pause und ihr Aufheben am Overlay (nur per Test).
- Commits cac558a (Doku), b91cc28 (Tests rot), 15be985 (Setter), 52f8514 (Netz, Log, Crash-Dialog).

## Bugfix Holo-Pump (D76-Addendum) (30.10.2026) – Branch `fix/holo-pump-regression`

### [x] P1 Maßstab beim Drehen festhalten, Grenzen schiffsfest (D76-Addendum)
**Warum:** Martin: Das Pumpen beim Drehen ist wieder da, gleich nach Start/Reset, ohne Zoomen.

**Befund (Schritt 2):** Kein Commit nach dem K5.5-Fix (9a0eef6) ruft im Drehpfad wieder den Fit auf. `OrbitBy`, `Navigate`,
`OnCameraChanged`, `HoloCamera`, `OverlayViewModel.Camera` und die XAML-Bindings sind unverändert; K5.2c und K5.3b liegen vor dem
Fix. Ursache ist ein Pfad, den K5.5 nicht abdeckte: K1.1 (bbb9314) und K5.2d (5e1e582) klemmen in `ViewNavigation.On` das
Framing bei jedem Zeichnen auf `ZoomLimits`, deren Untergrenze 0,5 × Fit(aktueller Winkel) war. Wo dieser Fit mehr als
doppelt so groß ist wie das Framing (Blick genau von vorn), hebt der Clamp den Maßstab an. Also keine Regression durch
einen neuen Commit, sondern eine Lücke im ersten Fix.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 Ursache wie im Befund, nicht ein Regressions-Commit;
P2 nur Drehen fixen, der Fit ohne Framing folgt weiter Fenster, Ladung und Holo (D76); P3 Grenzen aus dem Fit an der
Default-Kamera des Schiffs (`IsoProjection.DefaultFor(Ship.Bounds)`), auch die Obergrenze; P4 schiffsfest, nicht
szenenfest: `ProjectedScene.ShipExtent` = Fit-Extent mit allen Racks, beladen oder nicht (D61 nur für den Fit), Referenz
neu nur bei anderem Schiff, Holo-Ankunft oder anderem Marker; P5 dritter Wächter am Zoom-Anschlag, vierter für einen
Plan-Schritt am Anschlag; P6 Messung per Wegwerf-Harness mit der echten `ShipViewControl` (Reflection auf den
Orbit-Pfad), 420 × 420, Holo aus dem echten Cache; der echte Download mit leerem Cache bleibt Martins Gate.

**Messung `pxPerCell` (Schritt 1)**, Start an der Default-Kamera, je Ziel Yaw/Pitch ein Drehschritt vom selben Start:

| Schiff | Szenario | vorher 0/0 | vorher übrige Ziele (45/0, 90/0, 0/−30, 45/−30, 90/−30) | Spanne vorher | nachher alle Ziele | Spanne nachher |
|---|---|---|---|---|---|---|
| Hull A | (a) Start, (b) Reset, (c) Schiffswechsel, (d) Holo nach Fit | 22,028 | 17,790 | 23,8 % | 17,790 | 0,0 % |
| Hull A | (d2) Holo nach Drehen | 38,476 | 38,476 | 0,0 % | 38,476 | 0,0 % |
| Hull A | (e) Anschlag herausgezoomt | 22,028 | 8,895 · 8,895 · 12,451 · 8,895 · 8,895 | 147,6 % | 8,895 | 0,0 % |
| Hull C | (a)–(d) wie oben | 3,530 | 3,182 | 10,9 % | 3,182 | 0,0 % |
| Hull C | (d2) Holo nach Drehen | 7,150 | 7,150 | 0,0 % | 7,150 | 0,0 % |
| Hull C | (e) Anschlag herausgezoomt | 3,530 | 1,591 · 1,651 · 1,826 · 1,591 · 1,651 | 121,9 % | 1,591 | 0,0 % |

Zusatzzahl Obergrenze (Martin): Hull C ist kein langes Schiff (D61 greift nicht), Reset-Fit mit einem beladenen Rack
= leerer Plan = 7,15. Lange Schiffe, ein beladenes Rack, Keil: höchster Reset-Fit Syulen 147,9 (Aurora Mk II TS 137,9,
Fortune 99,0), alle unter 160 → nur ein Satz im Gegenargument.

**Akzeptanz:**
- [x] Messung vorher/nachher: jede Zeile identisch (Tabelle)
- [x] Ursache als Satz belegt (Befund)
- [x] Wächter `OrbitScaleTests`: Drehen aus dem Fit, Holo nach Framing, Drehen am Anschlag, Plan-Schritt am Anschlag; vor
  dem Fix rot außer „Holo nach Framing“ (seit K5.5 grün, bleibt als Wächter)
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (App 1.466 → 1.473, Core 1.492, Import 141; Slow grün)
- [x] Gate (Martin, Release-Build vom Branch, 30.10.2026, bestanden: „Kisten bleiben gleich groß, bzw. in scale“): Hull C
  mit Holo, Reset, von vorn und von oben drehen → Größe ruhig; bis zum Anschlag herauszoomen und drehen → ruhig
- Nicht von Hand geprüft: Holo-Download mit leerem Cache und danach drehen (nur per Harness, Szenario d).

**Teil 2 – das Holo selbst gemessen (Wegwerf-Harness, 30.10.2026):** Martins Befund danach: Das Holo atmet beim Drehen,
die Kisten nicht, beim GRIN UTV stärker als bei der Hull A. Gemessen über die echte WPF-Abbildung
(`GeneralTransform3DTo2D`) und gerenderte Pixel, Pitch 0, Yaw 0–360° in 15°-Schritten, 456 × 497, `holo-shaded`:

| Schiff | Kamera-Width | Holo-Höhe (WPF) | Würfel WPF = 2D | Dach − Würfel | gerenderte Höhe | gerenderte Breite |
|---|---|---|---|---|---|---|
| UTV | 3,884 konstant | 209,60 konstant | 117,40 = 117,40 | 46,10 konstant | 209 konstant | 246–397 |
| Hull A | 19,216 konstant | 106,30 konstant | 23,73 = 23,73 | 17,56 konstant | 106 konstant | 178–444 |
| Hull C | 107,438 konstant | 197,80 konstant | 4,24 = 4,24 | 107,39 konstant | 197 konstant | 198–424 |

Mit nur Ambient-Licht und Deckkraft 1 sind die gerenderten Maße identisch: kein Clipping (Near-Plane 0, Abstand 2000),
keine Fläche verschwindet; das Headlight ändert nur die mittlere Helligkeit um etwa ±8 %. Die Holo-Kamera hat keinen
eigenen Maßstab, sichtbar ändert sich allein die Silhouette.

**Teil 3 – Bisect mit Martin als Orakel (30.10.2026):** Release-Publishes je Stand auf `main`, Startskript mit gleichen
Einstellungen (UTV, keine gespeicherte Kamera, ContractTracker aus, leerer Plan), Martins Dateien nach jedem Lauf zurück.

| Build | Stand | Martin |
|---|---|---|
| 00 | 924a6f1 T7.4c-2 | Holo atmet, Kisten ruhig |
| 05 | 91af570 T7.4d | Holo atmet, Kisten ruhig |
| 10 | 3913bdf Merge K2 | Holo atmet, Kisten ruhig |
| 13 | 2922698 Merge K5 (vor K5.5) | Holo atmet, Kisten ruhig |
| 14 | 44353e2 Merge K5.5 | Holo atmet, Kisten ruhig |
| 15 | d92ca48 Merge K6 | Holo atmet, Kisten ruhig |

Ergebnis: keine Regression. Das Atmen des Holos ist die Silhouette des starr gedrehten Modells und seit T7.4c gleich,
es fiel nur bisher nicht auf. Kein Fix für Teil 2/3.

**Teil 5 – Drehpunkt und Frame-Log in Martins App (30.10.2026):**
- Drehpunkt (Wegwerf-Harness, 144 Frames, UTV, Hull A, Aurora Mk II, Pitch 0 und 35,26): Holo-Box-Mitte über WPF gegen
  Rack-Mitte über `IsoProjection`, Änderung über Yaw 0,000 px. Die Holo-Kamera übernimmt die Abbildung der Kisten, eine
  zweite Mitte gibt es nicht. Bei der Aurora ist `holoOffset` (0, 0, 1) ein fester senkrechter Versatz.
- Frame-Log (temporärer Release-Build über `run.ps1`, UTV, Aufnahme nach dem Render-Pass jedes Frames, Bild jedes Frames
  gespeichert): 119 Frames, ΔYaw, ΔPitch und ΔScale zwischen Kisten und Holo-Kamera in **jedem** Frame 0; Kisten konstant
  95,94 px/Zelle. Bei |Pitch| < 2° (46 Frames, Yaw 78–145°): gerenderte Holo-Höhe 210–215 px (bei gleichem Pitch 1,30°:
  213–214), Breite 368–398 px. Nach der vorab festgelegten Lesart: Lag 0, Höhe konstant, nur die Breite schwankt → die
  Drehung selbst, kein Bug. Die Bilder zeigen außerdem den 1-SCU-Würfel des UTV in der Fahrzeugmitte, nicht auf der
  Ladefläche (`HoloPlacement` legt die Holo-Mitte auf die Rack-Mitte, D63).

**Teil 1–5 in vier Sätzen:** Die Zoom-Grenzen hingen am Winkel und hoben am Anschlag und von vorn den Maßstab der Kisten
an; das ist behoben und mit vier Wächtern abgesichert (Teil 1). Die Holo-Kamera ist statisch korrekt: kein eigener
Maßstab, kein Clipping, derselbe Drehpunkt wie die Kisten (Teil 2, 5). Der Bisect zeigt das Atmen des Holos unverändert
seit T7.4c (Teil 3). In Martins App gibt es keinen Frame-Lag zwischen Kisten und Holo; was atmet, ist die Silhouette des
gedrehten Modells (Teil 5).

---

## Phase 8 – 3D-Schiffsansicht (D83) (30.10.2026, abgeschlossen 31.10.) – Branch `feat/p8-3d-view`, vor Phase 9 (Release)

**Warum:** Das Pumpen beim Drehen kommt von der orthografischen Projektion; mit einer `PerspectiveCamera` sind UTV und Hull A
ruhig (S8.0). Martins Entscheidung (30.10.2026): **Variante (a)** – die ganze Schiffsansicht wandert in einen `Viewport3D`,
Kisten, Holo, Marker und Hilfsgeometrie in einer Szene unter einer Kamera (D83). Der 2D-Painter (`SceneProjector`,
`DrawOrder`, `IsoProjection` als Zeichenpfad) entfällt am Ende der Phase (T8.6). Scope ist die Liste „2D-Funktionen, die neu
müssten“ unter S8.0.

**Regeln der Phase** (zusätzlich zu CLAUDE.md):
- Ein Task pro Session, Reihenfolge fest; jeder Task-Plan geht vor dem Code zur Freigabe (Martin/Claude.ai), Korrekturen
  kommen als `korrektur.md`.
- Core bleibt unverändert, alles in `CargoPlan.App`. Plan Schema 8, Schiff Schema 5, Mission Schema 3, Look Schema 1 bleiben.
- **Rendering zählt als Laufzeit:** Frame-Zeit und Hit-Test-Zeit vor und nach jedem Task messen. Messart wie S8.0: Abstand der
  Frames über `CompositionTarget.Rendering`, ≥ 300 Frames, Basislinie leeres Fenster. Messbefehl (seit T8.1), Release-Build,
  ohne SC, je Ziel 30 s, eine Zeile je Lauf in `measure-frames.log` neben der Exe, Exit-Code 0 = gültig:
  `dotnet publish src/CargoPlan.App -c Release -o ~/CargoPlan-measure` und dann
  `foreach ($t in 'empty','hull_c','utv','hull_a') { Start-Process ~/CargoPlan-measure/CargoPlan.App.exe -ArgumentList '--measure-frames', $t -Wait }`.
  Leichte Szenen (`empty`, `utv`) streuen um ±5 fps (M0, T8.1): Vergleiche mit mindestens zwei Läufen.
  **Messregime ab T8.2** (Präzisierung in der Planphase, ratifiziert durch Freigabe): Frame-Zeit in ms, Median und p95,
  **drei Läufe je Ziel, gewertet wird der Median der drei Läufe**; fps nur noch als Zusatzangabe. Gates werden gegen
  `empty` desselben Builds und gegen dasselbe Ziel vor dem Task formuliert (z. B. T8.2: p95 `hull_c_states` ≤ p95 `empty`
  + 2 ms; Abbruch, wenn p95 `hull_c_states` nach − vor dem Task > 2 ms). Ziel `hull_c_states` (seit T8.2): alle Zustände
  auf einmal (24 Ziele, erledigt, geladen, ein Fehler, eine Auswahl).
  **Gate ab T8.5** (Martin bei der M3b-Abnahme, 30.10.2026): **Anteil später Frames ≤ `empty` desselben Builds + 2
  Prozentpunkte**; p95 bleibt Information, weil es auf den Bildtakt quantisiert ist (33,9 ms = 2 Takte).
- **Bestehende Tests der Schiffsansicht sind das Orakel:** jedes Verhalten, das ein Test heute festnagelt (Gesten B3/B6/K3.2/
  T3.14b, Haken, Auswahl, Drop-Regeln, Fugen, Rack-Nummern), besteht nach dem Umbau denselben Test oder einen gleichwertigen.
  Kein Verhalten wird nebenbei geändert.
- **Handling gegen main (ab T8.4):** jeder Task-Plan enthält einen Abschnitt „Handling gegen main“, eine Liste der Gesten
  und Reaktionen, die der Task berührt (Maustaste, Modifier, Ort, Ergebnis, Notice), mit Soll aus `main` und Ist nach dem
  Task. Martin geht die Liste am Branch-Build und am `main`-Build mit derselben Plandatei durch, bevor das Gate zählt.
  Abweichungen sind Rückfragen, keine Befunde (Martin, 30.10.2026, während T8.3).
- **Kein paralleler 2D-Pfad**, kein `--legacy-2d`. Vergleichsstand für Screenshots und Verhalten ist ein Publish-Build von
  `main` (`dotnet publish src/CargoPlan.App -c Release -o ~/CargoPlan-main`). Bis T8.6 bleibt der alte Code liegen, wird aber
  nicht mehr aufgerufen.
- Spike-Code nur für Fragen, deren Antwort das Design ändert. Optik wird am echten Element geprüft, nie am Spike.
- Der Spike-Worktree `..\sc-cargoplan-s8` (Branch `spike/s8-3d`) bleibt bis zur T8.1-Basislinie: sein Frame-Zeit-Harness ist
  Messwerkzeug, kein Spike-Code (Präzisierung in der Planphase, ratifiziert durch Freigabe).

### [x] S8.0 Spike: Schiffsansicht als echtes 3D (Perspektive) (30.10.2026, kein Prod-Code)
**Frage:** Liest sich der GRIN UTV in einem `Viewport3D` mit `PerspectiveCamera`, weltfestem Licht und Bodenplatte beim
Drehen als Drehung statt als Pumpen (Anlass: Holo-Pump, Teil 5)? Was kostet der Umbau der 2D-Funktionen?

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Wegwerf-Code im Worktree `spike/s8-3d` (nie committet),
Start per `--spike-3d`; ein Fenster mit `AllowsTransparency` wie das Overlay (Software-Rendering, D2), links die heutige
`ShipViewControl` + `HoloBackdrop`, rechts zwei `Viewport3D` mit derselben Kamera (Holo und Boden hinten in der
Holo-Deckkraft, Quader vorn); Diagnose-Schalter P (Perspektive/ortho), L (Licht weltfest/Headlight), F (Boden); E1:
rechts dieselbe Kamera wie links, `Width` fest aus dem Maßstab (kein Fit pro Winkel), Start an der Default-Kamera des
Schiffs, Perspektive mit gleicher Bildbreite am Drehpunkt (FOV 40°); E2: feste Gate-Reihenfolge Step 0–4, je UTV und
Hull A, Maßstab fleetyards.net/ships/grin-utv; „600+ Quader“ als Hull C mit 4.608 Einzelzellen (schlimmster Fall) und
144 Kisten (real), bei Rot eine Zeile „alle Zellen als ein Mesh“; Frame-Zeit ohne SC; Step 5 (deckend) nach den ersten
zehn Urteilen; Look rechts wie Martins Setting (`holo-shaded`, Deckkraft 0,45), Quader einfarbig.

**1 – Gate (Martin, Release-Build, 30.10.2026), UTV und Hull A gleich:**

| Step | P | L | F | Holo | links (heute) | rechts |
|---|---|---|---|---|---|---|
| 0 | ortho | Headlight | aus | 0,45 | pumpt | pumpt |
| 1 | ortho | weltfest | aus | 0,45 | pumpt | pumpt |
| 2 | ortho | weltfest | an | 0,45 | pumpt | pumpt |
| 3 | persp | weltfest | an | 0,45 | pumpt | pumpt nicht |
| 4 | persp | Headlight | aus | 0,45 | pumpt | pumpt nicht |
| 5 | persp | weltfest | an | deckend, Kisten im selben Tiefenpuffer | pumpt | pumpt nicht |

- Martin trug die Urteile als links / rechts ein (Vorlage hatte Spalten UTV / Hull A); auf Nachfrage: „UTV und Hull A
  gleich“. Ab Step 3 „pumpt da rechts nichts mehr“.

- Step 0 reproduziert die App (rechts pumpt wie links); per Code geprüft: 96 Kameras je Schiff (Yaw 0–345°, vier Pitches),
  Abweichung rechts gegen `HoloCamera.For` der linken Ansicht ≤ 2·10⁻¹³ Zellen, Maßstab links konstant.
- Ein erster Durchgang (Ziffernblock 1/2/3 im Step 0) ist verworfen: der Spike kannte den Ziffernblock noch nicht, das
  Schiff wechselte vermutlich nicht.

**2 – Hit-Test** (`VisualTreeHelper.HitTest`, `PointHitTestParameters`, erster Treffer, 600 Punkte bei 60 Hz unter
`CaptureMouse`, Hull C, ohne SC; Schwelle p95 ≤ 5 ms):

| Inhalt | Median | p95 | max |
|---|---|---|---|
| 4.608 Zellen, je ein `GeometryModel3D`, persp | 0,004 ms | 0,22 ms | 0,71 ms |
| 4.608 Zellen, je ein Modell, ortho | 0,004 ms | 0,19 ms | 0,56 ms |
| 4.608 Zellen als ein Mesh (Treffer über Dreiecksindex), persp | 0,005 ms | 0,64 ms | 1,80 ms |
| 144 Kisten, persp | 0,005 ms | 0,02 ms | 0,06 ms |

**3 – 2D-Funktionen, die neu müssten:**

| Funktion | heute | in 3D | Aufwand |
|---|---|---|---|
| Kisten-Painter | `ShipViewControl.OnRender`, `SceneProjector` | ein Mesh je Rack und Farbe (Einzelmodelle kosten 30 fps, Messung 4); Kanten als Streifen wie `HoloLines`, `Viewport3D` zeichnet keine Linien | M |
| Trennachsen | `DrawOrder.cs` | entfällt (Tiefenpuffer); nur halbtransparente Kisten (`CrateFillAlpha`) brauchen wieder eine Reihenfolge | S |
| Rack-Nummern fester Pixelgröße | `RackNumberSize`, `OnRender` | 2D-Ebene über dem `Viewport3D`, Ankerpunkte perspektivisch projiziert | M |
| Tooltips | `SceneHitTest.ToolTipAt` | 3D-Treffer → Kiste, Texte aus `ShipViewBuilder` bleiben | S |
| Hit-Test, Ziel-Zelle | `SceneHitTest`, `DropTarget` | Kiste per `HitTest`; Zelle unter dem Cursor per Strahl-Ebene-Schnitt statt Unproject | M |
| Drag-Vorschau | `UpdateGesture`, `CrateDrop`, `DragGesture`, Geist | Geist als Modell, Ziel per Strahl; Core-Logik (`CrateDrop`) bleibt | L |
| Legenden-Drag | `StartLegendDrag` | hängt am Drag-Code, Ziel-Rack per Treffer | S |
| Umriss (D62) | `OutlinePlacement` | entfällt bei Holo; ohne Holo flaches Mesh auf dem Boden | S |
| Keil-Fallback (D52) | `DrawBowWedge`, `FloorLabel` | Keil als Mesh, „Bow“ in der 2D-Ebene | S |
| Auswahlrand | `SelectionFocus`, Pens | Kantenstreifen oder Emissive-Material | M |
| Schraffur (D29) | `StopBrushes` | `DrawingBrush` als Material, Texturkoordinaten je Fläche | M |
| Freies Zellraster | `FreeCellGrid` | Linien als Streifen | M |
| Kamera, Zoom-Grenzen, Pan, Persistenz (D76) | `ViewNavigation`, `ZoomLimits` | Distanz statt Maßstab, px/Zelle nur am Drehpunkt definiert, Zoom zum Cursor perspektivisch | M |
| Fit (D61) | `ViewTransform.Fit` | Fit über die projizierten Ecken bei Perspektive | S |

**4 – Frame-Zeit** (Hull C, Orbit 12°/s, 30 s, Abstand der Frames über `CompositionTarget.Rendering`, ohne SC):

| Inhalt | Frames | Median | p95 | fps |
|---|---|---|---|---|
| Basislinie (rechte Hälfte leer) | 1.771 | 16,67 ms | 16,68 ms | 59,0 |
| heute, links 2D + Holo | 1.758 | 16,67 ms | 16,68 ms | 58,6 |
| 144 Kisten + Holo, persp | 1.717 | 16,67 ms | 16,95 ms | 57,2 |
| 4.608 Zellen, je ein Modell + Holo, persp | 921 | 33,35 ms | 33,35 ms | 30,7 |
| 4.608 Zellen, je ein Modell + Holo, ortho | 923 | 33,35 ms | 33,35 ms | 30,8 |
| 4.608 Zellen als ein Mesh + Holo, persp | 1.738 | 16,67 ms | 16,95 ms | 57,9 |
| 4.608 Zellen als ein Mesh + Holo, ortho | 1.704 | 16,67 ms | 32,53 ms | 56,8 |

Die vorab gesetzte Schwelle (Δ Median ≤ 1 ms, p95 ≤ 16 ms) stammt aus T7.4b, das die Renderzeit maß; der Abstand der
Frames liegt bei 60 Hz schon leer bei p95 16,7 ms. Gewertet nach fps und Abstand zur Basislinie: Kisten und ein Mesh grün,
Einzelmodelle rot. Mit SC nicht gemessen.

**Was das für den Umbau heißt:**
- Wirksam ist allein die **Perspektive**: UTV und Hull A ruhig ab Step 3, auch ohne weltfestes Licht und Boden (Step 4).
  Licht, Bodenplatte und deckender Rumpf ändern am Pumpen nichts; ein Licht-Einzeiler in `HoloBackdrop` reicht also nicht,
  die Kamera muss perspektivisch werden.
- Damit ist die Spike-Frage für das Anlass-Schiff beantwortet: Der UTV liest sich in Perspektive als Drehung.
- Leistung trägt: `Viewport3D` im Software-Rendering zeigt Hull C mit Holo bei 57–58 fps, sofern Quader zu wenigen Meshes
  zusammengefasst sind; Hit-Test ist kein Thema.
- Umfang bei Umbau: 14 Funktionen, davon 1 × L (Drag-Vorschau), 7 × M, 6 × S. Weil Licht und Boden nichts beitragen,
  ist der kleinste wirksame Schritt die perspektivische Kamera; ob Kisten dafür in den `Viewport3D` wandern oder die 2D-
  Ansicht perspektivisch projiziert, ist eine eigene Entscheidung (D-Eintrag) vor dem Umbau-Task.
- Nebenbefund im deckenden Look (Step 5): die Kisten stecken im Rumpf (UTV im Fahrerhaus, Hull A durch die Flanken),
  weil `HoloPlacement` die Holo-Mitte auf die Rack-Mitte legt (D63) – im transparenten Look heute unsichtbar, deckend nicht.

### [x] T8.0 D83, Phasen 8–10, Lehre (30.10.2026, abgenommen 30.10.)
**Warum:** Vor dem Umbau stehen Entscheidung, Taskschnitt und Formatfolgen fest; T8.1 beginnt erst nach Freigabe von T8.0.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 S8.0 zuerst per `--no-ff` nach `main` (14f4889), dann
`feat/p8-3d-view` ab dem neuen `main`; geprüft vorher, dass der Holo-Pump-Fix (ed03709) samt `OrbitScaleTests` in `main` liegt.
P2 die alte „Phase 8 – nach dem Launch“ wird Phase 10 (T10.1–T10.3), D53-Addendum. P3 Phase 9 bekommt neue IDs T9.1–T9.4,
T7.5a–c bleiben, T7.5d zeigt auf T9.2. P4 D83 (D81 und D82 vergeben). P5 Addenda jetzt nur D14, D15, D53, D76; D52, D62, D63,
D32, D61 im umsetzenden Task. P6 Spike-Worktree bleibt bis zur T8.1-Basislinie, T8.1 portiert den Harness. K1 T9.1 umfasst auch
die Textdurchsicht auf durch D83 geänderte Begriffe.

**Akzeptanz:**
- [x] D83 in `docs/decisions.md` mit (a)/(b), S8.0-Zahlen, Formatfolgen, Gegenargument; Addenda D14, D15, D53, D76
- [x] Phase 8 mit T8.0–T8.7, Phase 9 (Release), Phase 10 (nach dem Launch) in TASKS.md; Backlog-Zeile Viewport3D geschlossen
- [x] Lehre in TASKS.md (Lehren) und CLAUDE.md (A9, Zeile Schiffsansicht)
- [x] `dotnet build` ohne Warnungen, `dotnet test` grün (Core 1.492, App 1.473, Import 141; nur Doku geändert)

### [x] T8.1 `CameraMath`, Szene und Kamera (30.10.2026, abgenommen 30.10.)
**Warum:** Die Projektion ist das Fundament: Bild, Overlay, Hit-Test und Gesten-Tests hängen an einer Kamera (D83).
- `CameraMath` (WPF-frei: Yaw, Pitch ±85°, Distanz, Ziel, FOV 40°, Viewport-Größe; `Project`, `Unproject`), daraus die
  `PerspectiveCamera`; Konformitätstest gegen `GeneralTransform3DTo2D` (Rack-Ecken, Kistenmitten, mehrere Kameras, 0,5 DIP,
  STA, gemessener und arrangierter `Viewport3D`); testseitiger Projektionshelfer für Klickpunkte
- `Viewport3D` in `ShipViewControl`: Kisten einfarbig, Holo als letztes Kind, Keil, drei weltfeste Lichter (Haupt, Ambient,
  schwaches Gegenlicht von unten)
- Orbit, Zoom (Distanz), Pan (Ziel); Reset, Doppelklick, Reset-Symbol, Hotkey `resetView` unverändert; Kamera-Persistenz
  `yaw`/`pitch`/`distance`/`target`, Einträge mit `pxPerCell` verworfen (D83)
- Frame-Zeit-Harness aus dem Spike als Dev-Schalter portieren, Befehl in den Phasenkopf; Basislinie damit messen, danach
  Spike-Worktree und Branch löschen
- Addenda D52, D63 (Holo in der Szene, Keil in 3D)

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** R1 Hit-Test bis T8.3 aus: Klicks auf die Ansicht treffen
nichts (kein Haken, kein Move, keine Auswahl, kein Tooltip; Doppelklick setzt überall zurück, Rechtsklick leert die Auswahl).
R2 FOV horizontal wie WPF, D83-Formel mit der Breite `W` (D83-Addendum). R3 Zoom-Grenzen schon in T8.1 (T8.5 behält Text und
Presets). R4 Ergebnis in `measure-frames.log` neben der Exe, eine Zeile je Lauf, Exit-Code 0/1 (kein stderr: kein `Console`
in der App). R5 Holo-Halbtransparenz: bei Artefakten Stopp, Versuche in fester Reihenfolge (nur Vorderseiten,
Alpha-Untergrenze, Deckkraft 1); ein zweiter `Viewport3D` wäre eine D83-Revision. R6 `docs/settings-format.md` im
Format-Commit. R7 im Umriss-Modus bis T8.5 kein Bug-Marker. R8 Downgrade hingenommen (D83-Addendum, Known issues in T9.1).
K1 Altfelder `pxPerCell`/`panX`/`panY` nur befristet lesbar (Backlog). K2 Hit-Test-Basislinie siehe M0/M1. Dazu beim Bauen:
`CameraMath.ApplyTo` setzt die WPF-Kamera (nicht das Control), damit der Konformitätstest genau diesen Weg prüft; der private
2D-Zeichencode des Controls ist entfernt (hing an `IsoProjection`), die öffentlichen Painter-Klassen bleiben bis T8.6;
`View`/`ShipViewState` entfallen (einziger Abnehmer war `HoloBackdrop`). Gate fps siehe unten.

**Befund:**
- Commits 60382f2 (Harness, M0), a0b96e1 (`CameraMath`, Konformitätstest), 36d8865 (Navigation, Fit, Grenzen), da83662
  (Persistenz), e4ec2d9 (Szene, Control, XAML), dieser (Doku). Spike-Worktree und `spike/s8-3d` nach M0 gelöscht.
- Konformität: UTV, Hull A, Hull C × 6 Kameras × 3 Fenstergrößen innerhalb 0,5 DIP. Gegenprobe: Fokus aus der Höhe
  (vertikales FOV) → alle drei rot.
- Die vier Holo-Pump-Wächter laufen als `CameraNavigationTests` auf der neuen Kamera; jeder einzeln gegengeprüft: Orbit, der
  neu fittet (Wächter 1, 2), Grenzen vom aktuellen Winkel (3), Referenz, die der Ladung folgt (4).
- Kein bestehender Test prüfte den Hit-Test des Controls gegen sein Bild: zwischen T8.1 und T8.3 ist der Live-Pfad ungedeckt.
- Holo-Kanten (Hidden-Line) sind nur in der Zielebene genau 1 px breit, vorn etwas breiter, hinten schmaler; Prüfung in T8.2
  mit den Fugen.
- Tests in T8.1:

  | Test | Stand | Grund |
  |---|---|---|
  | `SettingsStoreTests` Kamera-Round-Trip | umgeschrieben auf `distance`/`target`, + 3 neue | Altfeld-Einträge fallen beim Laden weg, Nullwerte werden nicht geschrieben |
  | `SettingsSessionTests` (Look-Import behält Kameras) | auf neue Felder umgestellt | Sinn bleibt |
  | `StartOptionsTests` | grün, + 5 Fälle `--measure-frames` | – |
  | `OrbitScaleTests`, `ShipCameraTests` (Rahmen-Teil), `ViewTransformTests` | grün, migriert in T8.1, gestrichen in T8.6 | testen `ViewNavigation`/`ZoomLimits`, die das Control nicht mehr nutzt; Gegenstücke in `CameraNavigationTests` |
  | `HoloViewTests` (`HoloCamera.For`), `SceneProjectorTests`, `DrawOrderTests`, `IsoProjectionTests` (Projektion), `RackNumberSizeTests`, `FreeCellGridTests` | grün, gestrichen bzw. migriert in T8.2/T8.6 | 2D-Painter und Holo-Kulisse, unbenutzt seit T8.1 |
  | `SceneHitTestTests`, `DropTargetTests`, `DragGestureTests`, `CrateDropTests`, `CrateSelectionTests`, `CrateTurnTests`, `SelectionFocusTests` | grün, Orakel für T8.3/T8.4 | reine Klassen, unverändert |
  | `ShipViewLockTests`, `OverlayLegendXamlTests` | grün | Sperre, Legenden-Drag-Start und Theme-DPs bleiben |
  | `StopHatchTimingTests` (nur von Hand) | misst seit T8.1 nichts Sinnvolles | Schraffur kommt in T8.2 |

- Neu: `CameraMathTests` (13), `CameraConformityTests` (3), `CameraNavigationTests` (20), `FrameStatisticsTests` (7). Tests App
  1.473 → 1.524, Core 1.492, Import 141; 0 Warnungen.
- **M0/M1** (`--measure-frames`, Release, ohne SC, 30 s je Lauf; fps, mehrere Läufe):

  | Ziel | M0 2D (60382f2) | M1 3D (e4ec2d9) |
  |---|---|---|
  | `hull_c` 144 Kisten + Holo | 58,8 / 59,5 | 53,3 / 54,6 / 55,0 |
  | `hull_a` 4 Kisten + Holo | 57,1 / 57,9 | 54,3 / 52,3 |
  | `utv` 1 Kiste + Holo | 57,6 / 52,4 | 55,8 / 52,9 |
  | `empty` | 52,2 / 53,2 | 53,1 / 51,2 |
  | Hit-Test p95 | ≤ 0,007 ms (alter Pfad, Referenz) | entfällt (R1); 3D-Basislinie = M2 in T8.3 |

  Median überall 16,67 ms, p95 33 ms in M1. Das leere Fenster ist in beiden Builds so langsam wie M1; ein Neuzeichnen pro
  Frame änderte M1 nicht (52,5 / 52,1). Vermutet wird die Taktung des Layered Window, nicht die Szene; belegt ist es nicht.
  Erster Versuch einer leeren Basislinie ohne Änderung pro Frame: 32 fps (WPF taktet ein unverändertes Fenster herunter).

**Akzeptanz:**
- [x] Konformitätstest grün
- [x] UTV und Hull A: kein Pumpen (Martin, Release-Build vom Branch, 30.10.2026: „perfekt, kein Pumpen, Freigabe erteilt“)
- [ ] Hull C mit Holo ≥ 55 fps: nicht erreicht (53,3 / 54,6 / 55,0); hingenommen mit der Freigabe, Messung mit SC in T8.7.
  Martin am Branch-Build: „Hull C: flüssig und ohne Pumpen“
- [x] Spike-Worktree und Branch gelöscht, nachdem die Basislinie steht
- [x] `dotnet build` ohne Warnungen, `dotnet test` grün
### [x] T8.2 Darstellung der Kisten (30.10.2026, abgenommen 30.10.)
**Warum:** Alle Zustände, die die 2D-Ansicht zeigt, müssen in 3D gleich lesbar sein.
- Stop-Farben (`StopBrushes` als Material), Schraffur ab der zweiten Farbrunde, Erledigt-Grau, Geladen-Symbol (K6.4), offShip,
  Fugen (Inset, T3.11b), Problem- und Auswahlrand (Kanten-Material oder Rahmenquader, Entscheidung mit Bild am echten Element)
- Freies Zellraster und „alle Zellen“ als ein Mesh pro Rack
- Rack-Nummern im 2D-Overlay (`RackNumberSize`), ausgeblendet bei Verdeckung (Strahl Kamera → Anker trifft vorher ein anderes
  Rack oder die Holo-Box); Addenda D32, D61 soweit betroffen
- Aus T8.1: „Bow“-Label des Keils im Overlay (D52-Addendum); Platz oben für die Nummern langer Schiffe im Fit (D61, in T8.1
  ohne); die Theme-DPs des Controls (`CrateFillAlpha`, `StopPalette`, Kanten, Auswahl) wieder anschließen; Breite der
  Holo-Kanten in Perspektive mit den Fugen prüfen

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** R1 Fehler-, Auswahl- und Missionsrahmen obenauf in einer
2D-Überlagerung (`DrawingVisual` über dem `Viewport3D`) mit den alten px-Stiften, Kisten-, Geladen-, Rack-, Rahmen- und
Rasterkanten als verdeckbare Streifen in der Szene; am Bild gegen `main` bestätigt. R2 nur Racks verdecken Nummern, das Holo
nicht (seine Box enthält jedes Rack; D83-Addendum). R3 Schraffur in Bildschirmgröße, Kachel in Zweierpotenzen von Zellen
gestuft. R4 Rahmen um `Ship.Bounds` als gestrichelte Streifen; Streichung ggf. eigene Entscheidung in T8.5. R5 Martin nimmt
die Bilder auf, Plandateien `docs/shots/t82-hull-a.plan.json` und `t82-c2.plan.json` per `--plan` in beiden Builds. K1
Messregime (Phasenkopf), Gate p95 `hull_c_states` (M1b) ≤ p95 `empty` + 2 ms, Abbruch bei M1b − M1a > 2 ms. Dazu beim Bauen:
die Nummer eines kompakten Schiffs steht auf der größten unverdeckten sichtbaren Fläche (mindestens ein Viertel der größten),
statt sich hinter einer verdeckten Oberseite auszublenden; wörtlich genommen blendete die D83-Regel in der Standardansicht die
Hälfte der Hull-A-Nummern aus. „offShip“ gibt es in der Szene nicht (D75: nicht gezeichnet), „alle Zellen“ ist kein eigener
Modus, sondern das freie Raster ab 12 px (D61).

**Befund:**
- Commits f15a9ba (Messziel `hull_c_states`, Anteil später Frames, M1a), 0477fe9 (`RackLabels`, `BoxFaces`, `BoxEdges`,
  `RayBox`, Platz oben im Fit), a3fe64f (`CrateMaterials`, Texturkoordinaten), 38f7693 (Zustände, Sortierung, Streifen),
  74f4de8 (`ShipOverlay`, M1b), 41cd34f (Plandateien für die Bilder), dieser (Doku).
- Kachel mit `ViewportUnits.Absolute` wird in Texturkoordinaten gemessen, die Doku sagt dazu nichts: `HatchRenderTests` hält
  es fest (2, 4, 8 Kacheln auf 4 Zellen bei Kachel 2, 1, 0,5).
- Holo-Kanten waren seit T8.1 nur 0,82 px breit (`HoloLines.HalfWidthCells` teilt durch die Ortho-Streckung); jetzt 1 px in
  der Zielebene.
- Bekannte Grenze: Holo-Teile hinter einer halbtransparenten Kiste fallen weg (die Kiste schreibt die Tiefe zuerst); bei 0xD0
  kaum sichtbar.
- Schraffur im 3D-Pfad (`StopHatchTimingTests`, Release, C2 mit 16 Stopps): 48,0 ms ohne, 48,4 ms mit Schraffur je Bild.
  Keine Vorher-Zahl: der Test misst seit T8.1 das Control, in T8.1 ohne Schraffur.
- **M1a/M1b** (`--measure-frames`, Release, ohne SC, 30 s je Lauf, Median aus drei Läufen; p95 / spät / fps):

  | Ziel | M1a vor T8.2 (e034a23) | M1b nach T8.2 |
  |---|---|---|
  | `hull_c_states` 145 Kisten, 24 Stopps, alle Zustände + Holo | 31,97 ms / 5,2 % / 57,0 | 16,95 ms / 4,2 % / 57,5 |
  | `hull_c` 144 Kisten + Holo | 33,07 ms / 7,8 % / 55,4 | 16,68 ms / 1,9 % / 58,9 |
  | `empty` | 33,07 ms / 9,6 % / 54,6 | 33,07 ms / 12,1 % / 53,3 |

  Gate erfüllt (16,95 ≤ 33,07 + 2), kein Abbruch (M1b − M1a = −15 ms). Dass die volle Szene schneller wird als vorher und
  als das leere Fenster, ist die Taktung des Fensters, kein Gewinn. M1b lief auf dem Arbeitsstand von Schritt 5 vor dessen
  Commit (Log-Hash 38f7693, `ShipOverlay` in der DLL), also auf dem Code von 74f4de8.
- Tests in T8.2 (rot wurde kein bestehender Test):

  | Test | Stand | Grund |
  |---|---|---|
  | `RackNumberSizeTests`, `FreeCellGridTests` | grün, unverändert | Eingabe ist px/Zelle in der Zielebene; `Default_is_the_size_at_zoom_1` wird in T8.6 neu begründet oder gestrichen |
  | `DropTargetTests` | grün, unverändert | Slab-Test nach `RayBox` herausgezogen |
  | `SceneProjectorTests` | grün, unverändert, gestrichen in T8.6 | `FloorOf` aus `ProjectWedge` herausgezogen |
  | `StopBrushesTests`, `OverlayLegendXamlTests`, `PaletteBrushConverterTests` | grün | 2D-API und Legenden-Marker unverändert; Legende und Kisten teilen `HatchTile` |
  | `CameraNavigationTests` | grün | `top` mit Default 0 |
  | `FrameStatisticsTests` | + 1 | Anteil später Frames |
  | `StopHatchTimingTests` (nur von Hand) | unverändert | misst seit T8.2 den 3D-Pfad mit Schraffur |

- Neu: `RayBoxTests` (5), `RackLabelsTests` (7), `BoxEdgesTests` (3), `CameraFitTests` (2), `CrateMaterialsTests` (12),
  `HatchRenderTests` (3), `ShipScene3DTests` (8). Gegengeprüft: Sortierung umgedreht, eine gespiegelte Fläche → je rot.
  Tests App 1.524 → 1.565, Core 1.492, Import 141; 0 Warnungen.

**Akzeptanz:**
- [x] Screenshots gegen den `main`-Build, jeder Zustand einmal sichtbar (Martin, beide Plandateien in beiden Builds,
  30.10.2026: „passt, Freigabe“)
- [x] Blick von unten geprüft (Unterseiten-Racks, Gegenlicht, Nummern; Bild 11 der Liste)
- [x] Frame-Zeit vorher/nachher (M1a/M1b oben)
- [x] `dotnet build` ohne Warnungen, `dotnet test` grün

### [x] T8.3 Hit-Test und Auswahl (30.10.2026, abgenommen 30.10.)
**Warum:** Hit-Test und Drop-Regeln ziehen in Strahl-Logik um – die Stelle mit dem höchsten Regressionsrisiko, darum vor dem Drag.
- Strahl gegen Quader auf den Rack-/Kistendaten (nicht `VisualTreeHelper.HitTest`), Tooltips (`SceneHitTest.ToolTipAt`),
  Rechtsklick-Auswahl mit Versatz (T3.14b), Kistenklick hakt das Ziel ab (B6), K1.4-Grenze
- Gesten-Tests auf den Projektionshelfer umstellen (hier, nicht erst in T8.6)
- Aus T8.1: `ShipViewControl.HitAt`/`DestinationAt` liefern seit T8.1 null (R1) – hier ersetzen; neue Tests klicken über
  `ScreenPoints` auf die echte Ansicht (bisher prüfte kein Test den Hit-Test des Controls gegen sein Bild); Hit-Sweep im
  `FrameMeasureWindow` wieder einschalten (M2 = 3D-Basislinie, Referenz M0 alter Pfad p95 ≤ 0,007 ms)

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** R1 Treffer-Regel wie `main` (D21): das erste Rack auf der
Sichtlinie entscheidet, auch auf freien Zellen, darin die erste Kiste; E1 die andere Regel (nächste Kiste über alle Racks,
Ablage behält „erstes Rack“) als Backlog-Zeile mit Auslöser. R2 Doppelklick wie `main` (D16): auf Fracht zweimal haken,
Reset nur daneben. R3 Treffer auf den exakten Zellquadern der Daten, nicht den eingerückten gezeichneten. R4 Testnaht
`InternalsVisibleTo` + `internal Press`/`Release`/`ToolTipAt`, keine öffentliche API für Tests. R5 Klickprobe durch Martin
(DPI). Dazu aus der Planung: `CrateSelectionTests` und `SelectionFocusTests` haben keine Klickpunkte, es gab nichts
umzustellen; „Versatz in Schiffskoordinaten“ (T3.14b) ist eine Drag-Regel und kommt mit dem Drop in T8.4 (bis dahin tut
ein Rechts-Ziehen von einer Kiste nichts); „Hover-Zustand“ ist in `main` nur der Tooltip. Beim Bauen: `InternalsVisibleTo`
als `AssemblyAttribute`-Item, weil das gleichnamige MSBuild-Item nicht in der MS-Doku steht.

**Befund:**
- Commits 408e721 (`RayBox.Span`/`Ahead`), 362ee61 (`SceneHitTest` auf Strahlen), 0b22b1d (Control live, Testnaht,
  `ShipViewHitTests`), 55ef0e3 (Hit-Sweep, M2), dieser (Doku).
- Der Hit-Test nimmt die Kamera, mit der das Bild gezeichnet wurde (`_shown` aus `ApplyCamera`), keine zweite. Holo, Umriss
  und Keil sind keine Szenendaten: sie treffen nicht und verdecken nicht. Ein Quader hinter der Kamera zählt nicht, einer um
  die Kamera ab der Kamera (`RayBox.Ahead`).
- Zwei Lücken der alten Tests geschlossen: kein Test hielt R1 fest (im Fall „Rack vorn gewinnt“ hat das vordere Rack selbst
  Fracht, Regel b gäbe dasselbe) → Wächter `An_empty_rack_in_front_covers_the_cargo_behind_it` (E1-Fall); die ersten
  Live-Tests merkten eine falsche Hit-Kamera nicht (Rack 1 ist voll Riker, ein verschobener Klick trifft auch Riker) → jede
  Kiste auf den oberen Racks wird einzeln geprüft, vor und nach einem Orbit.
- Ein Testpunkt war entartet: von unten streift die Sichtlinie zu (1, 2, 1,5) genau die Kante des Agricium bei (2, 3) und
  kippt in Perspektive hinein; jetzt x = 0,5 auf derselben Fläche, Erwartung unverändert.
- Klickprobe Martin am Branch-Build mit voller Hull C und allen Zuständen (Plan aus dem Messziel `hull_c_states`),
  Windows-Skalierung ≈ 110 % (106 DPI) auf 3440 × 1440: „kein Ruckeln“, abgenommen.
- **M2** (`--measure-frames`, Release, ohne SC, 30 s je Lauf, Median aus drei Läufen; p95 / spät / fps):

  | Ziel | M1b (T8.2) | M2 (T8.3) |
  |---|---|---|
  | `hull_c_states` | 16,95 ms / 4,2 % / 57,5 | 33,07 ms / 5,9 % / 56,4 |
  | `hull_c` | 16,68 ms / 1,9 % / 58,9 | 16,68 ms / 2,2 % / 58,7 |
  | `empty` | 33,07 ms / 12,1 % / 53,3 | 33,07 ms / 12,5 % / 53,1 |
  | Hit-Test `hull_c` (600 Punkte) | aus seit T8.1 (M0 alter Pfad: p95 0,007 ms, 30 Treffer) | p95 0,002 ms, max 0,59 ms, 23 Treffer |

  Gate Hit p95 ≤ 1 ms erfüllt; K1-Gate p95 `hull_c_states` ≤ p95 `empty` + 2 ms erfüllt. **Abbruchkriterium gerissen:**
  `hull_c_states` gegen M1b +16,1 ms, p95 kippte in zwei von drei Läufen (16,95 / 33,07 / 33,07). Gestoppt mit Zahlen;
  Martin hat nach eigener Probe trotzdem abgenommen („kein Ruckeln“). Am Zeichenpfad änderte T8.3 nur die Zuweisung von
  `_shown` in `ApplyCamera`; dass es Streuung ist, ist vermutet, nicht belegt.
- Tests in T8.3 (rot wurde kein bestehender Test außer `SceneHitTestTests` beim Umzug, die Datei kompilierte erst mit der
  neuen API):

  | Test | Stand | Grund |
  |---|---|---|
  | `SceneHitTestTests` (26 → 30) | migriert, gleiche Erwartungen | Kamera mit Standard-Ausrichtung 200 Zellen weit, Punkte über `CameraMath.Project`; K1.4 mit Viewport 400 × 300; Fugen-Test umbenannt (R3, weiter „Riker“); ein Punkt von unten verlegt (s. o.); der `DropTarget`-Teil des Rechts-Zieh-Tests bleibt orthografisch bis T8.4; + 4 neu (Nahsicht, Kamera im Rack, Fracht hinter der Kamera, leeres Rack vorn) |
  | `DragGestureTests`, `CrateTurnTests`, `CrateSelectionTests`, `SelectionFocusTests`, `CrateDropTests` | grün, unverändert | rein, ohne Klickpunkte |
  | `DropTargetTests`, `RackLabelsTests` | grün, unverändert | `Entry` aus derselben `Span` |
  | `RayBoxTests` | + 5 | `Span`, `Ahead` (vor, um, hinter der Kamera, daneben) |
  | `CameraConformityTests` | grün | WPF-Projektion nach `ScreenPoints.Wpf` herausgezogen |
  | `ShipViewLockTests` | grün | – |
  | `ShipViewHitTests` (neu, 8, STA) | – | erster Test, der das Control gegen sein eigenes Bild klickt; Klickpunkte aus WPFs Abbildung der Kamera des Controls, nicht aus `CameraMath` |

- Gegengeprüft (je rot): Kamera im Quader ohne `Ahead`-Zweig; nächste Kiste über alle Racks; `Entry` statt `Ahead`
  (erst mit dem Test „Fracht hinter der Kamera“); Hit-Kamera 20 % weiter weg; Doppelklick setzt immer zurück.
  Tests App 1.565 → 1.582, Core 1.492, Import 141; 0 Warnungen.

**Akzeptanz:**
- [x] bestehende Gesten-Tests grün (oder gleichwertig ersetzt, je Test benannt; Tabelle oben)
- [x] Hit-Test-Zeit vorher/nachher (M0 0,007 ms alter Pfad → M2 0,002 ms)
- [x] Klickprobe am Branch-Build (Martin, 30.10.2026, Skalierung ≈ 110 %)
- [x] `dotnet build` ohne Warnungen, `dotnet test` grün

### [x] T8.4 Drag & Drop (30.10.2026, abgenommen 30.10.)
**Warum:** Das L der S8.0-Liste; Drag-Semantik bleibt, nur Ziel und Vorschau werden 3D.
- Einzelkiste, Mehrfachauswahl atomar, Legenden-Drag, Drehgeste Mausrad (K3.2), Vorschau = Loslassen (Geisterquader an der
  Zielzelle, dieselbe Core-Prüfung), Verdrängen 3b; D45-Pfad (GetCursorPos pro Frame unter CaptureMouse) bleibt

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** R1 Geister, Ziel-Rack-Rand und Ablehnung obenauf in der
2D-Überlagerung (wie T8.2 R1), Entscheidung am Bild. R2 Ausblenden von Füllung und Kanten mit 0,35, Kanten-Neubau nur bei
neuer Menge. R3 Messziel Hull C halb gefüllt (volle Hull C hat kein Ziel). R4 Gate der Vorschau: p95 der Schlüsselwechsel
≤ 16 ms (D65), beides gemessen. R5 Nähte `internal` (`MoveTo`, `Wheel`, `StartLegendDrag` an einem Punkt, `ShownPreview`,
`PendingGesture`). E1 Handling-Zeile 23 (Strahl gegen die Stapelachse) nur als Test, Zeile 22 (SC im Vordergrund) → T8.7.
Dazu aus der Planung: die Tür-Regel liegt in Core, nicht im `DropTarget` (Ziel bleibt Ziel, Core legt um und sagt es in der
Notice); die Vorschau rechnet nicht in jedem Frame, sondern bei jedem Schlüsselwechsel (E2). Beim Bauen: Hull C nimmt nur
32er-Kisten (Mindestgröße der Racks, D70), das Messziel also 72 × 32 SCU statt 8er; M3 bewegt den echten Cursor
(`SetCursorPos`), weil der Poll (D45) einen simulierten Pfad überschriebe; der Sweep der Vorschau liegt auf dem Bildbereich der
Racks (mit dem Holo-Fit treffen nur 23 von 600 Punkten); Handling-Pläne: Hull A = Fixture `split_and_shared.json` statt
eigener Datei, Tür-Fall auf der Moth (Clipper hat ein Rack, Cutlass nimmt nur Kisten bis 2 SCU).

**Befund:**
- Commits 97e557d/76fc373 (Messziel `hull_c_drag`, `MoveTo`, `MoveCursor`, M3a), b88b73b (`DropTarget` auf dem Strahl),
  a3ade8a (`DragPreview`, Überlagerung, `Fade`), 5ba6dfc (Control: `DropAt`, `ShowDrag`, Nähte, Drag-Ende), e0fa86f (Sweep
  über den Racks), 02d9df1 (Moth-Plan), dieser (Doku).
- **Fehler gefunden, auch in `main`:** Loslassen beendete einen Drag nur über `LostMouseCapture`. Ohne Capture (fehlgeschlagenes
  `CaptureMouse`, Ansicht ohne Fenster) blieb er stehen, jeder weitere Druck galt als zweiter Drag und wurde verschluckt, das
  nächste Loslassen beendete den ersten. Ein T8.3-Test („zweiter Rechtsklick nimmt sie wieder heraus“) war nur deshalb grün.
  `FinishDrag` beendet den Drag jetzt selbst; mit Capture ändert sich nichts. Wächter
  `ShipViewHitTests.Each_click_ends_its_own_drag_even_without_the_mouse_captured` (vorher rot).
- Handling Zeile 9: in `split_and_shared` lehnt Core nirgends ab, sondern macht Platz (`Displace`); ein roter Geist ist dort
  in keinem Build zu sehen. Die Soll-Zeile war zu optimistisch. Über dem vollen Rack 1 gibt es kein Ziel (Zeile 4).
- Diagnose über einen nicht committeten Log-Build (`~/CargoPlan-t84-debug`, gelöscht): Vorschau und Commands liefen richtig;
  die vermeintlichen Abweichungen 17/18 waren die linke statt der rechten Maustaste.
- **M3a/M3b** (`--measure-frames`, Release, ohne SC, 30 s je Lauf, Median aus drei Läufen von Martin):

  | Ziel (spät / p95 / fps) | M3a vor T8.4 | M3b nach T8.4 |
  |---|---|---|
  | `hull_c_drag` | 13,7 % / 33,90 ms / 52,7 | 12,6 % / 33,90 ms / 52,4 |
  | `empty` | 15,5 % / 33,08 ms / – | 13,7 % / 33,07 ms / – |
  | Vorschau (Schlüsselwechsel) n / p95 / max | – (kein Ziel vor T8.4) | 343 / 1,81 ms / 12,1 ms |

  Gate Frames erfüllt (33,90 ≤ 33,07 + 2), kein Abbruch (M3b − M3a = 0 ms), Gate Vorschau erfüllt (1,81 ≤ 16 ms). Nach der
  Gate-Regel ab T8.5 (Phasenkopf, Information): spät 12,6 % ≤ 13,7 % + 2 pp. `empty`-fps nicht notiert (Martins Läufe).
- Tests in T8.4 (rot wurde kein bestehender Test außer `DropTargetTests` beim Umzug):

  | Test | Stand | Grund |
  |---|---|---|
  | `DragGestureTests`, `CrateDropTests`, `CrateTurnTests`, Core (`ResolveMove`, B3, B6, Tür) | grün, unverändert | Orakel |
  | `DropTargetTests` (10 → 14) | migriert, gleiche Erwartungen | Strahl statt `IsoProjection`; + Nahsicht, hinter der Kamera, streifender Blick, Rack mit `-z` von unten |
  | `SceneHitTestTests` | Rechts-Zieh-Test per Strahl | – |
  | `CrateMaterialsTests`, `ShipScene3DTests` | + 1 / + 1 | ausgeblendeter Anteil, `Fade` |
  | `FrameStatisticsTests` | + 2 | Logzeile der Vorschau |
  | `ShipViewHitTests` | + 1 | Drag-Ende ohne Capture |
  | **neu** `DragPreviewTests` (7), `ShipViewDragTests` (9, STA) | – | erster Test, der im Control zieht |

- Gegengeprüft (je rot): `Entry` statt `Ahead` im `DropTarget`; rote Geister bei jedem Ergebnis; ohne Drop-Ziel (vier
  Live-Tests rot); Loslassen ohne Drehung. Bild selbst geprüft (Renderbild mitten im Drag). Tests App 1.582 → 1.607, Core
  1.492, Import 141; 0 Warnungen.
- **Handling gegen `main`** (Martin, `main`- und Branch-Build, Hull A `split_and_shared.json`, Moth `t84-moth.plan.json`):
  Zeilen 1–21 gleich (Ist = Soll), Zeile 22 → T8.7, Zeile 23 nur Test.

**Akzeptanz:**
- [x] B3-, B6-, K3.2-Tests grün
- [x] Handling gegen `main` (Martin, 30.10.2026: „9,10,11 → funktioniert, Abnahme“)
- [ ] Gate am Aufzug (Martin) → T8.7 (Zeile 22 der Handling-Liste)
- [x] `dotnet build` ohne Warnungen, `dotnet test` grün

### [x] T8.5 Look und Settings (30.10.2026, abgenommen 30.10.)
**Warum:** Look-Presets und Settings müssen auf der neuen Ansicht gelten, ohne Formatbruch (D83).
- Halo-Texte im Overlay, Look-Presets und -Export unverändert gültig, Umriss als Fläche in der Holo-Bodenebene (inkl.
  `holoOffset`; bis dahin im Umriss-Modus kein Bug-Marker, T8.1 R7); Addendum D62
- Zoom-Grenzen als Distanz gibt es seit T8.1 (R3): hier nur Text und Tooltip des Reglers im Settings-Fenster (Zellgröße in
  der Zielebene, D76-/D83-Addendum)

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** R1 Core-Ausnahme: `ShipOutline.HeightM` liest das schon
vorhandene `heightM` aus `ship-outlines.json` (nur ein Leser, Format unverändert), weil im Umriss-Modus kein Holo geladen
ist und seine Unterkante sonst unbekannt wäre. R2 Boden nie über dem Rackboden (Klemme). R3 Bug-Marker im Umriss-Modus =
der Umriss selbst, kein Keil, kein Label (wie `main`, D62). R4 Rahmen um `Ship.Bounds` behalten, Entscheidung am Bild.
R5 Tooltip-Texte „Max zoom per cell“ und „Backdrop“ (Wortlaut im Commit). R6 Zoom-Bereich 40–240 bleibt. R7 Fläche unlit,
Entscheidung am Bild. R8 Altpfad-Abhängigkeit des Bow-Labels → T8.6. E1 schiffsfeste Untergrenze `near = max(…, halbe
Diagonale + 1)`, falls die Kamera bei Max-Zoom im Schiff steht (siehe Befund: Rückfrage). E2 Export → Import bitgleich,
bei Flackern Toleranz mit Begründung. Dazu aus der Planung: Gate ab T8.5 = Anteil später Frames (Phasenkopf). Beim
Bauen: kein `StartOptionsTests`-Fall, die Messziele stehen nur im `FrameMeasureWindow`.

**Befund:**
- Commits 1900013 (Nachtrag M3, Gate-Regel), 1036ee0 (`HeightM`), b21bc92 (Boden), 6f4f03e (`OutlineSurface`), af71038
  (Fix Wicklung), 177ee66 (Umriss in der Szene, Messziel), fb3c8b6 (Tooltips), 629afc3 (`LookSceneTests`), fd156b4
  (Material-Cache threadsicher), dieser (Doku).
- Nebenbefund: der statische Kanten-Material-Cache in `ShipScene3D` (seit T8.2) war ein normales `Dictionary`; parallele
  STA-Testthreads zerstörten ihn (21 rote Tests im ersten vollen Lauf mit `LookSceneTests`). Jetzt `ConcurrentDictionary`
  wie `CrateMaterials`/`StopBrushes`; drei volle Läufe grün. Die App hat nur den UI-Thread, war also nicht betroffen.
- **Fehler gefunden, seit T8.2:** `HoloLines.Strips` wickelte jeden Streifen mit der Vorderseite von der Kamera weg. Die
  Holo-Kanten haben ein `BackMaterial` und waren sichtbar, die Szenen-Strips (Kisten-, Geladen-, Rack-, Raster-,
  Rahmenkanten) haben keins und wurden weggeschnitten. Gefunden beim Rendern der Umrisslinie: im `RenderTargetBitmap` kein
  einziger Strip, mit `BackMaterial` alle. Wächter `HoloViewTests.A_strip_shows_its_front_to_the_camera` (vorher rot);
  Bild mit neuer Wicklung = Bild mit `BackMaterial` (Differenz 0 %). Die T8.2-Bilder entstanden auf diesem Stand.
- Boden aus `heightM` gegen die gecachten Holos (24 Schiffe): meist ≤ 0,06 m; Avenger Titan +0,72, Freelancer −0,4 bis
  −0,55, Fortune −1,02, Idris-P +0,79, Hull C −1,75, Zeus Mk II −1,91 m; Reliant Kore Holo 28 m gegen 4,5 m (Flügel
  hochgeklappt, betrifft nur das Holo). Geklemmt (R2): Hull A (0,27 → 0), Hull B (1,19 → 0).
- **Zoom-Bereich** (`d_min = W / (2 · px · tan 20°)`, `near = min(d_min, Referenz)`, Standardausrichtung, Fit-Ziel; „im
  Schiff“ = Kamera in `Ship.Bounds`):

  | Schiff (halbe Diagonale) | W | 40 px | 160 px | 240 px |
  |---|---|---|---|---|
  | Hull A (4,74) | 400 | 13,74 | 3,43 | 2,29 im Schiff |
  | Hull A | 800 | 16,81 (Referenz) | 6,87 | 4,58 |
  | Hull A | 1600 | 21,74 (Referenz) | 13,74 | 9,16 |
  | Hull C (24,17) | 400 | 13,74 | 3,43 im Schiff | 2,29 im Schiff |
  | Hull C | 800 | 27,47 | 6,87 im Schiff | 4,58 im Schiff |
  | Hull C | 1600 | 54,95 | 13,74 | 9,16 im Schiff |
  | UTV (0,87) | 400 | 3,96 (Referenz) | 3,43 | 2,29 |
  | UTV | 800 / 1600 | Referenz 3,89 / 5,00 | Referenz | Referenz |

  Anders als im Plan erwartet steht die Hull A bei 160 im Standard-Overlay **nicht** im Schiff (erst bei 240), die Hull C
  dagegen schon. E1 wörtlich hieße für die Hull C `near ≥ 25,2` Zellen, also höchstens ≈ 22 px/Zelle bei 400 DIP statt
  160: das nähme D76/K1.1 („eine Hull C kommt so nah wie eine Hull A“) zurück. Nicht gebaut, **Rückfrage**.
- Bilder für R4/R7 (Hull A, Outline / Holo shaded / None, je mit und ohne Rahmen; Umriss von unten; Hull C an der
  Fern-Grenze) liegen im Scratchpad der Session.
- **Stichprobe** (`--measure-frames`, Release 629afc3, ohne SC, 30 s je Lauf, Median aus drei Läufen; spät / p95 / fps):

  | Ziel | Läufe spät | Median |
  |---|---|---|
  | `hull_a_outline` (4 Kisten, Umriss) | 6,1 / 2,5 / 6,6 % | 6,1 % / 33,07 ms / 56,5 |
  | `empty` | 2,5 / 11,9 / 10,4 % | 10,4 % / 33,07 ms / 54,2 |

  Gate (spät ≤ `empty` + 2 pp) erfüllt: 6,1 ≤ 12,4. Hit p95 0,001 ms. Die Streuung des leeren Fensters (2,5–11,9 %) ist
  größer als der Abstand der Ziele.
- Tests in T8.5 (rot wurde kein bestehender Test):

  | Test | Stand | Grund |
  |---|---|---|
  | Core `ShipOutlineLoaderTests` | + 5 | Höhe gelesen, fehlt, ungültig (0, negativ), alle ausgelieferten haben eine |
  | `OutlinePlacementTests` | xy unverändert, + 5 | Boden aus Höhe, mit `holoOffset.z`, Klemme, ohne Höhe, `Place(outline, ship)` |
  | **neu** `OutlineSurfaceTests` (5) | – | konvex, konkav, Umlaufsinn, kollinear, alle 118 Umrisse flächentreu |
  | `ShipScene3DTests` | + 8 | Fläche, Keil nur bei Wedge, Linie/Halo je Look, Versatz Linie > Halo > Fläche, ohne Umriss keine Linien |
  | `OverlayViewModelTests` | + 1 | Umriss auf dem Holo-Boden (Aurora Mk II, `holoOffset`) |
  | `HoloViewTests` | + 1 | Strip zeigt der Kamera die Vorderseite |
  | `SettingsWindowXamlTests` | + 2 | Tooltip-Texte |
  | **neu** `LookSceneTests` (18, STA) | – | 14 Keys je ein Bild, Umriss nimmt Nummern-Farbe und Halo, Presets je eigenes Bild, Export → Import bitgleich |
  | `SceneProjectorTests`, `CameraFitTests`, `LookPresetsTests`, `LookFileTests`, `SettingsViewModelTests` | grün, unverändert | Orakel; `Place(outline, bounds)` behält den Rackboden als Vorgabe |

- Gegengeprüft (je rot): ungültige Höhe nicht verworfen; alter Aufruf ohne Boden im ViewModel; Ohr ohne Punktprüfung;
  Linie so weit gehoben wie der Halo; Kantenfarbe und Umriss-Halo abgeklemmt (`LookSceneTests`).
- **Präzisierung bei der Abnahme, ratifiziert durch Freigabe:** Hologram opacity von 1 % bis 100 % in 1er-Schritten
  (vorher 10 % in 5er-Schritten; `MinHoloOpacity` 0,01, `TickFrequency` 1; D63-Addendum, settings-format.md). Ältere
  Builds klemmen Werte unter 0,1 mit Hinweis auf 0,1. Handling-Zeile 2 weicht damit gewollt von `main` ab. Tests:
  `SettingsStoreTests` Klemme auf 0,005 → 0,01 umgestellt, + 2 (0,01 und 0,05 bleiben), `SettingsViewModelTests` + 1,
  `SettingsWindowXamlTests` + 1 (alle vorher rot).
- Rahmen um `Ship.Bounds` gestrichen nach Martins Bildentscheidung (D84): mit ihm `MissionPalette.Frame`,
  `BoxEdges.Dashed` und dessen zwei Tests; Wächter `ShipScene3DTests.An_empty_ship_shows_its_rack_edges_and_no_frame`
  (vorher rot). Tests App 1.607 → 1.650 (mit Opacity), Core 1.492 → 1.497, Import 141; 0 Warnungen.

**Akzeptanz:**
- [x] drei Presets im Spiel (Martin, 30.10.2026: „ja passt“)
- [x] Settings-Reset „Ship view“ und Handling gegen `main` (Settings-Zeilen 1–14, Martin: „alle Settings ausprobiert“);
  daraus der Wunsch Hologram opacity 1–100 % in 1er-Schritten, umgesetzt (Befund)
- [x] Look-Export → Import → identisches Bild (`LookSceneTests`, bitgleich in drei Läufen; Martin mit den Settings)
- [x] Rahmen (R4) und Fläche unlit (R7) am Bild (Martin, 30.10.2026: „ohne Rahmen, Rest passt“): Rahmen gestrichen (D84),
  Fläche bleibt unlit
- [x] Zoom-Untergrenze (E1): bleibt wie sie ist (Martin, 30.10.2026, nach der Tabelle oben); keine schiffsfeste
  Untergrenze, kein D76-Addendum
- [x] Stichprobe `hull_a_outline` nach der Gate-Regel ab T8.5 (6,1 % ≤ 10,4 % + 2 pp)
- [x] `dotnet build` ohne Warnungen, `dotnet test` grün

### [x] T8.6 2D-Painter entfernen (30.10.2026, abgenommen 31.10.)
**Warum:** Es gibt nur noch eine Projektion; toter Code und doppelte Tests gehen.
- `SceneProjector`, `DrawOrder`, `IsoProjection`-Zeichenpfad entfernen, verbliebene Tests migrieren, die 0,5-px-Abgleichtests
  (Teil 2–4 der Holo-Pump-Reihe) streichen
- Aus T8.1 unbenutzt: `HoloBackdrop`, `HoloCamera`, `ShipViewState`, `ViewNavigation`, `Framing`, `ViewBasis`, `ZoomLimits`,
  `ViewTransform`; mit ihnen `OrbitScaleTests`, der Rahmen-Teil von `ShipCameraTests`, `ViewTransformTests`, `HoloCamera` in
  `HoloViewTests` (Gegenstücke in `CameraNavigationTests`). `IsoProjection` bleibt als Orientierung (Yaw/Pitch, `DefaultFor`,
  `IsLong`) für `CameraMath`
- Aus T8.5 (R8): **umziehen, nicht löschen** – `SceneProjector.BowWedge`/`FloorOf` und `ViewTransform` tragen noch das
  Bow-Label (`ShipOverlay.DrawBowLabel` → `FloorLabel.Lay` mit Identitäts-`ViewTransform`), den 3D-Keil
  (`ShipScene3D.Wedge`) und die Keilnase im Fit (`CameraFit.Bow`); `SceneProjector.Corner` ist der Punkttyp von
  `OutlinePlacement` und `BowMarker`

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** R1 `IsoProjection` heißt nach dem Rückbau
`CameraOrientation` (eigener Commit, nur Umbenennung). R2 `SceneProjector.Corner` → `Point3D`. R3 die „0,5-px-Abgleichtests
Teil 2–4“ gab es im Repo nie (Wegwerf-Harnesses); gemeint war der `HoloCamera`-Abgleich in `HoloViewTests`, Nachfolger
`CameraConformityTests`. R4 Tests ohne 3D-Gegenstück gehen als „Verhalten entfällt (D83) + benannter Nachbartest“. R5 kein
Unit-Zeitwächter; Deckung über M4, dazu einmal der größte Umriss (Hull D) auf dem `hull_a_outline`-Pfad. R6
`RackNumberSizeTests.Default_is_the_size_at_zoom_1…` gestrichen, Herkunft der 27 ins D32-Addendum. R7 CC misst M4a/M4b.
R8 `HoloLines.HalfWidthCells` in px/Zelle der Zielebene, `ShipScene3D` nutzt es. Dazu aus der Planung: Nachfolger vor dem
Löschen und grün gegen den alten Code; Bildvergleich vorher/nachher am echten Control. Beim Bauen: `ShipCameraTests`' gerahmte
Kamera trägt `distance`/`target` statt der Altfelder; `TestPlans.HullABeforeK52` verlor seinen letzten Aufrufer und ging;
der Test „langes Schiff von Backbord“ (`CameraOrientationTests`) liest Flächen und Bug über `CameraMath`;
`ShipViewControl` übergibt der Szene `CameraNavigation.ZoomOn` statt derselben Formel inline.

**Befund:**
- Commits 7f7bd15 (`Point3D`), 0e905ef (`BowWedge`, `FloorLabel`), a65d06b (Nachfolger), 5cd813c (Holo-Kulisse), 56b73d1
  (Navigation und Painter), fadf0ac (`IsoProjection` nur Ausrichtung), fa6a28f (toter Code), e5419ca (Umbenennung), dieser
  (Doku). Entfernt ≈ 960 Zeilen src.
- **Lücken gefunden, vor dem Löschen geschlossen:** das „Bow“-Label war im 3D-Pfad ungetestet (alle fünf Tests liefen über
  `ViewTransform`); mehrere Fit-Regeln (Holo ganz auf langem Schiff, Umriss wie Keil, ohne Marker, kompakt = alles) nur
  ortho; in `CameraNavigation` fehlten `Clamped`, `ZoomOn`, Pan addiert, Zoom am Anschlag, Fern-Grenze „ganze Szene“,
  Defaults, Ausrichtung bleibt, anderes Fenster; `BoxFaces.Visible` und die Fuge 0,05 hatten keinen direkten Test.
  Gegengeprüft (je rot): Aufrechte des Labels quer, Boden hinter dem Bug, Fit ohne Holo-Zweig, Fern-Grenze ohne ganze
  Szene, `Clamped` klemmt nie, freie Zellen ohne Fuge.
- **Gleiches Bild:** acht Zustände über die Overlay-Verdrahtung (`ViewPictures`) gerendert, auf cf8716f (Worktree) und
  e5419ca: Keil mit Label im Fit, gedreht und nah; Umriss; Holo shaded und hidden-line (nah); C2 mit Keil und ohne
  Marker. Alle **byte-gleich**; Wegwerf-Test nicht committet, Bilder im Scratchpad der Session.
- Tests in T8.6 (App; Core 1.497 und Import 141 unverändert):

  | Gestrichen | Fälle | Nachfolger |
  |---|---|---|
  | `OrbitScaleTests` | 7 | `CameraNavigationTests`, die vier Wächter (seit T8.1) |
  | `ViewTransformTests` | 33 | `CameraNavigationTests` (Fit, Zoom zum Cursor, Grenzen, Pan, Orbit, gespeicherte Ansicht) + 11 neu dort; `CameraFitTests` (Raum oben) |
  | `ShipCameraTests` Navigationsteil | 7 | `CameraNavigationTests` (Kamera aus Settings, ohne Platzierung, außerhalb der Grenzen) + neu „Sync lädt Rack weiter achtern“ |
  | `HoloViewTests` 3D-Kamera folgt der Ansicht | 4 | `CameraConformityTests` (R3) |
  | `IsoProjectionTests` Projektionsteil | 25 | `CameraMathTests` (Mitte, Bug/Backbord/oben, Unproject) + neu „oben bleibt oben von unten“; ortho-Eigenschaften und `Depth`: entfällt (R4) |
  | `DrawOrderTests` | 18 | entfällt (Tiefenpuffer); halbtransparente Kisten: `ShipScene3DTests.Crates_are_drawn_from_the_back_to_the_front` |
  | `SceneProjectorTests` | 57 | neu `BoxFacesTests` (4), `CameraFitTests` (+8), `ShipScene3DTests` Fugen (+2), `ShipViewBuilderTests` Default-Marker (+1); `RackLabelsTests`, `ShipScene3DTests`, `DragPreviewTests` vorhanden; Reihenfolge, Anker, Zeitwächter: entfällt (R4, R5) |
  | `SceneProjectorTests` Keil und Label | 11 | umgezogen: **neu** `BowWedgeTests` (6), `FloorLabelTests` (9) auf `CameraMath` |
  | `SceneHitTestTests.Polygon_contains_inner_points_only` | 1 | entfällt mit `Contains`; Strahl: `RayBoxTests` |
  | `RackNumberSizeTests.Default_is_the_size_at_zoom_1…` | 1 | gestrichen (R6); 27 festgenagelt in `ThemeResourcesTests`, `SettingsStoreTests` |
  | in place migriert | – | `RackNumberSizeTests` Zoombereich (4) auf dem Perspektiv-Fit; `HoloViewTests` Streifenbreite (3) in der Zielebene |

  Zählung App 1.650 → 1.654 (Keil/Label) → 1.682 (Nachfolger) → 1.678 → 1.554 → 1.529; 0 Warnungen nach jedem Commit.
- **Toter Code** (IDE0051/IDE0052 über `dotnet format style --severity hidden`, vorher mit einer Probe-Methode geprüft;
  dazu jeder `public`/`internal` Name der App als Wort über src, XAML und Tests): gelöscht `MissionPalette.HoloAmbient`,
  `HoloLight`; `CameraNavigation.ZoomOn` hatte seit T8.1 nur Tests, das Control nutzt es jetzt. Behalten mit Grund: die
  vier Besitz-Felder in `App.xaml.cs` (dort kommentiert), die Felder von `CURSORINFO` (Struct-Layout),
  `LegendRowPanel.SetRank` (Attached Property), `ShipViewControl.ShownPreview` (Testnaht, T8.4 R5). **Vorbestand, älter als
  T8.6, nur Testaufrufer:** `WindowBounds.FromRect` (T3.4), `LegendEntry.ReserveLabel` → Backlog-Zeile mit Auslöser
  „Repo-Hygiene vor v0.1.0“ (Martin bei der Abnahme).
- **M4** (`--measure-frames`, Release, ohne SC, 30 s je Lauf, drei Runden über alle Ziele abwechselnd, Median der drei
  Läufe; spät / p95 / fps):

  | Ziel | M4a vor T8.6 (cf8716f) | M4b nach T8.6 (e5419ca) |
  |---|---|---|
  | `empty` | 12,9 % / 33,07 ms / 52,9 | 13,1 % / 33,07 ms / 53,0 |
  | `hull_c` | 1,1 % / 16,68 ms / 59,3 | 2,0 % / 16,68 ms / 58,8 |
  | `hull_c_states` | 4,6 % / 16,95 ms / 57,3 | 5,0 % / 32,80 ms / 57,1 |
  | `hull_c_drag` | 13,1 % / 33,90 ms / 52,1 | 12,8 % / 33,90 ms / 52,4 |
  | `hull_a_outline` | 5,5 % / 32,52 ms / 56,8 | 8,1 % / 33,07 ms / 55,4 |
  | Vorschau `hull_c_drag` p95 / max | 2,24 / 12,3 ms | 1,79 / 11,6 ms |
  | Hit-Test `hull_c` p95 | 0,001 ms | 0,001 ms |

  Gate (spät ≤ `empty` + 2 pp = 15,1 %) überall erfüllt. **M4b ist die Vergleichsbasis für T8.7 mit SC.**
  `hull_a_outline` ist mit +2,6 pp (5,5 → 8,1 %) die einzige Abweichung über der Streuung der übrigen Ziele (−0,3 bis
  +0,9 pp): alle drei M4b-Läufe (7,5 / 8,1 / 10,5 %) liegen über den T8.5-Läufen (629afc3: 6,1 / 2,5 / 6,6 %), aber in der
  M4a-Spanne (4,4–12,0 %); am Umriss-Pfad änderte T8.6 nur den Punkttyp (Bilder byte-gleich), in T8.7 mit SC beobachten.
  Einmalig (R5): Hull-D-Umriss (362 Punkte) auf der Hull A, Wegwerf-Build (eine Zeile in `FrameMeasureWindow`, nicht
  committet; der Log-Hash e5419ca zeigt die lokale Änderung nicht): 8,4 % / 33,07 ms / 55,3 gegen `empty` 10,3 %.
- Doku: Addenda D15, D21, D32, D63, D83. `CLAUDE.md` (Zeile Schiffsansicht, Dateiliste) und `docs/settings-format.md`
  (Kameras, Altfelder) gegengelesen, stimmen. Backlog-Zeile `ShipCamera.PxPerCell`/`PanX`/`PanY` steht.

**Akzeptanz:**
- [x] `dotnet build` ohne Warnungen, alle Tests grün (App 1.529, Core 1.497, Import 141; Slow grün)
- [x] Martins Abnahme (31.10.2026)

### [x] T8.7 Gates im Spiel am Aufzug (31.10.2026, abgenommen 31.10.)
**Warum:** S8.0 hat ohne SC gemessen; die Abnahme der Phase passiert im Spiel.
- UTV, Hull A, Hull C, Aurora Mk II mit Modul (`holoOffset`), ein Schiff mit Unterseiten-Racks; Frame-Zeit mit laufendem SC
- Aus T8.4 (Handling-Zeile 22): Kiste und Legendenzeile ziehen mit SC im Vordergrund, der Drag folgt dem Cursor per Poll
  (D45), Loslassen ohne Up-Nachricht wirkt; Gate am Aufzug
- Aus T8.4 (M3b): Maximum der Vorschau (Schlüsselwechsel) mit laufendem SC messen, Luft zum 16-ms-Budget (D65); ohne SC
  max 12,1 ms

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** R1 Messung mit dem Messfenster über dem Spiel
(`--measure-frames <Ziel> --over-game`), damit M5 mit M4b vergleichbar ist; ein Recorder im echten Overlay nur, falls M5
auffällt. R2 fünftes Schiff Hull B (8 von 16 Racks unter anderen, geklemmter Holo-Boden aus T8.5 R2; kein Schiff in
`ships/` hat Racks mit Stapelachse −z). R3 Plandateien committet in `docs/shots/`. R4 Martin pusht, CC merged lokal. R5 Mess-
und Vergleichs-Builds nach dem Merge löschen. Dazu aus der Planung: Hull B und Hull C nur als Overlay-Prüfung, Zeile 22
und echte Fracht nur mit spawnbaren Schiffen (Hull A, Aurora); das Log zählt die Schlüsselwechsel auf dem Cursorpfad
(„path keys“), weil die 343 des Sweeps ohne echten Cursor entstehen. **Präzisierung bei der Abnahme, ratifiziert durch
Freigabe:** das Frame-Gate „spät ≤ `empty` + 2 pp“ war für die Basislinie ohne SC (13 % spät) formuliert und ist mit SC
(0,1 %) nicht anwendbar; Lesart für T8.7: gleiches Ziel M4b → M5. Gate der Vorschau ist p95 ≤ 16 ms (T8.4 R4), das
Maximum Befund (D65-Addendum, Backlog-Zeile).

**Befund:**
- Commits c654338 (`--over-game`, „path keys“), be8e20c (Plandateien `t87-utv`, `t87-aurora`, `t87-hull-b`, `t87-hull-c`),
  dieser (Doku). Gate-Build `~/CargoPlan-t87` mit `backup.ps1` und `m5.ps1`, nach dem Merge gelöscht.
- Probelauf `--over-game` ohne SC (CC): Fenster nimmt keinen Fokus, Exit 0; `hull_c_drag` dreimal path keys 31 / 29 / 31,
  Vorschau max 10,6–11,6 ms, Hit p95 ≤ 0,001 ms; `hull_c` spät 1,8 %. Referenz der path keys für M5.
- **Checkliste** (Martin, 31.10.2026, mit laufendem SC; A = am Aufzug mit gespawntem Schiff, O = Overlay-Prüfung ohne
  Schiff am Aufzug):

  | # | Prüfpunkt | UTV (O) | Hull A (A) | Hull C (O) | Aurora (O, Zeile 22 A) | Hull B (O) |
  |---|---|---|---|---|---|---|
  | 1 | Drehen ohne Pumpen (das Gate) | ✓ | ✓ | ✓ | ✓ | ✓ |
  | 2 | Blick von unten | ✓ | ✓ | ✓ | ✓ | ✓ (Unterseite) |
  | 3 | Zoom bis zu beiden Anschlägen, am Anschlag drehen | ✓ | ✓ | ✓ | ✓ | ✓ |
  | 4 | Reset: Doppelklick daneben, Symbol, Hotkey | ✓ | ✓ | ✓ | ✓ | ✓ |
  | 5 | Kamera nach Neustart, Schiffswechsel hin und zurück | ✓ | ✓ | ✓ | ✓ | ✓ |
  | 6 | Kistenklick hakt ab, Tooltip, Rechtsklick wählt aus | ✓ | ✓ | ✓ | ✓ | ✓ |
  | 7 | Drag einer Kiste mit Vorschau, Mausrad dreht | ✓ | ✓ | ✓ | ✓ | ✓ |
  | 8 | Legenden-Drag | ✓ | ✓ | ✓ | ✓ | ✓ |
  | 9 | Lock/Unlock | ✓ | ✓ | – | – | – |
  | 10 | Holo shaded und hidden-line, Deckkraft 1 % und 100 % | ✓ | ✓ | ✓ | ✓ (Versatz) | ✓ (Klemme) |
  | 11 | Umriss-Modus (Fläche, Linie, Halo) | ✓ | ✓ | ✓ | ✓ | ✓ |
  | 22 | Kiste und Legendenzeile ziehen, SC vorn, Loslassen wirkt (D45) | – | ✓ | – | ✓ | – |

  Zeile 7 beim UTV prüfte Martin, obwohl der Plan sie als entfallend führte (ein Rack, eine Zelle).
- **M5** (`m5.ps1`, Martin, 31.10.2026 09:06–09:17, SC vorn, Cursor frei; `--measure-frames <Ziel> --over-game`, Release
  be8e20c, 30 s je Lauf, drei Runden, Median der drei Läufe; spät / p95 / fps):

  | Ziel | M4b ohne SC (e5419ca) | M5 mit SC (be8e20c) | M5 Läufe spät |
  |---|---|---|---|
  | `empty` | 13,1 % / 33,07 ms / 53,0 | 0,1 % / 16,67 ms / 59,9 | 1,0* / 0,1 / 0,1 |
  | `hull_c` | 2,0 % / 16,68 ms / 58,8 | 0,2 % / 16,67 ms / 59,9 | 0,2 / 0,2 / 0,3 |
  | `hull_c_states` | 5,0 % / 32,80 ms / 57,1 | 2,9 % / 16,67 ms / 58,3 | 0,6 / 2,9 / 9,3 |
  | `hull_c_drag` | 12,8 % / 33,90 ms / 52,4 | 11,8 % / 33,90 ms / 52,7 | 13,3 / 11,1 / 11,8 |
  | `hull_a_outline` | 8,1 % / 33,07 ms / 55,4 | 0,2 % / 16,67 ms / 59,9 | 0,2 / 0,2 / 0,1 |
  | Vorschau `hull_c_drag` p95 / max | 1,79 / 11,6 ms | 5,23 / 20,3 ms | max 14,6 / 24,6 / 20,3 |
  | Hit-Test p95 | ≤ 0,002 ms | ≤ 0,002 ms | – |

  \* Der erste `empty`-Lauf fiel in einen Build- und Testlauf von CC (09:06:50–09:07:20); der Median ist davon unberührt.
- **Frame-Gate (Martin bei der Abnahme):** die Basislinie mit SC ist 0,1 % spät statt 13 % ohne SC; die 13 % waren der
  Desktop-Compositor, nicht die App. Das Gate „≤ `empty` + 2 pp“ war für die 13-%-Basislinie formuliert und ist mit SC
  nicht anwendbar. Lesart für T8.7: gleiches Ziel M4b → M5 (Tabelle): **kein Ziel wird mit SC schlechter**. Damit ist die
  S8.0-Lücke geschlossen (D2: Software-Rendering unter `AllowsTransparency`, mit SC nie gemessen).
- **Vorschau:** Gate p95 ≤ 16 ms (T8.4 R4) hält mit 5,2 ms. Das Maximum steigt mit SC auf 20–25 ms in Einzelfällen
  (ohne SC 10,6–12,3 ms); die 16-ms-Regel (D65) ist der Rückfall für genau diesen Fall und fing ihn ab, am Aufzug nicht
  spürbar (Martin, Zeilen 7 und 22) → D65-Addendum, Backlog-Zeile.
- **Gültigkeit der Drag-Läufe:** path keys 25 / 33 / 28 gegen 29–31 ohne SC, „drag held yes“ in allen drei: SC hat den
  Cursor nicht gefangen. (Die „previews n 343“ belegen das nicht: sie stammen aus dem Sweep nach der Frame-Phase über
  `MoveTo`, ohne echten Cursor, und wären auch mit gefangenem Cursor 343.)
- **E1 (aus T8.5) für die Hull A:** keine Untergrenze gebaut und keine nötig. Im Standard-Overlay (400 DIP breit) steht
  die Kamera erst bei 240 px/Zelle in `Ship.Bounds`, beim Default 160 nicht; Zeile 3 grün. Bei der Hull C kann die Kamera
  bei Max-Zoom im Schiff stehen und schneidet nahe Teile an der Near-Plane ab; so gelassen, eine Grenze „halbe Diagonale
  + 1“ verhinderte, ein einzelnes Rack nah zu sehen → Known issue in T9.1.
- Keine Fixes nötig, kein Bild, keine Rückfrage aus dem Gate.

**Akzeptanz:**
- [x] Martins Abnahme (31.10.2026: „Alle tests erfolgreich bestanden!“), dann `git merge --no-ff` nach `main`

### Nicht in Phase 8
Schatten, Texturen, Auto-Rotation, konfigurierbares FOV, Bodengitter als Feature (S8.0: unnötig fürs Pumpen), Lokalisierung,
Velopack/winget, Retaliator-Module, `holoOffset` für den GRIN UTV (Datenfall, Backlog).

### Risiken, vorab benannt
- WPF-3D unter `AllowsTransparency` rendert in Software (D2); mit laufendem SC nicht gemessen → T8.7. Gemessen in T8.7
  (M5): kein Ziel wird mit SC schlechter.
- Transparente Modelle werden nicht tiefensortiert: Holo als letztes Kind, Kisten deckend; halbtransparente Kisten
  (`crates.alpha`) über dem Holo können Artefakte zeigen → am echten Element prüfen, ggf. Alpha-Untergrenze. T8.2: Kisten
  von hinten nach vorn sortiert, Grenze im D83-Addendum.
- Text nur im 2D-Overlay sauber; Rack-Nummern werden bei jedem Kamerawechsel neu gesetzt und auf Verdeckung geprüft →
  Hysterese aus dem Backlog, falls sie flackern.
- Hit-Test und Drop-Regeln ziehen um (höchstes Regressionsrisiko) → `CameraMath` als einzige Projektion, Konformitätstest,
  T8.3 vor T8.4, Tests als Orakel.
- Pitch ±85° mit Up (0,0,1): am Pol wäre die Blickrichtung parallel zu Up; Grenze nicht lockern.

---

## Phase 9 – Release (nach Phase 8)

**Warum:** T7.5a–c (README, Wiki, Medien) zeigen die 2D-Ansicht; nach D83 stimmen Begriffe und Bilder nicht mehr. Der
Release-Teil von T7.5 (T7.5d) zieht hierher. T7.5a–c bleiben unter ihrer Nummer.

### [x] T9.1 Textabgleich auf der 3D-Ansicht (31.10.2026, abgenommen 31.10.) – Branch `release/v0.1.0`
- Textdurchsicht auf durch D83 **falsch** gewordene Stellen: README, Wiki-Seiten (v. a. Ship-view, Preferences, Overlay-basics),
  `docs/settings-format.md`, Settings-Tooltips (Zoom-Grenzen = Zellgröße in der Zielebene, Kamera/Orbit/Pan, Reset, Abschnitt
  „Ship view“)
- Medien bleiben (D85), Hinweis in `docs/shots/SHOTLIST.md`, Auslöser zum Nachdrehen nur in der Backlog-Zeile
- Known issues aus Phase 8 (T8.7) ins Wiki (Troubleshooting) und in den Entwurf `docs/release-notes/v0.1.0.md`

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
- Medien inklusive Hero-GIF bleiben vom Stand vor D83 (D85, D83-Addendum); Auslöser-Wortlaut einmal im Backlog, SHOTLIST
  und D85 verweisen darauf.
- Neuer Branch `release/v0.1.0` ab `main`; `docs/release-v0.1.0` war vollständig in `main` und `origin/main` (9364ca0),
  lokal gelöscht, remote löscht Martin. Kein Merge nach T9.1: `--no-ff` nach der Abnahme der Notes in T9.2, vor dem Tag.
- Tooltip „Hologram opacity“ „behind the racks“ → „around the racks“ als eigener Commit (Text, kein Verhalten, kein Test
  pinnt ihn).
- Wiki `Ship-view`: der Satz zu langen Schiffen war schon vor D83 falsch („dimmed until you zoom out“): leere Racks langer
  Schiffe (`IsLong`: mehr als doppelt so lang wie breit) sind immer gedimmt (`ShipScene3D.cs:317`), der Start zeigt die
  beladenen Racks und den Bug (`CameraFit.cs:67`).
- README bleibt: Ansicht, Settings und Known issues stehen seit T7.5a im Wiki; Grep belegt, dass dort nichts falsch ist.
- Known issue „Planzeit bis ~4 s“ entfällt (B4, D66: Freelancer-Lastfall 2,3 ms). Retaliator: keine Datei in `ships/`,
  also „fehlt“, nicht „ohne Gittermaße“.

**Befund:**
- Downgrade geprüft am Code: rc.1 (6bbc115), rc.2 (1bfd3ba, beide am Commit ihrer Notes; die Tags gibt es nicht mehr) und
  rc.3 (Tag 01770ab) lesen Schema 2 mit `UnmappedMemberHandling.Disallow` und kennen weder `shipView` noch `shipOutline`.
  0.1.0 schreibt beide immer: **jede** von 0.1.0 gespeicherte `settings.json` ist für rc-Builds unlesbar (Defaults und
  Meldung, `.bak` erst beim nächsten Speichern), nicht erst eine mit `distance`/`target` (D83-Addendum T8.1).
- Netz: einziger `HttpClient` in `HoloSource.cs`, Basis `HoloRelease.BaseUri` (Release `holo-v1` dieses Repos), kein
  Aufruf von Fleetyards; sonst nur der Nexus-Link per Klick im Browser.
- Schiffe: 97 Dateien in `ships/` und ein Modul, der Loader lädt 98 Einträge ohne „Skipped“; README und Wiki stimmen.
- „New since rc.3“ aus `git log v0.1.0-rc.3..main` (318 Commits, 15 Merges): rc.3 war der Stand von T6.5 mit der Hull A
  allein; seitdem T7.0–T7.5, B1–B6, K1–K7, Sync-Gone, Sync-Net, Holo-Pump, T8.0–T8.7.

**Akzeptanz:**
- [x] Wiki: Ship-view, Preferences, Ships-and-cargo-grids, Troubleshooting (Known issues) folgen D83/D84 (Wiki `4d1dba0`,
  lokal; Push am Tag des Tags `v0.1.0`)
- [x] `docs/settings-format.md` folgt D83/D84 (Enum-Wert `"none"` unverändert)
- [x] Tooltip „Hologram opacity“ folgt D83
- [x] SHOTLIST-Hinweis, D85 und D83-Addendum, Backlog-Zeile
- [x] Entwurf `docs/release-notes/v0.1.0.md` mit Highlights, Known issues (je mit Fundort), Credits
- [x] Grep-Prüfung (README, Wiki, `docs/*.md`, XAML ohne Treffer), Link-Prüfung (0 kaputt, Negativtest schlägt an),
  `dotnet build` 0 Warnungen, `dotnet test` grün (Core 1.497, Import 141, App 1.533)
- [x] Diff-Durchsicht durch Martin/Claude.ai (31.10.2026); `origin/docs/release-v0.1.0` von Martin gelöscht
- Kein Merge nach T9.1: `--no-ff` nach `main` nach der Abnahme der Notes in T9.2, vor dem Tag

### [x] T9.2 Release (= T7.5d) (31.10.2026, abgenommen 02.11.)
- Release-Notes `docs/release-notes/v0.1.0.md`, Tag `v0.1.0`, rc-Releases und -Tags löschen (D49)
- Notiz aus T9.1: `gh` liegt nicht im PATH (`C:\Program Files\GitHub CLI\gh.exe`); das Holo-Asset ist ein eigenes Release
  `holo-v1`, nicht am App-Release; Wiki-Push am Tag des Tags `v0.1.0`

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
- R1 Retaliator-Zeile bleibt ohne Grund (kein Beleg im Repo). R2 Schiffszahl bleibt 97: die Auswahl hat 98 Einträge,
  der 98. ist die Aurora Mk II mit TS-Modul (`ModuleDto.cs:30-34`, Variante, kein eigenes Schiff); Notes, README und Wiki
  sagen 97. Highlights bleiben wortgleich zur README; N1 Drop-Satz, N2 „if you choose“, N3 Link auf „Check the download“.
- Kein Versions-Bump: `release.yml` nimmt die Version aus dem Tag, `0.0.0-dev` in `Directory.Build.props` bleibt
  Platzhalter für Entwickler-Builds (D49).
- M6 (self-contained statt framework-dependent wie M5) **vor dem Tag** mit dem lokalen ZIP aus `publish.ps1 -Version 0.1.0`
  (gleiches Skript, gleiche Schalter wie CI); das veröffentlichte ZIP bekommt die Funktionsprüfung. Gate je Ziel: Median
  spät ≤ M5-Median + 2 pp, Vorschau p95 ≤ 16 ms.
- rc-Notes bleiben (D49-Addendum, keine neue D-Nummer). `release/v0.1.0` nach der Nachprüfung lokal und remote löschen.
- Wiki-Push entfällt: `origin/master` steht schon auf `ef6b9f4`.

**Befund (vor dem Tag):**
- Version überall `AssemblyInformationalVersion`: Settings-Fußzeile, CLI `--version`, Holo-User-Agent (ohne `+…`),
  Messlog-Kopf; kein Fenstertitel, kein Versionskopf in `cargoplan.log`.
- Remote: Releases `v0.1.0-rc.3` und `holo-v1` (beide Pre-release), Tags dieselben; `releases/latest` → `/releases`.
  README zeigt schon auf `releases/latest`.
- `holo-v1`: 120 Assets = 118 `.holo` + `manifest.json` (listet 118) + `NOTICE.md`; URL = `HoloRelease.BaseUri`.
- Daten liegen in `%LocalAppData%\SC-CargoPlan` (nicht `%AppData%`, dort nur die CT-DB).
- Link-Check der Notes (`linkcheck.py` aus T9.1): 0 kaputt, Negativtest mit zwei falschen Ankern schlägt an; extern
  200 außer Nexus (403 an curl, Bot-Schutz; Prüfung im Browser bei der Nachprüfung).
- Reproduzierbarkeit: Hash lokal ≠ CI erwartet (D49); verglichen werden Eintragsliste und `--version`.
- Merge `--no-ff` 6ae6444; Tests auf dem Merge-Commit = `main` vorher (Core 1.497, Import 141, App 1.533), Debug und
  Release 0 Warnungen. `publish.ps1 -Version 0.1.0`: ZIP 62,1 MB, 360 Dateien (die 358 aus T7.4 plus
  `ships/salvation.json` und `ships/modules/ts_module.json`), `CargoPlan.Cli --version` = `0.1.0+6ae6444…`.
- **M6** (`m6.ps1`, Martin, 31.10.2026 16:54–17:04, SC vorn, Cursor frei; lokales ZIP, self-contained, Runtime 10.0.13;
  `--measure-frames <Ziel> --over-game`, 30 s je Lauf, drei Runden, Median der drei Läufe). Ein erster Durchgang um
  16:47 lief ohne SC (`empty` 14 % spät) und ist verworfen.

  | Ziel | M5 (T8.7) spät | M6 Läufe spät | M6 Median | Gate ≤ M5 + 2 pp |
  |---|---|---|---|---|
  | `empty` | 0,1 % | 1,0 / 0,3 / 0,3 | 0,3 % | ✓ |
  | `hull_c` | 0,2 % | 0,2 / 0,1 / 0,2 | 0,2 % | ✓ |
  | `hull_c_states` | 2,9 % | 0,4 / 0,7 / 0,4 | 0,4 % | ✓ |
  | `hull_c_drag` | 11,8 % | 11,9 / 11,5 / 12,1 | 11,9 % | ✓ |
  | `hull_a_outline` | 0,2 % | 0,2 / 0,2 / 0,1 | 0,2 % | ✓ |
  | Vorschau `hull_c_drag` p95 / max | 5,23 / 20,3 ms | p95 1,80 / 1,92 / 1,83, max 13,4 / 13,7 / 10,8 ms | 1,83 ms | ≤ 16 ms ✓ |

  path keys 25 / 27 / 26, „drag held yes“ in allen drei; Hit p95 ≤ 0,002 ms. Danach Tag `v0.1.0` (annotiert) auf 6ae6444.

**Befund (nach dem Push):**
- Release-Workflow grün (2 min 41 s), CI auf `main` grün; Release `v0.1.0` kein Pre-release, „Latest“,
  `releases/latest` → `tag/v0.1.0`; Assets ZIP 64,2 MB und `.sha256`; Download gegen `.sha256` OK, `--version` =
  `0.1.0+6ae6444…`, 360 Einträge, alle mit `/`.
- **Eintragsliste weicht in einem Namen ab:** das CI-ZIP bringt die Runtime **10.0.14** mit, das lokale (M6) 10.0.13
  (`mscordaccore_…`, `coreclr.dll`, `wpfgfx_cor3.dll`, `runtimeconfig.json`). Der Runner holt per `setup-dotnet 10.0.x`
  das neueste SDK, lokal SDK 10.0.402. Damit hält D49 „a local run gives the released artifact's content“ nur bei
  gleichem SDK-Stand. Self-contained mit 10.0.14 ist nicht über dem Spiel gemessen; M5 (10.0.14, framework-dependent)
  und M6 (self-contained, 10.0.13) sind beide grün. Martin bei der Abnahme: kein weiterer M6-Lauf, das veröffentlichte
  ZIP ist heruntergeladen und läuft.
- Veröffentlichtes ZIP (Runtime 10.0.14) nicht gemessen, M6 lief auf 10.0.13; siehe B7/D86.
- Nachprüfung (Martin, 02.11.2026): alle Punkte der Checkliste grün – Release-Seite (Titel, Latest, Notes, Assets,
  Nexus-Link im Browser), Hash, Start nach Wiki „Installation and first start“ mit frischem Profil, Version in der
  Fußzeile, Holo-Download aus `holo-v1`, Abschalten.

**Akzeptanz:**
- [x] Notes-Endfassung (R1, R2 unverändert; N1–N3), Link-Check 0 kaputt, Negativtest schlägt an
- [x] `dotnet build` 0 Warnungen, `dotnet test` grün, Zahlen = `main`
- [x] M6 über dem Spiel grün gegen M5
- [x] `git merge --no-ff` nach `main` (6ae6444), Tag `v0.1.0`, Release über `release.yml` grün, veröffentlichtes ZIP läuft
- [x] rc.3-Release und -Tag gelöscht (remote und lokal), `release/v0.1.0` gelöscht, dazu die gemergten Remote-Branches
  `feat/k2-plan-editor`, `feat/k3-planner-rules`, `feat/k5-data`, `feat/k6-elevator-flow`, `spike/s8-3d-finding`
  (Martin, 02.11.2026); remote bleiben die Releases `v0.1.0` (Latest) und `holo-v1`

### [ ] T9.3 Nexus-Mods-Seite (= T6.6) – Branch `docs/nexus`
- Texte und Checkliste versioniert in `docs/nexus/`: `summary.txt`, `description.bbcode`, `checklist.md`,
  `staff-request.md`; die Beschreibung aus README, `docs/release-notes/v0.1.0.md` und Wiki abgeleitet
- Upload = exakt das GitHub-Asset `SC-CargoPlan-v0.1.0-win-x64.zip` (gleicher SHA256), Version `0.1.0`
- D87, `CONTRIBUTING.md` Abschnitt „Release“, README-Link nach dem Launch
- Martin legt die Seite an (Account, Upload, Klicks); CC trägt URL und Datum nach

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
- Nexus-Regeln gelesen statt angenommen: Netzzugriff nur, wenn „crucial“, der Holo-Download ist optional → vor dem
  ersten Upload Anfrage an den Nexus-Support (`staff-request.md`), Upload nach Zustimmung oder nach Fristablauf
  16.11.2026 ohne Antwort (14 Tage statt offenes Warten, D87-Addendum); eine Ablehnung stoppt T9.3, kein
  Nexus-Sonder-Build.
- Anfrage, Abschnitt ContractTracker mit Belegen, ohne Wertung des Autors: Direktnachricht an den Autor am 20.10.2026,
  keine Antwort; Discord-Link auf mods/34 ungültig; optional, read-only auf die lokale Datei, kein Netz; bei Einwand
  des Autors gehen die CT-Bezüge von der Nexus-Seite. Silhouetten-Absatz nennt die schriftliche Zustimmung von
  Fleetyards (Torlek, 23.10.2026).
- Netzzugriff in der Anfrage genau wie im Code: Default-Look Outline, dann kein Request; erst ein Hologramm-Look lädt
  per GET aus `holo-v1`, danach je Start und Schiffswechsel das Manifest höchstens einmal pro Lauf und nicht, solange
  das gecachte jünger als 24 h ist; Schalter aus → kein Request. Code-Links auf `fd75313`.
- `outlines/` im ZIP (aus Renders von CIG-Schiffen, nicht MIT, D55), Befund außerhalb des Auftrags: offengelegt in
  Anfrage, Beschreibung und Permissions; Asset use „ask/credit“, Martin entscheidet an der Maske.
- Der CT-Autor wird nicht erneut gefragt; die Beschreibung sagt „only reads its database“.
- KI-Tags „AI-Generated Content“ und „AI Media“, dazu ein Satz unter Shout outs.
- Keine Donation Points, kein Text dazu. CT nicht im Requirements-Feld (Nexus kennt kein „optional“).
- Kategorie Miscellaneous (die einzige für Star Citizen), Tags Utilities for Players, Quality of Life, User Interface.
- Maske nach Martins Screenshots: fünf Schritte General → Media → Files → Requirements → Permissions, Summary
  350 Zeichen, Beschreibung als BBCode über den [ ]-Button; `description.bbcode` in den fünf Vorlagen-Blöcken
  (Description, Installation instructions, Main features, Requirements, Shout outs), kein `[font]`.
- Checkliste versionsneutral (`$version`), Hash in den Befund statt in die Checkliste; Größenvergleich in Byte, MB und
  MiB; nach Publish Hash und Größe des Nexus-Downloads.
- `holo-v1` in der Beschreibung nur als Name, kein Link (Regel gegen Links auf nicht hostbare Dateien).
- Hero-GIF als Bild 1; abgelehnt oder ohne Bewegung → Einzelbild per ffmpeg nach `docs/nexus/hero.png`.
- README nach dem Launch: Nexus als Klammerzusatz in Install Schritt 1, GitHub bleibt erste Quelle.

**Befund:**
- File Submission Guidelines gelesen 02.11.2026 (Stand 27.10.2026). Nexus-Seiten (Wiki, News, Mod-Seiten) antworten
  Skripten mit 403; Kategorien, Tags und Requirements aus der öffentlichen GraphQL-API (`api.nexusmods.com/v2/graphql`,
  Star Citizen = Spiel 2246): 34 Mods, alle Miscellaneous; `ModRequirement` hat `notes`, kein Optional-Flag; CT
  (Mod 34): Autor Yuiki, Uploader uctv. Ein SC-Mod mit KI-Übersetzung trägt bereits „AI-Generated Content“.
- BBCode-Syntax nur aus einer Sekundärquelle (BUTR/Converter.MarkdownToBBCodeNM); die Vorschau im Editor ist
  Checklist-Schritt.
- Asset `SC-CargoPlan-v0.1.0-win-x64.zip`: 64.164.865 Byte (64,2 MB / 61,2 MiB), SHA256
  `ad8330f9a579a935fb3bba43d3c8291e908b4f882ff71e687560c352f8786a97`. Die „63,6 MB“ aus der Freigabe passen zu keiner
  der beiden Einheiten.
- Netzcode: einziger `HttpClient` in `HoloSource.cs:67` (Grep über `src`).
- Link-Check `description.bbcode` (Skript nach `linkcheck.py`, T9.1, um `[url=…]` erweitert): 13 Links, 0 kaputt,
  Wiki-Anker gegen die lokale Wiki-Kopie, Nexus-Link im Browser; Negativtest (falscher Anker, tote URL) meldet 2.
  Code-Links der Anfrage (7) und Guidelines: 200.
- Quellentreue (Skript: jeder Satz wörtlich in README, Notes oder Wiki?): ohne Quelle nur die Überschriften, die
  D87-Sätze (Files tab, `.sha256` des GitHub-Release, Source code, Bugs auf GitHub, KI-Satz), der D63-Satz
  (`decisions.md:714`) und FAQ:11 ohne die Klammer „(see …)“. `summary.txt` = README:5-6, 167 Zeichen.
- CT-Discord (02.11.2026): die Beschreibung von mods/34 (GraphQL-API, zuletzt geändert 02.06.2026) enthält zweimal
  `discord.gg/9RNUXAdVn`; Martin im Browser „Ungültig oder abgelaufen“, Discord-API `invites/9RNUXAdVn` → 404
  „Unknown Invite“ (Code 10006). Read-only belegt: `ContractTrackerReader.cs:108` (`SqliteOpenMode.ReadOnly`), Link
  auf `fd75313`.
- Quellen im Repo nennen es anders: `contracttracker-schema.md:7` „asked of the author in the Nexus Discord on
  2026-10-20 09:26“ (Anfrage sagt neutral „by direct message“); Fleetyards-Zustimmung steht in T7.4b Gate 1 als
  „Mail von Marten (Fleetyards)“, D63 zitiert Marten mit „Grauzone, keine offizielle Freigabe“. Martin, 02.11.2026:
  Torlek ist Marten; die Zustimmung gilt der Nutzung aller Inhalte der Fleetyards-API, also auch den Silhouetten
  (aus den Renders der API, `outlines/NOTICE.md`).

**Akzeptanz:**
- [x] `docs/nexus/*`, D87, CONTRIBUTING, TASKS; Link-Check 0 kaputt, Negativtest schlägt an
- [ ] Anfrage an den Nexus-Support gesendet und beantwortet (Datum, Wortlaut) oder Frist 16.11.2026 ohne Antwort
  abgelaufen
- [ ] Nexus-Seite online mit dem ZIP des GitHub-Releases (gleicher SHA256 und gleiche Größe des Nexus-Downloads),
  Permissions passend zu MIT, CT-Credit sichtbar; URL und Datum
- [ ] README: Nexus als zweite Quelle
- [ ] `git merge --no-ff` nach `main`

### ~~[ ] T9.4 Fleetyards-Verlinkung~~ gegenstandslos seit D94 (gestrichen in K10)

---

## Bugfix B7 SDK-Pinnung (D86) (02.11.2026) – Branch `chore/sdk-pin`

**Warum:** Messlücke v0.1.0: das veröffentlichte ZIP bringt Runtime 10.0.14 mit (Release-Runner: SDK 10.0.403 über
`setup-dotnet` `10.0.x`), M6 maß das lokale ZIP mit 10.0.13 (SDK 10.0.402). Self-contained heißt, die Runtime des SDK
liegt im ZIP; ohne gepinntes SDK ist lokal ≠ CI. Kein Verhalten, Core unverändert.

### [x] B7 SDK pinnen (02.11.2026, abgenommen 02.11.)
- `global.json`: SDK 10.0.403, `"rollForward": "disable"`
- `ci.yml`, `release.yml`: `setup-dotnet` mit `global-json-file: global.json` statt `dotnet-version: 10.0.x`
- `dependabot.yml`: Ökosystem `dotnet-sdk`, damit Runtime-Patches als PR kommen
- Martin: lokales SDK auf 10.0.403
- D86, TASKS.md

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
- Dependabot `dotnet-sdk` `weekly` (Patch Tuesday ist monatlich, `monthly` kann einen Runtime-Fix vier Wochen liegen
  lassen), `version-update:semver-major` ignoriert (.NET 11 ist ein Stackwechsel und eine D-Nummer).
- `errorMessage` in `global.json`, ohne Versionsnummer (bleibt bei Dependabot-Bumps richtig).
- CI auf dem Branch über einen PR (`ci.yml` läuft nicht bei Branch-Push); CC merged lokal `--no-ff`, der Merge-Button auf
  GitHub bleibt unberührt, GitHub schließt den PR nach Martins Push.
- Kein `RemovePreviousVersion = never`: lautes Scheitern mit `errorMessage` ist gewollt, eine Registry-Einstellung ist
  nicht im Repo nachvollziehbar.
- `CONTRIBUTING.md` („Requires the .NET 10 SDK“ war mit `disable` falsch) und `CLAUDE.md` (Technik-Tabelle) mitgeändert;
  D49-Addendum verweist auf D86; Satz zur Messlücke auch unter T9.2.
- Schritt 0 belegt per `dotnet --list-sdks` (10.0.403, kein 10.0.402), sonst wäre der Negativtest keiner; steht
  10.0.402 noch da, läuft der Negativtest mit 10.0.401.

**Befund:**
- SDK des Release-Runners aus dem Log von „Release v0.1.0“ (Run 41218451178): `dotnet-sdk-10.0.403-win-x64.zip`,
  „Installed version is 10.0.403“. winget bot am 02.11.2026 als neueste `Microsoft.DotNet.SDK.10` ebenfalls 10.0.403.
- `winget install Microsoft.DotNet.SDK.10 --version 10.0.403` ließ 10.0.402 stehen (nach der Doku entfernt der Installer
  die Vorversion). Negativtest deshalb zweimal: vor der Installation mit 10.0.403 in `global.json` (nur 10.0.402
  installiert) und danach mit 10.0.401 (10.0.402 und 10.0.403 installiert, `patch` würde auf 10.0.403 rollen). Beide Male
  Exit 155 mit dem `errorMessage`-Text: SDK 10.0.403 wertet den Schlüssel aus.
- `dotnet build` 0 Warnungen, Tests Core 1.497, Import 141, App 1.533 (= `main`).
- `publish.ps1 -Version 0.0.0-dev`: `coreclr.dll` 10.0.1426, `wpfgfx_cor3.dll` 10.0.14, Build-Commit `0427b9a…` wie im
  CI-ZIP v0.1.0; Eintragsliste 360 Einträge, identisch mit dem CI-ZIP v0.1.0 (in T9.2 wich genau der `mscordaccore`-Name ab).
- `dependabot.yml` `ignore` (`dependency-name: "*"`, `update-types: ["version-update:semver-major"]`) gegen die
  Options-Referenz geprüft, keine Einschränkung auf Ökosysteme.

**Akzeptanz:**
- [x] `dotnet --version` im Repo = `10.0.403` = `global.json`; Negativtest mit fehlender Version schlägt an
- [x] `dotnet build` 0 Warnungen, `dotnet test` grün, Zahlen wie `main`
- [x] Lokales `publish.ps1 -Version 0.0.0-dev`: Runtime 10.0.14 im Ordner, Eintragsliste = CI-ZIP v0.1.0
- [x] CI auf dem PR grün (Windows und Ubuntu) mit SDK 10.0.403 (PR #20, Run 41389845142; Windows installiert 10.0.403,
  auf Ubuntu war 10.0.403 vorinstalliert und wurde mit `--version 10.0.403` angefordert)
- [x] Martins Abnahme (02.11.2026: „grün“), dann `git merge --no-ff` nach `main`

---

## K9 Sektionen pro Ziel (05.11.2026) – Branch `feat/k9-sections`

**Warum:** Der Planer legt auf flachen, breiten Gittern (ARGO RAFT 12 × 8 × 2) größte Kisten zuerst über alle Ziele hinweg. Martin schiebt sie heute von Hand zu Blöcken pro Ziel. Das soll der Planer übernehmen, konfigurierbar, weil je nach Beladung beide Bilder richtig sind.

**Modell (Martin, Rückfragen zum Plan):** Richtung und Reihenachse sind orthogonal. Zellreihenfolge = Stapel > Fortschrittsachse > Reihenachse; `fillRows` wählt die Reihenachse, `fillOrder` die Richtung entlang der Länge.

| `fillRows` | Ordnung | mit `grouping = byDestination` (K9.2) |
|---|---|---|
| `lengthwise` (Default, wie heute) | Stapel > Breite > Länge | Streifen Bug↔Heck, nebeneinander über die Breite |
| `crosswise` | Stapel > Länge > Breite | Blöcke über die volle Breite, nebeneinander entlang der Länge (Martins Handbild) |

**Fixture:** `tests/CargoPlan.Core.Tests/Fixtures/Plans/raft_hand_2026-11-05.json`, Martins `plan.json` (RAFT, 4 CT-Missionen, Schema 8). Green Glade 27 SCU, High Course 9, Thundering Express 22, Faithful Dream 6, Melodic Fields 2, Summe 66. Die Lücken darin sind Handarbeit, kein Planer-Soll.

### [x] K9.1 Reihenachse wählbar (D88) (05.11.2026, abgenommen 05.11.)

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** eigenes Feld `fillRows` statt der Task-Werte `portToStarboard`/`starboardToPort`; Stapel bleibt äußerst statt „immer innerst“; Breite läuft fest Steuerbord→Backbord; `grouping = none` bleibt größte zuerst über die Ziele hinweg (D64), der Satz „liefert schon ein Ziel nach dem anderen“ ist gestrichen; die Exhaustive-Begrenzung aus dem Plan entfällt, weil `FillOrder` zwei Werte behält.

- Core: `FillRows`, `Rack.StacksSideways`, `CrateLayout.Ranks` als einzige Stelle mit den Achsen, durchgereicht durch Layout, Suche, `RackAssignment`, `LoadPlanner`, `MoveResolution`
- `RackAssignment`: gleiche Racks gelten bei `crosswise` nur als Zwillinge, wenn `StacksSideways` übereinstimmt (Befund aus der Umsetzung, nicht im Plan)
- Formate: plan.json Schema 9, Missions-JSON Schema 4, `FillRowsJson`, `docs/plan-format.md`, `docs/mission-format.md`
- App: ComboBox **Rows** neben **Fill order**, `FillRowsChoice`; CLI liest das Feld
- Tests: `FillRowsTests`, `FillRowsFormatTests`, `OrderFreePlanTests` in allen Zellordnungen, `HullAGoldenTests` crosswise; Messung `FillOrderTimingTests`

**Messung** (Release, Median aus 3, ms; Knoten und Suchschritte identisch):

| Fall | Richtung | vorher | `lengthwise` | `crosswise` | Knoten | Schritte | gave up |
|---|---|---|---|---|---|---|---|
| RAFT-Fixture | bow | 10,6 | 10,2 | 8,4 | 30 | 0 | 0 |
| RAFT-Fixture | stern | 8,6 | 8,6 | 8,3 | 30 | 0 | 0 |
| Hull C, 8 Ziele | bow | 62,5 | 53,3 | 39,6 | 1.000.001 | 104 | 0 |
| Hull C, 8 Ziele | stern | 56,1 | 48,4 | 35,9 | 1.000.001 | 104 | 0 |

Hull C erreicht schon vor K9.1 das Knotenlimit der Rack-Zuordnung (D64): Ist-Stand, keine Änderung.

**Abnahme:**
- [x] `lengthwise` unverändert: Goldens nur `schemaVersion` plus `fillRows`-Zeile, Zeiten in der Streuung, Zähler identisch
- [x] `crosswise` nach den Rangtabellen, Ebene 0 voll vor Ebene 1
- [x] Racks mit Stapelachse ±y ignorieren `fillRows`, Hull A byteidentisch bis auf das Wort
- [x] Spiegeltest 1er/4er/8er, Kapazitäts-Eigenschaft mit länglichen Kisten, Ordnungsinvarianz in allen Zellordnungen
- [x] Schema 9 und 4 mit Migration, Fehlermeldungen für alte Schemas und unbekannte Wörter
- [x] Re-plan, Laden, Schiffswechsel, Sync, Discard, Split und Verschieben behalten den Wert
- [x] Standardlauf Core 1.592, App 1.544, Import 141, `Category=Slow` 2/2 grün; `Category=Exhaustive` auf Martins Wunsch abgebrochen, nicht durchgelaufen: er prüft nur Hull A `lengthwise`, das Goldens und Suchzähler belegen. Nach K9.2 laufen lassen
- [x] Martin: ComboBox **Rows** ansehen, RAFT crosswise planen (abgenommen 05.11.)

### [x] K9.2 Sektionen pro Ziel (D89) (05.11.2026, abgenommen 05.11.)

D89 mit Addenda zu D41, D64, D66. Korrigierte Vorgaben aus der Spec:

- **Ziel:** Pro Rack mit mehr als einem Ziel bekommt jedes Ziel einen zusammenhängenden Reihenblock entlang der Fortschrittsachse; Grenzen liegen auf Zellkanten, Sektionen liegen bündig aneinander, Löcher innerhalb einer Sektion bleiben frei.
- **Reihenfolge (E1):** Wie die Legende sie zeigt: Gruppen in Legendenordnung (B6), darin die Zeilen von oben nach unten, ein Ziel zählt beim ersten Auftreten. Fixture: Green Glade, High Course, Thundering Express, Faithful Dream, Melodic Fields.
- **Sektionsmaß (E3):** kleinste Reihenzahl, in die alle Kisten des Ziels passen, D71 unverändert: erst ungedreht, gedreht nur, wo ungedreht scheitert.
- **Passt die Summe nicht:** die kleinsten Ziele teilen sich eine Sektion, weißer Hinweis `shares a section with <Ziel>`.
- **Kapazität vor Sektionen (Martin, vorläufig):** Keine Lieferung wartet, die ohne Sektionen Platz hätte; sonst Hinweis `placed outside sections (capacity)`. Invariante als Test: wartende Lieferungen mit `byDestination` == mit `none`.
- **Key:** plan.json `grouping`: `none` | `byDestination`, Default `none`, Missions-JSON optional, Export schreibt immer. ComboBox „Group by destination“ im Editor, pro Rack, kein Hotkey, keine gezeichneten Grenzen.
- **Geladen/Abgeliefert (D79):** geladene Kisten sind Hindernisse. **Sync (D41):** vorhandenes Ziel in seine Sektion, sonst um eine Reihe wachsen, wenn die Nachbarreihe leer ist; neues Ziel in den freien Rest, sonst Hinweis `no free section – Re-plan`. **Verschieben, Undo:** unverändert.
- **Goldens (E2):** aus dem Fixture gerechnet, nicht aus der Spec: `crosswise` + Sektionen ist das Handbild ohne Lücken, dazu ein zweites Golden `lengthwise`. Die Zahl „2+1+2+1+2“ ist gestrichen.
- **Gate am Aufzug (Martin):** RAFT mit den 4 Missionen; fünfte Mission per CT (vorhandenes und neues Ziel); Mission auf „geladen“, dann Re-plan; Hull A mit 5 Zielen.
- **Nicht Teil:** Sektionsreihenfolge per Legenden-Drag, Default pro Schiff, zweiseitige Sektionen.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Racks mit Stapelachse ±y führen Sektionen entlang der Länge, egal was `fillRows` sagt; Invariante für Plan und Re-plan, nicht für jeden Sync-Schritt; Ordnungsinvarianz mit `byDestination` nur für die Racks; Hinweise als getragene Notizen mit eigener weißer Zeile, weil Plan-Warnungen seit D64 das Overlay nicht erreichen; kein eigenes Schema; Editor „Grouping:“ mit „None“/„By destination“; Legendenreihenfolge in Core; Drehen vor Teilen; geteilte Sektion an der Position des früheren Ziels; Verschieben einer Kiste löscht den Hinweis ihres Ziels.

**Neubewertung:** sobald im Live-Test eine Sync-Mission wartet, die mit `none` Platz hätte.

- Core: `PlanGrouping`, `RackSections`, `SectionNotes`, `LoadPlan.LegendMissions`, Durchgang in Plan, Editor-Plan, Re-plan, Schiffswechsel; `LayStop` für den Sync
- Formate: `grouping` in Plan-Schema 9 und Missions-Schema 4, `GroupingJson`, Goldens eine Zeile mehr
- App: ComboBox **Grouping**, weiße Zeile `NoticeSource.Sections`
- Tests: `RackSectionsTests`, `RackSectionsSyncTests`, `LegendOrderTests`, `FillRowsFormatTests`, `MissionEditorViewModelTests`

**Messung** (Release, Median aus 3, ms; Knoten und Suchschritte in allen Zeilen wie K9.1, kein „gave up“):

| Fall | `none` | `byDestination` | ohne Tiered JIT `none` / `byDestination` |
|---|---|---|---|
| RAFT-Fixture | 8–10 | 11–24 | 3,0–4,7 / 6,5–7,1 |
| Hull C, 8 Ziele | 34–57, erste Zeile 148 | 48–50 | 44 / 68 |
| RAFT, 8 Ziele | 1,2–1,5 | 11–16 | nicht gemessen |

Die 148 ms sind JIT-Aufwärmen: Mit `DOTNET_TieredCompilation=0` liegt dieselbe Zeile bei 44 ms.

**Abnahme:**
- [x] Goldens RAFT `crosswise` und `lengthwise`, aus den Kisten gerechnet
- [x] Invariante: gleiche Racks, gleiche Warteschlange wie `none` bei Plan und Re-plan
- [x] Sektionen zusammenhängend und bündig; geteilte Sektion im gemeinsamen Bereich, Hinweis nennt das richtige Ziel
- [x] Drehen vor Teilen, Position des früheren Ziels
- [x] Geladenes bleibt, Abgeliefertes und `offShip` unberührt
- [x] Sync: eigener Bereich, Wachsen, neue Sektion, beide Hinweise, nichts bewegt sich
- [x] Hull A: zwei Sektionen entlang der Länge in beiden `fillRows`
- [x] `none`: alle Goldens und Property-Tests aus B2 grün
- [x] `Category=Slow` 2/2, `Category=Exhaustive` 2/2 in 8 min 14 s (nachgeholt aus K9.1)
- [x] Martin: Gate am Aufzug, zusammen mit K9.2b (bestanden 05.11.)

### [x] K9.2b Section gap (D89-Addendum, D71-Addendum) (05.11.2026, abgenommen 05.11.)

**Warum:** Zwischen bündigen Sektionen lässt sich schlecht greifen. `sectionGap` hält 0, 1 oder 2 leere Reihen frei, Default 0.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Rückfallstufen ungedreht mit Lücken → drehen → Lücken 2 → 1 → 0, Drehungen bleiben → teilen → Rack wie vom Planer; keine Notice beim Streichen. Eine Reihe mit geladener Kiste hinter einer Sektion zählt als Lücke. Neues Ziel im Sync: freier Rest mit Abstand, dann enger, dann frühere leere Reihen, je Stufe ungedreht vor gedreht; „Ende zuerst“ auch bei gap 0. Freigewordene Reihen vorne bleiben bis zum Re-plan leer. Kein eigenes Schema. Eigene Zeile „Layout“ im Editor statt breiterem Fenster, Entscheidung am Gate.

- Core: `LoadPlan.SectionGap`, `RackSections` (Stufe, `RowsFor` in Stufen), `LayStop`; Wert überlebt `grouping = none`, Schiffswechsel und Discard
- Formate: `sectionGap` in Plan-Schema 9 und Missions-Schema 4; Goldens eine Zeile mehr, Lage unverändert
- App: ComboBox **Section gap**, nur aktiv bei „By destination“, `SectionGapChoice`; Zeile „Layout“ als `WrapPanel`, Fenster zurück auf 660 × 420, Mindestbreite 480
- Tests: `SectionGapTests`, `RackSectionsSyncTests`, `FillRowsFormatTests`, `MissionEditorViewModelTests`

**Messung** (Release, Median aus 3, ohne Tiered JIT, `bowFirst`, ms; Knoten und Suchschritte wie K9.1, kein „gave up“):

| Fall | `none` | gap 0 | gap 1 | gap 2 |
|---|---|---|---|---|
| RAFT-Fixture | 3,3 | 7,2 | 12,7–12,9 | 17,3–18,0 |
| RAFT, 8 Ziele | 2,0–2,1 | 18,3–23,7 | 19,5–28,1 | 22,1–32,6 |
| Hull C, 8 Ziele | 44,8–56,6 | 73,1–83,4 | 77,7–85,0 | 83,8–84,8 |

**Abnahme:**
- [x] Goldens RAFT gap 1: `crosswise` 12 von 12 mit gedrehten 2ern, `lengthwise` wie ohne Lücken; 3 Ziele mit gap 2
- [x] Jede Stufe einzeln, Lücken stufenweise, Drehung bleibt erhalten
- [x] Racks und Warteschlange wie `none` für gap 1 und 2, Plan und Re-plan
- [x] Geladene Kiste zählt als Lücke; Hull A nur mit Lücke, wenn sie passt
- [x] Sync: Rest mit Abstand, enger, Lücke zuletzt, gedreht vor Lücke, Wachsen in die Lücke, Ende zuerst auch bei gap 0
- [x] Formate und Editor: Roundtrip, alte Schemas, ungültige Werte, Box inaktiv ohne Grouping
- [x] Martin: Gate am Aufzug, zusammen mit K9.2 (Lücken im RAFT, gedrehte Kisten, Sync in einen Plan mit Lücken; bestanden 05.11.)
- [x] Martin: Editor-Zeile „Layout“ ansehen und entscheiden (abgenommen 05.11.)

### [x] K9.3 Doku und Merge (05.11.2026, abgenommen 05.11.)

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Stufe „Rack wie vom Planer“ bekommt eine eigene Notice (`Rack 1: sections don't fit – laid as with None`) statt der „shares“-Hinweise; „Rows“ bleibt als Label, das Wiki schreibt „rows of crates“; das Wiki beschreibt nur Plan und Re-plan als sektionstreu.

- Core: `SectionNotes.NoSections`, D89-Addendum
- Wiki `Plan-and-editor` (Abschnitt „Layout“, Schema 4), `Ship-view`; README-Punkt; `CLAUDE.md`
- Merge `--no-ff` nach `main`, Martin pusht `main`; das Wiki empfohlen erst mit dem Tag, weil v0.1.0 keine Sektionen hat

## Release v0.2.0 (05.11.2026) – Branch `release/v0.2.0`

**Warum:** K9 soll vor der Nexus-Seite raus (Martin), die dauert noch: Der erste Upload ist damit gleich die aktuelle Version.

### [x] R1 Release v0.2.0 (05.11.2026, abgenommen 05.11.)

- Release-Notes `docs/release-notes/v0.2.0.md`; Nexus-Checkliste auf 0.2.0; Wiki „Applies to v0.2.0“
- Kein Versions-Bump: `release.yml` nimmt die Version aus dem Tag (D49)

**Befund (vor dem Tag):**
- Vollständigkeit `git log v0.1.0..main`, 37 Commits: K9 und der RAFT-Fix (7112a35) stehen unter „New since v0.1.0“, die Schema-Sprünge unter „Before you update“, Microsoft.Data.Sqlite 10.0.13 → 10.0.14 als „Under the hood“. Nicht in den Notes, weil ohne Wirkung aufs ZIP: SDK-Pin B7 (Runtime weiter 10.0.14 wie v0.1.0 aus der CI), Nexus-Texte T9.3, T9.2-Nachträge.
- K7 (3c4d39b) liegt vor dem Tag und steht schon in den v0.1.0-Notes. `src/CargoPlan.App/Settings` seit dem Tag unverändert: `settings.json` bleibt für 0.1.0 lesbar, keine Zeile nötig.
- „0.1.0 says so“ am v0.1.0-Stand im Code belegt: `PlanFile` liest bis Schema 8, `PlanStore.Load` meldet `Plan not loaded (…). The file stays until you make a new plan.`. Nur gelesen, nicht mit 0.1.0 ausgeführt.
- Holo- und Netzcode seit v0.1.0 unverändert; die Code-Links der Staff-Anfrage auf `fd75313` stimmen weiter.
- Keine Frame-Messung wie M6: K9 ändert am Rendern nichts.
- `publish.ps1 -Version 0.2.0` lokal: ZIP 62,1 MB, Runtime 10.0.14, `--version` `0.2.0+da54632…`. CI auf da54632 grün.

**Abnahme:**
- [x] Notes-Text von Martin korrigiert und freigegeben
- [x] Martin: `main` gepusht, CI grün auf 8c6aedc, Tag `v0.2.0` auf 8c6aedc gepusht, Wiki gepusht (Anker `#layout` antwortet)
- [x] Release-Workflow grün, kein Pre-release, „Latest“; ZIP 64.2 MB gegen `.sha256` OK, 360 Dateien, `--version` `0.2.0+8c6aedc…`, RAFT `-z`

## v0.3.0 – Feedback-Release

**Warum:** Das erste Reddit-Feedback zu v0.2.0 geht als ein Release raus, in sechs Sessions (Session 4 dreigeteilt), jede per `--no-ff` nach `main`. Der Tag kommt erst in Session 6.

1. [x] F2 + F1 Schiffsauswahl – `fix/ship-picker` (abgenommen 06.11.)
2. [x] F7 Holo und Racks schiffsfest verankern – `feat/f7-ship-anchor` (abgenommen 06.11.)
3. [x] F3 freier Drehwinkel pro Rack, Schiffs-JSON 5→6 – `feat/f3-rack-rotation` (abgenommen 06.11.). **Vorgabe (Martin, 06.11.):** volle 3D-Rotation pro Rack, nicht Yaw. Eine Datenquelle mit Grid-Transformen im Schiffsraum existiert (Motherlode, 97 Schiffe, 3×4-Matrix in Metern, nur 5 Schiffe mit Drehung, Railen gekippt).
4. Schiffsdaten aus den Spieldateien (Motherlode, D92), drei Merges. **Ziel (Martin):** für alle Schiffe stimmen SCU und Rack-Anordnung.
   - [x] 4a Racks – `feat/ship-data-game` (abgenommen 07.11.)
   - [x] 4b Hülle und Umriss aus denselben Modellen – `feat/holo-v2` (abgenommen 08.11., `holo-v2` veröffentlicht)
   - [x] 4c Stilllegung Fleetyards, Holo-Übergang, Credits – `chore/retire-fleetyards` (abgenommen 08.11., D94, D95)
   - [x] 4d Editor-Fixes aus Issue #22/#21 (Reikal): B8 Dropdown, B9 „N SCU or smaller“, F9 Kistenliste gruppiert – `fix/editor-crates` (D96, abgenommen 08.11., Merge 754921c)
5. [ ] F8 flache 2D-Rasteransicht als zweiter Modus – `feat/f8-flat-view`. **Zurückgestellt** (Release-Session 5): nicht in v0.3.0.
6. [x] Release-Notes, Wiki, Tag `v0.3.0` – `release/v0.3.0` (siehe „Release v0.3.0“ unten; veröffentlicht 08.11.2026). **Aus 4c (D94):** Wiki (`FAQ.md`, `Ships-and-cargo-grids.md`, `Troubleshooting.md`, `_Footer.md` mit „Not affiliated with Motherlode, Fleetyards, ContractTracker or CIG“), Nexus-`description.bbcode` (Credits) und `.github/ISSUE_TEMPLATE/ship-correction.yml` (Gitter kommen aus den Spieldateien) nachziehen; sie beschreiben bis zum Tag v0.2.0. **Vor dem Tag `docs/release-notes/v0.3.0.md` fertig schreiben:** `release.yml` prüft nur, ob die Datei existiert, und veröffentlicht sie wörtlich.

### [x] F2 Mausrad scrollt gruppenweise (Bug) (06.11.2026, abgenommen 06.11.)

**Ursache:** Das Theme setzt `CanContentScroll` auf der ComboBox, unser `ScrollViewer` übernimmt es, und der Trigger des Standard-Templates, der es bei Gruppierung abschaltet, fehlte im eigenen Template. Damit scrollte das `StackPanel` in Gruppen.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Fix mit `CanContentScroll="False"` statt `ScrollUnit="Pixel"`, weil die Liste nicht virtualisiert und nur der `VirtualizingStackPanel` `ScrollUnit` liest. Keine Vorher-Messung, Nachweis an der echten Liste nach dem Fix. Der Grund für den abgeschnittenen Zeus ist damit nicht einzeln belegt: Die RSI-Gruppe passt in die Liste (Filter `rsi`: 21 Zeilen, nichts zu scrollen), die Vermutung „Gruppe höher als die Liste“ ist widerlegt.

- [x] Fest 48 DIP pro Raste, über einer Zeile, über einer Gruppenüberschrift und bei aktivem Filter (`a`, 77 Zeilen); kurze Trefferliste (`hull`) springt nicht
- [x] 38 Rasten bis zum Ende, keine der 101 Zeilen übersprungen, `Zeus Mk II ES` vollständig sichtbar
- [x] Suche, Esc und Enter unverändert; Pfeile und Bild auf/ab bleiben verschluckt (T7.2c), eine Pfeil-Navigation gibt es nicht
- [x] Beim Öffnen steht das Schiff statt seiner Gruppe oben, angenommen
- [x] Martin: Gate über Star Citizen: wenige Schiffe pro Raste, kein Sprung, Zeus sichtbar, Liste endet sauber
- [x] Martin: 38 Rasten bis zum Listenende, im Gate abgenommen

**Rückfall, nicht gebaut und im Gate nicht nötig:** Falls Spieler es später „zu zäh“ finden: `PreviewMouseWheel` am `ScrollViewer`, Schritt = `SystemParameters.WheelScrollLines` × Zeilenhöhe (respektiert die Windows-Einstellung).

### [x] F1 SCU-Kapazität in der Schiffsauswahl (06.11.2026, abgenommen 06.11.)

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** Die Kopfzeile hatte keine Tausendertrennung, sie übernimmt das Komma der Liste (`56/4,608 · 4,552 free`, D67-Addendum). Die Quelle der Zahl ist die Eigenschaft `Ship.TotalScu`, keine eigene Funktion. Rechtsbündige Spalte statt inline, Wahl am echten Element.

- [x] Hull A `(64 SCU)`, Aurora Mk II · TS module `(8 SCU)`, Hull C `(4,608 SCU)`, kleinstes Schiff UTV `(1 SCU)`
- [x] Suche findet weder `64` noch `scu`; Eintrag „Not loaded“ ohne Zahl
- [x] Name kürzt zuerst, die Kapazität nie (Probedatei mit überlangem Namen, nur in `bin/`)
- [x] Martin: Gate über Star Citizen: Kapazität hinter jedem Namen, Modul zeigt eigene Zahl, Suche unverändert

### [x] F7 Holo und Racks schiffsfest verankern (06.11.2026, abgenommen 06.11.)

**Warum:** Reddit (Ryekal): Das Holo hängt an der Mitte aller Racks. Ein korrigierter Rack-`origin` verschiebt alles gegeneinander, deshalb lassen sich Schiffsdaten nicht von Hand korrigieren. Bei der Hull A fällt es wegen der Symmetrie nicht auf.

**Befund:**
- Holo: [HoloPlacement.cs](src/CargoPlan.App/ShipView/HoloPlacement.cs), Rack-Mitte − Mesh-Box-Mitte + `holoOffset`. Umriss: [OutlinePlacement.cs](src/CargoPlan.App/ShipView/OutlinePlacement.cs), Mitte, Boden **und Maßstab** hingen an den Racks (Maßstab bei Railen 1,594, Hull A 1,12, Caterpillar 1,116, Syulen 1,032). Core liefert nur die Box `Ship.Bounds`.
- `holoOffset` hatte nur die Aurora Mk II, der UTV keinen. Einen Umriss-Offset gab es nicht, `ShipExtent` seit T8.6 auch nicht mehr.
- **Fleetyards-Offsets liegen nicht im Modellraum:** Bei 8 von 9 Schiffen beginnen sie in x, y, z bei 0 (lokal im Frachtraum). Der Mesh-Ursprung ist die Mitte der xy-Box und der Kiel, nicht der Modellursprung; das rohe glTF hat keinen gemeinsamen Maßstab (0,139–1,364).
- Echte Fleetyards-Lagen 7 (Hull A, B, C, C8 Pisces, C8X, Starlancer MAX, TAC), generiert 90 (88 ohne Offsets, Carrack und Caterpillar überlappend). Die Hull-A-Datei liegt +3 Zellen in y gegen Fleetyards.
- Kamera: Das Fit-Ziel bleibt rack-bezogen, gespeicherte Kameras bleiben gültig, weil kein `origin` sich ändert.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
- Schiffsursprung = Nullpunkt der Datei, nicht der Mesh-Ursprung; D90 heißt deshalb „Hologram, outline and racks lie independently in ship coordinates“.
- Neue Felder `holoOrigin`, `outlineOrigin`, `outlineScale` statt neuer Bedeutung für `holoOffset`; Schema bleibt 5, 5→6 bleibt für F3. Migration beim Laden ginge nicht, z braucht die Mesh-Box.
- Alle 98 Einträge bekommen Werte, Formel mit Mesh-Box-Mitte statt „alter Wert + Rack-Mitte“. Round-Trip-Doubles statt gerundet.
- Ohne Felder gilt die alte Regel, `holoOffset` bleibt nur dafür lesbar.
- TS-Modul trägt eigene Werte, Bild wie `main`; die Hülle liegt dort weiter 3 Zellen hinter der des Schiffs.
- Core ändert sich nur beim Format (`ShipDto`, `ModuleDto`, `Ship`, neu `ShipCoordinates`), Planer und `Rack` nicht.
- Das Fit-Ziel wandert nur ohne Holo mit dem korrigierten Rack; mit Holo bleibt es stehen, weil die Holo-Box das Rack enthält.
- Bildvergleich über `ShipPictureTests` auf Release-Builds statt Publish-Build, der ohne Fenster nicht rendert. Publish-Builds prüft Martin am Gate.
- Harness committet, `Category=Slow`, in der App-`default.runsettings` ausgeschlossen. `ShipAnchorValues` hat `Freeze` und zusätzlich `Carry`, damit eine neue Holo-Version keine Handkorrekturen zurücksetzt. Das Eintragen ist Teil des Harness (`ShipAnchorValueTests`), kein Wegwerf-Code.

**Bildvergleich** (98 Einträge × 5 Looks × 2 Größen, Default-Kamera, ohne Ladung, dazu 5 Gate-Schiffe × 2 Looks mit gespeicherter Kamera):

| Look | Bilder | Identisch | Abweichend |
|---|---|---|---|
| Umriss, 400×302 und 800×600 | 196 | 196 | 0 |
| None, 400×302 und 800×600 | 196 | 196 | 0 |
| Holo shaded, 400×302 und 800×600 | 196 | 196 | 0 |
| Holo flat, 400×302 und 800×600 | 196 | 196 | 0 |
| Holo hidden line, 400×302 und 800×600 | 196 | 196 | 0 |
| Gespeicherte Kamera (Umriss, Holo shaded), 800×600 | 10 | 10 | 0 |
| **Summe** | **990** | **990** | **0** |

Holo-Offset und Umriss-Punkte bitgleich auf 98 von 98. Gegenprobe `main` gegen `main`: 990 identisch. Probe: Rack 1 der Hull A um +1 in x (nur `bin`-Kopie) ergibt 12 abweichende Bilder, alle Hull A, keine Zahl verändert; im Holo-Look 3.166 von 480.000 Pixeln, nur im Bereich des Racks.

**Abnahme:**
- [x] Korrektur-Nachweis: nur das Rack bewegt sich, Racks 2–4, Holo und Umriss samt Maßstab bleiben bitgleich (`ShipAnchorTests`)
- [x] Fit-Ziel: Umriss-Look +0,5 Zellen in x, Holo-Look unverändert
- [x] Jedes ausgelieferte Schiff trägt die Felder, keines mehr `holoOffset`
- [x] `-Force` behält die Felder ziffergenau, auch beim Neuschreiben; `-Create` setzt keine (rot gegen das alte Skript unter PS 5.1)
- [x] Dateien ohne Felder zeichnen wie bisher, eigene Dateien von Nutzern laufen weiter
- [x] Martin: Handling gegen `main` (Orbit, Zoom, Pan, Reset, Drag, Auswahl, Abhaken, Schiffswechsel, gespeicherte Kamera)
- [x] Martin: Gate über Star Citizen: Hull A, Aurora Mk II · TS module, GRIN UTV, Hull C, Carrack, Bild wie vorher, gespeicherte Kamera stimmt
- [x] Martin: Korrektur-Probe: Kopie einer Schiffsdatei mit verschobenem Rack, nur dieses Rack bewegt sich

**Nächste Schritte mit Auslöser:**
- **Neue Holo-Version:** `holoOrigin` gilt für holo-v1. `ShipAnchorValueTests` mit `CARGOPLAN_HOLO_PREVIOUS`, danach `ShipPictureTests`. Sonst wandert die Hülle um bis zu 0,6 Zellen (D90).
- **Erste Datenkorrektur durch einen Nutzer:** Origins auf 4 Stellen runden, Bildvergleich dann mit Toleranz statt bitgleich (`ShipPictureTests` kann das noch nicht).

### [x] F3 Freier Drehwinkel pro Rack (06.11.2026, abgenommen 06.11.)

**Warum:** Reddit (Ryekal): Hull B und Railen brauchen gedrehte Racks, sonst passen sie nie zum Holo. Vorgabe Martin: volle 3D-Rotation pro Rack, weil die Railen gekippt ist. Das Format kommt hier, die Werte in einer eigenen Daten-Session.

**Befund:**
- Alles Achsparallele hängt an `Rack` (`Box`, `FrameOf`) und `ShipBox`, ganzzahlig. Ein gedrehtes Rack lässt sich nicht als `ShipBox` ausdrücken, wohl aber als ein Schritt obendrauf: **Grid → gerade → Schiff.** Die Reihenfolge ist zwingend, 19 von 293 Racks haben ein linkshändiges `FrameOf` (`+y`, `-z`).
- **Fleetyards liefert nichts für die Hull B.** Lokaler Export 26.10.: `rotation` nur 90/270 auf 19 Holds (Hull C, Carrack, Caterpillar), bei Hull B, Railen, Prowler Utility, 890 Jump, Syulen kein Schlüssel. Die Hull C drückt es schon über `stackAxis` aus; zusätzlich geschrieben würde sie doppelt gedreht. Keine Werte in dieser Session.
- Die Mehrfach-Auswahl über Racks (`CrateMove.InShip`) ist auf geraden Boxen sauber, solange sie sich nicht überlappen. Tun sie es, können zwei Kisten auf einer Zelle landen: `MoveResolution` prüft nur gegen `taken`, und die Rückfallstufe griff nicht, weil `ResolveMove` `Shift` erneut ruft. Gefunden von einem Gegenprüf-Agent, im Code bestätigt.
- M2 misst fast nur Fehltreffer (23 von 600 Punkten), und `ShipPictureTests` zeichnete keine Kisten.

**Entscheidungen Martin:**
- Drehpunkt = Rack-Mitte, `origin` bleibt die Min-Ecke der ungedrehten Box.
- `origin` bleibt ganzzahlig. Das spätere Feld ist festgelegt, siehe unten.
- Kollisions-Schutz in Core.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
- Mehrfach-Auswahl: Versatz in geraden Koordinaten statt im Zielrack-Raum, ganzzahlig und exakt.
- Kollisions-Schutz sitzt in `CrateMove.InShip`: `Shift` liefert `null`, `CrateDrop` und `LoadPlanner` nehmen ohne eigenen Zweig die vorhandene Rückfallstufe (D65). `CrateDrop.cs` bleibt unverändert.
- Gate-Punkt Hull B entfällt mangels Werten, dafür Probedatei.
- Der Import schreibt kein `rotation`. `-Force` behält den Rohtext pro Rack-`id`; ein Schiff mit gedrehtem Rack wird in der Lage nicht verglichen und nicht neu gelegt. Das gilt auch für das Neulegen generierter Reihen bei geändertem Rack-Satz (`Update-Ship`), sonst träfe es die Railen.
- Module tragen kein `rotation`, Fehler beim Laden.
- Rack-Nummer auf langen Schiffen bleibt über dem Rack (D61), die größte Fläche gilt auf kompakten.
- `Rack` bekommt ein Datenfeld ohne Logik. Kein Spike. Zusätzlicher `Category=Timing`-Test für Treffer.
- `ShipPictureTests` bekommt 196 Bilder mit Ladung und einer ausgewählten Kiste, statt der 990 sind es jetzt 1186.

**Bildvergleich** (Baseline aus einem `main`-Worktree auf `00e0019`, Testdatei dorthin kopiert):

| Look | Bilder | Identisch | Abweichend |
|---|---|---|---|
| Umriss, 400×302 und 800×600 | 196 | 196 | 0 |
| None, 400×302 und 800×600 | 196 | 196 | 0 |
| Holo shaded, 400×302 und 800×600 | 196 | 196 | 0 |
| Holo flat, 400×302 und 800×600 | 196 | 196 | 0 |
| Holo hidden line, 400×302 und 800×600 | 196 | 196 | 0 |
| Gespeicherte Kamera (Umriss, Holo shaded), 800×600 | 10 | 10 | 0 |
| **Mit Ladung und Auswahl** (Umriss, Holo shaded), 800×600 | 196 | 196 | 0 |
| **Summe** | **1186** | **1186** | **0** |

Platzierungszahlen bitgleich auf 98 von 98. Gegenprobe `main` gegen `main`: 1186 identisch. Probe: Hull A, Rack 1 Pitch 40°, Rack 3 Yaw 30° (nur `bin`-Kopie) ergibt 14 abweichende Bilder, alle Hull A, keine Zahl verändert; im Bild kippt Rack 1 mit dem Bugende nach unten, Kisten, Kanten, Auswahlrahmen und Nummern folgen.

**Messung** (Publish-Release, `--measure-frames`, je 3 Läufe, Median, **ohne Spiel**; gedreht = jedes Rack der Hull C `[30, 20, 10]`, nur im Scratch):

| Ziel | `main` | Branch | Gedreht | Gate |
|---|---|---|---|---|
| Hit-Test M2 p95 (23 von 600 Treffer) | 0,001 ms | 0,002 ms | 0,002 ms | ≤ 1 ms |
| Hit-Test M2 max | 0,142 ms | 0,195 ms | 0,208 ms | |
| Vorschau `hull_c_drag` p95 | 1,87 ms | 1,98 ms | 1,91 ms | ≤ 16 ms |
| Vorschau max | 11,1 ms | 11,6 ms | 11,7 ms | |
| Späte Frames `hull_c` | 0,4 % | 0,5 % | 0,9 % | über dem Spiel am Gate |
| Späte Frames `hull_c_drag` | 12,8 % | 11,8 % | 12,4 % | über dem Spiel am Gate |

`TurnedRackTimingTests` (Strahlen auf Kisten, alle Treffer, 3.600 Strahlen): p95 0,026 ms gerade, 0,030 ms gedreht.

**Abnahme:**
- [x] Format: lesen, Default, `[0,0,0]`, falsche Länge, Schema 4/5 mit und ohne Feld, Schema 7 abgelehnt, Überlappung, Modul (`ShipLoaderTests`), erst rot gesehen
- [x] Planer weiß nichts: gleiche Boxen, gleiches Layout, gleicher `Shift` mit und ohne `rotation` (`RackRotationTests`)
- [x] Nie zwei Kisten auf einer Zelle; Vorschau und Drop gleich (`RackRotationTests`, `TurnedRackViewTests`)
- [x] Ecken eines um 45° gedrehten Racks auf den berechneten Punkten, je ein Vorzeichentest Yaw, Pitch, Roll, Reihenfolge (`RackTransformTests`)
- [x] Hit-Test über `CameraMath`: Treffer außerhalb der geraden Box, Fehltreffer innerhalb, erstes Rack auf dem Strahl über gerade und gedreht (`TurnedRackViewTests`)
- [x] Drop-Ziel im gedrehten Rack, Säule entlang der lokalen Stapelachse (`TurnedRackViewTests`)
- [x] Bounds und Fit mit gedrehten Ecken, Kistenmodell und Kanten liegen im gedrehten Rack (`TurnedRackViewTests`)
- [x] Alle 97 Schiffsdateien je eine Zeile `5`→`6`, Zeilenenden byte-genau; Abgleich gegen den echten Export: 97 same
- [x] `-Force` behält `rotation` ziffergenau; der Import schreibt keines (`ImportFleetyardsScriptTests`)
- [x] 1186 Bilder bitgleich, Hit-Test p95 ≤ 1 ms, Vorschau p95 ≤ 16 ms, auch gedreht
- [x] Martin: Handling gegen `main` auf ungedrehten Racks (Orbit, Zoom, Pan, Reset, Drag, Mehrfach-Auswahl, Legenden-Drag, Mausrad, Auswahl, Abhaken, Tooltip, Schiffswechsel, gespeicherte Kamera, Backdrop-Look)
- [x] Martin: Gate über Star Citizen: Hull A, RAFT, Hull C, Bild wie vorher, Drag und Abhaken wie gewohnt
- [x] Martin: Probedatei im `ships/`-Ordner des Builds: Racks liegen wie gewollt, Kisten lassen sich auf gedrehte Racks ziehen, Säule entlang der lokalen Achse, Mausrad dreht im Rack, Tooltip trifft die richtige Kiste, Rack-Nummer auf der größten Fläche

**Nächste Schritte mit Auslöser:**
- **Erlaubnis Motherlode oder Nutzer-Issue mit Winkeln** (aufgenommen in Session 4a, D92: sieben Schiffe statt fünf): Daten-Session für 890 Jump, Hull B, Prowler Utility, Railen, Syulen. Achsparallele 90/180/270-Drehungen aus externen Daten beim Import normalisieren (Maße tauschen, `origin` versetzen, `stackAxis`), nicht als `rotation` schreiben; `rotation` nur für echte Schrägwinkel. Die Quelle gegen Fleetyards prüfen: sie nennt andere Schiffe.
- **Daten-Session misst mehr als ±0,5 Zellen:** optionales Rack-Feld `shift` `[x, y, z]` in Zellen mit Kommastellen, nur in `RackTransform` nach der Drehung angewandt, Core bleibt ganzzahlig (Muster `holoOrigin`). Ohne Schema-Sprung möglich, wie die F7-Felder.
- **Ein Schiff kippt durch seine Rotation über die 2:1-Grenze** (`IsLong`): Kamera und Label-Stil prüfen.

### [x] Session 4a – Racks aus den Spieldateien (07.11.2026, abgenommen 07.11.)

**Branch:** `feat/ship-data-game`. **Ziel (Martin):** SCU und Rack-Anordnung stimmen für alle Schiffe (Ryekals Kritik: Fleetyards hat Lagen für 7 Schiffe, 88 hatten generierte Reihen). Quelle ist der Cargo-Katalog von cryon.rocks (Motherlode), aus den Spieldateien, mit Erlaubnis der Betreiber. Session 4 ist dreigeteilt: 4a Racks (hier), 4b Hülle und Umriss, 4c Stilllegung Fleetyards (D92).

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:**
- Der Outline-Ordner ist `Documents\Coding\Dotnet\sc-ship-outlines`, nicht `~/source`.
- `transform` ist eine 3×4-Matrix zeilenweise `[R|t]`, nicht 3×3 plus Translation. Die Bodenflächen-Annahme ist im Viewer-Code belegt (`CargoScene`).
- **Hull A:** Es gilt die Spielgeometrie, nicht „exakt die heutigen Origins bis auf Verschiebung“.
  - Gleich bleiben Seite, Ebene, Reihenfolge, `stackAxis`, Maße und Längslage.
  - Der Plattenabstand ist 5 statt 3 Zellen. Das Modell trägt `cargoPose`, der Fleetyards-Wert war nie gemessen.
- **`rotation`:** 7 Bestandsschiffe statt 5, Schwelle 1°.
- **Türlimit:** `maxCrateSize = min(maxContainerScu, Handwert)`.
  - `reportedMaxSize` wird nicht gelesen, die API liefert es nicht.
  - `minCrateSize` bleibt, wenn der Katalog keine nennt.
- **Holo und Umriss** nicht in 4a, die Anker bleiben auf den F7-Werten. Box-Regel und Korrelationsskript entfallen; 4b baut die Hülle aus den Motherlode-Modellen.
- **Nullpunkt:** Modellursprung plus Bruchteil s pro Schiff und Achse (größte Rundung minimal). 4b setzt `holoOrigin = s`. `ts_module` wandert mit Rack 1 der Aurora Mk II.
- **Schiffsliste:**
  - Die 8 neuen Schiffe kommen dazu. Die Varianten tragen `fleetyardsSlug` und Anker des Basisschiffs, die Cyclone bekommt den Keil.
  - 135c, 600i EE und F7C Mk II bleiben Fleetyards.
- **Eingabeformat:** die Feldnamen der Motherlode-API.
  - `fetch-motherlode.ps1 -GameBuild` liefert den Build, `className` kommt aus dem Modell-Dateinamen.
  - Neuer Parameter `-Previous`: gleiche Grids in anderer Reihenfolge werden nicht geschrieben.
- **K1:** Überlappung führt zur Handprüfung. **E1:** Tabelle der Stapelachsen. **E2:** Material für die Release-Notes.
- **Exit-Codes:** 0 nach dem Schreiben, wie `import-fleetyards.ps1`; 1 nur bei „ansehen“.
- **Tests:** Mechanik-Tests laufen auf eingefrorenen Kopien (`tests/CargoPlan.Core.Tests/FrozenShips`), Datentests auf `ships/`.
  - 70 Tests hingen an den ausgelieferten Schiffen; Testcode stand nicht im Umfang, Martin hat die Kopien gewählt.
  - Lehre als A10 in CLAUDE.md.
- **Fixtures:** Die Konverter-Tests nutzen einen synthetischen Katalog. Nichts aus dem echten Katalog liegt im Repo.
- **`ShipPictureTests`:** neue Draufsicht („above“, 85°) für den Kontaktabzug; Schiffe ohne Holo werden nur ohne Holo-Looks gerendert.
- **Hull-A-Notiz:** einmal von Hand angepasst.
- **Nexus-Texte** bleiben beim 0.2.0-Upload (T9.3); der Motherlode-Credit kommt mit dem 0.3.0-Upload.

**Befund** (Katalog 2026-11-07, Build `4.10.2-LIVE.12913470`, SHA-256 `9f902a16…`, im Outline-Ordner unter `motherlode/`):
- **Katalog:** 103 Einträge, davon 6 Verweise:
  - MOLE und Mole Talus → Mole Carbon
  - Carrack w/C8X → Carrack
  - Carrack Expedition w/C8X → Carrack Expedition
  - Mustang Alpha Vindicator → Mustang Alpha
  - Valkyrie Liberator → Valkyrie
- **Modelle:** 96 `.glb` (47,5 MB), nur vermessen, nie ausgeliefert.
- **Abgleich mit `ships/`**, 95 gemeinsame Schiffe:

| gleich (SCU, Grid-Maße) | nur Lage anders | Maße oder SCU anders |
|---|---|---|
| 95 | 95 (alle, vorher generierte Reihen oder Fleetyards-Offsets) | 0 – die Spielstand-Frage entfällt für die Maße |

- **Abgleich mit Fleetyards:**
  - Nur im Katalog: Mole Carbon/Talus, Carrack Expedition, Carrack Expedition w/C8X, Carrack w/C8X, Cyclone, Mustang Alpha Vindicator, Valkyrie Liberator. Keines hat ein eigenes Holo.
  - Nur in `ships/`: 600i Executive Edition, F7C Hornet Mk II.
- **Für 4b:** Die Box der Motherlode-Modelle passt bei 32 von 94 Schiffen auf ≤ 0,5 m zum Pipeline-Mesh (≤ 0,25 m: 20, ≤ 1 m: 51, ≤ 2 m: 68).
  - Ausreißer: Corsair (B −31 m), Hull B (L +25 m, `cargoPose`), Reliant (Flügel), Aurora Mk II (B −16,9 m), Hull C (B/H −8,7 m), Hull A (L +6 m, `cargoPose`).
  - Fahrwerk: H oft 1,5–4 m anders.

**Hull-A-Kalibrierung** (echter Katalog, s = [0,188, 0,505, 0,136]):

| Rack | Lage | `origin` vorher → nachher | `stackAxis` | Rundung je Achse (Zellen) | Summe |
|---|---|---|---|---|---|
| rack-0 | Backbord oben | [0, 1, 3] → [−9, 3, 1] | +y | [−0,01, −0,15, −0,13] | 0,29 (0,36 m) |
| rack-1 | Backbord unten | [0, 1, 0] → [−9, 3, −2] | +y | [−0,01, −0,15, 0,14] | 0,29 (0,37 m) |
| rack-2 | Steuerbord oben | [0, −4, 3] → [−9, −4, 1] | −y | [0,01, 0,15, −0,14] | 0,29 (0,37 m) |
| rack-3 | Steuerbord unten | [0, −4, 0] → [−9, −4, −2] | −y | [0,00, 0,15, 0,13] | 0,28 (0,36 m) |

- Plattenabstand: im Spiel 5,88 m = 4,71 Zellen, gerundet 5 (Datei vorher 3, Fleetyards).
- Höhenabstand: 0,71 Zellen, gerundet 1 wie vorher.
- Grid 4×2×2, Längsachse und Reihenfolge gleich.

**Konverter-Lauf:**
- 94 Schiffe aktualisiert, 8 neu.
- 135c bleibt (Grids ohne `transform`). 600i EE und F7C Mk II bleiben (nicht im Katalog).
- 0 Handprüfungen (K1-Tabelle leer, auch der Reclaimer überlappt gerundet nicht), 0 Reihenfolge-Warnungen.
- RAFT ergibt −z. ✓
- Zweiter Lauf: 102 `same`, alle Dateien byte-gleich.
- Gesamt-SCU = Katalog `cargoScu` bei allen 102 Schiffen; der Konverter prüft das vor dem Schreiben.
- **Kisten:** nur die Corsair ändert sich (`maxCrateSize` 32 → 24).

**E1 – Stapelachse aus dem Spiel, 16 Schiffe** (nicht 17: meine Zählung im Befund war falsch, Hull A, Hull B und RAFT stimmten schon).
- Herleitung: Spalte 2 von R, abgebildet mit (y, −x, z), Vorzeichen der größten Komponente.
- Re-plan ausdrücklich empfehlen (E2).

| Schiff | Rack: alt → neu | Auffällig |
|---|---|---|
| aurora_mk_i_cl | rack-0 +z→-z | hängt unter dem Rumpf (−z) |
| aurora_mk_i_es | rack-0 +z→-z | hängt unter dem Rumpf (−z) |
| aurora_mk_i_ln | rack-0 +z→-z | hängt unter dem Rumpf (−z) |
| aurora_mk_i_lx | rack-0 +z→-z | hängt unter dem Rumpf (−z) |
| aurora_mk_i_mr | rack-0 +z→-z | hängt unter dem Rumpf (−z) |
| aurora_mk_i_se | rack-0 +z→-z | hängt unter dem Rumpf (−z) |
| fortune | rack-1 +z→+y; rack-2 +z→-y | Seitenracks waagerecht (±y) |
| golem_ox | rack-0 +z→-z | stapelt nach unten (−z) |
| hull_c | rack-0 +z→+x; rack-1 +z→+x; rack-2 +y→+x; rack-3 +y→+x; rack-4 +z→-x; rack-5 +z→-x; rack-6 +y→-x; rack-7 +y→-x; rack-8 +z→+x; rack-9 +z→+x; rack-10 +y→+x; rack-11 +y→+x; rack-12 +z→-x; rack-13 +z→-x; rack-14 +y→-x; rack-15 +y→-x | Innen-Bodengitter mit waagerechter Achse: stapelt entlang der Spindel (±x) |
| m80 | rack-0 +z→-x | waagerechte Achse (−x), 40° gekippt |
| moth | rack-1 +z→+y; rack-2 +z→-y | Seitenracks waagerecht (±y) |
| mpuv_tractor | rack-0 +z→-z | stapelt nach unten (−z) |
| prowler_utility | rack-0 +z→-z; rack-1 +z→-z | stapelt nach unten (−z) |
| railen | rack-0 +z→-z; rack-1 +z→+y; rack-2 +z→-z; rack-3 +z→-z; rack-4 +z→-y; rack-5 +z→-z | zwei Racks waagerecht (±y), Rest −z |
| syulen | rack-0 +z→-y; rack-2 +z→+y; rack-3 +z→-y; rack-5 +z→+y | vier Racks waagerecht (±y) |
| tyilui | rack-0 +z→-z | stapelt nach unten (−z) |

**Drehungen (D91), 36 Racks in 9 Schiffen** (Schwelle 1°; Hull A mit 0,6° gerade gestellt):

| Schiff | Rack | `rotation` [yaw, pitch, roll] | Abweichung |
|---|---|---|---|
| 890_jump | rack-2 | [-15, 0, 0] | 15,0° |
| 890_jump | rack-3 | [15, 0, 0] | 15,0° |
| 890_jump | rack-5 | [-15, 0, 0] | 15,0° |
| 890_jump | rack-6 | [15, 0, 0] | 15,0° |
| cyclone | rack-0 | [0, -15, 0] | 15,0° |
| hull_b | rack-0 | [-0,05, 0,03, -42,47] | 42,5° |
| hull_b | rack-1 | [-0,05, 0,03, -42,86] | 42,9° |
| hull_b | rack-2 | [0,02, 0,03, 42,87] | 42,9° |
| hull_b | rack-3 | [0,02, 0,03, 42,47] | 42,5° |
| hull_b | rack-4 | [-0,01, -0,01, -42,47] | 42,5° |
| hull_b | rack-5 | [-0,01, -0,01, -42,86] | 42,9° |
| hull_b | rack-6 | [-0,02, -0,01, 42,87] | 42,9° |
| hull_b | rack-7 | [-0,02, -0,01, 42,47] | 42,5° |
| hull_b | rack-8 | [-0,02, -0,01, 42,47] | 42,5° |
| hull_b | rack-9 | [-0,02, -0,01, 42,87] | 42,9° |
| hull_b | rack-10 | [-0,01, -0,01, -42,86] | 42,9° |
| hull_b | rack-11 | [-0,01, -0,01, -42,47] | 42,5° |
| hull_b | rack-12 | [0,02, 0,03, 42,47] | 42,5° |
| hull_b | rack-13 | [0,01, 0,03, 42,87] | 42,9° |
| hull_b | rack-14 | [-0,05, 0,03, -42,86] | 42,9° |
| hull_b | rack-15 | [-0,05, 0,03, -42,47] | 42,5° |
| m80 | rack-0 | [0, 40, 0] | 40,0° |
| mustang_alpha | rack-0 | [0, 16,38, 0] | 16,4° |
| mustang_alpha_vindicator | rack-0 | [0, 16,38, 0] | 16,4° |
| prowler_utility | rack-0 | [0, 0, -14] | 14,0° |
| prowler_utility | rack-1 | [0, 0, 14] | 14,0° |
| railen | rack-1 | [3,84, 40,22, -2,94] | 40,4° |
| railen | rack-3 | [0, 40, 0] | 40,0° |
| railen | rack-4 | [-3,84, 40,22, 2,84] | 40,4° |
| railen | rack-5 | [0, 40, 0] | 40,0° |
| syulen | rack-0 | [4,33, -2,5, 29,91] | 30,4° |
| syulen | rack-1 | [0, 5, 0] | 5,0° |
| syulen | rack-2 | [-4,33, -2,5, -29,91] | 30,4° |
| syulen | rack-3 | [4,33, -2,5, 29,91] | 30,4° |
| syulen | rack-4 | [0, 5, 0] | 5,0° |
| syulen | rack-5 | [-4,33, -2,5, -29,91] | 30,4° |

**Rundungsprotokoll:** 315 Racks in 102 Schiffen (mit den Varianten), **171 über 0,25 Zellen (Summe der Achsen) in 27 Schiffen**, pro Achse höchstens 0,40 Zellen; jede Zeile des Reports nennt alle drei Achsen in Zellen und Metern. Racks über 0,25:

| Schiff | Racks > 0,25 / alle | schlimmstes Rack | Summe Zellen | Meter |
|---|---|---|---|---|
| hull_c | 16 / 16 | rack-14 [0,26, 0,33, −0,33] | 0,93 | 1,16 |
| idris_p | 24 / 25 | rack-18 [−0,40, −0,34, −0,10] | 0,84 | 1,05 |
| 890_jump | 7 / 7 | rack-4 [−0,25, 0,38, 0,14] | 0,77 | 0,96 |
| idris_m | 21 / 21 | rack-12 [0,30, −0,34, −0,10] | 0,74 | 0,93 |
| ironclad | 9 / 9 | rack-2 [−0,20, 0,35, −0,18] | 0,73 | 0,91 |
| 600i_explorer | 6 / 7 | rack-4 [−0,25, 0,34, 0,07] | 0,66 | 0,83 |
| railen | 6 / 6 | rack-3 [−0,16, 0,21, −0,24] | 0,61 | 0,77 |
| moth | 3 / 3 | rack-1 [0,03, 0,29, −0,24] | 0,55 | 0,69 |
| fortune | 3 / 3 | rack-1 [0,12, 0,31, −0,11] | 0,54 | 0,67 |
| reclaimer | 10 / 12 | rack-4 [0,27, −0,20, −0,06] | 0,53 | 0,66 |
| syulen | 4 / 6 | rack-5 [−0,02, −0,27, −0,20] | 0,49 | 0,61 |
| hull_b | 16 / 16 | rack-14 [0,19, −0,09, −0,21] | 0,49 | 0,61 |
| freelancer_dur | 2 / 3 | rack-0 [0,11, 0,31, 0,02] | 0,44 | 0,55 |
| freelancer | 2 / 3 | rack-0 [0,11, 0,31, 0,02] | 0,44 | 0,55 |
| freelancer_mis | 2 / 3 | rack-0 [0,11, 0,31, 0,02] | 0,44 | 0,55 |
| freelancer_max | 2 / 3 | rack-0 [0,11, 0,31, 0,02] | 0,44 | 0,55 |
| mercury_star_runner | 2 / 2 | rack-1 [0,20, 0,18, 0,00] | 0,38 | 0,47 |
| starlancer_max | 4 / 4 | rack-2 [−0,11, 0,10, 0,16] | 0,38 | 0,47 |
| 600i_touring | 4 / 4 | rack-2 [−0,11, 0,25, 0,00] | 0,36 | 0,45 |
| cutlass_black | 2 / 2 | rack-1 [−0,06, −0,25, 0,00] | 0,31 | 0,39 |
| carrack_expedition_w_c8x | 3 / 9 | rack-8 [0,10, −0,20, 0,00] | 0,30 | 0,37 |
| carrack | 3 / 9 | rack-8 [0,10, −0,20, 0,00] | 0,30 | 0,37 |
| carrack_w_c8x | 3 / 9 | rack-8 [0,10, −0,20, 0,00] | 0,30 | 0,37 |
| carrack_expedition | 3 / 9 | rack-8 [0,10, −0,20, 0,00] | 0,30 | 0,37 |
| hull_a | 4 / 4 | rack-2 [0,01, 0,15, −0,14] | 0,29 | 0,37 |
| starfarer_gemini | 5 / 5 | rack-4 [−0,02, 0,25, 0,00] | 0,27 | 0,34 |
| starfarer | 5 / 5 | rack-4 [−0,02, 0,25, 0,00] | 0,27 | 0,34 |

<details><summary>Bruchteil s pro Schiff (Eingabe für 4b: holoOrigin = s)</summary>

| Schiff | s [x, y, z] Zellen |
|---|---|
| 100i | [0,264, 0,500, 0,798] |
| 125a | [0,264, 0,500, 0,798] |
| 300i | [0,531, 0,000, 0,334] |
| 315p | [0,545, 0,000, 0,332] |
| 325a | [0,559, 0,000, 0,330] |
| 350r | [0,559, 0,000, 0,330] |
| 400i | [0,350, 0,500, 0,000] |
| 600i_explorer | [0,764, 0,910, 0,290] |
| 600i_touring | [0,626, 0,500, 0,362] |
| 890_jump | [0,561, 0,625, 0,242] |
| a2_hercules | [0,800, 0,000, 0,100] |
| apollo_medivac | [0,412, 0,494, 0,080] |
| apollo_triage | [0,412, 0,494, 0,080] |
| asgard | [0,050, 0,500, 0,000] |
| aurora_mk_i_cl | [0,800, 0,500, 0,458] |
| aurora_mk_i_es | [0,800, 0,500, 0,458] |
| aurora_mk_i_ln | [0,800, 0,500, 0,458] |
| aurora_mk_i_lx | [0,800, 0,500, 0,458] |
| aurora_mk_i_mr | [0,800, 0,500, 0,458] |
| aurora_mk_i_se | [0,800, 0,500, 0,458] |
| aurora_mk_ii | [0,455, 0,000, 0,915] |
| avenger_titan | [0,080, 0,000, 0,771] |
| avenger_titan_renegade | [0,080, 0,000, 0,771] |
| c1_spirit | [0,372, 0,000, 0,400] |
| c2_hercules | [0,400, 0,000, 0,075] |
| c8_pisces | [0,550, 0,500, 0,600] |
| c8x_pisces_expedition | [0,550, 0,500, 0,600] |
| carrack | [0,598, 0,000, 0,000] |
| carrack_expedition | [0,598, 0,000, 0,000] |
| carrack_expedition_w_c8x | [0,598, 0,000, 0,000] |
| carrack_w_c8x | [0,598, 0,000, 0,000] |
| caterpillar | [0,958, 0,493, 0,170] |
| clipper | [0,400, 0,400, 0,800] |
| constellation_mk_iv_andromeda | [0,994, 0,000, 0,560] |
| constellation_mk_iv_aquila | [0,994, 0,000, 0,560] |
| constellation_mk_iv_phoenix | [0,200, 0,500, 0,563] |
| constellation_mk_iv_taurus | [0,750, 0,000, 0,361] |
| constellation_mk_v_centaurus | [0,790, 0,995, 0,759] |
| corsair | [0,300, 1,000, 0,360] |
| csv_sm | [0,384, 0,000, 0,790] |
| cutlass_black | [0,164, 0,242, 0,597] |
| cutlass_blue | [0,372, 0,500, 0,644] |
| cutlass_red | [0,371, 0,500, 0,644] |
| cutter | [0,850, 0,700, 0,200] |
| cutter_rambler | [0,348, 0,660, 0,200] |
| cutter_scout | [0,780, 0,220, 0,200] |
| cyclone | [0,457, 0,500, 0,178] |
| fortune | [0,896, 0,694, 0,754] |
| freelancer | [0,312, 0,688, 0,180] |
| freelancer_dur | [0,312, 0,688, 0,180] |
| freelancer_max | [0,312, 0,688, 0,180] |
| freelancer_mis | [0,312, 0,688, 0,180] |
| golem_ox | [0,152, 0,000, 0,744] |
| hammerhead | [0,575, 0,413, 0,624] |
| hermes | [0,200, 0,000, 0,056] |
| hull_a | [0,188, 0,505, 0,136] |
| hull_b | [0,412, 0,500, 0,983] |
| hull_c | [0,642, 0,333, 0,948] |
| idris_m | [0,700, 0,260, 0,300] |
| idris_p | [0,600, 0,260, 0,300] |
| intrepid | [0,057, 0,000, 0,895] |
| ironclad | [0,500, 0,150, 0,680] |
| ironclad_assault | [0,300, 0,000, 0,860] |
| m2_hercules | [0,400, 0,000, 0,075] |
| m80 | [0,770, 0,500, 0,807] |
| mercury_star_runner | [0,100, 0,076, 0,200] |
| mole | [0,975, 0,000, 0,223] |
| mole_carbon_edition | [0,975, 0,000, 0,223] |
| mole_talus_edition | [0,975, 0,000, 0,223] |
| moth | [0,947, 0,713, 0,458] |
| mpuv_cargo | [0,000, 0,500, 0,408] |
| mpuv_tractor | [0,701, 0,000, 0,741] |
| mule | [0,391, 0,500, 0,844] |
| mustang_alpha | [0,534, 0,500, 0,474] |
| mustang_alpha_vindicator | [0,534, 0,500, 0,474] |
| nomad | [0,200, 0,500, 0,444] |
| paladin | [0,400, 0,980, 0,360] |
| perseus | [0,200, 0,000, 0,799] |
| polaris | [0,400, 0,000, 0,801] |
| prowler_utility | [0,956, 0,500, 0,558] |
| raft | [0,000, 0,000, 0,325] |
| railen | [0,845, 0,014, 0,880] |
| reclaimer | [0,370, 0,000, 0,940] |
| redeemer | [0,200, 0,660, 0,960] |
| reliant_kore | [0,251, 0,500, 0,810] |
| reliant_tana | [0,240, 0,683, 0,827] |
| salvation | [0,125, 0,000, 0,812] |
| shiv | [0,969, 0,542, 0,629] |
| srv | [0,000, 0,000, 0,768] |
| starfarer | [0,518, 0,250, 0,295] |
| starfarer_gemini | [0,518, 0,250, 0,295] |
| starlancer_max | [0,287, 0,500, 0,170] |
| starlancer_tac | [0,400, 0,500, 0,007] |
| syulen | [0,055, 0,502, 0,997] |
| tiburon | [0,575, 0,413, 0,624] |
| tyilui | [0,000, 0,000, 0,500] |
| utv | [0,677, 0,497, 0,953] |
| valkyrie | [0,125, 0,500, 0,011] |
| valkyrie_liberator_edition | [0,125, 0,500, 0,011] |
| vulture | [0,624, 0,476, 0,116] |
| zeus_mk_ii_cl | [0,650, 0,500, 0,000] |
| zeus_mk_ii_es | [0,000, 0,000, 0,000] |

</details>

**Bildvergleich** (ShipPictureTests, Baseline main 061dfb3 mit derselben Harness-Datei, alle Schiffe in EXPECTED; Befund, kein Gate): **65 gleich, 1316 anders, 0 fehlen**. Unverändert sind genau 135c, 600i EE und F7C Mk II. Holo- und Umriss-Platzierung sind bei allen 95 Bestandsschiffen bitgleich (die Anker sind unverändert); neue Zahlen gibt es nur bei den 8 neuen Schiffen. 
one bleibt bei einigen kleinen Schiffen gleich, weil die Kamera auf die Racks passt und sich deren Lage zueinander nicht ändert.

| Look | Picture set | Identical | Different |
|---|---|---|---|
| holo-flat | 400x302 fit | 3 | 102 |
| holo-flat | 800x600 fit | 3 | 102 |
| holo-hiddenline | 400x302 fit | 3 | 102 |
| holo-hiddenline | 800x600 fit | 3 | 102 |
| holo-shaded | 400x302 fit | 3 | 102 |
| holo-shaded | 800x600 above | 3 | 102 |
| holo-shaded | 800x600 cargo | 3 | 102 |
| holo-shaded | 800x600 fit | 3 | 102 |
| holo-shaded | 800x600 saved | 0 | 5 |
| none | 400x302 fit | 20 | 86 |
| none | 800x600 fit | 12 | 94 |
| outline | 400x302 fit | 3 | 103 |
| outline | 800x600 above | 0 | 1 |
| outline | 800x600 cargo | 3 | 103 |
| outline | 800x600 fit | 3 | 103 |
| outline | 800x600 saved | 0 | 5 |

| Racks verschoben um (größte Origin-Änderung, Zellen) | Schiffe | Anzahl |
|---|---|---|
| 0–2 | c8x_pisces_expedition (2), c8_pisces (2), aurora_mk_i_mr (2), csv_sm (2), srv (2), redeemer (2), cutter_scout (2), 300i (2), 315p (2), 100i (2), 125a (2), aurora_mk_i_lx (2), aurora_mk_i_ln (2), aurora_mk_i_es (2), aurora_mk_ii (1), utv (1), mpuv_cargo (1), mpuv_tractor (1), mule (1) | 19 |
| 3–5 | aurora_mk_i_se (5), c1_spirit (5), m80 (5), golem_ox (5), aurora_mk_i_cl (5), mustang_alpha (4), perseus (4), paladin (4), reliant_kore (4), reliant_tana (4), clipper (4), 350r (4), intrepid (4), 325a (4), salvation (3), cutter_rambler (3), cutter (3), prowler_utility (3) | 18 |
| 6–10 | 600i_touring (10), hull_a (9), cutlass_red (9), cutlass_blue (9), valkyrie (9), nomad (9), mole (9), shiv (8), a2_hercules (8), constellation_mk_v_centaurus (8), zeus_mk_ii_cl (7), fortune (7), constellation_mk_iv_andromeda (7), constellation_mk_iv_phoenix (7), constellation_mk_iv_aquila (7), zeus_mk_ii_es (6), syulen (6), cutlass_black (6), avenger_titan_renegade (6), avenger_titan (6) | 20 |
| 11–20 | starfarer_gemini (20), starfarer (20), c2_hercules (19), m2_hercules (19), starlancer_max (18), starlancer_tac (18), moth (16), corsair (16), 400i (15), apollo_triage (14), apollo_medivac (14), mercury_star_runner (13), raft (13), polaris (12), freelancer_dur (12), freelancer (12), freelancer_max (12), hermes (12), freelancer_mis (12), vulture (11), asgard (11) | 21 |
| 21–50 | hull_c (47), reclaimer (44), 600i_explorer (35), hull_b (29), carrack (28), tyilui (27), ironclad_assault (27), constellation_mk_iv_taurus (24), hammerhead (23), tiburon (23), railen (22) | 11 |
| 51–200 | idris_p (128), idris_m (97), ironclad (91), caterpillar (78), 890_jump (62) | 5 |

**Kontaktabzug** (Martins Sichtprüfung): Documents\Coding\Dotnet\sc-ship-outlines\preview\motherlode\_sheet1.png … _sheet9.png, 106 Einträge, je Ansicht Standard (schräg) und von oben (85°), Holo schattiert mit Racks (motherlode_sheet.py). In 4a liegen die Hüllen noch mit F7-Ankern, Fehllagen der Hülle sind also erwartet (Hull A: Racks hinter dem Rumpf, Ladezustand der Spindel).

**Abnahme:**
- [x] Holen: `fetch-motherlode.ps1` schreibt Katalog und SHA-256 stabil (zwei Läufe, gleicher Hash), 6 Verweise aufgelöst, 96 Modelle mit Hashliste
- [x] Konverter-Tests erst rot (17), dann grün; Fleetyards-Tests nach dem Umzug nach `ShipJson.ps1` unverändert grün, dazu 2 Tests für den Guard
- [x] Hull A: Seite, Ebene, Reihenfolge, `stackAxis` ±y, Maße, Plattenabstand 5 (Test und echter Lauf)
- [x] `rotation` genau bei den 7 Bestandsschiffen (dazu Vindicator und Cyclone neu), RAFT −z
- [x] Gesamt-SCU = Katalog bei allen Schiffen, keine Überlappung, zweiter Lauf ändert nichts
- [x] `dotnet build` ohne Warnung, `dotnet test` grün (Import 141, App 1621, Core 1811), `Category=Slow` grün
- [x] Martin (07.11.): Kontaktabzug passt – kein Rack außerhalb der Hülle *im Rahmen des Erwartbaren* (die Holos liegen in 4a noch mit F7-Ankern, Fehllagen der Hülle sind erwartet), keine offensichtlich falsche Seite; keine auffälligen Schiffe gemeldet
- [x] Martin (07.11.): Hull B, Railen, 890 Jump – alle Racks zueinander plausibel
- [x] Martin am Aufzug (07.11.): Hull A, RAFT, Aurora Mk II + TS module – Lage und Stapelrichtung wie im Spiel

**Offen:**
- **4b Hülle und Umriss:** Plan nach Martins Vorgabe (Pipeline liest Motherlode-`.glb`, ohne Neu-Zentrierung und Skalierung, holo-v2 mit eigener NOTICE). Dort gilt `holoOrigin` = s aus der Tabelle oben, nicht 0. Der Plan nennt vorab: glTF-Format, Dreieckszahlen an Hull A, Hull C und Reclaimer, Asset-Größe, Aufwand.
- **4c Stilllegung:** wie vorgegeben. Der Konverter ist schon quellenneutral, die Katalogarchivierung mit Datum und SHA-256 macht `fetch-motherlode.ps1` bereits.
- **A2-Rück-Sweep (eigener Mini-Task):** Die übrigen Tests, die noch aus `ships/` laden, stellen auf `FrozenShips` um. Etwa 60 Stellen in App.Tests (`ShipLoader.Load("hull_a")` u. a.) und die Planer-Tests in Core.Tests; heute grün, weil sie an keiner Koordinate hängen.
- **Spielstand:** Der Katalog nennt `4.10.2-LIVE.12913470`; spätere Kataloge per `-GameBuild`, bis die API ihn liefert. Katalog und Modell-Hashes liegen im privaten Outline-Repo (Martin, 07.11.: committen), `motherlode/` byte-genau (`-text`), damit der SHA-256 auf jedem Checkout gilt.
- **Rundung:** 171 Racks in 27 Schiffen über 0,25 Zellen (Summe), pro Achse höchstens 0,40.
  - Der `shift`-Auslöser aus D91 (±0,5) greift damit nicht.
  - Neuer Auslöser: Ein Spieler meldet, dass ein Rack sichtbar daneben liegt. Dann kommt `shift` (Feld ist entschieden).
- **Hull-Pose:** Die Grids von Hull A, B und C liegen im Ladezustand der Spindel, die Fleetyards-Holos im Flugzustand. Das passt erst in 4b.
- **Nexus-Texte beim 0.3.0-Upload:** Credit „cargo grid data from cryon.rocks (Motherlode)“, 105 Schiffe, der Satz „Ship data are drafts“ neu (`docs/nexus/description.bbcode` Zeilen 18, 52, 65, 68).
- **Rückmeldung an Sciirof (K1):** Die Tabelle „Handprüfung nötig“ ist leer, nach dem Runden überlappt kein Schiff, auch der Reclaimer nicht.

**Material für Release-Notes v0.3.0 und die Antwort an Ryekal:**
- Rack layouts for all ships now come from the game files (cargo grid data from cryon.rocks (Motherlode)).
- Rack positions changed for most ships – your saved plan still loads; re-plan once if crate placement looks different. Re-plan for sure on: Aurora Mk I (all six), Fortune, Golem OX, Hull C, M80, MOTH, MPUV Tractor, Prowler Utility, Railen, Syulen, Tyilui – their racks now stack the way the game does.
- New ships: Carrack Expedition, Carrack (Expedition) w/C8X, MOLE Carbon/Talus Edition, Mustang Alpha Vindicator, Valkyrie Liberator Edition, Cyclone.
- Turned racks where the game turns them: Hull B, Railen, Prowler Utility, 890 Jump hangar, Syulen, M80, Mustang Alpha.

### [x] Session 4b – Hülle und Umriss aus den Motherlode-Modellen (07.11.2026, abgenommen 08.11.)

**Branch:** `feat/holo-v2` (App-Repo), `holo-v2` (Outline-Repo). **Ziel (Martin):** Hülle, Umriss und Racks kommen aus demselben Modell, die Anker sind danach überall `s`. Ergebnis ist das Release-Asset `holo-v2` mit eigener NOTICE, `holo-v1` bleibt für v0.2.0 (D93).

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:**
- **R1:** `outlineOrigin = [s.x, s.y, s.z + floorM/1,25]`, der Umriss liegt auf dem Boden der Modell-Box (T8.5), kein `outlineScale`.
- **R2:** Der Umriss aus dem Modell (2D-Polygon) liegt wie bisher in `outlines/` im Repo, mit neuer NOTICE. Meshes und `.glb` nie.
- **R3:** Die Cyclone bekommt `fleetyardsSlug: "tmbl-cyclone"` und damit Holo und Umriss aus ihrem Modell.
- **R4:** Neues Messziel `reclaimer` in `FrameMeasureWindow` (eigener Commit). Der `main`-Vergleichs-Build bekommt dieselbe Zeile in einem Scratch-Worktree.
- Die Anker schreibt der Konverter, immer an, ohne Schalter (`-Outlines`, `-ModelMap`).
- A2 umfasst auch eingefrorene Umrisse (`FrozenOutlines`) und war mit 145 Stellen in 82 Dateien größer als „≈ 60“. `ShipOutlineLoader.Load(path)` gab es schon, die Core-Testnaht entfiel.
- Der verwaiste Cache `holo\holo-v1\` bleibt, Aufräumen in 4c.
- Pipeline pro Modell (`className`), Slug erst beim Release. Ein Slug mit zwei Modellen nimmt das der kleinsten id: nur `anvl-carrack` (Carrack vs. Expedition, 24 Byte Unterschied).
- **Umriss mit adaptivem Schließen:** 0,1 → 1,6 m, bis die größte Kontur ≥ 99 % der Draufsicht hält. Anlass Hull A: Bug und Heck hängen in der Draufsicht nur am Spindelgitter, bei 0,1 m fehlte das Heck (Anteil 0,753).

**E1 – Schwellen des `fit`-Gates, festgelegt vor dem Lauf über alle 102** (Referenz: Hull A, RAFT, Aurora Mk II, am Aufzug geprüft; Holo und Umriss aus dem Modell, Racks aus `ships/` mit s aus der Map):

| Kennzahl | Hull A | RAFT | Aurora Mk II | Schwelle → Liste |
|---|---|---|---|---|
| (a) Rack-Ecken in der Modell-Box | 0,375 (Racks außen an der Spindel) | 1,000 | 1,000 | < 1,000 |
| (b) Eindringen: Hüllfläche in der Rack-Box (je Seite um min(0,5 m; 20 %) verkleinert) ÷ Box-Oberfläche, schlimmstes Rack | 0,0000 | 0,0000 | 0,0000 | > 0,05 (Regel: max(0,05; 2 × Referenzmaximum)) |
| (c) IoU Umriss ↔ Holo-Draufsicht | 0,9636 | 0,9212 | 0,9539 | < 0,90 (Regel: Referenzminimum − 0,02) |

Zum Lesen: Eine Wand quer durch die Mitte eines Racks ergibt (b) ≈ 0,17. Bei (c) liegt die RAFT am niedrigsten, weil ihr Gitterrahmen im Holo durchbrochen, im Umriss geschlossen ist. (a) < 1 ist bei Schiffen mit Außenfracht erwartet (Hull A) und kein Fehler, nur ein Grund zum Hinsehen.

**Befund** (Katalog 2026-11-07, 96 Modelle, 49,8 MB):
- **Format:** roh (kein Draco/meshopt), je 1 Node ohne Transform, 1 Mesh, nur POSITION, Meter. glTF = (Spiel x, Spiel z, −Spiel y), Bug −z, oben +y, Steuerbord +x. Schiff = (−glTF z, −glTF x, glTF y), det +1: keine Spiegelung (eindeutig an der Reliant Tana, deren einziges Grid außermittig sitzt). Ursprung = Spielmodell, quer mittig, längs nicht (Hull A 11 m hinter dem Bug).
- **Hull A, B, C** tragen `cargoPose`: Spindel ausgefahren, Hull A 29,4 m (Fleetyards im Flug 23,42 m).
- **Entfallen** in der Pipeline: Bugachse per IoU gegen den Fleetyards-Umriss, Skalierung auf die Fleetyards-Länge, Neu-Zentrierung, Draco-Decode (`holo_mesh.py flatten`). **Bleiben:** Streu-Filter, Weld, Cull, Leiter, `judge`, SCH2.
- **Hull-A-Probe** (`preview/holo-v2/hull_a_probe.png`): Racks am Spindelabschnitt, Platten in den Boxen, Spindel ausgefahren. Die Racks ragen seitlich bis 5,6 m über die Achse, der Bugteil ist ±4,66 m breit: Kisten hängen außen an der Spindel. Kein Stopp.

**Dreiecke und Größe:**

| | holo-v1 | holo-v2 |
|---|---|---|
| Dateien | 118 (65,1 MB) | 98 (31,4 MB): 95 Modelle, 3 aus holo-v1 byte-gleich |
| Dreiecke min / Median / max | 19 646 / 39 249 / 50 782 | 2 249 / 19 787 / 39 997 |
| Stufen 20k / 30k / 40k | 37 / 21 / 59 | 59 / 10 / 29 |
| `judge` bestanden | 69 von 117 | 77 von 98 |
| Hull A / Hull C / Reclaimer | 19 896 / 39 949 / 39 884 | 13 085 / 29 304 / 19 947 |
| Override nötig | Centaurus (50 782) | keiner (Centaurus 39 374) |

- Roh: 2 284 (UTV) bis 134 444 (Ironclad Assault), Median 37 773. Streu-Dreiecke: 0 bei allen 96.
- Nicht bestanden (alle auf 40k, wie in holo-v1 Sache des Kontaktabzugs): Hammerhead, Idris M/P, Tiburon, RAFT, SRV, Corsair, Ironclad, Ironclad Assault, Starfarer, Starfarer Gemini, Starlancer MAX/TAC, Apollo Medivac/Triage, Constellation Aquila/Phoenix, Centaurus, Hermes, Perseus, Polaris.
- Umrisse: 98 Einträge, 8 573 Punkte, 149 KB, alle eine Kontur. Größeres Schließen bei MOLE (0,8 m), MOTH (1,6), Ironclad und Ironclad Assault (0,8).

<details><summary>Dreiecke pro Slug, holo-v1 → holo-v2</summary>

| Slug | Modell | roh | Streu | v1 Stufe | v1 Dreiecke | v2 Stufe | v2 Dreiecke | v2 judge | v1 Bytes | v2 Bytes |
|---|---|---|---|---|---|---|---|---|---|---|
| aegs-avenger-titan | AEGS_Avenger_Titan | 7644 | 0 | 30000 | 29712 | 20000 | 6732 | True | 501740 | 105664 |
| aegs-avenger-titan-renegade | AEGS_Avenger_Titan_Renegade | 6931 | 0 | 30000 | 29725 | 20000 | 6060 | True | 501990 | 97408 |
| aegs-hammerhead | AEGS_Hammerhead_GS | 91543 | 0 | 40000 | 39947 | 40000 | 39353 | False | 657078 | 535478 |
| aegs-idris-m | AEGS_Idris_M | 90548 | 0 | 40000 | 39895 | 40000 | 39888 | False | 642734 | 565616 |
| aegs-idris-p | AEGS_Idris_P | 90680 | 0 | 40000 | 39895 | 40000 | 39840 | False | 642734 | 561848 |
| aegs-reclaimer | AEGS_Reclaimer | 69515 | 0 | 40000 | 39884 | 20000 | 19947 | True | 608016 | 337734 |
| aegs-redeemer | AEGS_Redeemer | 39434 | 0 | 20000 | 19782 | 20000 | 19716 | True | 361656 | 283752 |
| aegs-tiburon | AEGS_Tiburon | 94471 | 0 | 30000 | 29516 | 40000 | 39361 | False | 525936 | 521910 |
| anvl-asgard | ANVL_Asgard | 53350 | 0 | 30000 | 29654 | 40000 | 39997 | True | 540680 | 555550 |
| anvl-c8-pisces | ANVL_C8_Pisces | 3525 | 0 | 20000 | 19934 | 20000 | 3368 | True | 350448 | 56644 |
| anvl-c8x-pisces-expedition | ANVL_C8X_Pisces_Expedition | 3528 | 0 | 20000 | 19850 | 20000 | 3416 | True | 355944 | 57484 |
| anvl-carrack | ANVL_Carrack | 72464 | 0 | 40000 | 39620 | 20000 | 19741 | True | 642360 | 323358 |
| anvl-f7c-hornet-mk-ii | fleetyards (holo-v1) |  |  | 20000 | 19799 | 20000 | 19799 | True | 403390 | 403390 |
| anvl-paladin | ANVL_Paladin | 75101 | 0 | 40000 | 39600 | 30000 | 29328 | True | 668892 | 395916 |
| anvl-valkyrie | ANVL_Valkyrie | 40865 | 0 | 30000 | 29663 | 30000 | 29958 | True | 528494 | 454028 |
| argo-csv-sm | ARGO_CSV_Cargo | 5364 | 0 | 30000 | 29818 | 20000 | 5263 | True | 550872 | 68654 |
| argo-mole | ARGO_MOLE | 39399 | 0 | 40000 | 39896 | 30000 | 29999 | True | 717504 | 435594 |
| argo-moth | ARGO_MOTH | 48741 | 0 | 40000 | 39795 | 30000 | 29987 | True | 697650 | 417178 |
| argo-mpuv-cargo | ARGO_MPUV | 3779 | 0 | 20000 | 19988 | 20000 | 3457 | True | 396924 | 53190 |
| argo-mpuv-tractor | ARGO_MPUV_1T | 4935 | 0 | 40000 | 39914 | 20000 | 4482 | True | 713268 | 62620 |
| argo-raft | ARGO_RAFT | 45645 | 0 | 40000 | 39283 | 40000 | 39972 | False | 695750 | 558432 |
| argo-srv | ARGO_SRV | 48578 | 0 | 40000 | 39681 | 40000 | 39930 | False | 704722 | 512328 |
| cnou-mustang-alpha | CNOU_Mustang_Alpha | 12250 | 0 | 20000 | 19858 | 20000 | 11212 | True | 365880 | 172568 |
| cnou-nomad | CNOU_Nomad | 8541 | 0 | 20000 | 19793 | 20000 | 6379 | True | 348762 | 107874 |
| crus-a2-hercules | CRUS_Starlifter_A2 | 55135 | 0 | 30000 | 29847 | 20000 | 19998 | True | 506670 | 297000 |
| crus-c1-spirit | CRUS_Spirit_C1 | 20113 | 0 | 30000 | 29656 | 20000 | 15002 | True | 577416 | 222392 |
| crus-c2-hercules | CRUS_Starlifter_C2 | 45932 | 0 | 20000 | 19885 | 20000 | 19900 | True | 361206 | 304980 |
| crus-intrepid | CRUS_Intrepid | 32731 | 0 | 20000 | 19865 | 30000 | 23819 | True | 363054 | 308758 |
| crus-m2-hercules | CRUS_Starlifter_M2 | 47872 | 0 | 20000 | 19981 | 20000 | 19814 | True | 362550 | 302016 |
| crus-mercury-star-runner | CRUS_Star_Runner | 26996 | 0 | 20000 | 19773 | 20000 | 16513 | True | 363918 | 285822 |
| drak-caterpillar | DRAK_Caterpillar | 96024 | 0 | 40000 | 39746 | 30000 | 29407 | True | 678000 | 406986 |
| drak-clipper | DRAK_Clipper | 65370 | 0 | 40000 | 39613 | 40000 | 39918 | True | 662058 | 503292 |
| drak-corsair | DRAK_Corsair | 85953 | 0 | 40000 | 39825 | 40000 | 39923 | False | 659142 | 531266 |
| drak-cutlass-black | DRAK_Cutlass_Black | 26617 | 0 | 30000 | 29550 | 30000 | 20572 | True | 536648 | 328272 |
| drak-cutlass-blue | DRAK_Cutlass_Blue | 26600 | 0 | 30000 | 29934 | 20000 | 20000 | True | 547092 | 322784 |
| drak-cutlass-red | DRAK_Cutlass_Red | 23282 | 0 | 30000 | 29606 | 20000 | 19996 | True | 544956 | 323292 |
| drak-cutter | DRAK_Cutter | 18348 | 0 | 40000 | 39539 | 20000 | 15718 | True | 748182 | 234960 |
| drak-cutter-rambler | DRAK_Cutter_Rambler | 21704 | 0 | 40000 | 39651 | 20000 | 18623 | True | 752370 | 269010 |
| drak-cutter-scout | DRAK_Cutter_Scout | 20427 | 0 | 40000 | 39310 | 20000 | 17708 | True | 697556 | 261240 |
| drak-golem-ox | DRAK_Golem_OX | 21413 | 0 | 40000 | 39607 | 20000 | 17245 | True | 717798 | 238698 |
| drak-ironclad | DRAK_Ironclad | 125207 | 0 | 40000 | 39927 | 40000 | 39758 | False | 702098 | 488100 |
| drak-ironclad-assault | DRAK_Ironclad_Assault | 134444 | 0 | 40000 | 40025 | 40000 | 38716 | False | 689022 | 466608 |
| drak-mule | DRAK_Mule | 3559 | 0 | 30000 | 29952 | 20000 | 3283 | True | 560904 | 41718 |
| drak-vulture | DRAK_Vulture | 36696 | 0 | 40000 | 39768 | 20000 | 19596 | True | 719808 | 282584 |
| espr-prowler-utility | ESPR_Prowler_Utility | 46270 | 0 | 30000 | 29754 | 40000 | 35436 | True | 511904 | 454188 |
| gama-railen | GAMA_Railen | 53320 | 0 | 40000 | 39949 | 40000 | 39475 | True | 643546 | 492910 |
| gama-syulen | GAMA_Syulen | 19131 | 0 | 40000 | 39900 | 20000 | 17235 | True | 743412 | 270606 |
| gama-tyilui | GAMA_Tyilui | 64290 | 0 | 40000 | 39934 | 40000 | 39255 | True | 627204 | 469938 |
| grey-shiv | GLSN_Shiv | 50193 | 0 | 40000 | 39249 | 20000 | 19530 | True | 750002 | 283812 |
| grin-utv | GRIN_UTV | 2284 | 0 | 30000 | 29625 | 20000 | 2249 | True | 545886 | 28422 |
| misc-fortune | MISC_Fortune | 37546 | 0 | 20000 | 19927 | 30000 | 29974 | True | 372306 | 385304 |
| misc-freelancer | MISC_Freelancer | 15894 | 0 | 20000 | 19740 | 20000 | 10712 | True | 379704 | 158920 |
| misc-freelancer-dur | MISC_Freelancer_DUR | 18878 | 0 | 20000 | 19982 | 20000 | 13651 | True | 374784 | 219278 |
| misc-freelancer-max | MISC_Freelancer_MAX | 21907 | 0 | 20000 | 19940 | 20000 | 16761 | True | 380484 | 250602 |
| misc-freelancer-mis | MISC_Freelancer_MIS | 20772 | 0 | 20000 | 19958 | 20000 | 12399 | True | 379020 | 188362 |
| misc-hull-a | MISC_Hull_A | 14575 | 0 | 20000 | 19896 | 20000 | 13085 | True | 395196 | 185882 |
| misc-hull-b | MISC_Hull_B | 113235 | 0 | 20000 | 19646 | 20000 | 19702 | True | 368028 | 264756 |
| misc-hull-c | MISC_Hull_C | 88074 | 0 | 40000 | 39949 | 30000 | 29304 | True | 708138 | 435248 |
| misc-reliant-kore | MISC_Reliant | 13304 | 0 | 40000 | 39598 | 20000 | 11000 | True | 733900 | 177288 |
| misc-reliant-tana | MISC_Reliant_Tana | 14108 | 0 | 40000 | 39658 | 20000 | 11037 | True | 741564 | 177810 |
| misc-starfarer | MISC_Starfarer | 70283 | 0 | 40000 | 39775 | 40000 | 39330 | False | 688806 | 533940 |
| misc-starfarer-gemini | MISC_Starfarer_Gemini | 71121 | 0 | 40000 | 39775 | 40000 | 39277 | False | 692030 | 533246 |
| misc-starlancer-max | MISC_Starlancer_Max | 110375 | 0 | 30000 | 29902 | 40000 | 38577 | False | 512052 | 477534 |
| misc-starlancer-tac | MISC_Starlancer_TAC | 121909 | 0 | 40000 | 39958 | 40000 | 39098 | False | 652200 | 505704 |
| orig-100i | ORIG_100i | 8057 | 0 | 40000 | 39992 | 20000 | 6699 | True | 659212 | 93794 |
| orig-125a | ORIG_125a | 8992 | 0 | 40000 | 39895 | 20000 | 7208 | True | 662046 | 101228 |
| orig-135c | fleetyards (holo-v1) |  |  | 40000 | 39947 | 40000 | 39947 | True | 661206 | 661206 |
| orig-300i | ORIG_300i | 7772 | 0 | 20000 | 19855 | 20000 | 6507 | True | 396066 | 100214 |
| orig-315p | ORIG_315p | 8352 | 0 | 20000 | 19866 | 20000 | 6838 | True | 397164 | 104864 |
| orig-325a | ORIG_325a | 8667 | 0 | 40000 | 39977 | 20000 | 7079 | True | 600942 | 108230 |
| orig-350r | ORIG_350r | 7878 | 0 | 40000 | 39973 | 20000 | 6355 | True | 604758 | 100722 |
| orig-400i | ORIG_400i | 23741 | 0 | 30000 | 29643 | 20000 | 18508 | True | 523470 | 287576 |
| orig-600i-executive-edition | fleetyards (holo-v1) |  |  | 20000 | 19888 | 20000 | 19888 | True | 369420 | 369420 |
| orig-600i-explorer | ORIG_600i | 72150 | 0 | 20000 | 19888 | 20000 | 19803 | True | 369420 | 315402 |
| orig-600i-touring | ORIG_600i_Touring | 68205 | 0 | 20000 | 19883 | 20000 | 19818 | True | 370206 | 314316 |
| orig-890-jump | ORIG_890Jump | 64419 | 0 | 40000 | 39963 | 20000 | 19774 | True | 678894 | 334668 |
| orig-m80 | ORIG_m80 | 43469 | 0 | 40000 | 39842 | 20000 | 19669 | True | 681048 | 275526 |
| rsi-apollo-medivac | RSI_Apollo_Medivac | 88542 | 0 | 40000 | 39869 | 40000 | 39707 | False | 684810 | 567026 |
| rsi-apollo-triage | RSI_Apollo_Triage | 88824 | 0 | 40000 | 39804 | 40000 | 39694 | False | 683880 | 563520 |
| rsi-aurora-mk-i-cl | RSI_Aurora_GS_CL | 19941 | 0 | 20000 | 19916 | 20000 | 15463 | True | 356820 | 220002 |
| rsi-aurora-mk-i-es | RSI_Aurora_GS_ES | 19572 | 0 | 20000 | 19916 | 20000 | 15242 | True | 356820 | 217368 |
| rsi-aurora-mk-i-ln | RSI_Aurora_GS_LN | 20318 | 0 | 20000 | 19916 | 20000 | 15826 | True | 356820 | 223836 |
| rsi-aurora-mk-i-lx | RSI_Aurora_GS_LX | 19553 | 0 | 20000 | 19916 | 20000 | 15248 | True | 356820 | 217392 |
| rsi-aurora-mk-i-mr | RSI_Aurora_GS_MR | 19637 | 0 | 20000 | 19916 | 20000 | 15228 | True | 356820 | 217320 |
| rsi-aurora-mk-i-se | RSI_Aurora_GS_SE | 20438 | 0 | 20000 | 19836 | 20000 | 15970 | True | 379644 | 225768 |
| rsi-aurora-mk-ii | RSI_Aurora_Mk2 | 38000 | 0 | 40000 | 39952 | 30000 | 29312 | True | 731724 | 350732 |
| rsi-constellation-mk-iv-andromeda | RSI_Constellation_Andromeda_GS | 70768 | 0 | 40000 | 39924 | 40000 | 39462 | True | 719496 | 525168 |
| rsi-constellation-mk-iv-aquila | RSI_Constellation_Aquila_GS | 68091 | 0 | 40000 | 39854 | 40000 | 39676 | False | 727584 | 533388 |
| rsi-constellation-mk-iv-phoenix | RSI_Constellation_Phoenix_GS | 67773 | 0 | 40000 | 39904 | 40000 | 39517 | False | 712008 | 526794 |
| rsi-constellation-mk-iv-taurus | RSI_Constellation_Taurus_GS | 68614 | 0 | 40000 | 39600 | 40000 | 39525 | True | 732108 | 523398 |
| rsi-constellation-mk-v-centaurus | RSI_Constellation_Mk5_Centaurus | 94447 | 0 | 40000 | 50782 | 40000 | 39374 | False | 799308 | 516132 |
| rsi-hermes | RSI_Hermes | 85645 | 0 | 40000 | 39721 | 40000 | 39554 | False | 697442 | 559340 |
| rsi-perseus | RSI_Perseus | 108406 | 0 | 40000 | 39816 | 40000 | 38663 | False | 656460 | 404822 |
| rsi-polaris | RSI_Polaris | 119291 | 0 | 40000 | 39756 | 40000 | 39414 | False | 613560 | 485316 |
| rsi-salvation | RSI_Salvation | 19228 | 0 | 20000 | 19931 | 20000 | 14074 | True | 410550 | 206172 |
| rsi-zeus-mk-ii-cl | RSI_Zeus_CL | 29815 | 0 | 20000 | 19981 | 20000 | 19350 | True | 357630 | 300456 |
| rsi-zeus-mk-ii-es | RSI_Zeus_ES | 26957 | 0 | 20000 | 19981 | 20000 | 17439 | True | 357630 | 278010 |
| tmbl-cyclone | TMBL_Cyclone | 4660 | 0 |  |  | 20000 | 4585 | True |  | 60286 |

</details>

**`fit` über alle 102** (Schwellen E1, vor dem Lauf festgelegt):
- (b) > 0,05: nur **Fortune Rack 2** (rack-1, Backbord-Seitenbucht, senkrecht), 0,456. Rack 3 auf der anderen Seite liegt darunter. Seite und Lage stimmen, die Hülle schneidet in die Box. Vermutlich Rundung (rack-1 in 4a [0,12; 0,31; −0,11] Zellen) oder eine geschlossene Bucht im Modell. **Auffällig für Martins Blick.**
- (a) < 1: csv_sm 0,50, hull_a 0,375, hull_b 0,875, mpuv_tractor 0,50, utv 0,50. Außenfracht oder offene Ladefläche (Probe-Bilder Hull B, MPUV Tractor angesehen: Spindel ±42°, Frachtbucht unter dem Rumpf).
- (c) < 0,90: keins. Niedrigste: MOTH 0,919, RAFT 0,921, Hull C 0,941.

**Daten (C4):** Konverterlauf 102 updated, 0 „anchors kept“; zweiter Lauf 102 same. Diffs nur in Ankerzeilen. `outlineScale` fällt bei Hull A, Caterpillar, Syulen und Railen weg. Cyclone mit Slug `tmbl-cyclone`. TS-Modul ohne eigene Anker: Es zeigt die Hülle der Aurora, vorher drei Zellen achtern, nur um das Bild zu halten (D90). Aurora-Mk-II-Notiz ohne den veralteten Satz zur Holo-Höhe. Zwei Datentests bekamen Nachfolger (Hull-A-Umriss aus dem Modell, TS-Modul zeigt die Schiffshülle).

**Bildvergleich** (`ShipPictureTests` gegen `main` 05feead mit holo-v1, Branch mit holo-v2):

| Look | gleich | anders |
|---|---|---|
| none (400×302, 800×600) | **106 / 106** | 0 |
| outline, holo-* (fit, cargo, above) | 3 (die drei Fleetyards-Schiffe) | 103 |
| saved (5 Gate-Schiffe) | 0 | 5 |

1 Bild fehlt: `cyclone;outline;above`. Die Cyclone hat jetzt ein Holo, die Draufsicht zeigt es statt des Umrisses.

**Tests:** Core 1814, App 1621, Import 141 grün; `Category=Slow` 2/2; Pipeline 53 + 5 Tests (Outline-Repo).

**Kontaktabzug nachher** (Martins Sichtprüfung): `Documents\Coding\Dotnet\sc-ship-outlines\preview\holo-v2\sheet\_sheet1.png` … `_sheet9.png`, je schräg und von oben, Holo schattiert mit Racks. Die drei Fleetyards-Schiffe zeigen „no picture“: Sie sind bitgleich zu 4a, der Harness speichert mit Baseline nur abweichende Bilder (4a-Kontaktabzug). Probe-Bilder der Auffälligen: `preview\holo-v2\*_probe.png`.

**Frame-Zeit über dem Spiel** (Martin, 08.11., `--measure-frames <Ziel> --over-game`, SC vorn, je drei Läufe, Median). `main` 05feead mit holo-v1 und derselben Reclaimer-Zeile im Scratch-Worktree gegen den Branch d1b09aa mit holo-v2. Gate: gleiches Ziel, Anteil später Frames ≤ +2 pp.

| Ziel | `main` (holo-v1) Läufe → Median | Branch (holo-v2) Läufe → Median | Δ |
|---|---|---|---|
| hull_c (144 Kisten) | 0,2 / 0,2 / 0,3 → 0,2 % | 0,2 / 0,1 / 0,1 → 0,1 % | −0,1 pp ✓ |
| hull_c_drag (72) | 11,8 / 11,9 / 12,2 → 11,9 % | 11,6 / 11,1 / 14,5 → 11,6 % | −0,3 pp ✓ |
| reclaimer (420) | 5,1 / 3,7 / 1,5 → 3,7 % | 3,3 / 0,8 / 8,6 → 3,3 % | −0,4 pp ✓ |

- **Drag:** path keys 31–34 auf `main`, 78–82 auf dem Branch. SC hielt den Cursor in diesen Läufen weniger fest, der Pfad traf mehr Zellen. Hit-Test und Drag sind unverändert, das Holo ist nie getroffen. Damit passt: Vorschau-Median 1,9–2,1 → 2,6–3,1 ms, p95 5,7–6,2 → 7,8–8,5 ms, unter der Grenze von 16 ms (T8.4 R4).
- **Reclaimer:** streut in beiden Builds stark (1,5–8,6 %), der Median liegt gleich.

**Abnahme:**
- [x] A2 als eigener Commit vor jedem Anker, Testzahlen unverändert
- [x] Hull-A-Probe: Rack im Holo, Platten in der Spindel, Spindel ausgefahren
- [x] Alle Modelle, Dreieckstabelle, Asset-Größe (31,4 MB)
- [x] Anker für 102 Dateien, Look `none` bitgleich bei 106 von 106
- [x] Konverter schreibt die Anker (Tests erst rot), zweiter Lauf ändert nichts
- [x] Frame-Zeit `--over-game` `hull_c`, `hull_c_drag`, `reclaimer`, `main` gegen Branch (Martin, 08.11.): kein Ziel schlechter, Gate ≤ +2 pp überall erfüllt
- [x] Martin (08.11.): Kontaktabzug abgenommen, „passt perfekt“, keine Liste. Hull A: 5 Zellen zwischen den Platten, Anordnung im Holo stimmt.
- [x] Upload `holo-v2` nach Martins Freigabe (08.11.): Pre-Release „Ship holograms v2“, nicht Latest, 100 Assets (98 `.holo`, NOTICE, Manifest zuletzt). Größen gleich, Manifest byte-gleich, Hull A, 135c und Cyclone über die App-URL mit passendem SHA.
- [x] Publish-Build `out/publish/SC-CargoPlan` (ZIP 62,1 MB) mit den neuen Umrissen und Schiffsdateien
- [x] Martin über dem Spiel (Publish-Build): Hull A (Spindel), RAFT, Aurora Mk II · TS module, Hull C, GRIN UTV – Holo-Download, Bild flüssig, Racks sitzen; Holo-Halbtransparenz und Rack-Nummern-Verdeckung am neuen Mesh – vor dem Push. Abgehakt in der Release-Session (08.11.): Hauling-Abend auf `main` ohne Befund, keine Rack-Nummern-Verdeckung (Martin).
- [x] Merge `--no-ff` nach Martins Freigabe „für alles“ (08.11.), Martin pusht, kein Tag

**Offen für 4c:**
- ~~`fleetyardsSlug` → `modelKey` mit Lade-Migration (E2).~~ Erledigt in 4c: stiller Alias statt Migration (D94).
- ~~Den verwaisten Cache `holo\holo-v1\` beim Start entfernen (App-Code, E2).~~ Erledigt in 4c (D95).
- ~~Die drei Fleetyards-Schiffe (135c, 600i EE, F7C Mk II), `import-fleetyards.ps1`, Fleetyards-Credits.~~ Erledigt in 4c (D94).
- Erster echter Abruf über die Motherlode-API (`fetch-motherlode.ps1`, umgestellt in dieser Session, Martin 07.11.). Danach Konverter, Pipeline nur bei geänderten Modellen.
- Fortune Rack 2: hängt am Kontaktabzug. Bei Bedarf `shift` (D91) oder ein Spielerbericht als Auslöser.
- Nexus-Texte zum 0.3.0-Upload: `holo-v1` → `holo-v2` (`docs/nexus/checklist.md:100`, `staff-request.md:39`), Credit „cargo grid data and ship models from cryon.rocks (Motherlode)“.

**Material für Release-Notes v0.3.0:**
- Hull holograms now come from the same game data as the racks – ships finally line up.
- Hull A, B and C appear in the overlay with the spindle extended (cargo pose), as at the elevator.
- Outlines are the game models' top views at true size; the Cyclone now has a hologram too.
- Holograms download from the new release `holo-v2`: each ship's hologram re-downloads once when first shown (about 0.3 MB per ship); v0.2 keeps using `holo-v1`. (Korrigiert in 4c, Martin: keine Gesamtgröße, niemand lädt alle Schiffe.)

### [x] Session 4c – Fleetyards stilllegen, Holo-Übergang, Credits (08.11.2026, abgenommen 08.11.)

**Branch:** `chore/retire-fleetyards` (App-Repo und Outline-Repo). **Ziel:** den Fleetyards-Import stilllegen, `fleetyardsSlug` → `modelKey`, Übergang holo-v1 → holo-v2 für Nutzer von v0.2.0, Credits und Entscheidungen (D94, D95). Kein Bild ändert sich, kein Tag.

**Festlegungen von Martin:**
- `import-fleetyards.ps1` und seine Tests fliegen raus, `ShipJson.ps1` bleibt. Im Outline-Repo kommen Fetch, Index und Rohdaten nach `legacy/`.
- `modelKey` mit stillem Alias `fleetyardsSlug`, Schema 6 bleibt, alle 105 Dateien werden umgeschrieben.
- D95: fremde Tag-Ordner nach erfolgreicher Manifest-Prüfung löschen, sofort bei Schalter aus. Bei Fehlschlag bleibt alles. Einmalige Notice nur bei Tag-Wechsel.
- Credits-Wortlaut, D94/D95.

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:**
1. Notice ohne Größe: „Ship holograms updated to match the new rack data – each ship re-downloads when first shown“. Release-Notes: „about 0.3 MB per ship“.
2. Kein Holo-Look beim Start (Umriss/None) gilt wie Schalter aus: sofort löschen, kein Netz, keine Notice.
3. Auslöser ist die erfolgreiche Manifest-Prüfung. Scheitert danach eine Schiffsdatei, sind die alten Ordner trotzdem weg.
4. CSV-Spalte `-ModelMap` → `modelKey` in beiden Repos, `holo_mesh.py` liest beide Namen.
5. Die C#-Eigenschaft heißt `Ship.ModelKey`.
6. `HoloSource` bekommt die Wurzel `holo\` statt des Tag-Ordners (`HoloRelease.DefaultCacheRoot`).
7. Gelöscht werden nur Ordner `holo-v<Ziffern>` ≠ `HoloRelease.Tag` direkt unter der Wurzel.
8. **K1 (Korrektur bei der Freigabe):** `NoticeSource.HoloUpdate` lebt wie die Holo-Notice und verschwindet mit dem ersten Hologramm. Nur bei einem Löschfehler bleibt sie mit Grund bis Programmende und kommt beim nächsten Start wieder.
9. Wiki, Nexus-Beschreibung und das Issue-Template `ship-correction.yml` bleiben bis zur Release-Session, weil sie bis zum Tag v0.2.0 beschreiben.
10. Outline-Repo `legacy/` nur mit Pfadänderungen.
- In der Umsetzung kam dazu: Die Stilllegung wird vor dem Alias committet, Alias und 105 Dateien in einem Commit. So ist jeder Commit für sich grün (getrennt wäre der Alias-Commit am Datentest rot gewesen).
- In der Umsetzung kam auch dazu: Das Event `TagChanged` kommt nach der Manifest-Prüfung und vor dem ersten Download. Sonst hätte die Notice nach K1 im selben Aufruf, in dem das erste Hologramm kommt, schon wieder verschwinden müssen.

**Befund:**
- `ShipJsonContext` hat `UnmappedMemberHandling.Disallow`, also ist ohne Alias jede Nutzerdatei kaputt.
- `HoloSource` lief nur mit Holo-Look und bekam den Tag-Ordner. Die Tests lagen unter einem geteilten `%TEMP%\CargoPlan.Tests`.
- `NoticeSource.Holo` wird mit jedem Hologramm gelöscht, deshalb gibt es eine eigene Zeile.
- `tests/test_fetch.py` im Outline-Repo testet `fetch-motherlode.ps1` und bleibt. Das korrigiert den Befund des Plans.

**Ergebnis:**
- **App-Repo:**
  - `a3430b9` Stilllegung
  - `35905db` `modelKey` + 105 Dateien
  - `b0f9dd0` D95
  - `e4afa39` Doku, Credits, D94/D95
  - dazu dieser TASKS-Commit
- **Outline-Repo:** `5285556`.
- **Gegenprobe:** `import-motherlode.ps1` gegen `catalog-2026-11-07` ergibt Exit 0, 102 same, 0 updated. Die Model-Map unterscheidet sich nur im Kopf.
- **105 Dateien:** je eine Zeile `"fleetyardsSlug":` → `"modelKey":`, CRLF und kein BOM geprüft.
- **Bildvergleich** `ShipPictureTests` gegen `main`: **1388 identisch, 0 verschieden, 0 fehlend**, alle Looks (`none`, `outline`, `holo-flat`, `holo-hiddenline`, `holo-shaded` inkl. above/cargo/saved). Placement-Zahlen auf 0 von 106 verschieden.
- **Von den Tests gefunden:** `TagChanged?.Invoke(new HoloTagChange(RetireOtherTags()))` wertet ohne Abonnenten das Argument nicht aus, der Ordner blieb. Das Löschen steht jetzt davor.
- **Testzahlen**, sauberer Checkout (die lokal per `.git/info/exclude` ausgeschlossene `ScratchEveryShipBaseline.cs` zählt in der Arbeitskopie einen Core-Test mehr):

  | Projekt | `main` | nach Stilllegung | nach `modelKey` | nach D95 |
  |---|---|---|---|---|
  | Core | 1813 | 1776 (−37) | 1783 (+7) | 1783 |
  | App | 1621 | 1621 | 1621 | 1644 (+23) |
  | Import | 141 | 141 | 141 | 141 |
  | **Summe** | **3575** | 3538 | 3545 | **3568** |

  `Category=Slow` Core: 2 → 2 grün. Outline-Repo: 58 → 59 grün.

**Abnahme:**
- [x] Tests zuerst rot: Alias 7, Holo 15, Outline-Repo 2
- [x] 105 Schiffsdateien byte-genau umgeschrieben, Konverter schreibt `modelKey`
- [x] Fleetyards-Import, Tests und Fixtures entfernt, Testzahlen vorher/nachher
- [x] Doku, Credits, D94, D95
- [x] Bildvergleich bitgleich, `dotnet build` ohne Warnung, `dotnet test` und `Slow` grün
- [x] Gate Martin (Publish-Build, 08.11.: „funktioniert alles“):
  - Erstinstall: v2 lädt, keine Notice
  - Upgrade: Notice einmal, `holo-v1` weg, zweiter Start ohne Notice
  - Schalter aus: Ordner weg, kein Download
  - Offline: Umriss/Keil, `holo-v1` bleibt, mit Netz wird aufgeräumt
  - Umriss-Look: Ordner weg, keine Notice
  - eigene Schiffsdatei mit `fleetyardsSlug` lädt
- [x] Merge `--no-ff` nach Martins Freigabe (08.11.), beide Repos; Martin pusht, kein Tag

**Offen nach 4c:**
- `holo-v1`-Release auf GitHub entfernen. Auslöser: v0.3.0 vier Wochen stabil.
- Modelle bei Sciirof anfragen (135c: className null; 600i EE, F7C Mk II: nicht im Katalog), nach dem Launch, mit Ergebnis als Gegenleistung.
- Release-Session: Wiki, Nexus-Beschreibung, `ship-correction.yml` (siehe v0.3.0-Liste, Punkt 6).
- Outline-Repo: `decimate.ps1` und `run-all.ps1` haben weiter `-Source 'Fleetyards'` als Default. Umstellen auf Motherlode erst bei Bedarf.
- Aus 4b weiter offen: erster echter Abruf über die Motherlode-API, Fortune Rack 2, Nexus-Texte `holo-v2`.
- Nachricht an Torlek (Fleetyards): Martin schickt sie nach dem Merge, kein Repo-Inhalt. Das Verlinkungsangebot ist T9.4.

### [x] Session 4d – Editor-Fixes aus Issue #22 und #21 (08.11.2026, abgenommen 08.11.)

**Branch:** `fix/editor-crates` ab `main`. **Anlass:** Issues von **Reikal** (GitHub-Login) auf v0.2.0: #22 „Max Crate selection non-functional“ (mit Rohtext von vier Pyro-Verträgen), #21 „Re-work crate counter“. B8 und B9 blockieren das Release.

**Befund:**
- **B8:** Das Binding der Max-crate-Spalte war in Ordnung (`UpdateSourceTrigger=PropertyChanged` stand schon). Ursache: Der `ButtonBase.Click`-Handler auf dem DockPanel des Editors beendet jede Zellbearbeitung. Der Pfeil der ComboBox ist ein `ToggleButton` und löst ihn ebenfalls aus. Die Editier-Combo wird verworfen, bevor die Auswahl ankommt. Das gibt es seit T3.8b. Die Tastaturauswahl löst keinen Klick aus und funktioniert deshalb. Dass eine neue Zeile die Größe der Zeile darüber übernimmt, ist gewollt (D23).
- **B9:** Der Parser kannte nur „Max size … N“ und „bigger than N“. Die Red-Wind-Verträge sagen „8 SCU or smaller“. In zwei von drei Captures hat die OCR „smaller)“ verloren, übrig bleibt „(8 SCU or“. Martins CT-DB ist leer (0 Verträge, auch die `.bak`), weitere Formulierungen gibt es dort nicht.
- **F9:** Die Legende gruppiert seit K6.1 (`LegendText.Mix`). Nur die Editor-Spalte, die Planer-Notices und die Cli schrieben eigene Formate.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
- B8: kein Undo-Schritt für die Auswahl, wie beim Tippen (der Editor hat keins, D19).
- B8: erstes Test-Fenster im Repo erlaubt, falls `BeginEdit` realisierte Zellen braucht. Nicht nötig geworden, Layout offscreen reicht.
- B8 E1: ein Test sichert den ursprünglichen Zweck des Handlers (Toolbar-Knopf committet die offene Zelle).
- B9: Die Klammerform „(N SCU or“ zählt ohne „smaller“, außer bei „or more/larger/bigger“.
- B9: Der Default für eine unbekannte Größe bleibt die größte Kiste, die in die SCU passt, mit „?“. Grund: Kleine Kisten füllen den Platz großer lückenlos (D57).
- F9: Die Zelle zeigt keine eigene Summe, der Fehler nennt sie.
- F9: Notices und Cli folgen der einen Schreibweise; „(1×4)“ wird „(4)“.
- F9: Ein lesbarer Override erscheint normiert und sortiert. Der getippte Text bleibt zum Bearbeiten.

**Abnahme:**
- [x] B8 (e30d65c), Tests zuerst rot (3 von 4, die Zelle war nach dem Pfeilklick wieder im Anzeigemodus):
  - STA-Test für die Auswahl über `ButtonBase.OnClick`
  - Issue-Pfad 1 und 2
  - E1
  - VM-Test
  - Markup-Wächter
- [x] B9 (e57b1d2), Tests zuerst rot (16):
  - drei Fixtures wörtlich aus #22
  - Toleranz- und Negativfälle
  - Golden der Kistenliste: Endgame 124 `3×32+24+4` → `15×8+4`, Orbituary 82 → `10×8+2`, Potassium 215 `6×32+16+4+2+1` → `26×8+4+2+1`, Carbon 200 `6×32+8` → `25×8`
  - D40-Nachtrag, `contracttracker-schema.md`
- [x] F9 (4543c43), Tests zuerst rot (34 in Core gegen Stubs, danach 20 umgestellte Erwartungen):
  - `CrateMix.Format`/`Parse` in Core, Round-Trip
  - Legende unverändert
  - D96, Nachträge D23 und D69
- [x] `dotnet build` ohne Warnung, `dotnet test` (App 1655, Import 164, Core 1819; nach dem Nachtrag App 1659) und `Slow` grün
- [x] Gate Martin (Publish-Build), abgenommen (Martin, Freigabe der Release-Session 08.11.):
  - Pfad 1 und 2 aus #22: Das Dropdown ändert den Wert, die Kistenliste rechnet neu.
  - Sync mit einem „8 SCU or smaller“-Vertrag ergibt max 8 ohne „?“.
  - Die Kistenliste ist gruppiert in Editor und Legende.
  - `2x32+16` (SCU 80) eingetippt wird akzeptiert und erscheint als `2×32+16`.
  - **Spaltenbreite:** `6×32+16+4+2+1` braucht 108 px (Segoe UI 14), das alte Format 214 px. Erledigt durch den Nachtrag: Die Zelle bricht um, eine `MinWidth` für Crates ist nicht nötig.
  - Befund am Gate (08.11.): drei UI-Anpassungen, siehe Nachtrag.
- [x] **Nachtrag nach dem Gate** (UI, kein Verhalten; D36-Nachtrag):
  - Spaltenköpfe brechen um statt abgeschnitten zu werden (`EditorColumnHeaderStyle`), auch der eigene Kopf-Style der Crates-Spalte.
  - „Max crate“ heißt **„Max size“** (Kurzform von Martin), der Tooltip nennt den ganzen Begriff. Mit `MinWidth="72"` passt der Kopf auf eine Zeile.
  - Die Crates-Zelle bricht um, WPF trennt vor dem `+`: `6×32+16+4+2` / `+1`. Bearbeitet wird einzeilig.
  - Max-size-Dropdown: Die offene Combo und ihre Liste nutzen den impliziten Fluent-Style wie die Layout-Dropdowns, die geschlossene Zelle bleibt Text in der Zellfarbe. Ursache: `DataGridComboBoxColumn` setzt Styles ohne `BasedOn`.
  - Variante B, von Martin am Bild gewählt (light und dark, vorher, Variante A, Variante B). Variante A (beide Styles Fluent) schnitt jede geschlossene Zelle ab, weil Fluent 38 px für den Pfeil freihält. *Präzisierung in der Planphase, ratifiziert durch Freigabe.*
  - Tests zuerst rot (6 Wächter in `MissionEditorXamlTests`); Theme-Beispiel, B8-Testnachbau und `docs/shots/SHOTLIST.md` mitgezogen.
  - **Kein eigener `ToolComboBoxStyle` (A4).** Auslöser, ihn auszuziehen: der erste eigene Setter an den Layout-Dropdowns.
- [x] **Nachtrag 2, zweite und dritte Runde am Bild** (D36-Nachtrag „Markierung neutral, Akzent nur für Bedienelemente“):
  - Befund: Die Anzeige-Zelle erbte richtig. Grau auf Cyan zeigte die **Editier-Combo**, die nach der Auswahl in der Zelle bleibt. Die Popup-Auswahl hatte nur Fluents 3-px-Balken.
  - Der Auslöser ist erreicht: `ToolComboBoxStyle` und `ToolComboBoxItemStyle` baut `ToolWindowThemer` im Code auf Fluents Styles, implizit und per Schlüssel in jedem Werkzeugfenster. Die Layout-Combos und die Settings-Preset-Combo bauen auf dem Item-Style auf, die Grid-Combo auf dem Combo-Style.
  - **Variante C (Martin am Bild, gegen a und b):**
    - dark: markierte Zeile und Popup-Auswahl `SelectionFillDark` #3A4A55 deckend, 3 px Teal-Balken, normaler heller Text; Hover der Ton mit 60 %, ohne Balken.
    - light: unverändert Fluent.
    - Ein Auswahl-Dictionary pro Modus, getauscht wie der graue Dark-Text.
  - Die Editier-Combo im Grid übernimmt die Textfarbe der markierten Zeile, ohne eigene Füllung oder Rahmen.
  - Kontrast dark: Auswahl und Zeile 5,9:1, Hover 7,1:1; der Test hält alle Zustände in beiden Modi ≥ 4,5:1.
  - Tests zuerst rot (Styles im Fenster, gleicher Style für Layout und Grid am echten Editor, Auswahl-Dictionaries, Kontrast, Wächter). Unterwegs gefangen:
    - Ein `DynamicResource`-Alias im Dictionary ist in kompilierter XAML verboten, das Editorfenster wäre beim Öffnen abgestürzt.
    - Ein Brush mit dynamischer Farbe in Style-Ressourcen bleibt durchsichtig.
  - `WatchWindows` meldet sich ohne `Application` nicht bei `SystemEvents` an. Seit Tests `Apply` aufrufen, hing ein voller Lauf einmal und meldete einmal 67 Tests zu wenig; danach fünf Läufe stabil. Der Zusammenhang ist plausibel, nicht bewiesen.
  - **E1 nicht umgesetzt** (passt nicht in eine Zeile): Nach der Auswahl die Zellbearbeitung beenden. `DropDownClosed` ist kein Routed Event, also kein `EventSetter`; es bräuchte einen `PreparingCellForEdit`-Handler mit An- und Abmelden. Offen, bei Bedarf eigener Task.
  - **Rückfrage Martin:** Grid-Zeilen haben in Fluent keinen Hover. Soll dark einen Zeilen-Hover bekommen? Das braucht ein eigenes Zelltemplate, ist also eher ein eigener Task.
- [ ] Dank an Reikal in #22 und #21 (Martin postet, Text liegt im Chat)
- [x] Merge `--no-ff` nach Martins Abnahme (754921c, Martin, gepusht 08.11.). Notes, R1-Diff und Wiki (Editor-Seite: Kistenformat und beide Eingabeformen) in der Release-Session nachgezogen.

**Material für Release-Notes v0.3.0:**
- The max crate size drop-down in the mission editor works again and matches the light or dark theme; the column is now called "Max size" (#22, thanks Reikal).
- Long column headers and crate lists wrap in the mission editor instead of being cut off.
- Clearer selection in the dark tool windows: the selected row and the picked list entry get a calm fill with a teal bar.
- Contracts that say "N SCU or smaller" are parsed: their max crate size is read, no more "max 32 SCU?" (#22).
- Crate lists show grouped, e.g. 6×32+16+4+2+1 instead of every crate (#21, thanks Reikal). You can type either form into the Crates column: 2x32+16 or 32+32+16.

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

### [ ] R5 Nachlauf

- [x] Wiki-Push nach dem Release (Martin, 08.11.), Anker-Check gegen das Live-Wiki (R4-Befund)
- [ ] Martin: Settings-Fußzeile 0.3.0, Release-Seite im Browser (Notes, Assets, Nexus-Link)
- Nexus-Upload am 16.11. mit ~~0.3.0~~ **0.3.1** (Release v0.3.1) nach `docs/nexus/checklist.md` (T9.3)
- Ankündigungen (Entwürfe Claude.ai, Martin verschickt): Ryekal (Reddit), Sciirof, Torlek, Spectrum-Post
- Backlog-Zeilen „holo-v1-Release entfernen“ (Auslöser: v0.3.0 vier Wochen stabil) und „Modelle bei Sciirof anfragen“ mit Datum; nächster Chat „Feedback II“

## Bugfix B10 – Hotkeys im Spiel (Patch v0.3.1)

Eingeschoben vor Phase 10: T10.3 baut auf dem gepollten Hotkey auf.

### [x] B10 Hotkeys im Spiel (Branch `fix/hotkeys-in-game`, D97)

- **Anlass:** `RegisterHotKey`-Hotkeys feuern in v0.3.0 nicht, solange Star Citizen im Vordergrund ist (S10.0-Befund; die gepollte Taste F8 funktionierte).
- **Schritt 1, Ursache klären:** das v0.1.0-Release-ZIP neben dem aktuellen SC testen. Feuert es nicht, hat sich das Spiel geändert; feuert es, ist es eine Regression der App (Bisect).
- **Bei geändertem Spiel:**
  - ein Pfad: `GetAsyncKeyState`-Polling auf einem `DispatcherTimer` (30 Hz, Konstante) mit Flankenerkennung, Modifier wie gespeichert
  - aktiv nur, wenn SC oder CargoPlan vorne ist; ausgesetzt bei Fokus in einem Textfeld und während der Aufnahme
  - D35-Addendum: keine OS-Prüfung „belegt“ mehr, nur reservierte Tasten (Space, F12, Esc), Hinweis „keys also reach the game“
- **Gate im Spiel:**
  - jeder Hotkey genau einmal pro Druck, kein Auto-Repeat
  - kein Auslösen im Editor-Textfeld oder mit dem Browser vorne
  - Frame-Zeit unverändert
- Kein F3-Default, kein Scan-Hotkey (beides Phase 10).

**Befund (09.11.):** Hotkeys über SC sind in keinem Release-Build nachweislich angekommen: das v0.1.0-ZIP feuert im Spiel auch nicht (Martin), die isolierte Probe aus S10.0 bestätigt, dass `RegisterHotKey` mit SC vorne nicht zugestellt wird. `Hotkeys/`, `Win32/`, Overlay zwischen v0.1.0 und v0.3.0 unverändert, also keine Regression zwischen Releases → Polling (Schritt 2b). Vermutete Ursache (nicht prüfbar): SC registriert Raw Input mit `RIDEV_NOHOTKEYS`.

**Präzisiert in der Planung:** Testnaht als Delegate `Func<uint, bool>` statt `IKeyState` (A4); Ctrl/Alt/Win bleibt Pflicht, **Space reserviert** neben F12; Log pro Auslöser in `AppLog` (es gibt kein separates Debug-Log); Modifier exakt wie gespeichert; `TrySet` → `Set`. Review Claude.ai: K1 Befund ohne „25.10. wirkte noch“ (D17-Addendum „never verified in game“); K2 Textfokus zählt nur mit eigenem Prozess vorne; E1 Known issue „SC als Admin → CargoPlan als Admin“ in Release-Notes und Wiki; E2 UI-Thread-Stall als D97-Gegenargument, mit aktuellen Zahlen (Plan-Lauf ≤ 3,2 ms je Fixture, Stresslast 80 ms seit B2/D66; die ~4 s Freelancer stammen aus T7.2 vor B2). Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Umsetzung (Branch `fix/hotkeys-in-game`):** `HotkeyPoller` (Flanke, exakte Modifier, `IsArmed`), `HotkeyService` pollt alle 33 ms, `RegisterHotKey`-Pfad entfernt, Hinweise im Settings-Fenster, D97 + Addenda D17/D35, CLAUDE.md, settings-format.md. Poll-Last gemessen: **4,7 µs pro Tick** (Release, `HotkeyPollTimingTests`; Prozessname bei Fensterwechsel 14 µs).

- [x] Tests vor Implementierung: `HotkeyPollerTests` (Flanke, Halten, Modifier exakt, Win links/rechts, unscharf, `Set` bei gehaltener Taste, K2), Parser Space reserviert, AppLog-Textzeile
- [x] `dotnet build` ohne Warnungen, `dotnet test` grün
- [x] Gate Martin im Spiel (09.11., abgenommen): jeder gesetzte Hotkey genau einmal pro Druck mit SC vorne; `cargoplan.log` zeigt toggleLock, toggleOverlay, syncContractTracker, replan, undo, redo, resetView je Druck eine Zeile (undo/redo vor dem ersten Schritt „not available now“), drei Drücke in 0,6 s einzeln erkannt; discardPlan war nicht belegt
- [x] Gate: Taste gehalten → kein Mehrfachauslösen
- [x] Gate: Hotkey im Editor-Textfeld getippt → kein Auslösen
- [x] Gate: SC minimiert, Browser vorne → kein Auslösen
- [x] Gate: Aufnahme funktioniert, Clear schreibt `null`
- [x] Gate: Frame-Zeit `--over-game` gegen `main` unverändert (Martin)
- [x] Merge `--no-ff` nach Abnahme
- [x] Release v0.3.1 (Notes mit Known issue E1, ZIP, Wiki „Overlay basics#hotkeys“, Tag, Issue) – **v0.3.1 veröffentlicht (09.11.2026)**, siehe unten

## Release v0.3.1 (09.11.2026) – Branch `release/v0.3.1`

**Warum:** B10 (D97) als Patch, damit der Nexus-Start am 16.11. mit funktionierenden Hotkeys rausgeht. Ablauf wie v0.3.0: Notes → Build nachmessen → Tag → Wiki → Abschluss. Notes-Texte von Claude.ai freigegeben; Martin pusht und legt das GitHub-Issue an.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. Branch `release/v0.3.1` (wie `release/v0.3.0`), `--no-ff` nach Martins Abnahme; Tag auf dem Merge-Commit.
2. Notes im Rahmen von v0.3.0: keine H1 (GitHub zeigt den Tag als Titel), Einleitung + Download-Absatz + MIT-Zeile; die freigegebenen Sätze wörtlich, nur „(D97)“ gestrichen (Nutzer können D-Nummern nicht nachschlagen) und „Known issues from v0.3.0“ auf `Troubleshooting#known-issues` verlinkt.
3. Veralteter UI-Grund in `HotkeyParser` („or it would take the key from every program“) in v0.3.1 korrigiert: die gepollte Taste erreicht das Spiel (D97).
4. Wiki über „Overlay basics“ hinaus: Troubleshooting-Abschnitt Hotkeys, FAQ-Antwort zum Anti-Cheat, `Space` aus der Tastenliste, Fußzeile v0.3.1.

### [x] V1 Notes, Code-Grund, Nexus-Version (09.11.2026)

- `docs/release-notes/v0.3.1.md`; `HotkeyParser`-Grund mit Test vorab; `docs/nexus/checklist.md` und `description.bbcode` auf 0.3.1

**Befund:**
- `HotkeyParserTests.A_key_without_Ctrl_Alt_or_Win_is_refused_because_it_reaches_the_game` erst rot mit dem alten Text, nach dem Fix grün; sonst nirgends „every program“ in `src/`/`tests/`. Commits `0fab8eb` (Code), `5be8921` (Notes, Nexus, TASKS).
- `dotnet build` 0 Warnungen; `dotnet test` **Core 1819** (1818 ohne die lokale Scratch-Datei), **App 1698** (+1), **Import 164**. Ein erster Lauf auf `main` blieb im App-Testhost stehen (> 10 min, CPU still); abgebrochen, danach liefen die App-Tests zweimal in 3–4 s durch, mit `--blame-hang-timeout` ohne Hänger. Nicht reproduziert.
- Vollständigkeit `git log v0.3.0..main`: B10 (4 Commits + Merge `4326513`), S10.0-Befund, `d93bed6` (Doku), `7010eff` (REL-Messung). Nur B10 berührt `src/`/`tests/`; `global.json` und Pakete unverändert.
- „Ein gespeicherter `…+Space`-Hotkey wird mit Hinweis ignoriert“ belegt durch `HotkeyService` (Zeile „Hotkey … ignored: …“), Esc bricht die Aufnahme ab (`SettingsWindow`).

### [x] V2 Build, Merge, Tag, Nachmessung (09.11.2026)

- `publish.ps1 -Version 0.3.1` lokal: ZIP-Größe gegen v0.3.0 lokal (62.121.133 Byte), Einträge, Runtime, `--version`; > 1 MB Abweichung = Stopp
- Martin: Abnahme → `--no-ff` nach `main`, Tag `v0.3.1`, Martin pusht `main` und Tag
- Veröffentlichtes ZIP nachmessen, Notes-Text im Release = Datei

**Befund (lokal, Stand `5be8921`):** ZIP **62.121.374 Byte** (v0.3.0 lokal 62.121.133, +241 Byte), SHA256 gleich `.sha256`, 368 Einträge (wie v0.3.0), `ships/` 106 JSON = 105 + `modules/ts_module.json`, `outlines/` `ship-outlines.json` + `NOTICE.md`, Runtime `Microsoft.NETCore.App` und `WindowsDesktop.App` 10.0.14, `CargoPlan.Cli --version` `0.3.1+5be8921…`.

**Befund (nach dem Push):**
- Merge `--no-ff` `f87b201` (Martin, Befehle von CC), Tag `v0.3.1` annotiert auf `f87b201`, `release/v0.3.1` lokal gelöscht (nie gepusht). Martin: `main` und Tag gepusht, CI und Release-Workflow grün.
- Release `v0.3.1`: kein Pre-release, kein Draft, `releases/latest` → `v0.3.1`, veröffentlicht 09.11.2026 10:59 UTC.

| Prüfung | Soll | Veröffentlichtes ZIP | |
|---|---|---|---|
| SHA256 gegen `.sha256` | gleich | `B6141E9F…72CF43CF`, gleich | ✓ |
| Größe | wie v0.3.0 (CI 64.201.672) | 64.201.711 Byte (+39 Byte zu v0.3.0; lokal 62,1 MB, CI-Kompression wie bei v0.2.0/v0.3.0) | ✓ |
| Runtime | 10.0.14 | `Microsoft.NETCore.App` und `WindowsDesktop.App` 10.0.14 | ✓ |
| Einträge | wie lokal | 368, Eintragsliste gleich dem lokalen `publish.ps1 -Version 0.3.1` | ✓ |
| `ships/` | 105 + 1 Modul | 106 JSON | ✓ |
| `CargoPlan.Cli --version` | `0.3.1+f87b201…` | `0.3.1+f87b20165779767807dd09cb653c6ab574110879` | ✓ |
| Release-Text | = `docs/release-notes/v0.3.1.md` | zeichengleich (ordinal, nach Zeilenende-Normalisierung) | ✓ |
| Settings-Fußzeile | `SC-CargoPlan 0.3.1+…` | Martin | offen |

### [x] V3 Wiki und Abschluss (09.11.2026)

- Wiki nach dem Tag: Overlay-basics `#hotkeys`, Troubleshooting `#a-hotkey-does-nothing` + Known issues, FAQ, `_Footer`
- B10 abhaken, Memory, Grep `RegisterHotKey|WM_HOTKEY` in `docs/` und Wiki
- Martin: GitHub-Issue „Hotkeys do not fire while Star Citizen is in the foreground (v0.1.0–v0.3.0)“, als fixed in v0.3.1 schließen

**Befund:**
- Wiki `26d3996` (Martin pusht): Overlay-basics `#hotkeys` (in front = SC oder CargoPlan, nicht beim Tippen in CargoPlan-Textfeldern; Taste erreicht das Spiel; Admin-Zeile; `Space` aus der Tastenliste, „Space and F12 are reserved“; Satz „Windows or another program already uses it“ raus), Troubleshooting `#a-hotkey-does-nothing` neu (anderes Programm vorne, Admin, `…+Space` nach dem Update, Spiel reagiert mit; „registered“/„swallows“/„taken“ raus) und Admin-Zeile unter Known issues, FAQ Anti-Cheat („asks Windows whether those keys are down … no hook“ statt „normal Windows hotkey service“), Fußzeile v0.3.1. Anker unverändert.
- Grep Wiki nach `has the focus|every program|registered|hotkey service|swallow|\`Space\`|RegisterHotKey|WM_HOTKEY`: 0 Treffer. `docs/`: nur `decisions.md` (D17 + Addendum, D35, D60, D71, D97) – Entscheidungshistorie.
- Offen (Martin): Wiki-Push, GitHub-Issue anlegen und als fixed in v0.3.1 schließen, Settings-Fußzeile 0.3.1. Nexus-Upload am 16.11. mit **0.3.1** nach `docs/nexus/checklist.md` (ersetzt „mit 0.3.0“ in R5).

## K10 Repo-Hygiene (09.11.2026) – Branch `chore/k10-repo-hygiene`

Befunde aus `docs/review/2026-11-pre-p10.md` (Abschnitt 6.2, K10 vor Phase 10). Ein Commit je Befund, Prefix `K10-Fn`.
Nichts, was Phase 10 ändert: kein ContractTracker-Code, kein Plan- oder Settings-Schema.

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:** F6 → Backlog (Schema 4 steckt in 23 `TestShips`, Umbau
größer als der Nutzen); F9 mit Testumleitung des Logs (`AppLog.Path` internal setzbar, `TestLog` per Modul-Initialisierer)
und ohne die bewusst schluckenden catches (`PlanStore.PeekShipId`, `HoloSource` 3×), 19 Stellen; F13 löscht
`LoadPlanner.MoveCrate` trotz „kein LoadPlanner“ (reine Löschung); T6.6 als Duplikat von T9.3 gestrichen; Backlog auch
für F22 und F6; F25 glättet zusätzlich `AssemblyInfo.cs` (9 Format-Treffer der WPF-Vorlage); F26 nimmt 15,0 s statt < 8 s
Wandzeit hin (Takt jetzt `ImportMotherlodeScriptTests`, Backlog).

- [x] F25 `.editorconfig` an den Code, Format-Gate in CI
- [x] F26 FillRows 4 × 6 × 3 als `Slow`
- [x] F12 `ShipChoicesTests` ohne `ships/` (A10)
- [x] F13 `FromRect`, `ReserveLabel`, `MoveCrate` raus
- [x] F9 jede Notice auch ins Log
- [x] F18 TASKS-Häkchen, Backlog F6 F10 F16 F20 F21 F22
- [x] F19 `.gitignore` → `.git/info/exclude` (die Dateien im Root räumt Martin)
- [x] Abschluss: Messtabelle, Review 6.2

| Befund | Commit | Ergebnis |
|---|---|---|
| F25 | `8ac30e9` | `dotnet format --verify-no-changes` 614 → **0**; CI-Schritt `Format` (Windows) |
| F26 | `b67d0d3` | 4 × 6 × 3 in `Slow` (4 statt 2); Ursache: einziges Grid mit Stapelhöhe 3 |
| F12 | `58a857c` | `ShipLoader.LoadAll()` ohne Ordner nur noch in `ShipPictureTests` (Handlauf) |
| F13 | `9141f7a` | `FromRect`, `ReserveLabel`, `LoadPlanner.MoveCrate` raus; Test-Helfer `PlannerMoves` |
| F9 | `3564698` | 19 neue Logzeilen; Tests schreiben nach `%TEMP%`, das echte Log blieb byte-gleich |
| F18 | `2660944` | offene `### [ ]` nur noch T9.3, R5, T10.x, T11.x; 7 Backlog-Zeilen |
| F19 | `f8c2017` | 10 Zeilen aus `.gitignore` nach `.git/info/exclude` |

| Messung (lokal, Release) | vorher | nachher |
|---|---:|---:|
| Tests Core / Import / App | 1 819 / 164 / 1 698 | 1 817 / 164 / 1 701 |
| davon CI (ohne lokalen Scratch-Test) | 1 818 / 164 / 1 698 | 1 816 / 164 / 1 701 |
| `Category=Slow` (Core) | 2 | 4 |
| Core-Wandzeit Default-Run | 17,6 s | 15,1 s |
| `AppLog.Write`-Stellen in `src/` (Zählung Review) | 4 | 23 |
| Build-Warnungen | 0 | 0 |

**Nebenbefund (nicht K10):** `CargoPlan.App.Tests` hängt lokal ab und zu (`--blame-hang-timeout 60s`: 4 von 10 Läufen auf
`main` `da9c49e`, also vor K10). Aktiv beim Hänger u. a. `LookSceneTests.Each_ship_view_key_changes_the_picture`,
`MaxCrateDropdownTests`, `ToolComboBoxStyleTests`. CI war bisher grün. Eigener Task, Entscheidung Martin. → B11

## Bugfix B11 App-Tests hängen (09.11.2026) – Branch `fix/app-tests-hang`

### [x] B11 App-Tests hängen lokal (D98)

**Befund:** reproduziert, **5 von 10 Läufen** (`--blame-hang-timeout 60s`, Release, `main` `8acab33`), jeder Hänger mitten
in der Suite (1 562–1 667 von 1 701 bestanden), also nicht H3. Dumps (`dotnet-dump`: `clrthreads`, `syncblk`, `clrstack -all`;
`cdb`: `~*kc`, `!locks`) zeigen in allen 5 Läufen denselben Zyklus, **H1 belegt** (H2: kein `PushFrame`; H4: kein statischer
Konstruktor), ABBA in `d3d9.dll`:

```
WPF-Render-Thread   hält g_D3DDeviceManager + d3d9-Enum-Sperre, wartet auf d3d9!g_hDLLGlobalCS
  CHwDisplayRenderTarget::Create → CD3DDeviceManager::CreateNewDevice → d3d9!CEnum::CreateDeviceEx → CSwapChain::SetCooperativeLevel
LookSceneTests (STA) hält d3d9!g_hDLLGlobalCS, wartet auf die Enum-Sperre
  ViewPictures.Render → RenderTargetBitmap.Render → CMilViewport3DVisual::RenderContent → CHw3DSoftwareSurface::Create
  → d3d9!CBaseDevice::CreateAdditionalSwapChain → CEnum::CheckExclusiveMode
ShipScene3DTests (STA) hält g_csCompositionEngine, wartet per SyncFlush auf den Render-Thread; alle übrigen WPF-Threads
  (auch SettingsSessionTests auf einem xUnit-Thread) warten auf g_csCompositionEngine oder g_D3DDeviceManager
ToolComboBoxStyleTests.Layout_drop_downs_… in Window.Show() → HwndTarget → SyncFlush: in allen 5 Dumps (das echte Fenster)
```

Ursache: Ein echtes Fenster oder Popup bekommt mit GPU ein D3D9-Hardwaregerät, gleichzeitig rendert ein anderer Test
eine 3D-Szene per `RenderTargetBitmap` über D3D9; die Testklassen laufen parallel. Kein Produktcode beteiligt. CI hat
keine GPU (Vermutung, warum CI nie hing).

**Abhilfe:** `Sta.RenderInSoftware` (`[ModuleInitializer]`) setzt `RenderOptions.ProcessRenderMode = SoftwareOnly` für den
Testprozess (D98, CLAUDE.md A12), Commit `0f29d5b`. Collection `WPF` als geprüfte Alternative in D98.

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:** Vorversuch mit `SoftwareOnly` vor dem Collection-Umbau
(0 Hänger → die Zeile statt der Collection); die Zeile in `Sta` statt im `TestLog`-Initialisierer; D98 für B11, T10.1
und Review F24 auf D99 umgestellt.

| Messung (lokal, Release, `--no-build`) | vorher | Vorversuch | nachher |
|---|---:|---:|---:|
| Läufe / Hänger | 10 / **5** | 10 / 0 | 20 / **0** |
| App-Dauer vollständiger Läufe (TRX, Median) | 3,52 s | 3,59 s | 3,60 s |
| Wandzeit `dotnet test` (Median) | 4,19 s | 4,26 s | 4,27 s |
| `ShipPictureTests` mit/ohne die Zeile | – | 728 / 728 Bilder identisch | – |
| Tests Core / Import / App | 1 817 / 164 / 1 701 | – | 1 817 / 164 / 1 701 |

Rohdaten, Stacks und Dumps lokal unter `out/b11/` (nicht im Repo).

**Nebenbefund, auf Martins Wunsch im selben Branch behoben:** 2 der 20 Nachher-Läufe mit je einem roten Log-Test
(`PlanStoreTests`/`SettingsStoreTests.Broken_file_writes_its_notice_to_the_log`): `TestLog.Text()` las mit
`File.ReadAllText` (`FileShare.Read`) außerhalb des `lock` von `AppLog`; ein paralleler Schreiber ließ das Lesen werfen
(`IOException`) oder verlor seine Zeile (`AppLog` schluckt bewusst). Seit K10-F9. Jetzt liest `TestLog.Text()` mit
`FileShare.ReadWrite` (die Rotation bei 1 MB bliebe blockiert, ein Testlauf schreibt höchstens 23 KB): **20 von 20 Läufen
grün**, App-Dauer 3,58 s (TRX-Median).

## Phase 10 – OCR (v0.4.0)

CargoPlan liest Verträge selbst (Hotkey, ein Scan = ein Vertrag), der ContractTracker-Import entfällt. Grundlage ist der Befund S10.0 unten. Die Reihenfolge ist fest: T10.4 kommt nicht vor T10.3.

### [x] S10.0 Spike „Eigenes OCR“ (09.11.2026, abgenommen 09.11.)

**Branches:** Werkzeug auf `spike/s10-ocr` (Worktree `..\sc-cargoplan-s10`, `tools/OcrProbe`, nicht in der `.sln`, wird nach der Abnahme gelöscht), dieser Befund auf `spike/s10-ocr-finding`. Samples außerhalb des Repos (`~/source/sc-ocr-samples`, CIG-Material, nicht MIT).
**Frage:** Kann v0.4.0 Verträge mit eigener OCR lesen (Hotkey, ein Scan = ein Vertrag) und den ContractTracker-Import ersetzen? Kandidat: **RapidOcrNet 4.2.0** (Apache 2.0) mit PP-OCRv5 Latin, ONNX Runtime 1.31.1, SkiaSharp. Die frühere Zeile T10.3 (Windows.Media.Ocr) ist mit dem Doku-Commit nach diesem Befund entfallen; Phase 10 ist jetzt OCR, Velopack und winget sind Phase 11.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. „Terminal“ = mobiGlas OFFERS, „Manager“ = mobiGlas ACCEPTED. Probe-Tasten F8/F9 statt F3 (CT hält F3).
2. Der bestehende Parser liest keinen Abholort: Pickup und Reward kommen aus Spike-Code und werden getrennt ausgewiesen.
3. `InternalsVisibleTo OcrProbe` und Vortice (D3D11 für WGC/DDA) nur auf dem Spike-Branch.
4. Zeilen: zuerst ein Block = eine Zeile nach Oberkante (wie der CT-Rohtext). Der zweite Lauf mit Spaltentrennung war nötig (`+cols`, s. u.).
5. Erkennungsauflösung: Das Vollbild braucht die volle Seitenlänge (`@max`). Mit der Paketvorgabe 1024 px wurden 0 von 13 Feldern gelesen. Der ROI-Ausschnitt braucht das nicht.
6. Haupt-Satz **3440×1440** (Martins Monitor): 42 Aufnahmen, davon **20 Verträge = 10 Paare** OFFERS → ACCEPTED mit Wahrheit von Hand (Martin). Vier Tippfehler in der Wahrheit am Bild korrigiert (c02, c11, c18, c20). **4K ungeprüft** (kein Monitor dafür).
7. Live-Lauf mit 10 statt 20 Auslösungen je Ansicht.
8. Zwei Max-Größen-Formulierungen, die der Parser nicht kennt, als Spike-Code gemessen (`+max2`), getrennt ausgewiesen.
9. `RecMaxDegreeOfParallelism` = 8 angefasst, weil raw/ROI schon offline das p95-Ziel verfehlte (1652 ms).
10. **1080p-Nachmessung ohne Tipparbeit für Martin:** SC im Fenster auf 1920 × 1080. SC rendert intern 1080p und skaliert das Bild auf 2560 × 1440 mittig ins 3440er-Fenster. Der 16:9-Bereich wird per Lanczos auf 1920 × 1080 zurückskaliert, das ist eher unschärfer als nativ, also konservativ. 10 neue Paare (`set-1080s`). **Die Wahrheit hat Claude an vergrößerten Ausschnitten der Originale abgelesen**, unabhängig von der OCR.

**Gates (Martin hakt):**
- [x] **G1 Capture:** Alle vier Wege liefern 3440×1440 mit SC vorn, borderless und „Vollbild“, kein schwarzes Bild. GDI BitBlt ist am schnellsten (33–41 ms). Vorbehalt: Das SC-Vollbild ist vermutlich fullscreen-optimiert, also faktisch borderless.
- [x] **G2 ≥ 95 % Felder:** **100 % (158/158)** mit raw + ROI + Spaltenreihenfolge + `+max2`; Pickup 28/28, Reward 20/20. Der bestehende Parser allein erreicht 95,6 % (ROI + Spalten) bzw. 90,5 % (Vollbild, Oberkante). **1080p: 98,1 % (155/158) mit up2-gray + ROI.** Die 3 fehlenden Felder stammen aus einer Aufnahme während der Textanimation (c06); ohne sie sind es 100 %. raw allein erreicht bei 1080p nur 94,9 %. **Empfohlen für beide Auflösungen: up2-gray + ROI.** Bei 3440×1440 ebenfalls 100 %.
- [x] **G3 p95 ≤ 1500 ms über dem Spiel:** **1184 ms** (ACCEPTED) / **1188 ms** (OFFERS), Median 1059 / 1112 ms, n = 10 + 10, Taste bis geparster Vertrag. Der erste Scan nach dem Start brauchte 1311 ms. Gemessen mit raw. Die empfohlene Variante up2-gray mit Parallelität 8 ist offline gleich schnell (p95 1374 ms bei 3440×1440, 1299 ms bei 1080p; raw: 1332 ms); live nicht gemessen.
- [ ] **G4 Terminal = Manager:** Der Lesepfad ist gleich (ein ROI für beide Ansichten, ein Parser, beide 100 %). **Der Inhalt nicht:** Im Haupt-Satz hat das Spiel beim Annehmen in 10 von 10 Paaren die SCU neu gewürfelt (Tabelle unten), im 1080p-Satz in 6 von 10. Unverändert blieben dort vier Verträge mit nur einer Lieferung (Quartz, Silicon, zweimal Waste). Martins Einschätzung: Spielbug.
- [x] **G5 Schlüssel eindeutig:** Alle 20 Schlüssel aus der OCR sind gleich der Wahrheit, ohne Kollision. Ein Schlüssel **ohne SCU** (Pickup + Ziele + Waren + Max [+ Reward]) verbindet 10 von 10 Paaren, ebenfalls ohne Kollision.
- [ ] **G6 Anker ≥ 95 %:** **nicht erreicht.** Ankerausschnitt raw + `+cols+max2`: 90,5 % der Felder (ACCEPTED 97,5 %, OFFERS 83,5 %). Ausschnitt aus dem `@max`-Vollbild bei OFFERS: nur 3 von 10 so gut wie das Vollbild. 1080p: 91,8 % (up2-gray). Anker sind unnötig: Das mobiGlas liegt bei 21:9 und 16:9 im zentrierten 16:9-Bereich, also passt **ein ROI relativ zu diesem Bereich** für beide Auflösungen und beide Ansichten (s. u.).

**Capture (F1)**, je ein Bild pro Weg × Modus × Ansicht, SC vorn:

| Weg | borderless ms (ACCEPTED / OFFERS) | „Vollbild“ ms (ACCEPTED / OFFERS) | Befund |
|---|---|---|---|
| GDI `BitBlt` (Bildschirm-DC, Client-Rechteck) | 36 / 41 | 37 / 41 | Bild |
| GDI `PrintWindow(PW_RENDERFULLCONTENT)` | 42 / 43 | 41 / 46 | Bild |
| DXGI Desktop Duplication | 48 / 53 | 49 / 54 | Bild |
| Windows.Graphics.Capture (`IsBorderRequired=false`, Zugriff „Allowed“) | 96 / 149 | 79 / 151 | Bild, langsamer |

Sample-Satz mit BitBlt: 42 von 42 Bildern, Median 33–34 ms. **GDI reicht, die Frage Vortice vs. eigenes D3D11-Interop entfällt.**

**Genauigkeit (F2)**, 20 Verträge, 158 Felder (je Lieferung Ziel, Ware, SCU; je Vertrag Max-Größe), 3440×1440:

| Lesart | Region | Felder | Pickup | Reward |
|---|---|---|---|---|
| raw, Oberkante | Vollbild (1024) | 18,9 % | 27,8 % | 30 % |
| raw `@max`, Oberkante | Vollbild | 90,5 % | 100 % | 100 % |
| raw `@max` + Spalten + `max2` | Vollbild | 97,5 % | 100 % | 100 % |
| raw + Spalten | ROI | 95,6 % | 100 % | 100 % |
| **raw + Spalten + `max2`** | **ROI** | **100 %** | **100 %** | **100 %** |
| up2 / up2-gray / up2-gray-inv + Spalten + `max2` | ROI | 100 % | 100 % | 100 % |
| raw + Spalten + `max2` | Ankerausschnitt | 90,5 % | 93,1 % | 40 % |

- **Die Erkennung selbst war nie der Engpass.** Ziel, Ware und SCU wurden in allen 20 Bildern richtig gelesen, auch unter einem Lichtstreifen quer über dem Text (c17/c18). Die Fehler stammten aus der Zeilenreihenfolge und aus dem Parser.
- **Zeilenreihenfolge:** Sortiert man nach Oberkante, verschränken sich Prosa (links) und Ziele (rechts). „4“ und „SCU or smaller“ liegen dann nicht mehr nebeneinander, und der Reward-Betrag landet vor „Reward“. Die Spaltenreihenfolge (`+cols`) trennt die Spalten an den linken Kanten von „DETAILS“ und „PRIMARY OBJECTIVES“ und liest innerhalb einer Spalte zeilenweise von links nach rechts.
- **Parser-Lücken (`+max2`), nur Direct-Hauls:** „At most the containers will be 8 SCU in size.“ und „… their ship couldn't handle 16 SCU containers …“.
- **Vorverarbeitung bringt nichts:** Mit ROI liefern alle vier Varianten dasselbe. raw ist am schnellsten.
- Ein ROI deckt beide Ansichten ab (mobiGlas-Panel an derselben Stelle): `3440x1440: [1300, 210, 1560, 1000]`.

**1080p-Nachmessung**, 20 Verträge (10 Paare), 158 Felder, Spalten + `max2`:

| Lesart | Region | Felder | Pickup | Reward | offline Median / p95 |
|---|---|---|---|---|---|
| raw | ROI | 94,9 % | 96,4 % | 100 % | 1345 / 1467 ms |
| raw `@max` | ROI | 97,5 % | 96,4 % | 100 % | 1457 / 1556 ms |
| **up2-gray, Parallelität 8** | **ROI** | **98,1 %** | 96,4 % | 100 % | **1231 / 1299 ms** |
| up2-gray-inv | ROI | 96,8 % | 96,4 % | 100 % | 1439 / 1544 ms |
| raw `@max` | Vollbild | 97,5 % | 96,4 % | 100 % | 1947 / 2109 ms |
| up2-gray | Ankerausschnitt | 91,8 % | 96,4 % | 100 % | – |

- Der einzige Fehler mit up2-gray ist **c06**: Die Aufnahme kam, während das Spiel die Ziele noch einblendete („Deliver 0/9 SCU of Silicon“, ohne Ziel und ohne Abholort). Bei 3440×1440 war c01 ebenso angeschnitten („Station at H“), aber das Ziel war schon lesbar. Das ist kein OCR-Fehler, der Produktpfad muss aber damit umgehen (offene Frage).
- raw verliert bei 1080p kleine Schrift: zwei Max-Größen (c02, c05) und eine Lieferung (c07). up2-gray liest sie.
- ROI `1920x1080: [645, 157, 1170, 750]` = das 3440er-ROI, umgerechnet über den zentrierten 16:9-Bereich (x 440 … 3000 bei 3440×1440). Dass es ohne Nachjustieren trifft, belegt die Lage relativ zu diesem Bereich.
- Modelle laden: 182 ms.

**Zeit und Speicher (F3):**

| Messung | Median | p95 |
|---|---|---|
| Offline raw/ROI, `RecMaxDegreeOfParallelism` 1 | 1380–1425 ms | 1652–1658 ms |
| Offline raw/ROI, Parallelität 2 / 4 / 8 | 1260 / 1273 / 1272 ms | 1424 / 1395 / 1332 ms |
| Offline raw `@max` Vollbild | 2200 ms | 2412 ms |
| **Über dem Spiel raw/ROI p8**, Taste → Vertrag (Capture 42 ms, OCR ≈ 1000 ms, Parse < 1 ms) | **1059–1112 ms** | **1184–1188 ms** |

- Modelle laden: 225 ms Median (bis 420 ms), einmal beim Start.
- **Speicher über dem Spiel:** Das Working Set wuchs in 20 Scans von 407 auf 684 MB, die Private Bytes am Ende auf 802 MB, ohne Plateau.
- Platte: Modelle 14 MB, `onnxruntime.dll` 17 MB, `libSkiaSharp.dll` 11 MB, verwaltete DLLs etwa 1 MB, zusammen ≈ 43 MB unkomprimiert.

**OFFERS → ACCEPTED (F4)**, Wahrheit gegen Wahrheit:

| Paar | Beim Annehmen geändert |
|---|---|
| c01 → c02 | Faithful Dream Processed Food 7→6, Thundering Express Pressurized Ice 3→4, High Course Processed Food 3→5 |
| c03 → c04 | Quartz (Raw) 104→110 |
| c05 → c06 | Quantum Fuel 124→86, Hydrogen Fuel 105→141, Ship Ammunition 141→90 (Summe 370→317) |
| c07 → c08 | Silicon 10→9 |
| c09 → c10 | Waste 11→8 |
| c11 → c12 | Iron (Ore) 101→105 |
| c13 → c14 | Pressurized Ice 4→6 und 3→6, Processed Food 4→6 |
| c15 → c16 | Stims 4→3, 2→3 |
| c17 → c18 | Waste 9→7 |
| c19 → c20 | Pressurized Ice 7→6, 7→6, Processed Food 6→7, 6→4 |

Gleich blieben Titel, Auftraggeber, Reward, Abholort, Ziele, Waren und Max-Größe. Bei der ersten Durchsicht ohne Wahrheit zeigten auch die übrigen 11 Paare des Satzes andere Mengen. **Folge:** Der Vorschau-Scan ist eine Schätzung. Die Mengen gelten erst nach dem Scan im Manager, der Schlüssel ohne SCU ordnet ihn der Vorschau zu.

**Inhaltsschlüssel (F5):** Pickup + sortierte (Ziel, SCU) + Waren + Max-Größe in B1-Schlüsselform: 20 von 20 stabil, keine Kollision, mit und ohne Reward. Ohne SCU: 10 Gruppen = 10 Paare. Vorbehalt: 10 Verträge aus Hurston/Covalex sind statistisch dünn. c03 und c11 unterscheiden sich nur in der Ware, und laut D39-Addendum gibt es Waste-Verträge, die sich nur im Reward unterscheiden. Der Reward gehört also in den Schlüssel.

**Hotkeys (Befund unterwegs):** Mit SC im Vordergrund kommt **kein** `RegisterHotKey`-Hotkey an. Das gilt thread- und fenstergebunden, mit offenem und geschlossenem mobiGlas, auch für Ctrl+Alt+K; auf dem Desktop kommt er an. Damit funktionieren auch **CargoPlans eigene Hotkeys (D17) im Spiel nicht**, ein v0.3.0-Bug. `GetAsyncKeyState`-Polling (10 ms, kein Hook, Präzedenz D45) sieht jede Taste, die Taste erreicht dann aber auch das Spiel. Fn+F8 sendet auf Martins Tastatur keinen VK.

**Inventur CT-Abhängigkeiten (Schritt 6)**, `git grep` nach `ContractTracker|Watermark|Gone|Detached|contracts.db`, ohne Änderung:

| Bereich | Dateien | Zeilen |
|---|---|---|
| `src/CargoPlan.App` | 37 | 279 |
| `src/CargoPlan.Core` | 14 | 145 |
| `src/CargoPlan.Import` | 9 | 53 |
| `src/CargoPlan.Cli` | 2 | 8 |
| `tests/CargoPlan.App.Tests` | 32 | 341 |
| `tests/CargoPlan.Core.Tests` | 33 | 149 |
| `tests/CargoPlan.Import.Tests` (dazu 18 Fixtures) | 9 | 73 |
| `docs/` (19), README, CLAUDE, CONTRIBUTING, TASKS, `.github`, `.gitignore`, `outlines` | 26 | 341 |

**Der Planer selbst trägt die CT-Identität:** `LoadPlanner.cs` hat 50 Treffer, davon 35 `Watermark` (Lieferidentität D81). Dazu kommen `ContractSync`, `ContractSource` und `PlanFileDto`. Der Ausbau betrifft also auch Core und das Plan-Dateiformat, nicht nur `CargoPlan.Import`.

**Empfehlung für den Phase-10-Schnitt** (Martin entscheidet):
1. **Bugfix Hotkeys vorab:** D17-Hotkeys per Polling statt `RegisterHotKey`, mit D-Eintrag, eigener Branch. Das wirkt sofort für v0.3.x und ist die Grundlage für den Scan-Hotkey.
2. **Lesepfad in `CargoPlan.Import`, Tests zuerst:**
   - Zeilenbildung aus OCR-Blöcken (Spalten, Bildschirmzeilen)
   - Parser-Erweiterung um die zwei Max-Formulierungen, Pickup-Parser (neu), Reward aus Label und Betrag in einer Zeile
   - Fixtures als OCR-Text, nicht als Bild
3. **Capture + OCR in der App:**
   - GDI BitBlt auf das Client-Rechteck, ROI relativ zum zentrierten 16:9-Bereich
   - RapidOcrNet mit up2-gray (2× hoch, Graustufen), Parallelität 8, Modelle beim Start laden
   - Textanimation abwarten: erneut aufnehmen, bis zwei Scans gleich sind, oder eine Lieferzeile ohne Ziel als „noch nicht fertig“ werten
   - Speicher im Langlauf messen
4. **Identität und Plan-Schema:** Inhaltsschlüssel ohne SCU, mit Reward, ersetzt CT-Id und Watermark. Der Manager-Scan überschreibt die Mengen der Vorschau. Schema-Sprung, CT-Ausbau in Core, App und Doku.
5. **Messung 4K und mit anderer UI-Skalierung** vor dem Release, sobald jemand mit diesem Setup testet. Dafür reicht der Produktpfad mit Logausgabe.

**Offene Fragen:**
- **T10.3, F3 belegt (K2):** Wer CT nach v0.4.0 weiterlaufen lässt, hält F3. Mit Polling gibt es kein „belegt“ mehr, beide Programme reagieren auf F3. Möglichkeiten: Notice beim Start, wenn CT läuft, oder eine andere Default-Taste. Kein Spike-Code.
- **Vortice vs. eigenes D3D11-Interop (P3):** entfällt, GDI reicht (G1).
- **Re-roll beim Annehmen:** Haben die CT-basierten Pläne (CT las OFFERS) am Aufzug andere Mengen gezeigt als geliefert wurden? (Martin). Behebt CIG den Bug, wird die Vorschau exakt. Der Entwurf oben funktioniert in beiden Fällen.
- **ROI bei 4K, 32:9, 16:10 und anderer UI-Skalierung:** ungeprüft. Bei 21:9 und 16:9 trifft dasselbe ROI relativ zum zentrierten 16:9-Bereich. Ob 16:10 (Bereich durch die Breite begrenzt) und eine geänderte HUD-Skalierung mitspielen, ist offen. Die Anker bleiben der Rückfall (G6 verfehlt).
- **Textanimation:** Ein Scan direkt nach dem Klick auf einen Vertrag kann halb eingeblendeten Text lesen (c06). Wie lange dauert die Animation, und reicht ein zweiter Scan nach ≈ 300 ms?
- **Speicher:** Wächst das Working Set weiter (nach dem ersten Scan ≈ 7 MB pro Scan) oder pendelt es sich ein? Dispose von Bitmaps und Frames prüfen.
- **Abdeckung:** nur Hurston, nur Covalex, zwei Waste-Verträge, keine Pyro-/Red-Wind-Formulierungen im Satz. Weitere Formulierungen kommen wie bei B9 über Meldungen.
- Ein FPS-Overlay schreibt Text oben rechts; mit ROI irrelevant.

### [ ] T10.1 Capture + OCR (`CargoPlan.Ocr`)

- Neues Projekt neben `CargoPlan.Import`.
- `ScreenCapture` per GDI `BitBlt` (S10.0 G1).
- Relatives 16:9-Center-ROI für alle Seitenverhältnisse (S10.0, Anker entfallen).
- RapidOcrNet mit Vorverarbeitung **up2-gray** als Konstante, ONNX-Session einmal pro Prozess vorgewärmt.
- `ScanContractSource` als Klasse, Tests direkt gegen sie – kein Interface mit einer Implementierung (A4, Review 2026-11-pre-p10 6.1 Frage 3).
- **F22 (Review):** `App.OnStartup` (152 Zeilen) vor der OCR-Verdrahtung in `Compose*`-Methoden schneiden – reine Umstellung ohne Verhaltensänderung, eigener Commit, damit der Diff lesbar bleibt.
- **Gates:**
  - p95 Hotkey → geparster Vertrag ≤ 1500 ms **über dem Spiel** mit up2-gray und Parallelität 8 (im Spike nur offline gemessen)
  - **100 Scans, das Working Set muss ein Plateau erreichen** (Spike: Working Set 407 → 684 MB, Private Bytes bis 802 MB in 20 Scans ohne Plateau = Leak)
  - Frame-Zeit über dem Spiel unverändert
- Hier entstehen die NOTICE-Datei (RapidOcrNet, PP-OCRv5-Modelle, ONNX Runtime, SkiaSharp, Clipper2) und D99 „Contracts are read by CargoPlan's own OCR; the ContractTracker import is removed“.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. **Teilung:** T10.1a = reine Bildverarbeitung, offline testbar (Gate 4). T10.1b = alles, was SC laufen haben muss (F22, Capture, Verdrahtung, `--scan-probe`, Gates 1–3), eigener Branch `feat/t10-1b-capture`. a wird nach Abnahme sofort `--no-ff` nach `main` gemergt; T10.2 kann nach a starten, T10.3 braucht b.
2. **Trennlinie:** `CargoPlan.Ocr` ist `net10.0` ohne Windows-API (ROI, Vorverarbeitung, OCR, Zeilenbildung, `ScanContractSource.Read(SKBitmap)`). `ScreenCapture` und die Fenstersuche liegen in `src/CargoPlan.App/Win32/` neben der bestehenden `NativeMethods`. Die Cli bleibt `net10.0`.
3. Ocr ohne Core-Referenz (A4), bis T10.2 sie braucht.
4. Modelle aus dem RapidOcrNet-Paket als `None`-Items (`ExcludeAssets="build"`, das Paket-Target greift nicht transitiv) nach `models/v5/` neben der Exe; NOTICE als `models/NOTICE.md`.
5. ONNX Runtime 1.31.1 explizit referenziert (Spike-Stand), SkiaSharp nur transitiv (3.119.1; RapidOcrNet ist gegen 3.x gebaut).
6. Zeilenbildung: Blöcke einer Bildschirmzeile werden je Spalte links → rechts mit Leerzeichen zu einer Textzeile verbunden (`DeliveryLine` ist auf eine Zeile verankert).
7. `CargoPlan.Import.Tests` bekommt `default.runsettings` (`Category!=Slow&Category!=Timing`); der Hand-Test mit dem Parser liegt dort.
8. Dieser Textfix kam nach dem Plan statt davor (Plan Mode).

- [x] **T10.1a** Ocr-Projekt, ROI, up2-gray, OCR, Zeilenbildung, CLI `scan <png>`, NOTICE, D99 – Gate 4 (Build 0 Warnungen, Tests grün, `dotnet format` 0, `publish.ps1` mit neuer ZIP-Größe)
  - **Befund (09.11.2026, Branch `feat/t10-1-ocr`):**
    - **Gate 4:** Build 0 Warnungen; Tests Core 1 817 · Import 164 · App 1 701 · Ocr 32 grün (Hand-Tests gefiltert); `dotnet format --verify-no-changes` 0. `publish.ps1`: ZIP **86 399 140 Byte** (v0.3.1: 62 121 374, **+24,3 MB, +39 %**), 386 Einträge (v0.3.1: 368), Ordner 178 MB. Die Zahl geht in die Release-Notes von v0.4.0.
    - **Hand-Test über die S10.0-Aufnahmen** (bestehender Parser, Release, SC lief im Hintergrund): 3440×1440 **138/138** Lieferfelder, 1080p **135/138** (nur c06, Textanimation). Offline Median 1 171 / 1 240 ms, p95 1 257 / 1 303 ms (S10.0 offline up2-gray p8: p95 1 374 / 1 299 ms).
    - **Speicher offline** (`OcrMemoryTests`, 100 Lesevorgänge): 154 MB nach Laden und Vorwärmen, 554 MB nach dem 2. Lesevorgang, langsam bis 669 MB bei etwa 60, dann flach (650–669 MB, Private Bytes 778–798 MB). Sieht nach der Arena von ONNX Runtime aus; Gate 2 über dem Spiel ist T10.1b.
    - **Ziel-Symbol** (Fund im Hand-Test): Mit K2 hing „0“/„L“ vor „Deliver“, und der Parser verlor in 4 von 20 Verträgen je Satz eine Lieferung. Entscheidung Martin: Ein erster Block mit höchstens 2 Zeichen in einer etwa quadratischen Box (≤ 1,3 × Höhe; gemessen 1,02–1,20) bleibt eine eigene Zeile. Die Zwei-Boxen-Lieferung, die K2 vorbeugen soll, wurde in den 40 Aufnahmen **nicht beobachtet**; K2 bleibt vorsorglich.
    - Das Gray8-Bild liest RapidOcrNet direkt (`OcrUtils.SubtractMeanNormalize`).
    - Eine Lesung hat Grundkosten fast unabhängig von der Bildgröße (fast leerer Rahmen ≈ 0,9 s). Die Scan-Tests nutzen deshalb 960×540 und bleiben unter der Regel von 1,3 s.
- [x] **T10.1b** F22, `ScreenCapture`, Fenstersuche, Engine-Vorwärmen beim Start, `--scan-probe` – Gates 1–3
  - **Abnahme (Martin, 11.11.2026):** Gate-1-Schwelle **1 500 ms p95** bleibt und gilt verbindlich für T10.3, gleiche Methode (Taste → Rohtext, SC läuft, je Ansicht), dort **n ≥ 20 je Ansicht** (bei n = 10 ist p95 praktisch das Maximum). Reserve unangetastet: ORT-Threads, Priorität. Bestätigt vor dem Merge: B1 Test für den Fund f7adca0 (`A_scan_before_the_warm_up_started_waits_for_it`, im Fix-Commit, vorher rot); B2 ROI-PNG des 125-%-Laufs gesehen und richtig. D100-Blinken live mit dem echten Overlay beurteilt (Gate-1-Lauf).
  - **Präzisierung in der Planphase, ratifiziert durch Freigabe:** `--scan-probe [N]` = F8 ein Scan, F9 N Scans (Default 100) in einer Instanz; Testmatrix drei Fenstermodi plus 125 %; `HotkeyPoller<TAction>` statt eigener Flanke in der Probe; Client-Rechteck per `GetClientRect` + `ClientToScreen`, DWM-Bounds nur als Logspalte; K1 `Load(folder)` ohne geteilten Zustand, K2 Warten auf das Warmup höchstens 15 s, K3 eigener Schalter `--scan-load`.
  - **Befund (10./11.11.2026, Branch `feat/t10-1b-capture`, Release, SC 3440×1440 borderless, Monitor 110 %):**
    - **Gate 1** (Taste → Rohtext, `scan-probe.log`), alle Scans gelesen:

      | Lauf | n | Capture inkl. D100 | Preprocessing | OCR | Taste → Rohtext Median / p95 |
      |---|---|---|---|---|---|
      | OFFERS, F8 | 10 | 21,9 / 52,8 ms (1. Scan) | 40,3 / 43,2 ms | 1 185 / 1 242 ms | **1 255 / 1 304 ms** |
      | ACCEPTED, F8 | 10 | 23,6 / 33,0 ms | 43,9 / 53,2 ms | 1 218 / 1 320 ms | **1 296 / 1 391 ms** |
      | F9-Serie 1 (Dauerlast) | 100 | 23,1 / 27,0 ms | 48,3 / 53,6 ms | 1 329 / 1 428 ms | 1 407 / 1 511 ms |
      | F9-Serie 2 (Dauerlast) | 100 | – | – | 1 322 / 1 395 ms | 1 398 / 1 472 ms |

      Die 3,2 s aus T10.1a waren eine Einzelmessung; einzelne Auslösungen liegen unter dem Zielwert 1 500 ms, kein Tuning nötig (ORT-Threads, Priorität unangetastet). Schwelle legt Martin fest.
    - **Gate 2** (210 Scans in einem Prozess): Working Set 274 MB nach dem Warmup, ≈ 700 MB nach 10 Scans, ab etwa 70 ein Band von 773–892 MB (Mittel 824), Steigung über Scan 71–210 **0,03 MB/Scan**; Private Bytes 881–1 003 MB, −0,02 MB/Scan. Erste Serie allein zeigte +1 MB/Scan über 71–110 (Pendeln, die zweite Serie entschied). **GDI-Objekte konstant 35.** Offline (T10.1a, ohne WPF) 650–669 MB.
    - **Gate 3** (`sc-gate3.ps1`, `--measure-frames <Ziel> --over-game`, je 3 Läufe, Median, Anteil später Frames): `hull_c` main 8eb2141 **7,1 %** → Branch mit warmer Engine **5,5 %** (−1,6 pp); `reclaimer` **10,7 %** → **10,9 %** (+0,2 pp). Gate ≤ +2 pp erfüllt. Mit `--scan-load` (20–22 Scans je 30 s, nicht Teil des Gates): `hull_c` 28,1 % bei 93 fps, `reclaimer` 5,9 % bei 57,5 fps – **verfälscht**: das mittige Messfenster liegt über dem ROI, D100 schaltet es je Scan auf Opacity 0 und zurück, das erzwingt zusätzliche Renderdurchläufe (Median 8,33 statt 16,67 ms).
    - **Warmup** (Logzeile `OCR ready`, ab dem ersten Frame): 1 177 ms (Laden 692, Dummy-Lesevorgang 485) beim ersten Start mit SC, danach 677 und 1 054 ms; Start 1,2–2,4 s nach dem Prozess. Working Set 157 → 270 MB.
    - **Overlay über dem ROI → D100:** Messung über SC (20 Versuche je Variante) und Martins Entscheidung **Opacity 0 mit einem `DwmFlush`**, nur für Fenster, die das ROI schneiden, Rückstellung auch bei Ausnahme. Im Gate-1-Lauf lag das Overlay bei allen 10 OFFERS-Scans über dem ROI, auf keinem Bild zu sehen. Martin: das kurze Blinken ist eine gute Rückmeldung, dass gescannt wird.
    - **Fenstermodi** (je 1–2 Scans, ROI-PNG geprüft): Vollbild und Borderless identisch; **Fenster**: Client (8, 33), DWM-Bounds (7, 0) – über die DWM-Bounds läge das ROI 33 px zu hoch, über den Client sitzt es; SC blieb im Fenstermodus bei 3440×1440. **125 %** laut Martin (nicht abgemeldet): ROI identisch; das Log belegt die Skalierung nicht, weil SC `GetDpiForWindow` 106 meldet (Systemwert aus der Anmeldung).
    - **DPI gemessen:** der UI-Thread läuft `system`-aware (WPF ohne Manifest), der Capture-Thread `per-monitor`; bei 110 % saß das ROI pixelgenau.
    - **ZIP** (`publish.ps1`): 86 426 701 Byte (T10.1a 86 399 140, +27,6 KB), 386 Einträge, Ordner 178 MB.
    - **Gate 4:** Build 0 Warnungen; Tests Core 1 817 · Import 164 · App 1 731 · Ocr 34 grün; `dotnet format --verify-no-changes` 0; CI `main` 8eb2141 grün (Branch noch nicht gepusht).

### [ ] T10.2 Parser-Anbindung, Identität, plan.json 10

- Rohtext durch den bestehenden Parser (B1, B9, D96). **Neu: Abholort-Parsing** („Collect … from <Ort>“, bisher CT-Spalte).
- **Inhaltsschlüssel ohne SCU, mit Reward** (S10.0 G5; das Spiel würfelt beim Annehmen neu, 6 von 10 Paaren).
  - Ein Re-Scan desselben Schlüssels **aktualisiert** Mengen und Kistenliste, statt zu duplizieren; Notice `Updated #n · quantities changed`.
- Ein Scan gilt erst, wenn zwei Lesungen im Abstand von etwa 300 ms denselben Block liefern, sonst Notice „Contract text still loading – scan again“ (Spike-Fall c06).
- `plan.json` 9 → 10:
  - `source: scan`; `scanNo` ersetzt die CT-Id in Legende und Editor
  - kein Rohtext in der Datei; Rohtext und Ergebnis ins rollierende `%LocalAppData%\SC-CargoPlan\scans.log` (letzte 50)
- Ein Scan ist ein Undo-Schritt (K2.4-Addendum).
- **Rohtext aus T10.1a, was der Parser können muss** (Hand-Test `OcrSampleTests`, 40 Aufnahmen):
  - **Ziel-Symbol:** Vor jedem Ziel zeichnet das mobiGlas ein Symbol, das die OCR als eigenen Block „0“ oder „L“ liest. `ReadingOrder` gibt es als eigene Zeile aus (wie der CT-Rohtext). Das ist ein bekanntes OCR-Artefakt, kein Fehler: **Parser-Fixtures sollen eine solche Symbolzeile enthalten.**
  - **Reward:** In Spaltenreihenfolge steht „Reward 81,250“ in einer Zeile in der DETAILS-Spalte nach der Prosa. Der Parser sucht ihn nur am Textanfang (CT-Form) und las ihn in 0 von 40 Aufnahmen.
  - **Max-Größe:** Die zweite Formulierung („At most the containers will be 8 SCU in size.“, `+max2`) fehlt noch.
  - Lieferzeilen brechen wie im CT-Rohtext um („… to High Course Station“ / „at Hurston's L5 Lagrange point.“); das liest der Parser heute schon.
- **Rohtext aus T10.1b** (`scan-probe.log` vom 11.11., 227 Scans über dem Spiel, liegt in Martins `%LocalAppData%\SC-CargoPlan`, CIG-Material, nicht ins Repo):
  - **„O/“ statt „0/“:** In 254 von 877 Lieferzeilen liest die OCR den Zähler als Buchstaben („Deliver O/115 SCU of …“), vor allem in ACCEPTED. ~~Der Parser muss `[0O]/` akzeptieren.~~ Korrektur T10.2a: Das Muster `\S*/` liest sie schon, 877 von 877 Lieferzeilen; kein Verlust, nur ein Wächtertest.
  - **ACCEPTED** beginnt mit einer Zeile „ABANDON“, endet mit „SHARE TRACK“ (9×) oder „SHARE UNTRACK“ (201×) und hat „Contract Deadline N/A“ statt „Contract Availability 2h 1m“; **OFFERS** endet mit „ACCEPT OFFER“. Die Titelzeile („Member Rank - Small Cargo Haul“) steht vor „DETAILS“.
  - Die Zielsymbol-Zeilen „0“ (45×) und „L“ (80×) kommen auch über dem Spiel, immer vor einer „Deliver“-Zeile.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. **Teilung:** T10.2a (`feat/t10-2a-parser`: Import, Cli, Tests, offline prüfbar) und T10.2b (`feat/t10-2b-identity`: Core + App – plan.json 10 mit F4/F24, Scan-Identität, „Scan in den Plan“ als Undo-Schritt, Re-roll, Vollständigkeit, `AppFolders`/F14, `scans.log`). b bekommt nach a einen eigenen Plan.
2. Ocr bleibt Bild → Text; geparst wird in Import, zusammengesetzt in App und Cli.
3. **F7:** Parser, `ContractText`, `ParsedContract`, `ParsedDelivery` nach `Import/Contracts` (`CargoPlan.Import.Contracts`); `Stops()`/`Item()` aus dem Reader als `ContractStops.Of`. Fixtures bleiben in `Fixtures/RawText`.
4. Neuer öffentlicher Typ `ScannedContract` neben `ContractTrackerCandidate` (unberührt bis T10.4a); der Parser bleibt `internal`.
5. „O/“: kein Code, Wächtertest. Blockende auch „SHARE UNTRACK“.
6. Pickup **je Ware** aus den Collect-Zeilen (wie D68); eine Collect-Zeile ohne Punkt nimmt aus der nächsten Zeile höchstens drei Wörter aus Buchstaben mit Punkt („Station.“). Kein eigenes Symbol-Prädikat: eine Symbolzeile hat keinen Punkt.
7. Schlüssel aus **Paaren Ziel:Ware** statt getrennter Listen (H1); unlesbarer Reward oder Max = „?“, kein Joker (D101).
8. Hand-Tests: `OcrSampleTests` bewertet alle Felder und den Schlüssel je Paar; neu `ScanProbeLogTests` mit strukturellen Asserts, Zahlen nur im Bericht (H3); Pickup-Wächter über alle Fixtures (H2).

- [x] **T10.2a** Parser: Pickup, Reward-Zeile, `+max2`, Ansicht, Blockende, `ScannedContract` mit Inhaltsschlüssel (D101), Cli `scan` mit geparstem Vertrag, Hand-Tests
  - **Befund (11.11.2026, Branch `feat/t10-2a-parser`):**
    - **Hand-Test `OcrSampleTests`** (Release, S10.0-Aufnahmen):

      | Satz | Lieferfelder | Pickup | Max | Reward | Ansicht | Schlüssel | p95 |
      |---|---|---|---|---|---|---|---|
      | 3440×1440 | 138/138 | 20/20 | 20/20 | 20/20 | 20/20 | 10 Paare = 10 Schlüssel, keine Kollision | 1 291 ms |
      | 1080p | 135/138 (c06) | 19/20 (c06) | 20/20 | 20/20 | 20/20 | 10 Schlüssel (c06 ausgenommen), keine Kollision | 1 268 ms |

    - **`ScanProbeLogTests`** gegen `scan-probe.log` vom 11.11.: 227 Scans (OFFERS 17, ACCEPTED 210), jede Lieferzeile, jeder Reward, jede Ansicht gelesen; 20 Routen = 20 Schlüssel; der Fuel-Vertrag (191,500) mit 1 OFFERS- und 201 ACCEPTED-Scans unter einem Schlüssel.
    - **Fund im Hand-Test:** Bei 1080p liest die OCR das aUEC-Zeichen vor dem Betrag als „±“ („Reward ± 51,000“, c17). Ein Zeichen, das kein Buchstabe und keine Ziffer ist, darf zwischen Label und Betrag stehen.
    - **Pickup-Wächter (H2):** Zwei OCR-Fragmente im CT-Rohtext („Thundg“, „Green Gle“) stehen als bekannte Fragmente im Test. Mit einer Fortsetzungsregel, die jede Zeile nimmt, wird er auf 3 Fixtures rot.
    - **Offen für T10.3:** Würfelt das Spiel beim Annehmen auch die Ziele neu? Im Probe-Log ist nur ein Paar sicher (191,500). OFFERS und ACCEPTED mit gleichem Reward (100,500; 72,500; 60,750; 50,500; 50,250) haben andere Ziele, vermutlich andere Verträge. Gate-Lauf T10.3 mit echten Paaren: Vertrag ansehen, annehmen, beide scannen.
    - **Gate:** Build 0 Warnungen; Tests Core 1 817 · Import 287 (vorher 164) · App 1 731 · Ocr 34 grün (Hand-Tests gefiltert); `dotnet format --verify-no-changes` 0.
- **T10.2b, Präzisierung in der Planphase, ratifiziert durch Freigabe:** Teilung in b1 (Dateiformat, eigenes Gate) und b2
  (Scan → Plan). Schema 10 bleibt bis v0.4.0 offen, T10.4 ändert es ohne neue Nummer. Ein Re-Scan mit anderen Mengen bei
  geladenen/abgehakten Kisten der Mission geht wie der Editor-Plan (`PlanKeeping`), sonst wird nur die Mission neu gelegt.
  Der `scanNo`-Zähler liegt neben dem Plan (Datei `lastScanNo`, im ViewModel außerhalb der Undo-Schritte), nicht im
  `SavedPlan`. „Plan verwerfen“ setzt ihn auf 0. `Unknown` zählt als geschätzt (D102). F5 nach T10.4a. Die
  Nachfolger-Tabelle von F4 stand vor dem Abbau im Plan (P1).
- [x] **T10.2b1** `AppFolders` (F14), Plan-Schema 1–7 raus (F4), Schema 10 mit Scan-Identität, Goldens (F24), D102
  - **Befund (11./12.11.2026, Branch `feat/t10-2b1-format`):**
    - **F4:** Lesbar 8, 9, 10; 1–7 abgelehnt mit „expected 8 or 9 or 10“. Weg: `PlanFileV1Dto`, `PlanFileV2Dto`,
      `PlacementDto`, `ParseMissions`, `EmbeddedSchemaVersions`, vier `Check*`, zwei Alt-Zweige. 38 Core-Tests gestrichen,
      17 neu. **Nachfolger (Regel P1):** Verhalten → Nachfolger mit aktueller Datei, je grün gegen den alten Code und per
      Mutation rot (Hinweise neu abgeleitet; Racks geändert; ganzer Stopp abgehakt; 7 Validierungsfälle aus Schema 2 nach
      `V3Changes`), oder ein bestehender Test namentlich (Round-Trips für Haken, Positionen, `manual`, `loaded`, Item
      Pflicht, fremdes Schiff, Zurückschreiben); nur Alt-Format → Theory „Schema 1–7 wird abgelehnt“. Tabelle im
      Commit-Body von `5abaf6d`.
    - **Alt-Fixtures als Schema 9:** `before_b2_eight_stops`, `hurston_2026-10-23`, `hurston_2026-10-25` (19/17/23 Kisten,
      Rack, Position, Haken vorher/nachher identisch), `split_and_shared` (Schema 1: SCU je Rack/Ziel/Ware identisch; neuer
      Wächter für den `--plan`-Befehl). `split_and_shared_v2` gelöscht.
    - **Goldens:** Diff je Datei nur die `schemaVersion`-Zeile.
    - **Scan-Identität:** vier Weichen in `ContractSync`/`LoadPlanner`, jede vorher rot (Scan wäre gone geworden, nach
      Undo doppelt gekommen, `Quiet` nie ruhig, `DeliveryKey` warf).
    - **Martins plan.json** (Kopie, Schema 9, RAFT, 5 Missionen, 36 Kisten, Watermark 290): liest, schreibt als 10,
      Kisten byte-identisch.
    - **Gate:** Build 0 Warnungen; Tests Core 1 817 · Import 287 · App 1 737 · Ocr 34 grün; `dotnet format` 0.
      `CrateLayoutTests.Place_proves_stacking_in_under_100_ms` fiel einmal unter Last aller vier Testprojekte, allein 65 ms
      und in zwei weiteren Läufen grün.
    - Den `--plan`-Befehl habe ich nicht selbst gestartet: die App schreibt beim Speichern in Martins echte plan.json
      (Lehre T10.1b); der neue Wächtertest prüft die Datei.
- [ ] **T10.2b2** Scan → Plan: Abgleich per Schlüssel (`Added`/`Updated`/`AlreadyInPlan`), ein Undo-Schritt,
  Re-roll-Markierung, Vollständigkeit mit zweiter Lesung nur bei Verdacht, `ScanIntake`, `scans.log` – eigener Plan
  - Vorgemerkt (Martin): Test „neue Menge kleiner als bereits geladen“ durch `PlanKeeping`; die zweite Lesung hält den
    Ein-Scan-Riegel des `ContractScanner` nicht über die Wartezeit (zwei getrennte `ScanAsync`), sonst blockiert sie den
    Hotkey in T10.3 für ≈ 2,6 s.
  - Die nächste Nummer ist `max(LastScanNo, SavedPlan.HighestScanNo) + 1`.
  - Gefunden: `PickupNames.Clean` (`PickupNames.cs:44`) baut ein Item ohne `Manual` neu; ein Handeintrag einer
    CT-Mission verliert beim Bereinigen seine Markierung. `Clean` geht mit F23 in T10.4a.

### [ ] T10.3 Hotkey, Editor, Notices

- Hotkey `scanContracts`, Default **F3** (Ausnahme zu D35), gepollt nach B10/D97, aktiv nur bei SC- oder CargoPlan-Vordergrund.
- Editor-Button „Scan“ ersetzt „Sync from ContractTracker“.
- Notices unten links: `Scanned #3 · From … · n SCU to …`, `Updated #3 · quantities changed`, `Already in plan (#2)`, `No contract in frame`, `Star Citizen window not found`.
- Ein Vorschau-Scan vor der Annahme ist eine normale Mission, Notice „Rescan after accepting“; nicht genommen → Undo.
- Handling gegen `main`.
- **Aus T10.1b:**
  - Der Scan läuft über `ContractScanner.ScanAsync` (ein Scan gleichzeitig, Warten aufs Warmup höchstens 15 s). Ein zweiter Druck während eines Scans (1,3 s) wird verworfen: In der Probe drückte Martin im Abstand von 0,5–0,8 s, 5 von 12 Drücken fielen weg. Entscheiden, ob das so bleibt oder eine Notice bekommt.
  - Das Blinken eigener Fenster über dem Vertrag (D100) ist die Scan-Rückmeldung; keine eigene Anzeige nötig.
  - Frame-Zeit unter Scans neu messen, mit dem Messfenster **neben** dem ROI (`--scan-load`): die T10.1b-Zahl ist durch das D100-Blinken des Messfensters verfälscht.
  - **Gate:** p95 Taste → Rohtext ≤ 1 500 ms über dem Spiel, n ≥ 20 je Ansicht (OFFERS, ACCEPTED). Vergleich T10.1b: 1 304 / 1 391 ms bei n = 10.
  - **Speicher-Vergleichszahl:** T10.1b +0,03 MB pro Scan über Scan 71–210 (≈ 3 MB je 100 Scans), Band 773–892 MB.
  - Mit T10.5 entscheiden, ob „Scan aus“ auch das Warmup spart: die Engine kostet ≈ 400 MB Working Set (≈ 824 statt ≈ 400 MB vor OCR).


### [ ] T10.4a ContractTracker entfernen – Import, App, Doku

- Checkliste = CT-Inventur aus dem S10.0-Befund.
- CT-Reader, `ContractTrackerSync`, FileSystemWatcher, Fingerabdruck und Reset-Schutz raus.
- `settings.json` 2 → 3 mit Migration:
  - `contractTracker` raus, `scan` { `hotkey`, `notifyOnScan` } rein
  - `syncFromContractTracker` → `scanContracts`
  - Altfelder `pxPerCell`/`panX`/`panY` raus
- D81 `onContractGone` entfällt ersatzlos: kein „gone“ mehr, einen Plan verlässt man nur über Abhaken, Löschen oder Discard.
- CLI `contracts --raw` → `scan <png>`.
- Doku: README, Wiki, `docs/settings-format.md`, `docs/nexus/*`, Issue-Templates (Feld „ContractTracker-Version“ → „Screenshot des Vertrags“).
- **F5 (Review 2026-11-pre-p10):** Mission-Schema-Leser 1 und 2 (`MissionFileReader.cs:18-32`, `LegacySchemaVersion`,
  `NoFillOrderSchemaVersion`) raus; v0.1.0 schrieb Mission-Schema 3. Die Mission-Fixtures der Schemas 1/2 (≈ 9 Dateien, u. a.
  `example_missions`, `split_and_shared` des Cli-Befehls `plan`) als aktuelles Schema neu speichern, Nachfolger-Regel wie bei F4
  (T10.2b1). Kam von K10 über T10.2 hierher, weil T10.2b1 nur die Plan-Leser abbaut.

### [ ] T10.4b ContractTracker entfernen – Core, Planer

- `LoadPlanner` (35× Watermark), Identität D39/D42/D44 auf den Inhaltsschlüssel umstellen, Folgen fürs Plan-Schema.
- Eigener Plan **mit Zahlen** (Golden-Files, Planer-Laufzeit).
- Addenda: D37–D44 (superseded), D38/D42 (identity = content key), D81 (removed), K6.1 (`#scanNo`).

### [ ] T10.5 Settings „Scan“ und CLI-Abschluss

- Settings-Abschnitt „Scan“: Hotkey, `notifyOnScan`, Hinweis „Open the contract and press F3“, Reset wie D30.
- `--keep-scans`: PNG nach `scans\`, höchstens 20.
- Known issues:
  - ROI bei 32:9 und 16:10 ungeprüft, 4K ungeprüft
  - Verträge über eine Bildschirmseite
  - F3 von laufendem CT belegt (Notice mit Verweis auf die Settings)

### [ ] T10.6 Release v0.4.0

- Branch `release/v0.4.0`, Textabgleich.
- Release-Notes, Freigabe durch Claude.ai:
  - „ContractTracker is no longer needed – press F3 on an open contract“
  - Migrationshinweis settings 3 / plan 10, kein Downgrade
  - Speicher: mit OCR ≈ 824 MB Working Set statt ≈ 400 MB (T10.1b, Gate 2), ZIP ≈ 86 statt 62 MB (D99)
- Hero-GIF neu (Scan am Terminal; der D85-Auslöser ist erfüllt).
- Tag mit Nachmessung des ZIPs, Wiki, Nexus, Ankündigungen.

## Phase 11 – nach dem Launch

### [ ] T11.1 Auto-Update – **Velopack** evaluieren (ersetzt T6.2-Publish durch Installer mit Delta-Updates)
### [ ] T11.2 winget-Manifest – `winget install SC-CargoPlan`

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

---

## Backlog (nur bei Bedarf)

Kein Plan, sondern Reaktion: wird erst zum Task, wenn der Anlass eintritt.

- Eigener Datenordner für Diagnose-Schalter (`--data-dir` oder Env, z. B. für `--scan-probe`, `--measure-frames`):
  Probeläufe in T10.1b schrieben ins echte Profil: `plan.json` (der CT-Sync markierte Verträge als `gone`) und
  `cargoplan.log` mussten aus Sicherungen zurück, `settings.json` blieb laut Hash unverändert. Auslöser: der nächste Diagnose-Lauf, der eine Spieler-Datei
  verändert, oder ein neuer Diagnose-Schalter.

- Rechteck-Picker für das Scan-ROI (der Nutzer zieht den Vertragsbereich einmal auf), für Seitenverhältnisse, bei denen
  das relative 16:9-Center-ROI nicht trifft (32:9, 16:10, geänderte HUD-Skalierung; S10.0 nur 21:9 und 16:9 geprüft).
  Auslöser: Nutzer-Report „ROI trifft nicht“.
- Vertragsliste in einem Scan statt eines Scans pro Vertrag (S10.0: ein Scan = ein Vertrag). Auslöser: Nutzer-Report.
- Mehrseitige Verträge (Text länger als eine Bildschirmseite, scrollen und zusammensetzen). Auslöser: Nutzer-Report
  oder ein Vertrag, dessen Lieferungen im Scan fehlen.
- **Kiosk-Manifest als autoritative Quelle** für die Mengen. Auslöser: Der Aufzug liefert eine andere SCU-Summe als der
  ACCEPTED-Text. Martins Antwort steht aus; IC STARC-175359/223408 deuten darauf hin.

- `holo-v1`-Release auf GitHub entfernen (v0.1/v0.2 laden ihre Hologramme dann nicht mehr). Auslöser: v0.3.0 vier Wochen
  stabil, frühestens 06.12.2026 (Release 08.11.2026), ohne Meldung, die einen Rückweg auf 0.2.0 braucht (Session 4c).
- Modelle für 135c, 600i Executive Edition und F7C Hornet Mk II bei Sciirof (Motherlode) anfragen, mit Rückmeldung zur
  Handprüfung als Gegenleistung (Session 4a/4c). Auslöser: nach dem Launch, mit der Nachricht zu v0.3.0 (R5).

- Sektionsreihenfolge per Legenden-Drag (Spielerreihenfolge). Auslöser: Hauler-Report, dass die Legendenreihenfolge nicht reicht.
- `defaultFillOrder`, `fillRows`/`grouping`-Default pro Schiff in der Schiffs-JSON. Auslöser: zweites Schiff braucht einen anderen Default.
- Neubewertung „Sync-Mission wartet trotz freier Zellen“ (D89, Entscheidung 2). Auslöser: Live-Test, in dem eine Sync-Mission wartet, die mit `none` Platz hätte.
- Zweiseitige Sektionen (Backbord-/Steuerbord-Hälfte). Auslöser: ein Schiff braucht es.
- Sektionsgrenzen in der Schiffsansicht zeichnen. Auslöser: Hauler-Report, die Farben zeigen es heute.

- Warteeinheit Lieferung statt Mission (K6.4, E1): eine teilweise geladene Mission, deren Rest keinen Platz findet, lässt
  heute den Editor-Plan ablehnen. Auslöser: eine solche Ablehnung am Aufzug, vor allem nach K6.5 (Ziel an geladener
  CT-Mission nachgetragen). Aufwand: ≈ 20 Stellen an `Mission.Unplaced`, Formatfeld `stops[].unplaced`, Wartezeilen pro Lieferung.
- `RackAssignment` mit Hindernissen (K6.4, E2): mit geladenen Kisten legt der Planer nach der D41-Regel statt exakt (D64).
  Auslöser: Hauler-Report „Fragmentierung bei geladenen Kisten“ bzw. „Re-plan found no better layout“ ohne erkennbaren Grund.
- `CODE_OF_CONDUCT.md`, sobald es Contributors gibt; Community Standards mahnt es an, ein Solo-Repo braucht es vor dem ersten Hauler nicht (T6.4).
- ~~App-Icon (`.ico` mit 16/32/48/256 px, `<ApplicationIcon>` im App-csproj), sobald ein Icon vorliegt; aus T6.2 (D48).~~ → K5
- Topmost-Fallback (`SetWindowPos(HWND_TOPMOST)` per Timer) nur bei Issue-Report mit Repro. Im T3.1-Test blieb das Overlay über Borderless-SC (D13).
- ~~Rack `orientation` (physische Stapelrichtung, Rotation pro Rack), sobald ein Schiff es braucht (D14).~~ Stapelrichtung erledigt in T3.13c (`stackAxis`, D26), freie Rotation pro Rack in F3 (D91).
- ~~Viewport3D-Renderer auf denselben Geometriedaten, falls 2.5D nicht reicht (D14).~~ → Phase 8 (D83)
- Vorschau-Maximum mit SC: einzelne Schlüsselwechsel kosten 20–25 ms (T8.7, M5; p95 5,2 ms, ohne SC max 12 ms); die
  16-ms-Regel (D65) fängt sie ab. Erst klären, ob CPU-Konkurrenz mit dem Spiel oder eine GC die Spitzen macht, dann ggf.
  der Recorder im echten Overlay (T8.7 R1). Auslöser: spürbares Nachziehen der Vorschau am Aufzug.
- ~~`WindowBounds.FromRect` (T3.4) und `LegendEntry.ReserveLabel` haben nur Testaufrufer (Scan in T8.6): löschen samt
  Tests oder wieder nutzen. Auslöser: Repo-Hygiene vor v0.1.0.~~ Erledigt in K10 (F13): beide gelöscht, dazu
  `LoadPlanner.MoveCrate` (nur Tests) → Test-Helfer `PlannerMoves`.
- `ShipCamera.PxPerCell`/`PanX`/`PanY` (Lesefelder für Kameras von vor D83, T8.1) entfernen. Auslöser: der nächste
  Schema-Bump von settings.json; nach v0.1.0 schreibt kein Build sie mehr.
- Hysterese für Rack-Nummern: Fläche wechseln oder ausblenden erst nach einem Mindestabstand, damit eine Nummer beim Drehen
  nicht hin- und herspringt (D83, T8.2). Auslöser: Flackern am Aufzug (T8.7) oder Hauler-Report.
- Kiste unter einem leeren vorderen Rack ist in der Ansicht nicht anklickbar (Regel D21 „erstes Rack entscheidet“);
  Alternative (b) nächste Kiste über alle Racks, Ablage behält „erstes Rack“. Auslöser: Hauler-Report oder Martin am Aufzug
  (Hull A von oben, Rack 1 leer). Wächter: `SceneHitTestTests.An_empty_rack_in_front_covers_the_cargo_behind_it` (T8.3, E1).
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
- IBM-Palette (`648FFF 785EF0 DC267F FE6100 FFB000`) als vierte Stop-Palette, sobald eine IBM-eigene Quelle die Hex-Werte nennt; Orange liegt ΔE76 26 am Problemrot (unter der Schwelle 33,6), müsste also ohne Orange kommen (K4.2). Auslöser: Hauler fragt danach.
- `appearance.crates.customStopColors` (Hex-Liste, aktiv bei `stopPalette = custom`), falls ein Hauler mit seltener Farbfehlsicht keine der drei Paletten lesen kann. Gegen: ungeprüfte Farben (D29), und eine Liste im Look-Record bricht die Gleichheit, an der K4.3 „Custom“ erkennt (K4.2).
- Startordner der Look-Dialoge (Export/Import, K4.3): ohne `InitialDirectory` nimmt Windows den zuletzt benutzten Ordner der App, bei Martin `AppData\Roaming\VoiceAccess\UserPhrases\de-DE`. „Dokumente“ als Start beim ersten Export, falls Hauler ihre Datei nicht wiederfinden. Auslöser: Meldung.
- `MissionFrame` (weiß, D68) ist dieselbe Farbe wie Okabe-Weiß (ΔE 0), obwohl der Kommentar „white is in no stop colour“ sagt: ein ausgewählter Missionsrahmen um eine weiße Kiste trägt nichts. Befund aus K4.2, kein Task; Auslöser: Look-Gate oder Hauler-Meldung.
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
- Sync Ships in der App (Fleetyards-API), falls sich Frachtgitter häufiger ändern als Releases erscheinen oder Hauler zwischen Releases fehlende Schiffe melden. Voraussetzungen: D-Eintrag Netzwerkzugriff des Overlays, Lizenzfrage für Laufzeitabrufe, Merge-Logik aus import-fleetyards.ps1 nach C# (T7.1b; das Skript liegt seit 4c nur noch in der Git-Historie, `a3430b9^`, D94). Seit D92/D94 wäre die Quelle Motherlode, nicht Fleetyards.
- ~~Abholorte von Waste-Verträgen aus den Zeilen „Collect Waste from …“ des Rohtexts lesen (die Stationen), statt nur aus ContractTrackers `PickupLocation`, das einen davon zerrissen nennt (B6, D69). Auslöser: Hauler will Waste-Missionen nach Abholort unterscheiden.~~ → T10.2 (Abholort-Parsing)
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
- Fleetyards-Lage für Modul-Laderäume (Carrack, Caterpillar-Module und -Nase): heute modul-lokale Offsets, die überlappen, also generierte Reihe (D78). Auslöser: Fleetyards liefert schiffsweite Offsets für Module.
- Hull C: Stapelrichtung je Arm. Heute quer zum Arm, damit die Gitter 8×8×6/8×4×6 bleiben (D78); radial nach außen wie bei der Hull A hieße 8×6×8/8×6×4 mit Achse vom Kreuz weg. Auslöser: Beobachtung im Spiel oder in einem Beladungsvideo.
- Hull C: innerer und äußerer Block eines Arms ein Gitter oder zwei. Heute zwei Racks wie bei Fleetyards, beide einzeln voll packbar (K3.2). Auslöser: ein Hauler meldet, dass ein 32er über die Blockgrenze liegt.
- ~~Umrisse und Holos neu erzeugen (Pipeline `sc-ship-outlines`): die Salvation hat keinen (neu seit 25.10., zeigt den Bug-Keil, `NotTracedYet`), 18 Schiffe haben im Index vom 26.10. neue Rumpfmaße (K5.1). Auslöser: ein Hauler meldet die Salvation oder Racks liegen sichtbar außerhalb des Rumpfs.~~ → K5.2b
- Kisten großer Schiffe im Default-Overlay klein: C2 13,2 px, Hull C 5,1 px pro Kistenkante (400 × 302, nach K5.2c), unter dem Hull-A-Wert 27,2 (Befund K5.2c, D62-Addendum). Auslöser: ein Hauler meldet zu kleine Kisten auf einem großen Schiff.
- Railen: die generierte Reihe (53 Zellen) ist länger als der Rumpf (40 Zellen), ragt aus dem maßstabsgetreuen Umriss (K5.2c). Auslöser: Fleetyards-Offsets für die Railen oder eine Meldung aus dem Spiel.
- Doku aus `docs/wiki/` im Hauptrepo per Action ins Wiki spiegeln – Auslöser: erster externer Doku-Beitrag.
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
