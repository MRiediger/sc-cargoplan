using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Routing;

/// <summary>Where one contract of a plan on the map is accepted and collected, and where it goes.</summary>
/// <param name="AcceptAt">Null: at the plan's start.</param>
public sealed record MapContract(string? AcceptAt, IReadOnlyList<string> CollectAt, IReadOnlyList<string> Destinations);

/// <summary>
/// A plan as the map draws it (gate finding 7a/7b, D128 addendum): the active route or the custom route being clicked,
/// its start, its stations in the order flown and its contracts' places.
/// </summary>
public sealed record MapPlan(string Start, IReadOnlyList<string> Stations, IReadOnlyList<MapContract> Contracts)
{
    public static MapPlan Of(Route route) =>
        new(route.Hub, route.Stations, [.. route.Placeholders.Select(p => new MapContract(p.AcceptAt, p.CollectAt ?? [], p.Destinations))]);

    public static MapPlan Of(LoopCandidate loop) =>
        new(loop.Start, loop.Stations, [.. loop.Contracts.Select(r => new MapContract(r.AcceptAt, r.CollectAt ?? [], r.Destinations))]);
}

/// <summary>One pickup to one drop-off of an extra of the active route: an accepted contract no placeholder took (U9.2).</summary>
/// <param name="ScanNo">The mission's scan number (#n), as the route's block names it.</param>
public sealed record MapExtra(string Pickup, string Dropoff, int ScanNo, long? Reward)
{
    /// <summary>
    /// The extras of <paramref name="route"/> as <paramref name="plan"/> holds them: each item's pickups to its stop, the
    /// places resolved in the route's system (B1); a place it can't resolve or an item without pickup draws nothing.
    /// </summary>
    public static IReadOnlyList<MapExtra> Of(Route route, SavedPlan? plan, Positions positions)
    {
        if (plan is null || route.Extras.Count == 0)
        {
            return [];
        }

        HashSet<string> extras = [.. route.Extras];
        string? Name(string place) => positions.Resolve(route.System, place)?.Name;
        return
        [
            .. plan.Missions.Where(m => m.Contract is { } c && extras.Contains(c.Scan.Key)).SelectMany(m =>
                m.Stops.SelectMany(s => s.Items.SelectMany(i => i.Pickup ?? []).Select(p => (Pickup: Name(p), Dropoff: Name(s.Destination))))
                    .Where(x => x.Pickup is not null && x.Dropoff is not null)
                    .Distinct()
                    .Select(x => new MapExtra(x.Pickup!, x.Dropoff!, m.Contract!.Scan.No, m.Contract.Reward))),
        ];
    }
}

/// <summary>A plan's lines on the map, as nodes: its legs in the order flown, its stations' numbers, its contracts' edges.</summary>
/// <param name="Legs">Start → stations → start.</param>
/// <param name="Numbers">"1", "2, 4" where a node is visited twice; the start has none.</param>
/// <param name="Edges">Each contract's pickups (where it is accepted, else the start, and where it collects) to its destinations.</param>
public sealed record MapPlanLines(
    IReadOnlyList<(string From, string To)> Legs, IReadOnlyDictionary<string, string> Numbers, IReadOnlyList<(string From, string To)> Edges)
{
    public static MapPlanLines None { get; } = new([], new Dictionary<string, string>(), []);

    /// <summary>The lines of <paramref name="plan"/>, its places on their nodes; a place without node is left out.</summary>
    public static MapPlanLines Of(MapPlan plan, IReadOnlyDictionary<string, MapNode> nodeOf)
    {
        string? Node(string place) => nodeOf.GetValueOrDefault(place)?.Key;
        string[] stops = [.. new[] { plan.Start }.Concat(plan.Stations).Append(plan.Start).Select(Node).OfType<string>()];
        var numbers = plan.Stations.Select((s, i) => (Node: Node(s), Number: i + 1))
            .Where(x => x.Node is not null && x.Node != Node(plan.Start))
            .GroupBy(x => x.Node!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Number)), StringComparer.Ordinal);
        (string, string)[] edges =
        [
            .. plan.Contracts.SelectMany(c => new[] { c.AcceptAt ?? plan.Start }.Concat(c.CollectAt)
                    .SelectMany(from => c.Destinations.Select(to => (From: Node(from), To: Node(to)))))
                .Where(e => e.From is not null && e.To is not null && e.From != e.To)
                .Select(e => (e.From!, e.To!))
                .Distinct(),
        ];
        return new([.. stops.Zip(stops.Skip(1)).Where(l => l.First != l.Second)], numbers, edges);
    }
}

/// <summary>The extras of the active route between two nodes that no contract of the route joins.</summary>
public sealed record MapExtraEdge(string From, string To, IReadOnlyList<MapExtra> Extras);
