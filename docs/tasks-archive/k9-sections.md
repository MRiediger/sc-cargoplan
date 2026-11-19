# Archiv: K9 Sektionen pro Ziel

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

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

