# SC-CargoPlan

Open-Source Ladeplan-Overlay für Star Citizen Hauling (Alpha 4.10.x). Windows-Desktop-App, .NET / WPF.
**Ziel: möglichst viele Hauler nutzen es.** Reichweite und Einfachheit schlagen Eleganz.

## Wer hier arbeitet

Martin, 20+ Jahre IT (PowerShell, M365, Power Platform, Netzwerk). C# ist neu, PowerShell-Hintergrund hilft (gleiche .NET-Typen, ähnliche Syntax). Will den Code verstehen, nicht nur ausführen.

## Arbeitsweise (verbindlich)

- Wir sind per Du. Prägnant, direkt, keine Höflichkeitsfloskeln.
- **Bei jedem Schritt und jedem Befehl in einem Satz erklären, warum er nötig ist.**
- **Unterhaltung Deutsch. Code, Kommentare, Commits, README, UI-Texte: Englisch.** Warum: Open Source mit internationaler Zielgruppe.
- Moderne C#-Syntax (file-scoped namespaces, `record`, Pattern Matching, Collection Expressions, Primary Constructors), knappe Kommentare.
- Bei Mehrdeutigkeit **nachfragen statt raten**.
- Fachbegriffe korrigieren, wenn Martin sie falsch verwendet. Wo C# anders tickt als PowerShell, kurz darauf hinweisen.
- **Ein Task aus `TASKS.md` pro Session.** Vorher Task vorlesen, nachher Akzeptanzkriterien einzeln abhaken.
- **Tests vor Implementierung** bei allem mit Logik (Planer, Modelle, Reader).
- Keine neuen NuGet-Pakete ohne kurze Begründung. Weniger Pakete = kleinere EXE = weniger Angriffsfläche.

## Das Problem

Hauling-Aufträge für Covalex sind oft **1 Abholort → n Ziele**. Jeder Container ist intern an Mission + Ziel gebunden. Der Frachtaufzug am Ziel nimmt nur passende Container und **verweigert die ganze Übergabe**, sobald ein fremder Container auf der Plattform liegt. Das Spiel zeigt nicht, welcher Container wohin gehört.

## Der Ansatz

**Nicht erkennen, sondern festlegen.** Vor dem Beladen wird bestimmt, welches Ziel in welchen Schiffsabschnitt kommt. Der Spieler lädt nach Plan, das Overlay hält die Zuordnung fest, am Ziel wird der Block abgehakt.

- **Input:** pro Mission: Ziel → Ware → SCU → Kistengröße (1/2/4/8/16/24/32 SCU)
- **Verarbeitung:** Bin-Packing mit Reihenfolge-Constraint. Erster Stopp in Rack 1, die folgenden in Rack-Reihenfolge, kein Ziel über eine Abschnittsgrenze, wenn vermeidbar.
- **Output:** halbtransparentes Always-on-top-Overlay mit **Click-through als Modus** (Lock, D11), Schiff schematisch, farbige Blöcke pro Ziel mit Label („Riker · 12 SCU Titanium“), abhakbar.

## Domänenfakten

- **MISC Hull A:** 64 SCU, Spindel mit **4 Platten à 16 SCU**, getrennt beladbar (bestätigt). Rack = Ziel ist umsetzbar.
- **Lage:** 2 Seiten × 2 Ebenen, **nicht vorne→hinten**. Rack 1 = Backbord oben, Rack 2 = Backbord unten, Rack 3 = Steuerbord oben, Rack 4 = Steuerbord unten. Rack-Reihenfolge = Planungsreihenfolge, unabhängig von der Lage.
- **Raster:** jede Platte 4 × 2 × 2 Zellen (Length entlang der Spindel × Width quer × Stack weg von der Platte), 1 Zelle = 1 SCU = 1,25 m. Kisten in fester Orientierung: 1 = 1×1×1, 2 = 2×1×1, 4 = 2×2×1, 8 = 2×2×2, 16 = 4×2×2, 24 = 6×2×2, 32 = 4×4×2. Jede Mischung ≤ 16 passt in eine Platte, deshalb **kein 3D-Packer** (D9).
- Später: Cutlass Black, RAFT, Community-Beiträge für weitere Schiffe.
- Begriffe: **Rack** = Schiffsabschnitt (Platte), **Grid** = Zellraster eines Racks, **Cell** = 1 SCU, **Position** = Seite + Ebene, **Stop** = Zielhafen in Reihenfolge, **Placement** = Fracht → Rack.
- Star Citizen nutzt Easy Anti-Cheat. **Externes Fenster = unkritisch. Nichts in den Spielprozess injizieren, keine Hooks.**

