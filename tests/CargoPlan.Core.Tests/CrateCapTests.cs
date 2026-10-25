using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The largest crate a ship takes and why (D70): a door where some rack's grid would take more than the cap, the largest
/// rack where the grid itself is the limit, none where every crate fits somewhere.
/// </summary>
public class CrateCapTests
{
    private static Rack Rack(int index, int length, int width, int stack, CrateSize? door = null) =>
        new($"rack-{index}", index, length * width * stack, new Dimensions(length, width, stack), null, new ShipPoint(index * 10, 0, 0), StackAxis.PlusZ, door);

    private static CrateCap Of(params Rack[] racks) => CrateCap.Of(new Ship("test", "Test", racks.Sum(r => r.CapacityScu), racks));

    [Fact]
    public void A_door_below_the_grid_is_the_door()
    {
        Of(Rack(0, 2, 2, 2, CrateSize.Four)).Should().Be(new CrateCap(CrateSize.Four, CapReason.Door));
    }

    [Fact]
    public void A_door_equal_to_the_grid_is_the_largest_rack()
    {
        Of(Rack(0, 2, 2, 2, CrateSize.Eight)).Should().Be(new CrateCap(CrateSize.Eight, CapReason.LargestRack));
    }

    [Fact]
    public void Without_a_door_the_grid_decides()
    {
        Of(Rack(0, 4, 2, 2)).Should().Be(new CrateCap(CrateSize.Sixteen, CapReason.LargestRack));
        Of(Rack(0, 8, 2, 2)).Should().Be(CrateCap.None);
    }

    // The door rack's grid takes 8, the other rack 16 without a door: the cap is 16, and no grid takes more.
    [Fact]
    public void A_larger_rack_without_a_door_makes_the_door_no_cap()
    {
        Of(Rack(0, 2, 2, 2, CrateSize.Two), Rack(1, 4, 2, 2)).Should().Be(new CrateCap(CrateSize.Sixteen, CapReason.LargestRack));
    }

    // The door rack's grid takes 16, the other rack only 4: the cap is 4, and a grid would take more.
    [Fact]
    public void A_door_that_holds_back_a_larger_grid_is_the_door()
    {
        Of(Rack(0, 4, 2, 2, CrateSize.Four), Rack(1, 2, 2, 1)).Should().Be(new CrateCap(CrateSize.Four, CapReason.Door));
    }

    [Fact]
    public void The_shipped_ships_Hull_A_and_Clipper()
    {
        CrateCap.Of(ShipLoader.Load("hull_a")).Should().Be(new CrateCap(CrateSize.Sixteen, CapReason.LargestRack));
        CrateCap.Of(ShipLoader.Load("clipper")).Should().Be(new CrateCap(CrateSize.Two, CapReason.Door));
    }

    [Fact]
    public void Texts_name_the_reason()
    {
        var door = new CrateCap(CrateSize.Two, CapReason.Door);
        var rack = new CrateCap(CrateSize.Sixteen, CapReason.LargestRack);

        (door.Text, door.Notice).Should().Be(("door limit 2 SCU", "crates capped to 2 SCU (door limit)"));
        (rack.Text, rack.Notice).Should().Be(("largest rack takes 16 SCU", "crates capped to 16 SCU (largest rack)"));
    }

    [Fact]
    public void A_derived_mix_is_split_up_to_the_cap_and_a_set_mix_stays()
    {
        var door = new CrateCap(CrateSize.Two, CapReason.Door);
        var derived = new MissionItem("Titanium", 8, CrateSize.Eight);
        var set = new MissionItem("Titanium", 8, CrateSize.Eight, [CrateSize.Four, CrateSize.Four]);

        derived.CrateListUnder(door).Should().Equal(CrateSize.Two, CrateSize.Two, CrateSize.Two, CrateSize.Two);
        derived.CrateListUnder(CrateCap.None).Should().Equal(CrateSize.Eight);
        set.CrateListUnder(door).Should().Equal(CrateSize.Four, CrateSize.Four);
        (derived.CappedBy(door), derived.CappedBy(CrateCap.None), set.CappedBy(door)).Should().Be((true, false, false));
    }
}
