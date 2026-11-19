# Archiv: erledigte Backlog-Zeilen

Durchgestrichene Zeilen aus „Backlog (nur bei Bedarf)“ in [`TASKS.md`](../../TASKS.md), wortgleich verschoben; jeder weitere Umzug kommt unten als eigene Überschrift dazu.

## Umzug am 19.11.2026 (K11)

- ~~Zeit-Asserts im Standardlauf~~ – erledigt in B13 (erster Commit): beide Tests in `Category=Timing`.
  **Auslöser erreicht (T10.4a, 11.11.: drittes lokales Auftreten, 172 ms auf main; im a2-Gesamtlauf fiel auch
  `Place_lays_out_16_crates_of_three_destinations_apart_in_under_100_ms`, Core allein 2× grün).**
  `CrateLayoutTests.Place_proves_stacking_in_under_100_ms` fiel in T10.2b1 und T10.2b2 je einmal im Lauf aller vier
  Testprojekte (142 ms), allein immer grün (65 ms). Lehre T7.1d (1-s-Assert auf CI-Runnern): Zeit-Asserts im
  Standardlauf unter Parallel-Last. Auslöser: der erste rote CI-Lauf damit oder ein drittes lokales Auftreten → in
  `Category=Timing` verschieben oder die Schwelle mit Messung begründen.
- ~~Eigener Datenordner für Diagnose-Schalter~~ – erledigt in T10.4a (`--data-dir`, 552c052).
- ~~Erster Selbsttausch übers Netz v0.5.0 → v0.5.1~~ – erledigt in V5 (16.11., voll); das **erste Delta übers Netz**
  kam mit v0.6.0 (T12.6a Netz-Gate, 19.11.: 850 683 B auf `CargoPlan-0.5.1-full.nupkg`). SmartScreen für `Update.exe`
  ist weiter unbelegt (beide Proben waren keine frischen Browser-Downloads).
- ~~`holo-v1`-Release auf GitHub entfernen~~ – erledigt am 16.11.2026 mit v0.5.1 (V5, Martin), vor dem hier genannten
  06.12.: Release und Tag weg, `releases/download/holo-v1/manifest.json` 404; v0.1/v0.2 zeigen den Umriss.
- ~~Zweiseitige Sektionen (Backbord-/Steuerbord-Hälfte). Auslöser: ein Schiff braucht es.~~ → F10 (Backlog II,
  Seitenweise Planung, Hull B).
- ~~App-Icon (`.ico` mit 16/32/48/256 px, `<ApplicationIcon>` im App-csproj), sobald ein Icon vorliegt; aus T6.2 (D48).~~ → K5
- ~~Rack `orientation` (physische Stapelrichtung, Rotation pro Rack), sobald ein Schiff es braucht (D14).~~ Stapelrichtung erledigt in T3.13c (`stackAxis`, D26), freie Rotation pro Rack in F3 (D91).
- ~~Viewport3D-Renderer auf denselben Geometriedaten, falls 2.5D nicht reicht (D14).~~ → Phase 8 (D83)
- ~~`WindowBounds.FromRect` (T3.4) und `LegendEntry.ReserveLabel` haben nur Testaufrufer (Scan in T8.6): löschen samt
  Tests oder wieder nutzen. Auslöser: Repo-Hygiene vor v0.1.0.~~ Erledigt in K10 (F13): beide gelöscht, dazu
  `LoadPlanner.MoveCrate` (nur Tests) → Test-Helfer `PlannerMoves`.
