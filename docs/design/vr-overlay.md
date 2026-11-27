# SC-CargoPlan – VR-Modus: Erkenntnisse & Design

Stand: 27. November 2026 (Alpha 4.10.x, Martins Kette Luma Ultra ← VRto3D ← SteamVR ← SC/OpenXR). Grundlage: Versuch mit
Desktop+ am 26.11. und Spike S14.0 am 27.11. (Befund in [`TASKS.md`](../../TASKS.md), Abschnitt „S14.0 VR-Spike“; die
Belege aus `evidence\` sind nach der Abnahme verloren gegangen, es zählt der Befund). **D-Einträge gehen vor**, vor allem
[D131](../decisions.md).

## 0. Invarianten (Design-Leitplanken)

1. **Das Desktop-Fenster bleibt unverändert.** Das Overlay sieht aus und wird bedient wie heute; VR ist ein Spiegel.
2. **Kein neuer Pflichtschritt.** Laden, Abhaken, F3 laufen wie heute; wer kein VR hat, merkt nichts.
3. **VR aus = heutiges Verhalten.** `vr` fehlt in settings.json → aus; keine OpenVR-DLL geladen, kein Thread, kein
   Overlay.
4. **CargoPlan startet SteamVR nie** (`VRApplication_Background`) und macht keinen Netzzugriff.
5. **Nichts im Spielprozess** (EAC): CargoPlan ist ein eigener Prozess, der per IPC mit `vrserver` spricht.

## 1. Zielbild

Der Hauler sitzt in VR am Frachtaufzug und sieht den Ladeplan als flaches Fenster rechts neben dem Blick (Default 30°,
1,2 m, 0,5 m breit). Wo er ihn haben will, legt er per Taste fest („Place in current view“, Recenter auf Numpad 5); die
Lage gilt relativ zu seinem Kopf, nicht zum Ursprung des Raums. Bedient wird am Desktop-Fenster: ESC-Menü in SC, die Maus
auf dem gespiegelten Fenster, der Cursor ist im VR-Bild zu sehen.

## 2. Spike-Befunde

### 2.1 Desktop+ (Phase-Start, Martin, 26.11., Desktop+ 0,5 m, Graphics Capture)

Das Overlay war in der Brille sichtbar, der Scan lief über den Monitor-Mirror. SCs Recenter verschiebt nur den Space des
Spiels, das Desktop+-Overlay bleibt stehen. Die Befunde sind nicht entscheidungstragend; sie haben gezeigt, dass ein
gespiegeltes Fenster in Martins Kette ankommt.

### 2.2 S14.0 (27.11.)

Gate-Tabellen, G2/G3-Messungen und offene Punkte stehen im Befund (TASKS.md, Abschnitt „S14.0 VR-Spike“, nach dem
Phasenende im Phasenarchiv). Hier nur die Kernaussagen:

| Gate | Kernaussage |
|---|---|
| G1 | sichtbar in beiden Augen über SC; Rot/Blau vertauscht, mit Tausch richtig; kein EAC-Hinweis |
| G2 | Pfad b 8,2 / 13 ms auf dem VR-Thread, 12,8 Hz; Pfad a 76 ms auf dem UI-Thread → raus |
| G3 | über dem Spiel kein Mehr an späten Frames (`vr@15` 11,5 % gegen `hull_c` 14,2 %), aber ≈ 10 % Takt |
| G4 | Recenter per Numpad 5 (`VK_NUMPAD5` / `VK_CLEAR`), Formel in D131 |
| G5 | Color-Key aus dem Fensterhintergrund, 91,9 % der Pixel transparent |
| G6 | SteamVR fehlt / endet / DLL fehlt: App läuft weiter; Quit ignoriert → SteamVR beendet die App nach 5,1 s |
| G7 | F3 mit laufendem Feed: zwei Scans `Added`, ein Feed-Takt ausgelassen |
| G8 | 392 Dateien, Delta 1,10 MB |

Zwei Befunde aus der Durchführung prägen die Architektur: `SetOverlayRaw` ist **asynchron** (`VREvent_ImageLoaded` auf
der Overlay-Queue), und **ein Overlay allein flackert** bei jedem Texturtausch – daher der Double-Buffer.

## 3. Architektur

### 3.1 Bausteine (App, `src/CargoPlan.App/Vr/`)

| Baustein | Aufgabe |
|---|---|
| `OpenVR/` | Valves `openvr_api.cs`, `openvr_api.dll`, `LICENSE` v2.15.6, byte-gleich (`-text`, `generated_code`) |
| `OpenVrSession` | Init als Background, Event-Schleife (`PollNextEvent`, `PollNextOverlayEvent`), Quit, Shutdown, Reconnect |
| `VrOverlayHost` | zwei Overlays (Double-Buffer), Breite, Alpha, Transform, Flip nach `ImageLoaded` |
| Feed | `PrintWindow PW_RENDERFULLCONTENT`, Rot/Blau-Tausch, Color-Key, Cursor, Dirty-Signal, ≤ 15 Hz |
| Placement | Kopf-Pose → Transform (reine Mathematik, testbar ohne SteamVR) |

Core bleibt frei von VR (Kernregel); die Transform-Mathematik liegt in der App, weil nur die App sie braucht (A4).

### 3.2 Thread-Modell

- **UI-Thread:** rendert das Fenster wie heute und setzt nur ein **Dirty-Signal**, wenn sich das Bild ändert (nicht
  `CompositionTarget.Rendering`, das feuert mit einem Handler jeden Frame). Er ruft OpenVR nie auf.
- **Aufnahme-Thread:** nimmt bei Dirty höchstens mit 15 Hz per `PrintWindow` auf, tauscht die Kanäle, setzt den Key,
  zeichnet den Cursor ein und reicht das fertige Bild weiter. Er ruft OpenVR nie auf.
- **VR-Thread:** besitzt jeden OpenVR-Aufruf. Er pollt Events (`PollNextEvent`, `PollNextOverlayEvent`), lädt das
  jeweils neueste Bild ins verborgene Overlay hoch und flippt nach `ImageLoaded`; ein Bild, das während eines offenen
  Uploads kommt, ersetzt das wartende (im Spike `skip.pending`).
- **Warum zwei Threads (T14.0-Befund):** `PrintWindow` wartet auf den UI-Thread (`WM_PRINT`; im Spike 511 und 1 555 ms
  beim Start). Hätte der VR-Thread die Aufnahme, hielte ein hängender UI-Thread auch `VREvent_Quit` fest, und SteamVR
  beendet die App nach ≈ 5 s. So hält ein hängender UI-Thread höchstens den Aufnahme-Thread; der VR-Thread quittiert
  den Quit sofort (Ziel < 1 s). Der Spike hatte einen Thread für beides.

### 3.3 Sequenzen

**Toggle an:**
1. Prüfen, ob `openvr_api.dll` geladen werden kann; fehlt sie → Notice, VR bleibt aus.
2. `VR_Init(VRApplication_Background)`; `Init_NoServerForBackgroundApp` → Notice „SteamVR is not running“, VR bleibt
   aus (kein Retry-Takt, nur erneutes Toggle).
3. Zwei Overlays anlegen; `KeyInUse` → Notice (zweite Instanz), Shutdown.
4. Platzierung aus der aktuellen Kopf-Pose und dem Offset der Settings anwenden; Feed starten.

**Recenter / „Place in current view“:**
1. HMD-Pose in `TrackingUniverseSeated` lesen, `headYaw = atan2(m2, m10)`, Kopfposition.
2. `overlayYaw = headYaw − userYaw`, `R = Ry(overlayYaw) · Rx(pitch)`, `t = Ry(overlayYaw) · (0, height, −distance) +
   Kopfposition`.
3. Transform auf beide Overlays; im Log roher HMD-Yaw und gesetzter Overlay-Yaw.

**`VREvent_SeatedZeroPoseReset`:** die zuletzt gewählte Platzierung neu anwenden (wie Recenter mit der neuen Pose).
Grund: unter VRto3D steht der Kopf nach dem Connect auf y = 1,5 m, 13–18 s später kommt der Reset und setzt ihn auf
(0, 0, 0), Yaw 0.

**Scan (F3):** solange die Scan-Aufnahme unsere Fenster cloakt (D100), lässt der Feed den Takt aus.

**Quit (`VREvent_Quit`) / Toggle aus:**
1. Feed anhalten, keine neue Aufnahme.
2. `AcknowledgeQuit_Exiting` (nur bei Quit), Overlays zerstören, `VR_Shutdown` – alles auf dem VR-Thread.
3. VR-Thread endet, die App läuft weiter; ein späteres Toggle verbindet im selben Prozess neu.

## 4. Grenzen und Known issues

- **Color-Key:** Inhalt in exakt der Hintergrundfarbe des Fensters wird in VR durchsichtig („content in the exact
  background color turns see-through in VR“). Kantenglättung bleibt deckend (8,1 % der Pixel in G5); Toleranz ±1 je
  Kanal prüft T14.2.
- **4096 px:** `SetOverlayRaw` lehnt eine Kante über 4096 px ab (`InvalidParameter`). Ein größeres Fenster bekommt eine
  Notice (verkleinern oder ablehnen entscheidet T14.2).
- **Takt:** über dem Spiel ≈ 10 % weniger fps mit Feed (53,5–56,4 gegen 57,0–61,4 fps in S14.0), am Desktop ein halber
  später Frame je Aufnahme. Kippt es, kommt Pfad c (Backlog, D3D11-Textur).
- **VRto3D-Piepton:** „No profile found for App SC-CargoPlan.exe“ mit Piepton bei jedem Fokuswechsel auf CargoPlan.
  Abhilfe: Profil `SC-CargoPlan.exe_config.json` neben dem StarCitizen-Profil oder den Hinweis in VRto3D abschalten
  (Wiki, T14.6). Ursache nicht belegt (vermutlich VRto3Ds Profilsuche).
- **Downgrade:** v0.8.0 kennt `vr` nicht und startet mit Defaults (`UnmappedMemberHandling.Disallow`); erst ab der
  Reader-Toleranz (T14.3a) hält eine Version fremde Abschnitte.
- **Zweite Instanz:** nur eine kann die Overlay-Schlüssel halten; die zweite meldet `KeyInUse`.

## 5. Offen

1. **Wer löst `SeatedZeroPoseReset` 13–18 s nach dem Connect aus** (Luma-Bridge, Desktop+)? Das Design hängt nicht davon
   ab: T14.3b reagiert auf das Event, statt auf ein Timing zu hoffen.
