# Archiv: Phase 4 – Settings & Konfiguration

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Phase 4 – Settings & Konfiguration

Regel für alle Tasks dieser Phase: **Core bleibt unverändert**, alles in `CargoPlan.App`. Grundlage: D28 (Halo statt Backdrop-Blur), D29 (Stop-Farben fest), D30 (Apply-on-change). Reihenfolge der Umsetzung = Reihenfolge hier, nicht die Nummer.

Einstellbare Werte:
- **Fenster:** Transparenz, Hintergrund, Rahmen
- **Legende:** Schriftgröße, Schriftfarbe, Halo
- **Kisten:** Rack-Nummern-Schriftgröße und -farbe, Hintergrund-Transparenz, Rahmen-Transparenz, Halo
- **Kopfzeile/Reiter:** Schriftfarbe, Halo
- **Buttons:** Hintergrund, Rahmen, Halo, Eckenradius

### [x] T4.1 Settings-Modell Schema 2 + Theme-Ressourcen
**Warum:** Jede Einstellung braucht erst einen Platz in settings.json und einen Weg in die Oberfläche; das Settings-Fenster (T4.2) baut darauf auf.
**Präzisiert in der Planung:** Top-Level `overlay`, `hotkeys`, `appearance` (`window`, `header`, `legend`, `crates`, `buttons`); `legend` an/aus + Position kommt mit T4.3, Rack-Nummern-Schriftgröße mit T4.6, beide ohne Schema-Bump; Transparenz = Alpha der Farbe (`#AARRGGBB`), Kisten-Alpha als Zahl 0–255 (Farben fest, D29); Wert außerhalb des Bereichs → Clamp + Notice, ungültige Farbe → Default dieses Werts + Notice, unbekannte Keys/falscher Typ weiter strikt (ganze Datei, D13); klickende Hintergründe mindestens Alpha 1; Halo pro Abschnitt `enabled`/`color`/`size`, Text weich (`DropShadowEffect`), Rack-Nummern scharfer Umriss; Button-Rahmen nur für Glyph-Buttons, „Open editor“ behält seinen Umriss in Legendenfarbe; `legend.fontSize` `null` = Windows-Nachrichtenschrift (auf dem Dev-Rechner 11, nicht 12); Schema 1 wird erst beim nächsten Speichern als 2 geschrieben, dann vollständig; Ressourcen aus Code (`ThemeResources.Apply`), nicht als Literale in App.xaml; Halo aus = Key entfernt (Spike: `null` per Indexer liefert den Property-Default, nicht `null`); positionale Records statt `init`-Properties (Spike: Source-Gen in .NET 10 setzt fehlende `init`-Properties auf null/0); Zahnrad bleibt in T4.2. Pixelvergleich alt/neu ohne Datei: 0 abweichende Pixel. Präzisierung in der Planphase, ratifiziert durch Freigabe (D31).
- settings.json `schemaVersion` 2 mit `appearance`, `legend`, `hotkeys`; Fensterposition und -größe bleiben wie in T3.4
- Theme-Werte als Ressourcen in `App.xaml`, Views binden per `DynamicResource` (Grundlage für die Live-Vorschau in T4.2)
- Noch ohne UI: alle Werte wirken bereits aus settings.json
- Schema 1 bleibt lesbar, Defaults für Neues; kaputte Datei → Defaults + Notice (D13)

**Akzeptanz:** `dotnet test` grün (Schema 1 → Defaults für Neues, Schema-2-Rundlauf, kaputte Datei → Defaults + Notice, Datei unverändert); Wert in settings.json ändern → nach Neustart sichtbar; Core unverändert.

### [x] T4.6 Rack-Nummern und Linienstärken beim Zoom konstant
**Warum:** Rack-Nummern und Linien wachsen heute mit dem Zoom mit (Backlog aus D21). Eine Schriftgröße als Einstellung wäre damit bei jedem Zoom eine andere. Voraussetzung für die Rack-Nummern-Schriftgröße als Einstellung.
**Präzisiert in der Planung:** Linien waren schon konstant (Zoom skaliert die Punkte, nicht die Zeichnung), also keine Code-Änderung bei den Pens; nur die Rack-Nummer wuchs. `crates.rackNumberFontSize` Default 27 (alte Größe bei Zoom 1 im Standardfenster), Bereich 8–48; passt die Nummer nicht mehr aufs Rack, schrumpft sie bis zur kürzesten Rack-Seite, nicht unter 12 (D32). Die Backlog-Zeile stand nur in D21 und ist dort per Addendum erledigt. Präzisierung in der Planphase, ratifiziert durch Freigabe.
- Rack-Nummern in fester Bildschirmgröße, Linienstärken (Kanten, rote Kanten, Drop-Ziel, Auswahl) fest; Kisten und Zellen skalieren weiter
- Backlog-Zeile entfällt
- Danach `crates.rackNumberFontSize` in settings.json (optional, kein Schema-Bump, D31), Key `CratesRackNumberFontSize`

