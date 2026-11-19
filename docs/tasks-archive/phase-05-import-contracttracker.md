# Archiv: Phase 5 – Import aus ContractTracker V2

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Phase 5 – Import aus ContractTracker V2 (vor dem Release, weil die meisten Hauler CT parallel laufen lassen und keine Missionsdateien schreiben)

Umnummeriert mit D37: der Import war Phase 6, der Release Phase 5. T5.1 und T6.1 sind neu.

### [x] T5.1 Exit-Button und feste Notices
**Warum:** Beenden geht heute nur über die Taskleiste. Notices wandern mit der Legendenposition (D33) und schieben oben die Ansicht; mit dem CT-Import kommen mehr davon.
**Präzisiert in der Planung:** Exit ruft `Close()` auf dem Overlay, denselben Weg wie die Taskleiste; die vorhandenen `Closed`-Handler (`OverlayBoundsSaver`, `SettingsSession.Flush`, `PlanSaver`) schreiben Ausstehendes, kein zweiter Speicherpfad, kein `Shutdown()`. Exit verhält sich wie das Zahnrad (`HeaderButtonStyle`, gesperrt `Hidden`), nicht wie das Schloss, das als eigenes Fenster immer sichtbar bleibt. Glyph E8BB ChromeClose, Tooltip „Exit“, ganz rechts in der Kopfzeile, Zahnrad links daneben. Notices werden gestapelt: eigenes Kind des äußeren DockPanels, unterste Zeile, volle Breite; eine untere Legende steht darüber, niemand weicht seitlich aus. Die README-Anleitung für ein unsichtbares Theme zeigt jetzt auf den zweiten Button von rechts. Präzisierung in der Planphase, ratifiziert durch Freigabe (Addenda zu D33 und D34).
- Exit-Button in der Kopfzeile zwischen Zahnrad und Schloss, Lock-Verhalten wie die anderen Header-Buttons (nicht fokussierbar, gesperrt ausgeblendet)
- Beenden ohne Dialog, schreibt Ausstehendes wie `overlay.Closed` (Settings, Plan)
- Notices immer fix unten links, unabhängig von `legend.position` (Addendum zu D33)

**Akzeptanz:** Haken setzen, sofort Exit → Neustart zeigt den Haken; gesperrt ist der Button nicht sichtbar; Legende `top-right` → Notice steht unten links; Core unverändert.

### [x] T5.2 Schema erkunden
**Warum:** Erst schauen, dann Code. ContractTracker ist closed source; ohne Fingerabdruck liest ein CT-Update still falsch.
**Präzisiert in der Planung:** Befund als D38 (Tabelle Frage → Befund → Konsequenz für T5.3/T5.4, nur Konzepte, keine Tabellen- oder Spaltennamen, damit D38 ein CT-Update übersteht) und in `docs/contracttracker-schema.md` (Pfad, Pragmas, Fingerabdruck, Mapping, offene Fragen; nur dort stehen Namen, nur diese Datei fällt bei einem Nein von Yuiki). Dazu die Fixture `tests/CargoPlan.Import.Tests/Fixtures/ct_schema.sql`, Testdaten, kein Produktcode. Zusätzlich beobachtet: Lebenszyklus F3/F4/F5, Löschen und Erledigen in der Hauptapp, Edit per Location-Mapping, Dateien bei laufendem und beendetem CT, Schreibmuster pro F3 (WAL-Commit-Frames). Werkzeug `sqlite3` per winget, kein NuGet. Lizenzfrage war schon gestellt, nur der Stand wird notiert. Präzisierung in der Planphase, ratifiziert durch Freigabe (D38).
- DB-Pfad finden (`%APPDATA%`/`%LOCALAPPDATA%`), in `docs/contracttracker-schema.md` notieren
- Kopie anlegen, an der Kopie `sqlite3 copy.db .schema` und `PRAGMA user_version`
- Schema-Fingerabdruck definieren: Tabellen + Spalten, die wir lesen
- Prüfen, ob die maximale Kistengröße in der DB steht (D23 nimmt das an)
- Mapping skizzieren: Spalten → `MissionStop.Destination`, `MissionItem.Commodity`, `Scu`, `MaxCrateSize` (D23), Pickup. Was fehlt?
- Lizenzfrage an Yuiki über den Nexus-Discord: dürfen wir die DB lesen und das dokumentieren?
- Doku minimal: nur die gelesenen Spalten

