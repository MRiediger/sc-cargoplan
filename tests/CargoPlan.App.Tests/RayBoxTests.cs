using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

public class RayBoxTests
{
    private static readonly Point3D Min = new(0, 0, 0);
    private static readonly Point3D Max = new(2, 1, 1);

    [Fact]
    public void A_ray_towards_the_box_enters_it_at_the_near_face()
    {
        RayBox.Entry(new Ray(new Point3D(-3, 0.5, 0.5), new Vector3D(1, 0, 0)), Min, Max).Should().Be(3);
    }

    [Fact]
    public void A_ray_beside_the_box_misses_it()
    {
        RayBox.Entry(new Ray(new Point3D(-3, 1.5, 0.5), new Vector3D(1, 0, 0)), Min, Max).Should().BeNull();
    }

    [Fact]
    public void A_ray_parallel_to_a_face_outside_misses_it()
    {
        RayBox.Entry(new Ray(new Point3D(1, 0.5, 2), new Vector3D(1, 0, 0)), Min, Max).Should().BeNull();
    }

    [Fact]
    public void A_ray_away_from_the_box_enters_it_behind_its_origin()
    {
        RayBox.Entry(new Ray(new Point3D(-3, 0.5, 0.5), new Vector3D(-1, 0, 0)), Min, Max).Should().BeNegative();
    }

    [Fact]
    public void A_slanted_ray_enters_where_it_crosses_all_three_slabs()
    {
        var direction = new Vector3D(1, 1, 0);
        direction.Normalize();

        var entry = RayBox.Entry(new Ray(new Point3D(-1, -1, 0.5), direction), Min, Max);

        entry.Should().BeApproximately(Math.Sqrt(2), 1e-12);
    }

    [Fact]
    public void The_span_runs_from_the_near_face_to_the_far_face()
    {
        RayBox.Span(new Ray(new Point3D(-3, 0.5, 0.5), new Vector3D(1, 0, 0)), Min, Max).Should().Be((3.0, 5.0));
    }

    // --- Ahead: where a hit lies in front of the camera (T8.3) ---

    [Fact]
    public void A_box_in_front_is_hit_where_the_ray_enters_it()
    {
        RayBox.Ahead(new Ray(new Point3D(-3, 0.5, 0.5), new Vector3D(1, 0, 0)), Min, Max).Should().Be(3);
    }

    [Fact]
    public void A_box_around_the_origin_is_hit_at_once()
    {
        // The camera stands inside a large rack: the near plane cuts its walls, its cargo still answers.
        RayBox.Ahead(new Ray(new Point3D(1, 0.5, 0.5), new Vector3D(1, 0, 0)), Min, Max).Should().Be(0);
    }

    [Fact]
    public void A_box_behind_the_origin_is_not_hit()
    {
        RayBox.Ahead(new Ray(new Point3D(-3, 0.5, 0.5), new Vector3D(-1, 0, 0)), Min, Max).Should().BeNull();
    }

    [Fact]
    public void A_box_beside_the_ray_is_not_hit()
    {
        RayBox.Ahead(new Ray(new Point3D(-3, 1.5, 0.5), new Vector3D(1, 0, 0)), Min, Max).Should().BeNull();
    }
}
