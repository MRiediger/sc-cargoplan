# Archiv: Bugfix B1–B6 (Planregeln)

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

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

