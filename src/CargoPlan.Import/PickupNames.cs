using CargoPlan.Core.Missions;
using CargoPlan.Core.Plans;
using CargoPlan.Import.ContractTracker;

namespace CargoPlan.Import;

/// <summary>
/// A pickup is a place's name, never the contract's text (B6, D69). ContractTracker's pickup field sometimes holds the
/// next line of the contract (id 38: "Everus Harbor. - Freight elevator at Green Glade Station …"); the reader cuts it
/// on the way in, and plans saved before (T7.4d) are cleaned once on loading, like the stop names of B1.
/// </summary>
public static class PickupNames
{
    /// <summary>ContractTracker's pickup text: split at "/", each place cut to its name, then normalized (<see cref="Pickups"/>).</summary>
    public static IReadOnlyList<string>? Parse(string? text) =>
        text is null ? null : Pickups.Normalized(text.Split('/').Select(ContractText.NormalizePickup));

    /// <summary>
    /// The plan with every pickup of a ContractTracker mission cut to its name; typed pickups are the player's and stay.
    /// Only items change: crates, ticks and the watermark stay. Without a change it returns <paramref name="saved"/> itself.
    /// </summary>
    public static CleanedPickups Clean(SavedPlan saved)
    {
        var changed = false;
        List<string> names = [];
        Mission[] missions = [.. saved.Missions.Select(mission =>
        {
            if (mission.Contract is null)
            {
                return mission;
            }

            for (var i = 0; i < mission.Items.Count; i++)
            {
                var item = mission.Items[i].Item;
                if (item.Pickup is not { } pickup)
                {
                    continue;
                }

                var cut = Pickups.Normalized(pickup.Select(ContractText.NormalizePickup));
                if (!(cut ?? []).SequenceEqual(pickup))
                {
                    mission = mission.WithItem(i, new MissionItem(item.Commodity, item.Scu, item.MaxCrateSize, item.Crates, cut));
                    names.AddRange(cut ?? []);
                    changed = true;
                }
            }

            return mission;
        })];

        return changed
            ? new(new SavedPlan(missions, saved.Plan, saved.CrateCap, saved.Watermark), [.. names.Distinct(StringComparer.OrdinalIgnoreCase)])
            : new(saved, []);
    }
}

/// <param name="Cleaned">The names the cleaned pickups became, for the notice; empty without a change.</param>
public sealed record CleanedPickups(SavedPlan Saved, IReadOnlyList<string> Cleaned);
