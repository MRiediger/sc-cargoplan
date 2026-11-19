# Archiv: Backlog-Session K1–K7

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

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
K6.4-P12 Look: heller Rahmen, ` ▣` als festes Segment, Kopfzeile `· 40 loaded`. Ergänzungen bei der Freigabe: **E1** teilweise
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

