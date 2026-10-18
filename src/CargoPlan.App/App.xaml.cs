using System.Windows;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App;

/// <summary>
/// Composition root. Two windows, one view model: wired by hand, a DI container would add a package
/// and indirection for nothing.
/// </summary>
public partial class App : Application
{
    /// <summary>Starts the overlay locked (click-through); the lock window unlocks it.</summary>
    public const string LockedSwitch = "--locked";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var (ship, plan) = SamplePlan.Load();
        var viewModel = new OverlayViewModel(ship)
        {
            Plan = plan,
            IsLocked = e.Args.Contains(LockedSwitch, StringComparer.OrdinalIgnoreCase),
        };

        // Overlay first: the lock window's Owner must be a window that has been shown.
        var overlay = new OverlayWindow(viewModel);
        overlay.Show();
        new LockWindow(viewModel, overlay).Show();
    }
}
