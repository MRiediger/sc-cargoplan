using System.IO;
using System.Windows;
using CargoPlan.App.ContractTracker;
using CargoPlan.App.Editor;
using CargoPlan.App.Holo;
using CargoPlan.App.Hotkeys;
using CargoPlan.App.Plans;
using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // First: without the ships folder there is nothing to draw. One message, then the end, no crash dialog (D56).
        // The catalog's problems (skipped ship files) are shown in the header's ship selection (T7.2b).
        ShipCatalog ships;
        try
        {
            ships = ShipLoader.LoadAll();
        }
        catch (ShipFolderException ex)
        {
            MessageBox.Show(ex.Message, "SC-CargoPlan", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(ExitShipsFolderBroken);
            return;
        }

        // Once per start, not per ship change (200 KB). A broken file costs the outlines, never the start (D62).
        var outlines = ShipOutlineLoader.Load();

        var options = StartOptions.Parse(e.Args);
        var (settings, settingsProblem) = SettingsStore.Load(SettingsStore.DefaultPath);

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
        var overlay = new OverlayWindow(
            viewModel,
            session,
            () => CreateHotkeysViewModel(session),
            () => CreateContractTrackerSection(session, contractTracker, viewModel),
            () => contractTracker.SyncAsync(manual: true));
        if (settings.Overlay is { } saved)
        {
            RestoreBounds(overlay, saved);
        }

        // The saver's Closed handler goes first and hands over pending bounds; Flush then writes a pending look change.
        _boundsSaver = new OverlayBoundsSaver(overlay, session);
        overlay.Closed += (_, _) => session.Flush();

        // Before Show(): SourceInitialized, where the hotkeys get registered, is raised inside Show(). The sync hotkey runs
        // the editor's button; AsyncRelayCommand rethrows on the UI thread what SyncAsync doesn't handle.
        // Source: https://learn.microsoft.com/dotnet/api/communitytoolkit.mvvm.input.asyncrelaycommandoptions
        _hotkeys = new HotkeyService(
            overlay, settings.Hotkeys, viewModel, new AsyncRelayCommand(() => contractTracker.SyncAsync(manual: true)));

        // Before the start plan: a --plan file becomes plan.json this way, without a rule of its own.
        // A plan from plan.json is written back once, unchanged.
        _planSaver = new PlanSaver(overlay, viewModel, PlanStore.DefaultPath);
        var (plan, planProblem) = LoadStartPlan(options, ship);
        viewModel.SetNotice(NoticeSource.Plan, options.Problem ?? planProblem);
        if (plan is not null)
        {
            viewModel.ShowPlan(plan);
        }
        else if (planProblem is not null)
        {
            // The file stays until the player makes a new plan; a capture must not overwrite it (T5.4).
            viewModel.PauseContractTracker();
        }

        // After the start: the fallback or the plan's ship isn't a choice and stays out of settings.json (T7.2).
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

        // The hologram of the ship and look (D63): once the overlay is up, on a ship change, on a new look or switch.
        // Every request cancels the one before; the outline shows until the hologram is there.
        var holoLoader = _holoLoader = new HoloLoader(viewModel, HoloSource.CreateDefault());
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

        // Overlay first: the lock window's Owner must be a window that has been shown.
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

    /// <summary>
    /// The settings window's hotkeys (D35): registered first, saved only if Windows took them, so settings.json never holds
    /// a combination that doesn't work.
    /// </summary>
    private HotkeysViewModel CreateHotkeysViewModel(SettingsSession session)
    {
        var hotkeys = _hotkeys ?? throw new InvalidOperationException("The settings window opened before the hotkeys existed.");
        return new HotkeysViewModel(
            session.Current.Hotkeys,
            (action, hotkey) =>
            {
                var problem = hotkeys.TrySet(action, hotkey);
                if (problem is null)
                {
                    session.ChangeHotkey(action, hotkey);
                }

                return problem;
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
    private static (SavedPlan? Plan, string? Problem) LoadStartPlan(StartOptions options, Ship ship)
    {
        if (options.PlanPath is not { } path)
        {
            return PlanStore.Load(PlanStore.DefaultPath, ship);
        }

        var loaded = PlanStore.Load(path, ship);
        return loaded is (null, null) ? (null, $"Plan file {path} not found.") : loaded;
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
