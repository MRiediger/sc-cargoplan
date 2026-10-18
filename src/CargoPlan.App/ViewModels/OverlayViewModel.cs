using CargoPlan.Core.Models;
using CargoPlan.Core.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CargoPlan.App.ViewModels;

/// <summary>State of the overlay window. Shows what Core computes; no planning here.</summary>
public sealed partial class OverlayViewModel(Ship ship) : ObservableObject
{
    public const string NoPlanText = "No plan loaded.";

    public Ship Ship { get; } = ship;

    // [ObservableProperty] on partial properties (Toolkit 8.4+, C# 14 here): the generator writes the
    // body with SetProperty and PropertyChanged.
    // Source: https://github.com/CommunityToolkit/dotnet/releases/tag/v8.4.0
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanText))]
    public partial LoadPlan? Plan { get; set; }

    /// <summary>
    /// true = click-through: mouse input goes to the window below. Default false, see D11.
    /// Set by the --locked start switch for now; the lock window (T3.2) and auto-lock (T3.3) will set it.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    public string PlanText => Plan is null ? NoPlanText : PlanTextRenderer.Render(Plan, Ship);
}
