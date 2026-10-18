using CargoPlan.Core.Models;
using CargoPlan.Core.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    /// The player's lock: --locked sets the start value, the lock window switches it via <see cref="ToggleLockCommand"/>.
    /// </summary>
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/observableproperty#notifying-dependent-properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    public partial bool IsManuallyLocked { get; set; }

    /// <summary>Set by the cursor state monitor while Star Citizen has the mouse (T3.3).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    public partial bool IsAutoLocked { get; set; }

    /// <summary>
    /// true = click-through: mouse input goes to the window below. Default false, see D11.
    /// The manual lock forces locked but doesn't lift the auto-lock: unlocked by hand still locks
    /// while flying, or the next tractor-beam click would be lost.
    /// </summary>
    public bool IsLocked => IsManuallyLocked || IsAutoLocked;

    /// <summary>A problem the player should know about (settings not loaded or not saved); null hides the line.</summary>
    [ObservableProperty]
    public partial string? Notice { get; set; }

    // Generates ToggleLockCommand (IRelayCommand), bound by the lock window's button.
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/relaycommand
    [RelayCommand]
    private void ToggleLock() => IsManuallyLocked = !IsManuallyLocked;

    public string PlanText => Plan is null ? NoPlanText : PlanTextRenderer.Render(Plan, Ship);
}
