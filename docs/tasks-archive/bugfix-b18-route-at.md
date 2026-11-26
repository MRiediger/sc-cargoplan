# Archiv: Bugfix B18 – Leiste per Stationsname

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 26.11.2026 (T13.8, Nachlauf); Index und offene Tasks stehen dort. Gefunden in [Release v0.8.0](release-v0.8.0.md) Stufe 1.

## Bugfix B18 – Leiste per Stationsname (Branch `fix/route-at`, 26.11.2026, D126-Addendum)

**Warum:** Gefunden in T13.8 Stufe 1 (Release gestoppt): die App schrieb eine `routes.json`, die sie selbst nicht las.
Eine eigene Route mit Extras (Martins Datei, 12:13:32) stand bei `"at": 6` mit 4 `stations`; seit 12:56:45 meldete jeder
Start `Routes not loaded (… the route's at 6 is no stop of its 4 stations and the hub.)`, der Plan-Reiter lief ohne 13
Beobachtungen, Route und 3 Läufe. Ursache: die Leiste zählt die Orte der Extras mit und ordnet vor dem Abflug neu
(`RouteProgress.StationsOf`, K6), `Route.Step` begrenzt auf ihre Stopps, `RouteDto` prüfte gegen `stations.Count + 1`.

**Präzisierung in der Planphase, ratifiziert durch Freigabe (26.11.2026, „Freigabe B18 wie geplant, K1–K4
ratifiziert“, E1, E2):**
- Richtung C (Martin): `at` ist der Name des Stopps, `stations` und Loop-Schlüssel (D127) unverändert, Schema 3 bleibt
  (unveröffentlicht).
- **K1** Start = `at` fehlt, Ende = Name des Hubs (der Hub steht zweimal in der Leiste). **K2** voller Name wie in
  `stations`. **K3** alte Zahl gegen `[hub, ..stations, hub]`, außerhalb Start + Zeile. **K4** unbekannter Name → Start,
  eine Logzeile, Datei erst bei der nächsten Bewegung.
- **K (Martin):** Rundweg-Test über jeden per `Route.Step` erreichbaren Stopp, mit und ohne Extras; „gleich“ = Bytes nach
  erneutem Schreiben und aufgelöster Index; Martins Datei als Fixture.
- **E1** `ScratchRoutePicture.cs` (Spike T13.6) gelöscht statt angepasst, ihr Eintrag in `.git/info/exclude` mit.
- **E2** Zeile der alten Zahl: „Routes: route position 6 is outside the 4 stations – bar reset to start“.

**Stand (26.11.2026, CC):**
- Core: `Route.At` `string?`; `Route.Step(delta, progress, now)`, `Route.Advance` über `RouteProgress.IndexOf`;
  `RouteProgress.IndexOf(route)` (`null` = kein Stopp), `NameAt(index)`, die Simulation der Rückläufer mit demselben
  Index; `RouteDto.At` als optionales `JsonElement` (Name oder alte Zahl), `Position()` mit K3/E2, Zeile über `Dropped`
  ins Log. App: `ActiveRouteViewModel` Index aus `IndexOf`, ‹ › mit dem Fortschritt, K4-Zeile einmal je Name.
- Tests vor dem Code, rot gesehen (CS0029/CS1503/CS1929: `At` `int`, `IndexOf`/`NameAt` fehlen); dabei ein echter
  Testfehler (CS0136, Name `stops` in `RouteProgressTimingTests`), behoben. Neu: `RoutesFileTests` (Name, Start, Hub;
  alte Zahl 0/1/2/3; 4 und −1 → Start + Zeile; leer/`true`/`1.5` abgelehnt; Martins Datei `Fixtures/Routes/AtIndexCustom`:
  13 Beobachtungen, Route `custom` mit 2 Platzhaltern und 5 Extras, 3 Läufe, Leiste am Start, genau die E2-Zeile),
  `RouteProgressTests` (Rundweg ×4: ohne/mit Extra Teasa, vor/nach Abflug, jeder Stopp bis zum Hub; unbekannter Name),
  `RouteBarTests` (K4: Start, eine Logzeile, nichts gespeichert, › speichert Riker). Bestehende `At = n` auf Namen.
- **Mutationsproben:** Hub-Name als Start → 5 rot; alte Zahl außerhalb still auf den Hub → 3 rot.
- Build `0 Warnung(en)`, `dotnet format --verify-no-changes` Exit 0; Tests Core 2551/2551, App 2313/2313, Import
  430/430, Ocr 34/34.
- Doku: `routes-format.md` (`at` per Name, Abschnitt „Bar position“, Beispiele aus Martins Datei: zwei Beobachtungen,
  Route `custom`, ein Lauf – die alten Beispiele nannten Orte vor `33312a6` und eine Red-Wind-Beobachtung, die keine
  Datei hat), D126-Addendum.

**Gate:**
- CC: Start mit `--data-dir` auf einer Kopie von Martins gesicherter Datei → kein `Routes not loaded`, `13 observations`,
  die E2-Zeile.
- Martin: eigener Plan mit Extras → „›“ ×2 → Neustart → Reiter vollständig, Leiste am selben Stopp.

**Gate CC (26.11.2026, bestanden):** Dev-Build mit `--data-dir` auf einer Kopie von Martins Datei: `Routes: route position
6 is outside the 4 stations – bar reset to start`, `Routes: 13 observations: 12 matched, 0 ambiguous, 1 none`, `station
Everus Harbor (0/5)`; kein `Routes not loaded`; `routes.json` unverändert (13 898 B, K4). Nebenbei `Hauling: 587
templates, 157 positions, 0 left out, 50 ms`.

**Abnahme erteilt (26.11.2026, Martin: „Gate bestanden, B18 abgenommen“).**

**Akzeptanz:**
- [x] Gate CC und Gate Martin
- [x] Merge `--no-ff` nach `main` nach Martins Abnahme, dann `main` → `release/v0.8.0`; Abschnitt beim Schließen ins
  Archiv (K11)
