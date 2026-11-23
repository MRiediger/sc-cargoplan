namespace CargoPlan.Core.Routing;

/// <summary>
/// The hub of a local Covalex board (D121): in the local stellar blocks the game data leave the hub's side of a pool
/// empty (a single-to-multi from the hub has no pickup, a multi-to-single to it no drop-off). The board stands at the
/// planet's station, where the scans saw these contracts start (Everus → HUR-L3, design §8.3). Not in the game data: a
/// cell that uses it says so. Planetary and local templates get no hub, there is no evidence where their board stands.
/// </summary>
public static class BoardHubs
{
    /// <summary><c>areaKey</c> → the hub's starmap name; a data test checks each is a position of Stanton.</summary>
    public static IReadOnlyDictionary<string, string> ByArea { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Stanton1"] = "Everus Harbor",
        ["Stanton2"] = "Seraphim Station",
        ["Stanton3"] = "Baijini Point",
        ["Stanton4"] = "Port Tressler",
    };

    /// <summary>The hub that stands in for an empty pool side of <paramref name="template"/>, or null.</summary>
    public static string? HubOf(HaulTemplate template) =>
        template is { Contractor: HaulContractor.Covalex, MissionType: HaulMissionType.Stellar, System: StarSystem.Stanton }
            ? ByArea.GetValueOrDefault(template.AreaKey)
            : null;

    /// <summary>The pickups as the matrix shows them: the pool, or the hub when the pool is empty and there is one.</summary>
    public static (IReadOnlyList<string> Places, bool FromHub) Pickups(HaulTemplate template) => Side(template, template.PickupPool);

    /// <summary>The drop-offs as the matrix shows them: the pool, or the hub when the pool is empty and there is one.</summary>
    public static (IReadOnlyList<string> Places, bool FromHub) Dropoffs(HaulTemplate template) => Side(template, template.DropoffPool);

    private static (IReadOnlyList<string> Places, bool FromHub) Side(HaulTemplate template, IReadOnlyList<string> pool) =>
        pool.Count > 0 ? (pool, false) : HubOf(template) is { } hub ? ([hub], true) : ([], false);
}
