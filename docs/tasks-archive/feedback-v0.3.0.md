# Archiv: v0.3.0 – Feedback-Release

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

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
- Holo: [HoloPlacement.cs](../../src/CargoPlan.App/ShipView/HoloPlacement.cs), Rack-Mitte − Mesh-Box-Mitte + `holoOffset`. Umriss: [OutlinePlacement.cs](../../src/CargoPlan.App/ShipView/OutlinePlacement.cs), Mitte, Boden **und Maßstab** hingen an den Racks (Maßstab bei Railen 1,594, Hull A 1,12, Caterpillar 1,116, Syulen 1,032). Core liefert nur die Box `Ship.Bounds`.
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