**Akzeptanz:** Doku vorhanden (Pfad, `user_version`, Fingerabdruck, Mapping, offene Fragen), Lizenzfrage gestellt und Stand notiert. Kein Code.

### [x] T5.3 Read-only-Reader und Rohtext-Parser
**Warum:** Ziele, Waren, SCU und max. Kistengröße automatisch, ohne die DB des laufenden Trackers zu stören. CT liefert Capture und OCR; die Zerlegung des Vertragstexts machen wir selbst (D38-Addendum: der Rohtext ist vollständig, CTs Parser verliert Ziele).
**Vor der Planung D38 + Addendum und D39 lesen.** Kein Auto-Sync in den Plan; der Reader liefert **Import-Kandidaten** für die Durchsicht im Editor (T5.4).
**Präzisiert in der Planung:** Gruppierung über lückenlose Ids statt gleicher Capture-Zeit (die zwei Zeilen einer Erfassung lagen 0,36 ms auseinander); der Parser liest nur zwischen `DETAILS` und `ACCEPT OFFER`, weil die Listenfragmente vor `DETAILS` stehen, nicht danach; Akzeptanz ergänzt. Präzisierung in der Planphase, ratifiziert durch Freigabe (D39).
**Präzisiert in der Planung (Umsetzung):** Normalisierung von Ware und Zielen an einer Stelle im Parser, auch für den Kindzeilen-Fallback; T5.4 vergleicht nur mit `OrdinalIgnoreCase` (D40). Ziel = Stationsname, abgeschnitten vor „ at …“/„ above …“; abgeschnittene Ziele (auch mitten im Wort, „to Thunde“) werden per Zeichen-Präfix aus der Drop-off-Liste vervollständigt, nur bei genau einem Treffer; ein übriges Fragment nimmt das eine längere Lieferziel desselben Captures, sonst bleibt es (bekannte Grenze, Capture 28 → „Thundering Express“); Pick-up-Liste und Prosa vervollständigen nicht (D40-Addendum). Anker `DETAILS`/`ACCEPT OFFER` unscharf (O/0, I/L/1), beide optional: die Accepted-Ansicht (37/38 beobachtet) endet mit `TRACK`/`ABANDON`, `DETAILS` steht in beiden (schema.md, „Raw text layouts“). Fingerabdruck um Capture-Zeit und Rohtext erweitert; `Pooling=False`, Busy-Retry 5 s über das Command-Timeout von Microsoft.Data.Sqlite; fehlende Datei → nichts gelesen, keine Notice (der Hinweis gehört T5.5). Ein Capture mit mehreren Waren ist ein Kandidat mit mehreren Items. CLI-Befehl `contracts <db> [--since <id>] [--raw]` für den echten Lauf und als Bug-Report-Weg (`--raw` hängt den Rohtext an). Präzisierung in der Planphase, ratifiziert durch Freigabe (D40).
- `CargoPlan.Import`: Reader liest Vertragszeilen mit Id, Capture-Zeit, Rohtext und Kindzeilen; Fingerabdruck (schema.md) vor dem Lesen prüfen, Abweichung → nichts lesen, Notice „ContractTracker schema not supported – enter missions in the editor or use Import…“ (der Text zitiert den Editor-Knopf, nicht umgekehrt)
- Connection-String `Mode=ReadOnly`, Tracker schreibt parallel (WAL); Lock → Retry, keine Notice. Eigener Lesezugriff legt `-wal`/`-shm` an (D38) – dokumentiert, kein Aufräumen
- **Gruppierung:** Zeilen mit byteweise gleichem Rohtext und lückenlos aufeinanderfolgenden CT-Ids = eine Erfassung = ein Kandidat (D38: eine Transaktion pro Capture); Schlüssel des Kandidaten sind seine CT-Ids (AUTOINCREMENT, nie wiederverwendet). Kein Inhaltsschlüssel – Pickup ist unbrauchbar, gleiche Inhalte sind nicht unterscheidbar (D38)
- **Parser auf den Rohtext:** Ware, Ziele mit SCU aus den „Deliver … to …“-Zeilen und der Drop-off-Liste; tolerant gegen OCR-Fehler (`Dellver`, `O/3`, `LS` für `L5`); liest nur zwischen `DETAILS` und `ACCEPT OFFER`: Listenfragmente (`· Stims (3 SCU)`, abgeschnittene Zeilen) stehen davor im Kopfblock, danach nur Ziffernrauschen (D38-Addendum). Kindzeilen nur als Fallback, wenn der Parser kein Ziel findet
- **Max. Kistengröße** aus zwei beobachteten Phrasen („Max size will be N SCU.“, „none of the containers are bigger than N SCU.“, Zeilenumbruch innerhalb erlaubt); fehlt sie → Default größte Kiste, die in die SCU passt, Kandidat trägt `crateSizeAssumed = true`
- **Kandidat** (`ContractTrackerCandidate`, nur in `.Import`): Mission in Eingabetypen (`MissionStop`/`MissionItem`, D23) + CT-Ids + Capture-Zeit + Hinweise für die Durchsicht: `crateSizeAssumed`, `usedFallbackRows`, Zielanzahl laut Parser vs. laut Drop-off-Liste (Abweichung = unvollständig)
- `ReadSince(lastId)`: nur Zeilen mit Id > Wasserstand; der Wasserstand selbst gehört zum Plan (T5.4)
- Tests: Fixture-DB `ct_schema.sql` (nur gelesene Spalten) + Rohtext-Fixtures aus den echten Captures (anonymisiert, ohne Datum), darunter absichtlich: OCR-Fehler, Listenfragmente, 2+1-Split, fehlende Größenphrase, Fallback-Fall. Parser ohne DB testbar
- Offen (schema.md): deutscher Spiel-Client nicht beobachtet; ob CT Mappings beim nächsten Capture anwendet

