using CommunityToolkit.Mvvm.ComponentModel;

namespace CargoPlan.App.Editor;

/// <summary>
/// One line of the mission editor: one contract item for one destination, as typed. A class, not a record: the data grid
/// writes into it. Nothing here is validated; <see cref="MissionRows.ToStops"/> does that for all rows at once.
/// </summary>
public sealed partial class MissionRow : ObservableObject
{
    /// <summary>Parameterless for the data grid's new-item row.</summary>
    public MissionRow()
    {
        Destination = "";
        Commodity = "";
        Scu = "";
        MaxCrateSize = MissionRows.DefaultMaxCrateSize;
        Crates = "";
        CratesText = "";
        Pickup = "";
    }

    [ObservableProperty]
    public partial string Destination { get; set; }

    [ObservableProperty]
    public partial string Commodity { get; set; }

    /// <summary>Text, not int: "abc" must reach the validation instead of being refused by the binding unseen.</summary>
    [ObservableProperty]
    public partial string Scu { get; set; }

    /// <summary>The contract's max crate size in SCU, picked from <see cref="MissionRows.MaxCrateSizes"/> (D23).</summary>
    [ObservableProperty]
    public partial int MaxCrateSize { get; set; }

    /// <summary>
    /// Where the item's crates are collected, typed as "A / B" (D68); empty = not known. Not part of <see cref="IsBlank"/>:
    /// a new row takes it from the row above, like the max crate size.
    /// </summary>
    [ObservableProperty]
    public partial string Pickup { get; set; }

    /// <summary>The mission the row belongs to in the overlay's plan (D42); null for a row not planned yet.</summary>
    public int? MissionId { get; init; }

    /// <summary>Where the row came from: "#37–38" for a ContractTracker capture, empty if typed (T5.4); the Mission column.</summary>
    public string Source { get; init; } = "";

    /// <summary>The capture's first ContractTracker id, the Mission column's sort key (K6.3); null if typed.</summary>
    public long? ContractId { get; init; }

    /// <summary>
    /// Added to a ContractTracker mission or changed by hand (K6.5): stays with the mission until ContractTracker no
    /// longer lists it. Set by the editor, never for a typed row.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MissionLabel))]
    public partial bool Manual { get; set; }

    /// <summary>The Mission column: "#37–38", "#37–38 ✎" for a row added or changed by hand (K6.5), empty if typed.</summary>
    public string MissionLabel => Manual ? Source + ManualMark : Source;

    /// <summary>What marks a manual row in the Mission column.</summary>
    public const string ManualMark = " ✎";

    /// <summary>
    /// The max crate size was assumed by the import (the contract named none); the legend shows "4 SCU?". Picking a size
    /// ends the assumption, so the "?" goes with the next Plan.
    /// </summary>
    [ObservableProperty]
    public partial bool MaxCrateSizeAssumed { get; set; }

    /// <summary>The crates as the kiosk delivered them, typed as "2+2+2"; empty = derived from SCU and max (D23).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCrateOverride))]
    public partial string Crates { get; set; }

    /// <summary>What the crates column shows: the derived mix ("4+2") or the override; set by the editor.</summary>
    [ObservableProperty]
    public partial string CratesText { get; set; }

    /// <summary>The crates are typed by hand, not derived; the grid shows them in italics.</summary>
    public bool IsCrateOverride => !string.IsNullOrWhiteSpace(Crates);

    /// <summary>Stop the row belongs to (first appearance of its destination), set by the editor; null for a blank row.</summary>
    [ObservableProperty]
    public partial int? StopNumber { get; set; }

    /// <summary>What is wrong with the row, set by the editor; null = fine.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    /// <summary>For the row style: a data trigger can test a value, not "not null".</summary>
    public bool HasError => Error is not null;

    /// <summary>Nothing typed yet: the empty start row or a row just added. Skipped instead of flagged.</summary>
    public bool IsBlank =>
        string.IsNullOrWhiteSpace(Destination) && string.IsNullOrWhiteSpace(Commodity) && string.IsNullOrWhiteSpace(Scu)
        && !IsCrateOverride;

    // Generated hook: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/generators/observableproperty#running-code-upon-changes
    partial void OnMaxCrateSizeChanged(int value) => MaxCrateSizeAssumed = false;

    /// <summary>The properties the player types; the others are results.</summary>
    public static bool IsInput(string? propertyName) =>
        propertyName is nameof(Destination) or nameof(Commodity) or nameof(Scu) or nameof(MaxCrateSize) or nameof(Crates) or nameof(Pickup);
}
