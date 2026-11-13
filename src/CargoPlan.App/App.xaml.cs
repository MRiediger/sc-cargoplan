using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CargoPlan.App.Editor;
using CargoPlan.App.Holo;
using CargoPlan.App.Hotkeys;
using CargoPlan.App.Plans;
using CargoPlan.App.Scanning;
using CargoPlan.App.Settings;
using CargoPlan.App.Updates;
using CargoPlan.App.ViewModels;
using CargoPlan.App.Win32;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Ocr;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App;

/// <summary>
/// Composition root. Two windows, one view model: wired by hand, a DI container would add a package
/// and indirection for nothing.
/// </summary>
public partial class App : Application
{
    private const int ExitShipsFolderBroken = 1;

    private const int ExitDataFolderUnusable = 2;

    private CursorStateMonitor? _cursorMonitor;

    // Kept so the composition root visibly owns it; the window's events would keep it alive anyway.
    private OverlayBoundsSaver? _boundsSaver;

    // Same here: the overlay's events keep it alive, the field shows who owns it.
    private HotkeyService? _hotkeys;

    // Same here: the view model's and the overlay's events keep it alive.
    private PlanSaver? _planSaver;

    // The holograms (D63): the overlay's only network access. One HttpClient for the whole run, not disposed when the
    // overlay closes: a fetch still running would then hit a disposed client; the process ends right after.
    // Source: https://learn.microsoft.com/dotnet/fundamentals/networking/http/httpclient-guidelines
    private HoloLoader? _holoLoader;

    // The contract scanner (T10.1b): its engine lives for the whole run, like the hologram loader's client.
    private ContractScanner? _scanner;

    // Only with --scan-probe; its timer keeps it alive, the field shows who owns it.
    private Diagnostics.ScanProbe? _scanProbe;

    // The update check (D109): one per run, after the first frame. Cancelled when the overlay closes or the app exits, so its
    // result never reaches a window that is gone (Freigabe E1). Not disposed: a check still running holds its token, and the
    // process ends right after.
    private UpdateService? _updates;

    // The update line (T11.2): made with the service, the settings window's Check now offers into it.
    private UpdateViewModel? _updateLine;

    // This start's arguments: the restart after an update gets them again, without --plan (U5).
    private IReadOnlyList<string> _startArgs = [];

    private readonly CancellationTokenSource _closing = new();

    /// <summary>Update.exe started this process, after a swap or after a failed one (Program, VelopackApp.OnRestarted).</summary>
    internal bool RestartedByUpdate { get; init; }

    /// <summary>Velopack's lines into cargoplan.log, from the moment the data folder is known (D109).</summary>
    internal VelopackLogBridge? VelopackLog { get; init; }

    // What the dispatcher handler logged last: left unhandled, it reaches AppDomain.UnhandledException too, logged once.
    private Exception? _crashLogged;

    /// <summary>
    /// The start in its steps, in this order (F22): each Compose method wires one part; their order is the order of the
    /// side effects and must not change (bounds before Show, the plan saver before the start plan, the overlay before the
    /// lock window).
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Before anything reads a path, the crash log included: --data-dir moves every file of the run (T10.4a, B1).
        var options = StartOptions.Parse(e.Args);
        _startArgs = e.Args;
        if (!UseDataFolderOrExit(options))
        {
            return;
        }

        ComposeCrashLogging();
        VelopackLog?.Attach();

        if (LoadShipsOrExit() is not { } ships)
        {
            return;
        }

        // Once per start, not per ship change (200 KB). A broken file costs the outlines, never the start (D62).
        var outlines = ShipOutlineLoader.Load();

        // The measuring tool instead of the overlay (Phase 8): before settings and plan, so it touches neither.
        if (options.MeasureFrames is { } measured)
        {
            // With the engine warming from the first frame like the overlay's (Gate 3); its notices go to the log only.
            var measuring = new Diagnostics.FrameMeasureWindow(ships, measured, options.OverGame) { ScanLoad = options.ScanLoad };
            var scanner = measuring.Scanner = CreateScanner(notice: _ => { }, options.ScanLoad ? ProbeTarget() : null);
            WarmAfterFirstFrame(measuring, scanner);
            measuring.Show();
            return;
        }

