namespace CargoPlan.Core.Routing;

/// <summary>
/// A hauling board as the terminal shows it (design §7.3, D119, T13.3): every template of one system and area a player of
/// a rank sees. The planner knows no availability: every template of the board counts as offered (invariant 4).
/// </summary>
/// <param name="AreaKey">The area of the board: <c>Stanton1</c>…<c>Stanton4</c> for a Covalex board, <c>Solar</c> for Red Wind.</param>
/// <param name="Hub">The board's hub (D121) for a local Covalex board; null otherwise.</param>
/// <param name="Templates">The local block and the system-wide templates, in the order of the data.</param>
public sealed record Board(StarSystem System, string AreaKey, string? Hub, IReadOnlyList<HaulTemplate> Templates)
{
    /// <summary>The <c>areaKey</c> of Covalex's system-wide Stanton templates, on every Stanton board.</summary>
    public const string StantonWide = "Stanton";

    /// <summary>The <c>areaKey</c> of Red Wind's system-wide Pyro templates; the regions (<c>RegionA</c>…) are not on a board yet.</summary>
    public const string PyroSolar = "Solar";

    /// <summary>The templates of the board's own area, without the system-wide ones (design §1.1: the "local block").</summary>
    public IEnumerable<HaulTemplate> Local => Templates.Where(t => t.AreaKey == AreaKey);

    /// <summary>
    /// The board of <paramref name="areaKey"/> for <paramref name="rank"/>, every rank up to it (design §1.2). Stanton:
    /// Covalex stellar of the area and the system-wide ones. Pyro: Red Wind <c>Solar</c> of the player's
    /// <paramref name="faction"/> (null = both); the region blocks stay off until a scan shows which station has which
    /// region (design §5, §7.6). Any other system or area: an empty board.
    /// </summary>
    public static Board For(IEnumerable<HaulTemplate> templates, StarSystem system, string areaKey, HaulRank rank, HaulFaction? faction) =>
        new(system, areaKey, system == StarSystem.Stanton ? BoardHubs.ByArea.GetValueOrDefault(areaKey) : null,
        [
            .. templates.Where(t => t.Rank <= rank && system switch
            {
                StarSystem.Stanton => IsCovalexStellar(t) && (t.AreaKey == areaKey || t.AreaKey == StantonWide),
                StarSystem.Pyro => areaKey == PyroSolar && IsRedWindSolar(t) && (faction is null || t.Faction == faction),
                _ => false,
            }),
        ]);

    /// <summary>
    /// Every template a scan of <paramref name="contractor"/> may be (T13.3, D122): all boards of its system at once,
    /// every rank and faction, since the scan names its rank and the player may scan at any hub. Covalex: stellar of
    /// Stanton; Red Wind: <c>Solar</c>.
    /// </summary>
    public static IReadOnlyList<HaulTemplate> Matchable(IEnumerable<HaulTemplate> templates, HaulContractor contractor) =>
    [
        .. templates.Where(t => contractor switch
        {
            HaulContractor.Covalex => IsCovalexStellar(t),
            HaulContractor.RedWind => IsRedWindSolar(t),
            _ => false,
        }),
    ];

    private static bool IsCovalexStellar(HaulTemplate t) =>
        t is { Contractor: HaulContractor.Covalex, MissionType: HaulMissionType.Stellar, System: StarSystem.Stanton };

    private static bool IsRedWindSolar(HaulTemplate t) =>
        t is { Contractor: HaulContractor.RedWind, MissionType: HaulMissionType.Stellar, System: StarSystem.Pyro, AreaKey: PyroSolar };
}
