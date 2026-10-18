using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public class DrawOrderTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");
    private static readonly IReadOnlyList<SceneRack> Racks = ShipViewBuilder.Build(new LoadPlan(HullA.Id, [], []), HullA, []).Racks;

    private static readonly IsoProjection FromBelow = new(IsoProjection.DefaultYaw, -IsoProjection.DefaultPitch);
    private static readonly IsoProjection FromAftStarboard = new(IsoProjection.DefaultYaw + 180, IsoProjection.DefaultPitch);

    private static RackPosition PositionOf(SceneRack rack) => HullA.Racks.Single(r => r.Number == rack.Number).Position;

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
}