**Akzeptanz:** `dotnet test` grün; Zoom 0,5× bis 4×: Nummern und Linien bleiben gleich groß, Kisten skalieren; Hit-Test und Drop-Ziel unverändert.

### [x] T4.3 Legende an/aus + Position
**Warum:** Je nach Fensterform und Platz neben dem Kiosk steht die Legende besser oben oder unten, links, mittig oder rechts. Wer die Farben kennt, blendet sie aus.
**Präzisiert in der Planung:** `legend.position` als String mit sechs Namen (`bottom-left` …, kleingeschrieben), geprüft in `AppearanceRules` wie eine Farbe: ungültig → `bottom-left` + kurze Notice („edge-alignment, e.g. bottom-right“), kein Enum-Konverter, der die ganze Datei kippen würde; left/center/right verschiebt einen Block in Inhaltsbreite, innen linksbündig (Swatches in einer Spalte), die Hint-Reserve misst mit; oben: Kopfzeile → Notices → Block → Ansicht, Notices immer am Außenrand in voller Breite, nicht im Block; Live-Umschaltung über Style-Trigger auf `OverlayViewModel.IsLegendVisible`/`LegendPosition`, nicht DynamicResource; Legenden-Drag ohne Code-Änderung (die Ansicht arbeitet in eigenen Koordinaten); „aus“ nimmt den Legenden-Drag mit, ein Stop zieht dann über die Auswahl um (README/settings-format); Akzeptanz „Legende aus + kaputte settings.json“ als Wertfehler bei `visible: false` vorgeführt, weil eine ganz kaputte Datei Defaults lädt und die Legende damit ohnehin an ist; Prüfung offscreen per `RenderTargetBitmap`: bottom-left 0 abweichende Pixel, kein Sprung beim Abhaken in allen zwölf Layouts. Präzisierung in der Planphase, ratifiziert durch Freigabe (D33).
- Legende an/aus und Position (top/bottom × left/center/right) in settings.json, Top-Level `legend` (optional, kein Schema-Bump, D31)
- Layout-Umbau des Overlays; der reservierte Hinweisplatz (D16) bleibt, die Ansicht springt beim Abhaken nicht
- „aus“ blendet nur die Stop-Zeilen aus; Hinweise und Notices bleiben sichtbar (an der Legendenposition), sonst sähe niemand die D13-Notice „kaputte settings.json → Defaults“
- Abhaken (D16) und Legenden-Drag (D25) in jeder Position; ausgeblendet bleibt Abhaken per Klick in der Ansicht

**Akzeptanz:** alle sechs Positionen und „aus“ per settings.json; Abhaken und Legenden-Drag in jeder Position; Ansicht springt beim Abhaken nicht; Legende aus + kaputte settings.json → Notice sichtbar; Core unverändert.

### [x] T4.2a Zahnrad + Settings-Fenster (Window, Legend, Legend layout)
**Warum:** settings.json von Hand zu bearbeiten schreckt Hauler ab (D17). Die Lesbarkeit über hellem und dunklem Hintergrund stellt man im Spiel ein, nicht im Texteditor.
**Präzisiert in der Planung:** Schnitt in T4.2a/T4.2b nach Abschnitten, jeder Teil für sich nutzbar; `SettingsSession` als einzige Besitzerin von `AppSettings` (Start und Live über dieselbe Apply-Methode, `ChangeLook` normalisiert wie das Laden und speichert entprellt, Bounds über die Session statt eigener Kopie im `OverlayBoundsSaver`); `SettingsViewModel` WPF-frei, liefert Appearance + Legend-Layout statt ganzem `AppSettings` (sonst veraltete Bounds); Farbe = Hex `#RRGGBB` + Alpha-Regler + Swatch, ungültig → Feld markiert (Hintergrund, weil der System-Rahmen bei Fokus blau wird), kein Apply, keine Notice; Reglergrenzen aus `AppearanceRules`; „Use Windows text size“ = `null`; Legend layout eigener Abschnitt mit eigenem Reset; Fenster im Systemlook, eine Instanz, Esc/X schließen, neben dem Overlay, Position nicht gespeichert; Zahnrad rechts neben dem Stift am Schloss; Rettungsweg Tooltip + README statt Code; kein Hotkey-Platzhalter. Präzisierung in der Planphase, ratifiziert durch Freigabe (D34).
- Zahnrad in der Kopfzeile, verhält sich beim Lock wie der Stift: eine Regel für alle Kopfzeilen-Buttons
- Settings-Fenster: aktivierbar, Topmost, bei Auto-Lock ausgeblendet wie der Editor (D19), eins gleichzeitig
- Abschnitte Window / Legend / Legend layout
- Live-Vorschau: jede Änderung wirkt sofort und wird gespeichert, kein OK/Cancel (D30); „Reset to defaults“ pro Abschnitt

