using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Hauling templates built in the test, not read (A10): matrix, board, matching and recipe tests. Moved out of
/// PortMatrixTests with T13.3; every template gets its own uuid.
/// </summary>
internal static class TemplateFixtures
{
    private static int _uuid;

    /// <param name="maxScu">The one leg's upper band (Gold, 1 to it, 16s) unless <paramref name="legs"/> are given.</param>
    public static HaulTemplate T(
        string[] pickups,
        string[] dropoffs,
        long? reward = 100_000,
        int maxScu = 64,
        HaulContractor contractor = HaulContractor.Covalex,
        HaulRank rank = HaulRank.Senior,
        HaulSize size = HaulSize.Medium,
        bool direct = false,
        StarSystem? system = StarSystem.Stanton,
        string areaKey = "Stanton1",
        HaulMissionType type = HaulMissionType.Stellar,
        HaulFaction? faction = null,
        HaulShape shape = HaulShape.AToB,
        int stops = 1,
        HaulLeg[]? legs = null,
        string title = "title") => new(
        $"t{Interlocked.Increment(ref _uuid):D4}", "debug", contractor, title, rank, size, direct, shape, stops, system, areaKey, type, faction,
        reward, legs ?? [new HaulLeg("Gold", 1, maxScu, CrateSize.Sixteen)], pickups, dropoffs);

    /// <summary>A leg of <paramref name="commodity"/>, <paramref name="min"/> to <paramref name="max"/> SCU in crates of <paramref name="crate"/>.</summary>
    public static HaulLeg Leg(string commodity, int min, int max, CrateSize crate = CrateSize.Sixteen) => new(commodity, min, max, crate);
}