        var loaded = SettingsStore.Load(SettingsStore.DefaultPath);
        var settings = loaded.Settings;
        var parts = ComposeOverlay(ships, outlines, options, loaded);
        ComposeHotkeys(parts, settings);
        ComposeStartPlan(parts, options);
        ComposeSettingsFollowers(parts);
        ComposeHolograms(parts);
        ComposeUpdates(parts, options);

        // Without a scan key the OCR loads only when a key is set or the editor's Scan button is pressed (D108).
        if (options.WarmsOcrAtStart(_hotkeys?.HasScanKey == true))
        {
            WarmAfterFirstFrame(parts.Overlay, parts.Scanner);
        }
        else
        {
            AppLog.Write("OCR", "not loaded at start: no scan key");
        }

        if (options.ScanProbe is { } series)
        {
            // Next to the overlay, not instead of it: Gate 1 measures the scan as the player will have it (T10.1b).
            _scanProbe = new Diagnostics.ScanProbe(parts.Scanner, parts.Overlay, series, options.ScanProbeImages);
        }

        ShowWindows(parts, options);
    }

    /// <summary>
    /// <c>--data-dir</c>: its folder for every file of the run. A refused or unusable folder ends the app with one message,
    /// never the player's profile instead (T10.1b); false then. Without the switch nothing happens.
    /// </summary>
    private bool UseDataFolderOrExit(StartOptions options)
    {
        string? problem = options.DataDirRefused ? options.Problem : null;
        if (options.DataDir is { } folder)
        {
            try
            {
                AppFolders.Use(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problem = $"{StartOptions.DataDirSwitch} {folder}: {ex.Message}";
            }
        }

        if (problem is null)
        {
            return true;
        }

        MessageBox.Show(problem, "SC-CargoPlan", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(ExitDataFolderUnusable);
        return false;
    }

    /// <summary>First after the data folder, so nothing after goes unlogged (D82).</summary>
    private void ComposeCrashLogging()
    {
        // The dispatcher's: log, say so, and end as before (Handled stays false). Other threads': the runtime ends the
        // process anyway, so only the log.
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.application.dispatcherunhandledexception
        // Source: https://learn.microsoft.com/dotnet/api/system.appdomain.unhandledexception
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Without the ships folder there is nothing to draw: one message, then the end, no crash dialog (D56); null then.
    /// The catalog's problems (skipped ship files) are shown in the header's ship selection (T7.2b).
    /// </summary>
    private ShipCatalog? LoadShipsOrExit()
    {
        try
        {
            return ShipLoader.LoadAll();
        }
        catch (ShipFolderException ex)
        {
            MessageBox.Show(ex.Message, "SC-CargoPlan", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(ExitShipsFolderBroken);
            return null;
        }
    }

    /// <summary>What the later steps of the start wire together.</summary>
    private sealed record OverlayParts(
        Ship Ship, OverlayViewModel ViewModel, SettingsSession Session, OverlayWindow Overlay, ContractScanner Scanner, ScanRunner Scan);

    /// <summary>
    /// The view model, the settings session, the scanner with its runner and the overlay window, placed but not shown. The
    /// scanner comes before the window: the editor's Scan button needs it (T10.3); it has no side effect before the
    /// warm-up, which waits for the first frame, a scan key being set or the first scan (D108).
    /// </summary>
    private OverlayParts ComposeOverlay(
        ShipCatalog ships, ShipOutlineCatalog outlines, StartOptions options, SettingsLoad loaded)
    {
        var settings = loaded.Settings;
        // The settings' ship, else the start plan's, else the Hull A; one that doesn't load never stops the start (T7.2).
        var (ship, shipNotice) = ShipChoices.Resolve(ships, settings.Ship, PlanStore.PeekShipId(options.PlanPath ?? PlanStore.DefaultPath));
        var viewModel = new OverlayViewModel(ship, ships)
        {
            IsManuallyLocked = options.Locked,
            RecentShips = settings.RecentShips ?? [],
            Cameras = settings.ShipView.Cameras ?? new Dictionary<string, ShipCamera>(),
            Outlines = outlines,
        };
        viewModel.SetNotice(NoticeSource.Settings, loaded.Problem);
        viewModel.SetNotice(NoticeSource.Outlines, OverlayViewModel.OutlinesNoticeText(outlines.Problems));
        viewModel.SetNotice(NoticeSource.Ship, shipNotice);

        // Before any window: their DynamicResources find the theme from the first layout on. The settings window
        // changes the look through the same session (D34).
        var session = new SettingsSession(Resources, viewModel, SettingsStore.DefaultPath, settings);

        // A hotkey the migration to schema 3 took is said once (T10.4a): the file is written now, the old one stays as the
        // backup, and the hint stays for the run (B14). Every other load writes nothing until a change (D31).
        if (loaded.Removed is { } removed)
        {
            session.WriteBack(removed, loaded.Problem);
        }

        // Bounds before Show(), so the window appears in place instead of jumping there. The hotkeys' view model is made
        // when the settings window opens; by then _hotkeys exists (it needs the overlay, so it comes after it).

        // The scanner's notices and the scan's changes of the plan come from pool threads; the overlay lives on the UI
        // thread. Source: https://learn.microsoft.com/dotnet/api/system.windows.threading.dispatcheroperation.task
        var scanner = CreateScanner(
            text => Dispatcher.InvokeAsync(() => viewModel.SetNotice(NoticeSource.Scan, text)),
            options.ScanProbe is null ? null : ProbeTarget(),
            options.KeepScans);
        ScanPictures? pictures = null;
        if (options.KeepScans)
        {
            pictures = new ScanPictures(ScanPictures.DefaultFolder);
            AppLog.Write("Scan", $"keeping the last {ScanPictures.MaxPictures} scan pictures in {ScanPictures.DefaultFolder} ({StartOptions.KeepScansSwitch})");
        }

        var scan = new ScanRunner(
            () => scanner.ScanAsync(), viewModel, new ScanLog(ScanLog.DefaultPath), action => Dispatcher.InvokeAsync(action).Task, pictures: pictures);
        var overlay = new OverlayWindow(
            viewModel,
            session,
            () => CreateHotkeysViewModel(session),
            () => CreateUpdateSettingsViewModel(session),
            scan.Start);
        if (settings.Overlay is { } saved)
        {
            RestoreBounds(overlay, saved);
        }

        // The saver's Closed handler goes first and hands over pending bounds; Flush then writes a pending look change.
        _boundsSaver = new OverlayBoundsSaver(overlay, session);
        overlay.Closed += (_, _) => session.Flush();
        return new OverlayParts(ship, viewModel, session, overlay, scanner, scan);
    }

    /// <summary>
    /// Polled from here on (D97), before the settings window can ask for them. The scan's is a plain command: the runner
    /// guards itself, and a press during a scan must reach the scanner to say "Scan running" (D104).
    /// </summary>
    private void ComposeHotkeys(OverlayParts parts, AppSettings settings)
    {
        _hotkeys = new HotkeyService(
            parts.Overlay,
            settings.Hotkeys,
            parts.ViewModel,
            new RelayCommand(parts.Scan.Start),
            new ScanKeyCheck(ScanKeyCheck.IsContractTrackerRunning));
    }

    /// <summary>
    /// The plan saver before the start plan is shown: a --plan file becomes plan.json this way, without a rule of its own.
    /// A plan from plan.json is written back once, unchanged. The saver learns from the load whether plan.json couldn't be
    /// read, and keeps it aside before its first write (B13, D106). An older file's ContractTracker captures come in as scans,
    /// and the Plan notice says so (D107).
    /// </summary>
    private void ComposeStartPlan(OverlayParts parts, StartOptions options)
    {
        var viewModel = parts.ViewModel;
        var loaded = PlanStore.LoadStart(PlanStore.DefaultPath, options.PlanPath, parts.Ship);
        _planSaver = new PlanSaver(parts.Overlay, viewModel, PlanStore.DefaultPath, loaded.Unreadable, loaded.Notice);
        viewModel.SetNotice(NoticeSource.Plan, options.Problem ?? loaded.Problem ?? loaded.Notice);
        if (loaded.Plan is { } plan)
        {
            viewModel.LastScanNo = loaded.LastScanNo;
            viewModel.ShowPlan(plan);
        }
    }

    /// <summary>After the start: the fallback or the plan's ship isn't a choice and stays out of settings.json (T7.2).</summary>
    private static void ComposeSettingsFollowers(OverlayParts parts)
    {
        var (viewModel, session) = (parts.ViewModel, parts.Session);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OverlayViewModel.Ship))
            {
                session.ChangeShip(viewModel.Ship.Id, viewModel.RecentShips);
            }
            else if (e.PropertyName == nameof(OverlayViewModel.Cameras))
            {
                session.ChangeCameras(viewModel.Cameras);
            }
        };
    }

    /// <summary>
    /// The hologram of the ship and look (D63): once the overlay is up, on a ship change, on a new look or switch.
    /// Every request cancels the one before; the outline shows until the hologram is there.
    /// </summary>
    private void ComposeHolograms(OverlayParts parts)
    {
        var (viewModel, session, overlay) = (parts.ViewModel, parts.Session, parts.Overlay);
        var holoLoader = _holoLoader = new HoloLoader(viewModel, HoloSource.CreateDefault());

        // Another tag's cache (holo-v1 of v0.2.0) goes at once when holograms aren't wanted now; else after the current
        // tag's manifest is checked (D95).
        holoLoader.RetireUnneeded(session.ShipOutline, session.DownloadShipHolograms);
        overlay.Loaded += async (_, _) => await RequestHologram();
        session.ShipViewChanged += async (_, _) => await RequestHologram();
        viewModel.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(OverlayViewModel.Ship))
            {
                await RequestHologram();
            }
        };

        // Handlers are async void (CLAUDE.md: event handlers only); the loader ends every expected failure in the
        // outline and a notice, anything else is a bug and surfaces.
        Task RequestHologram() => holoLoader.RequestAsync(viewModel.Ship, session.ShipOutline, session.DownloadShipHolograms);
    }

    /// <summary>
    /// The update line (T11.1, T11.2, D109). After the overlay's first frame (E5): the start time goes to the log; a swap
    /// that failed before this restart shows as such; then the start's check runs on the pool and its offer becomes the
    /// line. Settings are read on the UI thread at that moment. The overlay closing cancels a check or download still
    /// running (Freigabe E1), after the bounds, settings and plan have flushed (their Closed handlers come first).
    /// </summary>
    private void ComposeUpdates(OverlayParts parts, StartOptions options)
    {
        var (viewModel, session, overlay) = (parts.ViewModel, parts.Session, parts.Overlay);
        var updates = _updates = new UpdateService(options.UpdatesSource);
        var line = _updateLine = new UpdateViewModel(
            new UpdateActions(
                (version, progress) => updates.DownloadAsync(version, progress, _closing.Token),
                version => ApplyAndShutDownAsync(updates, version, overlay),
                session.ChangeSkippedVersion),
            action => Dispatcher.InvokeAsync(action));
        viewModel.Update = line;
        overlay.Closed += (_, _) => _closing.Cancel();

        // Once: ContentRendered follows the first frame. An event handler, so async void is allowed (CLAUDE.md); the check
        // ends every failure in a log line, anything else is a bug and surfaces.
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.window.contentrendered
        async void Check(object? sender, EventArgs e)
        {
            overlay.ContentRendered -= Check;
            LogStartTime();

            // Back on the UI thread after each await: it resumes in the dispatcher's context.
            if (await updates.FailedApplyAsync(RestartedByUpdate, AppVersion.Current) is { } failed && IsRunning())
            {
                line.ShowFailedApply(failed.Version, failed.Reason);
            }

            var offer = await updates.CheckAsync(session.Current.Updates, AppVersion.Current, AppVersion.IsDevBuild, _closing.Token);
            if (offer is not null && IsRunning())
            {
                line.Offer(offer.Version);
            }
        }

        overlay.ContentRendered += Check;
    }

    private bool IsRunning() => !Dispatcher.HasShutdownStarted && !_closing.IsCancellationRequested;

    /// <summary>
    /// The download is there (U4): Update.exe starts and waits for this process, then the app shuts down so every Closed
    /// handler flushes (bounds, settings, plan) before Update.exe's 60 s run out (Freigabe E2). null = shutting down, else
    /// why not; the app then runs on.
    /// </summary>
    private async Task<string?> ApplyAndShutDownAsync(UpdateService updates, string version, Window overlay)
    {
        // A dialog opened during the download first: Shutdown inside its loop would close the windows under it.
        await UpdateShutdown.WhenNotModalAsync();
        if (!IsRunning())
        {
            return UpdateFailure.Cancelled;
        }

        if (updates.Apply(version, StartOptions.RestartArguments(_startArgs)) is { } reason)
        {
            return reason;
        }

        // Editor and settings first, each by its own Close; then Shutdown closes the overlay, whose Closed handlers flush.
        // Shutdown closes every window with InternalClose(shutdown: true, ignoreCancel: true), Closed included; no window
        // here handles Closing anyway.
        // Source: https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Application.cs (DoShutdown)
        UpdateShutdown.CloseAllBut(Windows.Cast<Window>(), overlay);
        Shutdown();
        return null;
    }

    /// <summary>
    /// The settings window's Updates section (U8, U9), made per window like the hotkeys'. Check now offers into the overlay's
    /// line; its own status says what came out.
    /// </summary>
    private UpdateSettingsViewModel CreateUpdateSettingsViewModel(SettingsSession session)
    {
        var updates = _updates ?? throw new InvalidOperationException("The settings window opened before the update service existed.");
        var line = _updateLine ?? throw new InvalidOperationException("The settings window opened before the update line existed.");
        return new UpdateSettingsViewModel(
            session.Current.Updates,
            session.ChangeCheckOnStartup,
            session.ChangeSkippedVersion,
            CheckNowAsync,
            handler => session.UpdatesChanged += handler,
            handler => session.UpdatesChanged -= handler);

        async Task<string> CheckNowAsync()
        {
            var result = await updates.CheckNowAsync(session.Current.Updates.SkippedVersion, AppVersion.Current, AppVersion.IsDevBuild, _closing.Token);
            if (result.Offer is { } offer && IsRunning())
            {
                line.Offer(offer.Version);
            }

            return result.Status;
        }
    }

    /// <summary>
    /// Process start to the overlay's first frame, one line per start: the measure of the start-time gates from T11.1 on
    /// (S11.0 G8 measured it with throwaway code). Through Velopack's stub the stub's own start comes before it (≈ 25 ms).
    /// Whether the app runs elevated comes last (E1): after a swap with UAC it should run as before; measure-start.ps1 reads
    /// only the milliseconds.
    /// </summary>
    private static void LogStartTime()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var elapsed = DateTime.Now - process.StartTime;
        var elevated = UpdateEnvironment.IsAdministrator() ? "yes" : "no";
        AppLog.Write("Start", string.Create(CultureInfo.InvariantCulture, $"overlay first frame {elapsed.TotalMilliseconds:F0} ms after the process start, elevated: {elevated}"));
    }

    /// <summary>
    /// The contract scanner (T10.1b, D99), not yet warm: <see cref="WarmAfterFirstFrame"/> starts that. Missing models end in
    /// a notice, never in a failed start (plan decision 3).
    /// </summary>
    /// <param name="notice">The scan's notice line; called from pool threads.</param>
    /// <param name="find">Another window than the game's, for a probe's dry run; null = Star Citizen.</param>
    /// <param name="keepPictures"><c>--keep-scans</c>: each attempt carries its picture (T10.5).</param>
    private ContractScanner CreateScanner(Action<string?> notice, Func<GameWindow?>? find = null, bool keepPictures = false)
    {
        // Our windows over the contract leave the picture for the capture (D100, B12). The window list is read at each
        // capture, so windows made after the scanner count too. A failed cloak isn't thrown: the window would only be in
        // the picture, the scan still runs; it is logged.
        var clearance = new RegionClearance(Dispatcher, () => Windows.Cast<Window>(), (handle, cloak) =>
        {
            if (NativeMethods.SetCloak(handle, cloak) is var result and not 0)
            {
                AppLog.Write("Scan", string.Create(CultureInfo.InvariantCulture, $"DWMWA_CLOAK {cloak} failed: 0x{result:X8}"));
            }
        });
        return _scanner = new ContractScanner(OcrEngine.Load, notice, find, clear: clearance.Clear, keepPictures: keepPictures);
    }

    /// <summary>
    /// The OCR engine loads and warms on a pool thread once <paramref name="window"/> has drawn its first frame, so the start
    /// is not a frame later than before (T10.1b plan decision 2).
    /// </summary>
    private static void WarmAfterFirstFrame(Window window, ContractScanner scanner)
    {
        // Once: ContentRendered follows the first frame. Source: https://learn.microsoft.com/dotnet/api/system.windows.window.contentrendered
        void Start(object? sender, EventArgs e)
        {
            window.ContentRendered -= Start;
            scanner.StartWarmup();
        }

        window.ContentRendered += Start;
    }

    /// <summary>
    /// The probe reads another process's window when CARGOPLAN_SC_CAPTURE_PROCESS names one (e.g. "Code"): a dry run of the
    /// whole path without the game, as in OverlayCaptureTests. Only with --scan-probe or --scan-load; the hotkey always reads
    /// the game.
    /// </summary>
    private static Func<GameWindow?>? ProbeTarget() =>
        Environment.GetEnvironmentVariable("CARGOPLAN_SC_CAPTURE_PROCESS") is { Length: > 0 } process ? () => GameWindow.Find(process) : null;

    /// <summary>The overlay first: the lock window's Owner must be a window that has been shown.</summary>
    private void ShowWindows(OverlayParts parts, StartOptions options)
    {
        var (viewModel, overlay) = (parts.ViewModel, parts.Overlay);
        overlay.Show();
        new LockWindow(viewModel, overlay).Show();

#if DEBUG
        // Next to the overlay, not instead of it: a tool window theme is judged against the overlay (T4.7a).
        if (options.ThemeSample)
        {
            new ThemeSampleWindow(overlay).Show();
        }
#endif

        _cursorMonitor = new CursorStateMonitor(viewModel);
        _cursorMonitor.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _closing.Cancel();
        _cursorMonitor?.Stop();
        base.OnExit(e);
    }

    /// <summary>The crash dialog's text: what went wrong and where the details are (D82).</summary>
    public static string CrashText(Exception error) =>
        $"SC-CargoPlan has to close: {error.GetType().Name}: {error.Message}{Environment.NewLine}{Environment.NewLine}Details: {AppLog.Path}";

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _crashLogged = e.Exception;
        AppLog.Write("Unhandled", e.Exception);
        MessageBox.Show(CrashText(e.Exception), "SC-CargoPlan", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var error = e.ExceptionObject as Exception ?? new InvalidOperationException($"Unhandled non-exception: {e.ExceptionObject}");
        if (!ReferenceEquals(error, _crashLogged))
        {
            AppLog.Write("Unhandled (other thread)", error);
        }
    }

    /// <summary>
    /// The settings window's hotkeys (D35): set and saved at once. Polled hotkeys can't be taken by another program (D97),
    /// so only the rules and the other action's combination refuse one, both in the view model. A scan key set warms the
    /// OCR now, not on its first press (D108); clearing it leaves a loaded engine until the next start.
    /// </summary>
    private HotkeysViewModel CreateHotkeysViewModel(SettingsSession session)
    {
        var hotkeys = _hotkeys ?? throw new InvalidOperationException("The settings window opened before the hotkeys existed.");
        return new HotkeysViewModel(
            session.Current.Hotkeys,
            (action, hotkey) =>
            {
                hotkeys.Set(action, hotkey);
                session.ChangeHotkey(action, hotkey);
                if (action == HotkeyAction.ScanContracts && hotkey is not null)
                {
                    _scanner?.StartWarmup();
                }

                return null;
            },
            hotkeys.Suspend,
            hotkeys.Resume);
    }

    /// <summary>Saved bounds may lie on an unplugged monitor: clamped into today's virtual desktop.</summary>
    private static void RestoreBounds(Window window, WindowBounds saved)
    {
        Rect screen = new(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        var bounds = WindowBoundsRestore.Clamp(saved.ToRect(), screen, new Size(window.MinWidth, window.MinHeight));
        window.Left = bounds.Left;
        window.Top = bounds.Top;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
    }
}
