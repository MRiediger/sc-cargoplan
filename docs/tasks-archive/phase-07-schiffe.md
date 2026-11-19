# Archiv: Phase 7 – Schiffe vor Nexus

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

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

