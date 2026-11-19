# Archiv: Phase 10 – OCR (v0.4.0)

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Phase 10 – OCR (v0.4.0)

CargoPlan liest Verträge selbst (Hotkey, ein Scan = ein Vertrag), der ContractTracker-Import entfällt. Grundlage ist der Befund S10.0 unten. Die Reihenfolge ist fest: T10.4 kommt nicht vor T10.3.

### [x] S10.0 Spike „Eigenes OCR“ (09.11.2026, abgenommen 09.11.)

**Branches:** Werkzeug auf `spike/s10-ocr` (Worktree `..\sc-cargoplan-s10`, `tools/OcrProbe`, nicht in der `.sln`, wird nach der Abnahme gelöscht), dieser Befund auf `spike/s10-ocr-finding`. Samples außerhalb des Repos (`~/source/sc-ocr-samples`, CIG-Material, nicht MIT).
**Frage:** Kann v0.4.0 Verträge mit eigener OCR lesen (Hotkey, ein Scan = ein Vertrag) und den ContractTracker-Import ersetzen? Kandidat: **RapidOcrNet 4.2.0** (Apache 2.0) mit PP-OCRv5 Latin, ONNX Runtime 1.31.1, SkiaSharp. Die frühere Zeile T10.3 (Windows.Media.Ocr) ist mit dem Doku-Commit nach diesem Befund entfallen; Phase 10 ist jetzt OCR, Velopack und winget sind Phase 11.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. „Terminal“ = mobiGlas OFFERS, „Manager“ = mobiGlas ACCEPTED. Probe-Tasten F8/F9 statt F3 (CT hält F3).
2. Der bestehende Parser liest keinen Abholort: Pickup und Reward kommen aus Spike-Code und werden getrennt ausgewiesen.
3. `InternalsVisibleTo OcrProbe` und Vortice (D3D11 für WGC/DDA) nur auf dem Spike-Branch.
4. Zeilen: zuerst ein Block = eine Zeile nach Oberkante (wie der CT-Rohtext). Der zweite Lauf mit Spaltentrennung war nötig (`+cols`, s. u.).
5. Erkennungsauflösung: Das Vollbild braucht die volle Seitenlänge (`@max`). Mit der Paketvorgabe 1024 px wurden 0 von 13 Feldern gelesen. Der ROI-Ausschnitt braucht das nicht.
6. Haupt-Satz **3440×1440** (Martins Monitor): 42 Aufnahmen, davon **20 Verträge = 10 Paare** OFFERS → ACCEPTED mit Wahrheit von Hand (Martin). Vier Tippfehler in der Wahrheit am Bild korrigiert (c02, c11, c18, c20). **4K ungeprüft** (kein Monitor dafür).
7. Live-Lauf mit 10 statt 20 Auslösungen je Ansicht.
8. Zwei Max-Größen-Formulierungen, die der Parser nicht kennt, als Spike-Code gemessen (`+max2`), getrennt ausgewiesen.
9. `RecMaxDegreeOfParallelism` = 8 angefasst, weil raw/ROI schon offline das p95-Ziel verfehlte (1652 ms).
10. **1080p-Nachmessung ohne Tipparbeit für Martin:** SC im Fenster auf 1920 × 1080. SC rendert intern 1080p und skaliert das Bild auf 2560 × 1440 mittig ins 3440er-Fenster. Der 16:9-Bereich wird per Lanczos auf 1920 × 1080 zurückskaliert, das ist eher unschärfer als nativ, also konservativ. 10 neue Paare (`set-1080s`). **Die Wahrheit hat Claude an vergrößerten Ausschnitten der Originale abgelesen**, unabhängig von der OCR.

**Gates (Martin hakt):**
- [x] **G1 Capture:** Alle vier Wege liefern 3440×1440 mit SC vorn, borderless und „Vollbild“, kein schwarzes Bild. GDI BitBlt ist am schnellsten (33–41 ms). Vorbehalt: Das SC-Vollbild ist vermutlich fullscreen-optimiert, also faktisch borderless.
- [x] **G2 ≥ 95 % Felder:** **100 % (158/158)** mit raw + ROI + Spaltenreihenfolge + `+max2`; Pickup 28/28, Reward 20/20. Der bestehende Parser allein erreicht 95,6 % (ROI + Spalten) bzw. 90,5 % (Vollbild, Oberkante). **1080p: 98,1 % (155/158) mit up2-gray + ROI.** Die 3 fehlenden Felder stammen aus einer Aufnahme während der Textanimation (c06); ohne sie sind es 100 %. raw allein erreicht bei 1080p nur 94,9 %. **Empfohlen für beide Auflösungen: up2-gray + ROI.** Bei 3440×1440 ebenfalls 100 %.
- [x] **G3 p95 ≤ 1500 ms über dem Spiel:** **1184 ms** (ACCEPTED) / **1188 ms** (OFFERS), Median 1059 / 1112 ms, n = 10 + 10, Taste bis geparster Vertrag. Der erste Scan nach dem Start brauchte 1311 ms. Gemessen mit raw. Die empfohlene Variante up2-gray mit Parallelität 8 ist offline gleich schnell (p95 1374 ms bei 3440×1440, 1299 ms bei 1080p; raw: 1332 ms); live nicht gemessen.
- [ ] **G4 Terminal = Manager:** Der Lesepfad ist gleich (ein ROI für beide Ansichten, ein Parser, beide 100 %). **Der Inhalt nicht:** Im Haupt-Satz hat das Spiel beim Annehmen in 10 von 10 Paaren die SCU neu gewürfelt (Tabelle unten), im 1080p-Satz in 6 von 10. Unverändert blieben dort vier Verträge mit nur einer Lieferung (Quartz, Silicon, zweimal Waste). Martins Einschätzung: Spielbug.
- [x] **G5 Schlüssel eindeutig:** Alle 20 Schlüssel aus der OCR sind gleich der Wahrheit, ohne Kollision. Ein Schlüssel **ohne SCU** (Pickup + Ziele + Waren + Max [+ Reward]) verbindet 10 von 10 Paaren, ebenfalls ohne Kollision.
- [ ] **G6 Anker ≥ 95 %:** **nicht erreicht.** Ankerausschnitt raw + `+cols+max2`: 90,5 % der Felder (ACCEPTED 97,5 %, OFFERS 83,5 %). Ausschnitt aus dem `@max`-Vollbild bei OFFERS: nur 3 von 10 so gut wie das Vollbild. 1080p: 91,8 % (up2-gray). Anker sind unnötig: Das mobiGlas liegt bei 21:9 und 16:9 im zentrierten 16:9-Bereich, also passt **ein ROI relativ zu diesem Bereich** für beide Auflösungen und beide Ansichten (s. u.).

**Capture (F1)**, je ein Bild pro Weg × Modus × Ansicht, SC vorn:

| Weg | borderless ms (ACCEPTED / OFFERS) | „Vollbild“ ms (ACCEPTED / OFFERS) | Befund |
|---|---|---|---|
| GDI `BitBlt` (Bildschirm-DC, Client-Rechteck) | 36 / 41 | 37 / 41 | Bild |
| GDI `PrintWindow(PW_RENDERFULLCONTENT)` | 42 / 43 | 41 / 46 | Bild |
| DXGI Desktop Duplication | 48 / 53 | 49 / 54 | Bild |
| Windows.Graphics.Capture (`IsBorderRequired=false`, Zugriff „Allowed“) | 96 / 149 | 79 / 151 | Bild, langsamer |

Sample-Satz mit BitBlt: 42 von 42 Bildern, Median 33–34 ms. **GDI reicht, die Frage Vortice vs. eigenes D3D11-Interop entfällt.**

**Genauigkeit (F2)**, 20 Verträge, 158 Felder (je Lieferung Ziel, Ware, SCU; je Vertrag Max-Größe), 3440×1440:

| Lesart | Region | Felder | Pickup | Reward |
|---|---|---|---|---|
| raw, Oberkante | Vollbild (1024) | 18,9 % | 27,8 % | 30 % |
| raw `@max`, Oberkante | Vollbild | 90,5 % | 100 % | 100 % |
| raw `@max` + Spalten + `max2` | Vollbild | 97,5 % | 100 % | 100 % |
| raw + Spalten | ROI | 95,6 % | 100 % | 100 % |
| **raw + Spalten + `max2`** | **ROI** | **100 %** | **100 %** | **100 %** |
| up2 / up2-gray / up2-gray-inv + Spalten + `max2` | ROI | 100 % | 100 % | 100 % |
| raw + Spalten + `max2` | Ankerausschnitt | 90,5 % | 93,1 % | 40 % |

- **Die Erkennung selbst war nie der Engpass.** Ziel, Ware und SCU wurden in allen 20 Bildern richtig gelesen, auch unter einem Lichtstreifen quer über dem Text (c17/c18). Die Fehler stammten aus der Zeilenreihenfolge und aus dem Parser.
- **Zeilenreihenfolge:** Sortiert man nach Oberkante, verschränken sich Prosa (links) und Ziele (rechts). „4“ und „SCU or smaller“ liegen dann nicht mehr nebeneinander, und der Reward-Betrag landet vor „Reward“. Die Spaltenreihenfolge (`+cols`) trennt die Spalten an den linken Kanten von „DETAILS“ und „PRIMARY OBJECTIVES“ und liest innerhalb einer Spalte zeilenweise von links nach rechts.
- **Parser-Lücken (`+max2`), nur Direct-Hauls:** „At most the containers will be 8 SCU in size.“ und „… their ship couldn't handle 16 SCU containers …“.
- **Vorverarbeitung bringt nichts:** Mit ROI liefern alle vier Varianten dasselbe. raw ist am schnellsten.
- Ein ROI deckt beide Ansichten ab (mobiGlas-Panel an derselben Stelle): `3440x1440: [1300, 210, 1560, 1000]`.

**1080p-Nachmessung**, 20 Verträge (10 Paare), 158 Felder, Spalten + `max2`:

| Lesart | Region | Felder | Pickup | Reward | offline Median / p95 |
|---|---|---|---|---|---|
| raw | ROI | 94,9 % | 96,4 % | 100 % | 1345 / 1467 ms |
| raw `@max` | ROI | 97,5 % | 96,4 % | 100 % | 1457 / 1556 ms |
| **up2-gray, Parallelität 8** | **ROI** | **98,1 %** | 96,4 % | 100 % | **1231 / 1299 ms** |
| up2-gray-inv | ROI | 96,8 % | 96,4 % | 100 % | 1439 / 1544 ms |
| raw `@max` | Vollbild | 97,5 % | 96,4 % | 100 % | 1947 / 2109 ms |
| up2-gray | Ankerausschnitt | 91,8 % | 96,4 % | 100 % | – |

- Der einzige Fehler mit up2-gray ist **c06**: Die Aufnahme kam, während das Spiel die Ziele noch einblendete („Deliver 0/9 SCU of Silicon“, ohne Ziel und ohne Abholort). Bei 3440×1440 war c01 ebenso angeschnitten („Station at H“), aber das Ziel war schon lesbar. Das ist kein OCR-Fehler, der Produktpfad muss aber damit umgehen (offene Frage).
- raw verliert bei 1080p kleine Schrift: zwei Max-Größen (c02, c05) und eine Lieferung (c07). up2-gray liest sie.
- ROI `1920x1080: [645, 157, 1170, 750]` = das 3440er-ROI, umgerechnet über den zentrierten 16:9-Bereich (x 440 … 3000 bei 3440×1440). Dass es ohne Nachjustieren trifft, belegt die Lage relativ zu diesem Bereich.
- Modelle laden: 182 ms.

**Zeit und Speicher (F3):**

