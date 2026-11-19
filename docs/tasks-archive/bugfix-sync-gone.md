# Archiv: Bugfix Sync-Gone (S1)

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Bugfix Sync-Gone (D81) (29.10.2026) – Branch `fix/sync-gone-keeps-plan`, Blocker für v0.1.0

### [x] S1 Verschwundene CT-Verträge bleiben im Plan (D81, Plan-Schema 8) (abgenommen 29.10.2026)
**Warum (Brief von Claude.ai als Reviewer):** Der Sync behandelte einen in CT verschwundenen Vertrag als erledigt und nahm
ihn aus dem Plan. CT verliert Verträge aus vielen Gründen (OCR, Neustart, Löschen in CT, im Spiel abgebrochen mit Fracht
an Bord); Datenverlust im Plan darf nie die Folge eines fremden Tools sein.

**Ist-Zustand vor S1 (A1, zwei Explore-Berichte), drei Punkte weichen vom Brief ab:** (1) Entfernt wurde nur, wenn nichts
geplant an Bord war: ganz geladen (Kisten weg, darüber Absinken), ganz abgeliefert oder wartend (für immer weg, der
Wasserstand stand schon darüber); mit geplanten Kisten blieb die Mission mit der Notice „Deleted in ContractTracker, crates
still on board“. (2) D80 war vergeben (K7), der Eintrag ist D81. (3) Einen Schutz „alle gleichzeitig weg“ gab es nicht
(D39 hatte ihn gestrichen), eine neue Datenbank machte jede Aufnahme zu gone; einen Inhaltsschlüssel aus T5.4 gibt es
nicht (D38/D42), F4 ist eine neue Zeile mit neuer Id.

**Präzisierung in der Planphase, ratifiziert durch Freigabe:** P1 Marke am Vertrag `ContractSource.Gone`
(`missions[].contract.gone`), damit Editor-Plan, Umbenennen und Schiffswechsel sie ohne eigenes Feld tragen; P2 Plan-Schema
7 → 8, `gone` nur bei `true`, in Schema 7 ein Fehler, Goldens nur `schemaVersion`; P3 `remove` in zwei Stufen (Martin:
Abgeliefertes darf gehen; Korrektur **K1** bei der Freigabe): beim Verschwinden gehen geplante und abgelieferte
Lieferungen, geladene bleiben mit `gone`, eine wartende Mission geht ganz; später geht eine gone-Mission nur ganz und nur,
wenn nichts mehr geplant oder geladen ist, nie rückwirkend Geplantes (Reset-Schutz und Umschalten keep → remove halten);
P3b (Korrektur **K2** bei der Freigabe) eine neue Datenbank hängt die alten Ids ab (`ContractSource.Detached`, nur mit
`gone`): angezeigt, nie mehr verglichen, nicht in `KnownIds`, eigener Lieferschlüssel in `PlanKeeping`; P4 Rückkehr nur
über dieselbe Id (Martin), F4 bleibt Duplikat; P5 wartende gone-Missionen werden nicht neu platziert (Martin), der
Editor-Plan legt sie; P6 Schutz neu gebaut: alle gelisteten Verträge in einem Read weg (mindestens zwei) oder neue
Datenbank → nichts entfernt; P7 die Konflikt-Notice entfällt, Ersatz `· gone` in Legende und Editor plus einmalige
Sync-Notice; P8 Notices `Contract #47 gone from ContractTracker – kept in plan`, `… – removed 2 planned, 1 delivered,
kept 1 loaded`, `… – removed, it was waiting`, `Contract #47 back in ContractTracker`, `All contracts gone from
ContractTracker at once – kept in plan`; P9 Reset im Abschnitt setzt Auto-sync und gone zurück, der Pfad behält „Detect
automatically“; P10 Branch ab main f3f9996, `docs/t75c-followup` unberührt.

**Beim Bauen hinzugekommen, ratifiziert durch Abnahme (Martin, 29.10.2026):** E1 eine neue CT-Datenbank leert die
Undo-Historie (`Undo history cleared by new ContractTracker database`): die Schnappschüsse tragen die alten Ids nicht
abgehängt, ein Resync würde sie mit den neuen verwechseln (wie D74 beim Schiffswechsel); E2 `ContractSync.Quiet` ersetzt
die frühe Rückkehr „alle bekannten Ids da“, die eine Rückkehr nicht erkannte; folglich geht eine gone-Mission, die man
unter `remove` später abhakt, erst beim nächsten Sync mit Neuigkeit oder per Button (D43: ein Read ohne Neues ändert
nichts); E3 der Sync liefert einen Bericht (`ContractSync.Sync` → `SyncResult`, `GoneReport`) statt eines nachträglichen
Vergleichs `Changes(before, after)`; E4 das Label steht über der ComboBox (die 110-px-Zeile hätte es vierzeilig
umgebrochen); E5 „Known issues“ gibt es nur im Wiki (Troubleshooting), dort ergänzt, README unverändert.
- Core: `ContractSource.Gone`/`Detached`/`AsDetached`, `ContractSourceDto`, `PlanFile.SupportedSchemaVersion = 8`,
  `NotGoneSchemaVersion`, `PlanFileDto.CheckGone`, `ContractGonePolicy`, `SyncResult`, `GoneReport`, `ContractSync.Sync`
  (Apply = Sync.Saved), `Resync(…, onGone)`, `Quiet`, `Conflicts` entfernt, `LoadPlanner.RemoveDeliveries` (nutzt `Without`),
  `RetryUnplaced` ohne gone, `DeliveryKey` für abgehängte negativ
