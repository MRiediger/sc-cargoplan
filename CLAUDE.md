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

- **Input:** pro Mission: Ziel → Ware → SCU → Kistengröße (1/2/4/8 SCU)
- **Verarbeitung:** Bin-Packing mit Reihenfolge-Constraint. Erster Stopp vorne, letzter hinten, kein Ziel über eine Abschnittsgrenze, wenn vermeidbar.
- **Output:** halbtransparentes Always-on-top-Overlay mit **Click-through**, Schiff schematisch, farbige Blöcke pro Ziel mit Label („Riker · 12 SCU Titanium“), abhakbar.

## Domänenfakten

- **MISC Hull A:** 64 SCU, Spindel mit **4 Abschnitten à 16 SCU**, getrennt beladbar (bestätigt). Rack = Ziel ist umsetzbar.
- **Offen:** exaktes Slot-Raster pro Abschnitt, welche Kistengrößen wo passen. Martin liefert das aus dem Spiel. Bis dahin **kapazitätsbasiert** (SCU-Summe pro Rack).
- Später: Cutlass Black, RAFT, Community-Beiträge für weitere Schiffe.
- Begriffe: **Rack** = Schiffsabschnitt, **Slot** = Stellplatz, **Stop** = Zielhafen in Reihenfolge, **Placement** = Fracht → Rack.
- Star Citizen nutzt Easy Anti-Cheat. **Externes Fenster = unkritisch. Nichts in den Spielprozess injizieren, keine Hooks.**

## Technische Entscheidungen

| Was | Entscheidung | Warum |
|---|---|---|
| Runtime | **.NET 10** (aktuelles LTS) | Support bis 2028. .NET 8 läuft Nov 2026 aus. |
| UI | **WPF** | Rahmenlose transparente Fenster seit 15 Jahren stabil, Win32-Handle direkt greifbar. WinUI 3 kann Transparenz schlecht, Avalonia bringt Cross-Platform, das wir nicht brauchen. |
| MVVM | **CommunityToolkit.Mvvm** | Source-Generatoren sparen Boilerplate (`[ObservableProperty]`, `[RelayCommand]`). Einziges UI-Paket. |
| JSON | `System.Text.Json` | Stdlib, kein Newtonsoft. |
| SQLite | `Microsoft.Data.Sqlite` | Offizielles, schlankes Paket für den ContractTracker-Import. |
| Tests | **xUnit** + `AwesomeAssertions` | Standard im .NET-Ökosystem. |
| Click-through | Win32 `SetWindowLongPtr` mit `WS_EX_LAYERED \| WS_EX_TRANSPARENT` via P/Invoke, HWND aus `WindowInteropHelper` | Keine Lib nötig. |
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

## Definition of Done (jeder Task)

1. Akzeptanzkriterien aus `TASKS.md` erfüllt und einzeln vorgeführt.
2. `dotnet build` ohne Warnungen (`TreatWarningsAsErrors`), `dotnet test` grün.
3. Martin hat jede neue Datei einmal erklärt bekommen (Zweck, Kernidee, nicht Zeile für Zeile).
4. Task in `TASKS.md` auf `[x]`, Commit mit Task-Prefix (`T2.1: ...`), englische Message.