| Messung | Median | p95 |
|---|---|---|
| Offline raw/ROI, `RecMaxDegreeOfParallelism` 1 | 1380–1425 ms | 1652–1658 ms |
| Offline raw/ROI, Parallelität 2 / 4 / 8 | 1260 / 1273 / 1272 ms | 1424 / 1395 / 1332 ms |
| Offline raw `@max` Vollbild | 2200 ms | 2412 ms |
| **Über dem Spiel raw/ROI p8**, Taste → Vertrag (Capture 42 ms, OCR ≈ 1000 ms, Parse < 1 ms) | **1059–1112 ms** | **1184–1188 ms** |

- Modelle laden: 225 ms Median (bis 420 ms), einmal beim Start.
- **Speicher über dem Spiel:** Das Working Set wuchs in 20 Scans von 407 auf 684 MB, die Private Bytes am Ende auf 802 MB, ohne Plateau.
- Platte: Modelle 14 MB, `onnxruntime.dll` 17 MB, `libSkiaSharp.dll` 11 MB, verwaltete DLLs etwa 1 MB, zusammen ≈ 43 MB unkomprimiert.

**OFFERS → ACCEPTED (F4)**, Wahrheit gegen Wahrheit:

| Paar | Beim Annehmen geändert |
|---|---|
| c01 → c02 | Faithful Dream Processed Food 7→6, Thundering Express Pressurized Ice 3→4, High Course Processed Food 3→5 |
| c03 → c04 | Quartz (Raw) 104→110 |
| c05 → c06 | Quantum Fuel 124→86, Hydrogen Fuel 105→141, Ship Ammunition 141→90 (Summe 370→317) |
| c07 → c08 | Silicon 10→9 |
| c09 → c10 | Waste 11→8 |
| c11 → c12 | Iron (Ore) 101→105 |
| c13 → c14 | Pressurized Ice 4→6 und 3→6, Processed Food 4→6 |
| c15 → c16 | Stims 4→3, 2→3 |
| c17 → c18 | Waste 9→7 |
| c19 → c20 | Pressurized Ice 7→6, 7→6, Processed Food 6→7, 6→4 |

Gleich blieben Titel, Auftraggeber, Reward, Abholort, Ziele, Waren und Max-Größe. Bei der ersten Durchsicht ohne Wahrheit zeigten auch die übrigen 11 Paare des Satzes andere Mengen. **Folge:** Der Vorschau-Scan ist eine Schätzung. Die Mengen gelten erst nach dem Scan im Manager, der Schlüssel ohne SCU ordnet ihn der Vorschau zu.

**Inhaltsschlüssel (F5):** Pickup + sortierte (Ziel, SCU) + Waren + Max-Größe in B1-Schlüsselform: 20 von 20 stabil, keine Kollision, mit und ohne Reward. Ohne SCU: 10 Gruppen = 10 Paare. Vorbehalt: 10 Verträge aus Hurston/Covalex sind statistisch dünn. c03 und c11 unterscheiden sich nur in der Ware, und laut D39-Addendum gibt es Waste-Verträge, die sich nur im Reward unterscheiden. Der Reward gehört also in den Schlüssel.

**Hotkeys (Befund unterwegs):** Mit SC im Vordergrund kommt **kein** `RegisterHotKey`-Hotkey an. Das gilt thread- und fenstergebunden, mit offenem und geschlossenem mobiGlas, auch für Ctrl+Alt+K; auf dem Desktop kommt er an. Damit funktionieren auch **CargoPlans eigene Hotkeys (D17) im Spiel nicht**, ein v0.3.0-Bug. `GetAsyncKeyState`-Polling (10 ms, kein Hook, Präzedenz D45) sieht jede Taste, die Taste erreicht dann aber auch das Spiel. Fn+F8 sendet auf Martins Tastatur keinen VK.

**Inventur CT-Abhängigkeiten (Schritt 6)**, `git grep` nach `ContractTracker|Watermark|Gone|Detached|contracts.db`, ohne Änderung:

| Bereich | Dateien | Zeilen |
|---|---|---|
| `src/CargoPlan.App` | 37 | 279 |
| `src/CargoPlan.Core` | 14 | 145 |
| `src/CargoPlan.Import` | 9 | 53 |
| `src/CargoPlan.Cli` | 2 | 8 |
| `tests/CargoPlan.App.Tests` | 32 | 341 |
| `tests/CargoPlan.Core.Tests` | 33 | 149 |
| `tests/CargoPlan.Import.Tests` (dazu 18 Fixtures) | 9 | 73 |
| `docs/` (19), README, CLAUDE, CONTRIBUTING, TASKS, `.github`, `.gitignore`, `outlines` | 26 | 341 |

**Der Planer selbst trägt die CT-Identität:** `LoadPlanner.cs` hat 50 Treffer, davon 35 `Watermark` (Lieferidentität D81). Dazu kommen `ContractSync`, `ContractSource` und `PlanFileDto`. Der Ausbau betrifft also auch Core und das Plan-Dateiformat, nicht nur `CargoPlan.Import`.

**Empfehlung für den Phase-10-Schnitt** (Martin entscheidet):
1. **Bugfix Hotkeys vorab:** D17-Hotkeys per Polling statt `RegisterHotKey`, mit D-Eintrag, eigener Branch. Das wirkt sofort für v0.3.x und ist die Grundlage für den Scan-Hotkey.
2. **Lesepfad in `CargoPlan.Import`, Tests zuerst:**
   - Zeilenbildung aus OCR-Blöcken (Spalten, Bildschirmzeilen)
   - Parser-Erweiterung um die zwei Max-Formulierungen, Pickup-Parser (neu), Reward aus Label und Betrag in einer Zeile
   - Fixtures als OCR-Text, nicht als Bild
3. **Capture + OCR in der App:**
   - GDI BitBlt auf das Client-Rechteck, ROI relativ zum zentrierten 16:9-Bereich
   - RapidOcrNet mit up2-gray (2× hoch, Graustufen), Parallelität 8, Modelle beim Start laden
   - Textanimation abwarten: erneut aufnehmen, bis zwei Scans gleich sind, oder eine Lieferzeile ohne Ziel als „noch nicht fertig“ werten
   - Speicher im Langlauf messen
4. **Identität und Plan-Schema:** Inhaltsschlüssel ohne SCU, mit Reward, ersetzt CT-Id und Watermark. Der Manager-Scan überschreibt die Mengen der Vorschau. Schema-Sprung, CT-Ausbau in Core, App und Doku.
5. **Messung 4K und mit anderer UI-Skalierung** vor dem Release, sobald jemand mit diesem Setup testet. Dafür reicht der Produktpfad mit Logausgabe.

**Offene Fragen:**
- **T10.3, F3 belegt (K2):** Wer CT nach v0.4.0 weiterlaufen lässt, hält F3. Mit Polling gibt es kein „belegt“ mehr, beide Programme reagieren auf F3. Möglichkeiten: Notice beim Start, wenn CT läuft, oder eine andere Default-Taste. Kein Spike-Code.
- **Vortice vs. eigenes D3D11-Interop (P3):** entfällt, GDI reicht (G1).
- **Re-roll beim Annehmen:** Haben die CT-basierten Pläne (CT las OFFERS) am Aufzug andere Mengen gezeigt als geliefert wurden? (Martin). Behebt CIG den Bug, wird die Vorschau exakt. Der Entwurf oben funktioniert in beiden Fällen.
- **ROI bei 4K, 32:9, 16:10 und anderer UI-Skalierung:** ungeprüft. Bei 21:9 und 16:9 trifft dasselbe ROI relativ zum zentrierten 16:9-Bereich. Ob 16:10 (Bereich durch die Breite begrenzt) und eine geänderte HUD-Skalierung mitspielen, ist offen. Die Anker bleiben der Rückfall (G6 verfehlt).
- **Textanimation:** Ein Scan direkt nach dem Klick auf einen Vertrag kann halb eingeblendeten Text lesen (c06). Wie lange dauert die Animation, und reicht ein zweiter Scan nach ≈ 300 ms?
- **Speicher:** Wächst das Working Set weiter (nach dem ersten Scan ≈ 7 MB pro Scan) oder pendelt es sich ein? Dispose von Bitmaps und Frames prüfen.
- **Abdeckung:** nur Hurston, nur Covalex, zwei Waste-Verträge, keine Pyro-/Red-Wind-Formulierungen im Satz. Weitere Formulierungen kommen wie bei B9 über Meldungen.
- Ein FPS-Overlay schreibt Text oben rechts; mit ROI irrelevant.

### [x] T10.1 Capture + OCR (`CargoPlan.Ocr`)

**Abgehakt in K12 (19.11.2026, R1):** aufgeteilt in T10.1a und T10.1b, beide `[x]` (unten); ausgeliefert mit v0.4.0.

- Neues Projekt neben `CargoPlan.Import`.
- `ScreenCapture` per GDI `BitBlt` (S10.0 G1).
- Relatives 16:9-Center-ROI für alle Seitenverhältnisse (S10.0, Anker entfallen).
- RapidOcrNet mit Vorverarbeitung **up2-gray** als Konstante, ONNX-Session einmal pro Prozess vorgewärmt.
- `ScanContractSource` als Klasse, Tests direkt gegen sie – kein Interface mit einer Implementierung (A4, Review 2026-11-pre-p10 6.1 Frage 3).
- **F22 (Review):** `App.OnStartup` (152 Zeilen) vor der OCR-Verdrahtung in `Compose*`-Methoden schneiden – reine Umstellung ohne Verhaltensänderung, eigener Commit, damit der Diff lesbar bleibt.
- **Gates:**
  - p95 Hotkey → geparster Vertrag ≤ 1500 ms **über dem Spiel** mit up2-gray und Parallelität 8 (im Spike nur offline gemessen)
  - **100 Scans, das Working Set muss ein Plateau erreichen** (Spike: Working Set 407 → 684 MB, Private Bytes bis 802 MB in 20 Scans ohne Plateau = Leak)
  - Frame-Zeit über dem Spiel unverändert
- Hier entstehen die NOTICE-Datei (RapidOcrNet, PP-OCRv5-Modelle, ONNX Runtime, SkiaSharp, Clipper2) und D99 „Contracts are read by CargoPlan's own OCR; the ContractTracker import is removed“.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. **Teilung:** T10.1a = reine Bildverarbeitung, offline testbar (Gate 4). T10.1b = alles, was SC laufen haben muss (F22, Capture, Verdrahtung, `--scan-probe`, Gates 1–3), eigener Branch `feat/t10-1b-capture`. a wird nach Abnahme sofort `--no-ff` nach `main` gemergt; T10.2 kann nach a starten, T10.3 braucht b.
2. **Trennlinie:** `CargoPlan.Ocr` ist `net10.0` ohne Windows-API (ROI, Vorverarbeitung, OCR, Zeilenbildung, `ScanContractSource.Read(SKBitmap)`). `ScreenCapture` und die Fenstersuche liegen in `src/CargoPlan.App/Win32/` neben der bestehenden `NativeMethods`. Die Cli bleibt `net10.0`.
3. Ocr ohne Core-Referenz (A4), bis T10.2 sie braucht.
4. Modelle aus dem RapidOcrNet-Paket als `None`-Items (`ExcludeAssets="build"`, das Paket-Target greift nicht transitiv) nach `models/v5/` neben der Exe; NOTICE als `models/NOTICE.md`.
5. ONNX Runtime 1.31.1 explizit referenziert (Spike-Stand), SkiaSharp nur transitiv (3.119.1; RapidOcrNet ist gegen 3.x gebaut).
6. Zeilenbildung: Blöcke einer Bildschirmzeile werden je Spalte links → rechts mit Leerzeichen zu einer Textzeile verbunden (`DeliveryLine` ist auf eine Zeile verankert).
7. `CargoPlan.Import.Tests` bekommt `default.runsettings` (`Category!=Slow&Category!=Timing`); der Hand-Test mit dem Parser liegt dort.
8. Dieser Textfix kam nach dem Plan statt davor (Plan Mode).