## Technische Entscheidungen

| Was | Entscheidung | Warum |
|---|---|---|
| Runtime | **.NET 10** (aktuelles LTS) | Support bis 2028. .NET 8 läuft Nov 2026 aus. |
| UI | **WPF** | Rahmenlose transparente Fenster seit 15 Jahren stabil, Win32-Handle direkt greifbar. WinUI 3 kann Transparenz schlecht, Avalonia bringt Cross-Platform, das wir nicht brauchen. |
| MVVM | **CommunityToolkit.Mvvm** | Source-Generatoren sparen Boilerplate (`[ObservableProperty]`, `[RelayCommand]`). Einziges UI-Paket. |
| JSON | `System.Text.Json` | Stdlib, kein Newtonsoft. |
| SQLite | `Microsoft.Data.Sqlite` | Offizielles, schlankes Paket für den ContractTracker-Import. |
| Tests | **xUnit** + `AwesomeAssertions` | Standard im .NET-Ökosystem. AwesomeAssertions ist der Apache-2.0-Fork von FluentAssertions (ab v8 kommerziell lizenziert). |
| Click-through | Win32 `SetWindowLongPtrW` schaltet `WS_EX_TRANSPARENT` via `[LibraryImport]`, HWND aus `WindowInteropHelper`; `WS_EX_LAYERED` setzt WPF selbst (D11) | Keine Lib nötig. |
| Hotkeys | Win32 `RegisterHotKey` + `HwndSource.AddHook` für `WM_HOTKEY` | Global, auch wenn das Spiel den Fokus hat, keine Lib. |
| Distribution | `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` → **eine EXE (~70 MB)**, zusätzlich framework-dependent (~3 MB) | Nutzer sollen nichts installieren müssen. Kleine Variante für die, die .NET haben. WPF kann nicht getrimmt werden. |
| CI/CD | GitHub Actions, `windows-latest`, Release bei Git-Tag | Niemand baut von Hand. |
| Lizenz | **MIT** | Maximale Verbreitung. |

**Kernregel:** `CargoPlan.Core` referenziert **nichts** aus WPF oder Windows-APIs. UI und Import referenzieren Core, nie umgekehrt. Warum: Planer bleibt testbar und tauschbar.

## Projektstruktur (Soll)

```
sc-cargoplan/
├── CLAUDE.md · TASKS.md · README.md · LICENSE
├── SC-CargoPlan.sln
├── Directory.Build.props          # gemeinsame Settings: LangVersion, Nullable, TreatWarningsAsErrors
├── src/
│   ├── CargoPlan.Core/            # net10.0 – Modelle, Planer, Ship-Loader, Ships/*.json als EmbeddedResource
│   ├── CargoPlan.Import/          # net10.0 – ContractTracker SQLite-Reader
│   ├── CargoPlan.Cli/             # net10.0 – Plan im Terminal ausgeben (Debugging ohne UI)
│   └── CargoPlan.App/             # net10.0-windows, WPF – Overlay, ViewModels, Win32-Interop
├── tests/
│   ├── CargoPlan.Core.Tests/
│   └── CargoPlan.Import.Tests/
├── docs/
│   └── contracttracker-schema.md
└── .github/workflows/
    ├── ci.yml                     # build + test bei jedem Push/PR
    └── release.yml                # publish + GitHub Release bei Tag v*
```

## Befehle

