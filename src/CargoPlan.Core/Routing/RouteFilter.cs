namespace CargoPlan.Core.Routing;

/// <summary>Whether direct contracts show: all, only them, or none of them.</summary>
public enum DirectFilter
{
    All,
    Only,
    Without,
}

/// <summary>
/// Which templates the planner shows (T13.2): one filter model for the matrix and later the map (T13.7), as design §8.2
/// names it. Pure data; the app keeps it per session, rank and faction in settings.json.
/// </summary>
/// <param name="MaxRank">Every rank up to this one, as a board shows them (design §1.2).</param>
/// <param name="Sizes">The sizes shown; empty shows none.</param>
/// <param name="Contractor">null = both.</param>
/// <param name="Faction">The player's side in Pyro; narrows Red Wind only, Covalex has none. null = both.</param>
/// <param name="MissionType">null = every area of operations.</param>
/// <param name="System">The board's system; null = the templates without one (interstellar).</param>
public sealed record RouteFilter(
    HaulRank MaxRank,
    IReadOnlySet<HaulSize> Sizes,
    DirectFilter Direct,
    HaulContractor? Contractor,
    HaulFaction? Faction,
    HaulMissionType? MissionType,
    StarSystem? System)
{
    /// <summary>Stanton, everything in it: the app knows no location yet (T13.2).</summary>
    public static RouteFilter Default { get; } = new(
        HaulRank.Master, new HashSet<HaulSize>(Enum.GetValues<HaulSize>()), DirectFilter.All, null, null, null, StarSystem.Stanton);

    public bool Matches(HaulTemplate template) =>
        template.Rank <= MaxRank
        && Sizes.Contains(template.Size)
        && Direct switch
        {
            DirectFilter.Only => template.Direct,
            DirectFilter.Without => !template.Direct,
            _ => true,
        }
        && (Contractor is null || template.Contractor == Contractor)
        && (Faction is null || template.Faction is null || template.Faction == Faction)
        && (MissionType is null || template.MissionType == MissionType)
        && template.System == System;
}
