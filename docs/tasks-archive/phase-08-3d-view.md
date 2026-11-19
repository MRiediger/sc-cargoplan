# Archiv: Phase 8 – 3D-Schiffsansicht

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

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

