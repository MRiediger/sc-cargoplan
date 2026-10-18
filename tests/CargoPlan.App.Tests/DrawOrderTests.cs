using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public class DrawOrderTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    [Fact]
    public void Hull_A_draws_starboard_lower_first_and_port_upper_last()
    {
        DrawOrder.BackToFront(HullA.Racks).Select(r => r.Number).Should().Equal(4, 2, 3, 1);
    }

    [Fact]
    public void Hull_A_draws_starboard_before_port_on_each_level()
    {
        var order = DrawOrder.BackToFront(HullA.Racks).ToList();

        foreach (var level in Enum.GetValues<RackLevel>())
        {
            var starboard = order.FindIndex(r => r.Position == new RackPosition(RackSide.Starboard, level));
            var port = order.FindIndex(r => r.Position == new RackPosition(RackSide.Port, level));
            starboard.Should().BeLessThan(port, $"the starboard {level} rack lies behind the port one");
        }
    }

    [Fact]
    public void Hull_A_draws_lower_before_upper_on_each_side()
    {
        var order = DrawOrder.BackToFront(HullA.Racks).ToList();

        foreach (var side in Enum.GetValues<RackSide>())
        {
            var lower = order.FindIndex(r => r.Position == new RackPosition(side, RackLevel.Lower));
            var upper = order.FindIndex(r => r.Position == new RackPosition(side, RackLevel.Upper));
            lower.Should().BeLessThan(upper, $"the {side} upper rack covers the lower one from above");
        }
    }

    [Fact]
    public void Equal_depth_falls_back_to_rack_order()
    {
        // Racks 2 and 3 of the Hull A have the same depth; they don't overlap on screen, the order only has to be stable.
        Rack[] reversed = [.. HullA.Racks.Reverse()];

        DrawOrder.BackToFront(reversed).Select(r => r.Number).Should().Equal(4, 2, 3, 1);
    }
}
