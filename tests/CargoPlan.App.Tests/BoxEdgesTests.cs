using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

public class BoxEdgesTests
{
    [Fact]
    public void A_box_has_twelve_edges_four_along_each_axis()
    {
        var edges = BoxEdges.Of(new Point3D(0, 0, 0), new Point3D(4, 2, 1));

        edges.Should().HaveCount(12);
        edges.Count(e => (e.To - e.From) == new Vector3D(4, 0, 0)).Should().Be(4);
        edges.Count(e => (e.To - e.From) == new Vector3D(0, 2, 0)).Should().Be(4);
        edges.Count(e => (e.To - e.From) == new Vector3D(0, 0, 1)).Should().Be(4);
    }

    [Fact]
    public void A_dashed_edge_is_dashes_and_gaps_from_its_start()
    {
        var dashes = BoxEdges.Dashed(new Point3D(0, 0, 0), new Point3D(10, 0, 0), dash: 2, gap: 1);

        dashes.Select(d => (d.From.X, d.To.X)).Should().Equal((0, 2), (3, 5), (6, 8), (9, 10));
    }

    [Fact]
    public void A_dash_longer_than_the_edge_is_the_edge()
    {
        BoxEdges.Dashed(new Point3D(0, 0, 0), new Point3D(0, 1, 0), dash: 5, gap: 1).Should().ContainSingle()
            .Which.Should().Be(new BoxEdges.Edge(new Point3D(0, 0, 0), new Point3D(0, 1, 0)));
    }
}
