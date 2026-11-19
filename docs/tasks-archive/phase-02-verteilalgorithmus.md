# Archiv: Phase 2 – Verteilalgorithmus

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

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

