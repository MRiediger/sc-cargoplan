using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public class RackSegmentsTests
{
    private static readonly Rack Rack1 = ShipLoader.Load("hull_a").Racks[0];

    private static Placement Cargo(string destination, int scu, bool delivered = false) =>
        new(destination, Rack1.Id, new CargoItem("Titanium", scu, CrateSize.Four), delivered);

    [Fact]
    public void Shares_follow_the_SCU_and_the_plan_order()
    {
        var segments = RackSegments.Build(Rack1, [Cargo("Riker", 12), Cargo("Shubin", 4)]);

        segments.Select(s => s.Placement?.Destination).Should().Equal("Riker", "Shubin");
        segments.Select(s => s.Length).Should().Equal(0.75, 0.25);
    }

    [Fact]
    public void Segments_follow_each_other_from_the_bow_end()
    {
        var segments = RackSegments.Build(Rack1, [Cargo("Riker", 4), Cargo("Shubin", 8)]);

        segments.Select(s => s.Start).Should().Equal(0, 0.25, 0.75);
    }

    [Fact]
    public void The_rest_of_a_partly_filled_rack_is_one_free_segment()
    {
        var segments = RackSegments.Build(Rack1, [Cargo("Riker", 4), Cargo("Shubin", 8)]);

        segments.Should().HaveCount(3);
        segments[^1].IsFree.Should().BeTrue();
        segments[^1].Length.Should().Be(0.25);
        segments.Sum(s => s.Length).Should().BeApproximately(1, 1e-9);
    }

    [Fact]
    public void An_empty_rack_is_one_free_segment_over_the_whole_length()
    {
        var segments = RackSegments.Build(Rack1, []);

        segments.Should().ContainSingle().Which.Should().Be(new RackSegment(0, 1, null));
    }

    [Fact]
    public void An_overloaded_rack_has_no_free_segment_and_still_sums_to_one()
    {
        // 24 SCU in a 16 SCU rack, only possible after a manual move (D10).
        var segments = RackSegments.Build(Rack1, [Cargo("Riker", 16), Cargo("Shubin", 8)]);

        segments.Should().NotContain(s => s.IsFree);
        segments.Sum(s => s.Length).Should().BeApproximately(1, 1e-9);
        segments[0].Length.Should().BeApproximately(2.0 / 3, 1e-9);
    }

    [Fact]
    public void A_delivered_placement_keeps_its_segment()
    {
        var segments = RackSegments.Build(Rack1, [Cargo("Riker", 8, delivered: true), Cargo("Shubin", 8)]);

        segments[0].Placement!.Delivered.Should().BeTrue();
        segments[0].Length.Should().Be(0.5);
    }
}
