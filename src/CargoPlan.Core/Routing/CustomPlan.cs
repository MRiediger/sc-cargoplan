using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Routing;

/// <summary>One mission of the custom route (T13.7c, D130): a template on one pair of places of an edge's cell.</summary>
public sealed record CustomPick(string Pickup, string Dropoff, HaulTemplate Template);

/// <summary>A mission as a role of the route, and how it goes along the order (D130).</summary>
/// <param name="OverAt">Where it is taken on and its worst case likely doesn't fit with what is aboard there (the SCU or the
/// planner's first fit); null where it fits.</param>
/// <param name="Behind">A stop of it comes before its pickup in the order (two missions each way between two places): it
/// is never delivered on this route.</param>
public sealed record CustomRole(CustomPick Pick, RecipeRole Role, string? OverAt = null, bool Behind = false)
{
    public bool Fits => OverAt is null && !Behind;
}

/// <summary>What a custom route is worked out with: the map's system and places, what the scans saw, the overlay's ship.</summary>
public sealed record CustomContext(StarSystem System, Positions Positions, ObservedEdges Edges, Ship Ship, FitCache? Fits);

/// <summary>The custom route as a loop (same figures as the ranking's) and its roles as flown.</summary>
/// <param name="PeakScu">The most SCU aboard at once along the order, every leg at its upper band.</param>
/// <param name="PeakAt">Where that is, after taking on; null without a mission.</param>
public sealed record CustomResult(LoopCandidate Loop, IReadOnlyList<CustomRole> Roles, int PeakScu = 0, string? PeakAt = null)
{
    public bool Fits => Roles.All(r => r.Fits);
}

/// <summary>A mission as a row of the map's panel or list would offer it (D130).</summary>
public enum CustomOffer
{
    /// <summary>In the plan already (each template once).</summary>
    InPlan,

    /// <summary>Not on the plan's board: refused for now.</summary>
    DifferentBoard,

    /// <summary>With it the plan still fits.</summary>
    Fits,

    /// <summary>With it the plan likely doesn't fit: it is taken, and marked.</summary>
    LikelyNot,
}

/// <summary>
/// The player's own route from the map (T13.7c, D130; D129 for the rest), pure: missions taken one by one (a panel's or
/// a list's [+]), on one board, <see cref="AreaKey"/>: a Stanton area's Covalex stellar and the system-wide ones, Pyro's
/// Red Wind <c>Solar</c>; any other is "different board". It starts at the board's hub (Stanton), Pyro's boards have none
/// (D121): there at the first mission's pickup. A mission from elsewhere is accepted at its pickup (acceptAt), one only to
/// the start is a way back. The stops are the missions' pickups and destinations; their order the shortest round trip
/// with each pickup before its stops (<see cref="Loops.Order"/>), not the order of the clicks. Along it the SCU aboard
/// and the planner's first fit at each pickup: a mission that likely doesn't fit there is taken and marked, never refused.
/// </summary>
/// <param name="AreaKey">The board: with no mission chosen yet the one of routes.hub (Pyro: <c>Solar</c>).</param>
/// <param name="Picks">In the order taken; each template once.</param>
public sealed record CustomPlan(StarSystem System, string AreaKey, IReadOnlyList<CustomPick> Picks)
{
    public const string DifferentBoard = "different board";

    public static CustomPlan Empty(StarSystem system, string areaKey) => new(system, areaKey, []);

    /// <summary>The board's hub (Stanton); Pyro: the first mission's pickup; null for Pyro without one.</summary>
    public string? Start => System == StarSystem.Stanton
        ? BoardHubs.ByArea.GetValueOrDefault(AreaKey)
        : Picks.FirstOrDefault()?.Pickup;

    /// <summary>The template is on the plan's board (<see cref="Board.On"/>).</summary>
    public bool OnBoard(HaulTemplate template) => Board.On(System, AreaKey, template);

    /// <summary>The template is in the plan, on any pair.</summary>
    public bool Holds(HaulTemplate template) => Picks.Any(p => p.Template.Uuid == template.Uuid);

    /// <summary>With <paramref name="pick"/>; this plan when it holds the template or the template is on another board.</summary>
    public CustomPlan Add(CustomPick pick) => Holds(pick.Template) || !OnBoard(pick.Template) ? this : this with { Picks = [.. Picks, pick] };

    /// <summary>Without <paramref name="pick"/>.</summary>
    public CustomPlan Remove(CustomPick pick) => this with { Picks = [.. Picks.Where(p => p != pick)] };

    /// <summary>Without the last mission on one of the edge's pairs of places (a right click); this plan when it has none.</summary>
    public CustomPlan Remove(MapEdge edge)
    {
        var last = Picks.LastOrDefault(p => On(edge, p));
        return last is null ? this : Remove(last);
    }

    /// <summary>How many missions of the plan go along <paramref name="edge"/> (the tool tip's "k/n in plan").</summary>
    public int Count(MapEdge edge) => Picks.Count(p => On(edge, p));

    /// <summary>The missions of <paramref name="edge"/>: each template on each pair of its cells, best-paying first.</summary>
    public static IReadOnlyList<CustomPick> Missions(MapEdge edge) =>
    [
        .. edge.Parts
            .SelectMany(part => part.Cell.Templates.Select(t => (Pick: new CustomPick(part.Pickup, part.Dropoff, t.Template), t.Reward)))
            .DistinctBy(c => (c.Pick.Pickup, c.Pick.Dropoff, c.Pick.Template.Uuid))
            .OrderByDescending(c => c.Reward ?? 0)
            .ThenBy(c => c.Pick.Template.Uuid, StringComparer.Ordinal)
            .Select(c => c.Pick),
    ];