**Akzeptanz:** jeden Wert der drei Abschnitte im Fenster ändern → Overlay ändert sich sofort, Neustart behält ihn; Reset pro Abschnitt; Auto-Lock blendet das Fenster aus und wieder ein; Core unverändert.

### [x] T4.2c Color-Picker im Settings-Fenster
**Warum:** Hex-Codes kennt kaum ein Hauler; eine Farbe sucht man mit der Maus, während man das Overlay über der Szene sieht.
**Präzisiert in der Planung:** eigenes Popup statt Paket oder Windows-Farbdialog (der wäre modal, ohne Live-Vorschau); Swatch als Toggle, Popup mit Sättigung/Helligkeit-Fläche (`SaturationValuePad`) und Farbton-Leiste, Alpha bleibt eigener Regler; `Hsv` WPF-frei; der Picker schreibt `Hex`, also ein Weg zum Overlay; Grau behält den Farbton, Schwarz die Sättigung; Esc schließt bei offenem Picker nur den Picker, sonst das Fenster (Gegenprobe ComboBox); der Picker schließt immer, wenn das Fenster unsichtbar wird (Auto-Lock = Tractor-Beam-Griff). Präzisierung in der Planphase, ratifiziert durch Freigabe (D34-Addendum).
- Klick auf den Swatch öffnet den Picker, Ziehen ändert das Overlay live
- Gilt für jede Farbe im Fenster, auch die aus T4.2b

**Akzeptanz:** Farbe per Picker wählen → Overlay ändert sich beim Ziehen, Hex-Feld folgt, Neustart behält sie; Esc mit offenem Picker schließt nur den Picker; Auto-Lock schließt den Picker; Core unverändert.

### [x] T4.2b Settings-Fenster: Header, Crates, Buttons
**Warum:** Der Rest der Werte aus T4.1 gehört ins selbe Fenster, sonst bleibt für Kopfzeile, Kisten und Buttons der Texteditor.
**Präzisiert in der Planung:** Reihenfolge Window → Legend → Legend layout → Crates → Header → Buttons (am Aufzug ist die Kisten-Füllung der nächste Regler nach Transparenz und Legende); jeder Halo nennt, was er umgibt (`HaloEditor.Label`), bei den Kisten „Outline around the rack numbers“; Kisten-Alpha in Prozent wie die Farben (`ColourField.Percent`), gespeichert 0–255; Kopfzeilenfarbe „Text and glyphs“ mit Hinweis auf den Rettungsweg. Präzisierung in der Planphase, ratifiziert durch Freigabe (D34-Addendum). Beim Laufzeit-Check gefunden und mit behoben: der Swatch aus T4.2c war 0 × 0 groß (der Button zentriert seinen Inhalt), jetzt gestreckt.
- Abschnitte Header (Text colour, Halo), Crates (Fill/Edge alpha 0–255, Rack number colour, Rack number size 8–48, Halo/Umriss), Buttons (Background Alpha ≥ 1, Border colour/width, Corner radius, Halo) mit `ColourField`/`HaloEditor` aus T4.2a
- „Reset to defaults“ pro Abschnitt, Tests nach dem Muster von T4.2a
- README/settings-format: „still set in the file“ entfällt

**Akzeptanz:** jeden Wert der drei Abschnitte im Fenster ändern → Overlay ändert sich sofort, Neustart behält ihn; Reset pro Abschnitt; Core unverändert.

