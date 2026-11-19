# Archiv: Phase 0 – Fundament

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Phase 0 – Fundament

### [x] T0.1 Solution-Scaffold
**Warum:** Projektgrenzen (Core ohne UI) müssen am Tag 1 stehen, später ist das nicht mehr zu trennen.
- `dotnet new sln`, Projekte wie in CLAUDE.md anlegen (`classlib`, `console`, `wpf`, `xunit`), Referenzen setzen: App → Core + Import, Import → Core, Cli → Core, Tests → jeweiliges Projekt
- `Directory.Build.props`: `<LangVersion>latest</LangVersion>`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- `.gitignore` (dotnet-Template), `.editorconfig` (dotnet-Defaults, 4 Spaces)
- `LICENSE` MIT, `README.md` englisch: what, why, download-link-Platzhalter, 3 Zeilen build
- `docs/decisions.md` mit den ersten Einträgen aus CLAUDE.md (D1 .NET 10, D2 WPF, D3 Records, D4 Single-File-EXE) – englisch, je Kontext / Entscheidung / Gegenargument / Konsequenz
- Ein Smoke-Test in `Core.Tests`

**Akzeptanz:** `dotnet build` ohne Warnung, `dotnet test` grün (1 Test), `git log` zeigt Initial-Commit. Martin kann erklären, was `Directory.Build.props` gegenüber Einstellungen pro `.csproj` bringt und warum Core kein `net10.0-windows` ist.

### [x] T0.2 CI-Workflow
**Warum:** Ab dem ersten PR von außen muss klar sein, ob er baut. Das kann nicht Martins Job sein.
**Warum Matrix:** Ursprünglich nur `windows-latest` geplant, ergänzt um `ubuntu-latest`. Der Linux-Lauf fängt Windows-Annahmen in Core/Import und den Tests früh ab (Pfadtrenner, Zeilenenden, Culture) und hält Core plattformneutral. Alternative „Ubuntu ohne App per Solution-Filter“ verworfen: zweiter Befehlspfad und eine Datei mehr für wenig Zusatznutzen.
- `.github/workflows/ci.yml`: Trigger `push` auf `main` + `pull_request`, Matrix `ubuntu-latest` + `windows-latest`, Schritte `setup-dotnet` (10.0.x) → `dotnet restore` → `dotnet build -c Release --no-restore` → `dotnet test -c Release --no-build`
- Concurrency-Gruppe pro Ref, laufender Lauf wird bei neuem Push abgebrochen
- Ubuntu baut die WPF-App über `EnableWindowsTargeting=true` (nur kompilieren, nicht ausführen)
- `.github/dependabot.yml`: `nuget` + `github-actions`, wöchentlich
- Badge in README

**Akzeptanz:** Workflow läuft auf beiden Runnern grün auf GitHub. Martin kann erklären, warum `windows-latest` zwingend ist (WPF läuft nur dort, Linux kompiliert nur gegen Reference Packs) und was der Ubuntu-Lauf zusätzlich absichert.

---

