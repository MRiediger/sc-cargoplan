using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A contract gone from ContractTracker (D81): marked gone and kept by default; with <see cref="ContractGonePolicy.Remove"/>
/// planned and delivered deliveries leave when it goes, loaded ones never, and later only a mission with nothing left
/// planned or loaded; the guard when all go at once; back by the same id; ids of an earlier database detached.
/// </summary>
public class ContractGoneTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static SavedPlan Empty => new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));

    private static MissionStop Stop(string destination, int scu = 4, CrateSize max = CrateSize.Four) =>
        new(destination, 1, [new MissionItem("Stims", scu, max)]);

    private static ContractCapture Capture(long[] ids, params MissionStop[] stops) =>
        new([.. stops.Select((s, i) => s with { Order = i + 1 })], new ContractSource(ids));

    private static SavedPlan Synced(params ContractCapture[] captures) => ContractSync.Apply(Empty, HullA, captures, null);

    private static SavedPlan With(SavedPlan saved, LoadPlan plan) => new(saved.Missions, plan, saved.CrateCap, saved.Watermark);

    private static Mission MissionTo(SavedPlan saved, string destination) =>
        saved.Missions.Single(m => m.Stops.Any(s => s.Destination == destination));

    private static SavedPlan Loaded(SavedPlan saved, string destination) =>
        With(saved, LoadPlanner.SetLoaded(saved.Plan, HullA, destination, MissionTo(saved, destination).Id, true));

    private static SavedPlan Delivered(SavedPlan saved, string destination) =>
        With(saved, LoadPlanner.SetDelivered(saved.Plan, HullA, destination, true));

    private static SavedPlan Keep(SavedPlan saved, params long[] present) => ContractSync.Apply(saved, HullA, [], Ids(present));

    private static SyncResult Remove(SavedPlan saved, params long[] present) =>
        ContractSync.Sync(saved, HullA, [], Ids(present), onGone: ContractGonePolicy.Remove);

    private static HashSet<long> Ids(params long[] ids) => [.. ids];

    private static Mission? Of(SavedPlan saved, long id) => saved.Missions.SingleOrDefault(m => m.Contract!.ContractIds.Contains(id));

    /// <summary>Riker (36) loaded, Shubin (37) delivered, Orison (38) planned, Lorville (40) still in ContractTracker.</summary>
    private static SavedPlan FourStates() =>
        Delivered(Loaded(Synced(
            Capture([36], Stop("Riker")), Capture([37], Stop("Shubin")), Capture([38], Stop("Orison")), Capture([40], Stop("Lorville"))),
            "Riker"), "Shubin");

    /// <summary>Five 16 SCU contracts on the Hull A: the fifth waits.</summary>
    private static SavedPlan Full()
    {
        var full = Synced(
            Capture([1], Stop("Riker", 16, CrateSize.Sixteen)), Capture([2], Stop("Shubin", 16, CrateSize.Sixteen)),
            Capture([3], Stop("Orison", 16, CrateSize.Sixteen)), Capture([4], Stop("Lorville", 16, CrateSize.Sixteen)),
            Capture([5], Stop("Hurston", 16, CrateSize.Sixteen)));
        full.MissionOf(5).Unplaced.Should().BeTrue();
        return full;
    }

    private static SavedPlan Unmarked(SavedPlan saved) =>
        new([.. saved.Missions.Select(m => m.Contract is { } c ? m with { Contract = c with { Gone = false } } : m)], saved.Plan, saved.CrateCap, saved.Watermark);

    private static string Written(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static SavedPlan Read(string json) => PlanFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "plan.json", HullA);

    // --- keep (the default) ---

    [Fact]
    public void Keep_marks_gone_contracts_and_changes_nothing_else()
    {
        var saved = FourStates();

        var synced = Keep(saved, 40);

        new[] { 36L, 37, 38, 40 }.Select(id => Of(synced, id)!.Contract!.Gone).Should().Equal(true, true, true, false);
        Written(Unmarked(synced)).Should().Be(Written(saved));
    }

    [Fact]
    public void Keep_twice_returns_the_same_plan()
    {
        var once = Keep(FourStates(), 40);

        Keep(once, 40).Should().BeSameAs(once);
    }

    [Fact]
    public void A_waiting_contract_that_is_gone_waits_without_being_placed()
    {
        var gone = Keep(Full(), 1, 2, 3, 4);
        gone.MissionOf(5).Contract!.Gone.Should().BeTrue();

        var again = Keep(Delivered(gone, "Riker"), 1, 2, 3, 4);

        again.MissionOf(5).Unplaced.Should().BeTrue("no planner move for a contract ContractTracker no longer lists");
    }

    [Fact]
    public void Keep_reports_each_contract_kept()
    {
        var result = ContractSync.Sync(FourStates(), HullA, [], Ids(40));

        result.Gone.Select(g => (g.Mission.Contract!.ContractIds[0], g.Planned, g.Delivered, g.Loaded, g.Left))
            .Should().Equal((36L, 0, 0, 0, false), (37L, 0, 0, 0, false), (38L, 0, 0, 0, false));
        result.AllAtOnce.Should().BeFalse();
    }

    // --- remove: when the contract goes ---

    [Fact]
    public void Remove_takes_planned_and_delivered_deliveries_and_keeps_loaded_ones()
    {
        var saved = FourStates();

        var result = Remove(saved, 40);

        Of(result.Saved, 37).Should().BeNull();
        Of(result.Saved, 38).Should().BeNull();
        Of(result.Saved, 36)!.Contract!.Gone.Should().BeTrue();
        result.Saved.Plan.Crates.Where(c => c.Destination == "Riker").Should().Equal(saved.Plan.Crates.Where(c => c.Destination == "Riker"));
        result.Gone.Select(g => (g.Mission.Contract!.ContractIds[0], g.Planned, g.Delivered, g.Loaded, g.Left))
            .Should().Equal((36L, 0, 0, 1, false), (37L, 0, 1, 0, true), (38L, 1, 0, 0, true));
    }

    [Fact]
    public void Remove_of_a_mission_loaded_in_part_keeps_only_its_loaded_deliveries()
    {
        var saved = Delivered(Loaded(Synced(Capture([36], Stop("Riker"), Stop("Shubin"), Stop("Orison")), Capture([40], Stop("Lorville"))), "Riker"), "Orison");

        var result = Remove(saved, 40);

        var mission = Of(result.Saved, 36)!;
        mission.Stops.Select(s => s.Destination).Should().Equal("Riker");
        mission.Contract!.Gone.Should().BeTrue();
        result.Saved.Plan.Crates.Where(c => c.Mission == mission.Id).Should().OnlyContain(c => c.Destination == "Riker" && c.Loaded);
        result.Gone.Single().Should().Match<GoneReport>(g => g.Planned == 1 && g.Delivered == 1 && g.Loaded == 1 && !g.Left);
        LoadPlanner.Validate(result.Saved.Plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void Remove_takes_a_waiting_contract()
    {
        var result = Remove(Full(), 1, 2, 3, 4);

        result.Saved.Missions.Select(m => m.Id).Should().Equal(1, 2, 3, 4);
        result.Gone.Single().Should().Match<GoneReport>(g => g.Left && g.Mission.Unplaced);
    }

    // --- remove: later syncs (K1: never planned cargo afterwards) ---

    [Fact]
    public void Remove_takes_a_gone_contract_once_its_loaded_cargo_is_delivered()
    {
        var first = Remove(FourStates(), 40).Saved;

        var result = Remove(Delivered(first, "Riker"), 40);

        Of(result.Saved, 36).Should().BeNull();
        result.Gone.Single().Should().Match<GoneReport>(g => g.Delivered == 1 && g.Left);
    }

    [Fact]
    public void Switching_from_keep_to_remove_takes_no_planned_cargo()
    {
        var kept = Keep(FourStates(), 40);

        var result = Remove(kept, 40);

        Of(result.Saved, 38)!.Contract!.Gone.Should().BeTrue("planned cargo leaves only when its contract goes");
        Of(result.Saved, 36)!.Contract!.Gone.Should().BeTrue();
        Of(result.Saved, 37).Should().BeNull("all delivered: nothing of it is left on board");
    }

    // --- all at once ---

    [Fact]
    public void All_contracts_gone_at_once_are_kept_even_with_remove_and_a_second_sync_takes_nothing()
    {
        var saved = Loaded(Synced(Capture([36], Stop("Riker")), Capture([37], Stop("Shubin"))), "Shubin");

        var first = Remove(saved);
        var second = Remove(first.Saved);

        first.AllAtOnce.Should().BeTrue();
        first.Saved.Missions.Should().HaveCount(2).And.OnlyContain(m => m.Contract!.Gone);
        Written(Unmarked(first.Saved)).Should().Be(Written(saved));
        second.Saved.Should().BeSameAs(first.Saved);
    }

    [Fact]
    public void A_delivered_contract_kept_by_the_guard_leaves_at_the_next_sync_with_remove()
    {
        var saved = Delivered(Synced(Capture([36], Stop("Riker")), Capture([37], Stop("Shubin"))), "Riker");

        var second = Remove(Remove(saved).Saved);

        second.Saved.Missions.Select(m => m.Contract!.ContractIds[0]).Should().Equal(37);
    }

    [Fact]
    public void One_contract_gone_is_no_reset()
    {
        var result = Remove(Synced(Capture([36], Stop("Riker"))));

        result.AllAtOnce.Should().BeFalse();
        result.Saved.Missions.Should().BeEmpty();
    }

    [Fact]
    public void A_new_database_is_kept_even_with_remove()
    {
        var saved = Synced(Capture([36], Stop("Riker")));

        var result = ContractSync.Sync(saved, HullA, [], Ids(), databaseIsNew: true, onGone: ContractGonePolicy.Remove);

        result.AllAtOnce.Should().BeTrue();
        result.Saved.Missions.Single().Contract!.Gone.Should().BeTrue();
    }

    // --- back ---

    [Fact]
    public void A_contract_back_in_ContractTracker_loses_its_mark_once()
    {
        var gone = Keep(FourStates(), 40);

        var result = ContractSync.Sync(gone, HullA, [], Ids(36, 37, 38, 40));

        result.Saved.Missions.Should().OnlyContain(m => !m.Contract!.Gone);
        result.Back.Select(m => m.Contract!.ContractIds[0]).Should().Equal(36, 37, 38);
        Written(result.Saved).Should().Be(Written(FourStates()));
        ContractSync.Apply(result.Saved, HullA, [], Ids(36, 37, 38, 40)).Should().BeSameAs(result.Saved);
    }

    [Fact]
    public void A_read_is_quiet_while_gone_contracts_stay_gone_and_listed_ones_stay_listed()
    {
        var gone = Keep(FourStates(), 40);

        ContractSync.Quiet(gone, Ids(40)).Should().BeTrue();
        ContractSync.Quiet(gone, Ids(36, 40)).Should().BeFalse("36 is back");
        ContractSync.Quiet(gone, Ids()).Should().BeFalse("40 is gone");
        ContractSync.Quiet(FourStates(), Ids(36, 37, 38, 40)).Should().BeTrue();
    }

    // --- a new database (K2) ---

    [Fact]
    public void A_new_database_detaches_the_old_ids_so_a_new_contract_1_is_new()
    {
        var old = Synced(Capture([1], Stop("Riker")), Capture([2], Stop("Shubin")));

        var reset = ContractSync.Apply(old, HullA, [Capture([1], Stop("Orison"))], Ids(), databaseIsNew: true);

        reset.Missions.Select(m => (m.Stops[0].Destination, m.Contract!.Gone, m.Contract.Detached))
            .Should().Equal(("Riker", true, true), ("Shubin", true, true), ("Orison", false, false));
        ContractSync.Apply(reset, HullA, [], Ids(1)).Should().BeSameAs(reset, "the old #1 is detached: the new #1 doesn't bring it back");
    }

    [Fact]
    public void Plan_keeping_never_gives_a_detached_delivery_s_state_to_a_new_contract_with_its_id()
    {
        var old = Loaded(Synced(Capture([1], Stop("Riker"))), "Riker");
        // The same crates: by id alone, the new #1 would take the old one's loaded cargo.
        var reset = ContractSync.Apply(old, HullA, [Capture([1], Stop("Riker"))], Ids(), databaseIsNew: true);
        var (detached, fresh) = (reset.Missions[0], reset.Missions[1]);

        var (planned, _) = LoadPlanner.PlanKeeping(reset, HullA, [fresh, detached], ["Riker"]);

        planned.Plan.Crates.Where(c => c.Mission == detached.Id).Should().OnlyContain(c => c.Loaded);
        planned.Plan.Crates.Where(c => c.Mission == fresh.Id).Should().NotContain(c => c.Loaded);
    }

    [Fact]
    public void Detached_needs_gone()
    {
        var act = () => new ContractSource([1], Detached: true);

        act.Should().Throw<ArgumentException>().WithMessage("*detached*gone*");
    }

    // --- plan file schema 8 ---

    [Fact]
    public void Write_writes_gone_and_detached_only_where_true()
    {
        var node = JsonNode.Parse(Written(Keep(FourStates(), 40)))!;

        node["schemaVersion"]!.GetValue<int>().Should().Be(8);
        var contracts = node["missions"]!.AsArray().Select(m => m!["contract"]!.AsObject()).ToList();
        contracts.Select(c => c.ContainsKey("gone")).Should().Equal(true, true, true, false);
        contracts.Where(c => c.ContainsKey("gone")).Should().OnlyContain(c => c["gone"]!.GetValue<bool>());
        contracts.Should().NotContain(c => c.ContainsKey("detached"));
    }

    [Fact]
    public void Write_then_Read_keeps_gone_and_detached()
    {
        var saved = ContractSync.Apply(Synced(Capture([1], Stop("Riker")), Capture([2], Stop("Shubin"))), HullA, [Capture([1], Stop("Orison"))], Ids(), databaseIsNew: true);

        Written(Read(Written(saved))).Should().Be(Written(saved));
        Read(Written(saved)).Missions.Select(m => (m.Contract!.Gone, m.Contract.Detached)).Should().Equal((true, true), (true, true), (false, false));
    }

    [Fact]
    public void Schema_7_with_gone_is_broken()
    {
        var node = JsonNode.Parse(Written(Keep(FourStates(), 40)))!;
        node["schemaVersion"] = 7;

        var act = () => Read(node.ToJsonString());

        act.Should().Throw<PlanFormatException>().WithMessage("*gone*schema 8*");
    }

    [Fact]
    public void Detached_without_gone_in_the_file_is_broken()
    {
        var node = JsonNode.Parse(Written(FourStates()))!;
        node["missions"]![0]!["contract"]!["detached"] = true;

        var act = () => Read(node.ToJsonString());

        act.Should().Throw<PlanFormatException>().WithMessage("*detached*gone*");
    }

    [Fact]
    public void A_schema_7_file_reads_without_gone_and_writes_back_as_schema_8()
    {
        string golden;
        using (var reader = new StreamReader(MissionFixtures.Open("plans/golden_example_missions_bow.json")))
        {
            golden = reader.ReadToEnd().ReplaceLineEndings("\n");
        }

        var schema7 = golden.Replace("\"schemaVersion\": 8", "\"schemaVersion\": 7", StringComparison.Ordinal);
        schema7.Should().NotBe(golden);

        var saved = Read(schema7);

        saved.Missions.Should().OnlyContain(m => m.Contract == null || !m.Contract.Gone);
        Written(saved).ReplaceLineEndings("\n").Should().Be(golden);
    }
}