- [x] **T10.1a** Ocr-Projekt, ROI, up2-gray, OCR, Zeilenbildung, CLI `scan <png>`, NOTICE, D99 – Gate 4 (Build 0 Warnungen, Tests grün, `dotnet format` 0, `publish.ps1` mit neuer ZIP-Größe)
  - **Befund (09.11.2026, Branch `feat/t10-1-ocr`):**
    - **Gate 4:** Build 0 Warnungen; Tests Core 1 817 · Import 164 · App 1 701 · Ocr 32 grün (Hand-Tests gefiltert); `dotnet format --verify-no-changes` 0. `publish.ps1`: ZIP **86 399 140 Byte** (v0.3.1: 62 121 374, **+24,3 MB, +39 %**), 386 Einträge (v0.3.1: 368), Ordner 178 MB. Die Zahl geht in die Release-Notes von v0.4.0.
    - **Hand-Test über die S10.0-Aufnahmen** (bestehender Parser, Release, SC lief im Hintergrund): 3440×1440 **138/138** Lieferfelder, 1080p **135/138** (nur c06, Textanimation). Offline Median 1 171 / 1 240 ms, p95 1 257 / 1 303 ms (S10.0 offline up2-gray p8: p95 1 374 / 1 299 ms).
    - **Speicher offline** (`OcrMemoryTests`, 100 Lesevorgänge): 154 MB nach Laden und Vorwärmen, 554 MB nach dem 2. Lesevorgang, langsam bis 669 MB bei etwa 60, dann flach (650–669 MB, Private Bytes 778–798 MB). Sieht nach der Arena von ONNX Runtime aus; Gate 2 über dem Spiel ist T10.1b.
    - **Ziel-Symbol** (Fund im Hand-Test): Mit K2 hing „0“/„L“ vor „Deliver“, und der Parser verlor in 4 von 20 Verträgen je Satz eine Lieferung. Entscheidung Martin: Ein erster Block mit höchstens 2 Zeichen in einer etwa quadratischen Box (≤ 1,3 × Höhe; gemessen 1,02–1,20) bleibt eine eigene Zeile. Die Zwei-Boxen-Lieferung, die K2 vorbeugen soll, wurde in den 40 Aufnahmen **nicht beobachtet**; K2 bleibt vorsorglich.
    - Das Gray8-Bild liest RapidOcrNet direkt (`OcrUtils.SubtractMeanNormalize`).
    - Eine Lesung hat Grundkosten fast unabhängig von der Bildgröße (fast leerer Rahmen ≈ 0,9 s). Die Scan-Tests nutzen deshalb 960×540 und bleiben unter der Regel von 1,3 s.
- [x] **T10.1b** F22, `ScreenCapture`, Fenstersuche, Engine-Vorwärmen beim Start, `--scan-probe` – Gates 1–3
  - **Abnahme (Martin, 11.11.2026):** Gate-1-Schwelle **1 500 ms p95** bleibt und gilt verbindlich für T10.3, gleiche Methode (Taste → Rohtext, SC läuft, je Ansicht), dort **n ≥ 20 je Ansicht** (bei n = 10 ist p95 praktisch das Maximum). Reserve unangetastet: ORT-Threads, Priorität. Bestätigt vor dem Merge: B1 Test für den Fund f7adca0 (`A_scan_before_the_warm_up_started_waits_for_it`, im Fix-Commit, vorher rot); B2 ROI-PNG des 125-%-Laufs gesehen und richtig. D100-Blinken live mit dem echten Overlay beurteilt (Gate-1-Lauf).
  - **Präzisierung in der Planphase, ratifiziert durch Freigabe:** `--scan-probe [N]` = F8 ein Scan, F9 N Scans (Default 100) in einer Instanz; Testmatrix drei Fenstermodi plus 125 %; `HotkeyPoller<TAction>` statt eigener Flanke in der Probe; Client-Rechteck per `GetClientRect` + `ClientToScreen`, DWM-Bounds nur als Logspalte; K1 `Load(folder)` ohne geteilten Zustand, K2 Warten auf das Warmup höchstens 15 s, K3 eigener Schalter `--scan-load`.
  - **Befund (10./11.11.2026, Branch `feat/t10-1b-capture`, Release, SC 3440×1440 borderless, Monitor 110 %):**
    - **Gate 1** (Taste → Rohtext, `scan-probe.log`), alle Scans gelesen:

      | Lauf | n | Capture inkl. D100 | Preprocessing | OCR | Taste → Rohtext Median / p95 |
      |---|---|---|---|---|---|
      | OFFERS, F8 | 10 | 21,9 / 52,8 ms (1. Scan) | 40,3 / 43,2 ms | 1 185 / 1 242 ms | **1 255 / 1 304 ms** |
      | ACCEPTED, F8 | 10 | 23,6 / 33,0 ms | 43,9 / 53,2 ms | 1 218 / 1 320 ms | **1 296 / 1 391 ms** |
      | F9-Serie 1 (Dauerlast) | 100 | 23,1 / 27,0 ms | 48,3 / 53,6 ms | 1 329 / 1 428 ms | 1 407 / 1 511 ms |
      | F9-Serie 2 (Dauerlast) | 100 | – | – | 1 322 / 1 395 ms | 1 398 / 1 472 ms |

      Die 3,2 s aus T10.1a waren eine Einzelmessung; einzelne Auslösungen liegen unter dem Zielwert 1 500 ms, kein Tuning nötig (ORT-Threads, Priorität unangetastet). Schwelle legt Martin fest.
    - **Gate 2** (210 Scans in einem Prozess): Working Set 274 MB nach dem Warmup, ≈ 700 MB nach 10 Scans, ab etwa 70 ein Band von 773–892 MB (Mittel 824), Steigung über Scan 71–210 **0,03 MB/Scan**; Private Bytes 881–1 003 MB, −0,02 MB/Scan. Erste Serie allein zeigte +1 MB/Scan über 71–110 (Pendeln, die zweite Serie entschied). **GDI-Objekte konstant 35.** Offline (T10.1a, ohne WPF) 650–669 MB.
    - **Gate 3** (`sc-gate3.ps1`, `--measure-frames <Ziel> --over-game`, je 3 Läufe, Median, Anteil später Frames): `hull_c` main 8eb2141 **7,1 %** → Branch mit warmer Engine **5,5 %** (−1,6 pp); `reclaimer` **10,7 %** → **10,9 %** (+0,2 pp). Gate ≤ +2 pp erfüllt. Mit `--scan-load` (20–22 Scans je 30 s, nicht Teil des Gates): `hull_c` 28,1 % bei 93 fps, `reclaimer` 5,9 % bei 57,5 fps – **verfälscht**: das mittige Messfenster liegt über dem ROI, D100 schaltet es je Scan auf Opacity 0 und zurück, das erzwingt zusätzliche Renderdurchläufe (Median 8,33 statt 16,67 ms).
    - **Warmup** (Logzeile `OCR ready`, ab dem ersten Frame): 1 177 ms (Laden 692, Dummy-Lesevorgang 485) beim ersten Start mit SC, danach 677 und 1 054 ms; Start 1,2–2,4 s nach dem Prozess. Working Set 157 → 270 MB.
    - **Overlay über dem ROI → D100:** Messung über SC (20 Versuche je Variante) und Martins Entscheidung **Opacity 0 mit einem `DwmFlush`**, nur für Fenster, die das ROI schneiden, Rückstellung auch bei Ausnahme. Im Gate-1-Lauf lag das Overlay bei allen 10 OFFERS-Scans über dem ROI, auf keinem Bild zu sehen. Martin: das kurze Blinken ist eine gute Rückmeldung, dass gescannt wird.
    - **Fenstermodi** (je 1–2 Scans, ROI-PNG geprüft): Vollbild und Borderless identisch; **Fenster**: Client (8, 33), DWM-Bounds (7, 0) – über die DWM-Bounds läge das ROI 33 px zu hoch, über den Client sitzt es; SC blieb im Fenstermodus bei 3440×1440. **125 %** laut Martin (nicht abgemeldet): ROI identisch; das Log belegt die Skalierung nicht, weil SC `GetDpiForWindow` 106 meldet (Systemwert aus der Anmeldung).
    - **DPI gemessen:** der UI-Thread läuft `system`-aware (WPF ohne Manifest), der Capture-Thread `per-monitor`; bei 110 % saß das ROI pixelgenau.
    - **ZIP** (`publish.ps1`): 86 426 701 Byte (T10.1a 86 399 140, +27,6 KB), 386 Einträge, Ordner 178 MB.
    - **Gate 4:** Build 0 Warnungen; Tests Core 1 817 · Import 164 · App 1 731 · Ocr 34 grün; `dotnet format --verify-no-changes` 0; CI `main` 8eb2141 grün (Branch noch nicht gepusht).

### [x] T10.2 Parser-Anbindung, Identität, plan.json 10

- Rohtext durch den bestehenden Parser (B1, B9, D96). **Neu: Abholort-Parsing** („Collect … from <Ort>“, bisher CT-Spalte).
- **Inhaltsschlüssel ohne SCU, mit Reward** (S10.0 G5; das Spiel würfelt beim Annehmen neu, 6 von 10 Paaren).
  - Ein Re-Scan desselben Schlüssels **aktualisiert** Mengen und Kistenliste, statt zu duplizieren; Notice `Updated #n · quantities changed`.
- Ein Scan gilt erst, wenn zwei Lesungen im Abstand von etwa 300 ms denselben Block liefern, sonst Notice „Contract text still loading – scan again“ (Spike-Fall c06).
- `plan.json` 9 → 10:
  - `source: scan`; `scanNo` ersetzt die CT-Id in Legende und Editor
  - kein Rohtext in der Datei; Rohtext und Ergebnis ins rollierende `%LocalAppData%\SC-CargoPlan\scans.log` (letzte 50)
