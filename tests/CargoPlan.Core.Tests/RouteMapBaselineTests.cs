using System.Globalization;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using Xunit.Abstractions;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The map's edges against the port matrix on the pinned release (T13.7, K1): between stations an edge is one cell,
/// with a surface the sum of its places' cells, and no contract of the matrix is lost. A data test of the baseline (A10).
/// </summary>
public class RouteMapBaselineTests(ITestOutputHelper output)
{
    private static readonly Lazy<HaulingData> Data = new(() =>
        ((HaulingLoad.Loaded)HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder))).Data);

    public static TheoryData<string> Filters => ["Stanton stellar Senior", "Stanton all areas Master", "Pyro both factions"];

    [Theory]
    [MemberData(nameof(Filters))]
    public void Each_edge_is_its_cell_between_stations_and_the_sum_of_its_places_cells_with_a_surface(string name)
    {
        var (filter, system) = Filter(name);
        var matrix = PortMatrix.Build(Data.Value.Templates, filter);
        var nodeOf = NodeOf(system);

        var map = RouteMap.Build(matrix, nodeOf, system);

        foreach (var edge in map.Edges)
        {
            // Every cell whose places fall into the edge's nodes, and only those.
            var cells = matrix.Pickups.SelectMany(p => matrix.Dropoffs.Select(d => (p, d)))
                .Where(c => matrix.Cell(c.p, c.d) is not null && nodeOf[c.p].Key == edge.From && nodeOf[c.d].Key == edge.To)
                .Select(c => matrix.Cell(c.p, c.d)!)
                .ToList();
            var stations = nodeOf.Values.First(n => n.Key == edge.From).Kind != MapNodeKind.Surface
                && nodeOf.Values.First(n => n.Key == edge.To).Kind != MapNodeKind.Surface;
            if (stations)
            {
                edge.Parts.Should().ContainSingle().Which.Cell.Should().BeSameAs(matrix.Cell(edge.From, edge.To), $"{edge.From} → {edge.To} is one cell");
            }

            edge.Parts.Select(p => p.Cell).Should().BeEquivalentTo(cells, o => o.WithoutStrictOrdering().ComparingByMembers<PortCell>(), $"{edge.From} → {edge.To}");
            edge.Count.Should().Be(cells.Sum(c => c.Count));
        }

        var all = matrix.Pickups.SelectMany(p => matrix.Dropoffs.Select(d => matrix.Cell(p, d))).OfType<PortCell>().Sum(c => c.Count);
        (map.Edges.Sum(e => e.Count) + map.InsideOneNode).Should().Be(all, "no contract of the matrix is lost");
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{name}: {map.Nodes.Count} nodes, {map.Edges.Count} edges ({map.Edges.Count(e => e.Parts.Count > 1)} of several cells), {map.InsideOneNode} contracts inside one node"));
    }

    [Fact]
    public void The_strengths_split_the_Stanton_stellar_edges_into_three()
    {
        var map = RouteMap.Build(PortMatrix.Build(Data.Value.Templates, RouteFilter.Everything(StarSystem.Stanton) with { MissionType = HaulMissionType.Stellar }), NodeOf(StarSystem.Stanton), StarSystem.Stanton);

        long[] sums = [.. map.Edges.Select(e => e.RewardSum ?? 0).Order()];
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{sums.Length} edges; aUEC thirds at {sums[sums.Length / 3]:N0} and {sums[2 * sums.Length / 3]:N0}; strengths 1/2/3: {map.Edges.Count(e => e.Strength == 1)}/{map.Edges.Count(e => e.Strength == 2)}/{map.Edges.Count(e => e.Strength == 3)}"));
        Enumerable.Range(1, 3).Should().OnlyContain(s => map.Edges.Count(e => e.Strength == s) >= sums.Length / 6, "each strength draws a fair share");
    }

    private static IReadOnlyDictionary<string, MapNode> NodeOf(StarSystem system)
    {
        var everything = PortMatrix.Build(Data.Value.Templates, RouteFilter.Everything(system));
        return MapNodes.For(everything.Pickups.Concat(everything.Dropoffs), Data.Value.Positions, system);
    }

    private static (RouteFilter Filter, StarSystem System) Filter(string name) => name switch
    {
        "Stanton stellar Senior" => (RouteFilter.Default with { MaxRank = HaulRank.Senior }, StarSystem.Stanton),
        "Stanton all areas Master" => (RouteFilter.Everything(StarSystem.Stanton), StarSystem.Stanton),
        _ => (RouteFilter.Default with { System = StarSystem.Pyro }, StarSystem.Pyro),
    };
}
