# Archiv: Phase 6 – Release

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Phase 6 – Release

### [x] T6.0 Erster Start (Spike)
**Warum:** Wer nach dem Download ein leeres Overlay sieht und nicht weiterweiß, schließt es wieder. Vor dem Release prüfen, was ein Hauler ohne README vorfindet.
- Drei Zustände durchspielen: (1) kein Plan, kein CT; (2) kein Plan, CT vorhanden; (3) Plan vorhanden, CT-DB gelöscht
- Screenshots je Zustand in `docs/screenshots/first-start/`
- Ergebnis: Liste „Blocker / Backlog“ (Blocker → Task vor dem Release, Rest → Backlog)
- Kein Code

**Akzeptanz:** Ein Hauler kommt ohne README vom leeren Overlay zu Editor und Settings; Screenshots und Liste liegen vor.

**Befund:** Zustände A/B/C ohne Blocker, Leerzustand und Settings selbsterklärend, Auto-Detect funktioniert, Sync ~1 s. Screenshots in `docs/screenshots/first-start/`.

### [x] T6.1 settings.json sichern
**Warum:** Release-Vorbedingung. Das erste Speichern überschreibt eine kaputte oder von Hand geänderte settings.json (D34), ohne Weg zurück.
- `settings.json` einmal pro Prozesslaufzeit, vor dem ersten Schreiben, als `settings.json.bak` sichern

**Präzisiert in der Planung:** Flag „already backed up“ in `SettingsSession` (einziger Besitzer pro Lauf, D34), nicht statisch; die Kopie macht `SettingsStore.BackUp` (`File.Copy`, overwrite, byte-gleich auch bei Nicht-JSON). Keine settings.json → kein Backup, Flag trotzdem gesetzt. Flag erst nach erfolgreicher Kopie: scheitert sie, wird nicht geschrieben und der nächste Save versucht es erneut. Lade-Hinweis bei kaputter Datei: „The next save overwrites {path}, keeping the old file as settings.json.bak.“; README und settings-format.md nennen die `.bak`. Präzisierung in der Planphase, ratifiziert durch Freigabe (D47).

**Akzeptanz:** settings.json von Hand ändern (auch kaputt), Overlay bewegen → `settings.json.bak` enthält den alten Inhalt byte-gleich; weiteres Speichern im selben Lauf ändert `settings.json.bak` nicht; Core unverändert.

### [x] T6.2 Publish-Profil
**Warum:** Entpacken, Doppelklick, läuft. Sonst nutzt es niemand.
- Ein Profil (D46): `src/CargoPlan.App/Properties/PublishProfiles/win-x64-selfcontained.pubxml`, `SelfContained=true`, `RuntimeIdentifier=win-x64`; kein `PublishSingleFile`, kein `IncludeNativeLibrariesForSelfExtract`, kein zweites Profil
- App und Cli in denselben Publish-Ordner, daraus ein ZIP
- Version sichtbar in der Fußzeile des Settings-Fensters und per `CargoPlan.Cli --version`, beide aus `AssemblyInformationalVersion`
- App-Icon, Assembly-Metadaten (Version, Author, Repo-URL)

**Präzisiert in der Planung:** Statt `win-x64-selfcontained.pubxml` ist `build/publish.ps1` das einzige Profil: ein pubxml gilt pro Projekt, zwei Projekte brauchen ohnehin zwei Aufrufe; Cli zuerst, App zuletzt (zwei gleichnamige Dateien unterscheiden sich, die der App müssen gewinnen), Ordner vorher leeren, `LICENSE` hinein, PDBs bleiben drin. `Directory.Build.props`: `Version` 0.0.0-dev, `Authors`/`Company` MRiediger wie LICENSE, `Product`, `Copyright`, `RepositoryUrl`; kein `PackageLicenseExpression` (wirkt nur bei `dotnet pack`). Version mit vollem Commit-Hash: Fußzeile „SC-CargoPlan 0.0.0-dev+<sha>“ außerhalb des Scrollbereichs, markierbar, Trenner oben, keine zweite Schriftgröße; `--version` gibt nur die Version aus. App-Icon in den Backlog (keine `.ico` im Repo). ZIP-Wurzelordner entscheidet T6.3. Präzisierung in der Planphase, ratifiziert durch Freigabe (D48).

