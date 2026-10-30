using System.Windows;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public class IsoProjectionTests
{
    private static readonly IsoProjection Default = IsoProjection.Default;

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

    [Theory]
    [InlineData("hull_a", false)]
    [InlineData("c2_hercules", true)]
    [InlineData("starfarer", true)]
    [InlineData("caterpillar", true)]
    // Long as a generated row of 16 racks (144 cells); two crosses since K5.2 (D78), 17 cells long and 32 wide.
    [InlineData("hull_c", false)]
    public void A_ship_longer_than_twice_its_width_is_long(string shipId, bool isLong)
    {
        IsoProjection.IsLong(ShipLoader.Load(shipId).Bounds).Should().Be(isLong);
    }

    [Fact]
    public void Exactly_twice_as_long_as_wide_is_not_long()
    {
        IsoProjection.IsLong(new ShipBox(new ShipPoint(0, 0, 0), new ShipPoint(10, 5, 3))).Should().BeFalse();
        IsoProjection.IsLong(new ShipBox(new ShipPoint(0, 0, 0), new ShipPoint(11, 5, 3))).Should().BeTrue();
    }

    [Fact]
    public void A_compact_ship_keeps_the_default_camera()
    {
        IsoProjection.DefaultFor(ShipLoader.Load("hull_a").Bounds).Should().BeSameAs(Default);
    }

    [Fact]
    public void A_long_ship_is_seen_from_port_above_with_its_length_flatter_than_by_default()
    {
        var bounds = ShipLoader.Load("caterpillar").Bounds;
        var orientation = IsoProjection.DefaultFor(bounds);
        var centre = ScreenPoints.Centre(bounds);
        CameraMath camera = new(orientation, 200, centre, new Size(800, 600));
        var bow = camera.Project(centre + new System.Windows.Media.Media3D.Vector3D(1, 0, 0)) - camera.Project(centre);

        // The bow still points left (D15), the length runs flatter than the default's 30° (D61).
        orientation.Pitch.Should().Be(IsoProjection.DefaultPitch);
        bow.X.Should().BeNegative();
        Math.Abs(bow.Y / bow.X).Should().BeLessThan(Math.Tan(Math.PI / 6) - 1e-9);
        BoxFaces.Visible(bounds, camera).Select(f => f.Side).Should().Contain([BoxFace.Top, BoxFace.Port]);
    }
}
