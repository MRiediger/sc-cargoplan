using CargoPlan.App.Vr;
using Valve.VR;

namespace CargoPlan.App.Tests;

// D131: the user's yaw is positive = right of the view; OpenVR's +yaw turns left about +Y.
public class VrPlacementTests
{
    private const double Precision = 1e-4;

    [Fact]
    public void The_head_yaw_is_read_from_the_forward_axis_of_the_pose()
    {
        VrPlacement.HeadYawDegrees(Pose(12.7)).Should().BeApproximately(12.7, Precision);
        VrPlacement.HeadYawDegrees(Pose(-95)).Should().BeApproximately(-95, Precision);
    }

    [Fact]
    public void The_g4_log_example_gives_an_overlay_yaw_of_minus_17_3()
    {
        // S14.0 G4: headYaw(openvr)=12.7° userYaw=30.0° → overlayYaw(openvr)=-17.3°.
        var place = VrPlacement.Place(Pose(12.7), VrOffset.Default with { YawDegrees = 30 });

        place.HeadYawDegrees.Should().BeApproximately(12.7, Precision);
        place.OverlayYawDegrees.Should().BeApproximately(-17.3, Precision);
    }

    [Theory]
    [InlineData(30, 0.6)]
    [InlineData(-30, -0.6)]
    public void A_positive_user_yaw_puts_the_overlay_right_of_the_view(double userYaw, double x)
    {
        // Head at the origin looking down −Z: right is +X.
        var t = VrPlacement.Place(Pose(0), VrOffset.Default with { YawDegrees = userYaw, DistanceMeters = 1.2 }).Transform;

        ((double)t.m3).Should().BeApproximately(x, Precision);
        ((double)t.m7).Should().BeApproximately(0, Precision);
        ((double)t.m11).Should().BeApproximately(-1.2 * Math.Cos(30 * Math.PI / 180), Precision);
    }

    [Fact]
    public void The_overlay_faces_back_along_its_own_yaw()
    {
        // Pitch 0: the overlay's own yaw is the overlay yaw, so it faces the head.
        var place = VrPlacement.Place(Pose(40), VrOffset.Default with { YawDegrees = 25 });

        VrPlacement.HeadYawDegrees(place.Transform).Should().BeApproximately(place.OverlayYawDegrees, Precision);
        place.OverlayYawDegrees.Should().BeApproximately(15, Precision);
    }

    [Fact]
    public void Height_and_the_head_position_are_added()
    {
        var offset = VrOffset.Default with { YawDegrees = 0, DistanceMeters = 1.2, HeightMeters = 0.1 };

        var t = VrPlacement.Place(Pose(0, (0.1f, 1.5f, -0.2f)), offset).Transform;

        ((double)t.m3).Should().BeApproximately(0.1, Precision);
        ((double)t.m7).Should().BeApproximately(1.6, Precision);
        ((double)t.m11).Should().BeApproximately(-1.4, Precision);
    }

    [Fact]
    public void The_rotation_is_yaw_times_pitch()
    {
        // R = Ry(overlayYaw) · Rx(pitch); yaw 0 leaves Rx alone.
        var pitch = 20 * Math.PI / 180;
        var t = VrPlacement.Place(Pose(0), VrOffset.Default with { YawDegrees = 0, PitchDegrees = 20 }).Transform;

        ((double)t.m5).Should().BeApproximately(Math.Cos(pitch), Precision);
        ((double)t.m6).Should().BeApproximately(-Math.Sin(pitch), Precision);
        ((double)t.m9).Should().BeApproximately(Math.Sin(pitch), Precision);
        ((double)t.m10).Should().BeApproximately(Math.Cos(pitch), Precision);
        ((double)t.m0).Should().BeApproximately(1, Precision);
    }

    [Fact]
    public void The_rotation_stays_orthonormal()
    {
        var t = VrPlacement.Place(Pose(73, (0.3f, 1.1f, 0.4f)), VrOffset.Default with { YawDegrees = -12, PitchDegrees = 35 }).Transform;
        double[][] rows = [[t.m0, t.m1, t.m2], [t.m4, t.m5, t.m6], [t.m8, t.m9, t.m10]];

        foreach (var row in rows)
        {
            row.Sum(v => v * v).Should().BeApproximately(1, Precision);
        }

        Dot(rows[0], rows[1]).Should().BeApproximately(0, Precision);
        Dot(rows[0], rows[2]).Should().BeApproximately(0, Precision);
        Dot(rows[1], rows[2]).Should().BeApproximately(0, Precision);
    }

    [Fact]
    public void The_defaults_are_the_spike_values()
    {
        // D131: off / 30 / 0 / 1.2 / 0 / 0.5 / 1.0; on and off is the session's, not the offset's.
        VrOffset.Default.Should().Be(new VrOffset(30, 0, 1.2, 0, 0.5, 1.0));
    }

    /// <summary>An HMD pose turned by an OpenVR yaw (left = positive) about +Y, at a position.</summary>
    private static HmdMatrix34_t Pose(double openVrYawDegrees, (float X, float Y, float Z) at = default)
    {
        var (s, c) = Math.SinCos(openVrYawDegrees * Math.PI / 180);
        return new HmdMatrix34_t
        {
            m0 = (float)c,
            m1 = 0,
            m2 = (float)s,
            m3 = at.X,
            m4 = 0,
            m5 = 1,
            m6 = 0,
            m7 = at.Y,
            m8 = (float)-s,
            m9 = 0,
            m10 = (float)c,
            m11 = at.Z,
        };
    }

    private static double Dot(double[] a, double[] b) => (a[0] * b[0]) + (a[1] * b[1]) + (a[2] * b[2]);
}