**Akzeptanz:** Tests grün: Fingerabdruck-Abweichung → nichts gelesen + Notice; 2+1-Split → ein Kandidat mit zwei Ids; 1-Ziel-Capture mit `Dellver`/`O0/5` → drei Ziele; Listenfragmente vor `DETAILS` ergeben keine Ware; beide Größenphrasen erkannt, fehlende → `crateSizeAssumed`; kein Parser-Ziel → Kindzeilen + `usedFallbackRows`; `ReadSince` liefert nur Ids > Wasserstand; Parser-Tests ohne DB; Accepted-Capture 37/38 mit Umbruch mitten im Wort → „Thundering Express Station“ (Namen vollständig). Echter Lauf gegen eine Kopie von Martins DB liefert plausible Kandidaten.

### [x] T5.3b Parser-Härtung aus dem Spieltest
**Warum:** Spieltest 2026-10-21: 47 „to Everus Harbor aboy“ und 48 „… abov“ ergeben Zielnamen mit abgerissenem „above“; „Max size willb 1 SCU“ ergibt `?`; 47 und 48 sind zwei echte Verträge (Müll nach Everus Harbor, andere Belohnung), aber `⚠ duplicate?` – bei Einzelziel-Verträgen zum selben Ort mit gleicher Ware (Covalex-Alltag) ist die Teilmengen-Regel ohne Belohnung immer falsch-positiv; unser Overlay stand in CTs Capture-Region und steht im Rohtext.
**Präzisiert in der Planung:** Größenphrase erlaubt bis zu zwei Wörter zwischen „size“ und Zahl (das Muster mit einem Wort trifft „will be“ nicht); Ziffernkorrektur O→0, I/l/|→1 nur in der Zahl der Größenphrase und nur, wenn eine gültige Kistengröße herauskommt (in der Lieferzeile gibt es keine); Schlusswort = Zeichen-Präfix von „above“/„at“ ab zwei Zeichen oder, ab vier Zeichen, ein Zeichen Abweichung von „abov“/„above“, danach gilt der Name als abgeschlossen; Prosa-Name endet vor „ at“, „ above“ oder „.“, Umbrüche erlaubt; der bestehende Test „Pick-up-Liste und Prosa vervollständigen nicht“ wird aufgeteilt, weil „taken to …“ jetzt vervollständigt; Belohnung nur, wenn ihre Zeile ganz Zahl ist (OCR „5O,250“ → unbekannt, nicht 5); die Belohnung wird im Plan gespeichert (`ContractSourceDto`, optional, Schema bleibt 3), sonst wären 47/48 nach dem Neustart wieder markiert. Präzisierung in der Planphase, ratifiziert durch Freigabe (D39/D40-Addenda).
- **Abgerissenes Schlusswort:** Lieferzeile ohne Abschluss, die auf ein Präfix von „above“/„at“ oder eine Zeichenabweichung davon endet (`abov`, `aboy`, `ab`) → Wort entfernt
- **Größenphrase worttolerant:** `Max size <Wort(e)> N SCU`, `bigger than N SCU`, mit Ziffernkorrektur
- **Vervollständigung aus der Prosa,** nur mit „to“: „to a freight elevator at <Name>“ liefert Zielnamen für Zeichen-Präfix-Treffer; „from a freight elevator at …“ nie (Pickup); Verb egal. Reihenfolge: Drop-off-Liste → Prosa → Fragment-Merge (D40-Addendum)
- **Belohnung** aus „Reward“ am Textanfang → `ParsedContract.Reward` → Kandidat → `ContractSource.Reward` (Core, ein Feld). Duplikat = Teilmenge **und** Belohnung gleich oder mindestens eine unbekannt (D39-Addendum)
- **Overlay-Text im Rohtext:** README-Satz im Abschnitt ContractTracker, Overlay außerhalb von CTs Capture-Region (F6)
- Fixtures aus den echten Rohtexten (anonymisiert, ohne Datum): Id 47, Id 48, Silicon-Capture mit Overlay-Text
- Nachtrag: CLI `contracts` hängt ` · reward 50,250` an die Kopfzeile, wenn die Belohnung bekannt ist; sie entscheidet über Duplikate, ein Bug-Report braucht sie

