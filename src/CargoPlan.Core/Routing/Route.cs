using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

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

    /// <summary>
    /// Whether <paramref name="placeholder"/> is filled: its scan key is a mission's of the plan. The plan is the truth
    /// about the cargo (D125): an undo or a discard opens the placeholder again, routes.json keeps the key.
    /// </summary>
    /// <param name="keysInPlan">The scan keys of the plan's missions.</param>
    public static bool IsFilled(Placeholder placeholder, IReadOnlySet<string> keysInPlan) =>
        placeholder.FilledBy is { } key && keysInPlan.Contains(key);

    /// <summary>How many placeholders are filled (<see cref="IsFilled"/>).</summary>
    public int FilledCount(IReadOnlySet<string> keysInPlan) => Placeholders.Count(p => IsFilled(p, keysInPlan));

    /// <summary>The first open placeholder of the template <paramref name="match"/> names (one of the candidates if
    /// ambiguous, U5); null for none or no match. Also the offer scan's hint at the terminal (E(a) of T13.6).</summary>
    public Placeholder? OpenFor(MatchResult? match, IReadOnlySet<string> keysInPlan)
    {
        IReadOnlyList<string> uuids = match switch
        {
            MatchResult.Matched m => [m.Template.Uuid],
            MatchResult.Ambiguous a => [.. a.Candidates.Select(t => t.Uuid)],
            _ => [],
        };
        return Placeholders.FirstOrDefault(p => uuids.Contains(p.TemplateUuid) && !IsFilled(p, keysInPlan));
    }

    /// <summary>
    /// What a scan does to the route (U3, U9.2, D125). Only an accepted contract the plan took fills: an estimate (an
    /// offer, a view not told apart, D102) or a refused update leaves the route as it is. Then, in turn: a placeholder
    /// filled by this key already is <see cref="FillOutcome.AlreadyFilled"/>; the first open placeholder of the matched
    /// template takes the key (a key of the extras moves to it); a key among the extras is already there; anything else
    /// is an extra. <see cref="ScanChange.AlreadyInPlan"/> fills too (K3 of T13.6): the scan key (D101) has no title,
    /// so a contract scanned before the route started can only be placed by scanning it again.
    /// </summary>
    /// <param name="match">The scan's template (<see cref="TemplateMatch.Find"/>); null where the scan was no observation.</param>
    /// <param name="scan">The scan's identity as read, not the mission's: an offer after the accepted contract is an estimate.</param>
    /// <param name="keysInPlan">The scan keys of the plan after the scan.</param>
    public static (Route Route, FillOutcome Outcome) Fill(Route route, MatchResult? match, ScanIdentity scan, ScanChange change, IReadOnlySet<string> keysInPlan)
    {
        if (scan.QuantitiesEstimated || change == ScanChange.Refused)
        {
            return (route, new FillOutcome.NotAccepted());
        }

        if (route.Placeholders.FirstOrDefault(p => p.FilledBy == scan.Key) is { } filled)
        {
            return (route, new FillOutcome.AlreadyFilled(filled));
        }

        if (route.OpenFor(match, keysInPlan) is { } open)
        {
            var now = open with { FilledBy = scan.Key };
            return (route with
            {
                Placeholders = [.. route.Placeholders.Select(p => p.Id == open.Id ? now : p)],
                Extras = [.. route.Extras.Where(k => k != scan.Key)],
            }, new FillOutcome.Filled(now));
        }

        return route.Extras.Contains(scan.Key)
            ? (route, new FillOutcome.AlreadyFilled(null))
            : (route with { Extras = [.. route.Extras, scan.Key] }, new FillOutcome.Extra());
    }
}

/// <summary>What a scan did to the route (<see cref="Route.Fill"/>).</summary>
public abstract record FillOutcome
{
    private FillOutcome()
    {
    }

    /// <summary>The scan filled <paramref name="Placeholder"/>.</summary>
    public sealed record Filled(Placeholder Placeholder) : FillOutcome;

    /// <summary>The scan's contract is in the route already: in <paramref name="Placeholder"/>, or among the extras (null).</summary>
    public sealed record AlreadyFilled(Placeholder? Placeholder) : FillOutcome;

    /// <summary>An accepted contract no placeholder took: it counts for the route (U9.2).</summary>
    public sealed record Extra : FillOutcome;

    /// <summary>An estimate or a refused update: the route is as it was.</summary>
    public sealed record NotAccepted : FillOutcome;
}