### [x] T4.4 Hotkey-Aufnahme
**Warum:** Hotkeys sind seit T3.7 nur über settings.json setzbar (D17). Tasten drücken ist schneller als tippen, und ein Tippfehler macht die ganze Datei ungültig.
**Präzisiert in der Planung:** Aufnahme über KeyDown des eigenen fokussierten Fensters (`RecordedKey`, kein Hook); unsere Hotkeys während der Aufnahme ausgesetzt, sonst kämen ihre Tasten nie an; `HotkeyParser.TryCreate` teilt Regeln und Meldungen mit `TryParse`; belegte Kombination (eigene andere Aktion per Name, anderes Programm per `RegisterHotKey`) wird abgelehnt und nicht gespeichert, der alte Hotkey bleibt aktiv; Meldung am Feld statt Notice; „Clear“ schreibt `null`, `null` und fehlend gelten gleich; Hinweis „Ctrl, Alt or Win required. Win combinations may be taken by Windows itself.“; Esc bricht nur die Aufnahme ab, Fokusverlust/Ausblenden/Schließen ebenso; kein „Reset to defaults“ (Default = kein Hotkey); eigener Abschnitt ganz unten, eigener DataContext. Präzisierung in der Planphase, ratifiziert durch Freigabe (D35).
- Abschnitt Hotkeys im Settings-Fenster: je Aktion (Lock, Overlay) Aufnahme per Tastendruck, Löschen möglich
- Regeln wie `HotkeyParser` (Ctrl/Alt/Win Pflicht, F12 abgelehnt), weiterhin keine Defaults (D17)
- Belegte Kombination → Konfliktmeldung aus T3.7

**Akzeptanz:** Hotkey im Fenster setzen → wirkt sofort ohne Neustart, steht in settings.json; belegte Kombination → Meldung, der andere Hotkey bleibt aktiv; Shift+L und F12 werden abgelehnt.

### [x] T4.5 Füllrichtung im Editor
**Warum:** Ein Koordinator soll allen dieselbe Füllrichtung zeigen (T3.10). Seit D24 steht sie im Plan (`fillOrder`), wählbar ist sie aber noch nicht.
**Präzisiert in der Planung:** ComboBox „Fill order:“ links vom Plan-Button (derselbe Begriff wie der Key `fillOrder`), Einträge „Bow first“/„Stern first“ (`FillOrderChoice`); Änderung markiert „unplanned changes“, wirkt erst mit Plan; Clear rows und Import behalten die Richtung; Startwert aus dem aktuellen Plan, ohne Plan Bug zuerst; Mission-Dateien tragen die Richtung nicht (Backlog, Core-Format). Präzisierung in der Planphase, ratifiziert durch Freigabe (D24-Addendum).
- Auswahl Bug zuerst / Heck zuerst im Editor, wirkt beim nächsten „Plan“
- Gespeichert im Plan (D24), nicht in settings.json
- Core unverändert: `LoadPlanner.Plan` nimmt `fillOrder` schon

**Akzeptanz:** „Heck zuerst“ → Plan → Kisten beginnen am Heck; Neustart behält die Richtung (plan.json); der Editor startet mit der Richtung des aktuellen Plans.

### [x] T4.7a Werkzeugfenster-Theme: Fluent-Spike + Musterseite
**Warum:** Settings-Fenster, Editor und Picker stehen im hellen Systemlook (D34) und blenden über dem dunklen Spiel. Drei feste Modi light/dark/system; das Overlay bleibt `appearance` (D34).
**Präzisiert in der Planung:** zuerst Fluent-Spike (`Window.ThemeMode`, nur auf Werkzeugfenstern, `Application.ThemeMode` bleibt `None`), Timebox halbe Session; Weiche F (Fluent) / E (eigene Templates) nach sieben Kriterien: 1 Overlay und Lock-Fenster unverändert, Gear-Tooltip im Systemlook; 2 jedes verwendete Control in light und dark lesbar (DataGrid, ComboBox-Popup, ScrollBar, ToolTip, Kontextmenü, Picker-Popup); 3 `System` folgt live, Titelleiste mit; 4 Settings-Fenster bei 400 px ohne Layoutbruch; 5 Akzent überschreibbar, `WPF0001` lokal per `#pragma`; 6 Markierungsfarben (ungültig, Problem, Warnung) folgen dem Modus; 7 **Martins Blick im Spiel** („Overlay oder Windows?“). **Die Session hält nach dem Spike an**, erst nach Martins Urteil geht es mit E oder F weiter. Weg E: Palette als C#-Record ohne Defaults (Compiler erzwingt gleiche Keys), Templates in einem pro Werkzeugfenster gemergten `ToolStyles.xaml` (nicht App.xaml, sonst trifft es Overlay-Tooltips), `WM_SETTINGCHANGE` am HwndSource mit Neulesen von `AppsUseLightTheme`, DWM-Titelleiste (`DWMWA_USE_IMMERSIVE_DARK_MODE` = 20, Fehler ignoriert), High Contrast → Systemlook, Kontrast-Test ≥ 4,5:1; reicht die Session nicht, Schnitt in 4.7a/4.7a2 (ScrollBar, DataGrid, ContextMenu, Popup). Präzisierung in der Planphase, ratifiziert durch Freigabe.
**Präzisiert nach dem Spike (Weg F, Akzent Teal fest):** `Settings/ToolWindowTheme` (Enum System/Light/Dark, WPF-frei; Parsen erst 4.7b); `Theme/ToolWindowThemer.Apply` setzt `ThemeMode` pro Fenster (einzige `WPF0001`-Stelle) und Teal über die sieben `SystemColors.AccentColor*Key`; Muster für eigene Styles: `Apply` vor `InitializeComponent`, `BasedOn` Fluent, eigene Styles eine Ebene unter dem Fenster (sonst kein Base), DataGrid-Zeilen `BasedOn` `DefaultDataGridRowStyle`; `HueSliderStyle` in App.xaml; deaktivierte Slider/TextBox mit Opacity 0.4; Popup über `FlyoutBackground`; Markierungen über `SystemFillColor*`; Fluent kann Zeilen nicht tönen, eine ungültige Zeile bekommt deshalb einen 1-px-Rahmen in `SystemFillColorCriticalBrush` (statt roter Schrift); Musterseite öffnet neben dem Overlay statt allein (Gate 1 und 7). Präzisierung in der Planphase, ratifiziert durch Freigabe (D36).
- Musterseite `--theme-sample`, nur im Debug-Build: jedes Control der Werkzeugfenster, Moduswahl light/dark/system live
- Spike-Ergebnis pro Kriterium in D36

