# SC-CargoPlan – Routenplaner / Offer-Scan: Erkenntnisse & Design

Stand: 20. November 2026 (Alpha 4.10.3, Covalex-Rang Senior, Hull B). Grundlage: vier Scan-Serien (Everus 19.11. 14:33 und 17:29, HUR-L2 19.11., Baijini 20.11., Everus 20.11., Seraphim 20.11.), insgesamt ~250 Vertrags-Screenshots, OCR + Parser – **plus vollständiger Wiki-API-Dump aller Hauling-Templates (Abschnitt 7), der das empirische Modell bestätigt und die Hub-Profile berechenbar macht.**

## 0. Invarianten (Design-Leitplanken)

1. **Der bestehende Ablauf bleibt unverändert:** F3 im Offers-Tab, F3 im Accepted-Tab, Ladeplan. Kein neuer Pflichtschritt, kein Fenster, das dazwischenfunkt.
2. **Der Planer ist eine Schicht obendrauf**, gespeist aus denselben F3-Scans. Ohne Hub-Profil, ohne Vorschläge, ohne Game.log funktioniert alles wie heute.
3. **Game.log-Lesen ist keine Voraussetzung.** Der Accepted-Scan liefert die verbindlichen Legs und bestätigt die Annahme. Log-Reader = spätere Komfortoption (Trigger: Nutzer wollen den Accepted-Scan sparen).

## 1. Wie das Spiel Hauling-Angebote anzeigt (empirisch)

### 1.1 Drei Ebenen (Area of Operations)

