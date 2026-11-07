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

    /// <summary>FrozenShips/ next to the test assembly (linked from the Core tests): ships that stay put when ships/ changes (D92).</summary>
    public static string TestShipsFolder { get; } = Path.Combine(AppContext.BaseDirectory, "FrozenShips");

    /// <summary>
    /// The Hull A as it was before the game-file racks (D92, session 4a), frozen: hit tests, drags and the scene check fixed
    /// cells and pixels, which a data import must not move. The shipped files are checked by the data tests.
    /// </summary>
    public static Ship HullA() => ShipLoader.Load(TestShipsFolder, "hull_a");

    public static SavedPlan SplitAndShared()
    {
        using var stream = typeof(TestPlans).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Test resource '{ResourceName}' not found.");

        var missions = MissionFileReader.Read(stream, ResourceName).Stops;
        var hullA = HullA();
        return SavedPlan.FromStops(missions, LoadPlanner.Plan(hullA, missions.ToStops()), CrateCap.Of(hullA));
    }

    public static (Ship Ship, LoadPlan Plan) Load() => (HullA(), SplitAndShared().Plan);

    private const string HurstonPickupsName = "plans/hurston_2026-10-25.json";

    /// <summary>
    /// Martin's plan.json of 25 Oct (B6), as saved: missions 1–3 without pickup (mission 3 Waste to Everus Harbor),
    /// mission 4 with ContractTracker's pickup text of id 38 uncleaned. The Import tests' fixture, linked.
    /// </summary>
    public static SavedPlan HurstonPickups()
    {
        using var stream = typeof(TestPlans).Assembly.GetManifestResourceStream(HurstonPickupsName)
            ?? throw new InvalidOperationException($"Test resource '{HurstonPickupsName}' not found.");
        return PlanFile.Read(stream, HurstonPickupsName, HullA());
    }

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