**Akzeptanz:** Tests grün: 47 → „Everus Harbor“, max. 1 SCU; 48 → „Everus Harbor“, Größe weiter `?`; Silicon → genau ein Ziel mit 7 SCU, max. 1 SCU, keine Overlay-Zeile als Ware oder Ziel; „to“-Prosa vervollständigt, „from“-Prosa nicht; 47 vs. 48 nicht markiert, Retry mit gleicher Belohnung markiert; Belohnung übersteht Speichern und Laden. Echter Lauf gegen eine Kopie von Martins DB zeigt 47/48 als „Everus Harbor“. Core-Änderung nur Belohnung (Feld + Vergleich).

### [x] T5.4a Stapelregel nach Zielreihenfolge
**Warum:** Eine Kiste, auf der entlang der Stapelachse eine andere liegt, lässt sich im Spiel nicht entnehmen (Martin, Spike T5.4). `CrateLayout.Place` legt nach Größe, nicht nach Route: Im vollen Hull A (A 20, B 12, C 16, D 16 SCU, max. 4) liegt B in Rack 2 auf A, das zuerst abgeladen wird. Mit A in 2er-Kisten liegt A auf B: das ist das Soll, die obere Kiste geht zuerst runter. Der Plan ist heute schon falsch, mit oder ohne CT – **Release-Blocker**. Vor T5.4, weil `PlaceInto` (D41) `CrateLayout.Place` nutzt und die Regel erbt: eine Regel, eine Stelle.
**Präzisiert in der Planung:** Regel weich – Verstoß = Befund, nie Abbruch (Spike: ab drei Stops in einem Rack gibt es Mischungen ohne regelkonforme Anordnung, z. B. 8/4/1 SCU); `Place` legt wie bisher, nur bei Verstoß sucht es im Rack exakt nach einer Anordnung in Routenreihenfolge, sonst bleibt das erste Layout. Zwei Befundklassen: blockierend (Platz, außerhalb, Überlappung, Stütze) lehnt Moves ab, Reihenfolge wird nur angezeigt – Drag und `MoveStop` legen und markieren. Reihenfolge = `LoadPlan.Route` (erstes Auftreten in den Kisten, wie Legende/Palette), kein Formatwechsel. `Partition` unverändert (formbewusst → Backlog). Gespeicherte Pläne mit falscher Stapelung: rot, keine Neuanordnung. Abgehakte Kisten freigeben → T5.4. Exhaustive-Test (`Category=Exhaustive`, Brute-Force-Referenz) nicht im Standardlauf (`default.runsettings`), einmal in der DoD. Präzisierung in der Planphase, ratifiziert durch Freigabe (D24-Addendum).
- **Regel (D24-Addendum):** Auf einer Kiste darf nur eine Kiste desselben oder eines früheren Stops liegen, nie ein späterer Stop auf einem früheren. Reihenfolge = Stop-Reihenfolge des Plans (Legende/Route). Eine Regel in `CrateLayout`, genutzt von `Place` und den Prüfungen
- Gilt für `Plan`, `MoveStop`, Drag (D25) und `Validate`; Verstoß → Kiste rot, Befund in der Legende
- Bestehende Planer-Tests mit geteilten Racks prüfen: Layouts ändern sich, wo heute ein späterer Stop auf einem früheren liegt

