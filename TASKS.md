# TASKS – SC-CargoPlan

Regeln: **ein Task pro Session**, Reihenfolge einhalten. Jeder Task hat eine „Warum“-Zeile und prüfbare Akzeptanzkriterien. Erledigt → `[x]`.
Abgeschlossenes wandert mit dem Schließen ins Archiv: die Abschluss-Session verschiebt ihren Abschnitt (Phase, Release oder Session) wortgleich nach `docs/tasks-archive/` und setzt eine Index-Zeile unter „Geschichte (Archiv)“ (K11).

Session-Start-Prompt für Claude Code:
> Lies CLAUDE.md und TASKS.md; die Archivdatei einer Phase nur, wenn der Task darauf verweist. Wir machen Task **Tx.y**. Fass den Task zusammen, dann los.

Schiffsdaten: die Pipeline (Racks, Umrisse, Holos, Data-Release `ships-*`) lebt in [`sc-cargoplan-shipdata`](https://github.com/MRiediger/sc-cargoplan-shipdata) (D111); deren `TASKS.md` ist der Einstieg für Pipeline-Tasks. Die App-Seite ist Phase 12 ([Archiv](docs/tasks-archive/phase-12-ship-data.md)).

---

## Geschichte (Archiv)

Abgeschlossene Phasen, Releases und Sessions, wortgleich verschoben (K11). Zeitraum aus Überschriften und Commits; D = D-Nummern der Überschrift und die, die in TASKS.md in diesem Abschnitt zuerst vorkommen.

| Abschnitt | Zeitraum | D | Archiv |
|---|---|---|---|
| Phase 0 – Fundament | 16.10.2026 | D1–D4 | [phase-00-fundament.md](docs/tasks-archive/phase-00-fundament.md) |
| Phase 1 – Datenmodell (Core) | 16.–17.10.2026 | D7, D9 | [phase-01-datenmodell.md](docs/tasks-archive/phase-01-datenmodell.md) |
| Phase 2 – Verteilalgorithmus | 17.–18.10.2026 | D6, D8, D10 | [phase-02-verteilalgorithmus.md](docs/tasks-archive/phase-02-verteilalgorithmus.md) |
| Phase 3 – Overlay (WPF) | 18.–19.10.2026 | D11–D27 | [phase-03-overlay.md](docs/tasks-archive/phase-03-overlay.md) |
| Phase 4 – Settings & Konfiguration | 19.–20.10.2026 | D28–D36 | [phase-04-settings.md](docs/tasks-archive/phase-04-settings.md) |
| Phase 5 – Import aus ContractTracker V2 | 20.–21.10.2026 | D37–D42, D44 | [phase-05-import-contracttracker.md](docs/tasks-archive/phase-05-import-contracttracker.md) |
| Phase 6 – Release | 21.–22.10.2026 | D45–D52, D87 | [phase-06-release.md](docs/tasks-archive/phase-06-release.md) |
| Phase 7 – Schiffe vor Nexus | 22.–27.10.2026 | D53–D64, D66, D68–D69, D83 | [phase-07-schiffe.md](docs/tasks-archive/phase-07-schiffe.md) |
| Bugfix B1–B6 (Planregeln) | 24.–25.10.2026 | D5, D65 | [bugfix-b1-b6-plan-rules.md](docs/tasks-archive/bugfix-b1-b6-plan-rules.md) |
| Backlog-Session K1–K7 | 25.–26.10.2026 | D67, D70–D80 | [backlog-session-k1-k7.md](docs/tasks-archive/backlog-session-k1-k7.md) |
| Bugfix Sync-Gone (S1) | 29.10.2026 | D43, D81–D82 | [bugfix-sync-gone.md](docs/tasks-archive/bugfix-sync-gone.md) |
| Bugfix Sync-Net (N1) | 29.10.2026 | D82 | [bugfix-sync-net.md](docs/tasks-archive/bugfix-sync-net.md) |
| Bugfix Holo-Pump (P1) | 30.10.2026 | D76 | [bugfix-holo-pump.md](docs/tasks-archive/bugfix-holo-pump.md) |
| Phase 8 – 3D-Schiffsansicht | 30.–31.10.2026 | D83–D84 | [phase-08-3d-view.md](docs/tasks-archive/phase-08-3d-view.md) |
| Phase 9 – Release | 31.10.–02.11.2026 | D85–D86, D94 | [phase-09-release.md](docs/tasks-archive/phase-09-release.md) |
| Bugfix B7 SDK-Pinnung | 02.11.2026 | D86 | [bugfix-b7-sdk-pin.md](docs/tasks-archive/bugfix-b7-sdk-pin.md) |
| K9 Sektionen pro Ziel | 05.11.2026 | D88–D89 | [k9-sections.md](docs/tasks-archive/k9-sections.md) |
| Release v0.2.0 | 05.11.2026 | – | [release-v0.2.0.md](docs/tasks-archive/release-v0.2.0.md) |
| v0.3.0 – Feedback-Release | 06.–08.11.2026 | D90–D93, D95–D96 | [feedback-v0.3.0.md](docs/tasks-archive/feedback-v0.3.0.md) |
| Release v0.3.0 | 08.11.2026 | – | [release-v0.3.0.md](docs/tasks-archive/release-v0.3.0.md) |
| Bugfix B10 – Hotkeys im Spiel | 09.11.2026 | D97 | [bugfix-b10-hotkeys.md](docs/tasks-archive/bugfix-b10-hotkeys.md) |
| Release v0.3.1 | 09.11.2026 | – | [release-v0.3.1.md](docs/tasks-archive/release-v0.3.1.md) |
| K10 Repo-Hygiene | 09.11.2026 | – | [k10-repo-hygiene.md](docs/tasks-archive/k10-repo-hygiene.md) |
| Bugfix B11 App-Tests hängen | 09.11.2026 | D98–D99 | [bugfix-b11-app-tests-hang.md](docs/tasks-archive/bugfix-b11-app-tests-hang.md) |
| Phase 10 – OCR (v0.4.0) | 09.–13.11.2026 | D100–D108 | [phase-10-ocr.md](docs/tasks-archive/phase-10-ocr.md) |
| Release v0.4.0 | 12.–19.11.2026 | – | [release-v0.4.0.md](docs/tasks-archive/release-v0.4.0.md) |
| Phase 11 – nach dem Launch (v0.5.0) | 13.–15.11.2026 | D109 | [phase-11-after-launch.md](docs/tasks-archive/phase-11-after-launch.md) |
| Release v0.5.1 | 16.11.2026 | D110 | [release-v0.5.1.md](docs/tasks-archive/release-v0.5.1.md) |
| Phase 12 – Ship data channel | 16.–19.11.2026 | D111–D114 | [phase-12-ship-data.md](docs/tasks-archive/phase-12-ship-data.md) |
| K12 Doku-Abschluss, Backlog II, F11 | 19.11.2026 | D115 | [k12-docs-backlog-ii.md](docs/tasks-archive/k12-docs-backlog-ii.md) |
| Backlog II (K11, B16, B17, F10a, F10b) | 19.–20.11.2026 | D116–D117 | [backlog-ii.md](docs/tasks-archive/backlog-ii.md) |
| Release v0.7.0 | 20.11.2026 | – | [release-v0.7.0.md](docs/tasks-archive/release-v0.7.0.md) |
| Phase 13 – Cargo-Routen (T13.0–T13.7, v0.8.0) | 21.–26.11.2026 | D118–D130 | [phase-13-cargo-routes.md](docs/tasks-archive/phase-13-cargo-routes.md) |
| Bugfix B18 – Leiste per Stationsname | 26.11.2026 | D126 | [bugfix-b18-route-at.md](docs/tasks-archive/bugfix-b18-route-at.md) |
| Release v0.8.0 | 26.11.2026 | – | [release-v0.8.0.md](docs/tasks-archive/release-v0.8.0.md) |

---

## Lehren

Regeln aus Fehlern, die durchgerutscht sind. Gelten für jeden Task.

- **Vor jedem Push `gh run list -L 3`.** Rote Läufe erst klären, dann pushen. Anlass T7.1d: Die CI war seit T7.0 rot (1-s-Zeit-Assert in `OrderSearchLimitTests`, 1.099 / 1.297 ms auf den Runnern), unbemerkt über mehrere Pushes.
- **Tests vor der Implementierung heißt: erst rot sehen.** Neue Tests vor dem Code laufen lassen und das Scheitern (Compile- oder Assert-Fehler) festhalten, dann implementieren. Anlass T7.2: Die Core-Tests (Replan, PlanFile, Katalog) wurden vor dem Code geschrieben, aber erst danach ausgeführt; in der App lief es richtig.
- **Neues Darstellungselement → Renderarchitektur neu entscheiden**, nicht das Element in die bestehende einpassen. Zwei Renderer für ein Bild sind ein Provisorium, kein Entwurf. Anlass Phase 8: das Holo kam in T7.4c als `Viewport3D`-Kulisse hinter den 2D-Painter; zwei Kameras mussten per Code gleich gehalten und gegeneinander gemessen werden (Holo-Pump Teil 2–5), und die Abhilfe gegen das Pumpen, die Perspektive (S8.0), hätte beide Zeichner getroffen. Umbau in Phase 8 (D83). Ergebnis (T8.7): eine Szene, eine Kamera, ein Hit-Test auf derselben `CameraMath`; im Spiel kein Pumpen auf fünf Schiffen, ≈ 960 Zeilen Painter-Code gelöscht.
- **Laufzeit messen wie Tests laufen lassen: fest geregelt, vorher und nachher.** Drei Läufe je Ziel, gewertet der Median; gegen das leere Fenster desselben Builds; der Anteil später Frames statt p95, weil p95 auf den Bildtakt quantisiert ist. Anlass Phase 8: einzelne Läufe streuten mehr als die Ziele sich unterschieden (`empty` 2,5–14,2 % spät), und ein p95 sprang zwischen 16,9 und 33,1 ms, ohne dass sich etwas geändert hatte. **Ab jetzt immer über dem Spiel messen** (`--over-game`, SC vorn): die Basislinie ohne SC misst den Desktop-Compositor (T8.7: `empty` 13,1 % spät ohne SC, 0,1 % mit SC). Gate-Regel für künftige Phasen: gleiches Ziel vorher/nachher über dem Spiel, nicht relativ zu `empty`.
- **Bestehende Tests sind beim Umbau das Orakel, und jeder gestrichene Test braucht einen benannten Nachfolger.** Nachfolger zuerst schreiben, grün gegen den alten Code, dann löschen; jeden einmal per Mutation rot sehen. Anlass T8.6: die Nachfolger-Tabelle fand drei Pfade, die nur der alte Code getestet hatte (Bow-Label in 3D, Fit-Regeln, Teile der Navigation).
- **Darstellung am gerenderten Bild prüfen, nicht an der Geometrie.** Anlass T8.2–T8.5: alle Kantenstreifen der Szene waren falsch gewickelt und unsichtbar; drei Tasks lang grün, weil die Tests nur Positionen prüften. Gefunden erst mit `RenderTargetBitmap` (`ViewPictures`); in T8.6 bewies ein Byte-Vergleich der Bilder vorher/nachher, dass der Rückbau nichts änderte.
- **Mechanik-Tests laden Fixtures aus dem Test-Ordner, nie aus `ships/`** (CLAUDE.md A10). Datentests prüfen `ships/` selbst. Anlass Session 4a (D92): Die Racks aus den Spieldateien brachen 70 Tests (Treffertest, Drag, Szene, Kamera, Rack-Geometrie), ohne dass die App einen Fehler hatte, weil sie feste Zellen und Pixel der ausgelieferten Hull A, Freelancer und Caterpillar prüften. Seitdem: `tests/CargoPlan.Core.Tests/FrozenShips`, `TestPlans.HullA()`, `FrozenShips.Load`.
- **Mess- und Gate-Befehle selbst einmal laufen lassen, bevor Martin sie bekommt.** Anlass T8.5 (Messbefehl) und T8.7 (`--over-game`): erst der eigene Lauf zeigt, ob das Log das Richtige zählt; die 343 Vorschau-Wechsel des Sweeps hätten einen von SC gefangenen Cursor nicht verraten, daher „path keys“.
- **Eine Notice-Zeile, mehrere Schreiber: alle greppen, Zusammenspiel testen** (CLAUDE.md A13). Anlass B14: B13 baute „nach dem Speichern ist ein Ladeproblem gelöst, Zeile leeren“, T10.4b setzte in dieselbe Zeile den Konvertierungshinweis; jeder Task war für sich getestet, der Hinweis stand im fertigen ZIP 500 ms. Gefunden erst von der Upgrade-Probe in T10.6; ein Grep nach `SetNotice(NoticeSource.Plan` hätte beide Schreiber gezeigt.
- **Fremd-Framework zuerst als Spike mit Gates am echten Layout.** Anlass Phase 11: S11.0 fand vor dem ersten Prod-Commit CS0017 (zwei `Main`), den Setup-Hang mit App-Argumenten (velopack#1039), das Löschverhalten je packId und das 10er-Fenster von `GithubSource`; jedes davon hätte sonst ein Release getroffen.
- **Bei fremden CLIs heißt „leer“ oft „nicht gesetzt“.** Anlass T11.3: `vpk --exclude ""` ließ die PDBs trotzdem weg (Default `.*\.pdb`). Der Wert, der nie matcht, ist `(?!)`, und der Wächter ist ein Test, der einmal provoziert rot war (PDB-Zählung in `check-portable.ps1`).
- **UAC nur im Standardkonto prüfen.** Anlass S11.0 G5: im Admin-Konto mit `ConsentPromptBehaviorAdmin = 0` kam keine Abfrage; erst `cargotest` als Standardnutzer zeigte Abfrage, Ablehnung und `cancelled` (T11.2 Gate 5b).
- **A1-Grep nach dem Zweck, nicht nach dem Schalter.** Anlass `e7d06bb`: `--updates-prerelease` hob nur einen von zwei Pre-Release-Filtern auf (`GithubSource`, nicht `UpdateGate.Offer`); der Grep nach dem Schalternamen fand den zweiten nicht, einer nach „pre-release“ hätte ihn gefunden.
- **Ein Pack-Skript für lokal und CI, und das CI-Artefakt gegen das lokale vergleichen.** Anlass B7 (Runtime des Release-ZIPs nie geprüft) und T11.3: `build/publish.ps1` ist der einzige Ort für Publish und Pack, `ci.yml` und `release.yml` rufen es auf; der lokale Lauf auf dem Merge-Commit ist die Probe für den Tag. T11.5 hat die Lücke geschlossen: Runtime und Cli-Version des CI-ZIPs = lokales Pack mit demselben SDK. Jedes Release so prüfen.
- **Testzahlen aus einem sauberen Worktree.** Anlass T11.3/T11.4: drei gitignorte Scratch-Tests (2 Core, 1 App) zählten mit, gemeldet waren 1772/1969 statt 1770/1968. Zählen mit `git worktree add --detach <scratch> <commit>`, `dotnet test`, dann `git worktree remove`.
- **Abschnitt mit dem Schließen archivieren.** Die Abschluss-Session einer Phase, eines Releases oder einer Session verschiebt ihren Abschnitt wortgleich nach `docs/tasks-archive/` und setzt eine Index-Zeile unter „Geschichte (Archiv)“. Anlass K11: TASKS.md war auf 859 KB / 8 063 Zeilen gewachsen, und jede Session las sie ganz.
- **Schreiber und Leser einer Datei über jeden Zustand testen, den die Oberfläche erreicht.** Rundweg schreiben → lesen → schreiben für jeden Zustand, den die Bedienung herstellen kann, nicht nur für die gebauten Beispiele. Anlass B18: der Rundweg-Test von `routes.json` lief mit `at` innerhalb der Stationen, die Leiste erreichte mit Extras mehr Stopps; die App schrieb eine Datei, die sie nicht las, und erst der Doku-Abgleich in T13.8 fand es.

---

## Phase 14 – VR-Modus (ab 27.11.2026, letztes Feature vor v0.9.0)

### [ ] S14.0 VR-Spike (27.11.2026, kein Prod-Code, wartet auf Abnahme)

**Branches:** Spike-Code auf `spike/s14-vr` (Worktree `..\sc-cargoplan-s14`, vier Commits: `b005d24` Spike, `98f4caf`
Takt/Flags für G3, `e9879f2` Flacker-Schalter, `ebfbda5` Größenreihe; wird nie gemergt), dieser Befund auf
`spike/s14-vr-finding`. Laufordner `..\sc-cargoplan-s14-run` (ein Datenordner je Lauf, Skripte `vr.ps1`, `g3.ps1`,
`g6.ps1`, Belege in `evidence\`), außerhalb des Repos. Profil-Hashes vor und nach allen Läufen gleich.
**Frage:** Lässt sich das Overlay als SteamVR-Overlay in die Brille (Luma über VRto3D) spiegeln, ohne das Spiel, den
UI-Thread oder den Scan zu stören, und welcher Texturpfad trägt D131? **Pin:** `ValveSoftware/openvr` **v2.15.6**
(`openvr_api.dll` 837 272 B, Authenticode Valve Corp., `openvr_api.cs` 371 580 B, `LICENSE` BSD-3), SHA256 in
`evidence\openvr-v2.15.6-sha256.txt`. `openvr_api.cs` bleibt byte-gleich: 226 Nullable-Warnungen (CS86xx) schaltet
`.editorconfig` mit `generated_code = true` für genau diese Datei ab, kein `NoWarn`.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. K1: Yaw in Nutzersemantik, **positiv = rechts vom Blick** (OpenVR +Yaw = links um +Y); je Recenter roher HMD-Yaw und
   gesetzter Overlay-Yaw im Log.
2. K2: Speicher-Plateau als Teil von G3 (Zwangsmodus 66 ms, 2 min, Pfad a und b, Working Set alle 10 s).
3. K3: `VREvent_Quit` in G6 mit Beleg (`AcknowledgeQuit_Exiting` + `VR_Shutdown` auf dem VR-Thread, Verhalten bei
   ignoriertem Quit, erneutes `VR_Init` im selben Prozess).
4. E1: Settings-Reader-Toleranz nicht im Spike: D105-Addendum in T14.0, Umsetzung T14.3.
5. E2: Numpad ohne Modifier: Addendum D35/D104 im T14.3-Plan; der Spike liefert nur den VK bei NumLock an/aus.
6. E3: CargoPlan startet SteamVR nie; erst Probe, Overlay nur bei laufendem `vrserver`; Retry-Takt nur bei einer Probe
   < 1 ms ohne Prozessstart.
7. E4: eine G1-Probe ohne Brille, die scheitert, ist Umgebung, kein Befund; das Gate bleibt Martins.
8. E5: je Update die Wartezeit des VR-Threads in `PrintWindow` im Log.

**Befunde in der Durchführung:**
- `SetOverlayRaw` ist **asynchron**: ein Rücklesen direkt danach gibt `InvalidTexture`; das Bild steht erst nach
  `VREvent_ImageLoaded`, und das kommt auf der **Overlay-Queue** (`PollNextOverlayEvent`), nicht auf `PollNextEvent`.
  Rücklesen danach: byte-gleich mit dem Gesendeten.
- Hull C nimmt nur 32er: die Fixture für G2/G3 ist eine eigene Missionsdatei (8 Stopps × 32er, 2 560 SCU, 80 Kisten),
  `CARGOPLAN_SPIKE_VR_MISSIONS`.
- In Martins erstem Durchgang liefen **drei Spike-Instanzen gleichzeitig**; die späteren bekamen `CreateOverlay KeyInUse`
  und zeigten nichts. `vr.ps1` schließt seitdem eine laufende Spike-Instanz vor dem Start. Für T14.x: ein zweiter
  Prozess mit VR an muss das melden (Notice), nicht still nichts zeigen.
- **G1 zeigte Rot und Blau vertauscht**: `SetOverlayRaw` liest RGBA, `PrintWindow` liefert BGRA. Mit Tausch stimmen die
  Farben in der Brille (Martin, Hologramm und Kisten); `vr.ps1` tauscht seitdem per Default.
- **Flackern in der Brille**, sobald sich das Bild ändert (Maus über dem Fenster, Holo drehen, Buttons): die Aufnahme ist
  sauber (Lauf `r08`, Dauerrotation: 0 von 378 Aufnahmen leer, schwarz oder mit Helligkeitssprung), aber dasselbe
  Testbild 15× pro Sekunde neu hochgeladen flackert (Martin, `-Test -Force`). Ursache ist der Texturtausch von
  `SetOverlayRaw` selbst. **Abhilfe Double-Buffer:** zwei Overlays, Upload ins verborgene, `ShowOverlay` des neuen und
  `HideOverlay` des alten erst nach dessen `ImageLoaded` → kein Flackern, Farben stimmen (Martin, `-Key -Double`);
  376 Flips bei 376 Uploads, ein Update wartete auf das vorige (`skip.pending=1`).
- Der Hinweis **„No profile found for App SC-CargoPlan.exe“ mit Piepton** in der Brille steht nicht im SteamVR-Log und
  nennt die installierte App (der Spike heißt `CargoPlan.App.exe`); vermutlich VRto3Ds Profilsuche für die App im
  Vordergrund, kein EAC-Hinweis. Nicht belegt.
- Ausreißer von `PrintWindow` (511 ms, 1 555 ms je einmal) fallen auf UI-Latenz-Spitzen (620 ms, 2 372 ms) beim Start:
  `PrintWindow` wartet auf den UI-Thread (`WM_PRINT` im Zielprozess). Ein hängender UI-Thread hält damit den VR-Thread
  fest – und SteamVR beendet einen Prozess, der `VREvent_Quit` nicht binnen ≈ 5 s quittiert (G6).

**Gates:**

| Gate | Ergebnis | Beobachtung |
|---|---|---|
| G1 sichtbar in der Luma über SC | **bestanden** (Martin) | Testbild in beiden Augen; Ecken vertauscht (Rot ↔ Blau, s. o.), mit Tausch richtig; kein EAC-Hinweis |
| G2 Texturpfad | **b bestanden, a gerissen** | Tabelle unten; a rendert Viewport3D per `RenderTargetBitmap` in Software auf dem UI-Thread |
| G3 späte Frames ≤ vorher + 2 pp | **bestanden** über dem Spiel (5 Runden) | Tabellen unten; `vr@15` 11,5 % gegen `hull_c` 14,2 % |
| G3/K2 Speicher | **bestanden** | b: Working Set 312–338 MB, GDI konstant 43; a: 369–401 MB, GDI 51–53; Plateau < 60 s, kein monotones Wachstum |
| G4 Recenter Numpad 5 | **bestanden** (Martin) | NumLock an `VK_NUMPAD5` (0x65), aus `VK_CLEAR` (0x0C), beide recentern; Formel unten |
| G5 Alpha, Lesbarkeit | **bestanden** (Martin) mit Color-Key | Transparenz folgt dem Fensterhintergrund der Preferences (Variante `-Key`); Key stellt Alpha bei 91,9 % der Pixel her, 8,1 % (Kantenglättung) bleiben deckend, 0,8 ms |
| G6 SteamVR fehlt / endet / DLL fehlt | **bestanden** | Tabelle unten |
| G7 F3 mit laufendem Feed | **bestanden** (Martin) | zwei Scans `Added` (je zweite Lesung nach `doubt (NoReward)`, D103), Taste → Notice 2 793 / 2 970 ms; ein Feed-Takt während des Cloaks ausgelassen (`skip.cloak=1`), keine Kollision |
| G8 Packaging, Delta < 2 MB | **bestanden** | `check-portable.ps1` grün, 392 Dateien (390 + `openvr_api.dll`, `openvr\LICENSE.txt`); Delta 0.8.0 → 0.8.1 1 102 908 B (**1,10 MB**); ZIP 97 739 703 B (93,2 MiB, +0,5); Linux-Tarball unverändert 328 Dateien |

**G2** (Hull C, 994 × 773 px bei 110 %, Holo, 80 Kisten, Legende, Notice; je 10-s-Fenster, Median / p95):

| | Pfad a (`RenderTargetBitmap`) | Pfad b (`PrintWindow PW_RENDERFULLCONTENT`) |
|---|---|---|
| Aufnahme | 76 / 80 ms **auf dem UI-Thread** | 8,2 / 13 ms auf dem VR-Thread (Martins Läufe über dem Spiel 9,1–12,3 / 15,6–25,2 ms) |
| UI-Latenz (ohne Feed 0,1–0,2 / 0,4–0,7 ms) | 0,34 / 65–73 ms | 0,33 / 0,9–1,1 ms |
| erreichter Takt (Ziel 15 Hz) | ≈ 6,5 Hz | 12,8 Hz |
| Upload `SetOverlayRaw` | 0,9 ms | 0,85 ms |

- `PrintWindow` liefert das geschichtete Fenster **auf Schwarz vormultipliziert mit Alpha 255** (Hintergrund `0D0D0DFF`
  statt `0D0D0DA0`); ohne `PW_RENDERFULLCONTENT` (`PWFLAGS=0`) ein leeres Bild in 1,9 ms.
- Der Cursor kommt nicht mit: `GetCursorInfo` + `DrawIconEx` zeichnen ihn ein (median 0,01 ms, mit Bewegung 0,12 ms).

**G3** (`--measure-frames hull_c` gegen `vr` = `hull_c` mit Feed, abwechselnd, Anteil später Frames, Median der Runden):

| Lauf | `hull_c` | `vr@15` | `vr@5` |
|---|---|---|---|
| Desktop ohne SC, 3 Runden | 0,9 / 1,0 / 0,7 → **0,9 %** | 7,1 / 6,9 / 7,1 → **7,1 %** (+6,2) | 3,6 / 2,0 / 2,9 → **2,9 %** (+2,0) |
| über dem Spiel, 3 Runden (11:35) | 36,1 / 10,8 / 3,0 → 10,8 % | 29,7 / 10,3 / 13,0 → 13,0 % | 11,7 / 8,7 / 8,6 → 8,7 % |
| **über dem Spiel, 5 Runden (12:08, gewertet)** | 13,8 / 19,2 / 13,0 / 14,2 / 14,4 → **14,2 %** | 6,9 / 11,5 / 11,5 / 12,1 / 10,6 → **11,5 %** | 7,8 / 19,0 / 19,1 / 10,6 / 12,1 → **12,1 %** |

- Der erste Lauf über dem Spiel ist nicht verwertbar: die Basis streut 3–36 % (Runde 1 mit 88 fps, danach 60).
- Über dem Spiel kostet der Feed keinen Anteil später Frames, aber Takt: `vr` 53,5–56,4 fps gegen `hull_c` 57,0–61,4.
  Am Desktop kostet jede Aufnahme etwa einen halben späten Frame.
- G3 lief ohne Kanaltausch und ohne Double-Buffer. Der Tausch ist ein Durchgang über ≈ 3 MB auf dem VR-Thread (nicht
  gemessen), der Double-Buffer nimmt keine Aufnahme dazu.

**G4/K1 Platzierung:** `headYaw = atan2(m2, m10)` (Vorwärtsachse −Z der HMD-Pose in `TrackingUniverseSeated`),
`overlayYaw = headYaw − userYaw`; Transform `R = Ry(overlayYaw) · Rx(pitch)`, `t = Ry(overlayYaw) · (0, height,
−distance) + Kopfposition`. Beispiel Log: `headYaw(openvr)=12.7° userYaw=30.0° → overlayYaw(openvr)=-17.3°`,
Overlay bei (0,371; −0,014; −1,135). Recenter 0,04–0,24 ms.

**G6/K3** (`g6.ps1`, SteamVR ohne Brille):

| Fall | Ergebnis |
|---|---|
| SteamVR aus, Start | `Init_NoServerForBackgroundApp` (59–104 ms), Notice, App läuft |
| `VREvent_Quit` mit `AcknowledgeQuit_Exiting` + `VR_Shutdown` (`ack`) | Quit in 62 ms behandelt, SteamVR nach 2,9 s weg, App läuft |
| nur `VR_Shutdown` (`shutdown`) | wie `ack` |
| Quit ignoriert (`ignore`) | **SteamVR beendet CargoPlan nach 5,1 s** (Exit-Code 0, kein Dispose) |
| Toggle ohne SteamVR / nach SteamVR-Neustart | NoServer-Notice / verbindet im selben Prozess neu (Init 38 ms) |
| `openvr_api.dll` fehlt | `DllNotFoundException` gefangen, Notice, App läuft; die Meldung ist Windows-lokalisiert → T14.x braucht einen eigenen englischen Text |

**Offene Punkte:**
1. **Seated unter VRto3D:** vorhanden, aber beim Verbinden steht der Kopf auf y = 1,5 m (Standing y + 2,0, Raw y + 1,0);
   13–18 s nach jedem Connect kommt ein `SeatedZeroPoseReset` (+ `StandingZeroPoseReset`), danach Kopf (0, 0, 0), Yaw 0.
   Wer ihn auslöst (Luma-Bridge, Desktop+?), ist offen. Folge: Platzierung **relativ zur Kopfposition**, nicht zum Ursprung.
2. **Obergrenze `SetOverlayRaw`:** **Kantenlänge ≤ 4096 px**, die Datenmenge zählt nicht (4000 × 4000 = 61 MiB geht,
   4097 × 100 nicht). Darüber sofort `InvalidParameter` (0,1 ms), kein `ImageFailed`. Kosten des Aufrufs: 2,9 MiB
   1,3 ms, 7,9 MiB 2,9 ms, 31,6 MiB 11,8 ms, 64 MiB 69 ms; `ImageLoaded` nach 29–144 ms.
3. **Alpha bei `PrintWindow`:** nein, immer 255 auf Schwarz (G2); Color-Key aus dem Fensterhintergrund stellt es her (G5).
4. **CargoPlan vor SteamVR:** `VRApplication_Overlay` **startet SteamVR** (vrserver nach 1,1 s, Init 1 545 ms, Desktop+
   startet mit); `VRApplication_Background` startet nichts (`Init_NoServerForBackgroundApp`) und darf trotzdem Overlays
   anlegen (Init 38–77 ms). Proben: `IsRuntimeInstalled` 0,3–5,7 ms, `IsHmdPresent` 61–71 ms, `vrserver`-Prozess
   2,5–3,3 ms – keine unter 1 ms, also **kein Retry-Takt** (E3), nur Toggle.

**Empfehlung für D131: Pfad b mit Double-Buffer.** `PrintWindow PW_RENDERFULLCONTENT` auf einem eigenen VR-Thread,
Rot/Blau tauschen, Color-Key aus dem Fensterhintergrund der Preferences, Upload per `SetOverlayRaw` abwechselnd in zwei
Overlays (sichtbar erst nach `ImageLoaded`), Takt ≤ 15 Hz und nur bei Änderung. Pfad a scheidet aus (76 ms UI-Thread),
Pfad c (WGC bzw. D3D11-Textur per `SetOverlayTexture`) ist nicht nötig. **Gegenargument:** jede Aufnahme kostet am
Desktop einen halben späten Frame und über dem Spiel ≈ 10 % Takt, und `SetOverlayRaw` kopiert über die CPU (31,6 MiB
bei 4K: 11,8 ms je Upload, Grenze 4096 px); eine D3D11-Textur spart Tausch, Kopie und den zweiten Overlay, kostet aber
ein D3D-Gerät, Interop mit WPF und deutlich mehr Code. Kippt, sobald ein Fenster über 4096 px oder der Takt über dem
Spiel stört.

**Vorgaben für T14.x (aus dem Spike, nicht entschieden):**
- Nur `VRApplication_Background`; Quit-Handler Pflicht, auf einem Thread, den ein hängender UI-Thread nicht festhält
  (`PrintWindow` wartet auf ihn).
- Aufnahme nur bei Änderung: der Spike nimmt 12,8×/s auf und verwirft danach 25–100 % je 10 s als unverändert (Martins Läufe); ein Dirty-Signal
  (nicht `CompositionTarget.Rendering`, das bei einem Handler jeden Frame feuert) spart die Kosten im Ruhezustand.
- Fenster über 4096 px verkleinern oder ablehnen, mit Notice.
- Zweite Instanz mit VR: `KeyInUse` melden.
- `.gitattributes`: `src/CargoPlan.App/Vr/OpenVR/* -text`, sonst ändert `* text=auto` Valves Zeilenenden.
- Notice-Texte englisch und eigen (DLL fehlt, SteamVR läuft nicht).
- Eingabe nur am Desktop-Fenster (ESC-Menü in SC, Maus am gespiegelten Fenster); der Cursor wird ins Bild gezeichnet.
- Breite, Abstand und Winkel kommen in die Einstellungen; wie, wird im Fable-Chat festgelegt (Martin, 27.11.). Die
  Spike-Werte: Breite 0,5 m, Abstand 1,2 m, Yaw 30° rechts, Pitch 0, Höhe 0.

---

## Backlog (nur bei Bedarf)

Kein Plan, sondern Reaktion: wird erst zum Task, wenn der Anlass eintritt.

- Hauling-Daten und `routes.json` erst nach dem ersten Frame laden (T13.8, Halt 3): v0.8.0 zeigt den ersten Frame im
  Median 79 ms später als v0.7.0 (785 zu 706 ms, abwechselnd je 3 Starts aus den veröffentlichten ZIPs; Hauling allein
  39 ms), von Martin akzeptiert. Gegen: ohne Umbau wird jeder Start mit jeder Schicht ein Stück langsamer; mit Umbau
  muss der Plan-Reiter einen Ladezustand zeigen. **Auslöser:** Nutzer-Report über einen langsamen Start oder ein erster
  Frame über 1 s in einer Release-Prüfung.
- Settings-Reader toleriert unbekannte Top-Level-Abschnitte und schreibt sie unverändert zurück (T13.8, Halt 1, E2):
  heute lehnt jede Version eine settings.json mit einem Abschnitt einer neueren ab (`UnmappedMemberHandling.Disallow`)
  und startet mit Defaults – v0.4.0 mit `updates`, v0.5.x mit `shipData`, v0.7.0 mit `routes`; ohne Änderung wiederholt
  sich der Downgrade-Befund bei jeder Version. Gegen: die Strenge fängt Tippfehler (settings-format „A misspelt key
  makes the whole file unreadable“) – unbekannte Schlüssel *in* bekannten Abschnitten bleiben deshalb ein Fehler, nur
  ganze unbekannte Abschnitte (Objekte auf oberster Ebene) werden gehalten; und es hilft erst beim Rückweg *auf* eine
  Version, die es schon kann. D-Eintrag (D105-Addendum). **Auslöser:** der nächste additive Abschnitt in settings.json.
- **F12 VR-Overlay (Phase 14):** CargoPlan in VR lesbar, kein Plan, kein Entwurf vor dem Start. Auslöser: v0.8.0
  veröffentlicht und Martin startet Phase 14.
- Red-Wind-Zwillinge live prüfen (T13.3, offen aus T13.4): Faktions-Zwillinge HH/CFP teilen heute eine mehrdeutige
  Beobachtung, der Fraktionsfilter zeigt die eigene Seite (D122, U5). Auslöser: die erste Pyro-Session mit Scans.
- Zuordnung per Fraktion, wenn die Rewards der Zwillinge abweichen (D122): ein Scan, dessen Reward nur zu einem Zwilling
  passt, ginge eindeutig an diesen statt `Ambiguous`. Auslöser: ein Pyro-Scan, dessen Reward einen Zwilling ausschließt.
- Tooltip-Zeile anklickbar: in Matrix und Karte ein bestimmtes Template einer Zelle wählen statt der ganzen Zelle.
  Auslöser: Nutzerwunsch, ein einzelnes Template in den Plan zu nehmen.
- Pyro-Kisten gegen `legs[].containerScu` der Red-Wind-Templates prüfen (größte Kiste, `hauling-data-format.md`; bisher
  nicht gegen Pyro-Scans geprüft). Auslöser: ein Pyro-Accepted-Scan, dessen Kisten vom Template abweichen.
- Startlauf nach der Wiederherstellung (E1 T13.4: heute kein Fehler, der Start rechnet einmal mit dem gespeicherten
  Schiff). Auslöser: Startzeit-Budget überschritten oder zweiter `loops`-Lauf innerhalb 10 s im Log.
- Testhost-Hänger (T13.3: ein voller `dotnet test` blieb einmal 10 min im App-Testhost stehen, mit
  `--blame-hang-timeout` nicht reproduziert). Auslöser: zweites Auftreten, dann Dump mit `--blame-hang-timeout`.
- Route überall violett (Variante zum Gate-Befund T13.7, D128-Addendum: heute nur auf der Karte in #E040FB). Auslöser:
  Martin oder ein Nutzer will die aktive Route auch in Matrix und Loops markiert sehen.

- Variante B der gemessenen Loop-Zeiten (T13.6c, D127): ein Korrekturfaktor (gemessen ÷ Modell) über alle Läufe des
  Schiffs, angewandt auf jede Loop, auch ungeflogene; heute rechnet nur eine Loop mit ≥ 3 eigenen Läufen mit ihrem
  Median (Variante A). **Auslöser:** Nutzerwunsch oder ≥ 10 Läufe in einer routes.json.

- Standorterkennung ersetzt den Hub-Button des Rezepts (T13.3b, D123): der Hub, an dem der Spieler steht, statt
  `routes.hub`. Auslöser: Game.log-Location wird gelesen.

- QT-Zeit aus Distanz und der Quantum-Geschwindigkeit des Schiffs statt einer Zeit je langem Sprung (T13.4, D124:
  heute `routes.times.jump` je Sprung > 10 Gm). Auslöser: Nutzer-Report, dass das Ranking lange und kurze Sprünge
  falsch gewichtet, oder Schiffsdaten mit QT-Werten.

- Kipprad (`WM_MOUSEHWHEEL`) scrollt die Hafen-Matrix seitwärts (K der Zwischenabnahme T13.3a: heute nur Shift+Rad).
  WPF hat dafür kein Event, es braucht einen `HwndSource`-Hook; derselbe Hook gälte für jede waagerecht scrollbare Fläche
  des Overlays (schmale Bildlaufleisten im Overlay-Stil, T13.2). Auslöser: Nutzer-Report mit Kipprad-Maus.

- Proben der Zuordnung nach Seite sparen (F10b, D117): `RackAssignment.AssignBySide` prüft jedes Ziel je Seite mit
  einer Machbarkeitssuche; nimmt ein leeres Rack der Seite das Ziel allein, gibt es sicher eine Zuordnung und die Probe
  kann entfallen (Hull B: fast alle 8 Proben). Auslöser: spürbare Planzeit mit Seiten auf einem Schiff mit vielen Racks.

- Kapazitäts-Ausweichweg erst nach den anderen Zielen eines Scans legen (B17, Lauf 135: B 9×1 drängt Cs 4er aus dem
  Block). Heute legt `StopByStop` die Ziele eines Scans größte zuerst; ein Ziel „placed outside sections (capacity)“
  belegt dabei Zellen in fremden Reihen, die für die späteren Ziele feste Hindernisse sind. B17 nimmt solche Racks von
  der Blockregel aus, `SectionBlockTests.A_destination_outside_sections_can_leave_a_gap_in_another_section` hält den
  Fall fest. Auslöser: Hauler-Report über ein Loch in einem Rack mit Kapazitätshinweis.

- winget-Manifest (`winget install SC-CargoPlan`), früher T11.2. Auslöser: Ein Nutzer fragt danach (D109).
- Setup.exe und/oder Code-Signing (D109: heute nur Portable, unsigniert). Auslöser: Nutzer-Reports, dass SmartScreen
  sie abhält (T11.5: Warnung einmal je Download, „More info → Run anyway“).
  **Stand 19.11.2026:** kostenlose Signatur bei der SignPath Foundation beantragt (README-Absatz `7901350`) und am
  19.11.2026 **abgelehnt** (Kriterien: Stars/Forks/Contributors, externe Artikel, Reddit/YouTube-Referenzen). Martins
  Entscheidung: unsigniert bleiben, keine bezahlte Signatur; der README-Absatz ist raus (K12), D109 gilt unverändert.
  **Auslöser neu:** (a) erster Nutzer-Report, dass SmartScreen ihn abhält, oder (b) genug Sichtbarkeit für einen
  SignPath-Reapply. Dann: D-Eintrag (Signatur im Release-Workflow, Gegenargument), „unsigned“-Texte nachziehen – Nexus
  `description.bbcode` (Known issues „Unsigned“, Installationsschritt 3), Wiki Installation (Schritt 4,
  SmartScreen-Bild) und Troubleshooting („Unsigned“), README Schritt 3; Nexus- und SignPath-Bedingungen für
  Herkunftsangaben prüfen (A3).
- Screenreader-Zugang zur Skip-Checkbox der Update-Zeile: Toggle per UIA löst den Command nicht aus (`OnToggle` statt
  `OnClick`, T11.2); dazu die beiden „Clear“-Buttons im Settings-Fenster unterscheidbar benennen. Auslöser: Report eines
  Screenreader-Nutzers.
- Velopack-Log vollständig umleiten: `SetLogger` schreibt heute ab Information und erst ab gesetztem Datenordner nach
  `cargoplan.log`, Velopacks eigene Datei unter `%LocalAppData%\velopack\` bleibt (T11.2, Known issue 6). Auslöser: ein
  Bug-Report, der ohne diese Datei nicht zu klären ist.

- Rechteck-Picker für das Scan-ROI (der Nutzer zieht den Vertragsbereich einmal auf), für Seitenverhältnisse, bei denen
  das relative 16:9-Center-ROI nicht trifft (32:9, 16:10, geänderte HUD-Skalierung; S10.0 nur 21:9 und 16:9 geprüft).
  Auslöser: Nutzer-Report „ROI trifft nicht“.
- `PlanHistory.Entry.Reference` hat seit T10.4b (D107) keinen Leser mehr: `Record`/`PushUndo`/`PushRedo` reichen den Plan
  nach dem Schritt nur noch durch. Auslöser: der nächste Umbau der Undo-Historie (Messung `UndoTimingTests`, Test allein).
- `ScreenCaptureTests.A_hundred_captures_leave_the_gdi_object_count_as_it_was` zählt GDI-Objekte des ganzen Testprozesses,
  während WPF-Tests parallel rendern; einmal rot in T10.4b. Auslöser: zweites Auftreten → eigene Collection ohne
  Parallelität oder Toleranz mit Messung begründen (Lehre T7.1d).
- Vertragsliste in einem Scan statt eines Scans pro Vertrag (S10.0: ein Scan = ein Vertrag). Auslöser: Nutzer-Report.
- Mehrseitige Verträge (Text länger als eine Bildschirmseite, scrollen und zusammensetzen). Auslöser: Nutzer-Report
  oder ein Vertrag, dessen Lieferungen im Scan fehlen.
- **Kiosk-Manifest als autoritative Quelle** für die Mengen. Auslöser: Der Aufzug liefert eine andere SCU-Summe als der
  ACCEPTED-Text. Martins Antwort steht aus; IC STARC-175359/223408 deuten darauf hin.

- `holo-v2`- und `holo-v3`-Release (Pre-Releases im App-Repo) samt Tags löschen. Anlass: D114 – seit v0.6.0 kommen die
  Holos aus dem Data-Release, nur v0.3.0–v0.5.0 fragen noch `holo-v2` und v0.5.1 `holo-v3` (D63-Addendum T12.6a,
  Martin 19.11.: gleiches Datum für beide). **Auslöser: 31.01.2027** (zwei Monate nach v0.6.0). Danach: beide
  Manifest-URLs 404 prüfen, die alten Versionen zeigen den Umriss; CONTRIBUTING „Release hygiene“ (Holo-Releases im
  10er-Fenster) mitziehen.
- Modelle für 135c, 600i Executive Edition und F7C Hornet Mk II bei Sciirof (Motherlode) anfragen, mit Rückmeldung zur
  Handprüfung als Gegenleistung (Session 4a/4c). Auslöser: nach dem Launch, mit der Nachricht zu v0.3.0 (R5).
  **Stand 19.11.:** F7C erledigt (Spieldateien seit `ships-2026.11.1`, T12.2), 135c-Transform angekündigt
  (Shipdata-Backlog), **600i EE angefragt** (Discord 19.11.). **Auslöser: Sciirofs Antwort.**
- Sciirof fragen, warum die **Cyclone** im Katalog 2026-11-16 (4.10.3-LIVE.13165946) fehlt; bis dahin behält sie ihre
  holo-v2-Datei und ihren Umriss, `ships/cyclone.json` bleibt `absent` (V2). Auslöser: nächste Nachricht an Sciirof.
  **Antwort Sciirof (18.11.):** kommt ggf. zurück; die Pipeline deckt das mit SD6 „wieder da“ ab → Shipdata-Backlog
  (T12.6b), hier nichts zu tun. **→ Shipdata-Backlog.**
- Sciirof fragen nach einer Platzierung (Transform) für das Grid der **135c**: Modell `ORIG_135c` ist seit 2026-11-16 im
  Katalog, aber ohne Transform bleibt sie `kept` (Fleetyards-Racks, holo-v1). Mit Transform: Racks und Anker aus dem
  Modell. Auslöser: nächste Nachricht an Sciirof.
  **Antwort Sciirof (18.11.):** Transform kommt voraussichtlich; die Pipeline stoppt dann mit PR „override obsolete“
  (Regel in T12.6b), hier nichts zu tun. **→ Shipdata-Backlog.**

- Sektionsreihenfolge per Legenden-Drag (Spielerreihenfolge). Auslöser: Hauler-Report, dass die Legendenreihenfolge nicht reicht.
- `defaultFillOrder`, `fillRows`/`grouping`-Default pro Schiff in der Schiffs-JSON. Auslöser: zweites Schiff braucht einen anderen Default.
- Neubewertung „Sync-Mission wartet trotz freier Zellen“ (D89, Entscheidung 2). Auslöser: Live-Test, in dem eine Sync-Mission wartet, die mit `none` Platz hätte.
- Sektionsgrenzen in der Schiffsansicht zeichnen. Auslöser: Hauler-Report, die Farben zeigen es heute.
- Legend limit covers stop rows only; warnings stack below unbounded – revisit if a user reports it (K7/D80;
  16.11.: RAFT und Hull A mit 16 Missionen bei 30 % ok). Auslöser: Nutzer-Report.

- Warteeinheit Lieferung statt Mission (K6.4, E1): eine teilweise geladene Mission, deren Rest keinen Platz findet, lässt
  heute den Editor-Plan ablehnen. Auslöser: eine solche Ablehnung am Aufzug, vor allem nach K6.5 (Ziel an geladener
  CT-Mission nachgetragen). Aufwand: ≈ 20 Stellen an `Mission.Unplaced`, Formatfeld `stops[].unplaced`, Wartezeilen pro Lieferung.
- `RackAssignment` mit Hindernissen (K6.4, E2): mit geladenen Kisten legt der Planer nach der D41-Regel statt exakt (D64).
  Auslöser: Hauler-Report „Fragmentierung bei geladenen Kisten“ bzw. „Re-plan found no better layout“ ohne erkennbaren Grund.
- `CODE_OF_CONDUCT.md`, sobald es Contributors gibt; Community Standards mahnt es an, ein Solo-Repo braucht es vor dem ersten Hauler nicht (T6.4).
- Topmost-Fallback (`SetWindowPos(HWND_TOPMOST)` per Timer) nur bei Issue-Report mit Repro. Im T3.1-Test blieb das Overlay über Borderless-SC (D13).
- Vorschau-Maximum mit SC: einzelne Schlüsselwechsel kosten 20–25 ms (T8.7, M5; p95 5,2 ms, ohne SC max 12 ms); die
  16-ms-Regel (D65) fängt sie ab. Erst klären, ob CPU-Konkurrenz mit dem Spiel oder eine GC die Spitzen macht, dann ggf.
  der Recorder im echten Overlay (T8.7 R1). Auslöser: spürbares Nachziehen der Vorschau am Aufzug.
- `ShipCamera.PxPerCell`/`PanX`/`PanY` (Lesefelder für Kameras von vor D83, T8.1) entfernen. Auslöser: der nächste
  Schema-Bump von settings.json; nach v0.1.0 schreibt kein Build sie mehr.
- Hysterese für Rack-Nummern: Fläche wechseln oder ausblenden erst nach einem Mindestabstand, damit eine Nummer beim Drehen
  nicht hin- und herspringt (D83, T8.2). Auslöser: Flackern am Aufzug (T8.7) oder Hauler-Report.
- Kiste unter einem leeren vorderen Rack ist in der Ansicht nicht anklickbar (Regel D21 „erstes Rack entscheidet“);
  Alternative (b) nächste Kiste über alle Racks, Ablage behält „erstes Rack“. Auslöser: Hauler-Report oder Martin am Aufzug
  (Hull A von oben, Rack 1 leer). Wächter: `SceneHitTestTests.An_empty_rack_in_front_covers_the_cargo_behind_it` (T8.3, E1).
- Orbit-Snap auf feste Winkel (z. B. 45°-Raster per Taste), falls freies Drehen schwer wiederholbar ist (D15).
- Editor-Position in `settings.json`, Entwurfs-Autosave, Undo im Editor, falls der Zwei-Minuten-Durchlauf daran scheitert (D19). Undo-Teil → K2.
- Backdrop-Blur hinter dem Fenster (Spike: DWM vs. AllowsTransparency) (D28). Auslöser: Hauler-Report „Legende trotz Halo unlesbar“.
- IBM-Palette (`648FFF 785EF0 DC267F FE6100 FFB000`) als vierte Stop-Palette, sobald eine IBM-eigene Quelle die Hex-Werte nennt; Orange liegt ΔE76 26 am Problemrot (unter der Schwelle 33,6), müsste also ohne Orange kommen (K4.2). Auslöser: Hauler fragt danach.
- `appearance.crates.customStopColors` (Hex-Liste, aktiv bei `stopPalette = custom`), falls ein Hauler mit seltener Farbfehlsicht keine der drei Paletten lesen kann. Gegen: ungeprüfte Farben (D29), und eine Liste im Look-Record bricht die Gleichheit, an der K4.3 „Custom“ erkennt (K4.2).
- Startordner der Look-Dialoge (Export/Import, K4.3): ohne `InitialDirectory` nimmt Windows den zuletzt benutzten Ordner der App, bei Martin `AppData\Roaming\VoiceAccess\UserPhrases\de-DE`. „Dokumente“ als Start beim ersten Export, falls Hauler ihre Datei nicht wiederfinden. Auslöser: Meldung.
- `MissionFrame` (weiß, D68) ist dieselbe Farbe wie Okabe-Weiß (ΔE 0), obwohl der Kommentar „white is in no stop colour“ sagt: ein ausgewählter Missionsrahmen um eine weiße Kiste trägt nichts. Befund aus K4.2, kein Task; Auslöser: Look-Gate oder Hauler-Meldung.
- Starbreaker (Fleetyards-Extractor, Rust) als Quelle für die Gitter von Frachtmodulen. Auslöser: zweites Schiff mit Frachtmodul.
- Sync Ships in der App (Fleetyards-API), falls sich Frachtgitter häufiger ändern als Releases erscheinen oder Hauler zwischen Releases fehlende Schiffe melden. Voraussetzungen: D-Eintrag Netzwerkzugriff des Overlays, Lizenzfrage für Laufzeitabrufe, Merge-Logik aus import-fleetyards.ps1 nach C# (T7.1b; das Skript liegt seit 4c nur noch in der Git-Historie, `a3430b9^`, D94). Seit D92/D94 wäre die Quelle Motherlode, nicht Fleetyards.
- `CrateLayout.InOrder` beschleunigen (`StartingAt` ohne LINQ, Pruning), Auslöser: ein echter Plan mit der Notiz „too many crates to rearrange“ (D54). Erwartet bei Racks ab ~80 Zellen mit vielen 1-SCU-Kisten (T7.1c: 9×3×3, 32 + 40 × 1).
- Optionale Spieler-Reihenfolge (Legende per Drag sortieren) → Stapelhinweise relativ zur Spieler-Reihenfolge („Shubin liegt auf Riker, Riker kommt zuerst“). Auslöser: Hauler wollen wieder wissen, welche Kiste zuerst runter muss (D64).
- B1-Restfall: abgeschnittenes Fragment ohne Ergänzung. Capture 21–25 (24.10.): „to Thur“ bricht um, OCR liest „Thundering“ als „Thur…“, „Melg“ entsprechend; die Ergänzung aus der Drop-off-Liste greift nicht, weil das Fragment kein Präfix ist. Auslöser: weitere Fälle in echten Captures.
- Mengen pro Abholort („collect 3 from A, 2 from B“) statt einer Ortsliste ohne Mengen (D68). Auslöser: CT oder das Spiel
  nennen die Teilmengen.
- Kisten anderer Missionen ausblenden statt dimmen (D68). Auslöser: Martin oder ein Hauler will es nach dem Spieltest.
- Abholort ändern ohne Re-plan (heute braucht jede Editor-Änderung „Plan“, D19). Auslöser: ein Hauler trägt Abholorte erst
  nach dem Beladen nach und verliert dabei Moves.
- Mehrsprachigkeit, falls nicht-englische Hauler danach fragen. Beginnt beim Parser (deutsche CT-Captures als Fixtures), nicht bei `.resx` (D53).
- Fleetyards-Lage für Modul-Laderäume (Carrack, Caterpillar-Module und -Nase): heute modul-lokale Offsets, die überlappen, also generierte Reihe (D78). Auslöser: Fleetyards liefert schiffsweite Offsets für Module.
- Hull C: Stapelrichtung je Arm. Heute quer zum Arm, damit die Gitter 8×8×6/8×4×6 bleiben (D78); radial nach außen wie bei der Hull A hieße 8×6×8/8×6×4 mit Achse vom Kreuz weg. Auslöser: Beobachtung im Spiel oder in einem Beladungsvideo.
- Hull C: innerer und äußerer Block eines Arms ein Gitter oder zwei. Heute zwei Racks wie bei Fleetyards, beide einzeln voll packbar (K3.2). Auslöser: ein Hauler meldet, dass ein 32er über die Blockgrenze liegt.
- Kisten großer Schiffe im Default-Overlay klein: C2 13,2 px, Hull C 5,1 px pro Kistenkante (400 × 302, nach K5.2c), unter dem Hull-A-Wert 27,2 (Befund K5.2c, D62-Addendum). Auslöser: ein Hauler meldet zu kleine Kisten auf einem großen Schiff.
- Railen: die generierte Reihe (53 Zellen) ist länger als der Rumpf (40 Zellen), ragt aus dem maßstabsgetreuen Umriss (K5.2c). Auslöser: Fleetyards-Offsets für die Railen oder eine Meldung aus dem Spiel.
- Doku aus `docs/wiki/` im Hauptrepo per Action ins Wiki spiegeln – Auslöser: erster externer Doku-Beitrag.
- Screenshots/GIFs auf Perspektive nachdrehen (alle Shots von `docs/shots/SHOTLIST.md`, Hero-GIF eingeschlossen; D85) –
  Auslöser: nächstes Release mit sichtbarer Änderung der Ansicht oder erster Nutzer-Report „sieht bei mir anders aus“.
- UI-Text-Konsistenz (Befund T7.5a): Schiffswechsel-Dialog mit Yes als Default, Re-plan mit No; Export… und Remove ohne
  Tooltip, gesperrte Knöpfe nennen ihren Grund nur teilweise (Plan, ↑/↓, Add to mission ja; Export…, Re-plan, Discard plan,
  Delete mission nein); Hotkey-Notice „already in use“ nennt den JSON-Key statt des Fenster-Labels; Notices mal mit, mal
  ohne Schlusspunkt, „crates capped …“ klein; Weiß dreifach belegt (Rand „loaded“, Missionsrahmen, Okabe-Weiß). Auslöser:
  nach v0.1.0.
- Auffangnetz je `DispatcherTimer` (Review F10): das D82-Netz (`GuardedAsync`, Pause nach 5 Fehlern) deckt nur den
  Sync-Tick; `PlanSaver`, `OverlayBoundsSaver`, `CursorStateMonitor`, `HotkeyService`, `SettingsSession` laufen in
  `DispatcherUnhandledException` (Log + Dialog, App endet). Bewusst so, die Ticks fangen selbst. Auslöser: ein Crash-Report
  aus einem dieser Ticks.
- Virtual-Key-Codes an einem Ort (Review F16): `NativeMethods.cs` (Maustasten), `HotkeyPoller.cs` (Modifier),
  `HotkeyParser.cs` (Tasten), kein Wert doppelt. Eine `VirtualKeys`-Datei. Auslöser: T10.3 braucht den Scan-Default als VK
  oder ein vierter Ort.
- Ubuntu-CI ohne WPF (Review F20): Ubuntu kompiliert alle Projekte inkl. WPF (`EnableWindowsTargeting`), testet aber nur
  Core und Import. Auf `Core`, `Import`, `Cli` und ihre Tests beschränken spart die Referenzpack-Kompilierung. Auslöser:
  die CI-Laufzeit stört.
- `HoloLoaderTests.WaitFor` (Review F21): Polling 200 × 10 ms (2 s Deckel), einziger zeitabhängiger Test im Default-Run.
  Auslöser: der erste rote Lauf auf einem Runner (Lehre T7.1d).
- Lange Methoden im Planer (Review F22): `CrateLayout.Apart` (192 Z.), `LoadPlanner.LayStop` (106 Z.); `App.OnStartup`
  schneidet T10.1. Auslöser: die nächste inhaltliche Änderung an einer der beiden.
- Schiffs-Schema 4 nicht mehr lesen (Review F6): v0.1.0 lieferte 5, Community-Dateien sind 5 oder 6. 23 `TestShips` und
  `PlainShip()` in `ShipLoaderTests` stehen auf 4, zwei Theories prüfen 4 ausdrücklich (K10: Umbau größer als der Nutzen).
  Auslöser: der nächste Schiffs-Schema-Sprung (7), im selben Zug.
- `ImportMotherlodeScriptTests` aufteilen (K10, F26): 13,4 s in einer Klasse, jeder Test startet PowerShell; xUnit führt
  eine Klasse nacheinander aus, sie bestimmt die Core-Wandzeit (15,0 s). Zwei bis drei Klassen laufen parallel. Auslöser:
  der Core-Testlauf stört.

- Game.log-Reader (Phase 13, Design §1.6, §4.1): angenommene Verträge, missionId und Abschluss aus dem Log statt aus dem
  Accepted-Scan. Auslöser: Nutzer wollen den Accepted-Scan sparen.
- Hotkey für den Reiterwechsel Cargo | Plan (T13.2, D120): gesperrt lässt sich die Leiste nicht klicken. Auslöser: ein
  Spieler wechselt im Lock-Modus.
- Fenstergröße je Reiter (T13.2, D120): die Matrix nutzt die Fläche von Legende und Schiffsansicht. Auslöser: Bild- oder
  Spieltest zeigt, dass die Standardgröße für die Matrix nicht reicht.
- Board-Hub für planetary und local (D121): dort bleibt eine leere Pool-Seite leer. Auslöser: ein Scan zeigt, wo ein
  solches Board steht.
- Zähler-Überwachung „HAULING – STELLAR n“ (Phase 13, Design §1.4, §4.3 Schritt 8): ändert sich n, Hinweis zum
  Nachscannen; nur Komfort, keine Planer-Logik (Invariante 4; den Pool gibt es seit U3 nicht mehr). Auslöser:
  Nutzer-Report „Angebote veraltet“.
- Planetary- und CrossStanton-Verträge im Planer (Phase 13 plant nur Stellar; Design §5: Sichtbarkeitsregel offen,
  vermutlich Nähe zum Pickup). Auslöser: Sichtbarkeitsregel geklärt oder Nutzer-Report.
- Pyro-Fraktionsgate HH/CFP (Design §7.6): sieht ein Spieler nur eine Seite? Bis dahin Fraktion als Filter in den
  Settings (T13.3). Auslöser: empirischer Befund im Spiel.
- Red-Wind-Regionen (`RegionA`–`RegionD`) auf dem Pyro-Board (T13.3, Design §5, §7.6): heute nur `Solar`, der Reiter sagt
  es. Auslöser: erster Pyro-Scan einer Region-Variante zeigt, welche Station welche Region hat.
