# Archiv: Release v0.3.1

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Release v0.3.1 (09.11.2026) – Branch `release/v0.3.1`

**Warum:** B10 (D97) als Patch, damit der Nexus-Start am 16.11. mit funktionierenden Hotkeys rausgeht. Ablauf wie v0.3.0: Notes → Build nachmessen → Tag → Wiki → Abschluss. Notes-Texte von Claude.ai freigegeben; Martin pusht und legt das GitHub-Issue an.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
1. Branch `release/v0.3.1` (wie `release/v0.3.0`), `--no-ff` nach Martins Abnahme; Tag auf dem Merge-Commit.
2. Notes im Rahmen von v0.3.0: keine H1 (GitHub zeigt den Tag als Titel), Einleitung + Download-Absatz + MIT-Zeile; die freigegebenen Sätze wörtlich, nur „(D97)“ gestrichen (Nutzer können D-Nummern nicht nachschlagen) und „Known issues from v0.3.0“ auf `Troubleshooting#known-issues` verlinkt.
3. Veralteter UI-Grund in `HotkeyParser` („or it would take the key from every program“) in v0.3.1 korrigiert: die gepollte Taste erreicht das Spiel (D97).
4. Wiki über „Overlay basics“ hinaus: Troubleshooting-Abschnitt Hotkeys, FAQ-Antwort zum Anti-Cheat, `Space` aus der Tastenliste, Fußzeile v0.3.1.

### [x] V1 Notes, Code-Grund, Nexus-Version (09.11.2026)

- `docs/release-notes/v0.3.1.md`; `HotkeyParser`-Grund mit Test vorab; `docs/nexus/checklist.md` und `description.bbcode` auf 0.3.1

**Befund:**
- `HotkeyParserTests.A_key_without_Ctrl_Alt_or_Win_is_refused_because_it_reaches_the_game` erst rot mit dem alten Text, nach dem Fix grün; sonst nirgends „every program“ in `src/`/`tests/`. Commits `0fab8eb` (Code), `5be8921` (Notes, Nexus, TASKS).
- `dotnet build` 0 Warnungen; `dotnet test` **Core 1819** (1818 ohne die lokale Scratch-Datei), **App 1698** (+1), **Import 164**. Ein erster Lauf auf `main` blieb im App-Testhost stehen (> 10 min, CPU still); abgebrochen, danach liefen die App-Tests zweimal in 3–4 s durch, mit `--blame-hang-timeout` ohne Hänger. Nicht reproduziert.
- Vollständigkeit `git log v0.3.0..main`: B10 (4 Commits + Merge `4326513`), S10.0-Befund, `d93bed6` (Doku), `7010eff` (REL-Messung). Nur B10 berührt `src/`/`tests/`; `global.json` und Pakete unverändert.
- „Ein gespeicherter `…+Space`-Hotkey wird mit Hinweis ignoriert“ belegt durch `HotkeyService` (Zeile „Hotkey … ignored: …“), Esc bricht die Aufnahme ab (`SettingsWindow`).

### [x] V2 Build, Merge, Tag, Nachmessung (09.11.2026)

- `publish.ps1 -Version 0.3.1` lokal: ZIP-Größe gegen v0.3.0 lokal (62.121.133 Byte), Einträge, Runtime, `--version`; > 1 MB Abweichung = Stopp
- Martin: Abnahme → `--no-ff` nach `main`, Tag `v0.3.1`, Martin pusht `main` und Tag
- Veröffentlichtes ZIP nachmessen, Notes-Text im Release = Datei

**Befund (lokal, Stand `5be8921`):** ZIP **62.121.374 Byte** (v0.3.0 lokal 62.121.133, +241 Byte), SHA256 gleich `.sha256`, 368 Einträge (wie v0.3.0), `ships/` 106 JSON = 105 + `modules/ts_module.json`, `outlines/` `ship-outlines.json` + `NOTICE.md`, Runtime `Microsoft.NETCore.App` und `WindowsDesktop.App` 10.0.14, `CargoPlan.Cli --version` `0.3.1+5be8921…`.

**Befund (nach dem Push):**
- Merge `--no-ff` `f87b201` (Martin, Befehle von CC), Tag `v0.3.1` annotiert auf `f87b201`, `release/v0.3.1` lokal gelöscht (nie gepusht). Martin: `main` und Tag gepusht, CI und Release-Workflow grün.
- Release `v0.3.1`: kein Pre-release, kein Draft, `releases/latest` → `v0.3.1`, veröffentlicht 09.11.2026 10:59 UTC.

| Prüfung | Soll | Veröffentlichtes ZIP | |
|---|---|---|---|
| SHA256 gegen `.sha256` | gleich | `B6141E9F…72CF43CF`, gleich | ✓ |
| Größe | wie v0.3.0 (CI 64.201.672) | 64.201.711 Byte (+39 Byte zu v0.3.0; lokal 62,1 MB, CI-Kompression wie bei v0.2.0/v0.3.0) | ✓ |
| Runtime | 10.0.14 | `Microsoft.NETCore.App` und `WindowsDesktop.App` 10.0.14 | ✓ |
| Einträge | wie lokal | 368, Eintragsliste gleich dem lokalen `publish.ps1 -Version 0.3.1` | ✓ |
| `ships/` | 105 + 1 Modul | 106 JSON | ✓ |
| `CargoPlan.Cli --version` | `0.3.1+f87b201…` | `0.3.1+f87b20165779767807dd09cb653c6ab574110879` | ✓ |
| Release-Text | = `docs/release-notes/v0.3.1.md` | zeichengleich (ordinal, nach Zeilenende-Normalisierung) | ✓ |
| Settings-Fußzeile | `SC-CargoPlan 0.3.1+…` | Martin | offen |

### [x] V3 Wiki und Abschluss (09.11.2026)

- Wiki nach dem Tag: Overlay-basics `#hotkeys`, Troubleshooting `#a-hotkey-does-nothing` + Known issues, FAQ, `_Footer`
- B10 abhaken, Memory, Grep `RegisterHotKey|WM_HOTKEY` in `docs/` und Wiki
- Martin: GitHub-Issue „Hotkeys do not fire while Star Citizen is in the foreground (v0.1.0–v0.3.0)“, als fixed in v0.3.1 schließen

**Befund:**
- Wiki `26d3996` (Martin pusht): Overlay-basics `#hotkeys` (in front = SC oder CargoPlan, nicht beim Tippen in CargoPlan-Textfeldern; Taste erreicht das Spiel; Admin-Zeile; `Space` aus der Tastenliste, „Space and F12 are reserved“; Satz „Windows or another program already uses it“ raus), Troubleshooting `#a-hotkey-does-nothing` neu (anderes Programm vorne, Admin, `…+Space` nach dem Update, Spiel reagiert mit; „registered“/„swallows“/„taken“ raus) und Admin-Zeile unter Known issues, FAQ Anti-Cheat („asks Windows whether those keys are down … no hook“ statt „normal Windows hotkey service“), Fußzeile v0.3.1. Anker unverändert.
- Grep Wiki nach `has the focus|every program|registered|hotkey service|swallow|\`Space\`|RegisterHotKey|WM_HOTKEY`: 0 Treffer. `docs/`: nur `decisions.md` (D17 + Addendum, D35, D60, D71, D97) – Entscheidungshistorie.
- Offen (Martin): Wiki-Push, GitHub-Issue anlegen und als fixed in v0.3.1 schließen, Settings-Fußzeile 0.3.1. Nexus-Upload am 16.11. mit **0.3.1** nach `docs/nexus/checklist.md` (ersetzt „mit 0.3.0“ in R5).