**Akzeptanz:** Tests grün: A 20 / B 12 / C 16 / D 16 in 4ern → A liegt außen auf B, nie B auf A; vier Stops à 12 (4er) + 16 SCU in 8ern → geht auf, weil frühere Stops auf späteren liegen dürfen; Drag einer Kiste eines späteren Stops auf eine eines früheren → gelegt, Kiste rot, Befund in der Legende; bestehende Planer-Tests grün oder bewusst angepasst und begründet. Im Overlay zeigt der erste Plan Rack 2 mit A außen auf B.

### [x] T5.4 Auto-Apply mit Markierungen + inkrementell platzieren
**Warum:** Hauler drücken F3 am Terminal und schauen aufs Overlay, den Editor öffnen sie nur bei Fehlern (D39-Addendum). CT-Importe sind je Capture lückenhaft (D38), deshalb sichtbar markieren statt still übernehmen. Hauler nehmen Aufträge nacheinander an; neu planen löscht Positionen und Haken (T3.8b).
**Präzisiert in der Planung:** Neu gefasst nach dem D39-Addendum (Revision des Prüforts von D39, ratifiziert durch Freigabe): Markierung in der Legende statt Pflichtschritt im Editor; jede Erfassung eine eigene Mission, Duplikate nur markiert; Entfernen einer Mission bewegt keine anderen Kisten. Präzisierung in der Planphase, ratifiziert durch Freigabe.
**Präzisiert in der Planung (Umsetzung):** Zwei Commits, T5.4b Core + Import (ohne WPF testbar), T5.4c App.
- **Erster Sync:** Wasserstand 0 → alles aus CT, auch bei migriertem Schema-2-Plan.
- **Unplatzierte Missionen:** Retry bei jedem Sync, nicht nach einem Haken.
- **Duplikat:** Teilmenge der (Ziel, Ware)-Paare statt Überlappung (Beleg 36 vs. 37/38), abgeleitet statt gespeichert; kein Marker für den Kindzeilen-Fallback.
- **Abgehakte Kisten:** grau bis überbaut, kein Drop-Ziel; Haken unter einer liegenden Kiste → rot, nicht abgelehnt.
- **Schema 3:** Stops unter der Mission statt Id am Item, Stop-Sicht abgeleitet.
- **Reihenfolge:** Legende und Stapelregel nach `UnloadOrder` (erste Kiste an Bord), `Route` nur für die Farben; Korrektur bei der Freigabe.
- **„In CT gelöscht“** = alle Ids der Erfassung fehlen; „nicht gelesen“ ist nie „gelöscht“.
- **Sync pausiert** bei kaputtem plan.json.
- **Fehlerfälle:** Fehlende DB ohne Notice, Datei ohne SQLite-DB mit Notice.
- **Delete mission:** Statuszeile „plan updated“.
- **README:** „retry before you load“.

