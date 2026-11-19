# Archiv: Release v0.2.0

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Release v0.2.0 (05.11.2026) – Branch `release/v0.2.0`

**Warum:** K9 soll vor der Nexus-Seite raus (Martin), die dauert noch: Der erste Upload ist damit gleich die aktuelle Version.

### [x] R1 Release v0.2.0 (05.11.2026, abgenommen 05.11.)

- Release-Notes `docs/release-notes/v0.2.0.md`; Nexus-Checkliste auf 0.2.0; Wiki „Applies to v0.2.0“
- Kein Versions-Bump: `release.yml` nimmt die Version aus dem Tag (D49)

**Befund (vor dem Tag):**
- Vollständigkeit `git log v0.1.0..main`, 37 Commits: K9 und der RAFT-Fix (7112a35) stehen unter „New since v0.1.0“, die Schema-Sprünge unter „Before you update“, Microsoft.Data.Sqlite 10.0.13 → 10.0.14 als „Under the hood“. Nicht in den Notes, weil ohne Wirkung aufs ZIP: SDK-Pin B7 (Runtime weiter 10.0.14 wie v0.1.0 aus der CI), Nexus-Texte T9.3, T9.2-Nachträge.
- K7 (3c4d39b) liegt vor dem Tag und steht schon in den v0.1.0-Notes. `src/CargoPlan.App/Settings` seit dem Tag unverändert: `settings.json` bleibt für 0.1.0 lesbar, keine Zeile nötig.
- „0.1.0 says so“ am v0.1.0-Stand im Code belegt: `PlanFile` liest bis Schema 8, `PlanStore.Load` meldet `Plan not loaded (…). The file stays until you make a new plan.`. Nur gelesen, nicht mit 0.1.0 ausgeführt.
- Holo- und Netzcode seit v0.1.0 unverändert; die Code-Links der Staff-Anfrage auf `fd75313` stimmen weiter.
- Keine Frame-Messung wie M6: K9 ändert am Rendern nichts.
- `publish.ps1 -Version 0.2.0` lokal: ZIP 62,1 MB, Runtime 10.0.14, `--version` `0.2.0+da54632…`. CI auf da54632 grün.

**Abnahme:**
- [x] Notes-Text von Martin korrigiert und freigegeben
- [x] Martin: `main` gepusht, CI grün auf 8c6aedc, Tag `v0.2.0` auf 8c6aedc gepusht, Wiki gepusht (Anker `#layout` antwortet)
- [x] Release-Workflow grün, kein Pre-release, „Latest“; ZIP 64.2 MB gegen `.sha256` OK, 360 Dateien, `--version` `0.2.0+8c6aedc…`, RAFT `-z`

