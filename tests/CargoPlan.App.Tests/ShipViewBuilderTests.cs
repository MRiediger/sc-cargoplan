using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

public class ShipViewBuilderTests
{
    private static readonly (Ship Ship, LoadPlan Plan) Sample = SamplePlan.Load();

    private static ShipScene Build(LoadPlan plan) => ShipViewBuilder.Build(plan, Sample.Ship, LoadPlanner.Validate(plan, Sample.Ship));

    private static SceneRack RackNumber(ShipScene scene, int number) => scene.Racks.Single(r => r.Number == number);

    /// <summary>Distinct mission colours in a rack, in strip order.</summary>
    private static IEnumerable<int?> MissionColours(SceneRack rack) =>
        rack.Segments.Where(s => s.Fill == SegmentFill.Mission).Select(s => s.PaletteIndex).Distinct();

    [Fact]
    public void Racks_come_in_rack_order()
    {
        // The draw order depends on the camera; the projector sorts (DrawOrder).
        Build(Sample.Plan).Racks.Select(r => r.Number).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void A_split_stop_has_the_same_colour_in_both_racks()
    {
        var scene = Build(Sample.Plan);
        var riker = scene.Legend.Single(e => e.Destination == "Riker").PaletteIndex;

        MissionColours(RackNumber(scene, 1)).Should().Equal(riker);
        MissionColours(RackNumber(scene, 2)).Should().StartWith(riker);
    }

    [Fact]
    public void A_shared_rack_shows_both_stops_and_its_free_rest()
    {
        var scene = Build(Sample.Plan);
        var legend = scene.Legend.ToDictionary(e => e.Destination, e => e.PaletteIndex);

        var rack2 = RackNumber(scene, 2);

        MissionColours(rack2).Should().Equal(legend["Riker"], legend["Shubin"]);
        rack2.Segments.Should().Contain(s => s.Fill == SegmentFill.Free);
    }

    [Fact]
    public void Legend_lists_each_stop_once_with_its_total_SCU()
    {
        Build(Sample.Plan).Legend.Select(e => (e.Destination, e.Scu)).Should().Equal(
            ("Riker", 20), ("Shubin", 8), ("Orison", 14), ("Lorville", 12));
    }

    [Fact]
    public void Legend_label_reads_stop_and_SCU()
    {
        new LegendEntry(0, "Riker", 20).Label.Should().Be("Riker · 20 SCU");
    }

    [Fact]
    public void Hints_are_the_planner_messages_word_for_word_once_each()
    {
        Build(Sample.Plan).Hints.Should().Equal(Sample.Plan.Warnings.Select(w => w.Message).Distinct());
    }

    [Fact]
    public void Planner_hints_colour_no_rack_red()
    {
        var scene = Build(Sample.Plan);

        Sample.Plan.Warnings.Should().NotBeEmpty();
        scene.Racks.Should().NotContain(r => r.IsViolated);
        scene.Violations.Should().BeEmpty();
    }

    [Fact]
    public void An_overloaded_rack_is_red_and_its_violation_is_listed()
    {
        // Shubin's 8 SCU onto the full Rack 1: 24/16 SCU.
        var shubin = Sample.Plan.Placements.Single(p => p.Destination == "Shubin");
        var moved = LoadPlanner.MovePlacement(Sample.Plan, Sample.Ship, shubin, "rack-0");

        var scene = Build(moved);

        scene.Racks.Where(r => r.IsViolated).Select(r => r.Number).Should().Equal(1);
        scene.Violations.Should().Equal("Rack 1 overloaded: 24/16 SCU");
    }

    [Fact]
    public void Segments_run_from_the_bow_end_and_cover_the_rack()
    {
        foreach (var rack in Build(Sample.Plan).Racks)
        {
            rack.Segments[0].Bow.Should().Be(rack.Bounds.Max.X);
            rack.Segments[^1].Aft.Should().BeApproximately(rack.Bounds.Min.X, 1e-9);
            rack.Segments.Zip(rack.Segments.Skip(1)).Should().AllSatisfy(pair =>
                pair.Second.Bow.Should().BeApproximately(pair.First.Aft, 1e-9));
        }
    }

    [Fact]
    public void Racks_and_ship_keep_their_boxes()
    {
        var scene = Build(Sample.Plan);

        scene.Bounds.Should().Be(Sample.Ship.Bounds);
        scene.Racks.Select(r => r.Bounds).Should().Equal(Sample.Ship.Racks.Select(r => r.Bounds));
    }

    [Fact]
    public void An_empty_plan_draws_every_rack_free()
    {
        var scene = Build(new LoadPlan(Sample.Ship.Id, [], []));

        scene.Racks.SelectMany(r => r.Segments).Should().OnlyContain(s => s.Fill == SegmentFill.Free);
        scene.Legend.Should().BeEmpty();
    }

    [Fact]
    public void Delivered_placements_are_grey()
    {
        var delivered = Sample.Plan with
        {
            Placements = [.. Sample.Plan.Placements.Select(p => p.Destination == "Lorville" ? p with { Delivered = true } : p)],
        };

        var rack4 = RackNumber(Build(delivered), 4);

        rack4.Segments.Should().Contain(s => s.Fill == SegmentFill.Delivered).And.NotContain(s => s.Fill == SegmentFill.Mission);
    }

    [Fact]
    public void A_delivered_split_stop_is_grey_in_both_racks_and_its_hints_are_gone()
    {
        var scene = Build(LoadPlanner.SetDelivered(Sample.Plan, Sample.Ship, "Riker", true));
        var riker = scene.Legend.Single(e => e.Destination == "Riker");

        // Rack 1 holds only Riker; Rack 2 keeps Shubin's colour next to Riker's grey.
        RackNumber(scene, 1).Segments.Should().NotContain(s => s.Fill == SegmentFill.Mission);
        RackNumber(scene, 2).Segments.Should().Contain(s => s.Fill == SegmentFill.Delivered);
        MissionColours(RackNumber(scene, 2)).Should().Equal(scene.Legend.Single(e => e.Destination == "Shubin").PaletteIndex);
        riker.Delivered.Should().BeTrue();
        scene.Legend.Where(e => e.Destination != "Riker").Should().OnlyContain(e => !e.Delivered);
        scene.Hints.Should().BeEmpty();
    }

    [Fact]
    public void Reserved_hints_are_those_of_the_plan_without_deliveries()
    {
        // The legend reserves their space, so ticking a stop off doesn't resize the view (no zoom jump).
        var expected = Build(Sample.Plan).Hints;

        var scene = Build(LoadPlanner.SetDelivered(Sample.Plan, Sample.Ship, "Riker", true));

        expected.Should().NotBeEmpty();
        scene.ReservedHints.Should().Equal(expected);
        Build(Sample.Plan).ReservedHints.Should().Equal(expected);
    }

    [Fact]
    public void Segments_carry_their_destination_and_free_space_none()
    {
        var scene = Build(LoadPlanner.SetDelivered(Sample.Plan, Sample.Ship, "Riker", true));
        var rack2 = RackNumber(scene, 2);

        rack2.Segments.Select(s => s.Destination).Should().Equal("Riker", "Shubin", null);
    }

    [Fact]
    public void Legend_keeps_route_order_and_colours_when_stops_are_delivered()
    {
        var scene = Build(LoadPlanner.SetDelivered(Sample.Plan, Sample.Ship, "Shubin", true));

        scene.Legend.Select(e => (e.Destination, e.PaletteIndex)).Should().Equal(
            Build(Sample.Plan).Legend.Select(e => (e.Destination, e.PaletteIndex)));
    }

    [Fact]
    public void AllDelivered_only_when_every_stop_is_delivered()
    {
        var plan = Sample.Plan;
        Build(plan).AllDelivered.Should().BeFalse();

        foreach (var destination in new[] { "Riker", "Shubin", "Orison" })
        {
            plan = LoadPlanner.SetDelivered(plan, Sample.Ship, destination, true);
        }

        Build(plan).AllDelivered.Should().BeFalse();
        Build(LoadPlanner.SetDelivered(plan, Sample.Ship, "Lorville", true)).AllDelivered.Should().BeTrue();
    }

    [Fact]
    public void AllDelivered_is_false_without_stops()
    {
        Build(new LoadPlan(Sample.Ship.Id, [], [])).AllDelivered.Should().BeFalse();
    }

    [Fact]
    public void A_plan_for_another_ship_is_rejected()
    {
        var act = () => ShipViewBuilder.Build(Sample.Plan with { ShipId = "cutlass_black" }, Sample.Ship, []);

        act.Should().Throw<ArgumentException>();
    }
}
