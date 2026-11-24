namespace CargoPlan.Core.Routing;

/// <summary>
/// The system each contractor hauls in (E3 of the T13.3a approval): the one table for "a scan of this contractor names
/// places of this system". Matching, the observed edges and routes.json resolve a scan's places there. It holds for every
/// template with a system in the pinned data (ships-2026.11.2: Covalex 377 in Stanton, Red Wind 194 in Pyro); the 16
/// interstellar Covalex templates have none. <c>BaselineHaulingDataTests</c> fails on a template of a contractor in
/// another system, a data release that would make this table wrong.
/// </summary>
public static class ContractorSystems
{
    public static IReadOnlyDictionary<HaulContractor, StarSystem> All { get; } = new Dictionary<HaulContractor, StarSystem>
    {
        [HaulContractor.Covalex] = StarSystem.Stanton,
        [HaulContractor.RedWind] = StarSystem.Pyro,
    };

    public static StarSystem Of(HaulContractor contractor) => All[contractor];
}
