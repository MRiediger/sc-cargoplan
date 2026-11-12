using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// An older plan file's ContractTracker captures (D107): read as scans with the next numbers in file order and a key from
/// their ids, never estimated; gone, detached and the watermark go, missions, crates and ticks stay as they are.
/// </summary>
public class LegacyCaptureTests
{
    private const string Raft = "raft_hand_2026-11-05";
    private const string Mixed = "ct_and_scan_schema10";

    private static readonly Ship RaftShip = FrozenShips.Load("raft");
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static string FixtureText(string name)
    {
        using var reader = new StreamReader(MissionFixtures.Open($"plans/{name}.json"));
        return reader.ReadToEnd();
    }

    private static (SavedPlan Plan, int LastScanNo, IReadOnlyList<int> FromContractTracker) Read(string json, Ship ship)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return PlanFile.ReadContent(stream, "plan.json", ship);
    }

    private static string Written(SavedPlan saved, int lastScanNo = 0)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved, lastScanNo);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void Martins_RAFT_plan_reads_its_captures_as_scans_1_to_4_in_file_order()
    {
        var (saved, lastScanNo, fromContractTracker) = Read(FixtureText(Raft), RaftShip);

        saved.Missions.Select(m => (m.Id, m.Contract!.Scan!.No, m.Contract.Scan.Key)).Should().Equal(
            (1, 1, "legacy:233-234-235"),
            (2, 2, "legacy:236-237-238"),
            (3, 3, "legacy:239-240"),
            (4, 4, "legacy:241"));
        saved.Missions.Should().OnlyContain(m => !m.Contract!.Scan!.QuantitiesEstimated, "the player works with these amounts");
        lastScanNo.Should().Be(4);
        fromContractTracker.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void A_capture_keeps_what_the_legend_marks()
    {
        var (saved, _, _) = Read(FixtureText(Raft), RaftShip);

        saved.Missions.Select(m => (m.Contract!.CrateSizeAssumed, m.Contract.ListedDestinations, m.Contract.Reward)).Should().Equal(
            (true, 4, null),
            (true, 3, null),
            (true, null, null),
            (true, 2, (long?)null));
    }

    [Fact]
    public void A_converted_plan_writes_its_missions_and_crates_as_the_file_had_them()
    {
        var file = JsonNode.Parse(FixtureText(Raft))!;
        var (saved, lastScanNo, _) = Read(file.ToJsonString(), RaftShip);

        var written = JsonNode.Parse(Written(saved, lastScanNo))!;

        JsonNode.DeepEquals(written["crates"], file["crates"]).Should().BeTrue("ticks, positions and items stay byte for byte");
        foreach (var (before, after) in file["missions"]!.AsArray().Zip(written["missions"]!.AsArray()))
        {
            JsonNode.DeepEquals(after!["stops"], before!["stops"]).Should().BeTrue();
            after["id"]!.GetValue<int>().Should().Be(before["id"]!.GetValue<int>());
            after["unplaced"]!.GetValue<bool>().Should().Be(before["unplaced"]!.GetValue<bool>());
        }

        written.AsObject().Should().NotContainKey("contractTrackerWatermark");
        written["missions"]!.AsArray().Select(m => m!["contract"]!.AsObject()).Should()
            .OnlyContain(c => !c.ContainsKey("contractIds") && !c.ContainsKey("gone") && !c.ContainsKey("detached"));
    }

    [Fact]
    public void A_converted_plan_reads_back_without_a_second_conversion()
    {
        var (saved, lastScanNo, _) = Read(FixtureText(Raft), RaftShip);

        var (again, againLastScanNo, fromContractTracker) = Read(Written(saved, lastScanNo), RaftShip);

        fromContractTracker.Should().BeEmpty();
        again.Missions.Select(m => m.Contract).Should().BeEquivalentTo(saved.Missions.Select(m => m.Contract), o => o.WithStrictOrdering());
        againLastScanNo.Should().Be(4);
    }

    [Fact]
    public void Captures_beside_scans_get_numbers_above_the_last_scan_number()
    {
        // The file says lastScanNo 5 beside scan #2; the captures come after it in file order.
        var (saved, lastScanNo, fromContractTracker) = Read(FixtureText(Mixed), HullA);

        saved.Missions.Select(m => (m.Id, m.Contract!.Scan!.No, m.Contract.Scan.Key.StartsWith("legacy:", StringComparison.Ordinal))).Should().Equal(
            (1, 6, true),
            (2, 2, false),
            (3, 7, true));
        fromContractTracker.Should().Equal(6, 7);
        lastScanNo.Should().Be(7);
        saved.MissionOf(3).Contract!.Scan!.Key.Should().Be("legacy:40-41", "ids in ascending order; gone and the watermark go");
    }

    [Fact]
    public void Captures_get_numbers_above_the_highest_scan_when_the_file_names_a_lower_last_scan_number()
    {
        var file = JsonNode.Parse(FixtureText(Mixed))!;
        file["lastScanNo"] = 1;

        var (saved, _, fromContractTracker) = Read(file.ToJsonString(), HullA);

        fromContractTracker.Should().Equal(3, 4);
        saved.Missions.Select(m => m.Contract!.Scan!.No).Should().Equal(3, 2, 4);
    }

    [Fact]
    public void A_waiting_mission_once_gone_from_ContractTracker_is_placed_when_it_fits()
    {
        // Riker fills the Hull A and is delivered, so its cells are free; Shubin waited, gone from ContractTracker (D81).
        static MissionStop Stop(string destination, int scu, CrateSize max) => new(destination, 1, [new MissionItem("Titanium", scu, max)]);
        SavedPlan empty = new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));
        var full = LoadPlanner.PlaceInto(empty, HullA, [Stop("Riker", 64, CrateSize.Sixteen)]);
        var waiting = LoadPlanner.PlaceInto(full, HullA, [Stop("Shubin", 8, CrateSize.Four)]);
        SavedPlan delivered = new(waiting.Missions, LoadPlanner.SetDelivered(waiting.Plan, HullA, "Riker", true), waiting.CrateCap);
        var file = JsonNode.Parse(Written(delivered))!;
        file["missions"]![1]!["contract"] = new JsonObject { ["contractIds"] = new JsonArray(40), ["crateSizeAssumed"] = false, ["gone"] = true };

        var (saved, _, _) = Read(file.ToJsonString(), HullA);
        var retried = LoadPlanner.RetryUnplaced(saved, HullA);

        saved.MissionOf(2).Unplaced.Should().BeTrue();
        retried.MissionOf(2).Unplaced.Should().BeFalse();
        retried.Plan.Crates.Where(c => c.Mission == 2).Should().HaveCount(2);
    }

    [Fact]
    public void A_scan_of_a_converted_missions_route_is_a_new_mission()
    {
        // A real content key (D101) never equals a legacy key: the player sees two missions and deletes one.
        var (saved, lastScanNo, _) = Read(FixtureText(Raft), RaftShip);
        var first = saved.MissionOf(1);
        ContractCapture scan = new(first.Stops, new ContractSource(new ScanIdentity(1, "PORTOLISAR | GREENGLADE:WASTE | 4 | 50250", true)));

        var merge = ScanSync.Apply(saved, RaftShip, scan, lastScanNo + 1);

        merge.Change.Should().Be(ScanChange.Added);
        merge.ScanNo.Should().Be(5);
    }
}
