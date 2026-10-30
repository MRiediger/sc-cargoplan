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
}
