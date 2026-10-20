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
        return SavedPlan.FromStops(missions, LoadPlanner.Plan(ShipLoader.Load("hull_a"), missions.ToStops()));
    }

    public static (Ship Ship, LoadPlan Plan) Load() => (ShipLoader.Load("hull_a"), SplitAndShared().Plan);

    /// <summary>
    /// The split-and-shared plan with Shubin's 8 SCU crate in the full Rack 1, on top of Riker's crates: what a plan file
    /// edited by hand can hold. The planner and its moves never make it (D24); it loads and shows red.
    /// </summary>
    public static LoadPlan Overloaded(LoadPlan plan) =>
        plan with { Crates = [.. plan.Crates.Select(c => c.Destination == "Shubin" ? c with { RackId = "rack-0" } : c)] };

    /// <summary>A plan of exactly these placements, laid out the way the planner lays out crates (D24).</summary>
    public static LoadPlan Of(Ship ship, params Placement[] placements) =>
        new(ship.Id, CrateLayout.LayOut(ship, placements, FillOrder.BowFirst), []);
}
