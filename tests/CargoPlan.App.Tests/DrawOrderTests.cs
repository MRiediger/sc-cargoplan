using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public class DrawOrderTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");
    private static readonly IReadOnlyList<SceneRack> Racks = ShipViewBuilder.Build(new LoadPlan(HullA.Id, [], []), HullA, [], FillOrder.BowFirst).Racks;

    private static readonly IsoProjection FromBelow = new(IsoProjection.DefaultYaw, -IsoProjection.DefaultPitch);
    private static readonly IsoProjection FromAftStarboard = new(IsoProjection.DefaultYaw + 180, IsoProjection.DefaultPitch);

    private static RackPosition PositionOf(SceneRack rack) => HullA.Racks.Single(r => r.Number == rack.Number).Position;

    private static SceneBox Box(int x0, int y0, int z0, int x1, int y1, int z1) =>
        new(new ShipBox(new ShipPoint(x0, y0, z0), new ShipPoint(x1, y1, z1)), CellFill.Free, null, null, null, null);

    [Fact]
    public void Hull_A_draws_starboard_lower_first_and_port_upper_last()
    {
        DrawOrder.BackToFront(Racks, IsoProjection.Default).Select(r => r.Number).Should().Equal(4, 2, 3, 1);
    }

    [Fact]
    public void Hull_A_draws_starboard_before_port_on_each_level()
    {
        var order = DrawOrder.BackToFront(Racks, IsoProjection.Default).ToList();

        foreach (var level in Enum.GetValues<RackLevel>())
        {
            var starboard = order.FindIndex(r => PositionOf(r) == new RackPosition(RackSide.Starboard, level));
            var port = order.FindIndex(r => PositionOf(r) == new RackPosition(RackSide.Port, level));
            starboard.Should().BeLessThan(port, $"the starboard {level} rack lies behind the port one");
        }
    }

    [Fact]
    public void Hull_A_draws_lower_before_upper_on_each_side()
    {
        var order = DrawOrder.BackToFront(Racks, IsoProjection.Default).ToList();

        foreach (var side in Enum.GetValues<RackSide>())
        {
            var lower = order.FindIndex(r => PositionOf(r) == new RackPosition(side, RackLevel.Lower));
            var upper = order.FindIndex(r => PositionOf(r) == new RackPosition(side, RackLevel.Upper));
            lower.Should().BeLessThan(upper, $"the {side} upper rack covers the lower one from above");
        }
    }

    [Fact]
    public void Equal_depth_falls_back_to_rack_order()
    {
        // Racks 2 and 3 of the Hull A have the same depth; they don't overlap on screen, the order only has to be stable.
        SceneRack[] reversed = [.. Racks.Reverse()];

        DrawOrder.BackToFront(reversed, IsoProjection.Default).Select(r => r.Number).Should().Equal(4, 2, 3, 1);
    }

    [Fact]
    public void From_below_the_upper_racks_are_drawn_first()
    {
        DrawOrder.BackToFront(Racks, FromBelow).Select(r => r.Number).Should().Equal(3, 1, 4, 2);
    }

    [Fact]
    public void From_aft_and_starboard_port_is_drawn_first()
    {
        DrawOrder.BackToFront(Racks, FromAftStarboard).Select(r => r.Number).Should().Equal(2, 1, 4, 3);
    }

    [Fact]
    public void Equal_cells_come_by_centre_depth_ties_in_fill_order()
    {
        // An empty rack is 16 equal cells: the centre depth is already a correct order, the painter keeps it.
        var cells = Racks[1].Boxes;

        var order = DrawOrder.BoxesBackToFront(cells, IsoProjection.Default).Select(b => (Depth: Depth(b.Bounds), Index: IndexOf(cells, b))).ToList();

        order.Select(o => o.Depth).Should().BeInDescendingOrder();
        order.Zip(order.Skip(1)).Where(pair => pair.First.Depth == pair.Second.Depth)
            .Should().NotBeEmpty().And.OnlyContain(pair => pair.First.Index < pair.Second.Index);
    }

    [Fact]
    public void A_separating_axis_puts_the_box_away_from_the_camera_first()
    {
        // The default camera looks along −(1, 1, 1): the box at larger x lies nearer.
        var (aft, bow) = (Box(0, 0, 0, 1, 1, 1).Bounds, Box(1, 0, 0, 3, 1, 1).Bounds);

        DrawOrder.Compare(aft, bow, IsoProjection.Default).Should().BeNegative();
        DrawOrder.Compare(bow, aft, IsoProjection.Default).Should().BePositive();
    }

    [Fact]
    public void Separating_axes_that_disagree_decide_nothing()
    {
        // Apart on x (bow box nearer) and on y (port box nearer): side by side on screen, no order needed.
        DrawOrder.Compare(Box(1, 0, 0, 2, 1, 1).Bounds, Box(0, 1, 0, 1, 2, 1).Bounds, IsoProjection.Default).Should().Be(0);
    }

    [Theory]
    [InlineData(45, 35.26)]
    [InlineData(225, 35.26)]
    [InlineData(135, -60)]
    [InlineData(300, 20)]
    public void An_8_SCU_crate_next_to_two_4s_is_drawn_in_ray_order(double yaw, double pitch)
    {
        var camera = new IsoProjection(yaw, pitch);
        Placement Crates(string destination, int scu, CrateSize size) => new(destination, "rack-0", new CargoItem("Titanium", scu, size));
        LoadPlan plan = new(HullA.Id, [Crates("Riker", 4, CrateSize.Four), Crates("Shubin", 4, CrateSize.Four), Crates("Orison", 8, CrateSize.Eight)], []);
        var rack1 = ShipViewBuilder.Build(plan, HullA, [], FillOrder.BowFirst).Racks[0];

        var order = DrawOrder.BoxesBackToFront(rack1.Boxes, camera);

        order.Should().HaveCount(3);
        ShouldMatchRays([.. order.Select(b => b.Bounds)], camera);
    }

    [Theory]
    [InlineData(45, 35.26)]
    [InlineData(225, 35.26)]
    [InlineData(135, -60)]
    [InlineData(300, 20)]
    public void The_whole_fixture_is_drawn_in_ray_order_rack_by_rack(double yaw, double pitch)
    {
        var camera = new IsoProjection(yaw, pitch);
        var (ship, plan) = TestPlans.Load();
        var racks = DrawOrder.BackToFront(ShipViewBuilder.Build(plan, ship, LoadPlanner.Validate(plan, ship), FillOrder.BowFirst).Racks, camera);

        ShouldMatchRays([.. racks.Select(r => r.Bounds)], camera);
        ShouldMatchRays([.. racks.SelectMany(r => DrawOrder.BoxesBackToFront(r.Boxes, camera)).Select(b => b.Bounds)], camera);
    }

    [Fact]
    public void A_cycle_falls_back_to_centre_depth_and_still_draws_every_box()
    {
        // A pinwheel: each stick covers the next one, so no order of the three is right.
        SceneBox[] sticks = [Box(1, 0, 1, 4, 1, 2), Box(0, 1, 0, 2, 3, 2), Box(2, 0, 0, 4, 3, 1)];
        var camera = IsoProjection.Default;

        DrawOrder.Compare(sticks[0].Bounds, sticks[1].Bounds, camera).Should().BeNegative();
        DrawOrder.Compare(sticks[1].Bounds, sticks[2].Bounds, camera).Should().BeNegative();
        DrawOrder.Compare(sticks[2].Bounds, sticks[0].Bounds, camera).Should().BeNegative();
        var order = DrawOrder.BoxesBackToFront(sticks, camera);

        order.Should().BeEquivalentTo(sticks);
        order.Select(b => Depth(b.Bounds)).Should().BeInDescendingOrder();
    }

    /// <summary>
    /// An independent check of a draw order: from points inside each visible face of a box a ray runs along the view
    /// direction. A box it meets towards the camera lies in front and must be drawn later; one it meets behind, earlier.
    /// </summary>
    private static void ShouldMatchRays(IReadOnlyList<ShipBox> order, IsoProjection camera)
    {
        var direction = camera.ViewDirection;
        for (var i = 0; i < order.Count; i++)
        {
            foreach (var point in SamplePoints(order[i], SceneProjector.VisibleFaces(camera)))
            {
                for (var j = 0; j < order.Count; j++)
                {
                    if (j == i || Hit(order[j], point, direction) is not { } t)
                    {
                        continue;
                    }

                    // t < 0: between the point and the camera.
                    if (t < 0)
                    {
                        j.Should().BeGreaterThan(i, $"{order[j]} lies in front of {order[i]}");
                    }
                    else
                    {
                        j.Should().BeLessThan(i, $"{order[j]} lies behind {order[i]}");
                    }
                }
            }
        }
    }

    /// <summary>Points at a quarter, half and three quarters across each face, on the face.</summary>
    private static IEnumerable<(double X, double Y, double Z)> SamplePoints(ShipBox box, IReadOnlyList<BoxFace> faces)
    {
        double[] steps = [0.25, 0.5, 0.75];
        static double Across(int min, int max, double t) => min + (max - min) * t;
        var (min, max) = (box.Min, box.Max);

        return from face in faces
               from u in steps
               from v in steps
               select face switch
               {
                   BoxFace.Top => (Across(min.X, max.X, u), Across(min.Y, max.Y, v), (double)max.Z),
                   BoxFace.Bottom => (Across(min.X, max.X, u), Across(min.Y, max.Y, v), (double)min.Z),
                   BoxFace.Port => (Across(min.X, max.X, u), (double)max.Y, Across(min.Z, max.Z, v)),
                   BoxFace.Starboard => (Across(min.X, max.X, u), (double)min.Y, Across(min.Z, max.Z, v)),
                   BoxFace.Bow => ((double)max.X, Across(min.Y, max.Y, u), Across(min.Z, max.Z, v)),
                   _ => ((double)min.X, Across(min.Y, max.Y, u), Across(min.Z, max.Z, v)),
               };
    }

    /// <summary>
    /// Slab test: the middle of the stretch of point + t · direction inside the box, or null if the ray misses it or
    /// only grazes an edge.
    /// </summary>
    private static double? Hit(ShipBox box, (double X, double Y, double Z) point, (double X, double Y, double Z) direction)
    {
        var (enter, leave) = (double.NegativeInfinity, double.PositiveInfinity);
        (double From, double Along, int Min, int Max)[] axes =
        [
            (point.X, direction.X, box.Min.X, box.Max.X),
            (point.Y, direction.Y, box.Min.Y, box.Max.Y),
            (point.Z, direction.Z, box.Min.Z, box.Max.Z),
        ];

        foreach (var (from, along, min, max) in axes)
        {
            if (Math.Abs(along) < 1e-12)
            {
                if (from <= min || from >= max)
                {
                    return null;
                }

                continue;
            }

            var (a, b) = ((min - from) / along, (max - from) / along);
            (enter, leave) = (Math.Max(enter, Math.Min(a, b)), Math.Min(leave, Math.Max(a, b)));
        }

        return leave - enter > 1e-6 ? (enter + leave) / 2 : null;
    }

    private static int IndexOf(IReadOnlyList<SceneBox> boxes, SceneBox box) => boxes.ToList().IndexOf(box);

    private static double Depth(ShipBox box) => Math.Round(
        IsoProjection.Default.Depth((box.Min.X + box.Max.X) / 2.0, (box.Min.Y + box.Max.Y) / 2.0, (box.Min.Z + box.Max.Z) / 2.0), 9);
}