**Akzeptanz:** Ein ZIP; `CargoPlan.App.exe` startet nach dem Entpacken; ZIP < 80 MB; Settings und CLI zeigen dieselbe Version.

**Befund:** Ordner 143,2 MB in 416 Dateien (260 im Wurzelordner, 13 Sprachordner à 12), ZIP (`Compress-Archive`) 62,0 MB. Aus dem entpackten ZIP: Overlay startet, Fußzeile und `CargoPlan.Cli --version` zeigen `0.0.0-dev+2290a0a…`, `plan` und `contracts` (Kopie der CT-DB) laufen. Kollisionscheck (App und Cli getrennt publiziert, SHA256 verglichen): 196 gleichnamige Dateien, 2 unterschiedlich (`WindowsBase.dll`, `SQLitePCLRaw.provider.e_sqlite3.dll`), daher die Reihenfolge Cli → App (D48).

### [x] T6.3 Release-Workflow
**Warum:** Tag setzen → Download steht. Kein manuelles Hochladen.
- Kein MinVer/GitVersion: `Directory.Build.props` setzt `<Version>0.0.0-dev</Version>`; CI setzt `-p:Version` aus dem Tag (ohne „v“) und bricht ab, wenn der Rest kein SemVer ist
- `.github/workflows/release.yml`: Trigger `push` auf `tags: v*`, `build/publish.ps1` aus T6.2 (D48), `softprops/action-gh-release` hängt ein ZIP + SHA256 an
- Release-Notes als Body-Datei, von Hand geschrieben, nicht `generate_release_notes`

**Präzisiert in der Planung:** Erster Schritt prüft den Tag (SemVer 2.0 ohne Build-Metadaten, das SDK hängt `+<sha>` selbst an) und verlangt `docs/release-notes/v<version>.md`, sonst Abbruch vor dem Build. Prerelease-Suffix → `prerelease: true`. `-p:Version` schon beim Build, Test ohne Neubau, dann `publish.ps1 -Version`. `publish.ps1` schreibt `out/publish/SC-CargoPlan/`, das ZIP `SC-CargoPlan-v<version>-win-x64.zip` mit Wurzelordner `SC-CargoPlan/` (Einträge mit `/`, auch unter Windows PowerShell 5.1) und eine `.sha256` im `sha256sum`-Format; ohne `-Version` bleibt 0.0.0-dev. `softprops/action-gh-release` v3.0.3 auf den Commit gepinnt. `SatelliteResourceLanguages` = `en` (D50). Akzeptanz mit `v0.1.0-rc.1` statt `v0.1.0`; `v0.1.0` erst nach T6.5, rc.1 bleibt bis dahin als Prerelease stehen (Prüfbuild für T6.5), dann Release und Tag löschen. Präzisierung in der Planphase, ratifiziert durch Freigabe (D49, D50).

**Akzeptanz:** Tag `v0.1.0-rc.1` pushen → Prerelease mit ZIP + SHA256 und den handgeschriebenen Notes, Version 0.1.0-rc.1 in Settings und CLI; Tag `v0.1` → Workflow bricht ab, kein Release.

**Befund (lokal):** Ordner 134,9 MB in 260 Dateien, keine Unterordner (vorher 143,2 MB / 416), ZIP 58,7 MB (vorher 62,0), ein Wurzelordner, alle Einträge mit `/`, `.sha256` passt zu `Get-FileHash`. `publish.ps1 -Version 0.1.0-rc.1` → `CargoPlan.Cli --version` aus dem entpackten ZIP zeigt `0.1.0-rc.1+9796bfb…`. Tag-Regex: `v0.1.0`, `v0.1.0-rc.1` ✓; `v0.1`, `v01.0.0`, `v0.1.0-rc.01`, `v0.1.0+build`, `v0.1.0-` ✗. Kollisionscheck nach D50 unverändert: 196 gleichnamig, 2 unterschiedlich, Vereinigung 259 + `LICENSE` = 260.

