using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.App.ShipView;

/// <summary>
/// How the legend writes quantities, crate mixes and ContractTracker ids (K6.1, D69 addendum), for the lines on board
/// and the missions that wait (D41) alike.
/// </summary>
public static class LegendText
{
    private const string Separator = " · ";

    /// <summary>" · <paramref name="text"/>", or nothing for no text.</summary>
    public static string Appended(string text) => text.Length == 0 ? "" : Separator + text;

    /// <summary>
    /// " · 5 SCU (4+1)", " · Pressurized Ice" for one commodity, no brackets around a single crate; with several,
    /// " · 12 SCU", " (8+2) ", "Titanium", ", (2) ", "Gold": every mix next to its commodity, only the names shorten.
    /// </summary>
    public static IReadOnlyList<LegendSegment> Quantity(int scu, IReadOnlyList<LineGoods> goods)
    {
        var total = Invariant($"{Separator}{scu} SCU");
        return goods switch
        {
            [] => [new(total)],
            [var one] => [new(one.Crates.Count > 1 ? Invariant($"{total} ({Mix(one.Crates)})") : total), new(Separator + one.Commodity, LegendSegment.TrimFirst)],
            _ => [new(total), .. goods.SelectMany((g, i) => (LegendSegment[])[new($"{(i == 0 ? " " : ", ")}({Mix(g.Crates)}) "), new(g.Commodity, LegendSegment.TrimFirst)])],
        };
    }

    /// <summary>"2×4+1": sizes largest first, a size that repeats as count×size.</summary>
    public static string Mix(IEnumerable<CrateSize> crates) =>
        string.Join("+", crates.Select(c => (int)c).GroupBy(size => size).OrderByDescending(g => g.Key)
            .Select(g => g.Count() == 1 ? Invariant($"{g.Key}") : Invariant($"{g.Count()}×{g.Key}")));

    /// <summary>
    /// The commodities of <paramref name="crates"/> in the order of their items (<see cref="Crate.Item"/>), each with its
    /// crates largest first.
    /// </summary>
    public static IReadOnlyList<LineGoods> Goods(IEnumerable<Crate> crates) =>
        [.. crates.GroupBy(c => c.Commodity).OrderBy(g => g.Min(c => c.Item))
            .Select(g => new LineGoods(g.Key, [.. g.Select(c => c.CrateSize).OrderDescending()]))];

    /// <summary>
    /// A mission without crates on board (it waits, D41): its commodities in item order with the crates its items take
    /// under <paramref name="cap"/>, as the planner would lay them (E1).
    /// </summary>
    public static IReadOnlyList<LineGoods> Goods(Mission mission, CrateCap cap) =>
        [.. mission.Items.Select(i => i.Item).GroupBy(i => i.Commodity)
            .Select(g => new LineGoods(g.Key, [.. g.SelectMany(i => i.CrateListUnder(cap)).OrderDescending()]))];

    /// <summary>"#47", consecutive ids as a range "#47–48", others apart "#47, 49" (ContractTracker's <c>Contracts.Id</c>).</summary>
    public static string ContractIds(IEnumerable<long> ids)
    {
        long[] sorted = [.. ids.Distinct().Order()];
        List<string> runs = [];
        for (var start = 0; start < sorted.Length;)
        {
            var end = start;
            while (end + 1 < sorted.Length && sorted[end + 1] == sorted[end] + 1)
            {
                end++;
            }

            runs.Add(end == start ? Invariant($"{sorted[start]}") : Invariant($"{sorted[start]}–{sorted[end]}"));
            start = end + 1;
        }

        return "#" + string.Join(", ", runs);
    }
}
