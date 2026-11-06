using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

/// <summary>
/// A rack of 4×2×2 cells at the ship's zero: x 0..4, y 0..2, z 0..2, its middle at (2, 1, 1). The step from where it lies
/// before its rotation to the ship (D91).
/// </summary>
public class RackTransformTests
{
    private const double Precision = 1e-9;

    private static readonly Point3D Middle = new(2, 1, 1);

    private static Rack RackOf(RackRotation? rotation) =>
        new("rack-0", 0, 16, new Dimensions(4, 2, 2), null, new ShipPoint(0, 0, 0), Rotation: rotation);

    private static RackTransform Turned(double yaw, double pitch, double roll) => RackTransform.Of(RackOf(new RackRotation(yaw, pitch, roll)));

    private static void ShouldBe(Point3D actual, Point3D expected)
    {
        actual.X.Should().BeApproximately(expected.X, Precision);
        actual.Y.Should().BeApproximately(expected.Y, Precision);
        actual.Z.Should().BeApproximately(expected.Z, Precision);
    }

    [Fact]
    public void A_straight_rack_hands_everything_back_unchanged()
    {
        var transform = RackTransform.Of(RackOf(null));
        Point3D point = new(0.1, 0.2, 0.3);
        Ray ray = new(point, new Vector3D(0, 0, -1));
        IReadOnlyList<BoxEdges.Edge> edges = BoxEdges.Of(point, new Point3D(1, 1, 1));
        var box = RackOf(null).Bounds;

        transform.Should().BeSameAs(RackTransform.Identity);
        transform.IsIdentity.Should().BeTrue();
        transform.ToShip(point).Should().Be(point);
        transform.ToStraight(point).Should().Be(point);
        transform.ToStraight(ray).Should().Be(ray);
        transform.ToShip(edges).Should().BeSameAs(edges);
        transform.Around(box).Should().BeSameAs(box);
    }

    [Fact]
    public void A_rack_turns_about_its_middle()
    {
        ShouldBe(Turned(30, 40, 10).ToShip(Middle), Middle);
    }

    [Fact]
    public void The_corners_of_a_rack_turned_by_45_degrees_lie_where_the_computation_says()
    {
        // The bow corner at starboard is (2, −1) from the middle in x and y; a quarter of a right angle further round.
        var half = Math.Sqrt(2) / 2;

        ShouldBe(Turned(45, 0, 0).ToShip(new Point3D(4, 0, 2)), new Point3D(2 + 3 * half, 1 + half, 2));
        ShouldBe(Turned(45, 0, 0).ToShip(new Point3D(0, 2, 0)), new Point3D(2 - 3 * half, 1 - half, 0));
    }

    [Fact]
    public void Positive_yaw_turns_the_bow_end_to_port()
    {
        ShouldBe(Turned(90, 0, 0).ToShip(new Point3D(4, 1, 1)), new Point3D(2, 3, 1));
    }

    [Fact]
    public void Positive_pitch_turns_the_bow_end_down()
    {
        ShouldBe(Turned(0, 90, 0).ToShip(new Point3D(4, 1, 1)), new Point3D(2, 1, -1));
    }

    [Fact]
    public void Positive_roll_turns_the_port_side_up()
    {
        ShouldBe(Turned(0, 0, 90).ToShip(new Point3D(2, 2, 1)), new Point3D(2, 1, 2));
    }

    [Fact]
    public void Roll_comes_first_then_pitch_then_yaw()
    {
        // Up goes to the bow by the pitch, then to port by the yaw; the other way round it would end at the bow.
        ShouldBe(Turned(90, 90, 0).ToShip(new Point3D(2, 1, 2)), new Point3D(2, 2, 1));

        // Port goes up by the roll, then to the bow by the pitch.
        ShouldBe(Turned(0, 90, 90).ToShip(new Point3D(2, 2, 1)), new Point3D(3, 1, 1));
    }

    [Fact]
    public void A_ray_keeps_its_length_and_comes_back_the_way_it_went()
    {
        var transform = Turned(30, 40, 10);
        Ray ray = new(new Point3D(5, -7, 11), new Vector3D(0.6, 0, -0.8));

        var straight = transform.ToStraight(ray);

        straight.Direction.Length.Should().BeApproximately(1, Precision);
        ShouldBe(transform.ToShip(straight.Origin), ray.Origin);
        ShouldBe((Point3D)transform.ToShip(straight.Direction), (Point3D)ray.Direction);
    }

    [Fact]
    public void Edges_map_with_both_ends()
    {
        BoxEdges.Edge[] edges = [new(new Point3D(4, 1, 1), new Point3D(2, 2, 1))];

        var mapped = Turned(90, 0, 0).ToShip(edges).Single();

        ShouldBe(mapped.From, new Point3D(2, 3, 1));
        ShouldBe(mapped.To, new Point3D(1, 1, 1));
    }

    [Fact]
    public void Around_a_right_angle_takes_no_extra_cell_for_rounding()
    {
        // cos 90° isn't quite 0 in doubles; rounding outward alone would make this box a cell too large.
        Turned(90, 0, 0).Around(RackOf(null).Bounds).Should().Be(new ShipBox(new ShipPoint(1, -1, 0), new ShipPoint(3, 3, 2)));
    }

    [Fact]
    public void Around_rounds_out_to_whole_cells()
    {
        // Half of the length and width along the diagonal: 3·√2/2 ≈ 2.12 cells from the middle.
        Turned(45, 0, 0).Around(RackOf(null).Bounds).Should().Be(new ShipBox(new ShipPoint(-1, -2, 0), new ShipPoint(5, 4, 2)));
    }

    [Fact]
    public void The_model_transform_is_the_same_step()
    {
        var transform = Turned(30, 40, 10);
        Point3D point = new(4, 0, 2);

        ShouldBe(transform.Model.Transform(point), transform.ToShip(point));
    }
}