**Befund (CI):** CI auf `6bbc115` grün (ubuntu, windows). Tag `v0.1.0-rc.1` → Release-Lauf grün, [Prerelease](https://github.com/MRiediger/sc-cargoplan/releases/tag/v0.1.0-rc.1) mit ZIP + `.sha256`, Body = Notes-Datei; heruntergeladenes ZIP passt zur `.sha256`, 260 Einträge unter `SC-CargoPlan/`, gleiche Namen und entpackte Größe (134,9 MB) wie lokal; `CargoPlan.Cli --version` zeigt `0.1.0-rc.1+6bbc115…`, die Fußzeile des Settings-Fensters „SC-CargoPlan 0.1.0-rc.1+6bbc115…“ (von Martin geprüft). Das CI-ZIP ist 60,7 MB statt lokal 58,7 MB: gleicher Inhalt, nur anders komprimiert (vermutlich .NET auf dem Runner gegenüber .NET Framework unter Windows PowerShell 5.1, nicht untersucht), also nicht byte-gleich. Tag `v0.1` → Lauf [39652136154](https://github.com/MRiediger/sc-cargoplan/actions/runs/39652136154) scheitert in „Version from tag“ („Tag 'v0.1' is not v<SemVer> …“), Rest übersprungen, kein Release; Tag lokal und remote gelöscht. **Der rote Lauf in der Actions-Historie ist dieser absichtliche Negativtest, kein Fehler.**

### [x] T6.4 Repo-Hygiene für Contributor
**Warum:** Erster externer PR soll nicht an Unklarheit scheitern.
- `CONTRIBUTING.md` (englisch): build, test, Schiff beisteuern, PR-Regeln
- Issue-Templates: Bug, Ship request, Feature; Bug verlangt die Version und die Ausgabe von `contracts --raw`
- `docs/screenshots/` mit Overlay über dem Spiel

**Präzisiert in der Planung:** CONTRIBUTING mit den Befehlen aus ci.yml, Publish nur über `build/publish.ps1` (D48), Schiff → ship-format.md mit `stackAxis` und Quelle. Issue Forms: Bug mit Pflichtfeldern Version (`input`, aus der Settings-Fußzeile), Schritte, erwartet, beobachtet, Screenshot (`upload`), SC-Version, Fenstermodus; `contracts --raw` optional mit „required if missions are involved“ (Issue Forms kennen kein bedingtes Pflichtfeld). Ship fragt zusätzlich Position und Rack-Reihenfolge ab, Label `ship` neu. Feature: Problem zuerst. `config.yml`: keine leeren Issues, Fragen → Discussions. Dazu PR-Template (vier Checkboxen aus den PR-Regeln) und `SECURITY.md` (Private vulnerability reporting), README-Abschnitt „Contributing“ mit den Links. Dependabot je Ökosystem eine Gruppe, monatlich (D51), alte PRs geschlossen, D49-Addendum. Code of Conduct → Backlog. Screenshots: T6.4 schließt mit der Liste, die Bilder macht Martin, Vorbedingung von T6.5. Präzisierung in der Planphase, ratifiziert durch Freigabe (D51).

**Akzeptanz:** Dateien vorhanden, Links aus README funktionieren.

**Befund:** Alle README- und CONTRIBUTING-Links auf vorhandene Dateien und Anker geprüft. Community-Profil 42 % → 85 %; REST `issue_template` bleibt null und GraphQL `issueTemplates` leer, laut Sekundärquelle bei YAML-Issue-Forms immer so (devactivity.com, nicht GitHub-Doku); `config.yml` wird erkannt (Contact-Link da); Template-Chooser von Martin eingeloggt geprüft: drei Formulare, Contact-Link, funktioniert. Dependabot: App.Tests fehlte in allen alten PRs, weil der letzte volle Scan (16.10.) vor App.Tests (18.10.) lag, kein Fehler (Log: `CargoPlan.App.csproj` mit `"Error": null`). Push der neuen Konfiguration startet die Läufe sofort. Dependabot schloss #4, #8–#11 selbst, #6, #7, #13–#16 von Hand. #17 (checkout v7, setup-dotnet v6 in ci.yml **und** release.yml) und #18 (coverlet 10.1.0, Test.Sdk 18.11.0, xunit.runner.visualstudio 4.0.1 in allen drei Test-Projekten) grün auf beiden Runnern mit 564/791/93 wie main, squash-gemergt; lokal 0 Warnungen, 564/791/93. softprops bleibt v3.0.3 (neuestes Release). Private vulnerability reporting, Dependabot alerts und Dependabot security updates an (Martin). `docs/screenshots/first-start/smartscreen.png` (SmartScreen-Dialog der unsignierten EXE, englisch) ins Repo genommen, für den SmartScreen-Hinweis in T6.5.

**Screenshots für README und Nexus** (Martin, nach `docs/screenshots/`; vorhanden sind nur die T6.0-Bilder ohne Spiel im Hintergrund):
1. `overlay-ingame-elevator.png` – Overlay mit echtem Plan (3–4 Ziele, Legende, ein Ziel abgehakt) über dem Spiel am Frachtaufzug
2. `editor.png` – Editor mit mehreren Zeilen (zwei Ziele, ein handgetippter Kistenmix kursiv)
3. `settings.png` – Settings-Fenster mit Fußzeile (Version)
4. optional `overlay-drag.png` (Drag mit cyan Ghost); Nexus-Titelbild bei T6.6 nach Nexus-Format

PNG, kein Spielername/Org im Bild, Overlay außerhalb des CT-Capture-Bereichs.

### [x] T6.4b Bow marker in the ship view
**Warum:** Release-Blocker, Präzisierung in der T6.5-Planung: ohne Markierung ist Rack 1 nicht von Rack 4 zu unterscheiden, sobald der Spieler die Ansicht gedreht hat.
- Form, keine Beschriftung: Keil am Bug, Bug bleibt links in der Default-Kamera (D15)
- Keine Schiffsformat-Änderung, keine Settings-Option, Core unverändert
- Test: Keil am Bug-Ende in den vier Default-Yaw-Quadranten; Hit-Test und Drop-Target unverändert
- D-Eintrag mit Gegenargument (stumpfe Schiffe bekommen eine Nase)

**Präzisiert in der Planung:** Befund: `ShipScene` kennt keinen Rumpf, `Ship.Bounds` ist die Vereinigung der Rack-Boxen, gezeichnet als gestrichelter Rahmen aus Linien außerhalb des Painters (D21); Hit-Test und Drop-Target sehen ihn nicht. Die Racks reichen bis zur Bugkante, eine Verjüngung auf den letzten 10 % schnitte durch sie (und wären beim Hull A 0,4 Zellen). Daher Nase **vor** der Bugkante, Länge max(1 Zelle, 10 % der Länge), Spitze halbe Breite, volle Höhe, Linien wie der Rahmen (gestrichelt, `MissionPalette.Frame`), nicht gefüllt, kein Painter-Eingriff. Folge für T6.5: `src` ändert sich nach rc.1, geprüft wird am Prerelease `v0.1.0-rc.2`. Präzisierung in der Planphase, ratifiziert durch Freigabe (D52).

**Akzeptanz:** Keil am Bug in allen vier Quadranten (Test); Hit-Test unverändert (bestehende Tests grün, Klick auf die Nase trifft nichts); Martin hat die Sichtbarkeit im Spiel beurteilt.

**Befund (Linien):** `SceneProjector.BowWedge` als 8 Kanten, `Frame` 20 statt 12 Linien. In der Default-Kamera bleibt die Hull-A-Nase im alten Extent (v max 3,375 < 3,5; u min −4,98 > −5,20), Default-Zoom und Nummerngröße unverändert (`RackNumberSizeTests` grün ohne Änderung). Im Spiel kaum erkennbar (Martin): Stil des Rahmens, keine Fläche.

**Nachbesserung, präzisiert:** Nase als 5 gefüllte Flächen (Spitze, oben, unten, zwei Flanken), Füllung `MissionPalette.FreeEdge` (Alpha 0x50; freie Zellen selbst haben keine Füllung, daher deren Kantenfarbe als Fläche), Kanten `RackPen` durchgezogen; sichtbare Flächen über die Normalen. Painter-Befund: die x-Achse trennt Nase und Racks immer, der Yaw-Quadrant entscheidet (Bugseite → nach den Kisten, vor Rot/Cyan; sonst vor den Rack-Kanten); kein `DrawOrder`-Eintrag. Länge bleibt max(1 Zelle, 10 %). Präzisierung in der Planphase, ratifiziert durch Freigabe (D52-Addendum).

**Befund (Flächen):** `BowWedge` liefert `BowWedgeFace` (Fläche, Ecken, Normale nach außen), `ProjectedScene.BowWedge` die sichtbaren Flächen und `InFront`; `Frame` wieder 12 Linien, Extent umschließt alle Keil-Ecken. Tests: 5 Flächen mit Spitzenmaßen und Normalen nach außen (Hull A, 20-Zellen-Box); in vier Yaw-Quadranten Spitze bugwärts, `InFront` und sichtbare Spitze genau von der Bugseite; Klick auf die Nase trifft nichts. Tests 564/798/93 (+7 App gegenüber main), 0 Warnungen. Im Spiel (Martin): Fläche verdeckt die Container.

**Nachbesserung 2, präzisiert:** Füllung eigene Farbe `MissionPalette.BowWedgeFill`, Alpha 0x18. Schriftzug „Bow“ (UI-Text Englisch, Martin schrieb „Bug“) flach auf dem Nasenboden wie in Martins Skizze, Zeile quer zum Schiff, Buchstabenoberkanten achtern; mit der Nase gezeichnet. Die Rack-Nummer-Einstellungen gelten auch für ihn: Farbe und Umriss direkt, Schriftgröße relativ zum Default 27, gedeckelt (nie größer als der Nasenboden); Settings-Fenster „Rack numbers and bow label“, Umriss-Label „Outline around the rack numbers and the bow label“. Präzisierung in der Planphase, ratifiziert durch Freigabe (D52-Addendum 2).

**Befund (Schriftzug):** `FloorLabel.Lay` legt die Glyphen per affiner Matrix auf den projizierten Boden (`WedgeFloor`: Mitte, eine Zelle quer und bugwärts, Platz 70 % von Nasenlänge und Spitzenbreite), zentriert die Tinte (nicht den Zeilenkasten) und flacht in Bildschirmpixel ab: keine Transformation bleibt, der Umriss wird in Pixeln gezogen (für WPF nicht dokumentiert, ob `Geometry.Transform` den Stift mitskaliert; so egal). Tests: in vier Quadranten jeder Punkt des Schriftzugs im projizierten Nasenboden; Anteil 0,5 halbiert Breite und Höhe um die Bodenmitte, Anteil 2 = Anteil 1. Im Spiel: passt (Martin). Tests 564/803/93, 0 Warnungen. Ein erster Testlauf lief gegen den alten Build, weil das offene Overlay die EXE sperrte; nach dem Beenden neu gebaut. Danach im Spiel (Martin): Label größer als die Racknummern.

**Nachbesserung 3, präzisiert:** Größe nicht mehr als Anteil vom Default 27, sondern auf dem Bildschirm wie die Racknummern: entlang der Glyph-Hochachse (Schiffs-x auf dem Boden) ist ein Em die aktuelle `RackNumberSize.For` (D32 inkl. Schrumpfen), umgerechnet über die projizierte Länge einer Zelle in x bei Kamera und Zoom; Obergrenze bleibt der Platz auf dem Boden. „Gleich hoch“ heißt: Großbuchstabe B so hoch wie die Ziffer 1 bei gleicher Schrift und Größe (Rückfrage; wörtlich „B = RackNumberSize px“ wäre ~1,4× die Ziffern). Präzisierung in der Planphase, ratifiziert durch Freigabe (D52-Addendum 3).

**Befund (Größe):** `FloorLabel.Placement` liefert die Matrix (Lay flacht damit ab), `numberSize` einmal pro Frame für Nummern und Label. Tests: Yaw 45, Default-Zoom und 2×: B auf dem Bildschirm = Ziffer 1 bei `RackNumberSize` ±1 px (die Obergrenze greift dort also nicht); Boden mit 0,1 Zellen Platz quer → Label genau 0,1 Zellen breit. Anteil-Test entfällt. Tests 564/805/93, 0 Warnungen.

### [x] T6.5 README final (erledigt, Häkchen nachgetragen in K10; Commits 1bfd3ba…c0aa7da, Bilder und Tag mit T7.5/T9.2)
**Warum:** Das README ist die Download-Seite. Was dort steht, muss der Build tun.
- Vorbedingung: Screenshots aus der T6.4-Liste liegen in `docs/screenshots/`
- Abschnitte: Was · Download · Erstes Mal · CT-Anbindung · Hotkeys · Known issues · Bug melden · Credits · Lizenz
- SmartScreen-Warnung bei der unsignierten EXE ehrlich dokumentieren (aus T6.3 hierher)
- Kein Ruckel-Hinweis mehr (in 7e94ac3 entfernt, D45)
- Jede Verhaltensaussage am Release-Build geprüft (das ZIP aus dem Prerelease `v0.1.0-rc.3`, nach T6.4b und dem Editor-Absturz-Fix `7f04dd7`)
- Danach Tag `v0.1.0` (Notes `docs/release-notes/v0.1.0.md`), dann die noch vorhandenen rc-Releases und -Tags löschen (D49). rc.3 ersetzt rc.2, das den Editor-Absturz noch enthält. rc.2 ersetzt rc.1: Prerelease aus `1bfd3ba` grün, Download passt zur `.sha256`, 260 Dateien unter `SC-CargoPlan/`, `CargoPlan.Cli --version` = `0.1.0-rc.2+1bfd3ba…`; Release und Tag `v0.1.0-rc.1` danach gelöscht (remote und lokal)

**Akzeptanz:** Alle Abschnitte vorhanden; jede Verhaltensaussage am Build nachvollzogen; Links funktionieren.

**Präzisiert in der Planung:** README in fester Hauler-Reihenfolge (Was · Download & Installation · Erstes Mal · ContractTracker · Am Aufzug · Settings · Known issues · Bug melden · Credits · Contributing/Lizenz), kurz; die Referenztiefe in die neue `docs/user-guide.md` mit gleicher Gliederung, README verlinkt pro Abschnitt. Hotkeys kein eigener README-Abschnitt (kurz in „Erstes Mal“, voll im Guide); Build-Abschnitt entfällt (Duplikat von CONTRIBUTING.md); kein Erec-Credit (Hull-A-Daten im Spiel ermittelt). Falsche Aussagen gestrichen, nicht umgeschrieben: „single Windows EXE“, „Screenshot: coming soon“, „Red edges … only a plan file edited by hand“ (seit T5.4a erzeugen Planer und Drag rote Stapelbefunde), „put crates on top of [delivered crates]“ (abgehakte Kisten sind Luft, `DropTarget.Resolve`), Reset nur durch Löschen von settings.json. Prüfung: CLI/Dateien/DB-Kopie durch CC am rc.3-ZIP, GUI-Aussagen am Code von `01770ab` (= rc.3) und per Checkliste durch Martin. Ohne Bilder (Martin): alle Bilder als Platzhalter-Kommentare `<!-- image: … -->`, auch die first-start-Bilder (zeigen das Overlay noch ohne Bug-Keil); die drei neuen PNGs bleiben uncommittet. Tag v0.1.0 erst nach dem Backlog (Martin). Präzisierung in der Planphase, ratifiziert durch Freigabe.

**Befund (Prüfung je Abschnitt, Build rc.3 = `01770ab`):**
- **Was:** Problem und Ansatz aus CLAUDE.md, „separates Fenster, nichts im Spielprozess“ aus D13/D45 (keine Hooks). Hero-Bild Platzhalter.
- **Download & Installation:** rc.3-Release per `gh release download`: ZIP passt zur `.sha256`, 260 Dateien alle unter `SC-CargoPlan/`, `CargoPlan.Cli.exe --version` = `0.1.0-rc.3+01770ab…`. Der Einzeiler `$zip = Get-Item .\SC-CargoPlan-*.zip; (Get-FileHash $zip).Hash -eq (Get-Content "$zip.sha256").Split(' ')[0]` liefert unter Windows PowerShell 5.1.26100 `True`. SmartScreen-Text aus `smartscreen.png` („Windows protected your PC“, „More info“); „Run anyway“ und „einmal pro Download“ → Checkliste Martin. Self-contained (D46). Borderless aus D13.
- **Erstes Mal:** Leerzustand-Text „No plan. Open the editor to enter missions.“ und „Open editor“ im Code; keine Hotkey-Defaults (`hotkeys` null, D17; rc.2-Settings-Screenshot zeigt drei Mal „Not set“); Tastenregel aus `HotkeyParser` (A–Z, 0–9, F1–F24 ohne F12, Space, Pause, ScrollLock, Insert, Delete, Home, End, PageUp, PageDown); Tooltip am gesperrten Plan (`51d77c6`, Test) → Checkliste Martin.
- **ContractTracker:** Markierungstexte `4 SCU?`, `⚠ 1 of 3 destinations`, `⚠ duplicate?`, `⚠ doesn't fit (16 SCU, 8 free) – tick to drop`, Notice „ContractTracker database looks new – all contracts imported“ wörtlich im Code; Auto-Detect-Pfad und „N new contracts“ aus D44; Nexus-URL aus `ContractTrackerSectionViewModel.NexusUrl` (Nexus antwortet Skripten mit 403, im Browser prüfen).
- **Am Aufzug:** Abhaken, Drag, Split/Merge unverändert aus der README (in T3/T4/T5 im Spiel abgenommen); Stapelregel weich am Code: `CrateLayout` meldet „… in rack N lies on cargo for …, which is unloaded first“, die obere Kiste bekommt den roten Umriss (`CrateLayout.Faulty`), das Rack rote Kanten, die Zeile steht rot unter der Legende (`Scene.Violations`); kein Abbruch (D24-Addendum T5.4a). Abhaken außer Reihe: „has nothing below it“. → Checkliste Martin.
- **Settings:** Abschnitte und „Reset to defaults“ aus `SettingsWindow.xaml` (Window, Legend, Legend layout, Crates, Header, Buttons mit Reset; Tool windows, Hotkeys, ContractTracker ohne); Default Tool windows = `System`; `settings.json.bak` (D47); Fußzeile mit Version (T6.2).
- **Known issues:** aus dem Backlog, was Hauler merken: nur Hull A, unsigniert, kein Auto-Update (T7.2), Exclusive Fullscreen, Kamera nicht gespeichert (D15), Stapelbefunde bei ≥ 3 Stops (formbewusste Partition). Weggelassen: App-Icon, Editor-Undo, Hit-Test über der Legende (D33).
- **Bug melden:** Bug-Formular-URL `issues/new?template=bug.yml` leitet auf das GitHub-Login um (Konto nötig, im README genannt). `Copy-Item … .\contracts-copy.db` und `.\CargoPlan.Cli.exe contracts .\contracts-copy.db --raw` wörtlich im entpackten rc.3-Ordner ausgeführt: Exit 0, Vertrag und Rohtext. Kopie in den Programmordner statt auf den Desktop (der kann per OneDrive umgeleitet sein).
- **Credits:** Okabe-Ito-Quelle `https://jfly.uni-koeln.de/color/` liefert 200, Titel „Color Universal Design (CUD) / Colorblind Barrier Free“; acht Stop-Farben in `MissionPalette.Colors`.
- **Links:** Skript über README, user-guide.md, settings-format.md, CONTRIBUTING.md: alle relativen Dateien und Anker vorhanden (Negativtest mit falschem Anker und fehlender Datei schlägt an); `settings-format.md` zeigt jetzt auf `user-guide.md#hotkeys`. `releases/latest` leitet bis v0.1.0 auf `/releases` um.
- Tests 564/809/93, 0 Warnungen (XAML nur Kommentar).

**Offen:** Checkliste Martin am rc.3-ZIP (Run anyway und zweiter Start ohne Dialog; Plan-Tooltip; roter Stapelbefund mit Zeile unter der Legende; Drag einer Kiste auf die Stelle einer abgehakten); Bilder einsetzen; Notes `v0.1.0.md`, Tag `v0.1.0`, dann rc.3 löschen – nach dem Backlog.

### ~~[ ] T6.6 Nexus Mods~~ → T9.3 (dort weitergeführt, „= T6.6“; gestrichen in K10)
**Warum:** Viele Hauler finden Tools auf Nexus, dort liegt auch ContractTracker.
- Nach dem GitHub-Tag, gleiches ZIP wie im GitHub-Release
- Permissions aus der MIT-Lizenz abgeleitet
- Credit für ContractTracker

**Akzeptanz:** Nexus-Seite online mit dem ZIP des GitHub-Releases (gleiche SHA256), Permissions passend zu MIT, CT-Credit sichtbar.

→ Umgesetzt als T9.3 (Phase 9, D87).

---

