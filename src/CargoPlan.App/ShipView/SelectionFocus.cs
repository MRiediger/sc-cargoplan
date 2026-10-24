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

    /// <summary>
    /// "Lorville · Titanium · Riker", or "Titanium · Riker" without pickup, for exactly one selected crate; null for none
    /// or several.
    /// </summary>
    public static string? InfoLine(SavedPlan? saved, IReadOnlySet<Crate> selected)
    {
        if (saved is null || selected.Count != 1)
        {
            return null;
        }

        var crate = selected.Single();
        var pickup = saved.Missions.FirstOrDefault(m => m.Id == crate.Mission)?.Items.ElementAtOrDefault(crate.Item).Item?.Pickup;
        string[] parts = pickup is null ? [crate.Commodity, crate.Destination] : [Pickups.Text(pickup), crate.Commodity, crate.Destination];
        return string.Join(Separator, parts);
    }

    /// <summary>The missions the selected crates belong to; empty without selection.</summary>
    public static IReadOnlySet<int> Missions(IReadOnlySet<Crate> selected) => selected.Select(c => c.Mission).ToHashSet();

    /// <summary>A crate of a selected mission: it gets the mission frame. Free cells (null) never do.</summary>
    public static bool Frames(Crate? crate, IReadOnlySet<int> missions) => crate is not null && missions.Contains(crate.Mission);

    /// <summary>A crate of another mission while something is selected: drawn dimmed. Free cells (null) never are.</summary>
    public static bool Dims(Crate? crate, IReadOnlySet<int> missions) => crate is not null && missions.Count > 0 && !missions.Contains(crate.Mission);
}
