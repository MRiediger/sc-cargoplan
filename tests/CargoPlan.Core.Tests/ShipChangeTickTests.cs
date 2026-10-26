using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// What is delivered stays delivered (K2.2): a tick survives a plan planned anew for other racks (ship change, D59; racks
/// that changed, D71), per delivery (mission × destination, the legend's line, D69). Delivered cargo was never on the new
/// ship: its crates lie in no rack (off the ship), take no room and aren't drawn; ticked back they are laid in (D41).
/// </summary>
public class ShipChangeTickTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");
    private static readonly Ship Freelancer = ShipLoader.Load("freelancer");
    private static readonly Ship C2 = ShipLoader.Load("c2_hercules");
    private static readonly Ship Clipper = ShipLoader.Load("clipper");

    private static MissionStop Stop(string destination, string commodity, int scu, CrateSize max, IReadOnlyList<CrateSize>? crates = null) =>
        new(destination, 1, [new MissionItem(commodity, scu, max, crates)]);

    /// <summary>Each stop a mission of its own, as captures come in.</summary>
    private static SavedPlan Placed(Ship ship, params MissionStop[] stops) =>
        stops.Aggregate(new SavedPlan([], new LoadPlan(ship.Id, [], []), CrateCap.Of(ship)), (saved, stop) => LoadPlanner.PlaceInto(saved, ship, [stop]));

    /// <summary>The T7.0 load case: 8 stops, 58 of 64 SCU, one mission per stop.</summary>
    private static SavedPlan EightStops()
    {
        var stops = MissionFixtures.HullAEightStopsMissions;
        return SavedPlan.FromStops(stops, LoadPlanner.Plan(HullA, stops.ToStops()), CrateCap.Of(HullA));
    }

    private static SavedPlan Ticked(SavedPlan saved, Ship ship, string destination) =>
        new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, ship, destination, true), saved.CrateCap, saved.Watermark);

    private static SavedPlan TickedLine(SavedPlan saved, Ship ship, string destination, int mission) =>
        new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, ship, destination, mission, true), saved.CrateCap, saved.Watermark);

    private static IEnumerable<Crate> Of(SavedPlan saved, int mission, string destination) =>
        saved.Plan.Crates.Where(c => c.Mission == mission && c.Destination == destination);

    private static IEnumerable<(string, string, CrateSize, string, GridPosition, bool)> Lying(IEnumerable<Crate> crates) =>
        crates.Select(c => (c.Destination, c.Commodity, c.CrateSize, c.RackId, c.Position, c.Rotated));

    // --- Ticks survive ---

    [Fact]
    public void A_ticked_stop_stays_ticked_on_another_ship_and_back()
    {
        var saved = EightStops();
        var first = saved.Plan.Route[0];

        var there = LoadPlanner.Replan(Ticked(saved, HullA, first), Freelancer);
        var back = LoadPlanner.Replan(there, HullA);

        foreach (var plan in new[] { there, back })
        {
            plan.Plan.Crates.Where(c => c.Destination == first).Should().NotBeEmpty().And.OnlyContain(c => c.Delivered && c.OffShip);
            plan.Plan.Crates.Where(c => c.Destination != first).Should().OnlyContain(c => !c.Delivered && !c.OffShip);
            plan.Missions.Should().OnlyContain(m => !m.Unplaced);
        }
    }

    [Fact]
    public void A_ticked_legend_line_stays_ticked_and_the_other_mission_to_that_stop_stays_on_board()
    {
        var saved = Placed(HullA, Stop("Riker", "Titanium", 8, CrateSize.Four), Stop("Riker", "Gold", 4, CrateSize.Four), Stop("Shubin", "Quartz", 8, CrateSize.Four));

        var there = LoadPlanner.Replan(TickedLine(saved, HullA, "Riker", 1), Freelancer);
        var back = LoadPlanner.Replan(there, HullA);

        foreach (var plan in new[] { there, back })
        {
            Of(plan, 1, "Riker").Should().NotBeEmpty().And.OnlyContain(c => c.Delivered && c.OffShip);
            Of(plan, 2, "Riker").Should().NotBeEmpty().And.OnlyContain(c => !c.Delivered);
            Of(plan, 3, "Shubin").Should().NotBeEmpty().And.OnlyContain(c => !c.Delivered);
        }
    }

    [Fact]
    public void A_stop_ticked_before_new_cargo_for_it_came_in_keeps_only_the_old_cargo_ticked()
    {
        // D41: Riker ticked at the first visit, then a sync brings a new contract for Riker.
        var ticked = Ticked(Placed(HullA, Stop("Riker", "Titanium", 8, CrateSize.Four), Stop("Shubin", "Quartz", 8, CrateSize.Four)), HullA, "Riker");
        var synced = LoadPlanner.PlaceInto(ticked, HullA, [Stop("Riker", "Gold", 4, CrateSize.Four)]);

        var there = LoadPlanner.Replan(synced, Freelancer);

        Of(there, 1, "Riker").Should().NotBeEmpty().And.OnlyContain(c => c.Delivered && c.OffShip);
        Of(there, 3, "Riker").Should().NotBeEmpty().And.OnlyContain(c => !c.Delivered && !c.OffShip);
    }

    // --- Ticked cargo takes no room ---

    [Fact]
    public void The_cargo_on_board_lies_as_a_plan_without_the_ticked_stop_would_lay_it()
    {
        var saved = EightStops();
        var first = saved.Plan.Route[0];

        var there = LoadPlanner.Replan(Ticked(saved, HullA, first), Freelancer);

        var others = saved.Missions.Where(m => m.Stops.All(s => s.Destination != first)).ToList();
        var editor = LoadPlanner.Plan(Freelancer, others, [.. saved.Stops.Select(s => s.Destination).Where(d => d != first)]).Plan;
        Lying(there.Plan.Crates.Where(c => !c.Delivered)).Should().BeEquivalentTo(Lying(editor.Crates));
    }

    [Fact]
    public void Ticked_cargo_is_in_no_rack_and_counts_for_no_room()
    {
        var there = LoadPlanner.Replan(Ticked(EightStops(), HullA, EightStops().Plan.Route[0]), Freelancer);

        var offShip = there.Plan.Crates.Where(c => c.OffShip).ToList();
        offShip.Should().NotBeEmpty().And.OnlyContain(c => c.RackId.Length == 0 && c.Delivered);
        Freelancer.Racks.Should().OnlyContain(r => there.Plan.CratesForRack(r.Id).All(c => !c.OffShip));
        there.Plan.ScuOnBoard().Should().Be(there.Plan.Crates.Where(c => !c.Delivered).Sum(c => (int)c.CrateSize));
        LoadPlanner.Validate(there.Plan, Freelancer).Should().BeEmpty();
    }

    [Fact]
    public void A_load_that_fits_only_without_the_ticked_cargo_is_planned_whole_on_the_smaller_ship()
    {
        // 72 SCU on the C2; Riker's 32 ticked, so the 40 left fit the Hull A's 64 (before K2.2 Lorville waited).
        var onC2 = Placed(
            C2,
            Stop("Riker", "Titanium", 32, CrateSize.Sixteen),
            Stop("Shubin", "Quartz", 16, CrateSize.Sixteen),
            Stop("Orison", "Gold", 16, CrateSize.Sixteen),
            Stop("Lorville", "Agricium", 8, CrateSize.Eight));

        var onHullA = LoadPlanner.Replan(Ticked(onC2, C2, "Riker"), HullA);

        onHullA.Missions.Should().OnlyContain(m => !m.Unplaced);
        Of(onHullA, 1, "Riker").Should().OnlyContain(c => c.Delivered && c.OffShip);
        onHullA.Plan.ScuOnBoard().Should().Be(40);
    }

    // --- Every path that plans anew, and the ones that don't ---

    [Fact]
    public void Racks_that_changed_keep_a_ticked_legend_line()
    {
        var saved = TickedLine(Placed(Freelancer, Stop("Riker", "Titanium", 8, CrateSize.Four), Stop("Riker", "Gold", 4, CrateSize.Four)), Freelancer, "Riker", 1);
        var changed = new SavedPlan(saved.Missions, saved.Plan, saved.CrateCap.Because(CapReason.RacksChanged), saved.Watermark);

        var replanned = LoadPlanner.Replan(changed, Freelancer);

        Of(replanned, 1, "Riker").Should().NotBeEmpty().And.OnlyContain(c => c.Delivered && c.OffShip);
        Of(replanned, 2, "Riker").Should().NotBeEmpty().And.OnlyContain(c => !c.Delivered);
    }

    [Fact]
    public void A_sync_and_a_replan_on_the_new_ship_leave_the_ticked_cargo_off_the_ship()
    {
        var there = LoadPlanner.Replan(Ticked(EightStops(), HullA, EightStops().Plan.Route[0]), Freelancer);
        var offShip = there.Plan.Crates.Where(c => c.OffShip).ToList();

        var synced = LoadPlanner.RetryUnplaced(LoadPlanner.PlaceInto(there, Freelancer, [Stop("Area18", "Gold", 2, CrateSize.Two)]), Freelancer);
        var replanned = LoadPlanner.Replan(synced, Freelancer);

        synced.Plan.Crates.Where(c => c.OffShip).Should().Equal(offShip);
        replanned.Plan.Crates.Where(c => c.OffShip).Should().Equal(offShip);
    }

    // --- Ticked back: laid in like a capture (D41) ---

    [Fact]
    public void Ticking_back_lays_the_cargo_into_free_cells_and_moves_nothing_else()
    {
        // The C2 has room for Lorville's 16 next to the rest; the Freelancer's 24 free SCU hold no 4×2×2 gap.
        var first = EightStops().Plan.Route[0];
        var there = LoadPlanner.Replan(Ticked(EightStops(), HullA, first), C2);

        var back = LoadPlanner.SetDelivered(there.Plan, C2, first, false);

        back.Crates.Where(c => c.Destination == first).Should().NotBeEmpty().And.OnlyContain(c => !c.Delivered && !c.OffShip);
        Lying(back.Crates.Where(c => c.Destination != first)).Should().Equal(Lying(there.Plan.Crates.Where(c => c.Destination != first)));
        back.Route.Should().Equal(there.Plan.Route, "the legend and its colours keep their order");
        LoadPlanner.Validate(back, C2).Should().BeEmpty();
        new SavedPlan(there.Missions, back, there.CrateCap, there.Watermark).Plan.Should().BeSameAs(back);
    }

    [Fact]
    public void Ticking_back_without_room_is_refused()
    {
        // Riker's 32 ticked on the C2; on the Hull A 40 SCU on board and 24 more fill it.
        var onHullA = LoadPlanner.Replan(
            Ticked(Placed(C2, Stop("Riker", "Titanium", 32, CrateSize.Sixteen), Stop("Shubin", "Quartz", 16, CrateSize.Sixteen), Stop("Orison", "Gold", 16, CrateSize.Sixteen), Stop("Lorville", "Agricium", 8, CrateSize.Eight)), C2, "Riker"),
            HullA);
        var full = LoadPlanner.PlaceInto(onHullA, HullA, [Stop("Area18", "Stims", 24, CrateSize.Eight)]);
        full.Plan.ScuOnBoard().Should().Be(64);

        var act = () => LoadPlanner.SetDelivered(full.Plan, HullA, "Riker", false);

        act.Should().Throw<PlanException>().WithMessage("Riker can't go back on board: no room for 16 SCU crate*");
    }

    // --- A crate the new ship can't hold (D70) ---

    [Fact]
    public void Ticked_cargo_the_new_ship_cant_hold_stays_ticked_and_its_mission_doesnt_wait()
    {
        // A set 16 SCU crate: the Clipper's doors take 2 SCU.
        var saved = Ticked(Placed(HullA, Stop("Riker", "Titanium", 16, CrateSize.Sixteen, [CrateSize.Sixteen]), Stop("Shubin", "Quartz", 2, CrateSize.Two)), HullA, "Riker");

        var onClipper = LoadPlanner.Replan(saved, Clipper);

        onClipper.Missions.Should().OnlyContain(m => !m.Unplaced);
        Of(onClipper, 1, "Riker").Should().ContainSingle().Which.Should().Match<Crate>(c => c.Delivered && c.OffShip && c.CrateSize == CrateSize.Sixteen);
    }

    // --- Plan file schema 6 ---

    private static string Written(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static SavedPlan Read(string json, Ship ship) => PlanFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "plan.json", ship);

    private static SavedPlan OnFreelancerWithATickedStop() => LoadPlanner.Replan(Ticked(EightStops(), HullA, EightStops().Plan.Route[0]), Freelancer);

    [Fact]
    public void Write_writes_schema_6_and_offShip_only_where_true_and_Read_brings_it_back()
    {
        var saved = OnFreelancerWithATickedStop();

        var json = Written(saved);
        var node = JsonNode.Parse(json)!;

        node["schemaVersion"]!.GetValue<int>().Should().Be(7);
        var crates = node["crates"]!.AsArray().Select(c => c!.AsObject()).ToList();
        crates.Count(c => c.ContainsKey("offShip")).Should().Be(saved.Plan.Crates.Count(c => c.OffShip));
        crates.Where(c => c.ContainsKey("offShip")).Should().OnlyContain(c => c["offShip"]!.GetValue<bool>() && c["rackId"]!.GetValue<string>() == "");
        Read(json, Freelancer).Plan.Crates.Should().Equal(saved.Plan.Crates);
    }

    [Fact]
    public void An_off_ship_crate_is_checked_against_no_rack_on_reading()
    {
        var saved = Ticked(Placed(HullA, Stop("Riker", "Titanium", 16, CrateSize.Sixteen, [CrateSize.Sixteen]), Stop("Shubin", "Quartz", 2, CrateSize.Two)), HullA, "Riker");

        var read = Read(Written(LoadPlanner.Replan(saved, Clipper)), Clipper);

        read.CrateCap.Should().Be(CrateCap.Of(Clipper), "an off-ship crate is no reason to plan anew");
    }

    public static TheoryData<string, string> BrokenOffShip => new()
    {
        { "schema_5", "*offShip*new in schema 6*the file says 5*" },
        { "not_delivered", "*off the ship*delivered*" },
        { "with_a_rack", "*off the ship*rack*" },
    };

    [Theory]
    [MemberData(nameof(BrokenOffShip))]
    public void A_broken_off_ship_crate_is_refused_with_the_reason(string change, string expectedMessage)
    {
        var node = JsonNode.Parse(Written(OnFreelancerWithATickedStop()))!;
        var offShip = node["crates"]!.AsArray().Select(c => c!.AsObject()).First(c => c.ContainsKey("offShip"));
        switch (change)
        {
            case "schema_5":
                node["schemaVersion"] = 5;
                break;
            case "not_delivered":
                offShip["delivered"] = false;
                break;
            default:
                offShip["rackId"] = Freelancer.Racks[0].Id;
                break;
        }

        var act = () => Read(node.ToJsonString(), Freelancer);

        act.Should().Throw<PlanFormatException>().WithMessage(expectedMessage);
    }
}
