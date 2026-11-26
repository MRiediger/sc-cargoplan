using System.Globalization;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using Xunit.Abstractions;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The map's layout on the pinned release (T13.7, D128): every place the templates of a system name, laid out for the
/// view the overlay gets for the map (K3: 1100 × 700 asserted, 470 × 700 counted). A data test of the baseline (A10).
/// </summary>
public class MapLayoutBaselineTests(ITestOutputHelper output)
{
    private static readonly MapSize Wide = new(1100, 700);

    private static readonly MapSize Narrow = new(470, 700);

    private static readonly Lazy<HaulingData> Data = new(() =>
        ((HaulingLoad.Loaded)HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder))).Data);

    [Theory]
    [InlineData(StarSystem.Stanton)]
    [InlineData(StarSystem.Pyro)]
    public void At_1100_by_700_no_two_shown_labels_overlap_and_from_one_and_a_half_none_at_all(StarSystem system)
    {
        var nodes = Universe(system);
        var layout = MapLayout.For(nodes, Wide, TestLabels.Size);

        var hidden = nodes.Where(n => !layout.ShownLabels(1).Contains(n.Key)).Select(n => n.Label).ToList();
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{system}: {nodes.Count} nodes, {hidden.Count} labels only from {MapLayout.AllLabelsZoom}×: {string.Join(", ", hidden)}"));
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{system} at {Narrow.Width} × {Narrow.Height}: {Overlaps(MapLayout.For(nodes, Narrow, TestLabels.Size), 1)} overlapping pairs at 1×, {Overlaps(MapLayout.For(nodes, Narrow, TestLabels.Size), MapLayout.AllLabelsZoom)} at {MapLayout.AllLabelsZoom}× (counted, K3)"));

        Overlaps(layout, 1).Should().Be(0);
        Overlaps(layout, MapLayout.AllLabelsZoom).Should().Be(0);
    }

    [Theory]
    [InlineData("Everus Harbor")]
    [InlineData("Baijini Point")]
    [InlineData("Port Tressler")]
    [InlineData("Seraphim Station")]
    public void A_hubs_angle_about_the_star_is_its_angle_in_the_data(string hub)
    {
        var layout = MapLayout.For(Universe(StarSystem.Stanton), Wide, TestLabels.Size);

        Polar(layout, hub).Should().BeApproximately(RawAngle(hub), 2);
    }

    [Theory]
    [InlineData("HUR-L3 Thundering Express Station", 180)]
    [InlineData("HUR-L4 Melodic Fields Station", 60)]
    [InlineData("HUR-L5 High Course Station", -60)]
    public void HUR_L3_lies_opposite_Everus_across_the_star_and_L4_L5_before_and_behind_it_on_its_orbit(string station, double fromEverus)
    {
        var layout = MapLayout.For(Universe(StarSystem.Stanton), Wide, TestLabels.Size);

        var angle = Polar(layout, station) - Polar(layout, "Everus Harbor");
        Math.Abs(Normalized(angle - fromEverus)).Should().BeLessThanOrEqualTo(2);
    }

    [Theory]
    [InlineData(StarSystem.Stanton, "Lorville", "Hurston (surface)")]
    [InlineData(StarSystem.Stanton, "Ita", "Hurston (surface)")]
    [InlineData(StarSystem.Stanton, "Magda", "Hurston (surface)")]
    [InlineData(StarSystem.Stanton, "Ruptura PAF-I", "Hurston (surface)")]
    [InlineData(StarSystem.Stanton, "Area18", "ArcCorp (surface)")]
    [InlineData(StarSystem.Stanton, "Lyria", "ArcCorp (surface)")]
    [InlineData(StarSystem.Stanton, "Wala", "ArcCorp (surface)")]
    [InlineData(StarSystem.Stanton, "Covalex Distribution Centre S4DC05", "microTech (surface)")]
    [InlineData(StarSystem.Pyro, "Last Landings", "Terminus (surface)")]
    [InlineData(StarSystem.Pyro, "Shepherd's Rest", "Bloom (surface)")]
    [InlineData(StarSystem.Pyro, "Rustville", "Pyro I (surface)")]
    public void A_surface_place_joins_its_nearest_planet(StarSystem system, string place, string node) =>
        MapNodes.For([place], Data.Value.Positions, system)[place].Key.Should().Be(node);

    [Fact]
    public void Ruin_Station_and_the_surface_of_Terminus_under_it_lie_apart()
    {
        var layout = MapLayout.For(Universe(StarSystem.Pyro), Wide, TestLabels.Size);

        var (ruin, terminus) = (layout.Points["Ruin Station"], layout.Points["Terminus (surface)"]);
        Math.Sqrt(Math.Pow(ruin.X - terminus.X, 2) + Math.Pow(ruin.Y - terminus.Y, 2)).Should().BeApproximately(MapLayout.SpreadPx, 1e-6);
    }

    internal static IReadOnlyList<MapNode> Universe(StarSystem system)
    {
        var matrix = PortMatrix.Build(Data.Value.Templates, RouteFilter.Everything(system));
        return [.. MapNodes.For(matrix.Pickups.Concat(matrix.Dropoffs), Data.Value.Positions, system).Values.Distinct()];
    }

    // Pairs of shown labels that share area at zoom.
    private static int Overlaps(MapLayout layout, double zoom)
    {
        MapRect[] boxes = [.. layout.ShownLabels(zoom).Select(k => layout.Labels[k].Box(layout.Project(layout.Points[k], zoom, default)))];
        return boxes.SelectMany((a, i) => boxes.Skip(i + 1).Where(a.Intersects)).Count();
    }

    private static double Polar(MapLayout layout, string key)
    {
        var p = layout.Points[key];
        return Math.Atan2(layout.Centre.Y - p.Y, p.X - layout.Centre.X) * 180 / Math.PI;
    }

    private static double RawAngle(string name)
    {
        var p = Data.Value.Positions.Find(StarSystem.Stanton, name)!;
        return Math.Atan2(p.Y, p.X) * 180 / Math.PI;
    }

    private static double Normalized(double degrees) => ((degrees % 360) + 540) % 360 - 180;
}
