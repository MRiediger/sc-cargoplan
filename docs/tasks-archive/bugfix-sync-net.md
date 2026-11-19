# Archiv: Bugfix Sync-Net (N1)

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Bugfix Sync-Net (D82) (29.10.2026) – Branch `fix/sync-safety-net`, eigener Merge nach Sync-Gone

### [x] N1 Auffangnetz Sync: ein Fehler kostet weder Plan noch App (D82) (abgenommen 29.10.2026)
**Warum:** Jede unerwartete Ausnahme im CT-Sync lief bis an den Dispatcher; das Overlay verschwand ohne Meldung, es gab
kein Log. Ein Fehler, der bei jedem Datei-Event wiederkehrt, konnte schleifen.

**Brief (Martin):** Catch nur im Timer-Tick-Handler, darunter wirft alles weiter; im Catch Plan unverändert, Notice mit
Typ und Meldung, Stacktrace ins Log; gleiche Meldung nur einmal bis zum nächsten Erfolg; nach 5 Fehlschlägen in Folge
Pause („Sync paused – check ContractTracker file“), Wiederaufnahme bei Änderung der CT-Datei oder manuellem Sync, Zähler
bei Erfolg auf 0; `DispatcherUnhandledException` nur loggen und Crash-Dialog, `Handled` bleibt false.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 der eine Catch sitzt in `RunSyncGuardedAsync`, das Tick
und Button/Hotkey aufrufen; P2 manueller Sync hebt die Pause nur bei Erfolg auf; P3 Log `%LocalAppData%\SC-CargoPlan\cargoplan.log`,
ab 1 MB nach `.log.1` (`File.Move` mit overwrite), `AppLog` wirft nie (Rückfall `Debug.WriteLine`); P4 `Saved`-Setter:
Szene bauen → Feld → `Scene` → `OnPropertyChanged(Saved)`, ohne `SetProperty`, Early-Return mit gleichem Vergleich (K1,
K2: kein Aufrufer setzt einen gleichen Plan zum Neuaufbau); P5 der ganze Tick-Körper im try, da
`ContractTrackerFileState.Of` werfen kann (K3); Pause-Stand = Dateistand vor dem fehlgeschlagenen Lesen, der Catch
fasst das Dateisystem nicht an; kein Timer-Neustart im finally (der Timer entprellt nur, Watcher-Events starten ihn);
P6 `_lastFailure = null` beim Pausieren; P7 `AppDomain.UnhandledException` nur loggen, ohne Doppeleintrag; P8
`SyncAsync` meldet, ob es gelesen oder nur eingereiht hat, ein eingereihter Aufruf zählt weder als Erfolg noch als
Fehlschlag; P9 Bug-Vorlage fragt nach `cargoplan.log`; Test für einen werfenden Szenenaufbau entfällt (kein Hook).

**Akzeptanz:**
- [x] Tick mit geworfener Ausnahme: Plan unverändert, App lebt, Notice, Log mit Stacktrace (Test; von Hand am Overlay)
- [x] Gleiche Meldung nur einmal bis zum nächsten Erfolg (Test)
- [x] Fünfter Fehlschlag pausiert, unveränderte Datei bleibt pausiert, CT schreibt → neuer Versuch (Test)
- [x] Erfolg setzt den Zähler zurück (Test)
- [x] Manueller Sync hebt die Pause nur bei Erfolg auf (Test)
- [x] Crash-Dialog: Meldung mit OK, App endet, genau ein Eintrag „Unhandled“ im Log (von Hand)
- [x] `AppLog`: ganzer Eintrag, Rotation, wirft nie (Tests); Build 0 Warnungen, Tests App 1.458 → 1.466, Core 1.492, Import 141

- Nicht von Hand geprüft: Pause und ihr Aufheben am Overlay (nur per Test).
- Commits cac558a (Doku), b91cc28 (Tests rot), 15be985 (Setter), 52f8514 (Netz, Log, Crash-Dialog).