- ~~Ship mesh outline (Schiffsumriss): erst nach Klärung von Asset-Pipeline, Rechtslage bei Spieldaten und Render-Kosten im Layered Window (D14).~~ Übernommen als T7.4 (D55).
- ~~Umriss in 3D wie im Maintenance Panel (Prisma aus Draufsicht + Höhe, grober Rumpf aus Drauf- und Seitenansicht, oder Holo-Modelle), falls der flache Umriss im Spiel nicht reicht (D62).~~ Holo-Modelle übernommen als T7.4b (Spike) und T7.4c.
- ~~Kamera, Zoom und Pan der Schiffsansicht in `settings.json`, falls das Zurücksetzen beim Neustart nervt (D15).~~ → K1
- ~~Plan verwerfen ohne neuen Plan („Discard plan“), falls ein leeres Overlay nach der Route gewünscht wird; heute ersetzt nur „Plan“ den alten Plan (D19).~~ → K2
- ~~Rückfall auf Segmente ab einer Zellenzahl, sobald ein großes Schiff (Hull C, ~4.600 Zellen) beim Orbit ruckelt (D21).~~ Gestrichen: erledigt mit T7.3.
- ~~Spindelmuster für Hull B/C wie bei Hull A statt der generierten Reihe (Daten, T7.1b-Nachtrag), sobald die echte Anordnung
  bekannt ist (D61).~~ → K5
- ~~Nasenlänge des Bug-Keils auf die Schiffsbreite deckeln (Hull C 14 → 6 Zellen, ~10 % mehr Maßstab), falls lange Schiffe
  im Spiel zu klein wirken (D61).~~ Gestrichen: der Keil ist seit T7.4 nur noch Fallback ohne Umriss.