- App: `OverlayViewModel.OnContractGone`, `GoneText`, `AllGoneAtOnceText`, `HistoryClearedByDatabaseText`, `ClearHistory`,
  `KnownIds` ohne abgehängte; `NoticeSource.DeletedContracts` und `UpdateDeletedContracts` entfernt; `LegendEntry.Gone`
  (festes Segment ` · gone` nach der Id), `UnplacedEntry` mit `gone`, `MissionText.GoneText`, `MissionRow.Gone`/`GoneMark`;
  `ContractTrackerSettings.OnContractGone`/`GonePolicy`, `ContractGonePolicies`, `AppearanceRules.NormalizeContractTracker`,
  `ContractGoneChoice`, `ContractTrackerSectionViewModel.OnContractGone`/`GoneChoices`/`ResetCommand`, XAML, `App.xaml.cs`
- Tests: `ContractGoneTests` (Core, 23), `GoneOverlayTests` (App, 14), `SyncTimingTests` (Timing); umgestellt:
  `ContractSyncTests`, `LoadedStateTests`, `UndoRestoreTests`, `ReplanInPlaceTests`, Schema-Zahlen, `OverlayViewModelTests`,
  `ManualRowsTests`, `LoadedOverlayTests`; neu in `LegendLineTests`, `SettingsStoreTests`,
  `ContractTrackerSectionViewModelTests`, `SettingsWindowXamlTests`
- Übergang: Auf dem Core-Commit 9eca83f scheitern drei alte App-Tests (altes Entfernen), bis 1469c81 sie umstellt; gebaut
  hat jeder Commit.

**Zahl vorher/nachher (nur Sync-Pfad, Release, Median aus 5, `SyncTimingTests` auf dem Hurston-Plan vom 25.10., Hull A):**
Watcher-Read ohne Neues 0,01 → 0,00 ms, Button ohne Neues 0,05 → 0,05, zwei weg 0,07 → 1,22 (jetzt markiert: neue
Szene), zwei weg und alles abgeliefert 0,55 → 0,68; neu: remove 0,84, remove alles abgeliefert 0,54, Watcher-Read mit zwei
schon gone 0,00, zwei zurück 0,73; weit unter 16 ms (K2.4). Goldens nur `schemaVersion`, Slow grün. Tests Core
1.467 → 1.492, App 1.435 → 1.458, Import 141.

**Nebenbefund (nicht behoben):** `ContractTrackerSettings.DatabasePath` (berechnet) wird in settings.json mitgeschrieben
(`"databasePath": …`), beim Lesen ignoriert; das neue `GonePolicy` trägt `[JsonIgnore]`.

**Akzeptanz:**
- [x] keep: verschwundener Vertrag bleibt byte-gleich bis auf `gone`; Kisten, Zustände, Positionen, Haken unverändert
- [x] remove: geplant und abgeliefert weg, geladen bleibt mit Flag, gemischte Mission nur das Ungeladene weg; später nur
  ganz und ohne Geplantes/Geladenes (K1); wartend weg
- [x] Rückkehr entfernt das Flag, idempotent; alle auf einmal / neue Datenbank entfernen nichts, auch der zweite Sync nicht
- [x] Neue Datenbank: alte Ids abgehängt, neuer Vertrag #1 ist neu (K2); Undo-Historie geleert (E1, ratifiziert)
- [x] Schema 8 mit Migration 7 → 8 (Golden), `gone` in 7 und `detached` ohne `gone` abgelehnt
- [x] Undo nach markierendem Sync: Flag wieder da; Sync bleibt kein Schritt
- [x] Legende `· gone` fest nach der Id, Editor `#47 · gone`, Sortierung unverändert, Editor-Plan behält die Marke
- [x] `contractTracker.onContractGone` in settings.json, Settings-Fenster und Reset; unbekannter Wert → keep mit Problemzeile
- [x] `dotnet build` ohne Warnungen; `dotnet test` grün; Zahl vorher/nachher
- [x] Look-Gate (Martin, 29.10.): `· gone` in der Kopfzeile bei 420 px; Zeile und Reset im Abschnitt ContractTracker
- [x] Gate (Martin, mit ContractTracker, 29.10.; Absturz bei `remove` gefunden und behoben, s. u.): Vertrag annehmen → Sync → laden → in CT löschen → bleibt mit `· gone`, Kisten unverändert;
  mit remove: abgeben → abhaken → Sync → weg
- Gate-Befund (Martin, 29.10.): `remove` mit geladenen Stopps 2 und 3 stürzte ab (Kisten behielten die Item-Indizes der
  gestrichenen Stopps, D68); `RemoveDeliveries` nummeriert sie neu, Regressionstest. Tests Core 1.491 → 1.492.
- Commits 1e264df (Tests rot Core), 9eca83f (Core), a406540 (Tests rot App), 1469c81 (App), af83a2f (Label), 9acfc6b
  (Doku), 8d64da5 (Test rot Absturz), 2b24418 (Fix); Wiki d486ad5.
- Offen (nicht Teil von S1): einmalige Notice bei teilweise gelöschten CT-Zeilen einer Aufnahme; ~~Auffangnetz, damit ein
  Fehler im Sync die App nicht beendet~~ (erledigt: Sync-Net, D82); Aufnahmen D3/D4 (Abschnitt ContractTracker) neu.