```powershell
dotnet build                                   # alles kompilieren
dotnet test                                    # alle Tests
dotnet run --project src/CargoPlan.App         # Overlay starten
dotnet run --project src/CargoPlan.Cli -- plan tests/CargoPlan.Core.Tests/Fixtures/example_missions.json
dotnet publish src/CargoPlan.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Anti-Patterns – wovor Claude Code sich schützen soll

| # | Stichwort | Regel |
|---|---|---|
| A1 | **Grep-first** | Vor jedem neuen Typ, jeder Methode, jeder Datei: `Grep` nach dem Namen **und nach Synonymen des Zwecks** (`Render`, `Format`, `Print`…). Treffer ⇒ Ist-Zustand melden und stoppen, nicht daneben neu bauen. Der Compiler fängt nur exakte Doppelnamen im selben Namespace. |
| A2 | Rück-Sweep | Neue Hausregel gelernt ⇒ Grep über den Bestand als eigener Mini-Task, nicht nur in künftigen Code anwenden. |
| A3 | Confident Hallucination | P/Invoke-Signaturen, NuGet-APIs, Attribute (`[LibraryImport]`, `[ObservableProperty]`, STJ-Generatoren) **gegen aktuelle Doku prüfen**, Quelle als Kommentar an die Stelle. Gedächtnis reicht nicht. |
| A4 | YAGNI / Minimal zuerst | Kein Interface ohne zweite Implementierung, kein DI-Container ohne Bedarf, keine Abstraktion „für später“. Erst die kleinste lauffähige Version, dann erweitern. |
| A5 | Sycophancy | Jede Architekturentscheidung mit mindestens einem Gegenargument. Martin ist C#-Einsteiger und kann nur abwägen, was er sieht. |
| A6 | Defensiv nur an Grenzen | Grenzen: JSON-Laden, SQLite-Reader, Win32-Calls, Nutzereingaben. Innen (Core) sind Records validiert – kein Null-Check-Teppich. |
| A7 | Plan-Mode-Stopp | Task berührt ≥ 3 Dateien ⇒ erst Plan vorlegen, dann auf wörtliche **„Freigabe“** warten. Ein Neustart des Plan Mode hebt eine ausstehende Freigabe nicht auf. |
| A8 | Stille Annahmen | Fragen statt raten. |

## NIEMALS

- `catch { }` oder `catch (Exception) { }` ohne Logging/Rethrow (stille Fehler)
- `.Result` / `.Wait()` / `GetAwaiter().GetResult()` im UI-Thread (Deadlock in WPF)
- `async void` außer bei Event-Handlern
- Magic Numbers für Win32 – jede Konstante benannt (`WS_EX_TRANSPARENT = 0x20`)
- `Console.WriteLine` in Core/App (nur in `CargoPlan.Cli`)
- Hardcoded Pfade, Hotkeys, Farben – gehört in `settings.json` oder eine zentrale Konstanten-Datei
- `TODO` ohne Issue-Nummer
- Planungslogik in Views oder ViewModels

## IMMER

- `Nullable` enabled, Warnungen sind Fehler (steht in `Directory.Build.props`)
- Records immutable, Änderung via `with`
- Externe Library-Aufrufe gegen aktuelle Doku verifiziert (A3)
- Entscheidungen mit Alternativen in `docs/decisions.md` festhalten – nummeriert, je 5–10 Zeilen: Kontext, Entscheidung, Gegenargument, Konsequenz

## Kommunikation mit Martin

- Deutsch, prägnant, Markdown mit fetten Schlüsselbegriffen, Trade-offs statt nur Lösungen
- **Abweichungen vom Task-Text in der Planphase nie als „Martins Entscheidung“ etikettieren.** Korrektes Label: „Präzisierung in der Planphase, ratifiziert durch Freigabe“ (nie „Abweichung“), auch im Commit – **auch für Präzisierungen, die Martin selbst gewählt hat** (Startprompt, Antwort auf eine Rückfrage): in Plan, TASKS.md und Commit-Body ein Label für alle Punkte.
- Niemals ungefragt Framework-Wechsel, „neuere Library“ oder Cross-Platform vorschlagen – Stack ist gesetzt, Änderung nur per Eintrag in `docs/decisions.md` nach Diskussion.

## Definition of Done (jeder Task)

1. Akzeptanzkriterien aus `TASKS.md` erfüllt und einzeln vorgeführt.
2. `dotnet build` ohne Warnungen (`TreatWarningsAsErrors`), `dotnet test` grün.
3. Martin hat jede neue Datei einmal erklärt bekommen (Zweck, Kernidee, nicht Zeile für Zeile).
4. Task in `TASKS.md` auf `[x]`, Commit mit Task-Prefix (`T2.1: ...`), englische Message.