    /// <summary>What a row offers for <paramref name="pick"/>: in the plan, another board, or whether the plan fits with it.</summary>
    public (CustomOffer Offer, string? At) Offer(CustomPick pick, CustomContext context)
    {
        if (Holds(pick.Template))
        {
            return (CustomOffer.InPlan, null);
        }

        if (!OnBoard(pick.Template))
        {
            return (CustomOffer.DifferentBoard, null);
        }

        var role = Add(pick).Evaluate(context).Roles.Single(r => r.Pick == pick);
        return role.Fits ? (CustomOffer.Fits, null) : (CustomOffer.LikelyNot, role.OverAt ?? role.Pick.Pickup);
    }

    public CustomResult Evaluate(CustomContext context)
    {
        var start = Start ?? "";
        var cap = CrateCap.Of(context.Ship);
        HashSet<string> known = [.. Picks.SelectMany(p => new[] { p.Pickup, p.Dropoff })];
        var roles = Picks.Select(p => (Pick: p, Role: Role(p, start, known, context, cap))).ToList();

        // Each pickup before its stops; the start is first and last anyway.
        string[] stops = [.. roles.SelectMany(r => new[] { Pickup(r.Role, start) }.Concat(r.Role.CollectAt ?? []).Concat(r.Role.Destinations)).Where(p => p != start).Distinct()];
        (string, string)[] before = [.. roles.SelectMany(r => r.Role.Destinations.Select(d => (Pickup(r.Role, start), d)))];
        string[] oneStop = [.. roles.Where(r => r.Role.Destinations.Count == 1).Select(r => r.Role.Destinations[0])];
        var stations = stops.Length == 0 ? [] : Loops.Order(context.System, start, stops, context.Positions, oneStop, before);

        // A multi-stop mission's other stops in the order flown: its legs go out in that order (as D129's click order did).
        int Flown(string place) => stations.ToList().IndexOf(place) is >= 0 and var i ? i : stations.Count;
        roles = [.. roles.Select(r => (r.Pick, Role: r.Role with { Destinations = [r.Role.Destinations[0], .. r.Role.Destinations.Skip(1).OrderBy(Flown)] }))];

        // Stop by stop as flown, each mission taken on at its pickup with what is still aboard there.
        string[] trip = [start, .. stations];
        List<RecipeRole> aboard = [];
        List<CustomRole> done = [];
        var (peak, peakAt) = (0, (string?)null);
        for (var i = 0; i < trip.Length; i++)
        {
            HashSet<string> ahead = [.. trip.Skip(i + 1), start];
            aboard = [.. aboard.Select(r => Loops.Ahead(r, ahead)).OfType<RecipeRole>()];
            foreach (var (pick, role) in roles.Where(r => Pickup(r.Role, start) == trip[i]))
            {
                var behind = role.Destinations.Any(d => !ahead.Contains(d));

                // Martin's R2: the SCU aboard and the planner's first fit; LoadPlanner.Lays asks the SCU against the ship first.
                var fits = Recipe.Fits(context.Ship, cap, [.. aboard, role], context.Fits);
                aboard.Add(role);
                done.Add(new CustomRole(pick, role, fits ? null : trip[i], behind));
            }

            if (Scu(aboard) > peak)
            {
                (peak, peakAt) = (Scu(aboard), trip[i]);
            }
        }

        var loop = Loops.Custom(context.System, start, stations, [.. done.Select(r => r.Role)], context);
        return new CustomResult(loop, done, peak, peakAt);
    }

    private static bool On(MapEdge edge, CustomPick pick) => edge.Parts.Any(part => part.Pickup == pick.Pickup && part.Dropoff == pick.Dropoff);

    private static string Pickup(RecipeRole role, string start) => role.AcceptAt ?? start;

    private static int Scu(IEnumerable<RecipeRole> roles) => roles.Sum(r => r.Template.Legs.Sum(l => l.MaxScu));

    // The role of a pick: its template's kind and crates (Recipe.Role), to the clicked drop-off first, then, for a
    // multi-stop one, the other places it may draw from the pickup (its pool, else what the scans saw), places the plan
    // goes to anyway first, then place order; accepted at its pickup unless that is the start.
    private static RecipeRole Role(CustomPick pick, string start, IReadOnlySet<string> known, CustomContext context, CrateCap cap)
    {
        var template = pick.Template;
        var stops = template.Shape == HaulShape.SingleToMulti ? template.Stops : 1;
        var pool = BoardHubs.Dropoffs(template).Places is { Count: > 0 } places
            ? places
            : [.. context.Edges.Of(template).Where(e => e.From == pick.Pickup).Select(e => e.To)];
        string[] others =
        [
            .. pool.Where(p => p != pick.Pickup && p != pick.Dropoff).Distinct()
                .OrderBy(p => known.Contains(p) ? 0 : 1)
                .ThenBy(p => p, PlaceNames.Order),
        ];
        var role = Recipe.Role(template, [pick.Dropoff], cap, pick.Pickup) with { Destinations = [pick.Dropoff, .. others.Take(stops - 1)] };
        return pick.Pickup == start ? role : Recipe.AcceptedAt(role, pick.Pickup, start);
    }
}
