using System.Windows;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

public class IsoProjectionTests
{
    private static readonly double Cos30 = Math.Sqrt(3) / 2;
    private static readonly IsoProjection Default = IsoProjection.Default;

    private static void ShouldBeAt(Point actual, double x, double y)
    {
        actual.X.Should().BeApproximately(x, 1e-9);
        actual.Y.Should().BeApproximately(y, 1e-9);
    }

    [Fact]
    public void Origin_projects_to_the_screen_origin()
    {
        ShouldBeAt(Default.Project(0, 0, 0), 0, 0);
    }

    [Fact]
    public void Bow_points_left_down()
    {
        ShouldBeAt(Default.Project(1, 0, 0), -Cos30, 0.5);
    }

    [Fact]
    public void Port_points_right_down_towards_the_viewer()
    {
        ShouldBeAt(Default.Project(0, 1, 0), Cos30, 0.5);
    }

    [Fact]
    public void Up_points_up_on_the_screen()
    {
        ShouldBeAt(Default.Project(0, 0, 1), 0, -1);
    }

    [Fact]
    public void All_three_axes_keep_their_length()
    {
        // Isometric: one cell is equally long along x, y and z.
        double[] lengths =
        [
            ((Vector)Default.Project(1, 0, 0)).Length,
            ((Vector)Default.Project(0, 1, 0)).Length,
            ((Vector)Default.Project(0, 0, 1)).Length,
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
        var camera = new IsoProjection(123, -40);
        var near = camera.Project(1, 2, 3) - camera.Project(0, 0, 0);
        var far = camera.Project(x + 1, y + 2, z + 3) - camera.Project(x, y, z);

        far.X.Should().BeApproximately(near.X, 1e-9);
        far.Y.Should().BeApproximately(near.Y, 1e-9);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(2, 2, 5)]
    [InlineData(4, -2, 3)]
    [InlineData(-7, 5, 11)]
    public void Default_is_the_fixed_view_of_T3_5b(double x, double y, double z)
    {
        // The formula before T3.5c: u = (y − x)·cos 30°, v = (x + y)·sin 30° − z.
        ShouldBeAt(Default.Project(x, y, z), (y - x) * Cos30, (x + y) * 0.5 - z);
    }

    [Fact]
    public void Default_looks_along_minus_one_one_one()
    {
        var d = Default.ViewDirection;
        var expected = -1 / Math.Sqrt(3);

        d.X.Should().BeApproximately(expected, 1e-9);
        d.Y.Should().BeApproximately(expected, 1e-9);
        d.Z.Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData(-10, 350)]
    [InlineData(370, 10)]
    [InlineData(360, 0)]
    [InlineData(-720, 0)]
    public void Yaw_wraps_into_0_to_360(double yaw, double expected)
    {
        new IsoProjection(yaw, 0).Yaw.Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData(90, IsoProjection.MaxPitch)]
    [InlineData(-90, IsoProjection.MinPitch)]
    [InlineData(-30, -30)]
    public void Pitch_stays_within_its_limits(double pitch, double expected)
    {
        new IsoProjection(0, pitch).Pitch.Should().Be(expected);
    }

    [Fact]
    public void Orbiting_adds_to_yaw_and_pitch()
    {
        var orbited = new IsoProjection(350, 20).OrbitBy(20, -50);

        orbited.Yaw.Should().BeApproximately(10, 1e-9);
        orbited.Pitch.Should().BeApproximately(-30, 1e-9);
    }

    [Fact]
    public void Up_stays_up_when_seen_from_below()
    {
        new IsoProjection(45, -60).Project(0, 0, 1).Y.Should().BeNegative();
    }

    [Theory]
    [InlineData(45, 35)]
    [InlineData(45, -35)]
    [InlineData(225, 35)]
    [InlineData(170, -80)]
    public void Depth_grows_along_the_view_direction(double yaw, double pitch)
    {
        var camera = new IsoProjection(yaw, pitch);
        var d = camera.ViewDirection;

        camera.Depth(d.X, d.Y, d.Z).Should().BeGreaterThan(camera.Depth(0, 0, 0));
    }

    [Fact]
    public void From_below_the_floor_is_nearer_than_the_ceiling()
    {
        var below = new IsoProjection(45, -35);

        below.Depth(0, 0, 0).Should().BeLessThan(below.Depth(0, 0, 1));
        Default.Depth(0, 0, 0).Should().BeGreaterThan(Default.Depth(0, 0, 1));
    }

    [Theory]
    [InlineData(45, 35)]
    [InlineData(45, -35)]
    [InlineData(225, 35)]
    [InlineData(170, -80)]
    [InlineData(0, 0)]
    public void Unproject_gives_a_point_that_projects_back(double yaw, double pitch)
    {
        var camera = new IsoProjection(yaw, pitch);
        Point projected = new(1.7, -2.3);

        var (x, y, z) = camera.Unproject(projected);

        ShouldBeAt(camera.Project(x, y, z), projected.X, projected.Y);
    }

    [Fact]
    public void Unproject_lies_on_the_view_line_of_the_projected_point()
    {
        // A ship point and the unprojected point differ only along the view direction.
        var (x, y, z) = Default.Unproject(Default.Project(2, 3, 4));
        var d = Default.ViewDirection;
        var t = Default.Depth(2, 3, 4) - Default.Depth(x, y, z);

        x.Should().BeApproximately(2 - t * d.X, 1e-9);
        y.Should().BeApproximately(3 - t * d.Y, 1e-9);
        z.Should().BeApproximately(4 - t * d.Z, 1e-9);
    }
}
