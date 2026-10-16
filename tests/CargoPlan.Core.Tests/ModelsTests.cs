using CargoPlan.Core.Models;

namespace CargoPlan.Core.Tests;

public class ModelsTests
{
    private static Rack EmptyRack(int index, int capacityScu = 16) =>
        new($"rack-{index}", index, capacityScu, []);

    private static IReadOnlyList<Rack> HullARacks() =>
        [EmptyRack(0), EmptyRack(1), EmptyRack(2), EmptyRack(3)];

    // --- CrateSize ---

    [Theory]
    [InlineData(CrateSize.One, 1)]
    [InlineData(CrateSize.Two, 2)]
    [InlineData(CrateSize.Four, 4)]
    [InlineData(CrateSize.Eight, 8)]
    public void CrateSize_value_equals_its_scu(CrateSize size, int expectedScu)
    {
        ((int)size).Should().Be(expectedScu);
    }

    // --- CargoItem ---

    [Theory]
    [InlineData(1, CrateSize.One)]
    [InlineData(6, CrateSize.Two)]
    [InlineData(12, CrateSize.Four)]
    [InlineData(16, CrateSize.Eight)]
    public void CargoItem_accepts_scu_that_is_a_multiple_of_crate_size(int scu, CrateSize size)
    {
        var item = new CargoItem("Titanium", scu, size);

        item.Scu.Should().Be(scu);
        item.CrateSize.Should().Be(size);
    }

    [Theory]
    [InlineData(3, CrateSize.Two)]
    [InlineData(10, CrateSize.Four)]
    [InlineData(12, CrateSize.Eight)]
    public void CargoItem_rejects_scu_that_is_not_a_multiple_of_crate_size(int scu, CrateSize size)
    {
        var act = () => new CargoItem("Titanium", scu, size);

        act.Should().Throw<ArgumentException>().WithMessage($"*{scu}*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void CargoItem_rejects_non_positive_scu(int scu)
    {
        var act = () => new CargoItem("Titanium", scu, CrateSize.Four);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void CargoItem_rejects_undefined_crate_size(int rawSize)
    {
        var act = () => new CargoItem("Titanium", 8, (CrateSize)rawSize);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CargoItem_has_value_equality()
    {
        var a = new CargoItem("Quartz", 10, CrateSize.Two);
        var b = new CargoItem("Quartz", 10, CrateSize.Two);

        a.Should().Be(b);
    }

    // --- Rack ---

    [Fact]
    public void Rack_with_positive_capacity_and_index_is_valid()
    {
        var rack = EmptyRack(index: 0, capacityScu: 16);

        rack.CapacityScu.Should().Be(16);
        rack.Slots.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-16)]
    public void Rack_rejects_non_positive_capacity(int capacityScu)
    {
        var act = () => EmptyRack(index: 0, capacityScu);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rack_rejects_negative_index()
    {
        var act = () => EmptyRack(index: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // --- Ship ---

    [Fact]
    public void Ship_accepts_racks_that_sum_to_total_scu()
    {
        var ship = new Ship("hull_a", "MISC Hull A", 64, HullARacks());

        ship.Racks.Should().HaveCount(4);
        ship.TotalScu.Should().Be(64);
    }

    [Fact]
    public void Ship_rejects_racks_that_do_not_sum_to_total_scu()
    {
        IReadOnlyList<Rack> racks = [EmptyRack(0), EmptyRack(1), EmptyRack(2), EmptyRack(3, capacityScu: 12)];

        var act = () => new Ship("hull_a", "MISC Hull A", 64, racks);

        act.Should().Throw<ArgumentException>().WithMessage("*60*64*");
    }

    [Fact]
    public void Ship_is_not_affected_by_later_changes_to_the_source_list()
    {
        List<Rack> racks = [.. HullARacks()];
        var ship = new Ship("hull_a", "MISC Hull A", 64, racks);

        racks.Add(EmptyRack(4));

        ship.Racks.Should().HaveCount(4);
    }

    // --- Stop ---

    [Fact]
    public void Stop_total_scu_is_sum_of_items()
    {
        var stop = new Stop("Riker", 1,
        [
            new CargoItem("Titanium", 12, CrateSize.Four),
            new CargoItem("Quartz", 6, CrateSize.Two),
        ]);

        stop.TotalScu.Should().Be(18);
    }

    [Fact]
    public void Stop_without_items_has_zero_total_scu()
    {
        new Stop("Riker", 1, []).TotalScu.Should().Be(0);
    }

    // --- Placement / LoadPlan ---

    [Fact]
    public void Placement_is_not_delivered_by_default_and_with_leaves_original_unchanged()
    {
        var original = new Placement("Riker", "rack-0", new CargoItem("Titanium", 12, CrateSize.Four));

        var delivered = original with { Delivered = true };

        original.Delivered.Should().BeFalse();
        delivered.Delivered.Should().BeTrue();
    }

    [Fact]
    public void LoadPlan_filters_placements_by_rack_and_by_stop()
    {
        var riker = new Placement("Riker", "rack-0", new CargoItem("Titanium", 12, CrateSize.Four));
        var shubin = new Placement("Shubin", "rack-1", new CargoItem("Quartz", 10, CrateSize.Two));
        var rikerOverflow = new Placement("Riker", "rack-1", new CargoItem("Titanium", 4, CrateSize.Four));
        var plan = new LoadPlan("hull_a", [riker, shubin, rikerOverflow], []);

        plan.PlacementsForRack("rack-1").Should().Equal(shubin, rikerOverflow);
        plan.PlacementsForStop("Riker").Should().Equal(riker, rikerOverflow);
    }

    [Fact]
    public void LoadPlan_returns_empty_list_for_unknown_rack_or_stop()
    {
        var plan = new LoadPlan("hull_a",
            [new Placement("Riker", "rack-0", new CargoItem("Titanium", 12, CrateSize.Four))], []);

        plan.PlacementsForRack("rack-9").Should().BeEmpty();
        plan.PlacementsForStop("Orison").Should().BeEmpty();
    }
}
