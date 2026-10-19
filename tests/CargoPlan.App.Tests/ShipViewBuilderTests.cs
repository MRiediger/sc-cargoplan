using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

public class ShipViewBuilderTests
{
    private static readonly (Ship Ship, LoadPlan Plan) Sample = TestPlans.Load();

    private static ShipScene Build(LoadPlan plan) =>
        ShipViewBuilder.Build(plan, Sample.Ship, LoadPlanner.Validate(plan, Sample.Ship), FillOrder.BowFirst);

    private static SceneRack RackNumber(ShipScene scene, int number) => scene.Racks.Single(r => r.Number == number);

    /// <summary>Distinct mission colours in a rack, in fill order.</summary>
    private static IEnumerable<int?> MissionColours(SceneRack rack) =>
        rack.Boxes.Where(s => s.Fill == CellFill.Mission).Select(s => s.PaletteIndex).Distinct();

    private static int Volume(ShipBox box) => (box.Max.X - box.Min.X) * (box.Max.Y - box.Min.Y) * (box.Max.Z - box.Min.Z);

    private static IEnumerable<ShipPoint> CellsOf(ShipBox box) =>
        from x in Enumerable.Range(box.Min.X, box.Max.X - box.Min.X)
        from y in Enumerable.Range(box.Min.Y, box.Max.Y - box.Min.Y)
        from z in Enumerable.Range(box.Min.Z, box.Max.Z - box.Min.Z)
        select new ShipPoint(x, y, z);

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
        MissionColours(RackNumber(scene, 2)).Should().Contain(riker);
    }

    [Fact]
    public void A_shared_rack_shows_both_stops_and_its_free_rest()
    {
        var scene = Build(Sample.Plan);
        var legend = scene.Legend.ToDictionary(e => e.Destination, e => e.PaletteIndex);

        var rack2 = RackNumber(scene, 2);

        // The largest crate goes first: Shubin's 8 SCU crate before Riker's 2 SCU crates.
        MissionColours(rack2).Should().Equal(legend["Shubin"], legend["Riker"]);
        rack2.Boxes.Should().Contain(c => c.Fill == CellFill.Free);
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
        RackNumber(scene, 1).Boxes.Should().NotContain(c => c.Fill == CellFill.Free);
        RackNumber(scene, 1).Boxes.Select(c => c.Placement!.Destination).Distinct().Should().Equal("Riker", "Shubin");
    }

    [Fact]
    public void Boxes_fill_the_rack_exactly_without_overlap()
    {
        foreach (var rack in Build(Sample.Plan).Racks)
        {
            var (min, max) = (rack.Bounds.Min, rack.Bounds.Max);
            ShipPoint[] cells = [.. rack.Boxes.SelectMany(b => CellsOf(b.Bounds))];

            cells.Should().HaveCount(Volume(rack.Bounds)).And.OnlyHaveUniqueItems();
            cells.Should().OnlyContain(c =>
                c.X >= min.X && c.X < max.X && c.Y >= min.Y && c.Y < max.Y && c.Z >= min.Z && c.Z < max.Z);
        }
    }

    [Fact]
    public void Crates_of_4_4_and_8_are_three_boxes_in_their_shape()
    {
        Placement Crates(string destination, int scu, CrateSize size) => new(destination, "rack-0", new CargoItem("Titanium", scu, size));
        LoadPlan plan = new(Sample.Ship.Id, [Crates("Riker", 4, CrateSize.Four), Crates("Shubin", 4, CrateSize.Four), Crates("Orison", 8, CrateSize.Eight)], []);

        var rack1 = RackNumber(Build(plan), 1);

        rack1.Boxes.Should().HaveCount(3);
        rack1.Boxes.Select(b => Volume(b.Bounds)).Should().Equal(8, 4, 4);
        rack1.Boxes.Should().OnlyContain(b => b.Crate != null && b.Fill == CellFill.Mission);
    }

    [Fact]
    public void Boxes_come_in_fill_order_of_their_first_cell()
    {
        // Rack 2 (y 1..3, z 0..2): Shubin's 8 SCU crate takes the bow half, Riker's 2 SCU Quartz crates the aft half of
        // the bottom layer, the row next to the spindle first; the free cells of the top layer follow.
        var rack2 = RackNumber(Build(Sample.Plan), 2);

        rack2.Boxes.Take(3).Select(b => b.Bounds).Should().Equal(
            new ShipBox(new ShipPoint(2, 1, 0), new ShipPoint(4, 3, 2)),
            new ShipBox(new ShipPoint(0, 1, 0), new ShipPoint(2, 2, 1)),
            new ShipBox(new ShipPoint(0, 2, 0), new ShipPoint(2, 3, 1)));
        rack2.Boxes.Skip(3).Should().HaveCount(4).And.OnlyContain(b => b.Fill == CellFill.Free && Volume(b.Bounds) == 1);
    }

    [Fact]
    public void A_crate_is_one_box_with_its_id()
    {
        var shubin = Sample.Plan.Placements.Single(p => p.Destination == "Shubin");

        RackNumber(Build(Sample.Plan), 2).Boxes.Where(b => b.Placement == shubin).Should().ContainSingle().Which
            .Should().Be(new SceneBox(
                new ShipBox(new ShipPoint(2, 1, 0), new ShipPoint(4, 3, 2)),
                CellFill.Mission,
                Build(Sample.Plan).Legend.Single(e => e.Destination == "Shubin").PaletteIndex,
                "Shubin · Rack 2 · port lower · 8 SCU Agricium crate (1 of 1)",
                shubin,
                new CrateId(shubin, 1)));
    }

    [Fact]
    public void Free_cells_and_the_cells_of_an_overloaded_rack_are_boxes_of_one_cell()
    {
        var shubin = Sample.Plan.Placements.Single(p => p.Destination == "Shubin");
        var scene = Build(LoadPlanner.MovePlacement(Sample.Plan, Sample.Ship, shubin, "rack-0"));

        RackNumber(scene, 1).Boxes.Should().HaveCount(16).And.OnlyContain(b => Volume(b.Bounds) == 1 && b.Crate == null);
        RackNumber(scene, 2).Boxes.Where(b => b.Fill == CellFill.Free).Should().OnlyContain(b => Volume(b.Bounds) == 1 && b.Crate == null);
    }

    [Fact]
    public void Stern_first_starts_at_the_aft_end()
    {
        var rack2 = ShipViewBuilder.Build(Sample.Plan, Sample.Ship, [], FillOrder.SternFirst).Racks.Single(r => r.Number == 2);

        rack2.Boxes[0].Bounds.Min.Should().Be(new ShipPoint(0, 1, 0));
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

        scene.Racks.SelectMany(r => r.Boxes).Should().OnlyContain(c => c.Fill == CellFill.Free);
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

        rack4.Boxes.Should().Contain(c => c.Fill == CellFill.Delivered).And.NotContain(c => c.Fill == CellFill.Mission);
    }

    [Fact]
    public void A_delivered_split_stop_is_grey_in_both_racks_and_its_hints_are_gone()
    {
        var scene = Build(LoadPlanner.SetDelivered(Sample.Plan, Sample.Ship, "Riker", true));
        var riker = scene.Legend.Single(e => e.Destination == "Riker");

        // Rack 1 holds only Riker; Rack 2 keeps Shubin's colour next to Riker's grey.
        RackNumber(scene, 1).Boxes.Should().NotContain(c => c.Fill == CellFill.Mission);
        RackNumber(scene, 2).Boxes.Should().Contain(c => c.Fill == CellFill.Delivered);
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
    public void Cells_carry_their_placement_and_free_cells_none()
    {
        var plan = LoadPlanner.SetDelivered(Sample.Plan, Sample.Ship, "Riker", true);
        var rack2 = RackNumber(Build(plan), 2);

        rack2.Boxes.Select(c => c.Placement).Distinct().Should().BeEquivalentTo([.. plan.PlacementsForRack("rack-1"), null]);
        rack2.Boxes.Select(c => c.Placement?.Destination).Distinct().Should().BeEquivalentTo(["Riker", "Shubin", null]);
    }

    [Fact]
    public void Racks_carry_their_rack_id_for_a_drop()
    {
        Build(Sample.Plan).Racks.Select(r => r.RackId).Should().Equal(Sample.Ship.Racks.Select(r => r.Id));
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
        var act = () => ShipViewBuilder.Build(Sample.Plan with { ShipId = "cutlass_black" }, Sample.Ship, [], FillOrder.BowFirst);

        act.Should().Throw<ArgumentException>();
    }
}
