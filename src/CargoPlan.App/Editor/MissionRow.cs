using CommunityToolkit.Mvvm.ComponentModel;

namespace CargoPlan.App.Editor;

/// <summary>
/// One line of the mission editor: one cargo item for one destination, as typed. A class, not a record: the data grid
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
        CrateSize = 1;
    }

    [ObservableProperty]
    public partial string Destination { get; set; }

    [ObservableProperty]
    public partial string Commodity { get; set; }

    /// <summary>Text, not int: "abc" must reach the validation instead of being refused by the binding unseen.</summary>
    [ObservableProperty]
    public partial string Scu { get; set; }

    /// <summary>Crate volume in SCU, picked from <see cref="MissionRows.CrateSizes"/>.</summary>
    [ObservableProperty]
    public partial int CrateSize { get; set; }

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
        string.IsNullOrWhiteSpace(Destination) && string.IsNullOrWhiteSpace(Commodity) && string.IsNullOrWhiteSpace(Scu);

    /// <summary>The properties the player types; the others are results.</summary>
    public static bool IsInput(string? propertyName) =>
        propertyName is nameof(Destination) or nameof(Commodity) or nameof(Scu) or nameof(CrateSize);
}
