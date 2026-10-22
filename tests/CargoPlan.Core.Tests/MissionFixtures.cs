using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Mission files embedded in this test assembly (Fixtures/*.json). Every access parses a fresh copy,
/// so tests share no state and need no ClassFixture.
/// </summary>
internal static class MissionFixtures
{
    /// <summary>3 stops, 30 SCU: Riker 12 Titanium (4), Shubin 10 Quartz (2), Orison 8 Agricium (8).</summary>
    public static IReadOnlyList<Stop> ExampleMissions => Load("example_missions");

    /// <summary>3 stops, 72 SCU: more than a Hull A holds.</summary>
    public static IReadOnlyList<Stop> OverflowMissions => Load("overflow_missions");

    /// <summary>1 stop, 20 SCU Titanium in 4 SCU crates: more than one 16 SCU rack.</summary>
    public static IReadOnlyList<Stop> SplitRequired => Load("split_required");

    /// <summary>4 stops, 54 SCU: Riker (20) splits over racks 1+2 and shares rack 2 with Shubin.</summary>
    public static IReadOnlyList<Stop> SplitAndShared => Load("split_and_shared");

    /// <summary>The same route as entered (schema 1: explicit crates per item).</summary>
    public static IReadOnlyList<MissionStop> SplitAndSharedMissions => LoadMissions("split_and_shared");

    /// <summary>
    /// Schema 2: Green Glade 6 Stims and 15 Scrap, max 4, derived (4+2, 4+4+4+2+1); Riker 6 Titanium, max 4,
    /// overridden as 2+2+2.
    /// </summary>
    public static IReadOnlyList<MissionStop> CrateMixMissions => LoadMissions("crate_mix");

    /// <summary>
    /// Schema 2, 8 stops, 58 of 64 SCU in 1–16 SCU crates: Lorville 16 alone, Riker 12, Shubin and Orison share rack 3,
    /// four stops share rack 4 (T7.0 load case).
    /// </summary>
    public static IReadOnlyList<MissionStop> HullAEightStopsMissions => LoadMissions("hull_a_eight_stops");

    /// <summary>A fixture expanded for the planner.</summary>
    public static IReadOnlyList<Stop> Load(string name) => LoadMissions(name).ToStops();

    /// <summary>A fixture as entered, for the file format tests.</summary>
    public static IReadOnlyList<MissionStop> LoadMissions(string name)
    {
        var resourceName = ResourceName(name);
        using var stream = Open(resourceName);
        return MissionFileReader.Read(stream, resourceName);
    }

    public static string ResourceName(string name) => $"missions/{name}.json";

    public static string InvalidResourceName(string name) => $"missions/invalid/{name}.json";

    public static Stream Open(string resourceName) =>
        typeof(MissionFixtures).Assembly.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"Test resource '{resourceName}' not found.");
}