- Ein Scan ist ein Undo-Schritt (K2.4-Addendum).
- **Rohtext aus T10.1a, was der Parser können muss** (Hand-Test `OcrSampleTests`, 40 Aufnahmen):
  - **Ziel-Symbol:** Vor jedem Ziel zeichnet das mobiGlas ein Symbol, das die OCR als eigenen Block „0“ oder „L“ liest. `ReadingOrder` gibt es als eigene Zeile aus (wie der CT-Rohtext). Das ist ein bekanntes OCR-Artefakt, kein Fehler: **Parser-Fixtures sollen eine solche Symbolzeile enthalten.**
  - **Reward:** In Spaltenreihenfolge steht „Reward 81,250“ in einer Zeile in der DETAILS-Spalte nach der Prosa. Der Parser sucht ihn nur am Textanfang (CT-Form) und las ihn in 0 von 40 Aufnahmen.
  - **Max-Größe:** Die zweite Formulierung („At most the containers will be 8 SCU in size.“, `+max2`) fehlt noch.
  - Lieferzeilen brechen wie im CT-Rohtext um („… to High Course Station“ / „at Hurston's L5 Lagrange point.“); das liest der Parser heute schon.
- **Rohtext aus T10.1b** (`scan-probe.log` vom 11.11., 227 Scans über dem Spiel, liegt in Martins `%LocalAppData%\SC-CargoPlan`, CIG-Material, nicht ins Repo):
  - **„O/“ statt „0/“:** In 254 von 877 Lieferzeilen liest die OCR den Zähler als Buchstaben („Deliver O/115 SCU of …“), vor allem in ACCEPTED. ~~Der Parser muss `[0O]/` akzeptieren.~~ Korrektur T10.2a: Das Muster `\S*/` liest sie schon, 877 von 877 Lieferzeilen; kein Verlust, nur ein Wächtertest.
  - **ACCEPTED** beginnt mit einer Zeile „ABANDON“, endet mit „SHARE TRACK“ (9×) oder „SHARE UNTRACK“ (201×) und hat „Contract Deadline N/A“ statt „Contract Availability 2h 1m“; **OFFERS** endet mit „ACCEPT OFFER“. Die Titelzeile („Member Rank - Small Cargo Haul“) steht vor „DETAILS“.
  - Die Zielsymbol-Zeilen „0“ (45×) und „L“ (80×) kommen auch über dem Spiel, immer vor einer „Deliver“-Zeile.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. **Teilung:** T10.2a (`feat/t10-2a-parser`: Import, Cli, Tests, offline prüfbar) und T10.2b (`feat/t10-2b-identity`: Core + App – plan.json 10 mit F4/F24, Scan-Identität, „Scan in den Plan“ als Undo-Schritt, Re-roll, Vollständigkeit, `AppFolders`/F14, `scans.log`). b bekommt nach a einen eigenen Plan.
2. Ocr bleibt Bild → Text; geparst wird in Import, zusammengesetzt in App und Cli.
3. **F7:** Parser, `ContractText`, `ParsedContract`, `ParsedDelivery` nach `Import/Contracts` (`CargoPlan.Import.Contracts`); `Stops()`/`Item()` aus dem Reader als `ContractStops.Of`. Fixtures bleiben in `Fixtures/RawText`.
4. Neuer öffentlicher Typ `ScannedContract` neben `ContractTrackerCandidate` (unberührt bis T10.4a); der Parser bleibt `internal`.
5. „O/“: kein Code, Wächtertest. Blockende auch „SHARE UNTRACK“.
6. Pickup **je Ware** aus den Collect-Zeilen (wie D68); eine Collect-Zeile ohne Punkt nimmt aus der nächsten Zeile höchstens drei Wörter aus Buchstaben mit Punkt („Station.“). Kein eigenes Symbol-Prädikat: eine Symbolzeile hat keinen Punkt.
7. Schlüssel aus **Paaren Ziel:Ware** statt getrennter Listen (H1); unlesbarer Reward oder Max = „?“, kein Joker (D101).
8. Hand-Tests: `OcrSampleTests` bewertet alle Felder und den Schlüssel je Paar; neu `ScanProbeLogTests` mit strukturellen Asserts, Zahlen nur im Bericht (H3); Pickup-Wächter über alle Fixtures (H2).

- [x] **T10.2a** Parser: Pickup, Reward-Zeile, `+max2`, Ansicht, Blockende, `ScannedContract` mit Inhaltsschlüssel (D101), Cli `scan` mit geparstem Vertrag, Hand-Tests
  - **Befund (11.11.2026, Branch `feat/t10-2a-parser`):**
    - **Hand-Test `OcrSampleTests`** (Release, S10.0-Aufnahmen):

      | Satz | Lieferfelder | Pickup | Max | Reward | Ansicht | Schlüssel | p95 |
      |---|---|---|---|---|---|---|---|
      | 3440×1440 | 138/138 | 20/20 | 20/20 | 20/20 | 20/20 | 10 Paare = 10 Schlüssel, keine Kollision | 1 291 ms |
      | 1080p | 135/138 (c06) | 19/20 (c06) | 20/20 | 20/20 | 20/20 | 10 Schlüssel (c06 ausgenommen), keine Kollision | 1 268 ms |

    - **`ScanProbeLogTests`** gegen `scan-probe.log` vom 11.11.: 227 Scans (OFFERS 17, ACCEPTED 210), jede Lieferzeile, jeder Reward, jede Ansicht gelesen; 20 Routen = 20 Schlüssel; der Fuel-Vertrag (191,500) mit 1 OFFERS- und 201 ACCEPTED-Scans unter einem Schlüssel.
    - **Fund im Hand-Test:** Bei 1080p liest die OCR das aUEC-Zeichen vor dem Betrag als „±“ („Reward ± 51,000“, c17). Ein Zeichen, das kein Buchstabe und keine Ziffer ist, darf zwischen Label und Betrag stehen.
    - **Pickup-Wächter (H2):** Zwei OCR-Fragmente im CT-Rohtext („Thundg“, „Green Gle“) stehen als bekannte Fragmente im Test. Mit einer Fortsetzungsregel, die jede Zeile nimmt, wird er auf 3 Fixtures rot.
    - **Offen für T10.3:** Würfelt das Spiel beim Annehmen auch die Ziele neu? Im Probe-Log ist nur ein Paar sicher (191,500). OFFERS und ACCEPTED mit gleichem Reward (100,500; 72,500; 60,750; 50,500; 50,250) haben andere Ziele, vermutlich andere Verträge. Gate-Lauf T10.3 mit echten Paaren: Vertrag ansehen, annehmen, beide scannen.
    - **Gate:** Build 0 Warnungen; Tests Core 1 817 · Import 287 (vorher 164) · App 1 731 · Ocr 34 grün (Hand-Tests gefiltert); `dotnet format --verify-no-changes` 0.
- **T10.2b, Präzisierung in der Planphase, ratifiziert durch Freigabe:** Teilung in b1 (Dateiformat, eigenes Gate) und b2
  (Scan → Plan). Schema 10 bleibt bis v0.4.0 offen, T10.4 ändert es ohne neue Nummer. Ein Re-Scan mit anderen Mengen bei
  geladenen/abgehakten Kisten der Mission geht wie der Editor-Plan (`PlanKeeping`), sonst wird nur die Mission neu gelegt.
  Der `scanNo`-Zähler liegt neben dem Plan (Datei `lastScanNo`, im ViewModel außerhalb der Undo-Schritte), nicht im
  `SavedPlan`. „Plan verwerfen“ setzt ihn auf 0. `Unknown` zählt als geschätzt (D102). F5 nach T10.4a. Die
  Nachfolger-Tabelle von F4 stand vor dem Abbau im Plan (P1).
- [x] **T10.2b1** `AppFolders` (F14), Plan-Schema 1–7 raus (F4), Schema 10 mit Scan-Identität, Goldens (F24), D102
  - **Befund (11./12.11.2026, Branch `feat/t10-2b1-format`):**
    - **F4:** Lesbar 8, 9, 10; 1–7 abgelehnt mit „expected 8 or 9 or 10“. Weg: `PlanFileV1Dto`, `PlanFileV2Dto`,
      `PlacementDto`, `ParseMissions`, `EmbeddedSchemaVersions`, vier `Check*`, zwei Alt-Zweige. 38 Core-Tests gestrichen,
      17 neu. **Nachfolger (Regel P1):** Verhalten → Nachfolger mit aktueller Datei, je grün gegen den alten Code und per
      Mutation rot (Hinweise neu abgeleitet; Racks geändert; ganzer Stopp abgehakt; 7 Validierungsfälle aus Schema 2 nach
      `V3Changes`), oder ein bestehender Test namentlich (Round-Trips für Haken, Positionen, `manual`, `loaded`, Item
      Pflicht, fremdes Schiff, Zurückschreiben); nur Alt-Format → Theory „Schema 1–7 wird abgelehnt“. Tabelle im
      Commit-Body von `5abaf6d`.
    - **Alt-Fixtures als Schema 9:** `before_b2_eight_stops`, `hurston_2026-10-23`, `hurston_2026-10-25` (19/17/23 Kisten,
      Rack, Position, Haken vorher/nachher identisch), `split_and_shared` (Schema 1: SCU je Rack/Ziel/Ware identisch; neuer
      Wächter für den `--plan`-Befehl). `split_and_shared_v2` gelöscht.
    - **Goldens:** Diff je Datei nur die `schemaVersion`-Zeile.
    - **Scan-Identität:** vier Weichen in `ContractSync`/`LoadPlanner`, jede vorher rot (Scan wäre gone geworden, nach
      Undo doppelt gekommen, `Quiet` nie ruhig, `DeliveryKey` warf).
    - **Martins plan.json** (Kopie, Schema 9, RAFT, 5 Missionen, 36 Kisten, Watermark 290): liest, schreibt als 10,
      Kisten byte-identisch.
    - **Gate:** Build 0 Warnungen; Tests Core 1 817 · Import 287 · App 1 737 · Ocr 34 grün; `dotnet format` 0.
      `CrateLayoutTests.Place_proves_stacking_in_under_100_ms` fiel einmal unter Last aller vier Testprojekte, allein 65 ms
      und in zwei weiteren Läufen grün.
    - Den `--plan`-Befehl habe ich nicht selbst gestartet: die App schreibt beim Speichern in Martins echte plan.json
      (Lehre T10.1b); der neue Wächtertest prüft die Datei.
- [x] **T10.2b2** Scan → Plan: Abgleich per Schlüssel, ein Undo-Schritt, Re-roll-Markierung, zweite Lesung nur bei
  Verdacht, `ScanIntake`, `scans.log`, D103
  - **Präzisierung in der Planphase, ratifiziert durch Freigabe:** Abgleich in Core (`ScanSync`); Verdacht in zwei Stufen
    (hart: Objectives ohne Lieferung, Objective-Zeile ohne Inhalt → nach zweiter Lesung `Incomplete`; weich: Reward fehlt,
    Ware ohne Collect-Zeile, **weniger Ziele als die Liste (E1, Martin)** → übernommen); `Refused`, wenn `PlanKeeping`
    keinen Platz findet; Hand-Items bleiben beim Update; `ScanIntake` mit Delegates statt Interface. Ersetzt die
    Task-Zeile „zwei Lesungen im Abstand von 300 ms“ (Gate 1).
  - **Befund (12.11.2026, Branch `feat/t10-2b2-scan-into-plan`):**
    - `ScanSync`: neu → `Added #n`; Offer nach Accepted → nichts; gleiche Mengen → Markierung weg oder nichts; andere
      Mengen → nur die Mission neu (`ReplaceMission`, gleiche Id und Stelle) oder mit geladenen/abgehakten Kisten
      `PlanKeeping`. Martins Test „neue Menge kleiner als bereits geladen“: Lieferung neu geplant und in `Cleared`
      genannt. Mutation „immer nur die Mission neu“ macht die drei Fälle mit geladenen Kisten rot.
    - `ApplyScan`: ein Undo-Schritt, auch der erste Scan in einen leeren Plan; Nummer nach Undo nicht wiederverwendet,
      nach einem rückgängig gemachten Verwerfen über den Scans des Plans. E1: „⚠ 1 of 2 destinations“, nach Hinzufügen
      des Stopps im Editor weg. Die App-Tests liefen erst nach dem Code; rot nachgeholt per Mutation (2 Stellen, 3 Tests rot).
    - `ScanIntake`: zwei getrennte Scanner-Aufrufe, der Riegel ist während der Wartezeit frei (Test mit Fake-Scanner,
      der bei laufender Lesung `Busy` meldet). `ScanLog`: letzte 50, atomar, wirft nie.
    - **Hand-Tests:** S10.0 harter Verdacht nur bei c06, sonst keiner (39/40); `scan-probe.log` 227 Scans ohne jeden
      Verdacht: über dem Spiel keine zweite Lesung.
    - **Gate:** Build 0 Warnungen; Tests Core 1 828 · Import 321 · App 1 762 · Ocr 34 grün; `dotnet format` 0.
      `CrateLayoutTests.Place_proves_stacking_in_under_100_ms` fiel im Gesamtlauf erneut (142 ms), allein grün – siehe
      Backlog.
  - Gefunden: `PickupNames.Clean` (`PickupNames.cs:44`) baut ein Item ohne `Manual` neu; ein Handeintrag einer
    CT-Mission verliert beim Bereinigen seine Markierung. `Clean` geht mit F23 in T10.4a.

### [x] T10.3 Hotkey, Editor, Notices

- **Aus T10.2b2 (D103):** `ScanIntake` an Hotkey und Editor-Button hängen, `ApplyScan` per Dispatcher auf den UI-Thread;
  eine Notice je `ScanReport` (`Added`, `Updated` mit „quantities changed“, `AlreadyInPlan`, `Refused` mit Grund,
  `Incomplete` = „Contract text still loading – scan again“, `NoContract` und die Scanner-Ergebnisse). Die `Cleared`-Texte
  aus `PlanKeeping` sagen „changed in the editor“ – für einen Scan umformulieren. Erst ab hier schreibt `scans.log` ins
  echte Profil.
- Hotkey `scanContracts`, Default **F3** (Ausnahme zu D35), gepollt nach B10/D97, aktiv nur bei SC- oder CargoPlan-Vordergrund.
- Editor-Button „Scan“ ersetzt „Sync from ContractTracker“.
- Notices unten links: `Scanned #3 · From … · n SCU to …`, `Updated #3 · quantities changed`, `Already in plan (#2)`, `No contract in frame`, `Star Citizen window not found`.
- Ein Vorschau-Scan vor der Annahme ist eine normale Mission, Notice „Rescan after accepting“; nicht genommen → Undo.
- Handling gegen `main`.
- **Aus T10.1b:**
  - Der Scan läuft über `ContractScanner.ScanAsync` (ein Scan gleichzeitig, Warten aufs Warmup höchstens 15 s). Ein zweiter Druck während eines Scans (1,3 s) wird verworfen: In der Probe drückte Martin im Abstand von 0,5–0,8 s, 5 von 12 Drücken fielen weg. Entscheiden, ob das so bleibt oder eine Notice bekommt.
  - Das Blinken eigener Fenster über dem Vertrag (D100) ist die Scan-Rückmeldung; keine eigene Anzeige nötig.
  - Frame-Zeit unter Scans neu messen, mit dem Messfenster **neben** dem ROI (`--scan-load`): die T10.1b-Zahl ist durch das D100-Blinken des Messfensters verfälscht.
  - **Gate:** p95 Taste → Rohtext ≤ 1 500 ms über dem Spiel, n ≥ 20 je Ansicht (OFFERS, ACCEPTED). Vergleich T10.1b: 1 304 / 1 391 ms bei n = 10.
  - **Speicher-Vergleichszahl:** T10.1b +0,03 MB pro Scan über Scan 71–210 (≈ 3 MB je 100 Scans), Band 773–892 MB.
  - Mit T10.5 entscheiden, ob „Scan aus“ auch das Warmup spart: die Engine kostet ≈ 400 MB Working Set (≈ 824 statt ≈ 400 MB vor OCR).
- **Präzisierung in der Planphase, ratifiziert durch Freigabe** (Branch `feat/t10-3-scan-hotkey`, D104):
  1. F-Tasten ohne Modifier: **F1–F11 und F13–F24** (eine Regel), alle Aktionen; F12 und Space bleiben gesperrt.
  2. Default **F3** über den Parameter-Default von `HotkeySettings.ScanContracts`; fehlt = F3, `null` = aus.
     **B1 (Bedingung der Freigabe):** `null` landet auf der Platte (kein Ignore im `SettingsJsonContext`); Test „löschen →
     speichern → laden → aus“, Mutation `WhenWritingNull` macht ihn rot.
  3. CT-Hinweis über den Prozessnamen **`SC Contract Tracker`** (Exe von CT 2.2, nicht `ContractTracker*`), in der
     Hotkeys-Zeile, einmal je Lauf, nur wenn die Scan-Taste F3 ist.
  4. Zweiter Druck: Notice „Scan running“, kein Puffer.
  5. Editor: „Scan“ **kommt dazu**, „Sync from ContractTracker“ bleibt bis T10.4a.
  6. Scan-Ergebnisse in der **Move-Zeile** (Martin), die Scan-Zeile bleibt dem Scanner-Zustand.
  7. Added-Notice: mehrere Ziele als Summe (Martin).
  8. `ScanIntake` misst Taste → Rohtext und Taste → Notice selbst, Zeilen in `scans.log` und `cargoplan.log`.
  9. `ScanReport.Refusal` → `Reason` (auch der Win32-Fehler einer Aufnahme); `PlanKeeping` nennt den Auslöser.
  10. Gate 4: `--scan-load` öffnet das Messfenster links neben dem ROI.
  11. Commit-Reihenfolge: Core und Runner vor dem Hotkey (der Hotkey braucht den Runner).
  - **Release-Notes v0.4.0 vormerken:** „F3 scans the open contract; change or clear it in Settings if F3 is bound in Star
    Citizen.“ Und die Frame-Zeit unter Scan-Last aus Gate 4. README und Wiki bekommen „Open the contract and press F3“
    **zum Release** (Martin): das Wiki steht auf „Applies to v0.3.1“ und ist live.
  - **Befund (11.11.2026, Branch `feat/t10-3-scan-hotkey`, Release, SC 3440×1440, Martin bedient):**
    - **Gate 1** (Taste → Rohtext und Taste → Notice aus `scans.log` des echten Hotkey-Pfads, gelesene Scans):

      | Ansicht | n | Taste → Rohtext Median / p95 | Taste → Notice Median / p95 / max |
      |---|---|---|---|
      | OFFERS | 24 | 1 302 / **1 383 ms** | 1 308 / 1 389 / 1 571 ms |
      | ACCEPTED | 22 | 1 339 / **1 423 ms** | 1 345 / 1 423 / 1 694 ms |

      Gate ≤ 1 500 ms erfüllt (T10.1b bei n = 10: 1 304 / 1 391 ms). 20 Drücke fielen in einen laufenden Scan (Abstand
      ≈ 1,1 s, Scan ≈ 1,3 s) → `Scan running`; deshalb n aus den Gate-1- und Gate-2-Kopien zusammen. Die 1 694 ms sind der
      erste Scan nach dem Start (Rohtext → Notice 244 ms), danach 0–20 ms.
    - **Gate 2 – echte Paare** (OFFERS scannen, annehmen, ACCEPTED scannen), alle `Added #n` → `Updated #n · quantities changed`:

      | # | Ware(n) | SCU OFFERS → ACCEPTED | Ziele gleich | Reward gleich |
      |---|---|---|---|---|
      | 1 | Waste | 11 → 10 | ja | ja (72 500) |
      | 2 | Pressurized Ice + Processed Food, 4 Ziele | 7/4/3/7 → 6/4/6/7 | ja | ja (100 500) |
      | 3 | Stims, 4 Ziele | 3/3/2/2 → 2/3/2/3 | ja | ja (72 500) |
      | 4 | Waste | 6 → 9 | ja | ja (50 250) |
      | 5 | Carbon | 8 → 9 | ja | ja (45 000) |
      | 6 | Pressurized Ice + Processed Food, 4 Ziele | 6/5/4/4 → 4/5/3/6 | ja | ja (81 250) |

      **Antwort auf die Frage aus T10.2a:** Das Spiel würfelt beim Annehmen **nur die Mengen** neu (6 von 6), nie Ziele,
      Pickups oder Reward. Ein Rescan von #1 danach: `Already in plan (#1)`. Kein Paar als zweite Mission.
    - **Gate 3** (Martin: alle Fälle durchgespielt, Logzeilen wo vorhanden): Doppeldruck (20× „another scan is running“ →
      `Scan running`), ohne mobiGlas (2× `No contract in frame`), ohne SC, Editor-Button mit Editor vorn, Undo nach Scan,
      Scan in leeren Plan + Undo, `Updated` mit geladenen Kisten, **CT-Hinweis kam** (Prozessname bestätigt). Ein
      Nicht-Hauling-Vertrag (Salvage, Bounty) ergibt `NoContract` → `No contract in frame`. Keine zweite Lesung: das Spiel
      wurde nie beim Eintippen erwischt (keine „doubt“-Zeile).
    - **Gate 4** (`hull_c`, `--scan-load`, Messfenster links neben dem ROI, Vertrag offen, 3 Läufe): 26,2 / 38,0 / 29,2 %
      späte Frames, **Median 29,2 %**, Frame-Median 8,33 ms, 86–95 fps; T10.1b mit mittigem Fenster 25,9–30,0 %. Martin:
      flüssig. **Befund:** Das 8,33-ms-Muster bleibt auch neben dem ROI; die T10.1b-Erklärung (D100 blinkt das Messfenster)
      trägt nicht, die Ursache ist offen (mehr Renderdurchläufe unter Last, kein Ruckeln). `reclaimer` 1 Lauf: 6,2 %.
    - **Speicher:** Overlay nach 40 Scans **770 MB** Working Set, 770 MB Private Bytes (T10.1b-Band 773–892 MB nach > 70 Scans).
    - **Gate 5:** Build 0 Warnungen; Tests Core 1 828 (sauber 1 827, lokale Scratch-Datei) · Import 321 · App 1 826
      (vorher 1 762) · Ocr 34 grün; `dotnet format --verify-no-changes` 0. CI nach Martins Push.
    - Martin: „funktioniert einwandfrei, schneller als CT, erkennt zuverlässig ob Vertrag, Offer, Accepted, korrigiert“.


### [x] T10.4a ContractTracker entfernen – Import, App, Doku

- Checkliste = CT-Inventur aus dem S10.0-Befund.
- CT-Reader, `ContractTrackerSync`, FileSystemWatcher, Fingerabdruck und Reset-Schutz raus.
- `settings.json` 2 → 3 mit Migration:
  - `contractTracker` raus
  - `syncContractTracker` raus (siehe Präzisierung 2)
  - Altfelder `pxPerCell`/`panX`/`panY` raus
- D81 `onContractGone` entfällt ersatzlos: kein „gone“ mehr, einen Plan verlässt man nur über Abhaken, Löschen oder Discard.
- CLI `contracts --raw` → `scan <png>`.
- Doku: README, Wiki, `docs/settings-format.md`, `docs/nexus/*`, Issue-Templates (Feld „ContractTracker-Version“ → „Screenshot des Vertrags“).
- **F5 (Review 2026-11-pre-p10):** Mission-Schema-Leser 1 und 2 (`MissionFileReader.cs:18-32`, `LegacySchemaVersion`,
  `NoFillOrderSchemaVersion`) raus; v0.1.0 schrieb Mission-Schema 3. Die Mission-Fixtures der Schemas 1/2 (≈ 9 Dateien, u. a.
  `example_missions`, `split_and_shared` des Cli-Befehls `plan`) als aktuelles Schema neu speichern, Nachfolger-Regel wie bei F4
  (T10.2b1). Kam von K10 über T10.2 hierher, weil T10.2b1 nur die Plan-Leser abbaut.
- **Präzisierung in der Planphase, ratifiziert durch Freigabe** (Branch `feat/t10-4a-ct-removal`, D105):
  1. Kein Abschnitt `scan` in Schema 3: der Hotkey liegt in `hotkeys.scanContracts` (D104), `notifyOnScan` kommt in T10.5.
  2. `syncContractTracker` (F2: so heißt der Key) wird **gelöscht, nicht umbenannt**; ein gesetzter Wert gibt einmal eine
     Notice, die Datei wird dann gleich als 3 geschrieben (`.bak` = Original). Jede andere Migration schreibt nach D31 nichts.
  3. Migration als `JsonNode`-Umschreibung vor dem typisierten Lesen; eine Kamera mit gesetztem `pxPerCell`/`panX`/`panY`
     fällt ganz weg (so las sie der Code vorher), `Disallow` bleibt.
  4. Fixtures: das Doku-Beispiel (in v0.1.0–v0.3.1 byte-gleich), eine Datei mit jedem Key von v0.3.1, eine T10.3-Datei;
     Martins Kopie nur im Scratchpad.
  5. D81 ersatzlos auch in der Darstellung; `PickupNames` ganz weg (`Parse` hatte nach dem Reader keinen Aufrufer).
  6. Nutzer-Doku (README, `outlines/NOTICE.md`-Zeile, `docs/nexus/description.bbcode` + `checklist.md`, `bug.yml`) auf
     Branch `docs/v0.4.0-user-docs`, Wiki auf lokalem Branch `v0.4.0`; beide werden in T10.6 gemergt.
  7. Teilung a1 (Code, Commits 0–5) und a2 (F5, Doku); B1/B2 der Freigabe: `--data-dir` (eigener Commit),
     Beleg-Test für `Resync(present: null)`.
- **Befund (11.11.2026):**
  - **a1** gemergt `--no-ff` bei `0f4a314`: `--data-dir` (552c052), B2-Beleg (a1229b8), Sync raus (9f7deae), Settings 3
    (1317252), Gone-Darstellung (a7d6b77), Reader/SQLite/Cli `contracts` (de087a8), `PickupNames` (9961c26).
    **a2:** F5 (ad67e01), Doku.
  - **Gate 1 – Migration:** alle Formen laden ohne Problem-Notice und sind gleich dem, was der Code vorher las, minus der
    entfernten Keys:

    | Datei | Keys vorher → nachher | Problem | = Erwartung |
    |---|---|---|---|
    | Doku-Beispiel v0.1.0–v0.3.1 | 66 → 71 | nein | ja |
    | v0.3.1 mit jedem Key | 100 → 86 | nein | ja |
    | T10.3 mit `scanContracts` | 89 → 83 | nein | ja |
    | Martins Kopie | 105 → 99 | nein | ja |

    Echte Dateien enthalten auch `contractTracker.databasePath` (die berechnete Property wurde mitgeschrieben). Start von
    Hand mit `--data-dir` auf Martins Kopie: Notice kam (Martin), Datei als 3 zurückgeschrieben, `.bak` = Original.
  - **Gefunden per Test (A3):** ein Key doppelt in einem Objekt ließ `JsonObject` eine `ArgumentException` werfen, die
    `Load` nicht fing – Absturz beim Start. Jetzt „Settings not loaded“ wie ein Tippfehler (D105).
  - **Gate 3:** der Scan-Pfad ist unverändert; Martins Lauf fand einen Fehler aus T10.3/D100 (Editor-Titelleiste im
    OCR-Text) → **B12**. a1 trotzdem abgenommen (Martin).
  - **ZIP:** 86 426 805 → 85 227 035 Bytes, 386 → 381 Dateien (SQLite weg).
  - **Tests** (ohne lokale Scratch-Tests): main Core 1 827 · Import 321 · App 1 826 · Ocr 34 → Core 1 820 · Import 279 ·
    App 1 776 · Ocr 34. Gestrichene Tests mit Nachfolger je Commit im Commit-Body.
  - **Gate 2 – Ausnahmeliste** (Treffer von `git grep -iE 'contracttracker|contracts\.db|ct_copy'`, die bleiben):
    (a) `ScanKeyCheck` + Tests + `HotkeyService`/`App.xaml.cs` (D104: CT auf F3); (b) Herkunft des Parser-/OCR-Korpus
    (`Import/Contracts`, `Ocr/ReadingOrder.cs`, `ScanResult.cs`, Parser-/Scan-/ReadingOrder-Tests, `ImportFixtures`);
    (c) Plan-Schema-Felder bis T10.4b (`contractTrackerWatermark` in Plan-Fixtures, `plan-format.md`, `docs/shots/*.plan.json`);
    (d) Geschichte: TASKS.md (S10.0–T10.4a), `decisions.md`, `release-notes/`, `review/`, `docs/nexus/staff-request.md`,
    CLAUDE.md (A11, OCR-Begründung); (e) die Migration nennt die alten Keys (`SettingsStore`, Settings-Fixtures und
    -Tests, `settings-format.md` „Older files“). Nutzer-Doku auf `docs/v0.4.0-user-docs` bis T10.6.
  - **Core-Restliste für T10.4b: 36 Dateien, 82 Zeilen** – `Missions/` ContractCapture, ContractSource, Mission, MissionItem,
    Pickups; `Planning/` ContractGonePolicy, ContractSync, LoadPlanner, SyncResult; `Plans/` ContractSourceDto, PlanFile,
    PlanFileDto, SavedPlan; Tests ContractGone, ContractSync, Discard, FillRowsFormat, LoadedState, ManualItem, Pickup,
    PlanFile, ScanIdentity, UndoRestore; 14 Plan-Fixtures (Goldens, `before_b2_eight_stops`, `raft_hand`, `split_and_shared`).
    Tot nach a, für b: `ContractSync.Apply/Sync/Quiet`, `SyncResult`, `ContractGonePolicy`; der Gone-Filter in
    `LoadPlanner.RetryUnplaced` lässt eine wartende Mission mit Gone-Markierung nie platzieren.
    **→ 0 nach T10.4b (D107).**

### [x] B13 Eine nicht lesbare plan.json überschreibt der erste Scan

- Gefunden in T10.4a: Der Pause-Schutz (T5.4, „plan.json couldn't be read: the sync must not overwrite it“) galt nur dem
  CT-Sync. Seit T10.3 legt der erste Scan in einen leeren Plan und `PlanSaver` überschreibt die kaputte Datei, ohne Backup.
- Wichtiger als B12 (Martin). Eigener Plan; Vorschlag dort: `.bak` wie bei settings.json und/oder Scan-Pause mit Notice.
- **Erledigt (D106), Branch `fix/b13-plan-keep-unreadable`:** Beiseitelegen im einzigen Schreiber statt Blockieren. Der
  erste Save des Laufs verschiebt die unlesbare Datei nach `plan.json.unreadable-yyyyMMdd-HHmmss` und schreibt dann; die
  Plan-Notice nennt die Datei bis zum Schließen. Scheitert das Verschieben, wird nichts geschrieben.
  - Befund: `--plan` las `plan.json` gar nicht; jetzt ein zweiter Load nur für das Flag (`PlanStore.LoadStart`, vorher
    `App.LoadStartPlan`). `plan-format.md` versprach „never overwrites a plan file it can't read“ schon – jetzt stimmt es.
  - Präzisierung in der Planphase, ratifiziert durch Freigabe: Beiseitelegen statt Blockieren; Fehlertext mit voller
    Exception-Message statt „is locked“; Datei inzwischen weg = kein Fehler; Kept-Notice bleibt für den Lauf (wie
    `HoloUpdate`); `PlanSaver.Flush` für Tests; Hand-Test „gesperrt“ statt „schreibgeschützt“ (Read-only lässt Umbenennen
    zu); Zeit-Asserts als erster Commit aus dem Standardlauf (Backlog).
  - Downgrade: Schutz nur ab dieser Version. v0.3.x liest Plan-Schema ≤ 9 ohne Beiseitelegen und überschreibt eine
    Schema-10-Datei beim ersten Ändern → Release-Note v0.4.0 (T10.6): plan.json vorher kopieren.
  - **Gate:** Build 0 Warnungen; Tests (sauberer Worktree) Core 1 818 (−2 nach Timing) · Import 279 · App 1 791 (+15) ·
    Ocr 34 grün; neue Tests vorher rot gegen Stubs (14 von 15); `dotnet format` Exit 0.
  - **Hand-Test ohne SC** (`--data-dir` im Scratchpad): (1) abgeschnittene `plan.json` + `--plan split_and_shared.json` →
    `plan.json` 7 325 B neu, `plan.json.unreadable-20261111-173239` 200 B, Notice im Overlay, Logzeile. (2) dieselbe Datei
    von einem PowerShell-Prozess mit `FileShare.None` gesperrt → nichts geschrieben, Notice „Plan not saved: plan.json
    couldn't be moved aside (The process cannot access the file …)“, Datei nach Programmende byte-gleich. „Sperre lösen,
    Plan ändern“ nur per Test (`A_locked_file_is_not_overwritten_and_kept_aside_once_it_is_free`).

### [x] B12 Fenster mit Titelleiste bleiben bei der Aufnahme im Bild (D100)

- Gate 3 von T10.4a: Scan per Editor-Button mit dem Editor über dem Vertrag → OCR liest den Fenstertitel „Missions“ in die
  Zielzeile („… to Melodic Fields Missions“) → anderer Schlüssel → `Added #2` statt `Updated #1` (`scans.log` 11.11.,
  15:50:01/:08/:24). `Opacity = 0` wirkt nur bei Fenstern mit `AllowsTransparency` (Overlay, Lock).
- Entscheidung (Martin): **DWM-Cloak** (`DWMWA_CLOAK`) für alle unsere Fenster statt Opacity, vorher über dem Spiel messen
  wie D100 (`OverlayCaptureTests` um ein Fenster mit Titelleiste erweitern); Addendum D100.
- **Erledigt (D100-Addendum B12), Branch `fix/b12-clearance-cloak`:** Cloak für alle unsere Fenster, **je Owner-Kette nur
  der oberste** (ein besessenes Fenster verschwindet per DWM-Vererbung mit), ein `DwmFlush`; Opacity-Code raus.
  - **Präzisierungen in der Planphase, ratifiziert durch Freigabe:** Chrome-Marker mit Fluent-Theme wie der Editor;
    Zeilen R5 (Vererbung) und R5b (nimmt der Uncloak des Owners unser Cloak mit?, aus der Freigabe); Ereigniszähler je
    Versuch; eigener `[Fact]`, D100-Tabelle bleibt reproduzierbar; Cloak-Fehler geloggt; P/Invoke bleibt als Messwerkzeug;
    T10.6-Satz wörtlich mit D106-Verweis (aus der Freigabe). Nach der Messung (Auswahlfragen): Cloak für alle; Bedingung
    „besessenes Fenster zuerst zurück“ → R5c gemessen: `E_INVALIDARG` → Regel „nur der oberste Owner“ (R5d); Tests für
    die drei Owner-Fälle im Default-Run; Gate 1 ausdrücklich mit Overlay **und** Editor über dem Vertrag.
  - **Messung über dem Spiel** (`OverlayCaptureTests.Cloak`, 3440×1440, je 20 Versuche, Median/p95): Kontrollen Ref
    Titel 0/20, C0/C1 20/20 erkannt. Chrome Opacity 0 (alt): **Titel 20/20**, 27,3/32,6 ms. Chrome Cloak + 1 Flush 0/20,
    30,1/52,9 ms (ein Ausreißer; R4 gleicher Weg 27,7/32,1 ms); layered Opacity 0 (R1) 0/20 27,9/34,4 ms, layered Cloak +
    1 Flush 0/20 31,6/35,0 ms; Fokus 20/20 gehalten, keine Fenster-Ereignisse. Owner-Zeilen (über dem Desktop): R5
    besessenes Fenster mit weg 20/20; R5c besessenes zuerst zurück → `0x80070057` 20/20; R5d nur Owner → sauber; alle
    Varianten am Ende sichtbar 20/20.
  - **Gefunden im Harness:** `ReadingOrder` liefert ohne Vertragsüberschrift keine Zeilen → Titel in den OCR-Blöcken
    gesucht; Chrome-Fenster bei Opacity 0 hat schwarzen Client (Magenta allein übersieht es); `Hide` am Owner lässt das
    besessene Fenster stehen (nur Minimieren nimmt es mit).
  - **Tests:** `RegionClearanceTests` 10 (3 D100-Nachfolger + 7 neu), vorher 8 rot gegen Stub; sauberer Worktree Core
    1 818 · Import 279 · App 1 798 (+7) · Ocr 34 grün; Build 0 Warnungen; `dotnet format` Exit 0.
  - **Gates** (Release, `--data-dir` im Scratchpad, 12.11.): (1a) Overlay + Editor über dem Vertrag, Button: `Added #1` →
    ACCEPTED `Updated #1`; (1b) Settings + F3, (2) nur Overlay + F3: `AlreadyInPlan #1`; Rohtexte aller 5 Scans ohne
    „Missions“ und ohne unsere Fenstertitel; capture 23–37 ms, Taste→Text 1 189–1 401 ms; (3) Fokus im Feld gehalten;
    alle Fenster blinken (Vererbung, gewollt). Martin: „es hat alles gepasst“.
  - Unterwegs: Die Mengen ändern sich zwischen OFFERS und ACCEPTED **immer** (Spielbug, Martin) – `Updated … quantities
    changed` nach dem Annehmen ist erwartet, kein OCR-Fehler.

### [x] T10.4b ContractTracker entfernen – Core, Planer

- `LoadPlanner` (35× Watermark), Identität D39/D42/D44 auf den Inhaltsschlüssel umstellen, Folgen fürs Plan-Schema.
- Eigener Plan **mit Zahlen** (Golden-Files, Planer-Laufzeit).
- Addenda: D37–D44 (superseded), D38/D42 (identity = content key), D81 (removed), K6.1 (`#scanNo`).
- **Erledigt (D107), Branch `feat/t10-4b-core-identity`** (`d73d5a5`…, 7 Commits): Der Planer kennt Scan oder Hand.
  `ContractSource(Scan, CrateSizeAssumed, ListedDestinations, Reward)`; `SavedPlan` ohne Watermark; `ContractSync`,
  `SyncResult`, `ContractGonePolicy`, `RemoveDeliveries` weg, die Duplikat-Regel heißt `PossibleDuplicates.Of`, Undo/Redo
  stellt per `RetryUnplaced` wieder her; Liefer-Schlüssel (Scan-Nummer, Ziel); Gone-Filter weg (Befund a1 erledigt).
  Plan-Schema 10 endgültig: Schreiber ohne CT-Felder, Leser nimmt `contractIds/gone/detached/contractTrackerWatermark`.
  - **Präzisierungen in der Planphase, ratifiziert durch Freigabe:** (1) Eine CT-Capture älterer Dateien wird beim Lesen
    eine **Scan-Mission mit Ersatzschlüssel** `legacy:<ids>` (Martins Antwort auf die Rückfrage zu Vorgabe 1), Nummern
    über `lastScanNo` und dem höchsten Scan in Dateireihenfolge, nicht geschätzt, Legendenfelder bleiben; Notice
    „n contracts from ContractTracker are scan missions #a–#b now.“, Einzahl eigene Form. (2) Fixtures: Vorgabe 9 nannte
    `split_and_shared`/`before_b2_eight_stops`, beide ohne CT-Mission; die Tests tragen die echten CT-Fixtures
    (`raft_hand_2026-11-05` Schema 8 mit `gone`, `hurston_2026-10-25` und `-23` Schema 9), keine Fixture umgeschrieben
    (Martin), neu nur `ct_and_scan_schema10.json` (von Hand). (3) Reihenfolge: Goldens zuerst (der Test bricht, sobald
    der Schreiber das Feld weglässt), `ContractSync` vor dem Watermark (sonst kein mechanischer Commit). (4) `Duplicates`
    in eigene Klasse; `DeliveryKey` ohne Art-Enum; `Scanned()` weg, der Name `ContractSource` bleibt.
  - **Gefunden an Gate 1:** Jeder Vertrag wurde mit `"isCapture": false` geschrieben (berechnete Property im DTO, wie
    `contractTracker.databasePath` in T10.4a); behoben vor dem Merge, ein Test prüft die Schlüssel eines Vertrags exakt.
  - **Gefunden beim Streichen:** 33 Aufrufe übergaben den Watermark als Literal `0`, das ohne Parameter still in
    `FillRows` gerutscht wäre (int-Literal 0 → Enum); vorher entfernt, Restprüfung aller Aufrufe. Zwei `cref` auf die
    alte `Plan`-Signatur (kein XML-Doku-Build fängt sie).
  - **Gate 1:** 5 Dateien mit `main` und Branch gelesen und geschrieben: Kisten, Stops, Missions-Ids, `unplaced` gleich;
    Unterschiede nur `contract` (Ids → `legacy:`-Scan, `gone` weg), Watermark, `lastScanNo`. RAFT 28 Kisten #1–#4,
    Hurston 25.10. 23 Kisten #1–#4 (Mission 4 steht vor 3 in der Datei: die alte #37–38 ist #3), Hurston 23.10.
    17 Kisten #1–#3, Mischdatei #6/#7 neben Scan #2 (`lastScanNo` 5), Martins `plan.json` (seit 11.11. nur Scans #1/#2)
    9 110 → 9 076 B, nur die Watermark-Zeile. Eine echte Datei mit CT **und** Scan gibt es nicht mehr (`plan.orig.json`
    ist Schema 4). Goldens: je eine Zeile weniger.
  - **Gate 2 – Ausnahmeliste** (`git grep -iE 'contracttracker|watermark|\bgone\b|detached|contractids' -- src`,
    101 Zeilen): (a) ScanKeyCheck 18 (`ScanKeyCheck` 13, `HotkeyService` 4, `App.xaml.cs` 1); (b) Parser-/OCR-Herkunft
    10; (e) Settings-Migration 6; **(f) neu: Lesepfad ältere Dateien und Ausbau-Vermerk 39** (`ContractSourceDto` 10,
    `PlanFileDto` 8, `PlanStore` 8, `PlanFile` 7, je 1 `App.xaml.cs`, `MissionDto`, `ContractSource`, `MissionText`,
    `OverlayViewModel`, `PlanHistory`; dazu der String `legacy:`); **(g) neu: „gone/detached“ im Wortsinn 28** (z. B.
    `LoadPlanner` 7 für gelieferte Kisten, `MissionEditorViewModel` 4, `NativeMethods.Capture` 3, `PlanHistory` 3).
  - **Gate 3 – Zahlen** (sauberer Worktree, Release, je 3 Läufe, Median; vorher `7e7d45f`, nachher `3d1bddd`):

    | Messung | vorher | nachher |
    |---|---:|---:|
    | `Place_proves_stacking_in_under_100_ms` | 63 ms | 62 ms |
    | `Place_lays_out_16_crates…_in_under_100_ms` | 4 ms | 4 ms |
    | `MoveResolutionTimingTests` Freelancer / Hurston | 321 / 41 ms | 312 / 42 ms |
    | `FillOrderTimingTests` RAFT-Layout | 641 ms | 617 ms |
    | `ReplanTimingTests` erster Stopp geladen / Schiffswechsel | 451 / 42 ms | 428 / 42 ms |
    | `UndoTimingTests` Undo / Redo Hurston (Tabelle) | 0,42 / 0,35 ms | 0,40 / 0,35 ms |
    | `UndoTimingTests` Speicher Hurston allein / Move (Test allein, 7 Läufe) | 10,2 / 1,8 KB | 10,2 / 1,8 KB |
    | `HullAGoldenTests` (Lauf) | 698 ms | 681 ms |
    | Default-Run Wand Core / Import / App / Ocr | 25,2 / 1,1 / 6,5 / 5,8 s | 25,2 / 1,1 / 6,8 / 5,3 s |

    Über 10 % nur Werte, die in ganzen Sekunden gemeldet werden (`Plan_of_each_case` 8 → 7 s) oder streuen (Ocr-Wand
    5,2–5,8 s). **Befund Messmethode:** Der Speicher je Schnappschuss streut im gemeinsamen Timing-Lauf stark (Hurston
    vorher 4,2–19,9 KB, nachher 3,2–13,6 KB), allein ist er deterministisch und gleich.
  - **Tests** (sauberer Worktree): Core 1 818 → 1 770 (−48), Import 279 → 280, App 1 797 → 1 793 (−4), Ocr 34. Je Commit
    gestrichen/neu mit Nachfolger im Commit-Body; neue Tests vorher rot, Nachfolger per Mutation rot gesehen.
  - **Gate 4 (über dem Spiel, Martin, 12.11.):** OFFERS- und ACCEPTED-Scan (`Added`/`Updated`), einmal Undo – „funktioniert
    alles“. Abgenommen (Martin).
  - Unterwegs: `ScreenCaptureTests.A_hundred_captures_leave_the_gdi_object_count_as_it_was` einmal rot im Gesamtlauf
    (prozessweite GDI-Zahl neben parallelen WPF-Tests), allein und im nächsten Lauf grün → Backlog. Mit `--no-build`
    gegen eine veraltete App-Test-DLL (Build gescheitert) hing der App-Lauf; Tests nur nach grünem Build.

### [x] T10.5 Settings „Scan“ und CLI-Abschluss

- Settings-Abschnitt „Scan“: Hotkey, `notifyOnScan`, Hinweis „Open the contract and press F3“, Reset wie D30.
- `--keep-scans`: PNG nach `scans\`, höchstens 20.
- Known issues:
  - ROI bei 32:9 und 16:10 ungeprüft, 4K ungeprüft
  - Verträge über eine Bildschirmseite
  - F3 von laufendem CT belegt (Notice mit Verweis auf die Settings)
- **Präzisierung in der Planphase, ratifiziert durch Freigabe** (Branch `feat/t10-5-scan-settings`, D108):
  1. **Kein `notifyOnScan`** (A4): Fehler-Notices und Blinken bleiben, ein Schalter nur für Added/Updated/AlreadyInPlan hat
     keinen Anlass; nach D31 jederzeit nachrüstbar.
  2. **„Scan aus“ = Taste gelöscht, dann kein Warmup beim Start.** `ComposeOcr` gibt es nicht: `WarmAfterFirstFrame` läuft nur
     mit gültiger Taste oder `--scan-probe`; Taste setzen wärmt sofort; ein Scan ohne Warmup startet es selbst
     (`StartWarmup` einmalig) – das K2-Warten allein endete am 15-s-Limit.
  3. **Kein Abschnitt „Scan“** (Martin): Hinweis unter der Scan-Zeile im Hotkeys-Abschnitt, „the key“ statt „F3“, **ohne
     MB-Zahl** (Hinweis aus der Freigabe: die Zahl altert mit Modell/ORT; sie steht in D108 und Wiki).
  4. `--keep-scans`: das Bild liegt im `ContractScanner`, nicht in `ScanIntake` → reist als `ScanAttempt.Picture` (PNG vor
     der Lesung); Name mit Sekunden; gespeichert wird der Versuch, auf dem das Ergebnis beruht, auch `NoContractInFrame`;
     Bildtest mit Fake-Lesedelegat statt echter Engine (Hinweis aus der Freigabe).
  5. CLI ohne Code (Usage nennt `scan <screenshot.png>` schon); `scan` gibt Rohtext und geparsten Vertrag aus, nicht das ROI.
  6. README: Known issues des Scans kurz + Link aufs Wiki (Martin); Wiki-Branch `v0.4.0` pusht Martin.
- **Befund (12.11.2026):**
  - Commits: Warmup nur mit Taste (`354459e`), `--keep-scans` (`e930174`), Settings-Hinweis (`71a0c32`, von Martin im
    Fenster abgenommen), D108/Doku.
  - **Gate 1 – Speicher** (Release, ohne SC, `--data-dir` im Scratchpad, Hologramme aus, 30 s nach Start, 3 Läufe, Median):

    | | Working Set | Private Bytes | Log |
    |---|---:|---:|---|
    | mit F3 | **217 MB** (216–233) | 181 MB | „OCR ready“ 3/3 |
    | ohne Taste | **128 MB** (3× 128) | 83 MB | „not loaded at start“ 3/3 |

    Erwartung aus dem Startprompt (≈ 270 / ≈ 160) lag höher, der Abstand stimmt (≈ 90 MB vor dem ersten Scan). Die ≈ 400 MB
    aus T10.1b kommen erst mit Scans (T10.3: 770 MB nach 40 Scans) und werden ohne Taste nie erreicht.
  - **Tests** (sauberer Worktree): main Core 1 770 · Import 280 · App 1 793 · Ocr 34 → Core 1 770 · Import 280 · App
    **1 817** · Ocr 34 grün. Nachfolger: `A_scan_before_the_warm_up_started_waits_for_it` →
    `A_scan_without_a_warm_up_starts_it_and_waits_for_it`. C1: 3 neue Verhaltenstests vorher rot gegen Stub. C2 (Stubs
    scheitern an ungelesenen Parametern unter `TreatWarningsAsErrors`): 4 Mutationen, je rot – keine Kodierung (2), kein
    Rollieren (2), Bild des ersten Versuchs bei Zweitlesung (1), Schalter nicht geparst (2). Build 0 Warnungen,
    `dotnet format` Exit 0.
  - **Known issues** (Liste für README und Wiki, Beleg je Zeile):
    1. Scan-Bereich nur bei 3440×1440 und 1920×1080 geprüft; 32:9, 16:10, 4K ungeprüft → `--keep-scans` (PNG + `scans.log`)
       oder `CargoPlan.Cli scan <Fenster-Screenshot>` (D99 Gegenargument, S10.0, T10.1a).
    2. Ziele über eine mobiGlas-Seite hinaus: nur die sichtbaren werden gelesen; die Legende zeigt „⚠ n of m destinations“,
       sobald die Liste es verrät (`ShipViewBuilder`, D101).
    3. F3 bei laufendem ContractTracker belegt → Notice, Taste ändern oder CT schließen (D104, T10.3 Gate 3).
    4. Zwei inhaltsgleiche Verträge (Route und Reward) haben einen Schlüssel: der zweite Scan aktualisiert den ersten → im
       Editor anlegen (D101 Gegenargument).
    5. Eigene Fenster über dem Vertrag blinken beim Scan kurz (D100, B12).
    6. Speicher: 770–890 MB Working Set nach vielen Scans (T10.1b, T10.3); ohne Scan-Taste lädt die OCR nicht
       (128 statt 217 MB nach dem Start, D108).
  - **Doku-Branches:** README „Known issues“ (Scan kurz + Link) auf `docs/v0.4.0-user-docs` (`a32feb3`); Wiki `v0.4.0`
    (`a17098e`): „Scanning contracts“ (Taste löschen = aus, `--keep-scans`, `Cli scan`), „Troubleshooting“ (Known issues);
    Wiki-Repo wieder auf `master`, den Branch pusht Martin. **Gate 3:** Probe-Merge des Doku-Branches gegen Feature-Branch
    und `main` im Scratch, je 0 Konflikte, verworfen.
  - **Gate 2 über dem Spiel** (Martin, 12.11., Release, 3440×1440, `--data-dir` je Lauf):

    | Lauf | Start | Scan | Ergebnis |
    |---|---|---|---|
    | F3 (Default) | „OCR ready“ 1,2 s nach dem Prozess, 218 MB | F3 → Text 1 467 ms | `Added #1` |
    | `scanContracts: null` | „not loaded at start“ | Editor-Button: „warming up“, Laden 589 ms (`waitMs=613`), → Text 2 198 ms | `Added #1` |
    | `--keep-scans` | „OCR ready“ | F3 → Text 1 475 ms | `Added #1`, `picture=20261112-101440-offer-added-1.png` |

    Das PNG zeigt das ROI (OFFERS, ohne fremde Fenster), Name = lokale Zeit, Ansicht, Ergebnis; **2,6 MB** je Bild
    (≈ 50 MB für 20). Kodierzeit nicht messbar (1 475 vs. 1 467 ms, je n = 1). `NoContractInFrame` mit Bild nur per Test.
    Erster Start von Lauf 3 ohne Tastendruck im Log (kein Scan versucht), zweiter Start gleich konfiguriert ok.
  - Abgenommen (Martin, 12.11.2026). CI nach dem Push von `main`.

### [x] B14 Plan- und Settings-Notice überleben das Speichern (Branch `fix/b14-plan-notice`, Addenda D105, D106)

- **Anlass (T10.6, R4-Upgrade-Probe am ZIP, 13.11.):** Ein v0.3.1-Plan mit CT-Verträgen lädt richtig (`#1–#4`, Kisten
  und Haken bleiben), aber „4 contracts from ContractTracker are scan missions #1–#4 now.“ steht nur im Log: 500 ms nach
  dem Start schreibt `PlanSaver` den konvertierten Plan zurück und setzt die Plan-Zeile auf `_keptNotice` (ohne
  beiseitegelegte Datei `null`). B13 baute das Löschen, T10.4b das Setzen, kein gemeinsamer Test; der Test-Helfer
  `PlanSaverTests.Start()` setzte nur `Problem`, nicht `Notice`.
- **Fix (Martins Muster aus B13):** eine haftende Plan-Notice im Saver. `_keptNotice` → `_notice`, neuer Parameter
  `notice` (`PlanLoad.Notice` des Starts, `App.ComposeStartPlan`); nach jedem erfolgreichen Speichern steht `_notice`
  wieder in der Zeile: Fehler weg, Hinweise bleiben. `KeepUnreadable` verbindet statt zu verdrängen:
  `kept as … · n contracts …` (`--plan` mit CT-Datei über unlesbarer plan.json).
- **Präzisierung in der Planphase, ratifiziert durch Freigabe:** beide Hinweise mit ` · ` in einer Zeile statt „kept as
  gewinnt“ (Claude.ai); Lehre als CLAUDE.md A13 und Zeile unter „Lehren“ (Martin); Settings-Zeile (Fund des Rück-Sweeps)
  im selben Branch vor dem Merge (Martin), gleiches Muster: `SettingsSession` hält den Migrationshinweis (`_hint`,
  `WriteBack(hint, problem)`), jedes erfolgreiche Speichern zeigt Korrekturen (D34) und Hinweis (D105-Addendum).

**Befund:**
- Tests vorab, rot an der Assertion: `A_plan_from_before_v0_4_0_keeps_its_notice_after_the_first_save` (Notices leer),
  `A_failed_save_shows_its_problem_and_the_next_save_brings_the_notice_back` (zweiter Teil: leer statt Hinweis),
  `A_plan_file_with_captures_over_an_unreadable_plan_json_keeps_both_notices` (nur „kept as …“). Nach dem Fix
  `PlanSaverTests` + `PlanStoreTests` 36/36.
- `dotnet build` 0 Warnungen; `dotnet test` Core 1770, App 1820 (+3), Import 280, Ocr 34 (lokal Core +2 und App +1 durch
  drei ignorierte Scratch-Dateien).
- Rück-Sweep (A2), Schreiber je `NoticeSource` in mehreren Dateien: **Move** (ScanRunner, Editor, OverlayViewModel) –
  „letzte Aktion gewinnt“, gewollt; **Ship** (Start, Schiffswechsel) – der Wechsel macht den Start-Hinweis hinfällig,
  gewollt; **Settings** (Start, `SettingsSession`) – **derselbe Fehlertyp**: nach jedem Settings-Speichern setzt
  `SettingsSession.Save` die Zeile auf `_corrections`, und Speichern löst schon ein Drehen oder Zoomen der Ansicht aus
  (`ChangeCameras`); der Migrationshinweis „hotkey … for the ContractTracker sync was removed“ verschwindet also mit der
  ersten Kameraänderung (bis dahin sichtbar, Probe b2 nach 10 s), ebenso mit dem ersten Verschieben des Overlays
  (`ChangeBounds` speichert sofort). Martin: vor dem Merge beheben (zweiter Commit).
- Settings-Teil, Tests vorab rot an der Assertion: `The_migration_hint_stays_after_a_camera_change_is_saved` (leer),
  `A_failed_save_shows_its_problem_and_the_next_save_brings_the_migration_hint_back` (zweiter Teil leer),
  `A_look_correction_and_the_migration_hint_stand_together` (nur die Korrektur). Nach dem Fix `SettingsSessionTests` +
  `PlanSaverTests` 58/58; `dotnet build` 0 Warnungen, `dotnet test` Core 1770, App 1823 (+6 mit dem Plan-Teil), Import
  280, Ocr 34 (ohne die drei ignorierten Scratch-Dateien).
- [x] Upgrade-Probe b2 am Release-Build (`--data-dir`, v0.3.1-Settings + Schema-9-Plan auf den heutigen Racks, Screenshot
  nach 10 s): Amber-Zeile „settings.json: hotkey Ctrl+Alt+O … removed …“ und „4 contracts from ContractTracker are scan
  missions #1–#4 now.“; zweiter Start ohne beide, Log mit der Konvertierungszeile einmal
- [x] Probe Settings-Teil am Release-Build (13.11., Martin nicht im Spiel): Overlay nach 8 s per `SetWindowPos`
  verschoben, `settings.json` mit den neuen Bounds gespeichert; danach beide Hinweise weiter in der Amber-Zeile, zweiter
  Start an der neuen Stelle ohne Hinweise
- [x] Abnahme Martin (13.11.2026), `--no-ff` nach `main`, dann `main` → `release/v0.4.0`

### [x] T10.6 Release v0.4.0

**Abgehakt in K12 (19.11.2026, R1):** umgesetzt als Abschnitt „Release v0.4.0“ (R0–R5), Tag `v0.4.0` veröffentlicht
13.11.2026.

- Branch `release/v0.4.0`, Textabgleich.
- Release-Notes, Freigabe durch Claude.ai:
  - „ContractTracker is no longer needed – press F3 on an open contract“
  - Migrationshinweis settings 3 / plan 10, kein Downgrade
  - Speicher: mit OCR ≈ 824 MB Working Set statt ≈ 400 MB (T10.1b, Gate 2), ZIP ≈ 86 statt 62 MB (D99)
- Hero-GIF neu (Scan am Terminal; der D85-Auslöser ist erfüllt).
- Tag mit Nachmessung des ZIPs, Wiki, Nexus, Ankündigungen.
- **Aus T10.4a:** Branch `docs/v0.4.0-user-docs` (README, NOTICE-Zeile, Nexus-Beschreibung und -Checkliste, `bug.yml`) und
  den Wiki-Branch `v0.4.0` (Seite „Scanning contracts“ statt „ContractTracker integration“) mergen; Release-Notes:
  „settings.json 3: hotkey for ContractTracker sync removed; going back to v0.3.x needs `settings.json.bak`“, ZIP −1,2 MB
  (SQLite weg), Mission-Dateien Schema 1/2 werden abgelehnt.
- **Aus B13 (D106):** Release-Notes: „Going back to v0.3.x: v0.3.x overwrites a schema-10 `plan.json` on the first change
  – copy it first.“ Der Schutz vor dem Überschreiben gilt erst ab v0.4.0 (D106, Downgrade).
- **Aus T10.4b (D107):** Release-Notes: „Contracts from ContractTracker in your plan become scan missions #1, #2 … when
  v0.4.0 loads it; their crates and ticks stay.“
- **Aus T10.5 (D108):** Release-Notes: „Clear the scan key in the settings to turn scanning off; the OCR then doesn't load
  at start (about 90 MB less right after the start, and none of the growth that comes with scans).“ Zahlen: 217 vs 128 MB
  Working Set 30 s nach dem Start (Gate 1). `--keep-scans` für Bug-Reports (die letzten 20 Aufnahmen in `scans\`,
  ≈ 2,6 MB je Bild bei 3440×1440, ≈ 50 MB zusammen).
  README und Wiki: Known issues des Scans (Liste in T10.5) mit den Doku-Branches mergen.

