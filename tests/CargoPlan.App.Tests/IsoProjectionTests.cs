using System.Windows;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

public class IsoProjectionTests
{
    private static readonly double Cos30 = Math.Sqrt(3) / 2;

    [Fact]
    public void Origin_projects_to_the_screen_origin()
    {
        IsoProjection.Project(0, 0, 0).Should().Be(new Point(0, 0));
    }

    [Fact]
    public void Bow_points_left_down()
    {
        var bow = IsoProjection.Project(1, 0, 0);

        bow.X.Should().BeApproximately(-Cos30, 1e-9);
        bow.Y.Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void Port_points_right_down_towards_the_viewer()
    {
        var port = IsoProjection.Project(0, 1, 0);

        port.X.Should().BeApproximately(Cos30, 1e-9);
        port.Y.Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void Up_points_up_on_the_screen()
    {
        IsoProjection.Project(0, 0, 1).Should().Be(new Point(0, -1));
    }

    [Fact]
    public void All_three_axes_keep_their_length()
    {
        // Isometric: one cell is equally long along x, y and z.
        double[] lengths =
        [
            ((Vector)IsoProjection.Project(1, 0, 0)).Length,
            ((Vector)IsoProjection.Project(0, 1, 0)).Length,
            ((Vector)IsoProjection.Project(0, 0, 1)).Length,
        ];

        lengths.Should().AllSatisfy(l => l.Should().BeApproximately(1, 1e-9));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(4, -2, 3)]
    [InlineData(-7, 5, 11)]
    public void The_same_step_gives_the_same_screen_step_anywhere(double x, double y, double z)
    {
        // No perspective: parallel edges stay parallel and equally long.
        var near = IsoProjection.Project(1, 2, 3) - IsoProjection.Project(0, 0, 0);
        var far = IsoProjection.Project(x + 1, y + 2, z + 3) - IsoProjection.Project(x, y, z);

        far.X.Should().BeApproximately(near.X, 1e-9);
        far.Y.Should().BeApproximately(near.Y, 1e-9);
    }

    [Fact]
    public void Depth_grows_away_from_the_viewer_at_bow_port_top()
    {
        IsoProjection.Depth(0, 0, 0).Should().BeGreaterThan(IsoProjection.Depth(1, 0, 0));
        IsoProjection.Depth(0, 0, 0).Should().BeGreaterThan(IsoProjection.Depth(0, 1, 0));
        IsoProjection.Depth(0, 0, 0).Should().BeGreaterThan(IsoProjection.Depth(0, 0, 1));
    }
}
