# Archiv: Bugfix Holo-Pump (P1)

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Bugfix Holo-Pump (D76-Addendum) (30.10.2026) – Branch `fix/holo-pump-regression`

### [x] P1 Maßstab beim Drehen festhalten, Grenzen schiffsfest (D76-Addendum)
**Warum:** Martin: Das Pumpen beim Drehen ist wieder da, gleich nach Start/Reset, ohne Zoomen.

**Befund (Schritt 2):** Kein Commit nach dem K5.5-Fix (9a0eef6) ruft im Drehpfad wieder den Fit auf. `OrbitBy`, `Navigate`,
`OnCameraChanged`, `HoloCamera`, `OverlayViewModel.Camera` und die XAML-Bindings sind unverändert; K5.2c und K5.3b liegen vor dem
Fix. Ursache ist ein Pfad, den K5.5 nicht abdeckte: K1.1 (bbb9314) und K5.2d (5e1e582) klemmen in `ViewNavigation.On` das
Framing bei jedem Zeichnen auf `ZoomLimits`, deren Untergrenze 0,5 × Fit(aktueller Winkel) war. Wo dieser Fit mehr als
doppelt so groß ist wie das Framing (Blick genau von vorn), hebt der Clamp den Maßstab an. Also keine Regression durch
einen neuen Commit, sondern eine Lücke im ersten Fix.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 Ursache wie im Befund, nicht ein Regressions-Commit;
P2 nur Drehen fixen, der Fit ohne Framing folgt weiter Fenster, Ladung und Holo (D76); P3 Grenzen aus dem Fit an der
Default-Kamera des Schiffs (`IsoProjection.DefaultFor(Ship.Bounds)`), auch die Obergrenze; P4 schiffsfest, nicht
szenenfest: `ProjectedScene.ShipExtent` = Fit-Extent mit allen Racks, beladen oder nicht (D61 nur für den Fit), Referenz
neu nur bei anderem Schiff, Holo-Ankunft oder anderem Marker; P5 dritter Wächter am Zoom-Anschlag, vierter für einen
Plan-Schritt am Anschlag; P6 Messung per Wegwerf-Harness mit der echten `ShipViewControl` (Reflection auf den
Orbit-Pfad), 420 × 420, Holo aus dem echten Cache; der echte Download mit leerem Cache bleibt Martins Gate.

**Messung `pxPerCell` (Schritt 1)**, Start an der Default-Kamera, je Ziel Yaw/Pitch ein Drehschritt vom selben Start:

| Schiff | Szenario | vorher 0/0 | vorher übrige Ziele (45/0, 90/0, 0/−30, 45/−30, 90/−30) | Spanne vorher | nachher alle Ziele | Spanne nachher |
|---|---|---|---|---|---|---|
| Hull A | (a) Start, (b) Reset, (c) Schiffswechsel, (d) Holo nach Fit | 22,028 | 17,790 | 23,8 % | 17,790 | 0,0 % |
| Hull A | (d2) Holo nach Drehen | 38,476 | 38,476 | 0,0 % | 38,476 | 0,0 % |
| Hull A | (e) Anschlag herausgezoomt | 22,028 | 8,895 · 8,895 · 12,451 · 8,895 · 8,895 | 147,6 % | 8,895 | 0,0 % |
| Hull C | (a)–(d) wie oben | 3,530 | 3,182 | 10,9 % | 3,182 | 0,0 % |
| Hull C | (d2) Holo nach Drehen | 7,150 | 7,150 | 0,0 % | 7,150 | 0,0 % |
| Hull C | (e) Anschlag herausgezoomt | 3,530 | 1,591 · 1,651 · 1,826 · 1,591 · 1,651 | 121,9 % | 1,591 | 0,0 % |

Zusatzzahl Obergrenze (Martin): Hull C ist kein langes Schiff (D61 greift nicht), Reset-Fit mit einem beladenen Rack
= leerer Plan = 7,15. Lange Schiffe, ein beladenes Rack, Keil: höchster Reset-Fit Syulen 147,9 (Aurora Mk II TS 137,9,
Fortune 99,0), alle unter 160 → nur ein Satz im Gegenargument.

**Akzeptanz:**
- [x] Messung vorher/nachher: jede Zeile identisch (Tabelle)
- [x] Ursache als Satz belegt (Befund)
- [x] Wächter `OrbitScaleTests`: Drehen aus dem Fit, Holo nach Framing, Drehen am Anschlag, Plan-Schritt am Anschlag; vor
  dem Fix rot außer „Holo nach Framing“ (seit K5.5 grün, bleibt als Wächter)
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün (App 1.466 → 1.473, Core 1.492, Import 141; Slow grün)
- [x] Gate (Martin, Release-Build vom Branch, 30.10.2026, bestanden: „Kisten bleiben gleich groß, bzw. in scale“): Hull C
  mit Holo, Reset, von vorn und von oben drehen → Größe ruhig; bis zum Anschlag herauszoomen und drehen → ruhig
