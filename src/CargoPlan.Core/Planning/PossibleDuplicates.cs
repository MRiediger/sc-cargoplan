using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Planning;

/// <summary>
/// What the legend marks as a possible duplicate: the net under the content key (D101), for a scan the OCR read
/// otherwise and a contract typed twice. Marked, not decided; the player deletes one.
/// </summary>
public static class PossibleDuplicates
{
    /// <summary>
    /// The missions that may be a second scan of another one (D39 addendum, D101): of two missions without any ticked crate,
    /// the later one, if the destinations and commodities it delivers are all delivered by the other, or the other way
    /// round. Overlapping alone is no duplicate: two real contracts can share destinations. Different rewards are two
    /// contracts too (T5.3b); an unknown reward (typed, unreadable) doesn't rule one out.
    /// Derived, not stored, so it goes with the first tick on either and comes back with ticking back. Names compare
    /// ignoring case (D40).
    /// </summary>
    public static IReadOnlySet<int> Of(SavedPlan saved)
    {
        HashSet<int> ticked = [.. saved.Plan.Crates.Where(c => c.Delivered).Select(c => c.Mission)];
        var candidates = saved.Missions
            .Where(m => !ticked.Contains(m.Id))
            .Select(m => (
                m.Id,
                m.Contract?.Reward,
                Deliveries: m.Deliveries.Select(d => (d.Destination.ToUpperInvariant(), d.Commodity.ToUpperInvariant())).ToHashSet()))
            .ToArray();

        HashSet<int> duplicates = [];
        for (var later = 1; later < candidates.Length; later++)
        {
            for (var earlier = 0; earlier < later; earlier++)
            {
                var (first, second) = (candidates[earlier], candidates[later]);
                if ((first.Reward is null || second.Reward is null || first.Reward == second.Reward)
                    && (second.Deliveries.IsSubsetOf(first.Deliveries) || first.Deliveries.IsSubsetOf(second.Deliveries)))
                {
                    duplicates.Add(candidates[later].Id);
                }
            }
        }

        return duplicates;
    }
}
