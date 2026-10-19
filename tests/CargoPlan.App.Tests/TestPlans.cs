using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>
/// The split-and-shared missions planned for the Hull A: four stops, Riker split over racks 1+2 and sharing rack 2
/// with Shubin. Every call plans a fresh copy, so tests share no state.
/// </summary>
internal static class TestPlans
{
    private const string ResourceName = "missions/split_and_shared.json";

    public static SavedPlan SplitAndShared()
    {
        using var stream = typeof(TestPlans).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Test resource '{ResourceName}' not found.");

        var missions = MissionFileReader.Read(stream, ResourceName);
        return new SavedPlan(missions, LoadPlanner.Plan(ShipLoader.Load("hull_a"), missions.ToStops()));
    }

    public static (Ship Ship, LoadPlan Plan) Load() => (ShipLoader.Load("hull_a"), SplitAndShared().Plan);
}
