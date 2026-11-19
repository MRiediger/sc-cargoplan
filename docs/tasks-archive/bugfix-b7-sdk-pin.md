# Archiv: Bugfix B7 SDK-Pinnung

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Bugfix B7 SDK-Pinnung (D86) (02.11.2026) – Branch `chore/sdk-pin`

**Warum:** Messlücke v0.1.0: das veröffentlichte ZIP bringt Runtime 10.0.14 mit (Release-Runner: SDK 10.0.403 über
`setup-dotnet` `10.0.x`), M6 maß das lokale ZIP mit 10.0.13 (SDK 10.0.402). Self-contained heißt, die Runtime des SDK
liegt im ZIP; ohne gepinntes SDK ist lokal ≠ CI. Kein Verhalten, Core unverändert.

### [x] B7 SDK pinnen (02.11.2026, abgenommen 02.11.)
- `global.json`: SDK 10.0.403, `"rollForward": "disable"`
- `ci.yml`, `release.yml`: `setup-dotnet` mit `global-json-file: global.json` statt `dotnet-version: 10.0.x`
- `dependabot.yml`: Ökosystem `dotnet-sdk`, damit Runtime-Patches als PR kommen
- Martin: lokales SDK auf 10.0.403
- D86, TASKS.md

**Präzisierung in der Planphase, ratifiziert durch Freigabe:**
- Dependabot `dotnet-sdk` `weekly` (Patch Tuesday ist monatlich, `monthly` kann einen Runtime-Fix vier Wochen liegen
  lassen), `version-update:semver-major` ignoriert (.NET 11 ist ein Stackwechsel und eine D-Nummer).
- `errorMessage` in `global.json`, ohne Versionsnummer (bleibt bei Dependabot-Bumps richtig).
- CI auf dem Branch über einen PR (`ci.yml` läuft nicht bei Branch-Push); CC merged lokal `--no-ff`, der Merge-Button auf
  GitHub bleibt unberührt, GitHub schließt den PR nach Martins Push.
- Kein `RemovePreviousVersion = never`: lautes Scheitern mit `errorMessage` ist gewollt, eine Registry-Einstellung ist
  nicht im Repo nachvollziehbar.
- `CONTRIBUTING.md` („Requires the .NET 10 SDK“ war mit `disable` falsch) und `CLAUDE.md` (Technik-Tabelle) mitgeändert;
  D49-Addendum verweist auf D86; Satz zur Messlücke auch unter T9.2.
- Schritt 0 belegt per `dotnet --list-sdks` (10.0.403, kein 10.0.402), sonst wäre der Negativtest keiner; steht
  10.0.402 noch da, läuft der Negativtest mit 10.0.401.

**Befund:**
- SDK des Release-Runners aus dem Log von „Release v0.1.0“ (Run 41218451178): `dotnet-sdk-10.0.403-win-x64.zip`,
  „Installed version is 10.0.403“. winget bot am 02.11.2026 als neueste `Microsoft.DotNet.SDK.10` ebenfalls 10.0.403.
- `winget install Microsoft.DotNet.SDK.10 --version 10.0.403` ließ 10.0.402 stehen (nach der Doku entfernt der Installer
  die Vorversion). Negativtest deshalb zweimal: vor der Installation mit 10.0.403 in `global.json` (nur 10.0.402
  installiert) und danach mit 10.0.401 (10.0.402 und 10.0.403 installiert, `patch` würde auf 10.0.403 rollen). Beide Male
  Exit 155 mit dem `errorMessage`-Text: SDK 10.0.403 wertet den Schlüssel aus.
- `dotnet build` 0 Warnungen, Tests Core 1.497, Import 141, App 1.533 (= `main`).
- `publish.ps1 -Version 0.0.0-dev`: `coreclr.dll` 10.0.1426, `wpfgfx_cor3.dll` 10.0.14, Build-Commit `0427b9a…` wie im
  CI-ZIP v0.1.0; Eintragsliste 360 Einträge, identisch mit dem CI-ZIP v0.1.0 (in T9.2 wich genau der `mscordaccore`-Name ab).
- `dependabot.yml` `ignore` (`dependency-name: "*"`, `update-types: ["version-update:semver-major"]`) gegen die
  Options-Referenz geprüft, keine Einschränkung auf Ökosysteme.

**Akzeptanz:**
- [x] `dotnet --version` im Repo = `10.0.403` = `global.json`; Negativtest mit fehlender Version schlägt an
- [x] `dotnet build` 0 Warnungen, `dotnet test` grün, Zahlen wie `main`
- [x] Lokales `publish.ps1 -Version 0.0.0-dev`: Runtime 10.0.14 im Ordner, Eintragsliste = CI-ZIP v0.1.0
- [x] CI auf dem PR grün (Windows und Ubuntu) mit SDK 10.0.403 (PR #20, Run 41389845142; Windows installiert 10.0.403,
  auf Ubuntu war 10.0.403 vorinstalliert und wurde mit `--version 10.0.403` angefordert)
- [x] Martins Abnahme (02.11.2026: „grün“), dann `git merge --no-ff` nach `main`

---

