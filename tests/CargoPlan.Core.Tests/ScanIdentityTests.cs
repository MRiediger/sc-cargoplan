using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A contract read by CargoPlan's own scan (D102, T10.2b1): identified by its scan number and content key (D101), never
/// by ContractTracker's ids; plan schema 10 keeps it, and every ContractTracker rule leaves it alone.
/// </summary>
public class ScanIdentityTests
{
    private const string Key = "EVERUSHARBOR | RIKER:STIMS | 4 | 50250";

    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static SavedPlan Empty => new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));

    private static MissionStop Stop(string destination, string commodity = "Stims", int scu = 4) =>
        new(destination, 1, [new MissionItem(commodity, scu, CrateSize.Four)]);

    private static ContractSource Scanned(int no, string key = Key, bool estimated = false) =>
        ContractSource.Scanned(new ScanIdentity(no, key, estimated), reward: 50_250);

    /// <summary>A ContractTracker capture #1 to Orison and a scanned contract #1 to Riker.</summary>
    private static SavedPlan CaptureAndScan()
    {
        var captured = LoadPlanner.PlaceInto(Empty, HullA, [Stop("Orison")], new ContractSource([1]));
        return LoadPlanner.PlaceInto(captured, HullA, [Stop("Riker")], Scanned(1));
    }

    private static string Written(SavedPlan saved, int lastScanNo = 0)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved, lastScanNo);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static (SavedPlan Plan, int LastScanNo) Read(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var (plan, lastScanNo, _) = PlanFile.ReadContent(stream, "plan.json", HullA);
        return (plan, lastScanNo);
    }

    // --- The identity ---

    [Fact]
    public void A_scanned_contract_has_no_ContractTracker_id()
    {
        var source = Scanned(3, estimated: true);

        source.ContractIds.Should().BeEmpty();
        source.Scan.Should().Be(new ScanIdentity(3, Key, true));
        source.Reward.Should().Be(50_250);
    }

    [Fact]
    public void A_contract_has_ContractTracker_ids_or_a_scan_never_both_or_neither()
    {
        var both = () => new ContractSource([36], Scan: new ScanIdentity(1, Key, false));
        var neither = () => new ContractSource([]);

        both.Should().Throw<ArgumentException>().WithMessage("*exactly one*");
        neither.Should().Throw<ArgumentException>().WithMessage("*exactly one*");
    }

    [Fact]
    public void A_scanned_contract_is_never_gone()
    {
        // Gone and detached are ContractTracker's (D81): a scan has no list it could leave.
        var act = () => new ContractSource([], Gone: true, Scan: new ScanIdentity(1, Key, false));

        act.Should().Throw<ArgumentException>().WithMessage("*scan*gone*");
    }

    [Theory]
    [InlineData(0, Key)]
    [InlineData(1, " ")]
    public void A_scan_has_a_number_from_1_and_a_key(int no, string key)
    {
        var act = () => new ScanIdentity(no, key, false);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_plan_holds_each_scan_number_once()
    {
        var once = LoadPlanner.PlaceInto(Empty, HullA, [Stop("Riker")], Scanned(2));

        var act = () => LoadPlanner.PlaceInto(once, HullA, [Stop("Orison")], Scanned(2, "ANOTHER | KEY | ? | ?"));

        act.Should().Throw<ArgumentException>().WithMessage("*scan #2*");
    }

    // --- Plan schema 10 ---

    [Fact]
    public void Write_then_Read_keeps_a_scanned_contract_and_the_last_scan_number()
    {
        // A capture beside it would come back as a scan of its own (D107, LegacyCaptureTests).
        var saved = LoadPlanner.PlaceInto(Empty, HullA, [Stop("Riker")], Scanned(1));

        var (read, lastScanNo) = Read(Written(saved, lastScanNo: 4));

        read.Missions.Select(m => m.Contract).Should().BeEquivalentTo(saved.Missions.Select(m => m.Contract), o => o.WithStrictOrdering());
        lastScanNo.Should().Be(4);
    }

    [Fact]
    public void A_scanned_contract_is_written_with_its_scan_and_without_ContractTracker_ids()
    {
        var node = JsonNode.Parse(Written(CaptureAndScan(), lastScanNo: 4))!;

        var contract = node["missions"]![1]!["contract"]!.AsObject();
        contract.ContainsKey("contractIds").Should().BeFalse();
        contract["scan"]!.ToJsonString().Should().Be($$"""{"no":1,"key":"{{Key}}","estimated":false}""");
        node["lastScanNo"]!.GetValue<int>().Should().Be(4);
        node["missions"]![0]!["contract"]!["contractIds"]!.AsArray().Select(id => id!.GetValue<long>()).Should().Equal(1);
    }

    [Fact]
    public void A_plan_without_a_scan_writes_no_last_scan_number()
    {
        JsonNode.Parse(Written(Empty))!.AsObject().ContainsKey("lastScanNo").Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void The_last_scan_number_read_is_never_below_a_scan_in_the_plan(int lastScanNo)
    {
        // Only a mission removed after the highest number lifts the file's value above the plan's; never the other way.
        var node = JsonNode.Parse(Written(LoadPlanner.PlaceInto(Empty, HullA, [Stop("Riker")], Scanned(3))))!;
        node["lastScanNo"] = lastScanNo;

        Read(node.ToJsonString()).LastScanNo.Should().Be(3);
    }

    [Theory]
    [InlineData("scan")]
    [InlineData("lastScanNo")]
    public void A_scan_in_a_schema_9_file_is_an_error(string field)
    {
        var node = JsonNode.Parse(Written(CaptureAndScan(), lastScanNo: 4))!;
        node["schemaVersion"] = 9;
        if (field == "lastScanNo")
        {
            node["missions"]!.AsArray().RemoveAt(1);
            node["crates"] = new JsonArray([.. node["crates"]!.AsArray().Where(c => c!["mission"]!.GetValue<int>() == 1).Select(c => c!.DeepClone())]);
        }
        else
        {
            node.AsObject().Remove("lastScanNo");
        }

        var act = () => Read(node.ToJsonString());

        act.Should().Throw<PlanFormatException>().WithMessage($"*{field}*new in schema 10*the file says 9*");
    }

    [Fact]
    public void A_contract_with_ids_and_a_scan_in_the_file_is_an_error()
    {
        var node = JsonNode.Parse(Written(CaptureAndScan()))!;
        node["missions"]![1]!["contract"]!["contractIds"] = new JsonArray(5);

        var act = () => Read(node.ToJsonString());

        act.Should().Throw<PlanFormatException>().WithMessage("*scanned contract has no ContractTracker ids*");
    }

    // --- ContractTracker's rules leave a scan alone ---

    [Fact]
    public void A_sync_never_marks_a_scanned_contract_gone()
    {
        // ContractTracker lists its capture #1; a scan has no id it could list, so "no id present" must not count.
        var result = ContractSync.Sync(CaptureAndScan(), HullA, [], new HashSet<long> { 1 });

        result.Gone.Should().BeEmpty();
        result.Saved.Missions.Should().OnlyContain(m => !m.Contract!.Gone);
    }

    [Fact]
    public void A_read_with_the_capture_still_listed_is_quiet_beside_a_scan()
    {
        ContractSync.Quiet(CaptureAndScan(), new HashSet<long> { 1 }).Should().BeTrue();
    }

    [Fact]
    public void The_sync_after_an_undo_never_brings_back_a_scanned_contract()
    {
        // Resync places the captures that came in by a sync since; a scan is an undo step of its own (K2.4), never one.
        var target = LoadPlanner.PlaceInto(Empty, HullA, [Stop("Orison")], new ContractSource([1]));

        var resynced = ContractSync.Resync(target, target, CaptureAndScan(), HullA, new HashSet<long> { 1 });

        resynced.Missions.Should().ContainSingle().Which.Contract!.ContractIds.Should().Equal(1);
    }

    [Fact]
    public void A_ticked_delivery_of_ContractTracker_capture_3_is_no_delivery_of_scan_3()
    {
        // The editor's Plan keeps a delivery by its contract (D41 keeping): capture #3 and scan #3 are two contracts.
        var captured = LoadPlanner.PlaceInto(Empty, HullA, [Stop("Riker")], new ContractSource([3]));
        SavedPlan ticked = new(captured.Missions, LoadPlanner.SetDelivered(captured.Plan, HullA, "Riker", true), captured.CrateCap);
        Mission[] scanned = [new Mission(1, [Stop("Riker")], Scanned(3))];

        var (plan, _) = LoadPlanner.PlanKeeping(ticked, HullA, scanned, ["Riker"]);

        plan.Plan.Crates.Should().NotBeEmpty().And.OnlyContain(c => !c.Delivered);
    }

    [Fact]
    public void A_ticked_delivery_of_scan_3_stays_ticked_for_scan_3()
    {
        var placed = LoadPlanner.PlaceInto(Empty, HullA, [Stop("Riker")], Scanned(3));
        SavedPlan ticked = new(placed.Missions, LoadPlanner.SetDelivered(placed.Plan, HullA, "Riker", true), placed.CrateCap);

        var (plan, _) = LoadPlanner.PlanKeeping(ticked, HullA, placed.Missions, ["Riker"]);

        plan.Plan.Crates.Should().NotBeEmpty().And.OnlyContain(c => c.Delivered);
    }
}
