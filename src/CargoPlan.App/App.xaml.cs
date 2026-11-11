using System.IO;
using System.Windows;
using System.Windows.Threading;
using CargoPlan.App.ContractTracker;
using CargoPlan.App.Editor;
using CargoPlan.App.Holo;
using CargoPlan.App.Hotkeys;
using CargoPlan.App.Plans;
using CargoPlan.App.Scanning;
using CargoPlan.App.Settings;
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

    private CursorStateMonitor? _cursorMonitor;

    // Kept so the composition root visibly owns it; the window's events would keep it alive anyway.
    private OverlayBoundsSaver? _boundsSaver;

    // Same here: the overlay's events keep it alive, the field shows who owns it.
    private HotkeyService? _hotkeys;

    // Same here: the view model's and the overlay's events keep it alive.
    private PlanSaver? _planSaver;

    // Same here: its timer and watcher keep it alive until the overlay closes.
    private ContractTrackerSync? _contractTracker;

    // The holograms (D63): the overlay's only network access. One HttpClient for the whole run, not disposed when the
    // overlay closes: a fetch still running would then hit a disposed client; the process ends right after.
    // Source: https://learn.microsoft.com/dotnet/fundamentals/networking/http/httpclient-guidelines
    private HoloLoader? _holoLoader;

    // The contract scanner (T10.1b): its engine lives for the whole run, like the hologram loader's client.
    private ContractScanner? _scanner;

    // Only with --scan-probe; its timer keeps it alive, the field shows who owns it.
    private Diagnostics.ScanProbe? _scanProbe;

    // What the dispatcher handler logged last: left unhandled, it reaches AppDomain.UnhandledException too, logged once.
    private Exception? _crashLogged;

    /// <summary>
    /// The start in its steps, in this order (F22): each Compose method wires one part; their order is the order of the
    /// side effects and must not change (bounds before Show, the plan saver before the start plan, the overlay before the
    /// lock window, the contract tracker's start last).
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ComposeCrashLogging();

        if (LoadShipsOrExit() is not { } ships)
        {
            return;
        }

        // Once per start, not per ship change (200 KB). A broken file costs the outlines, never the start (D62).
        var outlines = ShipOutlineLoader.Load();

        var options = StartOptions.Parse(e.Args);

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

        var (settings, settingsProblem) = SettingsStore.Load(SettingsStore.DefaultPath);
        var parts = ComposeOverlay(ships, outlines, options, settings, settingsProblem);
        ComposeHotkeys(parts, settings);
        ComposeStartPlan(parts, options);
        ComposeSettingsFollowers(parts);
        ComposeHolograms(parts);
        WarmAfterFirstFrame(parts.Overlay, parts.Scanner);
        if (options.ScanProbe is { } series)
        {
            // Next to the overlay, not instead of it: Gate 1 measures the scan as the player will have it (T10.1b).
            _scanProbe = new Diagnostics.ScanProbe(parts.Scanner, parts.Overlay, series, options.ScanProbeImages);
        }

        ShowWindows(parts, options);
    }

    /// <summary>First, so nothing after goes unlogged (D82).</summary>
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
        Ship Ship, OverlayViewModel ViewModel, SettingsSession Session, ContractTrackerSync ContractTracker, OverlayWindow Overlay,
        ContractScanner Scanner, ScanRunner Scan);

    /// <summary>
    /// The view model, the settings session, the ContractTracker sync, the scanner with its runner and the overlay window,
    /// placed but not shown. The scanner comes before the window like the sync: the editor's buttons need both (T10.3); it
    /// has no side effect before the warm-up, which waits for the first frame.
    /// </summary>
    private OverlayParts ComposeOverlay(
        ShipCatalog ships, ShipOutlineCatalog outlines, StartOptions options, AppSettings settings, string? settingsProblem)
    {
        // The settings' ship, else the start plan's, else the Hull A; one that doesn't load never stops the start (T7.2).
        var (ship, shipNotice) = ShipChoices.Resolve(ships, settings.Ship, PlanStore.PeekShipId(options.PlanPath ?? PlanStore.DefaultPath));
        var viewModel = new OverlayViewModel(ship, ships)
        {
            IsManuallyLocked = options.Locked,
            RecentShips = settings.RecentShips ?? [],
            Cameras = settings.ShipView.Cameras ?? new Dictionary<string, ShipCamera>(),
            Outlines = outlines,
        };
        viewModel.SetNotice(NoticeSource.Settings, settingsProblem);
        viewModel.SetNotice(NoticeSource.Outlines, OverlayViewModel.OutlinesNoticeText(outlines.Problems));
        viewModel.SetNotice(NoticeSource.Ship, shipNotice);

        // Before any window: their DynamicResources find the theme from the first layout on. The settings window
        // changes the look through the same session (D34).
        var session = new SettingsSession(Resources, viewModel, SettingsStore.DefaultPath, settings);

        // Bounds before Show(), so the window appears in place instead of jumping there. The hotkeys' view model is made
        // when the settings window opens; by then _hotkeys exists (it needs the overlay, so it comes after it).
        // ContractTracker where the settings say, detected by default (D44).
        var contractTracker = new ContractTrackerSync(
            viewModel, settings.ContractTracker.DatabasePath, autoSync: settings.ContractTracker.AutoSync);
        viewModel.OnContractGone = settings.ContractTracker.GonePolicy;
        _contractTracker = contractTracker;

        // The scanner's notices and the scan's changes of the plan come from pool threads; the overlay lives on the UI
        // thread. Source: https://learn.microsoft.com/dotnet/api/system.windows.threading.dispatcheroperation.task
        var scanner = CreateScanner(
            text => Dispatcher.InvokeAsync(() => viewModel.SetNotice(NoticeSource.Scan, text)),
            options.ScanProbe is null ? null : ProbeTarget());
        var scan = new ScanRunner(() => scanner.ScanAsync(), viewModel, new ScanLog(ScanLog.DefaultPath), action => Dispatcher.InvokeAsync(action).Task);
        var overlay = new OverlayWindow(
            viewModel,
            session,
            () => CreateHotkeysViewModel(session),
            () => CreateContractTrackerSection(session, contractTracker, viewModel),
            () => contractTracker.RunSyncGuardedAsync(manual: true));
        if (settings.Overlay is { } saved)
        {
            RestoreBounds(overlay, saved);
        }

        // The saver's Closed handler goes first and hands over pending bounds; Flush then writes a pending look change.
        _boundsSaver = new OverlayBoundsSaver(overlay, session);
        overlay.Closed += (_, _) => session.Flush();
        return new OverlayParts(ship, viewModel, session, contractTracker, overlay, scanner, scan);
    }

    /// <summary>
    /// Polled from here on (D97), before the settings window can ask for them. The sync hotkey runs the editor's button,
    /// guarded like the timer (D82); AsyncRelayCommand would rethrow on the UI thread what isn't. The scan's is a plain
    /// command: the runner guards itself, and a press during a scan must reach the scanner to say "Scan running" (D104).
    /// </summary>
    // Source: https://learn.microsoft.com/dotnet/api/communitytoolkit.mvvm.input.asyncrelaycommandoptions
    private void ComposeHotkeys(OverlayParts parts, AppSettings settings)
    {
        var contractTracker = parts.ContractTracker;
        _hotkeys = new HotkeyService(
            parts.Overlay,
            settings.Hotkeys,
            parts.ViewModel,
            new AsyncRelayCommand(() => contractTracker.RunSyncGuardedAsync(manual: true)),
            new RelayCommand(parts.Scan.Start),
            new ScanKeyCheck(ScanKeyCheck.IsContractTrackerRunning));
    }

    /// <summary>
    /// The plan saver before the start plan: a --plan file becomes plan.json this way, without a rule of its own.
    /// A plan from plan.json is written back once, unchanged.
    /// </summary>
    private void ComposeStartPlan(OverlayParts parts, StartOptions options)
    {
        var viewModel = parts.ViewModel;
        _planSaver = new PlanSaver(parts.Overlay, viewModel, PlanStore.DefaultPath);
        var (plan, planProblem, lastScanNo) = LoadStartPlan(options, parts.Ship);
        viewModel.SetNotice(NoticeSource.Plan, options.Problem ?? planProblem);
        if (plan is not null)
        {
            viewModel.LastScanNo = lastScanNo;
            viewModel.ShowPlan(plan);
        }
        else if (planProblem is not null)
        {
            // The file stays until the player makes a new plan; a capture must not overwrite it (T5.4).
            viewModel.PauseContractTracker();
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
    /// The contract scanner (T10.1b, D99), not yet warm: <see cref="WarmAfterFirstFrame"/> starts that. Missing models end in
    /// a notice, never in a failed start (plan decision 3).
    /// </summary>
    /// <param name="notice">The scan's notice line; called from pool threads.</param>
    /// <param name="find">Another window than the game's, for a probe's dry run; null = Star Citizen.</param>
    private ContractScanner CreateScanner(Action<string?> notice, Func<GameWindow?>? find = null)
    {
        // Our windows over the contract leave the picture for the capture (D100). The window list is read at each capture,
        // so windows made after the scanner count too.
        var clearance = new RegionClearance(Dispatcher, () => Windows.Cast<Window>());
        return _scanner = new ContractScanner(OcrEngine.Load, notice, find, clear: clearance.Clear);
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

    /// <summary>The overlay first: the lock window's Owner must be a window that has been shown. The sync starts last.</summary>
    private void ShowWindows(OverlayParts parts, StartOptions options)
    {
        var (viewModel, contractTracker, overlay) = (parts.ViewModel, parts.ContractTracker, parts.Overlay);
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

        // After the start plan, so the first read knows its watermark; it comes after the debounce, with the overlay up.
        contractTracker.Start();
        overlay.Closed += (_, _) => contractTracker.Dispose();
    }

    protected override void OnExit(ExitEventArgs e)
    {
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
    /// so only the rules and the other action's combination refuse one, both in the view model.
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
                return null;
            },
            hotkeys.Suspend,
            hotkeys.Resume);
    }

    /// <summary>
    /// The settings window's ContractTracker section (D44): saved through the session, applied to the sync and the overlay
    /// at once, so a new path is watched and a policy for gone contracts (D81) holds from the next read without a restart (D30).
    /// </summary>
    private static ContractTrackerSectionViewModel CreateContractTrackerSection(SettingsSession session, ContractTrackerSync sync, OverlayViewModel overlay) =>
        new(session.Current.ContractTracker, File.Exists, settings =>
        {
            session.ChangeContractTracker(settings);
            overlay.OnContractGone = settings.GonePolicy;
            sync.Configure(settings.DatabasePath, settings.AutoSync);
        });

    /// <summary>--plan wins over plan.json; a missing --plan file is a problem, a missing plan.json is the empty start.</summary>
    private static (SavedPlan? Plan, string? Problem, int LastScanNo) LoadStartPlan(StartOptions options, Ship ship)
    {
        if (options.PlanPath is not { } path)
        {
            return PlanStore.Load(PlanStore.DefaultPath, ship);
        }

        var loaded = PlanStore.Load(path, ship);
        return loaded is (null, null, _) ? (null, $"Plan file {path} not found.", 0) : loaded;
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
