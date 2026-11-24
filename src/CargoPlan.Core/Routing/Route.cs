using CargoPlan.Core.Models;

namespace CargoPlan.Core.Routing;

/// <summary>
/// One contract a route plans for (T13.6, D125): a role of the loop, kept as the recipe's line shows it, so routes.json
/// reads without the hauling data and a later data release changes no started route. The player finds it at the terminal
/// by title and reward; an accepted scan of its template fills it (<see cref="FilledBy"/>).
/// </summary>
/// <param name="Id">1 to n in the route's order: out first, then the ways back.</param>
/// <param name="Title">As the card shows it: Covalex the template's title, Red Wind the card's form (issue #22).</param>
/// <param name="Reward">The template's, else the last a scan saw (Red Wind), else unknown; never estimated (D118).</param>
/// <param name="Destinations">The places of the recipe's line: the first stops of its pool; the hub for a way back.</param>
/// <param name="AcceptAt">For a way back: the station where it is accepted; null otherwise.</param>
/// <param name="CollectAt">For a way back with several pickups: where it collects the rest; null for a role that is no way back.</param>
/// <param name="FilledBy">The scan key (D101) of the contract that filled it. Filled is what the plan says: a key no
/// mission of the plan has any more (undo, discard) leaves the placeholder open again.</param>
public sealed record Placeholder(
    int Id,
    RecipeKind Kind,
    string TemplateUuid,
    string Title,
    long? Reward,
    IReadOnlyList<HaulLeg> Legs,
    IReadOnlyList<string> Destinations,
    string? AcceptAt = null,
    IReadOnlyList<string>? CollectAt = null,
    string? FilledBy = null)
{
    /// <summary>Every leg at its lower band.</summary>
    public int MinScu => Legs.Sum(l => l.MinScu);

    /// <summary>Every leg at its upper band: the worst case.</summary>
    public int MaxScu => Legs.Sum(l => l.MaxScu);

    /// <summary>The largest crate of its legs.</summary>
    public CrateSize Container => Legs.Max(l => l.Container);

    /// <summary>The placeholder of <paramref name="role"/>; the reward from the template, else from <paramref name="edges"/>.</summary>
    public static Placeholder Of(int id, RecipeRole role, ObservedEdges edges)
    {
        var t = role.Template;
        return new(id, role.Kind, t.Uuid, CardTitle(t), t.Reward ?? edges.RewardOf(t), t.Legs, role.Destinations, role.AcceptAt, role.CollectAt);
    }

    /// <summary>
    /// The title as the terminal shows it: Covalex's equals the data's; Red Wind's card says "Member | Stellar Medium
    /// Haul | to Ruin Station" where the data say "Member Hauler Needed for Medium Shipment" (T13.1, issue #22). The
    /// place after "to" is the instance's, so it is left out.
    /// </summary>
    public static string CardTitle(HaulTemplate template) => template.Contractor switch
    {
        HaulContractor.RedWind => string.Join(' ', [
            template.Rank.ToString(), "|", "Stellar", .. template.Direct ? ["Direct"] : Array.Empty<string>(),
            template.Size == HaulSize.Xs ? "Extra Small" : template.Size.ToString(), "Haul",
        ]),
        _ => template.Title,
    };
}

/// <summary>
/// The route the player flies (T13.6, D125): a loop's stations in order and a placeholder per role, kept in routes.json
/// next to the observations, never in plan.json (D118). What is loaded and delivered is the plan's; the route keeps only
/// which scan filled which placeholder and the contracts accepted beside it.
/// </summary>
/// <param name="Name">The loop's row as the Plan tab showed it ("Everus ring").</param>
/// <param name="Hub">The loop's start, where it ends.</param>
/// <param name="Stations">In the order flown, without the hub.</param>
/// <param name="Extras">Scan keys of accepted contracts no placeholder took (U9.2); they count for the route.</param>
/// <param name="DepartedUtc">When the player left the hub (T13.6 6b); the round's time runs from here.</param>
/// <param name="At">Where the station bar stands: 0 the hub at the start, 1 to n the stations, the hub again last (6b).</param>
public sealed record Route(
    string Name,
    DateTime StartedUtc,
    string Hub,
    LoopKind Kind,
    StarSystem System,
    IReadOnlyList<string> Stations,
    IReadOnlyList<Placeholder> Placeholders,
    IReadOnlyList<string> Extras,
    DateTime? DepartedUtc = null,
    int At = 0)
{
    /// <summary>What leaves the hub.</summary>
    public IEnumerable<Placeholder> Outbound => Placeholders.Where(p => p.Kind != RecipeKind.Return);

    /// <summary>The ways back, accepted along the ring.</summary>
    public IEnumerable<Placeholder> Back => Placeholders.Where(p => p.Kind == RecipeKind.Return);

    /// <summary>The known rewards of <see cref="Outbound"/>.</summary>
    public long OutboundReward => Outbound.Sum(p => p.Reward ?? 0);

    /// <summary>The known rewards of <see cref="Back"/>.</summary>
    public long BackReward => Back.Sum(p => p.Reward ?? 0);

    /// <summary>
    /// The route of <paramref name="loop"/> (D124 gives the stations and roles): its stations as flown, a placeholder per
    /// contract in the loop's order. The source is always a loop, also from the recipe view: the recipe has no order.
    /// </summary>
    public static Route From(LoopCandidate loop, string name, ObservedEdges edges, DateTime now) => new(
        name, now, loop.Start, loop.Kind, loop.System, loop.Stations,
        [.. loop.Contracts.Select((role, i) => Placeholder.Of(i + 1, role, edges))], []);
}