Präzisierung in der Planphase, ratifiziert durch Freigabe (D24/D39/D41/D42-Addenda).
- `FileSystemWatcher` auf DB + `-wal` übernimmt Kandidaten seit dem Wasserstand **direkt** in den Plan; **Wasserstand = höchste übernommene Id**, steigt bei Übernahme, gehört zum Plan. „Diff, don't react“ (D38) bleibt: Arbeit nur, wenn Zeilen über dem Wasserstand existieren oder eine Plan-Id in CT fehlt; Mapping-Write, Checkpoint und unser eigener Lesezugriff (`-wal`/`-shm`) ergeben nichts
- **Legende** zeigt die Hinweise pro Stop: Größe angenommen (`4 SCU?`), Ziele unvollständig (`⚠ 1 of 3 destinations`), Fallback auf Kindzeilen, `⚠ duplicate?`
- **Jede Erfassung eine eigene Mission, nichts wird ersetzt:** gleiche Ware + überlappende Ziele mit einer Mission ohne Haken → `⚠ duplicate?`, keine Notice; verschwindet beim ersten Haken auf einer der beiden. Retry nach OCR-Fehler → Spieler löscht die falsche Mission im Editor
- **In CT gelöscht:** alle Kisten der Mission abgehakt → Mission raus; sonst Notice, Plan bleibt. Dafür prüft der Reader, welche Plan-Ids in CT noch existieren (Erweiterung hier, T5.3 bleibt wie formuliert)
- Übernommene Missionen behalten ihre **CT-Ids im Plan** (nötig für die Löschnotiz). Plan-Format prüfen; ändert es sich, eigener Eintrag in decisions.md
- Einzige Core-Änderung der Phase, erste seit T3.15: Mission in den bestehenden Plan legen, ohne andere zu verschieben; Positionen und Haken bleiben. Spike erledigt: D41 (inkrementell platzieren), D42 (Missions-Identität, Schema 3); setzt T5.4a voraus. Hier, nicht in T5.4a: abgehakte Kisten geben ihre Zellen frei und tragen nicht mehr; Zurückhaken bei neu belegten Zellen abgelehnt mit Grund (D24-Addendum)
- **Mission entfernen** (in CT gelöscht, im Editor gelöscht) nimmt nur ihre Kisten heraus, andere bewegen sich nicht, Haken bleiben
- Knopf „Sync from ContractTracker“ im Editor neben „Import…“ bleibt als manueller Weg: übernimmt Kandidaten seit dem Wasserstand wie der Watcher. „Sync“, nicht „Load“: „Import…“ daneben ersetzt den Plan, diese Aktion legt inkrementell nach
- Entfällt (D39): Reset-Erkennung, Multimenge, Debounce-Logik auf Inhalt; „verschwindet = erfüllt“ nur in der engen Form oben

**Akzeptanz:** Tests grün (inkrementell ohne Bewegung liegender Kisten, Haken bleiben, Wasserstand steigt bei Übernahme, Dateievent ohne neue Zeilen → nichts; Duplikat-Markierung nur ohne Haken; Entfernen bewegt keine anderen Kisten; Entfernen nach CT-Löschung nur bei vollständig abgehakt); im Spiel: zweiten Auftrag erfassen → ohne Klick im Overlay, Kisten und Haken des ersten unverändert; Capture ohne Größenphrase → `?` in der Legende; F4 vor dem ersten Haken → zweite Mission mit `⚠ duplicate?`, erste unverändert, keine Notice; F4 nach dem ersten Haken → beide im Plan, keine Markierung; falsche Mission im Editor löschen → weg, übrige Kisten und Haken unverändert; in CT löschen bei vollständig abgehakt → Mission weg; bei offenen Kisten → Notice, Plan bleibt.

