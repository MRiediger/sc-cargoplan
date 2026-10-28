using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A delivery (mission × destination, K2.2) is planned, loaded or delivered (K6.4, D79). Loaded crates are on board and
/// fixed: the planner never moves them (Plan, Re-plan, sync, displacement, another destination's legend drag), only the
/// player does; where support goes, everything on board settles straight down its column. Plan schema 7 stores
/// <c>crates[].loaded</c>.
/// </summary>
public class LoadedStateTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");
    private static readonly Ship Freelancer = ShipLoader.Load("freelancer");

    // --- Helpers ---

    private static MissionStop Stop(string destination, string commodity, int scu, CrateSize max, IReadOnlyList<CrateSize>? crates = null) =>
        new(destination, 1, [new MissionItem(commodity, scu, max, crates)]);

    /// <summary>Each mission's stops placed as a capture comes in (D41), in this order.</summary>
    private static SavedPlan Placed(Ship ship, params MissionStop[][] missions) =>
        missions.Aggregate(new SavedPlan([], new LoadPlan(ship.Id, [], []), CrateCap.Of(ship)),
            (saved, stops) => LoadPlanner.PlaceInto(saved, ship, [.. stops.Select((s, i) => s with { Order = i + 1 })]));

    private static SavedPlan With(SavedPlan saved, LoadPlan plan) => new(saved.Missions, plan, saved.CrateCap, saved.Watermark);

    private static SavedPlan Loaded(SavedPlan saved, Ship ship, string destination, int mission) =>
        With(saved, LoadPlanner.SetLoaded(saved.Plan, ship, destination, mission, true));

    private static IEnumerable<Crate> Of(SavedPlan saved, int mission, string destination) =>
        saved.Plan.Crates.Where(c => c.Mission == mission && c.Destination == destination);

    /// <summary>Where and how the crates lie, in a fixed order; mission and item ids left out (the editor numbers anew).</summary>
    private static string[] Lying(IEnumerable<Crate> crates) =>
        [.. crates.Select(c => $"{c.Destination} {c.Commodity} {(int)c.CrateSize} {c.RackId} {c.Position} rotated={c.Rotated} loaded={c.Loaded}").Order()];

    private static string[] RacksOf(IEnumerable<Crate> crates) => [.. crates.Select(c => c.RackId).Distinct().Order()];

    private static string Written(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static SavedPlan Read(string json, Ship ship) => PlanFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "plan.json", ship);

    /// <summary>One rack of these cells, stacking up.</summary>
    private static Ship OneRack(int length, int width, int stack) =>
        new("one_rack", "One rack test ship", length * width * stack,
        [
            new Rack("rack-0", 0, length * width * stack, new Dimensions(length, width, stack), null, new ShipPoint(0, 0, 0), StackAxis.PlusZ, null, null),
        ]);

    /// <summary>A mission of one 1 SCU crate of Stims, from ContractTracker capture <paramref name="id"/>.</summary>
    private static Mission One(int id, string destination, CrateSize size = CrateSize.One) =>
        new(id, [new MissionStop(destination, 1, [new MissionItem("Stims", (int)size, size)])], new ContractSource([id]));

    private static Crate CrateOf(int mission, string destination, int length, int width, int stack, CrateSize size = CrateSize.One, bool loaded = false) =>
        new(destination, "Stims", size, "rack-0", new GridPosition(length, width, stack), Mission: mission, Loaded: loaded);

    /// <summary>A on the plate, B on A, C on B, in a column one cell wide; B and C as given.</summary>
    private static SavedPlan Column(bool bLoaded, bool cLoaded)
    {
        var ship = OneRack(1, 1, 3);
        return new SavedPlan(
            [One(1, "A"), One(2, "B"), One(3, "C")],
            new LoadPlan(ship.Id, [CrateOf(1, "A", 0, 0, 0, loaded: true), CrateOf(2, "B", 0, 0, 1, loaded: bLoaded), CrateOf(3, "C", 0, 0, 2, loaded: cLoaded)], []),
            CrateCap.Of(ship));
    }

    private static int StackOf(SavedPlan saved, string destination) => saved.Plan.Crates.Single(c => c.Destination == destination).Position.Stack;

    // --- The model ---

    [Fact]
    public void A_crate_is_never_loaded_and_delivered()
    {
        var act = () => new Crate("Riker", "Stims", CrateSize.Four, "rack-0", new GridPosition(0, 0, 0), Delivered: true, Loaded: true);

        act.Should().Throw<ArgumentException>().WithMessage("*loaded*delivered*");
    }

    // --- Plan file schema 7 ---

    private static SavedPlan RikerLoaded() =>
        Loaded(Placed(HullA, [Stop("Riker", "Titanium", 8, CrateSize.Four)], [Stop("Shubin", "Gold", 4, CrateSize.Four)]), HullA, "Riker", 1);

    [Fact]
    public void Write_writes_loaded_only_where_true()
    {
        var node = JsonNode.Parse(Written(RikerLoaded()))!;

        node["schemaVersion"]!.GetValue<int>().Should().Be(7);
        var crates = node["crates"]!.AsArray().Select(c => c!.AsObject()).ToList();
        crates.Where(c => c.ContainsKey("loaded")).Select(c => (string)c["destination"]!).Should().Equal("Riker", "Riker");
        crates.Where(c => c.ContainsKey("loaded")).Should().OnlyContain(c => c["loaded"]!.GetValue<bool>());
    }

    [Fact]
    public void Write_then_Read_keeps_loaded()
    {
        var saved = RikerLoaded();

        Read(Written(saved), HullA).Plan.Crates.Should().Equal(saved.Plan.Crates);
    }

    [Fact]
    public void Schema_6_reads_with_nothing_loaded()
    {
        var node = JsonNode.Parse(Written(Placed(HullA, [Stop("Riker", "Titanium", 8, CrateSize.Four)])))!;
        node["schemaVersion"] = 6;

        Read(node.ToJsonString(), HullA).Plan.Crates.Should().NotBeEmpty().And.OnlyContain(c => !c.Loaded);
    }

    [Fact]
    public void Schema_6_with_loaded_is_broken()
    {
        var node = JsonNode.Parse(Written(RikerLoaded()))!;
        node["schemaVersion"] = 6;

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*loaded*schema 7*");
    }

    [Fact]
    public void Loaded_and_delivered_in_the_file_is_broken()
    {
        var node = JsonNode.Parse(Written(RikerLoaded()))!;
        node["crates"]![0]!["delivered"] = true;

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*loaded*delivered*");
    }

    [Fact]
    public void Loaded_off_the_ship_in_the_file_is_broken()
    {
        var node = JsonNode.Parse(Written(RikerLoaded()))!;
        var crate = node["crates"]![0]!;
        crate["rackId"] = "";
        crate["offShip"] = true;
        crate["position"] = new JsonArray(0, 0, 0);

        var act = () => Read(node.ToJsonString(), HullA);

        act.Should().Throw<PlanFormatException>().WithMessage("*loaded*off the ship*");
    }

    // --- Setting the state ---

    [Fact]
    public void Loading_a_delivery_marks_its_crates_and_moves_nothing()
    {
        var saved = Placed(HullA, [Stop("Riker", "Titanium", 8, CrateSize.Four)], [Stop("Shubin", "Gold", 4, CrateSize.Four)]);

        var loaded = Loaded(saved, HullA, "Riker", 1);

        Of(loaded, 1, "Riker").Should().OnlyContain(c => c.Loaded);
        Of(loaded, 2, "Shubin").Should().OnlyContain(c => !c.Loaded);
        loaded.Plan.Crates.Select(c => (c.RackId, c.Position)).Should().Equal(saved.Plan.Crates.Select(c => (c.RackId, c.Position)));
    }

    [Fact]
    public void A_delivery_without_crates_on_board_cant_be_loaded()
    {
        // E3: delivered (or waiting, which has no crates) is not on board; loaded would be a state without cargo.
        var saved = Placed(HullA, [Stop("Riker", "Titanium", 8, CrateSize.Four)]);
        var delivered = LoadPlanner.SetDelivered(saved.Plan, HullA, "Riker", 1, true);

        var act = () => LoadPlanner.SetLoaded(delivered, HullA, "Riker", 1, true);
        var none = () => LoadPlanner.SetLoaded(saved.Plan, HullA, "Shubin", 1, true);

        act.Should().Throw<PlanException>().WithMessage("*Riker*not on board*");
        none.Should().Throw<PlanException>().WithMessage("*Shubin*not placed yet*");
    }

    [Fact]
    public void Delivering_a_loaded_delivery_clears_loaded()
    {
        var delivered = LoadPlanner.SetDelivered(RikerLoaded().Plan, HullA, "Riker", 1, true);

        delivered.Crates.Where(c => c.Destination == "Riker").Should().OnlyContain(c => c.Delivered && !c.Loaded);
    }

    // --- The planner never moves loaded crates ---

    /// <summary>
    /// Riker (16) dragged by the player into rack 4 and loaded there; Shubin (16) planned. A fresh plan would put Riker
    /// into rack 1.
    /// </summary>
    private static SavedPlan RikerLoadedInRack4()
    {
        var saved = Placed(HullA, [Stop("Riker", "Titanium", 16, CrateSize.Sixteen)], [Stop("Shubin", "Gold", 16, CrateSize.Sixteen)]);
        var moved = With(saved, LoadPlanner.ResolveMoveToRack(saved.Plan, HullA, [.. Of(saved, 1, "Riker")], "rack-3").Plan!);
        return Loaded(moved, HullA, "Riker", 1);
    }

    [Fact]
    public void Plan_keeps_loaded_crates_and_lays_the_rest_around_them()
    {
        var current = RikerLoadedInRack4();
        Mission[] missions = [.. current.Missions, new Mission(3, [Stop("Orison", "Quartz", 8, CrateSize.Eight)])];
        string[] route = ["Riker", "Shubin", "Orison"];

        var (plan, cleared) = LoadPlanner.PlanKeeping(current, HullA, missions, route, FillOrder.BowFirst, 0);

        cleared.Should().BeEmpty();
        Lying(Of(plan, 1, "Riker")).Should().Equal(Lying(Of(current, 1, "Riker")));
        RacksOf(Of(plan, 1, "Riker")).Should().Equal("rack-3");
        LoadPlanner.Validate(plan.Plan, HullA).Should().BeEmpty();
        plan.Missions.Should().OnlyContain(m => !m.Unplaced);

        // The test means something: planned from scratch, Riker would lie elsewhere.
        RacksOf(LoadPlanner.Plan(HullA, missions, route).Plan.Crates.Where(c => c.Destination == "Riker")).Should().NotEqual(["rack-3"]);
    }

    [Fact]
    public void Replan_keeps_loaded_crates()
    {
        var current = RikerLoadedInRack4();

        var replanned = LoadPlanner.Replan(current, HullA);

        Lying(Of(replanned, 1, "Riker")).Should().Equal(Lying(Of(current, 1, "Riker")));
        LoadPlanner.Validate(replanned.Plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void A_sync_places_new_missions_around_loaded_crates()
    {
        var current = RikerLoadedInRack4();
        ContractCapture capture = new([Stop("Orison", "Quartz", 16, CrateSize.Sixteen)], new ContractSource([47]));

        var synced = ContractSync.Apply(current, HullA, [capture], present: null);

        Lying(Of(synced, 1, "Riker")).Should().Equal(Lying(Of(current, 1, "Riker")));
        synced.Plan.Crates.Should().Contain(c => c.Destination == "Orison");
    }

    /// <summary>Shubin's 16 fills rack 2; Orison's 8 lies in rack 3.</summary>
    private static SavedPlan FullRack2(bool shubinLoaded)
    {
        var saved = Placed(HullA, [Stop("Riker", "Titanium", 16, CrateSize.Sixteen)], [Stop("Shubin", "Gold", 16, CrateSize.Sixteen)], [Stop("Orison", "Quartz", 8, CrateSize.Eight)]);
        return shubinLoaded ? Loaded(saved, HullA, "Shubin", 2) : saved;
    }

    [Fact]
    public void Displacement_never_takes_loaded_crates()
    {
        var planned = FullRack2(shubinLoaded: false);
        var loaded = FullRack2(shubinLoaded: true);
        var shubinRack = Of(planned, 2, "Shubin").Single().RackId;

        var withPlanned = LoadPlanner.ResolveMoveToRack(planned.Plan, HullA, [.. Of(planned, 3, "Orison")], shubinRack);
        var withLoaded = LoadPlanner.ResolveMoveToRack(loaded.Plan, HullA, [.. Of(loaded, 3, "Orison")], shubinRack);

        // Planned, Shubin makes room (stage 3b, D65); loaded, Orison goes elsewhere and Shubin stays.
        withPlanned.Stage.Should().Be(MoveStage.Displace);
        withLoaded.Plan!.Crates.Where(c => c.Destination == "Shubin").Should().Equal(Of(loaded, 2, "Shubin"));
        withLoaded.Plan.Crates.Where(c => c.Destination == "Orison").Should().OnlyContain(c => c.RackId != shubinRack);
    }

    /// <summary>
    /// Rack 1 holds Riker's 4 in the middle of the plate (cells 1–2 along the length); Shubin's 8 lies in rack 2. An 8
    /// fits rack 1 only if the 4 moves to one end.
    /// </summary>
    private static SavedPlan RikerInTheMiddle(bool rikerLoaded)
    {
        Mission[] missions =
        [
            new(1, [new MissionStop("Riker", 1, [new MissionItem("Titanium", 4, CrateSize.Four)])]),
            new(2, [new MissionStop("Shubin", 1, [new MissionItem("Gold", 8, CrateSize.Eight)])]),
        ];
        Crate[] crates =
        [
            new("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(1, 0, 0), Mission: 1, Loaded: rikerLoaded),
            new("Shubin", "Gold", CrateSize.Eight, "rack-1", new GridPosition(0, 0, 0), Mission: 2),
        ];
        return new SavedPlan(missions, new LoadPlan(HullA.Id, crates, []), CrateCap.Of(HullA));
    }

    [Fact]
    public void Another_destinations_legend_drag_goes_around_loaded_crates()
    {
        var planned = RikerInTheMiddle(rikerLoaded: false);
        var loaded = RikerInTheMiddle(rikerLoaded: true);

        var aroundPlanned = LoadPlanner.ResolveMoveToRack(planned.Plan, HullA, [.. Of(planned, 2, "Shubin")], "rack-0").Plan!;
        var aroundLoaded = LoadPlanner.ResolveMoveToRack(loaded.Plan, HullA, [.. Of(loaded, 2, "Shubin")], "rack-0").Plan!;

        // Planned, the rack is laid out anew and the 4 moves (stage 3); loaded, it stays and the 8 goes elsewhere.
        aroundPlanned.Crates.Single(c => c.Destination == "Riker").Position.Should().NotBe(new GridPosition(1, 0, 0));
        aroundLoaded.Crates.Single(c => c.Destination == "Riker").Should().Be(Of(loaded, 1, "Riker").Single());
        aroundLoaded.Crates.Single(c => c.Destination == "Shubin").RackId.Should().NotBe("rack-0");
    }

    [Fact]
    public void The_player_may_drag_a_loaded_crate_and_it_stays_loaded()
    {
        var loaded = RikerInTheMiddle(rikerLoaded: true);

        var moved = LoadPlanner.ResolveMoveToRack(loaded.Plan, HullA, [.. Of(loaded, 1, "Riker")], "rack-2").Plan!;

        moved.Crates.Single(c => c.Destination == "Riker").Should().Match<Crate>(c => c.RackId == "rack-2" && c.Loaded);
    }

    [Fact]
    public void Split_and_merge_keep_loaded()
    {
        var loaded = RikerInTheMiddle(rikerLoaded: true);

        var split = LoadPlanner.SplitCrate(loaded, HullA, Of(loaded, 1, "Riker").Single(), [CrateSize.Two, CrateSize.Two]);
        var merged = LoadPlanner.MergeCrates(split, HullA, [.. Of(split, 1, "Riker")]);

        Of(split, 1, "Riker").Should().HaveCount(2).And.OnlyContain(c => c.Loaded);
        Of(merged, 1, "Riker").Should().ContainSingle().Which.Loaded.Should().BeTrue();
    }

    // --- Settling: support goes, everything on board above drops straight down its column ---

    [Fact]
    public void A_stack_of_three_settles_from_the_bottom_when_the_lowest_is_delivered()
    {
        // E4: worked off from the bottom up, or the middle crate would hold the top one where it was.
        var column = Column(bLoaded: true, cLoaded: true);
        var ship = OneRack(1, 1, 3);

        var delivered = With(column, LoadPlanner.SetDelivered(column.Plan, ship, "A", 1, true));

        (StackOf(delivered, "B"), StackOf(delivered, "C")).Should().Be((0, 1));
        delivered.Plan.Crates.Where(c => c.Destination != "A").Should().OnlyContain(c => c.Loaded);
        LoadPlanner.Validate(delivered.Plan, ship).Should().BeEmpty();
    }

    [Fact]
    public void A_sync_takes_a_gone_mission_whose_crates_are_loaded_and_the_crates_above_settle()
    {
        var column = Column(bLoaded: true, cLoaded: true);
        var ship = OneRack(1, 1, 3);

        var synced = ContractSync.Apply(column, ship, [], present: new HashSet<long> { 2, 3 });

        synced.Missions.Select(m => m.Id).Should().Equal(2, 3);
        (StackOf(synced, "B"), StackOf(synced, "C")).Should().Be((0, 1));
        synced.Plan.Crates.Should().OnlyContain(c => c.Loaded);
    }

    [Fact]
    public void A_gone_mission_with_planned_crates_on_board_stays()
    {
        // Only loaded cargo was handed over in the game; planned cargo on board keeps the conflict notice (D39 addendum).
        var ship = OneRack(1, 1, 3);
        var column = Column(bLoaded: true, cLoaded: true);
        var planned = new SavedPlan(column.Missions, column.Plan with { Crates = [.. column.Plan.Crates.Select(c => c.Destination == "A" ? c with { Loaded = false } : c)] }, column.CrateCap);

        var synced = ContractSync.Apply(planned, ship, [], present: new HashSet<long> { 2, 3 });

        synced.Missions.Select(m => m.Id).Should().Equal(1, 2, 3);
        ContractSync.Conflicts(synced, new HashSet<long> { 2, 3 }).Should().Equal(1);
    }

    [Fact]
    public void Planned_crates_above_settle_too()
    {
        var column = Column(bLoaded: false, cLoaded: true);
        var ship = OneRack(1, 1, 3);

        var delivered = With(column, LoadPlanner.SetDelivered(column.Plan, ship, "A", 1, true));

        (StackOf(delivered, "B"), StackOf(delivered, "C")).Should().Be((0, 1));
        LoadPlanner.SettledCrates(column.Plan, delivered.Plan).Should().Be((1, 1));
    }

    [Fact]
    public void Deleting_a_mission_settles_what_lay_on_it()
    {
        var column = Column(bLoaded: true, cLoaded: false);

        var removed = LoadPlanner.RemoveMission(column, OneRack(1, 1, 3), 1);

        (StackOf(removed, "B"), StackOf(removed, "C")).Should().Be((0, 1));
    }

    [Fact]
    public void A_crate_that_lands_half_supported_stays_there_and_is_marked()
    {
        // A and B 1s side by side, C a 2 across both; A goes: C still touches B and falls no further.
        var ship = OneRack(2, 1, 2);
        Mission[] missions = [One(1, "A"), One(2, "B"), One(3, "C", CrateSize.Two)];
        Crate[] crates = [CrateOf(1, "A", 0, 0, 0, loaded: true), CrateOf(2, "B", 1, 0, 0, loaded: true), CrateOf(3, "C", 0, 0, 1, CrateSize.Two, loaded: true)];
        var saved = new SavedPlan(missions, new LoadPlan(ship.Id, crates, []), CrateCap.Of(ship));

        var delivered = LoadPlanner.SetDelivered(saved.Plan, ship, "A", 1, true);

        delivered.Crates.Single(c => c.Destination == "C").Position.Should().Be(new GridPosition(0, 0, 1));
        LoadPlanner.Validate(delivered, ship).Should().ContainSingle().Which.Message.Should().Contain("nothing below it");
    }

    // --- The editor's Plan keeps what is loaded and delivered (D19 addendum) ---

    [Fact]
    public void Without_loaded_or_delivered_crates_the_editors_plan_is_the_plan_from_scratch()
    {
        var current = Placed(HullA, [Stop("Riker", "Titanium", 16, CrateSize.Sixteen)], [Stop("Shubin", "Gold", 8, CrateSize.Four)]);
        string[] route = ["Riker", "Shubin"];

        var (kept, cleared) = LoadPlanner.PlanKeeping(current, HullA, current.Missions, route, FillOrder.BowFirst, 0);

        cleared.Should().BeEmpty();
        kept.Plan.Crates.Should().Equal(LoadPlanner.Plan(HullA, current.Missions, route).Plan.Crates);
    }

    [Fact]
    public void A_delivered_delivery_stays_delivered_through_the_editors_plan()
    {
        var current = Placed(HullA, [Stop("Riker", "Titanium", 16, CrateSize.Sixteen)], [Stop("Shubin", "Gold", 8, CrateSize.Four)]);
        var ticked = With(current, LoadPlanner.SetDelivered(current.Plan, HullA, "Riker", 1, true));

        var (kept, _) = LoadPlanner.PlanKeeping(ticked, HullA, ticked.Missions, ["Riker", "Shubin"], FillOrder.BowFirst, 0);

        Of(kept, 1, "Riker").Should().NotBeEmpty().And.OnlyContain(c => c.Delivered);
        Of(kept, 2, "Shubin").Should().OnlyContain(c => !c.Delivered);
    }

    [Fact]
    public void A_typed_delivery_is_known_by_its_destination_whatever_its_new_mission_id()
    {
        // The editor numbers typed missions anew on every Plan (MissionRows.ToMissions).
        var current = RikerLoadedInRack4();
        Mission[] renumbered = [.. current.Missions.Select(m => new Mission(m.Id + 10, m.Stops))];

        var (kept, _) = LoadPlanner.PlanKeeping(current, HullA, renumbered, ["Riker", "Shubin"], FillOrder.BowFirst, 0);

        Lying(Of(kept, 11, "Riker")).Should().Equal(Lying(Of(current, 1, "Riker")));
    }

    [Fact]
    public void A_delivery_changed_in_the_editor_is_no_longer_loaded()
    {
        var current = RikerLoadedInRack4();
        Mission[] changed = [new Mission(1, [Stop("Riker", "Titanium", 8, CrateSize.Eight)]), current.Missions[1]];

        var (kept, cleared) = LoadPlanner.PlanKeeping(current, HullA, changed, ["Riker", "Shubin"], FillOrder.BowFirst, 0);

        cleared.Should().Equal("Riker: changed in the editor – no longer loaded");
        kept.Plan.Crates.Should().OnlyContain(c => !c.Loaded);
    }

    [Fact]
    public void A_split_loaded_crate_stays_loaded_through_the_editors_plan()
    {
        // E4: the kiosk gave 2+2 for a 4; the split goes into the mission (T3.15), so the delivery is unchanged.
        var loaded = RikerInTheMiddle(rikerLoaded: true);
        var split = LoadPlanner.SplitCrate(loaded, HullA, Of(loaded, 1, "Riker").Single(), [CrateSize.Two, CrateSize.Two]);

        var (kept, cleared) = LoadPlanner.PlanKeeping(split, HullA, split.Missions, ["Riker", "Shubin"], FillOrder.BowFirst, 0);

        cleared.Should().BeEmpty();
        Lying(Of(kept, 1, "Riker")).Should().Equal(Lying(Of(split, 1, "Riker")));
    }

    /// <summary>
    /// Mission 1 to Riker (16, loaded, rack 1) and Shubin (8, planned, rack 2); Orison (16) and Lorville (16) loaded in
    /// racks 3 and 4. 24 SCU are free: rack 2 alone.
    /// </summary>
    private static SavedPlan PartlyLoaded()
    {
        var saved = Placed(HullA,
            [Stop("Riker", "Titanium", 16, CrateSize.Sixteen), Stop("Shubin", "Gold", 8, CrateSize.Eight)],
            [Stop("Orison", "Quartz", 16, CrateSize.Sixteen)],
            [Stop("Lorville", "Tin", 16, CrateSize.Sixteen)]);
        return Loaded(Loaded(Loaded(saved, HullA, "Riker", 1), HullA, "Orison", 2), HullA, "Lorville", 3);
    }

    [Fact]
    public void A_partly_loaded_mission_whose_rest_finds_no_room_refuses_the_plan()
    {
        // E1 fallback: a mission waits whole, and its loaded crates can't wait.
        var current = PartlyLoaded();
        Mission[] grown = [new Mission(1, [current.Missions[0].Stops[0], Stop("Shubin", "Gold", 24, CrateSize.Eight) with { Order = 2 }]), .. current.Missions.Skip(1)];

        var act = () => LoadPlanner.PlanKeeping(current, HullA, grown, ["Riker", "Shubin", "Orison", "Lorville"], FillOrder.BowFirst, 0);

        act.Should().Throw<PlanException>().WithMessage("*Shubin*24 SCU*loaded*");
    }

    [Fact]
    public void The_unloaded_deliveries_of_a_partly_loaded_mission_go_first()
    {
        // Area18 (16) would take rack 2 before Shubin (8) by size; Shubin's mission is loaded in part, so it comes first.
        var current = PartlyLoaded();
        Mission[] missions = [.. current.Missions, new Mission(4, [Stop("Area18", "Stims", 16, CrateSize.Sixteen)])];

        var (kept, _) = LoadPlanner.PlanKeeping(current, HullA, missions, ["Riker", "Shubin", "Orison", "Lorville", "Area18"], FillOrder.BowFirst, 0);

        kept.Plan.Crates.Should().Contain(c => c.Destination == "Shubin");
        kept.Missions.Single(m => m.Id == 4).Unplaced.Should().BeTrue();
    }

    /// <summary>
    /// Riker (16) loaded in rack 1, Orison (8) and Lorville (8) loaded in racks 3 and 4; Area18's two 8s beside them, Shubin's
    /// 16 in rack 2. Laid anew around the loaded cargo, Area18 (by name before Shubin at equal SCU) takes rack 2 and Shubin waits.
    /// </summary>
    private static SavedPlan GrownLayout()
    {
        Mission[] missions =
        [
            new(1, [Stop("Riker", "Titanium", 16, CrateSize.Sixteen)]),
            new(2, [Stop("Orison", "Quartz", 8, CrateSize.Eight)]),
            new(3, [Stop("Lorville", "Tin", 8, CrateSize.Eight)]),
            new(4, [Stop("Area18", "Stims", 16, CrateSize.Eight)]),
            new(5, [Stop("Shubin", "Gold", 16, CrateSize.Sixteen)]),
        ];
        Crate[] crates =
        [
            new("Riker", "Titanium", CrateSize.Sixteen, "rack-0", new GridPosition(0, 0, 0), Mission: 1, Loaded: true),
            new("Shubin", "Gold", CrateSize.Sixteen, "rack-1", new GridPosition(0, 0, 0), Mission: 5),
            new("Orison", "Quartz", CrateSize.Eight, "rack-2", new GridPosition(0, 0, 0), Mission: 2, Loaded: true),
            new("Area18", "Stims", CrateSize.Eight, "rack-2", new GridPosition(2, 0, 0), Mission: 4),
            new("Lorville", "Tin", CrateSize.Eight, "rack-3", new GridPosition(0, 0, 0), Mission: 3, Loaded: true),
            new("Area18", "Stims", CrateSize.Eight, "rack-3", new GridPosition(2, 0, 0), Mission: 4),
        ];
        return new SavedPlan(missions, new LoadPlan(HullA.Id, crates, []), CrateCap.Of(HullA));
    }

    [Fact]
    public void Replan_with_loaded_cargo_keeps_the_layout_when_more_would_wait()
    {
        // E2: the grown layout holds everything; laid anew, Shubin would wait.
        var current = GrownLayout();
        var (anew, _) = LoadPlanner.PlanKeeping(current, HullA, current.Missions, [.. current.Stops.Select(s => s.Destination)], FillOrder.BowFirst, 0);
        anew.Missions.Should().Contain(m => m.Unplaced);

        LoadPlanner.Replan(current, HullA).Should().BeSameAs(current);
    }

    /// <summary>
    /// Mission 1: Riker (16) loaded in rack 1, its Shubin (24 = 16 + 8) planned in rack 2 and beside Orison's loaded 8 in
    /// rack 3; Lorville (16) loaded in rack 4. The ship is full.
    /// </summary>
    private static SavedPlan PartlyLoadedAndFull()
    {
        Mission[] missions =
        [
            new(1, [Stop("Riker", "Titanium", 16, CrateSize.Sixteen), Stop("Shubin", "Gold", 24, CrateSize.Sixteen) with { Order = 2 }]),
            new(2, [Stop("Orison", "Quartz", 8, CrateSize.Eight)]),
            new(3, [Stop("Lorville", "Tin", 16, CrateSize.Sixteen)]),
        ];
        Crate[] crates =
        [
            new("Riker", "Titanium", CrateSize.Sixteen, "rack-0", new GridPosition(0, 0, 0), Mission: 1, Loaded: true),
            new("Shubin", "Gold", CrateSize.Sixteen, "rack-1", new GridPosition(0, 0, 0), Mission: 1, Item: 1),
            new("Shubin", "Gold", CrateSize.Eight, "rack-2", new GridPosition(0, 0, 0), Mission: 1, Item: 1),
            new("Orison", "Quartz", CrateSize.Eight, "rack-2", new GridPosition(2, 0, 0), Mission: 2, Loaded: true),
            new("Lorville", "Tin", CrateSize.Sixteen, "rack-3", new GridPosition(0, 0, 0), Mission: 3, Loaded: true),
        ];
        return new SavedPlan(missions, new LoadPlan(HullA.Id, crates, []), CrateCap.Of(HullA));
    }

    [Fact]
    public void Replan_lays_the_rest_of_a_partly_loaded_mission_around_the_loaded_cargo_without_giving_up()
    {
        // T7.5a2, the check behind "Re-plan found no better layout": its PlanException branch keeps a plan that already
        // holds everything; the rest of the mission is laid largest first into the cells the current plan proves free.
        var current = PartlyLoadedAndFull();
        LoadPlanner.Validate(current.Plan, HullA).Should().BeEmpty();

        var keeping = () => LoadPlanner.PlanKeeping(current, HullA, current.Missions, [.. current.Stops.Select(s => s.Destination)], FillOrder.BowFirst, 0);
        keeping.Should().NotThrow();

        var replanned = LoadPlanner.Replan(current, HullA);
        LoadPlanner.Validate(replanned.Plan, HullA).Should().BeEmpty();
        replanned.Missions.Should().NotContain(m => m.Unplaced);
        Of(replanned, 1, "Shubin").Select(c => c.CrateSize).Should().BeEquivalentTo([CrateSize.Sixteen, CrateSize.Eight]);
        Lying(replanned.Plan.Crates.Where(c => c.Loaded)).Should().Equal(Lying(current.Plan.Crates.Where(c => c.Loaded)));
    }

    // --- Another ship ---

    [Fact]
    public void A_ship_change_clears_loaded_and_keeps_delivered()
    {
        var saved = Placed(HullA, [Stop("Riker", "Titanium", 8, CrateSize.Four)], [Stop("Shubin", "Gold", 4, CrateSize.Four)], [Stop("Orison", "Quartz", 4, CrateSize.Four)]);
        var loaded = Loaded(saved, HullA, "Riker", 1);
        var state = With(loaded, LoadPlanner.SetDelivered(loaded.Plan, HullA, "Shubin", 2, true));

        var there = LoadPlanner.Replan(state, Freelancer);

        there.Plan.Crates.Should().OnlyContain(c => !c.Loaded);
        Of(there, 1, "Riker").Should().NotBeEmpty();
        Of(there, 2, "Shubin").Should().NotBeEmpty().And.OnlyContain(c => c.Delivered && c.OffShip);
    }
}