**Akzeptanz:** Kriterien 1–6 belegt; Martin nimmt die Musterseite in beiden Modi im Spiel ab; `--theme-sample` fehlt im Release-Build; Core unverändert.

### [x] T4.7b Werkzeugfenster-Theme: Setting + Verdrahtung
**Warum:** Erst mit Setting und Verdrahtung sieht der Spieler den Modus in Settings-Fenster, Editor und Picker.
**Präzisiert in der Planung:** Top-Level `"toolWindowTheme"` (light|dark|system, klein), optional, kein Schema-Bump; `null` und fehlend = `system` ohne Notice (wie `legend: null`, T4.3), ungültiger String → `system` + Notice wie `legend.position`; nicht unter `appearance`, damit „Reset to defaults“ ihn nicht mitnimmt; `Settings/ToolWindowTheme` WPF-frei (Names/TryParse/Resolve), die WPF-Seite heißt `ToolWindowThemer`; Abschnitt „Tool windows“ nach Buttons, vor Hotkeys, ComboBox „Follow Windows / Light / Dark“, Apply-on-change (D30), kein Reset. Präzisierung in der Planphase, ratifiziert durch Freigabe.
**Präzisiert in der Umsetzungsplanung:** kein `Resolve` (nur Weg E brauchte es, Fluent folgt Windows selbst); `OverlayWindow` schaltet die offenen Werkzeugfenster live um, weil es beide besitzt, neue Fenster bekommen den Modus im Konstruktor; die Musterseite schaltet nur noch sich selbst (sonst kämpft sie gegen das Setting); `AppSettings.ToolWindowTheme` ist ein String, geparst über `ToolWindowThemes` (Names/TryParse/Parse/ToName) neben dem Enum. Präzisierung in der Planphase, ratifiziert durch Freigabe (D36).
**Präzisiert nach der Abnahme:** Text im Dark-Modus grau (`#D0D0D0`, nach Augenmaß aus drei Kandidaten) statt Fluents reinem Weiß; dafür kommt `Resolve` zurück (Registry wie Fluent) und `SystemEvents.UserPreferenceChanged` hält „Follow Windows“ live; Fokusrahmen bleibt weiß, High Contrast unangetastet. Präzisierung in der Planphase, ratifiziert durch Freigabe (D36, Nachtrag).
- `SettingsSession.ChangeToolWindowTheme`, entprellt gespeichert wie Hotkeys
- Settings-Fenster, Editor und Picker-Popup folgen dem Modus; Datei-Dialoge und MessageBox bleiben System
- settings-format.md, README, D36

**Akzeptanz:** Modus im Fenster wechseln → Settings, Editor und Picker folgen sofort, Neustart behält ihn; „system“ folgt dem Windows-Wechsel live; Reset eines Abschnitts lässt den Modus stehen; `"toolWindowTheme": "blue"` → system + Notice, `null` → system ohne Notice; Overlay unverändert; Core unverändert.

---

