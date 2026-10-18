using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.ViewModels;

/// <summary>State of the overlay window. Shows what Core computes; no planning here.</summary>
public sealed partial class OverlayViewModel(Ship ship) : ObservableObject
{
    public const string NoPlanText = "No plan loaded.";

    public const string AllDeliveredText = "All stops delivered.";

    public Ship Ship { get; } = ship;

    // [ObservableProperty] on partial properties (Toolkit 8.4+, C# 14 here): the generator writes the
    // body with SetProperty and PropertyChanged.
    // Source: https://github.com/CommunityToolkit/dotnet/releases/tag/v8.4.0
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Scene))]
    [NotifyPropertyChangedFor(nameof(HasPlan))]
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

    /// <summary>
    /// Hotkeys that could not be registered, one line each; null hides the line. Separate from <see cref="Notice"/>,
    /// which a successful save clears: this one must stay until the next start (D17).
    /// </summary>
    [ObservableProperty]
    public partial string? HotkeyNotice { get; set; }

    /// <summary>Both windows hide while true. Hiding resets nothing: position, zoom, orbit and lock stay as they are.</summary>
    [ObservableProperty]
    public partial bool IsOverlayHidden { get; set; }

    // Generates ToggleLockCommand (IRelayCommand), bound by the lock window's button and run by the toggleLock hotkey.
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/relaycommand
    [RelayCommand]
    private void ToggleLock() => IsManuallyLocked = !IsManuallyLocked;

    /// <summary>Run by the toggleOverlay hotkey: with a hidden cursor nobody can click the overlay away (D17).</summary>
    [RelayCommand]
    private void ToggleOverlay() => IsOverlayHidden = !IsOverlayHidden;

    /// <summary>
    /// Ticks a stop off in the legend, or back after a misclick: the whole stop, split parts included (D16).
    /// A method parameter makes the generator write an <c>IRelayCommand&lt;string&gt;</c>; the legend passes the destination.
    /// </summary>
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/relaycommand#command-parameters
    [RelayCommand]
    private void ToggleDelivered(string destination)
    {
        // Unreachable without a plan: the legend has no lines then.
        var plan = Plan ?? throw new InvalidOperationException(NoPlanText);
        var delivered = plan.PlacementsForStop(destination).All(p => p.Delivered);
        Plan = LoadPlanner.SetDelivered(plan, Ship, destination, !delivered);
    }

    public bool HasPlan => Plan is not null;

    /// <summary>
    /// What the ship view and the legend draw: the plan with Core's validation (D10), or the empty ship without a plan.
    /// Built once per change of <see cref="Plan"/>, not per binding.
    /// </summary>
    public ShipScene Scene { get; private set; } = EmptyScene(ship);

    // Runs before PropertyChanged for Plan and Scene, so bindings read the new scene.
    // Source: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/observableproperty#running-code-upon-changes
    partial void OnPlanChanged(LoadPlan? value) => Scene = value is null
        ? EmptyScene(Ship)
        : ShipViewBuilder.Build(value, Ship, LoadPlanner.Validate(value, Ship));

    private static ShipScene EmptyScene(Ship ship) => ShipViewBuilder.Build(new LoadPlan(ship.Id, [], []), ship, []);
}