- ~~Rastergrenze über 12 px (z. B. 20), falls das volle Raster der C2 (18 px pro Zelle) im Spiel stört (D61).~~ → K1
- ~~Kistengrößen als Blöcke und Zellbeschriftung in der Schiffsansicht (D21).~~ Gestrichen: erledigt durch T3.11b.
- ~~Alternative Stop-Paletten, nur geprüft farbfehlsicht-tauglich (D29).~~ → K4
- ~~Legenden-Checkboxen: Farbe und Deckkraft in den Preferences wählbar (Abschnitt Legend; heute fest grau `#B0B0B0`, `MissionPalette.CheckBoxBrush`, D69). Auslöser: Taste-Gate B6.~~ → K4
- ~~Theme-Presets/Export (A4).~~ → K4
- ~~Füllrichtung in Mission-Dateien (Export/Import), falls Koordinatoren sie mit der Route teilen wollen; heute nur über plan.json (Core-Formatwechsel, D24-Addendum).~~ → K2
- ~~Formbewusste `Partition` (Racks nach legbarer Anordnung in Routenreihenfolge, nicht nur nach SCU), falls Stapel-Befunde in echten Plänen häufig sind. Beleg aus dem Spike T5.4 (Brute-Force, abgebrochen): 3 Stops in einem Rack → 207 Aufteilungen ohne Anordnung bis Mischung 146/186; 4 Stops → 397 bis Mischung 65/170 (D24-Addendum, T5.4a).~~ → T7.0
- ~~Geometrie bei Schiffen, deren Grid kein Vielfaches der Kistenmaße ist (D9 gilt nur für Hull A), sobald T7.1 ein solches Schiff bringt: `Plan` verteilt nach SCU und scheitert erst beim Legen, die Meldung („64/75 SCU“) klingt nach Platz und nennt nur Rack 1. `PlaceProblem` sagt es schon besser („no room for 8 SCU crates“). Beleg: S7.0, Lauf 3.~~ → T7.0
- ~~Stapelbewusste Partition: Racks so verteilen, dass keine Stapel-Befunde entstehen (Rest der übernommenen T7.0-Zeile), falls Stapel-Befunde in echten Plänen häufig sind (D54). Beleg T7.2: echte CT-Captures (High Course Station, 11 SCU) per `PlaceInto` in den Hull-A-Plan split_and_shared → 3 rote Kisten auf Riker/Orison/Lorville.~~ Erledigt in B2 (D64): Prio 3 wählt auch zwischen Zuordnungen, `PlaceInto` nimmt das Rack ohne Fremdauflage.
- ~~Ein Ziel pro Rack auf Mehrrack-Schiffen, wenn die Last es zulässt (Partition nach SCU füllt heute Rack 1 mit allen Zielen, C2: sechs Ziele in Rack 1). Auslöser: Hauler eines Mehrrack-Schiffs meldet, dass er ein Ziel pro Rack will. Planer-Änderung, D6-Addendum. Beleg T7.2: Legende „Rack 1 shared by Riker, Shubin, High Course Station, Melodic Fields Station, Faithful Dream Station, Everus Harbor – identify crates by count“; auf einem 480-Zellen-Rack hilft „by count“ nicht mehr.~~ Erledigt in B2 (D64): Prio 2 „wenigste Ziele im vollsten Rack, dann wenigste geteilte Racks“.
- ~~Frachtmodule (Aurora-Heckmodul, Retaliator, Caterpillar `module_01–04`) als zusätzliche Katalogeinträge aus einer Datei, ohne Änderung an Plan-Schema oder Auswahl (D59). Auslöser: erstes Schiff in `ships/` mit im Spiel wechselbarem Frachtmodul.~~ → K5
- ~~Haken eines Altbesuchs beim Schiffswechsel erhalten: ein abgehakter Stopp, der danach neue Fracht bekam (D41), kommt nach `Replan` auf einem anderen Schiff unabgehakt zurück und belegt Platz (D59); auf dem eigenen Schiff erledigt in B4 (D66). Auslöser: Meldung eines Haulers.~~ → K2
- ~~Abholorte von Waste-Verträgen aus den Zeilen „Collect Waste from …“ des Rohtexts lesen (die Stationen), statt nur aus ContractTrackers `PickupLocation`, das einen davon zerrissen nennt (B6, D69). Auslöser: Hauler will Waste-Missionen nach Abholort unterscheiden.~~ → T10.2 (Abholort-Parsing)
- ~~Kisten horizontal drehbar (Gier 90°) im Planer, statt Racks beim Import zu drehen (T7.1b: `length` = längere Seite, Rack in `notes` genannt). Auslöser: ein Schiff, dessen Rack nur mit gedrehten Kisten so voll wird wie im Spiel, oder ein Hauler-Report über abgelehnte Kisten, die im Spiel passen.~~ → K3.2
- ~~Planen in `Task.Run` statt im UI-Thread (`MissionEditorViewModel.Plan`, CT-Sync, Schiffswechsel), Auslöser: ein gemeldetes Einfrieren (D54) oder der erste Hauler-Report zur Planungsdauer. Messwert T7.2: 8-Stopp-Lastfall auf der Freelancer ~4 s Release (Suchlimit, D58), beim Editor-Plan wie beim Wechsel (`ReplanTests`, `Slow`). Ein UI-Task (Knopf gesperrt, „Planning…“), kein Planer-Task; kann als erster nach dem Launch kommen. Bis dahin Known issue (T7.5).~~ Geschlossen in B4 (D66): seit B2 plant jedes Fixture im Prozess in höchstens 3,2 ms (Median, erster Lauf 7 ms, Freelancer-Lastfall 2,3 ms, `ReplanTimingTests`); nur die Stresslast (kein Schiff) braucht 80 ms. Wiederöffnen bei einem gemeldeten Einfrieren.
- ~~Hit-Test der Schiffsansicht auf ihre Fläche begrenzen: gezeichnet wird mit `ClipToBounds`, getroffen nicht, also kann ein Drop über der Legende ein beim Hineinzoomen abgeschnittenes, unsichtbares Rack treffen (D33).~~ → K1
- ~~Undo für Drags und Re-plan: ab Stufe 3 (D65) ist Zurückziehen kein Undo mehr, ein Re-plan (B4) verwirft Moves. Auslöser: ein Hauler verliert eine Lage durch einen Drop.~~ → K2
- ~~Umrisse und Holos neu erzeugen (Pipeline `sc-ship-outlines`): die Salvation hat keinen (neu seit 25.10., zeigt den Bug-Keil, `NotTracedYet`), 18 Schiffe haben im Index vom 26.10. neue Rumpfmaße (K5.1). Auslöser: ein Hauler meldet die Salvation oder Racks liegen sichtbar außerhalb des Rumpfs.~~ → K5.2b
