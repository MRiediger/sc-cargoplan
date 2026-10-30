using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

public class CameraMathTests
{
    private static readonly Size View = new(800, 600);
    private static readonly Point3D Target = new(4, 2, 1);

    private static CameraMath At(double yaw, double pitch, double distance = 30) => new(new IsoProjection(yaw, pitch), distance, Target, View);

    [Fact]
    public void The_target_lies_in_the_middle_of_the_view()
    {
        At(45, 35.26).Project(Target).Should().Be(new Point(400, 300));
    }

    [Fact]
    public void The_camera_looks_along_the_isometric_view_direction_from_its_distance()
    {
        var camera = new CameraMath(IsoProjection.Default, 30, Target, View);
        var (x, y, z) = IsoProjection.Default.ViewDirection;

        camera.LookDirection.Should().Be(new Vector3D(x, y, z));
        (camera.Position - Target).Length.Should().BeApproximately(30, 1e-9);
        Vector3D.DotProduct(camera.UpDirection, camera.LookDirection).Should().BeApproximately(0, 1e-12);
    }

    [Fact]
    public void In_the_default_view_the_bow_points_left_down_port_right_down_and_up_stays_up()
    {
        var camera = At(IsoProjection.DefaultYaw, IsoProjection.DefaultPitch);
        var middle = camera.Project(Target);

        var bow = camera.Project(Target + new Vector3D(1, 0, 0)) - middle;
        var port = camera.Project(Target + new Vector3D(0, 1, 0)) - middle;
        var up = camera.Project(Target + new Vector3D(0, 0, 1)) - middle;

        (bow.X < 0 && bow.Y > 0).Should().BeTrue($"bow went {bow}");
        (port.X > 0 && port.Y > 0).Should().BeTrue($"port went {port}");
        (Math.Abs(up.X) < 1e-9 && up.Y < 0).Should().BeTrue($"up went {up}");
    }

    [Fact]
    public void A_cell_across_the_view_at_the_target_is_px_per_cell_wide_from_the_horizontal_field_of_view()
    {
        var camera = At(30, 20, distance: 25);
        var right = Vector3D.CrossProduct(camera.LookDirection, camera.UpDirection);

        var width = (camera.Project(Target + right) - camera.Project(Target)).Length;

        camera.PxPerCellAtTarget.Should().BeApproximately(800 / (2 * 25 * Math.Tan(20 * Math.PI / 180)), 1e-9);
        width.Should().BeApproximately(camera.PxPerCellAtTarget, 1e-9);
    }

    [Fact]
    public void Half_the_field_of_view_to_the_right_lands_on_the_right_edge_whatever_the_height()
    {
        var camera = new CameraMath(IsoProjection.Default, 10, Target, new Size(800, 200));
        var right = Vector3D.CrossProduct(camera.LookDirection, camera.UpDirection);
        var edge = Target + 10 * Math.Tan(CameraMath.FieldOfView / 2 * Math.PI / 180) * right;

        camera.Project(edge).X.Should().BeApproximately(800, 1e-9);
    }

    [Fact]
    public void Nearer_is_larger()
    {
        var camera = At(45, 35.26);
        var look = camera.LookDirection;
        var up = camera.UpDirection;

        var near = (camera.Project(Target - 10 * look + up) - camera.Project(Target - 10 * look)).Length;
        var far = (camera.Project(Target + 10 * look + up) - camera.Project(Target + 10 * look)).Length;

        near.Should().BeGreaterThan(far);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(45, 35.26)]
    [InlineData(200, -80)]
    [InlineData(330, 80)]
    public void Unproject_gives_the_ray_from_the_camera_through_what_projects_there(double yaw, double pitch)
    {
        var camera = At(yaw, pitch);
        Point3D point = new(7, -3, 2.5);

        var ray = camera.Unproject(camera.Project(point));

        ray.Origin.Should().Be(camera.Position);
        ray.Direction.Length.Should().BeApproximately(1, 1e-12);
        var toPoint = point - ray.Origin;
        Vector3D.CrossProduct(toPoint, ray.Direction).Length.Should().BeApproximately(0, 1e-9);
        Vector3D.DotProduct(toPoint, ray.Direction).Should().BePositive();
    }

    [Fact]
    public void The_middle_of_the_view_unprojects_to_the_look_direction()
    {
        var camera = At(120, -20);

        (camera.Unproject(new Point(400, 300)).Direction - camera.LookDirection).Length.Should().BeApproximately(0, 1e-12);
    }

    [Fact]
    public void Yaw_and_pitch_come_wrapped_and_clamped_from_the_orientation()
    {
        var camera = At(400, 100);

        camera.Orientation.Yaw.Should().Be(40);
        camera.Orientation.Pitch.Should().Be(IsoProjection.MaxPitch);
    }

    [Fact]
    public void The_near_plane_is_small_and_the_far_plane_lies_beyond_the_target()
    {
        var camera = At(45, 35.26, distance: 3);

        camera.Near.Should().BeLessThan(0.1);
        camera.Far.Should().BeGreaterThan(camera.Distance + 1000);
    }
}
