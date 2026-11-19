# Archiv: Bugfix B11 App-Tests hängen

Wortgleich aus [`TASKS.md`](../../TASKS.md) verschoben am 19.11.2026 (K11, Stand `a735ba6`); Index und offene Tasks stehen dort.

## Bugfix B11 App-Tests hängen (09.11.2026) – Branch `fix/app-tests-hang`

### [x] B11 App-Tests hängen lokal (D98)

**Befund:** reproduziert, **5 von 10 Läufen** (`--blame-hang-timeout 60s`, Release, `main` `8acab33`), jeder Hänger mitten
in der Suite (1 562–1 667 von 1 701 bestanden), also nicht H3. Dumps (`dotnet-dump`: `clrthreads`, `syncblk`, `clrstack -all`;
`cdb`: `~*kc`, `!locks`) zeigen in allen 5 Läufen denselben Zyklus, **H1 belegt** (H2: kein `PushFrame`; H4: kein statischer
Konstruktor), ABBA in `d3d9.dll`:

```
WPF-Render-Thread   hält g_D3DDeviceManager + d3d9-Enum-Sperre, wartet auf d3d9!g_hDLLGlobalCS
  CHwDisplayRenderTarget::Create → CD3DDeviceManager::CreateNewDevice → d3d9!CEnum::CreateDeviceEx → CSwapChain::SetCooperativeLevel
LookSceneTests (STA) hält d3d9!g_hDLLGlobalCS, wartet auf die Enum-Sperre
  ViewPictures.Render → RenderTargetBitmap.Render → CMilViewport3DVisual::RenderContent → CHw3DSoftwareSurface::Create
  → d3d9!CBaseDevice::CreateAdditionalSwapChain → CEnum::CheckExclusiveMode
ShipScene3DTests (STA) hält g_csCompositionEngine, wartet per SyncFlush auf den Render-Thread; alle übrigen WPF-Threads
  (auch SettingsSessionTests auf einem xUnit-Thread) warten auf g_csCompositionEngine oder g_D3DDeviceManager
ToolComboBoxStyleTests.Layout_drop_downs_… in Window.Show() → HwndTarget → SyncFlush: in allen 5 Dumps (das echte Fenster)
```

Ursache: Ein echtes Fenster oder Popup bekommt mit GPU ein D3D9-Hardwaregerät, gleichzeitig rendert ein anderer Test
eine 3D-Szene per `RenderTargetBitmap` über D3D9; die Testklassen laufen parallel. Kein Produktcode beteiligt. CI hat
keine GPU (Vermutung, warum CI nie hing).

**Abhilfe:** `Sta.RenderInSoftware` (`[ModuleInitializer]`) setzt `RenderOptions.ProcessRenderMode = SoftwareOnly` für den
Testprozess (D98, CLAUDE.md A12), Commit `0f29d5b`. Collection `WPF` als geprüfte Alternative in D98.

**Präzisierungen in der Planphase, ratifiziert durch Freigabe:** Vorversuch mit `SoftwareOnly` vor dem Collection-Umbau
(0 Hänger → die Zeile statt der Collection); die Zeile in `Sta` statt im `TestLog`-Initialisierer; D98 für B11, T10.1
und Review F24 auf D99 umgestellt.

| Messung (lokal, Release, `--no-build`) | vorher | Vorversuch | nachher |
|---|---:|---:|---:|
| Läufe / Hänger | 10 / **5** | 10 / 0 | 20 / **0** |
| App-Dauer vollständiger Läufe (TRX, Median) | 3,52 s | 3,59 s | 3,60 s |
| Wandzeit `dotnet test` (Median) | 4,19 s | 4,26 s | 4,27 s |
| `ShipPictureTests` mit/ohne die Zeile | – | 728 / 728 Bilder identisch | – |
| Tests Core / Import / App | 1 817 / 164 / 1 701 | – | 1 817 / 164 / 1 701 |

Rohdaten, Stacks und Dumps lokal unter `out/b11/` (nicht im Repo).

**Nebenbefund, auf Martins Wunsch im selben Branch behoben:** 2 der 20 Nachher-Läufe mit je einem roten Log-Test
(`PlanStoreTests`/`SettingsStoreTests.Broken_file_writes_its_notice_to_the_log`): `TestLog.Text()` las mit
`File.ReadAllText` (`FileShare.Read`) außerhalb des `lock` von `AppLog`; ein paralleler Schreiber ließ das Lesen werfen
(`IOException`) oder verlor seine Zeile (`AppLog` schluckt bewusst). Seit K10-F9. Jetzt liest `TestLog.Text()` mit
`FileShare.ReadWrite` (die Rotation bei 1 MB bliebe blockiert, ein Testlauf schreibt höchstens 23 KB): **20 von 20 Läufen
grün**, App-Dauer 3,58 s (TRX-Median).

