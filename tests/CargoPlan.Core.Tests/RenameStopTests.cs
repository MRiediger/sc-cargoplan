using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Renaming a stop (B1): mechanical, the import decides which names are one place. Missions and crates change together,
/// nothing moves.
/// </summary>
public class RenameStopTests
{
    private const string Short = "Thundering Express";
    private const string Full = "Thundering Express Station";

    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static SavedPlan Empty => new([], new LoadPlan(HullA.Id, [], []));

    private static MissionStop Stop(string destination, string commodity, int scu, int order = 1) =>
        new(destination, order, [new MissionItem(commodity, scu, CrateSize.Four)]);

    /// <summary>Two captures, one with the fragment, one with the full name: two stops in two racks.</summary>
    private static SavedPlan TwoCaptures() =>
        LoadPlanner.PlaceInto(LoadPlanner.PlaceInto(Empty, HullA, [Stop(Short, "Pressurized Ice", 4)]), HullA, [Stop(Full, "Stims", 8)]);

    [Fact]
    public void Renaming_across_missions_gives_one_stop_and_moves_no_crate()
    {
        var before = TwoCaptures();

        var after = LoadPlanner.RenameStop(before, HullA, Short, Full);

        after.Plan.Crates.Should().Equal(before.Plan.Crates.Select(c => c with { Destination = Full }));
        after.Missions.SelectMany(m => m.Stops).Select(s => s.Destination).Should().OnlyContain(d => d == Full);
        after.Stops.Should().ContainSingle().Which.Items.Select(i => i.Commodity).Should().Equal("Pressurized Ice", "Stims");
    }

    [Fact]
    public void Renaming_derives_the_hints_anew()
    {
        var after = LoadPlanner.RenameStop(TwoCaptures(), HullA, Short, Full);

        after.Plan.Warnings.Select(w => w.Message).Should().Contain($"{Full} spans racks 1+2");
    }

    [Fact]
    public void A_mission_with_both_names_keeps_one_stop_at_the_first_place()
    {
        // Capture 9–12 before B1: "Melodic Fieids" first, "Melodic Fields Station" last, one mission.
        var before = LoadPlanner.PlaceInto(Empty, HullA,
        [
            Stop("Melodic Fieids", "Pressurized Ice", 4, 1), Stop("Faithful Dream Station", "Processed Food", 4, 2),
            Stop("Melodic Fields Station", "Processed Food", 4, 3),
        ]);

        var after = LoadPlanner.RenameStop(before, HullA, "Melodic Fieids", "Melodic Fields Station");

        var mission = after.Missions.Should().ContainSingle().Subject;
        mission.Stops.Select(s => (s.Destination, s.Order)).Should().Equal(("Melodic Fields Station", 1), ("Faithful Dream Station", 2));
        mission.Stops[0].Items.Select(i => i.Commodity).Should().Equal("Pressurized Ice", "Processed Food");
        after.Plan.Crates.Select(c => (c.RackId, c.Position)).Should().Equal(before.Plan.Crates.Select(c => (c.RackId, c.Position)));
    }

    [Fact]
    public void Ticks_stay_with_their_crates()
    {
        var before = TwoCaptures();
        var ticked = new SavedPlan(before.Missions, LoadPlanner.SetDelivered(before.Plan, HullA, Short, true), before.Watermark);

        var after = LoadPlanner.RenameStop(ticked, HullA, Short, Full);

        after.Plan.Crates.Select(c => c.Delivered).Should().Equal(ticked.Plan.Crates.Select(c => c.Delivered));
    }

    [Theory]
    [InlineData("Riker", Full)]
    [InlineData(Short, Short)]
    public void Unknown_or_same_name_is_a_caller_error(string from, string to)
    {
        var act = () => LoadPlanner.RenameStop(TwoCaptures(), HullA, from, to);

        act.Should().Throw<ArgumentException>();
    }
}
