using System.Windows;
using CargoPlan.App.Hotkeys;
using CargoPlan.App.Plans;
using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.App;

/// <summary>
/// Composition root. Two windows, one view model: wired by hand, a DI container would add a package
/// and indirection for nothing.
/// </summary>
public partial class App : Application
{
    /// <summary>The only ship until there is a ship selection (T7.1).</summary>
    public const string ShipId = "hull_a";

    private CursorStateMonitor? _cursorMonitor;

    // Kept so the composition root visibly owns it; the window's events would keep it alive anyway.
    private OverlayBoundsSaver? _boundsSaver;

    // Same here: the overlay's events keep it alive, the field shows who owns it.
    private HotkeyService? _hotkeys;

    // Same here: the view model's and the overlay's events keep it alive.
    private PlanSaver? _planSaver;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var options = StartOptions.Parse(e.Args);
        var (settings, settingsProblem) = SettingsStore.Load(SettingsStore.DefaultPath);

        var ship = ShipLoader.Load(ShipId);
        var viewModel = new OverlayViewModel(ship) { IsManuallyLocked = options.Locked };
        viewModel.SetNotice(NoticeSource.Settings, settingsProblem);

        // Before any window: their DynamicResources find the theme from the first layout on. The settings window
        // changes the look through the same session (D34).
        var session = new SettingsSession(Resources, viewModel, SettingsStore.DefaultPath, settings);

        // Bounds before Show(), so the window appears in place instead of jumping there. The hotkeys' view model is made
        // when the settings window opens; by then _hotkeys exists (it needs the overlay, so it comes after it).
        var overlay = new OverlayWindow(viewModel, session, () => CreateHotkeysViewModel(session));
        if (settings.Overlay is { } saved)
        {
            RestoreBounds(overlay, saved);
        }

        // The saver's Closed handler goes first and hands over pending bounds; Flush then writes a pending look change.
        _boundsSaver = new OverlayBoundsSaver(overlay, session);
        overlay.Closed += (_, _) => session.Flush();

        // Before Show(): SourceInitialized, where the hotkeys get registered, is raised inside Show().
        _hotkeys = new HotkeyService(overlay, settings.Hotkeys, viewModel);

        // Before the start plan: a --plan file becomes plan.json this way, without a rule of its own.
        // A plan from plan.json is written back once, unchanged.
        _planSaver = new PlanSaver(overlay, viewModel, PlanStore.DefaultPath);
        var (plan, planProblem) = LoadStartPlan(options, ship);
        viewModel.SetNotice(NoticeSource.Plan, options.Problem ?? planProblem);
        if (plan is not null)
        {
            viewModel.ShowPlan(plan);
        }

        // Overlay first: the lock window's Owner must be a window that has been shown.
        overlay.Show();
        new LockWindow(viewModel, overlay).Show();

        _cursorMonitor = new CursorStateMonitor(viewModel);
        _cursorMonitor.Start();
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
