using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.App;

/// <summary>
/// Placeholder content until the mission editor (T3.8): the example missions embedded in this assembly,
/// planned for the Hull A.
/// </summary>
public static class SamplePlan
{
    public const string ShipId = "hull_a";

    private const string ResourceName = "missions/example_missions.json";

    public static (Ship Ship, LoadPlan Plan) Load()
    {
        using var stream = typeof(SamplePlan).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        var stops = MissionFileReader.Read(stream, ResourceName);
        var ship = ShipLoader.Load(ShipId);
        return (ship, LoadPlanner.Plan(ship, stops));
    }
}
