# Archiv: Bugfix B10 – Hotkeys im Spiel

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Bugfix B10 – Hotkeys im Spiel (Patch v0.3.1)

Eingeschoben vor Phase 10: T10.3 baut auf dem gepollten Hotkey auf.

### [x] B10 Hotkeys im Spiel (Branch `fix/hotkeys-in-game`, D97)

- **Anlass:** `RegisterHotKey`-Hotkeys feuern in v0.3.0 nicht, solange Star Citizen im Vordergrund ist (S10.0-Befund; die gepollte Taste F8 funktionierte).
- **Schritt 1, Ursache klären:** das v0.1.0-Release-ZIP neben dem aktuellen SC testen. Feuert es nicht, hat sich das Spiel geändert; feuert es, ist es eine Regression der App (Bisect).
- **Bei geändertem Spiel:**
  - ein Pfad: `GetAsyncKeyState`-Polling auf einem `DispatcherTimer` (30 Hz, Konstante) mit Flankenerkennung, Modifier wie gespeichert
  - aktiv nur, wenn SC oder CargoPlan vorne ist; ausgesetzt bei Fokus in einem Textfeld und während der Aufnahme
  - D35-Addendum: keine OS-Prüfung „belegt“ mehr, nur reservierte Tasten (Space, F12, Esc), Hinweis „keys also reach the game“
- **Gate im Spiel:**
  - jeder Hotkey genau einmal pro Druck, kein Auto-Repeat
  - kein Auslösen im Editor-Textfeld oder mit dem Browser vorne
  - Frame-Zeit unverändert
- Kein F3-Default, kein Scan-Hotkey (beides Phase 10).

**Befund (09.11.):** Hotkeys über SC sind in keinem Release-Build nachweislich angekommen: das v0.1.0-ZIP feuert im Spiel auch nicht (Martin), die isolierte Probe aus S10.0 bestätigt, dass `RegisterHotKey` mit SC vorne nicht zugestellt wird. `Hotkeys/`, `Win32/`, Overlay zwischen v0.1.0 und v0.3.0 unverändert, also keine Regression zwischen Releases → Polling (Schritt 2b). Vermutete Ursache (nicht prüfbar): SC registriert Raw Input mit `RIDEV_NOHOTKEYS`.

**Präzisiert in der Planung:** Testnaht als Delegate `Func<uint, bool>` statt `IKeyState` (A4); Ctrl/Alt/Win bleibt Pflicht, **Space reserviert** neben F12; Log pro Auslöser in `AppLog` (es gibt kein separates Debug-Log); Modifier exakt wie gespeichert; `TrySet` → `Set`. Review Claude.ai: K1 Befund ohne „25.10. wirkte noch“ (D17-Addendum „never verified in game“); K2 Textfokus zählt nur mit eigenem Prozess vorne; E1 Known issue „SC als Admin → CargoPlan als Admin“ in Release-Notes und Wiki; E2 UI-Thread-Stall als D97-Gegenargument, mit aktuellen Zahlen (Plan-Lauf ≤ 3,2 ms je Fixture, Stresslast 80 ms seit B2/D66; die ~4 s Freelancer stammen aus T7.2 vor B2). Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Umsetzung (Branch `fix/hotkeys-in-game`):** `HotkeyPoller` (Flanke, exakte Modifier, `IsArmed`), `HotkeyService` pollt alle 33 ms, `RegisterHotKey`-Pfad entfernt, Hinweise im Settings-Fenster, D97 + Addenda D17/D35, CLAUDE.md, settings-format.md. Poll-Last gemessen: **4,7 µs pro Tick** (Release, `HotkeyPollTimingTests`; Prozessname bei Fensterwechsel 14 µs).

- [x] Tests vor Implementierung: `HotkeyPollerTests` (Flanke, Halten, Modifier exakt, Win links/rechts, unscharf, `Set` bei gehaltener Taste, K2), Parser Space reserviert, AppLog-Textzeile
- [x] `dotnet build` ohne Warnungen, `dotnet test` grün
- [x] Gate Martin im Spiel (09.11., abgenommen): jeder gesetzte Hotkey genau einmal pro Druck mit SC vorne; `cargoplan.log` zeigt toggleLock, toggleOverlay, syncContractTracker, replan, undo, redo, resetView je Druck eine Zeile (undo/redo vor dem ersten Schritt „not available now“), drei Drücke in 0,6 s einzeln erkannt; discardPlan war nicht belegt
- [x] Gate: Taste gehalten → kein Mehrfachauslösen
- [x] Gate: Hotkey im Editor-Textfeld getippt → kein Auslösen
- [x] Gate: SC minimiert, Browser vorne → kein Auslösen
- [x] Gate: Aufnahme funktioniert, Clear schreibt `null`
- [x] Gate: Frame-Zeit `--over-game` gegen `main` unverändert (Martin)
- [x] Merge `--no-ff` nach Abnahme
- [x] Release v0.3.1 (Notes mit Known issue E1, ZIP, Wiki „Overlay basics#hotkeys“, Tag, Issue) – **v0.3.1 veröffentlicht (09.11.2026)**, siehe unten

