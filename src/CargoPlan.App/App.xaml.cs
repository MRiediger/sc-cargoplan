using System.Windows;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App;

/// <summary>
/// Composition root. One window, one view model: wired by hand, a DI container would add a package
/// and indirection for nothing.
/// </summary>
public partial class App : Application
{
    /// <summary>Starts the overlay locked (click-through); unlocking needs a restart until the lock window (T3.2).</summary>
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

        new OverlayWindow(viewModel).Show();
    }
}
