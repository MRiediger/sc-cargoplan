using System.Text;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Import.ContractTracker;

namespace CargoPlan.Import.Tests;

/// <summary>One place, one stop (B1): names that differ by " Station" or by letters OCR confuses are the same stop.</summary>
public class DestinationNamesTests
{
    private const string Short = "Thundering Express";
    private const string Full = "Thundering Express Station";

    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    // --- Key, Comparer, Preferred ---

    [Theory]
    [InlineData("Thundering Express", "Thundering Express Station")]
    [InlineData("Melodic Fields", "Melodic Fields Station")]
    [InlineData("Melodic Fieids", "Melodic Fields Station")]
    [InlineData("thundering express station", "Thundering Express Station")]
    [InlineData("Thundering Express Stati0n", "Thundering Express")]
    [InlineData("Melodic  Fields Statlon", "Melodic Fields")]
    public void Names_of_one_place_are_equal(string first, string second) =>
        DestinationNames.Comparer.Equals(first, second).Should().BeTrue();

    [Theory]
    [InlineData("Thundering Express Station", "Thunderhead Station")]
    [InlineData("Faithful Dream Station", "Melodic Fields Station")]
    [InlineData("Everus Harborstation", "Everus Harbor")]
    [InlineData("Station", "")]
    public void Names_of_different_places_are_not(string first, string second) =>
        DestinationNames.Comparer.Equals(first, second).Should().BeFalse();

    [Fact]
    public void Equal_names_have_equal_hash_codes() =>
        DestinationNames.Comparer.GetHashCode("Melodic Fieids").Should().Be(DestinationNames.Comparer.GetHashCode("Melodic Fields Station"));

    [Theory]
    [InlineData(Short, Full, Full)]
    [InlineData(Full, Short, Full)]
    [InlineData("Melodic Fieids Station", "Melodic Fields Station", "Melodic Fieids Station")]
    public void The_longer_name_wins_else_the_first(string first, string second, string expected) =>
        DestinationNames.Preferred(first, second).Should().Be(expected);

    // --- Unify ---

    [Fact]
    public void Loading_the_plan_of_23_october_merges_the_misread_stop()
    {
        var saved = HurstonPlan();

        var unified = DestinationNames.Unify(saved, HullA, []);

        unified.Merged.Should().Equal(("Melodic Fieids", "Melodic Fields Station"));
        unified.Saved.Stops.Select(s => s.Destination).Should().BeEquivalentTo(
            "Melodic Fields Station", "Faithful Dream Station", Short, "Everus Harbor");
        unified.Saved.Plan.Crates.Select(c => (c.RackId, c.Position, c.Delivered, c.Mission))
            .Should().Equal(saved.Plan.Crates.Select(c => (c.RackId, c.Position, c.Delivered, c.Mission)));
    }

    [Fact]
    public void Sync_of_the_same_capture_after_unify_adds_and_removes_no_mission()
    {
        // K2: the plan of 23 October and capture 9–12 as the reader gives it now ("Melodic Fields Station"). The capture is
        // below the watermark, so it never comes in again, and the duplicate key sees one name for the place.
        var saved = HurstonPlan();
        var captures = CapturesOf(9, 10, 11, 12);

        var unified = DestinationNames.Unify(saved, HullA, captures);
        var applied = ContractSync.Apply(unified.Saved, HullA, unified.Captures, new HashSet<long>([9, 10, 11, 12, 17, 18]));

        applied.Missions.Select(m => (m.Id, m.Contract!.ContractIds.Count)).Should().Equal(saved.Missions.Select(m => (m.Id, m.Contract!.ContractIds.Count)));
        applied.Watermark.Should().Be(saved.Watermark);
        applied.Plan.Crates.Select(c => (c.RackId, c.Position, c.Delivered, c.Mission, c.Commodity, c.CrateSize))
            .Should().Equal(saved.Plan.Crates.Select(c => (c.RackId, c.Position, c.Delivered, c.Mission, c.Commodity, c.CrateSize)));
        // The capture spells both places in full now, so both of the plan's short names go.
        unified.Merged.Should().BeEquivalentTo([("Melodic Fieids", "Melodic Fields Station"), (Short, Full)]);
        applied.Plan.Crates.Select(c => c.Destination).Distinct().Should().BeEquivalentTo(
            "Melodic Fields Station", "Faithful Dream Station", Full, "Everus Harbor");
        ContractSync.Duplicates(applied).Should().BeEmpty();
    }

    [Fact]
    public void A_capture_with_the_longer_name_renames_the_stop_and_joins_its_rack()
    {
        var plan = LoadPlanner.PlaceInto(new SavedPlan([], new LoadPlan(HullA.Id, [], [])), HullA, [Stop(Short, "Pressurized Ice", 4)]);
        ContractCapture[] captures = [new([Stop(Full, "Stims", 8)], new ContractSource([60]))];

        var unified = DestinationNames.Unify(plan, HullA, captures);
        var applied = ContractSync.Apply(unified.Saved, HullA, unified.Captures, null);

        unified.Merged.Should().Equal((Short, Full));
        applied.Stops.Should().ContainSingle().Which.Destination.Should().Be(Full);
        applied.Plan.Crates.Select(c => c.RackId).Distinct().Should().Equal("rack-0");
    }

    [Fact]
    public void A_capture_with_the_shorter_name_takes_the_plan_spelling()
    {
        var plan = LoadPlanner.PlaceInto(new SavedPlan([], new LoadPlan(HullA.Id, [], [])), HullA, [Stop(Full, "Stims", 4)]);
        ContractCapture[] captures = [new([Stop(Short, "Pressurized Ice", 4)], new ContractSource([60]))];

        var unified = DestinationNames.Unify(plan, HullA, captures);

        unified.Saved.Should().BeSameAs(plan);
        unified.Merged.Should().BeEmpty();
        unified.Captures.Should().ContainSingle().Which.Stops.Should().ContainSingle().Which.Destination.Should().Be(Full);
    }

    [Fact]
    public void Nothing_to_unify_returns_the_same_plan_and_captures()
    {
        // D38: diff, don't react – the sync compares by reference.
        var plan = LoadPlanner.PlaceInto(new SavedPlan([], new LoadPlan(HullA.Id, [], [])), HullA, [Stop("Riker", "Gold", 4)]);
        ContractCapture[] captures = [new([Stop("Shubin", "Gold", 4)], new ContractSource([60]))];

        var unified = DestinationNames.Unify(plan, HullA, captures);

        unified.Saved.Should().BeSameAs(plan);
        unified.Captures.Should().BeSameAs(captures);
        unified.Merged.Should().BeEmpty();
    }

    private static MissionStop Stop(string destination, string commodity, int scu) =>
        new(destination, 1, [new MissionItem(commodity, scu, CrateSize.Four)]);

    private static SavedPlan HurstonPlan()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ImportFixtures.Plan(ImportFixtures.HurstonPlan)));
        return PlanFile.Read(stream, ImportFixtures.HurstonPlan, HullA);
    }

    /// <summary>Capture 9–12 read from a database as ContractTracker wrote it.</summary>
    private static IReadOnlyList<ContractCapture> CapturesOf(params long[] ids)
    {
        using var database = new TempContractDatabase();
        var raw = ImportFixtures.RawText(ImportFixtures.HurstonAnyOrder);
        foreach (var id in ids)
        {
            database.AddContract(id, "Pressurized Ice", raw, (3, "Thundering Express"));
        }

        return [.. ContractTrackerReader.ReadSince(database.Path, 0).Candidates.Select(c => c.ToCapture())];
    }
}
