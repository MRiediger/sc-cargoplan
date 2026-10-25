using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;

namespace CargoPlan.App.ShipView;

/// <summary>
/// What a crate selection shows besides its magenta edges (T7.4d, D68): one crate's pickup, commodity and destination
/// in the info line, and the missions of the selection framed with every other crate dimmed. Crates of two missions
/// come up the elevator together, so the mission is what the player has to tell apart. View only, no planning.
/// </summary>
public static class SelectionFocus
{
    private const string Separator = " · ";

    private const string FromPrefix = "from ";
    private const string ToPrefix = "to ";

    /// <summary>
    /// "from Lorville · Titanium · to Riker", or "from ? · Titanium · to Riker" without pickup (B6, like the legend's
    /// "From ?"), for exactly one selected crate; null for none or several.
    /// </summary>
    public static string? InfoLine(SavedPlan? saved, IReadOnlySet<Crate> selected)
    {
        if (saved is null || selected.Count != 1)
        {
            return null;
        }

        var crate = selected.Single();
        var pickup = saved.Missions.FirstOrDefault(m => m.Id == crate.Mission)?.Items.ElementAtOrDefault(crate.Item).Item?.Pickup;
        var from = FromPrefix + (pickup is null ? ShipViewBuilder.UnknownPickupText : Pickups.Text(pickup));
        return string.Join(Separator, from, crate.Commodity, ToPrefix + crate.Destination);
    }

    /// <summary>Every one of <paramref name="crates"/> is selected, and there is one: the legend's check box is ticked.</summary>
    public static bool HasAll(IReadOnlySet<Crate> selected, IReadOnlyList<Crate> crates) => crates.Count > 0 && crates.All(selected.Contains);

    /// <summary>
    /// A legend check box clicked: <paramref name="crates"/> taken out of the selection if all of them are in it, else
    /// added to it; the rest of the selection stays.
    /// </summary>
    public static CrateSelection Toggled(CrateSelection selection, ShipScene scene, IReadOnlyList<Crate> crates)
    {
        var selected = selection.In(scene);
        return CrateSelection.Of(scene, HasAll(selected, crates) ? selected.Except(crates) : selected.Union(crates));
    }

    /// <summary>The missions the selected crates belong to; empty without selection.</summary>
    public static IReadOnlySet<int> Missions(IReadOnlySet<Crate> selected) => selected.Select(c => c.Mission).ToHashSet();

    /// <summary>A crate of a selected mission: it gets the mission frame. Free cells (null) never do.</summary>
    public static bool Frames(Crate? crate, IReadOnlySet<int> missions) => crate is not null && missions.Contains(crate.Mission);

    /// <summary>A crate of another mission while something is selected: drawn dimmed. Free cells (null) never are.</summary>
    public static bool Dims(Crate? crate, IReadOnlySet<int> missions) => crate is not null && missions.Count > 0 && !missions.Contains(crate.Mission);
}