| Ebene | Inhalt | Verhalten |
|---|---|---|
| Stanton-weit („Stellar") | Pickup und Ziel auf verschiedenen Planeten (LEO-Stationen, L-Punkt-Raffinerien) | immer sichtbar; über Tage stabil (gleiche Instanzen, gleiche Rewards); einzelne werden vergeben und ersetzt |
| Planetensystem (Planet + seine Lagrange-Stationen) | „lokaler Block": Hub = LEO-Station, Ziele = L-Punkte des Planeten, plus Rückläufer | wird beim Eintritt ins Planetensystem erzeugt/eingeblendet, beim Verlassen ausgeblendet (nicht gelöscht – kommt mit identischen Instanzen zurück) |
| Schwerefeld (Orbit + Oberfläche) | „Planetary" (z. B. Everus → HDPC-Cassillo) | nur im Schwerefeld sichtbar (an HUR-L2 bereits weg) |

Belege: Hurston-Block (23) identisch nach 3 h und nach Relog; an HUR-L2 unverändert (nur Cassillo weg); an Baijini ausgeblendet, systemweite 61 unverändert; an Everus wieder da (325.250-Senior an alter Position); Seraphim: Crusader-Block 19 + dieselben 61 systemweiten. **API-Entsprechung (7.3):** beide ersten Ebenen sind `mission_type = Hauling - Stellar`, unterschieden nur durch Geo `Stanton` vs. `Stanton<Planet>`; die dritte ist `Hauling - Planetary`.

### 1.2 Sichtbarkeit nach Rang
Alle Ränge ≤ eigener Rang gleichzeitig (Rookie bis Senior). Templates tragen eine ganzzahlige Rang-Anforderung; Fortschritt innerhalb eines Rangs ändert nichts. Weitere Filter: Ketten-Voraussetzung, CrimeStat, AO. Affinity: unbekannt, nicht als Filter annehmen. Bestätigt durch API: Master-Varianten fehlen auf dem Senior-Board vollständig.

### 1.3 Sortierung
Beim Öffnen des mobiGlas: lokaler Block zuerst, dann systemweit. Verträge, die während geöffnetem mobiGlas nachgeneriert werden, hängt die UI unten an (ArcCorp-Befund: 4 oben, 22 unten). Regel für den Scan: **mobiGlas schließen und neu öffnen, dann von oben bis zum ersten Fremdort.**

### 1.4 Zähler
Die Kategoriezeile „HAULING – STELLAR n" ist der billigste Änderungsindikator (ein OCR-Wert). Beobachtet: 84 → 83 (Cassillo weg) → 87 (Baijini) → 83/84 (Everus, +1 nach Ablieferung = Nachspawn) → 80 (Seraphim).

### 1.5 „Contract Availability"
Echter Countdown (~2h Small/Medium, ~4h Large; API: `deadline.completion_time_minutes`), wird aber beim erneuten Anbieten (Login, AO-Wiedereintritt) zurückgesetzt. Nicht als Ablaufzeit des Pools interpretieren.

### 1.6 Angenommene Verträge im Game.log (Referenz, nicht Voraussetzung)
Nach Annahme schreibt der Client pro Leg `New Objective: Deliver 0/N SCU of <Ware> to <Ziel>` ins Game.log, dazu Marker-Zeilen mit missionId und `CompletionType[Complete]` am Ende. Angebote (nicht angenommen) erscheinen nirgends im Log – OCR ist für die Entscheidungsphase der einzige legale Weg.

## 2. Template-Set des lokalen Blocks (Spiegel-Befund)

Dasselbe Set an allen drei getesteten Planeten, parametrisiert mit Hub + Stationsliste. Instanzzahlen hängen an der Stationsanzahl. **Korrektur aus den Locations-Daten: microTech hat fünf L-Stationen (MIC-L1 Shallow Frontier, L2 Long Forest, L3 Endless Odyssey, L4 Red Crossroads, L5 Modern Icarus); nur Crusader hat drei (CRU-L1/L4/L5).**

| Rolle / Template | Hurston (Everus, 5 L) | ArcCorp (Baijini, 5 L) | Crusader (Seraphim, 3 L: L1/L4/L5) |
|---|---|---|---|
| Blockgröße | 22 (+1 Planetary) | 22 | 19 |
| Senior Medium (16er, 2–4 Stopps, 190–395 SCU) | 2 × 290–325k | 2 × 302–340k | 1 × 296k |
| Experienced Medium (16er, 4 Stopps) | 1 × 191k | 1 × 197k | 1 × 195k |
| Experienced Direct Medium (16er, 1 Ziel, 280–360 SCU) | 4 × 160–165k | 1 × 163k | 1 × 159k |
| Experienced Small (4er, 4 Stopps, ~22 SCU) | – | 1 × 116k | – |
| Member Small (4er, 4 Stopps, 10–23 SCU) | 2 × 91–100k | 3 × 89–90k | 1 × 90k |
| Junior Small (4er, 1–4 Stopps) | 6 × 51–72k | 3 × 74k | 3 × 56–67k |
| Rookie Direct Medium (8er, 1 Ziel, 80–110 SCU) | 4 × 50,5k | 4 × 51k | 5 × 50,75k |
| Rookie Small / Direct Small / XS | 4–6 × 35–63k | 4–6 × 40–49k | 7 × 35–50k |
| Rückläufer L-Punkte → Hub | 6 × 45–72k | 5 × 40–90k | 5 × 35–67k |

Bei 3 Stationen wiederholen 4-Stopp-Verträge Stationen (L5 · L4 · L1 · L5).

### Reward-Modell (aus 83 systemweiten + 3 Blöcken)
Reward = f(Rang, Größe, Stoppzahl, Distanz), kaum SCU-abhängig. Alle Rewards Vielfache von 250. Senior Medium: 2 Stopps ~278k, 3 Stopps 276–290k, 4 Stopps 313–346k. Member Small (4 Stopps, ~20 SCU) zahlt mehr als Member Medium (2 Stopps, ~90 SCU). Beste aUEC pro Kiste: Experienced Small, Member Small, Junior Small. **API: Reward ist pro Variante fest (`reward_min`), kein Zufallsanteil.**

### Größen (Wiki, empirisch bestätigt mit Abweichungen)
XS ≤ 10 SCU/1er · Small ≤ 25/4er · Medium ≤ 126/8er (bis Member) bzw. 186–395/16er (ab Experienced) · Large 500–2750/32er. API: exakte Bänder je Leg in `hauling_orders.min_scu/max_scu`, Containergröße in `max_container_size`.

## 3. Hull-B-Ringe (512 SCU, 10 Slots)

| Hub | Ring | Landungen | Hinweg | Rückfracht | Summe |
|---|---|---|---|---|---|
| Everus | L1 → L2 → L4 → L3 → L5 → Everus | 6 | 780k (Senior 354 + 7 Smalls) | 234k | ≈ 1,01 M |
| Baijini | L1 → L2 → L4 → L3 → L5 → Baijini | 6 | ~830k (Senior 349 + Exp Small + 3 Member Small …) | ~250k | ≈ 1,08 M |
| Seraphim | L1 → L4 → L5 → Seraphim | 4 | ~637k (Senior 328 + Smalls + Rookie Direct Medium) | ~240k | ≈ 875k |

Rezept-Prinzip: 1 Bulk (Senior/Experienced Medium) + Smalls nach Reward/Kiste + 1 Direct-Vertrag mit einem Ziel an früher Station (öffnet Slot für Rückfracht) + Rückläufer, die erst an den Stationen angenommen werden, sobald Slots frei werden. Large-Verträge (32er) nie. Zwei Mediums passen nur, wenn beide ≤ 256 SCU.

## 4. Design Routenplaner

### 4.1 Architektur (additiv)
- **Quelle bleibt der F3-Scan.** Neu ist nur, dass jeder Scan zusätzlich den **Tab** erkennt (Reiterzeile „OFFERS" / „ACCEPTED (n/10)") und daraus den Status ableitet:
  - F3 im Offers-Tab → Pool-Eintrag *Gescannt* (+ optional Rollen-Zuordnung/Passt-Hinweis, wenn ein Rezept aktiv ist).
  - F3 im Accepted-Tab → Status *Angenommen*; Matching gegen den Offer-Scan per Titel + Reward + Legs. Liefert die verbindlichen Legs für Ladeplan und Route.
- **Entscheiden** (was annehmen): Hub-Profil → Rezept als Hinweis; Prüfung vor Annahme beim Offer-Scan.
- **Ausführen** (Ladeplan, Route, Fortschritt): aus den *Angenommen*-Einträgen. Fehlt ein Scan, fehlt der Vertrag in der Route – Hinweis, kein Fehler.
- Game.log-Reader: Backlog (ersetzt später optional den Accepted-Scan, liefert missionId/Abschluss).

### 4.2 Datenmodell
```
Leg(Scu, Commodity, From, To)
Contract(Id, Rank, Size, Direct, Reward, MaxContainerScu, Legs)  // TotalScu, Pickups, Dropoffs, Boxes
Offer(Contract, ScannedTab: Offers|Accepted, ScannedAt, ScannedUtc,
      Status: Gescannt|ImPlan|Angenommen|Erledigt|Abgelaufen, MissionId?, TemplateUuid?)
Template(Uuid, DebugName, Contractor: Covalex|RedWind, Title, Rank, Size, Direct, Shape: AToB|SingleToMultiN|MultiNToSingle,
         System, AreaKey (Stanton|Stanton1..4|Solar|RegionA..D|Interstellar), MissionType, Faction?: HH|CFP, Reward?,
         Legs[] (Commodity, MinScu, MaxScu, ContainerScu), PickupPool[], DropoffPool[])
// Matching Scan → Template: Titel + Shape + Ware + SCU-Band; Reward nur als Plausibilitätscheck, wo vorhanden (Stanton)
HubProfile(System, Hub, Stations[] mit Koordinaten/Distanzen, Rank → [Template…])   // generiert aus Templates
LoopCandidate(Name, Kind: LokalerRing|SystemweitLoop|Direct, Hub, Stationsfolge, Contracts/Slots, Kennzahlen, Status: Exakt|Schätzung)
```

### 4.3 Spieler-Loop (ohne Bruch des heutigen Ablaufs)
0. Kontext automatisch: Standort (Game.log-Location oder zuletzt gewählt), Rang (Settings), Schiff (Overlay) → Profil, falls vorhanden.
1. Spieler öffnet mobiGlas, scannt Offers wie gewohnt. Pool füllt sich nebenbei. Hinweis: mobiGlas schließen/öffnen, dann steht der lokale Block oben.
2. *Optional:* Planer-Reiter zeigt Loop-Ranking (4.7) und das Rezept für den gewählten Loop – Rollen mit Titel, **festem Reward (aus Template)**, Form/Stoppzahl, SCU-Band, auf Kapazität/Slots zugeschnitten. Wer den Reiter ignoriert, merkt nichts.
3. Beim Offer-Scan, falls Rezept aktiv: Match Titel+Reward → Template; Prüfung vor Annahme (Rolle, SCU-Summe ≤ Kapazität, Ziele ⊆ Zielmenge) → „Annehmen" / „zu groß – nimm den anderen".
4. Spieler nimmt an, wechselt zu Accepted, scannt wie gewohnt → Status *Angenommen*, Ladeplan wie heute.
5. *Optional:* „Route starten" – nutzt die *Angenommen*-Einträge, baut Stationsfolge und Stations-Checkliste (abladen, freie Slots, Rückfracht-Platzhalter), Holo-Overlay zeigt die Ladung bis zur nächsten Station. Platzhalterkarten sind adaptiv.
6. An der Station: Rückfracht annehmen, Accepted-Scan wie gewohnt → Route aktualisiert sich.
7. Abschluss am Hub: Summe aUEC (aus Scans) und Zeit → Messwerte ins Profil und in die Zeitparameter (4.7).
8. Zähler-Überwachung (nur Pool-Komfort, keine Planer-Logik): ändert sich n, Hinweis zum Nachscannen; Einträge grau markieren, nicht löschen.

### 4.4 Daten-Pipeline
- `hauling-templates.json` und daraus generiertes `hub-profiles.json` im Monatslauf von `sc-cargoplan-shipdata` (Details in 7.5). Empirische Pflege entfällt; Scan-CSVs dienen nur noch zur Validierung.
- Distanzmatrix der Stationen aus Starmap/Motherlode als weiteres Artefakt des bestehenden Monatslaufs (`sc-cargoplan-shipdata`, live seit v0.7.0) – die Wiki-Locations haben keine Koordinaten.
- Keine API-Calls zur Laufzeit.

### 4.5 OCR-Pipeline (für F3 und Datensammlung)
Regionen (Anteile bei 3440×1440): Titel (0.384, 0.174, 0.230, 0.076), Reward (0.628, 0.174, 0.186, 0.076), Objectives (0.599, 0.306, 0.203, 0.458), Details (0.390, 0.306, 0.189, 0.389); zusätzlich Reiterzeile (OFFERS/ACCEPTED) und Kategoriezeile (Zähler). Vorverarbeitung: Graustufen → Autokontrast → 2× → invertieren. Parser: `Deliver 0/(\d+) SCU of (.+?) to (.+?)\.` / `Collect .+? from (.+?)\.`; Containergröße aus Details; Fuzzy-Match der Orte gegen die Stationsliste des Profils (Pflicht). Validierung: Leg-Anzahl, Ort bekannt, **Reward muss einem Template-Reward des Boards entsprechen** (ersetzt „Vielfaches von 250 im Band"; fängt OCR-Fehler wie 51.000 statt 52.500 ab). Dedupe: (Titel, Reward, sortierte Legs). Windows.Media.Ocr als Engine. Offen: zeigt der Accepted-Tab `Deliver 12/94` (Fortschritt)? Dann Parser erweitern und Fortschritt im Stationsmodus nutzen.

### 4.6 Baureihenfolge
1. Tab-Erkennung + Pool mit Status (unsichtbar für den Klassik-Nutzer).
2. Pool-Reiter mit Spalten (SCU, Kisten, Stopps, Reward/Kiste, passt ins Schiff, lokal).
3. `hauling-templates.json` (Pipeline 7.5) + Rezept-Generator als Pure Function; Anzeige als Hinweis im Reiter.
4. Loop-Ranking (4.7) über Scan + Templates.
5. „Route starten": Stationsfolge, Slot-Simulation, Stations-Checkliste, Übergabe ans Overlay.
6. Game.log-Reader (optional, ersetzt Accepted-Scan).

### 4.7 Loop-Ranking – „Welche Loop ist für mich die effektivste, auch anderswo?"

**Kandidatenklassen**

| Klasse | Datenquelle | Genauigkeit |
|---|---|---|
| Lokaler Ring am aktuellen Hub | Offer-Scan | exakt |
| Lokale Ringe anderer Planeten | Templates (feste Rewards, SCU-Bänder, Ortspools) | Rewards exakt, Orte/SCU als Band; wird zur Messung, sobald dort gescannt |
| Systemweite Loops (LEO-Ring Everus → Baijini → PT → Seraphim; Raffinerie CRU-L4 → LEO-Stationen) | Offer-Scan – systemweite Verträge sind überall sichtbar | exakt, unabhängig vom Standort |
| Direct-Einzelläufe (1 Landung) | Scan/Templates | exakt |

**Kennzahlen pro Kandidat**
- aUEC pro Umlauf (Hinweg + Rückfracht)
- Landungen; Kisten (SCU/Containergröße); lange Sprünge (> 10 Gm) aus der Stationsgeometrie
- aUEC pro Landung (Vergleichszahl zwischen 4er- und 6er-Ringen)
- geschätzte aUEC/h = Umlauf / (Landungen × t_Station + Kisten × t_Kiste + Sprünge × t_Sprung); t-Werte sind Nutzerparameter (Defaults 10 min / 20 s / 4 min), nach jedem gemessenen Lauf nachgeführt
- Anreise als einmaliger Abzug, wenn Hub ≠ Standort
- Gerechnet wird immer mit den Templates (alle Verträge des Boards gelten als vorhanden); Scan-Werte ersetzen Bänder, sobald sie da sind

**Ringreihenfolge** wird berechnet, nicht hinterlegt: Permutation über ≤ 5 Stationen, kürzeste Distanzsumme, Gleichstand → Station mit Ein-Ziel-Vertrag zuerst.

**Beispiel mit den Daten vom 19./20.11. (Hull B, Senior, Default-Zeiten)**

| Loop | aUEC/Umlauf | Landungen | Kisten | Sprünge | aUEC/Landung | ~aUEC/h | Status |
|---|---|---|---|---|---|---|---|
| Baijini-Ring (5 L) | 1,08 M | 6 | ~70 | 4 | 180k | ~500k | exakt |
| Everus-Ring (5 L) | 1,01 M | 6 | ~75 | 4 | 168k | ~470k | exakt |
| Seraphim-Ring (3 L) | 875k | 4 | ~55 | 3 | 219k | ~520k | exakt |
| LEO-Ring (systemweit) | ~700–800k | 4 | ~60 | 4 (30–40 Gm) | ~190k | ~400k | exakt, Zusammenstellung zu optimieren |
| Port-Tressler-Ring (5 L) | ~1,0 M | 6 | ~70 | 4 | ~170k | ~470k | aus Templates (19 lokale Varianten, 1 Senior Medium, 2 Exp Direct Medium) |
| Direct-Einzellauf (Exp Direct Medium) | 160k | 1 | ~22 | 1 | 160k | ~450k | exakt |

Lesart: Seraphim bester Wert pro Landung; Baijini bester Wert pro Umlauf; Einzellauf für kurze Sessions. Das Ranking kippt mit den Zeitparametern – gewollt.

## 5. Offene Punkte
- Relog an fremdem Hub: bleibt der lokale Block erhalten (erwartet: ja)?
- Verzögerte Generierung beim AO-Eintritt (ArcCorp 4/22) reproduzieren.
- Planetary (124 Hurston-Varianten in der API, 1 an Everus sichtbar): Sichtbarkeitsregel klären – vermutlich Nähe zum Pickup-Ort (Oberfläche/Orbit). `CrossStanton` ist ebenfalls als Planetary typisiert (54 Varianten, z. B. Baijini → Everus 137k) – wo erscheinen die?
- Ortspools in `mission_tokens` sind bei einem Teil der Stanton-Varianten leer (Wiki-Auflösung unvollständig) → Fallback: Pool des Geschwister-Templates gleicher Form/Geo.
- Accepted-Tab: Feldlayout/Fortschrittsanzeige prüfen (4.5).
- Distanzmatrix: Starmap/Motherlode-Koordinaten im Monatslauf erzeugen.
- Pyro: Fraktionsgate HH/CFP empirisch prüfen (sieht ein Spieler nur eine Seite?); Region-A–D-Zuordnung zu Stationen aus den Ortspools ableiten.
- OCR-Abweichungen beim Everus-Scan nachsehen: 345.500 (Senior Medium, 4 Pickups → CRU-L4) hat keinen Stellar-Zwilling – vermutlich CrossStanton/Planetary; Member Small 91.250/100.500 vs. API 81.250/85.750.
- Hull B: Pilotenseitiger Traktorturm (STARC-202627) weiter gesperrt; rechter Turm über Co-Pilotensitz nutzbar; ATLS an Hangar-Standorten.

## 6. Testdaten
Scan-CSVs/Screenshots: Everus 84 (19.11. 14:33), Everus 29 (17:29), HUR-L2 30, Baijini 29 + 12 Listen + 22, Everus Kopfzeilen, Seraphim 80 (20.11. 09:17). Rohdaten lokal bei Martin; CSV `hauling_angebote_2026-11-19.csv` aus diesem Chat. API-Dump: `routing-fetches\` (haul_p001–019.json, details\ 1.843 Dateien, locations_p001–022.json, `fetch-hauling.ps1`).

## 7. API-Befund: Das Board ist aus den Spieldaten berechenbar (20.11., Wiki-API-Dump)

Datenbasis: `GET /api/missions?filter[reward_scope]=Hauling` (19 Seiten, 1.865 Einträge, 889 `HaulCargo_*`) + `GET /api/missions/<uuid>` für alle 1.843 UUIDs (`fetch-hauling.ps1`, Invoke-RestMethod – curl scheitert an den eckigen Klammern ohne `-g`). Spielversion 4.10.3-LIVE.13165946.

### 7.1 Namensschema `debug_name`
`HaulCargo_<Form>_<Kategorie>_<Ware>_<Geo>_<Grade>`
- **Form** = Stoppzahl: `AToB`, `SingleToMulti2/3/4`, `Multi2/3/4ToSingle`.
- **Geo**: `Stanton` (ohne Zahl) = systemweit · `Stanton1` Hurston · `Stanton2` Crusader · `Stanton3` ArcCorp · `Stanton4` microTech · `CrossStanton` · `Interstellar` · `TEMPLATE` (abstrakt, nicht im Spiel).
- **Grade**: `SmallGrade*` (XS/Small), `SupplyGrade*` (Medium), `BulkGrade*` (Large).
- Titel sauber: `<Rang> Rank - [Direct] <Größe> Cargo Haul`, Ränge Rookie…Master.

### 7.2 Relevante Detailfelder
`mission_type` ∈ {Hauling - Local, Planetary, Stellar, Interstellar} = exakt die AO-Ebenen aus Abschnitt 1 · `reward_min` = **fester Reward dieser Variante** (kein Multiplikator) · `lifetime.max_instances` (Stellar: immer 1), `respawn_time_seconds` 300 ± 120 · `hauling_orders[]` je Leg: `min_scu/max_scu`, `max_container_size` · `mission_tokens` `Location|Address` / `Destination*|Address` = Ortspools (L-Stationen, Hubs, Raffinerien; bei manchen Varianten leer) · `deadline.completion_time_minutes` · `released`/`not_for_release`.

### 7.3 Verifikation gegen den Everus-Scan vom 19.11. (84 Verträge)
Modell: **Board am Hub = alle `released` Covalex-Varianten mit `mission_type = Hauling - Stellar` und Geo ∈ {Stanton, Stanton<Planet>}, Rang ≤ Spielerrang, je 1 Instanz.**
- Ergebnis: 89 Varianten (≤ Senior) vs. 84 gescannt. **78 von 84 Rewards stimmen exakt** (Titel + Reward). Senior Medium 22/22, Junior Direct Medium 14/14, Junior Medium 8/8, Member Medium 5/5, Exp Direct Large 4/4 …
- Die 5 fehlenden Varianten (Rookie Direct Small ×2, Rookie Small, Member Small, Exp Small): `max_instances = 1` **serverweit** – von anderen Spielern angenommen, Respawn 5 min. Erklärt das „Verschwinden" der Member-Verträge am 18.11.
- Master-Varianten (5 Medium, 1 Large) korrekt nicht sichtbar → Rang-Gate bestätigt.
- SCU-Bänder passen: 94 Quartz ∈ [80,112], Senior-4-Stopp 71/114/106/63 ∈ [41–72, 81–144, 82–144, 40–72].
- Lokaler Block = die `Stanton<Planet>`-Stellar-Varianten: Hurston 21 (gescannt 22), ArcCorp 21 (22), Crusader 18 (19), microTech 19 (nie gescannt, jetzt bekannt).

### 7.4 Konsequenzen für das Design
- `hub-profiles.json` wird **nicht mehr empirisch gepflegt, sondern generiert**: pro Planet die Stellar-Varianten mit Titel, Reward, Form (Stopps), SCU-Bändern, Containergröße, Ortspool. Reward ist damit schon **vor** dem Scan ein eindeutiger Schlüssel; der Scan liefert nur noch die gezogenen Orte/SCU-Werte und bestätigt Verfügbarkeit.
- Loop-Ranking für alle vier Stanton-Hubs ohne Anflug: Σ Reward Experienced+Senior auf dem Board – Everus 9,21 M (27 Medium-16er, 7 Large), Baijini 9,20 M (26/7), Port Tressler 9,16 M (26/7), Seraphim 8,73 M (25/7). Die Hubs sind nahezu gleichwertig; der Unterschied kommt aus Stationsgeometrie und Rückfracht.
- Verfügbarkeitsmodell (nur Erklärung, keine Planer-Logik): Vertrag weg = von jemandem genommen, kommt nach 5 ± 2 min zurück.
- **Pyro/Nyx:** Covalex hat dort nur Intro-Varianten; das Pyro-Board stellt **Red Wind Linehaul** (7.6). Nyx hat in 4.10.3 kein Cargo-Hauling-Board.
- **Distanzmatrix:** `/api/locations` (auch Detail) liefert keine Koordinaten – Quelle muss Starmap/Motherlode sein; Erzeugung im bestehenden Monatslauf.

### 7.5 Pipeline (ersetzt 4.4 Punkt 1)
1. Bestehender Monatslauf in `sc-cargoplan-shipdata` (Phase 12, live seit v0.7.0) bekommt einen zweiten Job: Listen-Fetch + Detail-Fetch (≈ 12 min bei 300 ms Pause) → `hauling-templates.json` (alle released HaulCargo-/RedWind-Varianten, normalisiert aus `debug_name` + Detailfeldern).
2. Ableitung `hub-profiles.json` = Gruppierung nach System × AreaKey × mission_type × Rang (× Fraktion), inkl. Ortspools.
3. Diff zum Vorlauf ins Release-Changelog (neue/entfernte Varianten, Reward-Änderungen).

### 7.6 Pyro: gleiches Prinzip, anderer Auftraggeber (Red Wind Linehaul, 208 released Varianten)
Namensschema: `RedWind_Pyro_<Grade>_<Area>_<Fraktion>_<Routentyp>_<Ware>_CargoHauling_<Form>`
- **Area**: `Solar` (71, systemweit = `Hauling - Stellar`) · `RegionA` 28 / `RegionB` 38 / `RegionC` 28 / `RegionD` 19 (= `Hauling - Planetary`-Blöcke) · `Planetary` 2.
- **Fraktion**: `HH` (Headhunters) 84 · `CFP` (Citizens for Prosperity) 102 → zusätzliches Filterfeld neben Rang; vermutlich reputationsabhängig.
- **Routentyp**: StationToRuin, StationToStation, StationToOutpost, TradepostToStation, RuinToStation, … – Pyro verbindet Stationen, Tradeposts, Outposts und Ruin Station, nicht Hub + L-Punkte.
- **Ware**: Carbon 45, Potassium 39, Waste 32, ProcessedFood 23, Hydrogen 17, Aluminum/Copper/Quartz/Silicon/AgriSupplies.
- Titel: `<Rang> Hauler Needed for [Direct] <Größe> Shipment`. `max_instances = 1`, SCU-Bänder/Containergröße in `hauling_orders`, Ortspools vollständig gefüllt (Solar-Varianten: alle 11 Pyro-Stationen; Region-Varianten z. B. Checkmate/Patch City → Ruin Station).
- **Stellar-Board Pyro (≤ Senior):** 30 Experienced Direct Large, 24 Senior Large, 16 Junior Small, 12 Member Medium, 7 Member Small, 6 Experienced Medium – kein Senior Medium, Large (32er, 400–600 SCU) ist Standard → Hull B nur mit ATLS sinnvoll.
- **Rewards fehlen** in der Wiki-API für alle Red-Wind-Varianten (`reward_min = null`). Entscheidung (Martin, 20.11.): irrelevant – der Reward kommt wie bisher aus dem F3-Scan. Folge: Loop-Ranking für einen noch nicht gescannten Pyro-Hub liefert nur Struktur (Landungen, Kisten, Sprünge), aUEC erst nach dem ersten Scan dort.
- Dead Saints (8) und Ling Family (7): reine Interstellar-Verträge Stanton ⇄ Pyro, `max_instances = -1`, eigene Kandidatenklasse „Gateway-Lauf", kein Board. Nyx: nur Intro/Kurier/Recovery, kein Hauling-Board.
- Generator muss beide Namensschemata normalisieren (Covalex `HaulCargo_*`, Red Wind `RedWind_Pyro_*`) in dasselbe `Template`-Modell (4.2).

## 8. Streckenkarte – Route vor dem Annehmen planen (Idee 21.11.)

Auslöser: Abweichung von der Standardroutine (Everus → Baijini → Port Tressler). Frage „welche großen Verträge gibt es zwischen diesen Häfen?" ist mit der Vertragsliste im mobiGlas nicht beantwortbar; es gibt dafür kein Werkzeug (weder im Spiel noch Community). Da der Template-Pool fest ist (Abschnitt 7), lässt sich die Route **vor** dem Annehmen planen und an den Cargo-Tab übergeben.

### 8.1 Dritte Sicht auf dieselben Daten
Pool-Liste (heute) · Loop-Ranking (4.7) · **Streckenkarte** lesen alle `hauling-templates.json`. Die Karte zeigt die Templates als gerichteten Graphen.

### 8.2 Graph
- **Knoten**: Häfen – LEO-Hubs (Everus, Baijini, Port Tressler, Seraphim), L-Stationen (HUR/ARC/CRU/MIC), Pyro-Stationen, später Oberflächenorte (Planetary). Layout zunächst schematisch (Planeten-Quadrant = Hub + L-Ring, Pyro separat), geografisch, sobald der Monatslauf die Starmap-Koordinaten liefert.
- **Kante** = Template-Variante `PickupPool → DropoffPool`. AToB = Kante, SingleToMultiN = Fächer, MultiNToSingle = Trichter. Label: aUEC (wo bekannt) · Rang · Größe · SCU-Band; Dicke = Reward, Farbe = Rang, gestrichelt = Pool (mehrere mögliche Orte) statt konkretem Ort.
- **Filter**: Rang (≤ eigener), Größe/Containergröße („passt ins Schiff"), Direct/Multi, Auftraggeber/Fraktion, System. Defaults aus Overlay-Kontext (Schiff, Rang, Standort).

### 8.3 Zwei Kantenqualitäten
| Qualität | Quelle | Gilt für |
|---|---|---|
| Template-Kante | API-Ortspools (`mission_tokens`) | Lokalblöcke (Hub ↔ L-Stationen) – Pools gefüllt, exakt ohne Scan |
| Beobachtete Kante | Offer-Scan: Titel + Reward → Template, Legs → Endpunkte, mit Zeitstempel | systemweite `_Stanton_`-Varianten – Wiki-Pools leer; über zwei Scan-Tage stabil |

**Kerntest (21.11., Everus, zwei Verträge angenommen und abgebrochen):**
| Template | 19.11. | 21.11. abgebrochen | 21.11. neu (≤ 10 min) |
|---|---|---|---|
| Exp Direct Medium 160.250 (Stanton1) | Everus → L3 (109/116/127) | Everus → L3 (142/97/117) | Everus → L3 (127/113/123) |
| Senior Medium 325.250 (Stanton1, 4 Stopps) | → L3·L4·L5·L2 | → L1·L2·L3·L4 | noch nicht wieder da (Zähler 83) |

Ergebnis: **Respawn bestätigt** – neue Instanz mit gleichem Template/Reward, **SCU neu gewürfelt** (innerhalb der API-Bänder). **Multi-Stopp-Ziele werden pro Instanz aus dem Pool gezogen** → Kante = Fächer „Hub → Pool", nicht fest. Direct-Ziel 3/3 gleich (L3) – möglicherweise fest, bei 5 möglichen Zielen noch kein Beweis. API-Feld `reaccept_after_abandoning = false` (alle Covalex). **Nach dem Abbruch** verschwanden beide Templates vollständig aus Martins Liste (Zähler 84 → 83 → 82) und blieben weg nach Respawn (Backspace) und nach AO-Wiedereintritt (QT HUR-L1 und zurück, > 20 min). Frühere Erfahrung von Martin: Aufträge waren nach Abbruch sofort wieder da. → Verhalten nach Abbruch ist **unbestimmt** (zeitabhängig oder Bug); offen: Relog, Zähler am nächsten Tag. Beobachtet außerdem: ~5 min nach **Annahme** erscheint eine neue Instanz des Templates im Angebot (andere SCU), während die eigene noch läuft – „vergeben" ist nie dauerhaft. Konsequenz für den Plan: bis zum Scan nur Bänder und Pools, nie Werte; **der Planer kennt keine Verfügbarkeit** – er geht immer davon aus, dass ALLE Verträge des API-Pools (Board nach Rang/System/Area) vorhanden sind; keine Sperr- oder „vergeben“-Zustände, keine Alternativ-Logik. Was am Terminal tatsächlich steht, sieht der Spieler selbst; der F3-Scan füllt wie heute die Werte. Abbrechen ist kein Testwerkzeug (Reputation + Vertrag weg). Für systemweite `_Stanton_`-Varianten (Wiki-Pools leer) gilt derselbe Modus; ihre beobachteten Endpunkte bleiben Hinweis (Hub-Klasse), nicht Zusage.

### 8.4 Interaktion
1. Startpunkt wählen → Karte zeigt nur Kanten mit Pickup am Start bzw. entlang der bisher gewählten Route.
2. Klick auf Kante → in den Plan; Live-Prüfung Kapazität/Slots mit Slot-Freigabe je Stopp.
3. Routenleiste: Stationsfolge, Σ aUEC, Landungen, Sprünge, Kisten (Kennzahlen aus 4.7).
4. **„An Cargo-Tab übergeben"** → Plan mit Platzhalterkarten (Titel + aUEC + Ware + SCU-Band). Im Spiel Vertrag per Reward finden, annehmen, F3 im Accepted-Tab füllt echte Werte (Ablauf 4.3 ab Schritt 4). Verfügbarkeit ist nicht Sache des Planers – der Spieler sieht am Terminal, was da ist.

### 8.5 Hafen-Matrix als zweite Darstellung
Zeilen = Pickup, Spalten = Dropoff, Zelle = Anzahl Verträge / Σ aUEC / max SCU, dieselben Filter. Beantwortet „gibt es etwas von A nach B?" schneller als die Karte; in WPF ein DataGrid. Baureihenfolge: Matrix vor Karte.

### 8.6 Beispiel aus den Scan-Daten (19.11., Hull B, Senior) – LEO-Ring Everus → Baijini → Port Tressler → Seraphim → Everus
| Station | Annehmen | SCU an Bord danach |
|---|---|---|
| Everus | Senior Medium 289.000 Tin (113 Baijini / 129 PT / 91 Seraphim) · Junior Medium 69.250 Tin (41 → Baijini) · Junior Direct Medium 57.750 Tin (89 → Baijini) | 463 |
| Baijini | abladen 243 · Junior Direct Medium 87.250 Stims (91 → PT) | 311 |
| Port Tressler | abladen 220 · Experienced Direct Medium 165.250 Pressurized Ice (339 → Everus) | 430 |
| Seraphim | abladen 91 · Junior Small 57.000 + Rookie Direct Small 41.750 (→ Everus) | ~364 |
| Everus | abladen | 0 |

≈ 820k / 4 Landungen / 4 lange Sprünge (30–40 Gm). Experienced-Large systemweit = 1.024–2.816 SCU → für Hull B ungeeignet; einziger passender Large: Senior Large 209.500 Waste Baijini → Everus (503 SCU, 32er, ATLS nötig). Weitere Hub→Hub-Kanten aus dem Scan: Baijini → Everus Exp Direct Medium 162.250 (280 P.Ice); PT → Everus Exp Direct Large 127.250 (572 Waste, zu groß); Seraphim → Baijini/Everus Exp Large 263.750 (2.434 Hydrogen).

## 9. Umsetzung (Phase 13 „Cargo-Routen“, Master-Chat ab 21.11.)

Umsetzungsthread: „SC-CargoPlan | Cargo-Routen Umsetzung“. App-Repo: Phase 13, D-Nummern ab D118. Shipdata-Repo: Templates-Job als SD14 ff. im bestehenden Monatslauf (`ship-data.yml`, Python `tools/run.py`).

### 9.1 Repo-Befund vor dem ersten Task (21.11.)
- **Tab-Erkennung existiert bereits:** `ContractView` (Offer | Accepted | Unknown) in `CargoPlan.Import`, erkannt an Buttons und Availability-/Deadline-Zeile (T10.2a). `ScannedContract` liefert Stops mit SCU, Pickups, Max-Kistengröße, Reward, Inhaltsschlüssel (D101, ohne SCU). Baustein 1 aus 4.6 schrumpft damit auf den Pool.
- **Titel wird nicht geparst** (`<Rang> Rank - [Direct] <Größe> Cargo Haul`). Für Template-Matching (Titel + Reward) und den Rang-Filter muss `ContractTextParser` Rang, Größe und Direct liefern → Teil des ersten Tasks.
- **Heutiger Offer-Scan geht in den Plan:** F3 im Offers-Tab legt den Vertrag als geschätzte Mission ab (plan.json Schema 11, `scan.estimated = true`). Ein Pool aus 20 Angeboten hätte 20 Missionen im Ladeplan erzeugt → Entscheidung U1.

### 9.2 Entscheidungen
- **U1 Pool-Scan (Martin, 21.11.):** Ist der Planer-Reiter geöffnet, legt F3 im Offers-Tab den Vertrag **nur in den Pool**, nicht in den Plan. Ist der Reiter geschlossen, bleibt F3 exakt wie heute (Plan, geschätzt). F3 im Accepted-Tab bleibt in beiden Fällen unverändert (Plan + Status *Angenommen* im Pool). Präzisierung zu Invariante 1: der Klassik-Ablauf ändert sich nicht, solange der Reiter zu ist. Verworfen: zweiter Hotkey „Scan to pool“ (mehr Tasten), „Offers immer nur Pool“ (bricht Invariante 1).
- **U3 Beobachtungen statt Pool (Martin, 23.11.):** Ablauf = Reiter „Routes“ → Route aus Matrix/Karte (Templates) → Platzhalter je Template (Titel + Reward) → „Cargo“ → Vertrag am Terminal annehmen, F3 im Accepted-Tab füllt den Platzhalter → Wizard über die Stationen. Kein Pool gescannter Angebote, kein Status; jeder lesbare Scan schreibt nur **Beobachtungen** je Template (Endpunkte, SCU-Band, Reward) nach `routes.json`, Karte und Matrix zeigen sie als „beobachtet“ (§8.3). §4.1 „Pool-Eintrag“ und §4.6 Baustein 1–2 sind damit überholt (D118-Addendum T13.1).

### 9.3 Distanzmatrix – Quelle geklärt (Recherche 21.11.)
- **Primärquelle: `starmap_positions.json` im Repo `StarCitizenWiki/scunpacked-data`** (aus den Spieldateien per `octfx/ScDataDumper`, dieselbe Quelle wie `api.star-citizen.wiki`). Eine Datei (~455 KB), `entities[]` mit `uuid, name, type, system, parent_uuid, hidden, qt_valid, x, y, z` in **Metern, Systemkoordinaten**. Enthält alle vier LEO-Hubs (Everus, Baijini, Port Tressler, Seraphim), alle 18 Stanton-L-Stationen (`Manmade_VisibleOnInteraction`), Grim HEX, Gateways, Pyro-Stationen (Ruin Station, Checkmate, Orbituary, Patch City, Gaslight, Endgame, Megumi, Rod's, Starlight, Dudley), Nyx. Plausibilitätsprobe: Everus→HUR-L1 1,3 Gm, Everus→HUR-L3 25,7 Gm, Everus→Baijini 22,9 Gm, Everus→Port Tressler 38,4 Gm, Everus→Seraphim 31,9 Gm, Baijini→PT 59,5 Gm – passt zu den Spielwerten. Planeten stehen in SC still → statische Matrix reicht. Lizenz des Repos nicht ausgewiesen (Spieldaten = CIG-Material, gleiche Grauzone wie Holos; Credit an StarCitizenWiki/octfx).
- **Zweitquelle zur Validierung: UEX API 2.0 `GET https://api.uexcorp.uk/2.0/orbits_distances`** (ohne Auth, Cache 1 Tag, `distance` in Gm, von Datarunnern gemeldet, Update je Patch-Zyklus; Parameter `id_star_system_origin/destination`, optional `id_orbit_origin/destination`). Unklar, ob „Orbits“ L-Stationen einschließen – nur Stichprobe, nicht Primärquelle.
- **Verworfen:** RSI-ARK-Starmap (`starmap.json`: Koordinaten nur an Jump-Point-Tunneln), Wiki `/api/locations` (keine Koordinaten, Befund 7.4), `Valalol/Star-Citizen-Navigation Database.json` (handgemessen, POI-zentriert, L-Stationen nicht verifiziert), Fleetyards (keine Orte).
- **Pipeline:** der Monatslauf lädt `starmap_positions.json` roh, filtert `system ∈ {stanton, pyro, nyx}` und `type ∈ {Manmade, Manmade_VisibleOnInteraction}` plus Planeten, rechnet `distances.json` (Gm, symmetrisch, pro System) und legt beide ins Data-Release. Diff gegen den Vorlauf (neue/verschobene Stationen) ins Changelog. Ortsnamen-Abgleich gegen die Ortspools der Templates (`mission_tokens`) über die B1-Normalisierung (`DestinationNames.Key`).