- Nicht von Hand geprüft: Holo-Download mit leerem Cache und danach drehen (nur per Harness, Szenario d).

**Teil 2 – das Holo selbst gemessen (Wegwerf-Harness, 30.10.2026):** Martins Befund danach: Das Holo atmet beim Drehen,
die Kisten nicht, beim GRIN UTV stärker als bei der Hull A. Gemessen über die echte WPF-Abbildung
(`GeneralTransform3DTo2D`) und gerenderte Pixel, Pitch 0, Yaw 0–360° in 15°-Schritten, 456 × 497, `holo-shaded`:

| Schiff | Kamera-Width | Holo-Höhe (WPF) | Würfel WPF = 2D | Dach − Würfel | gerenderte Höhe | gerenderte Breite |
|---|---|---|---|---|---|---|
| UTV | 3,884 konstant | 209,60 konstant | 117,40 = 117,40 | 46,10 konstant | 209 konstant | 246–397 |
| Hull A | 19,216 konstant | 106,30 konstant | 23,73 = 23,73 | 17,56 konstant | 106 konstant | 178–444 |
| Hull C | 107,438 konstant | 197,80 konstant | 4,24 = 4,24 | 107,39 konstant | 197 konstant | 198–424 |

Mit nur Ambient-Licht und Deckkraft 1 sind die gerenderten Maße identisch: kein Clipping (Near-Plane 0, Abstand 2000),
keine Fläche verschwindet; das Headlight ändert nur die mittlere Helligkeit um etwa ±8 %. Die Holo-Kamera hat keinen
eigenen Maßstab, sichtbar ändert sich allein die Silhouette.

**Teil 3 – Bisect mit Martin als Orakel (30.10.2026):** Release-Publishes je Stand auf `main`, Startskript mit gleichen
Einstellungen (UTV, keine gespeicherte Kamera, ContractTracker aus, leerer Plan), Martins Dateien nach jedem Lauf zurück.

| Build | Stand | Martin |
|---|---|---|
| 00 | 924a6f1 T7.4c-2 | Holo atmet, Kisten ruhig |
| 05 | 91af570 T7.4d | Holo atmet, Kisten ruhig |
| 10 | 3913bdf Merge K2 | Holo atmet, Kisten ruhig |
| 13 | 2922698 Merge K5 (vor K5.5) | Holo atmet, Kisten ruhig |
| 14 | 44353e2 Merge K5.5 | Holo atmet, Kisten ruhig |
| 15 | d92ca48 Merge K6 | Holo atmet, Kisten ruhig |

Ergebnis: keine Regression. Das Atmen des Holos ist die Silhouette des starr gedrehten Modells und seit T7.4c gleich,
es fiel nur bisher nicht auf. Kein Fix für Teil 2/3.

**Teil 5 – Drehpunkt und Frame-Log in Martins App (30.10.2026):**
- Drehpunkt (Wegwerf-Harness, 144 Frames, UTV, Hull A, Aurora Mk II, Pitch 0 und 35,26): Holo-Box-Mitte über WPF gegen
  Rack-Mitte über `IsoProjection`, Änderung über Yaw 0,000 px. Die Holo-Kamera übernimmt die Abbildung der Kisten, eine
  zweite Mitte gibt es nicht. Bei der Aurora ist `holoOffset` (0, 0, 1) ein fester senkrechter Versatz.
- Frame-Log (temporärer Release-Build über `run.ps1`, UTV, Aufnahme nach dem Render-Pass jedes Frames, Bild jedes Frames
  gespeichert): 119 Frames, ΔYaw, ΔPitch und ΔScale zwischen Kisten und Holo-Kamera in **jedem** Frame 0; Kisten konstant
  95,94 px/Zelle. Bei |Pitch| < 2° (46 Frames, Yaw 78–145°): gerenderte Holo-Höhe 210–215 px (bei gleichem Pitch 1,30°:
  213–214), Breite 368–398 px. Nach der vorab festgelegten Lesart: Lag 0, Höhe konstant, nur die Breite schwankt → die
  Drehung selbst, kein Bug. Die Bilder zeigen außerdem den 1-SCU-Würfel des UTV in der Fahrzeugmitte, nicht auf der
  Ladefläche (`HoloPlacement` legt die Holo-Mitte auf die Rack-Mitte, D63).

**Teil 1–5 in vier Sätzen:** Die Zoom-Grenzen hingen am Winkel und hoben am Anschlag und von vorn den Maßstab der Kisten
an; das ist behoben und mit vier Wächtern abgesichert (Teil 1). Die Holo-Kamera ist statisch korrekt: kein eigener
Maßstab, kein Clipping, derselbe Drehpunkt wie die Kisten (Teil 2, 5). Der Bisect zeigt das Atmen des Holos unverändert
seit T7.4c (Teil 3). In Martins App gibt es keinen Frame-Lag zwischen Kisten und Holo; was atmet, ist die Silhouette des
gedrehten Modells (Teil 5).

---