### [x] T5.5 Settings-Abschnitt „ContractTracker“ (erledigt, Häkchen nachgetragen in K10; Commits b67aa54, b2b33e2)
**Warum:** Nicht jede Installation liegt am Standardpfad, nicht jeder will Erfassungen ohne Zutun im Plan, und wer CT nicht hat, soll erfahren, woher.
**Präzisiert in der Planung:** Schalter wieder „Auto-sync“ an/aus (D39-Addendum: Übernahme ohne Pflichtschritt im Editor); der Hotkey übernimmt direkt, statt den Editor zu öffnen; Abschnitt auch ohne CT sichtbar. Präzisierung in der Planphase, ratifiziert durch Freigabe.
**Präzisiert in der Planung (Umsetzung):** Schema bleibt 2 (optionaler Abschnitt `contractTracker` mit `path`/`autoSync`, `hotkeys.syncContractTracker`); Pfad, der kein voller Pfad ist → Autodetektion + Notice, fehlende Datei bleibt stehen. Pfad nur als Anzeige mit „Browse…“ und „Detect automatically“; Status beim Öffnen und bei Änderung geprüft, nicht live. Auto-sync aus ändert den Plan nie (auch nicht nach CT-Löschung), die Notice zählt Erfassungen; wieder an → Sync nach dem Debounce. Fehlende DB → Notice nur bei Knopf/Hotkey, der Watcher bleibt stumm. Hotkey-Doppelprüfung über alle drei Aktionen. **Neue DB** (Neuinstallation, Update legt die Datei neu an, anderer Pfad): höchste je vergebene Id (`sqlite_sequence`) < Wasserstand → ab 0 lesen, Wasserstand zurück, Notice „ContractTracker database looks new – all contracts imported“; nicht `MAX(Id)`, das beim Löschen des neuesten Vertrags sinkt und alle älteren doppelt importieren würde. Präzisierung in der Planphase, ratifiziert durch Freigabe (D44).
- Abschnitt „ContractTracker“ im Settings-Fenster: Pfad (Autodetektion + manuell), „Auto-sync“ an/aus. Aus → Watcher zeigt nur Notice „N new contracts“, Übernahme per Knopf oder Hotkey
- CT nicht gefunden → Abschnitt bleibt sichtbar mit Hinweis „ContractTracker not found – set path or install“ und Link zur Nexus-Seite von ContractTracker (URL bei der Umsetzung prüfen, A3)
- „Sync from ContractTracker“ im Editor (aus T5.4) bleibt der manuelle Weg, auch bei Auto-sync aus
- Optionaler Hotkey „Sync from ContractTracker“ im Hotkeys-Abschnitt, Default `null` (keine Default-Hotkeys, D17): übernimmt direkt wie der Knopf, öffnet nicht den Editor
- **Neue DB:** Ids beginnen wieder bei 1 → ohne Guard nie wieder ein Import, still. Reader erkennt, Core setzt den Wasserstand zurück

**Akzeptanz:** Autodetektion findet Martins DB; manueller Pfad wirkt ohne Neustart und überlebt ihn; Auto-sync aus → neuer CT-Vertrag erzeugt nur die Notice „N new contracts“ und steht nicht im Plan, Knopf oder Hotkey übernimmt ihn; Hotkey gesetzt → übernimmt ohne Editor, ohne Eintrag kein Hotkey; CT fehlt oder Pfad ungültig → Abschnitt sichtbar mit Hinweis und Nexus-Link. Tests grün: Plan mit Wasserstand 57, DB mit Ids 1–3 → drei Kandidaten, Wasserstand 3; neuesten Vertrag in CT löschen → kein Neuimport.

---

