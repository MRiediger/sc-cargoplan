using CargoPlan.Core.Models;
using CargoPlan.Core.Text;

namespace CargoPlan.Core.Routing;

/// <summary>
/// The loop ranking (T13.4, D124, design §4.7): "which loop pays best for my ship, here or at another hub?", from the
/// templates (every contract of a board counts as offered, invariant 4) and what the scans saw. Greedy per station as
/// the recipe (D123), the order of the stations by the shortest round trip: no optimum over contracts and order, the
/// player chooses at the terminal. Pure: the same inputs give the same list.
/// </summary>
public static class Loops
{
    /// <summary>A leg longer than this is a long jump (design §4.7), in Gm; shorter legs are part of a landing's time.</summary>
    public const double LongJumpGm = 10;

    /// <summary>
    /// A Stanton ring takes every place of a role's pool up to this many (design §3: five L stations at most), a larger
    /// pool only the places the recipe sends it to (D123: the L stations first).
    /// </summary>
    public const int MaxRingStations = 5;

    // Up to this many stations every order is tried (8! = 40,320); more keep the place order.
    private const int MaxPermuted = 8;

    /// <summary>
    /// Two round trips this close, in Gm, are a tie (design §9.3 gives distances to ±0.1 Gm): a trip and its reverse
    /// always are, and at Seraphim L1 → L4 → L5 and L1 → L5 → L4 differ by less than 0.01 Gm.
    /// </summary>
    public const double TieGm = 0.1;

    /// <summary>
    /// Every loop for <paramref name="ship"/> at <paramref name="rank"/>: per Stanton hub its ring and its direct runs, the
    /// system-wide ring from <paramref name="hub"/>, a ring per Pyro station for <paramref name="faction"/> (null = both
    /// sides, a variant's two sides once). <paramref name="hub"/> is where the player is (routes.hub): the travel to each
    /// loop's start is measured from it, and the system-wide ring starts there.
    /// </summary>
    public static IReadOnlyList<LoopCandidate> For(
        IReadOnlyList<HaulTemplate> templates, Positions positions, ObservedEdges edges, Ship ship, HaulRank rank, HaulFaction? faction, string hub)
    {
        var context = new Context(positions, edges, ship, CrateCap.Of(ship), positions.Find(StarSystem.Stanton, hub));
        List<LoopCandidate> loops = [];
        foreach (var (areaKey, start) in BoardHubs.ByArea.OrderBy(h => h.Key, StringComparer.Ordinal))
        {
            var board = Board.For(templates, StarSystem.Stanton, areaKey, rank, null);
            if (Ring(board, start, context) is { } ring)
            {
                loops.Add(ring);
            }

            loops.AddRange(Directs(board, start, context));
        }

        loops.Add(LeoRing(Board.For(templates, StarSystem.Stanton, BoardHubs.ByArea.Keys.Min(StringComparer.Ordinal)!, rank, null), hub, context));

        var pyro = Board.For(templates, StarSystem.Pyro, Board.PyroSolar, rank, faction);
        pyro = pyro with { Templates = OneSide(pyro.Templates) };
        var starts = pyro.Local.Where(Carried).SelectMany(t => BoardHubs.Pickups(t).Places).Distinct(StringComparer.Ordinal).Order(PlaceNames.Order);
        loops.AddRange(starts.Select(start => Ring(pyro, start, context)).OfType<LoopCandidate>());
        return loops;
    }

    /// <summary>
    /// The loops in the order of <paramref name="sort"/>, best first; those without the figure (no known reward) after
    /// them, those without contracts (a system-wide ring that needs scans) last. Ties by kind, start and stations.
    /// </summary>
    public static IReadOnlyList<LoopCandidate> Rank(IEnumerable<LoopCandidate> loops, LoopSort sort, LoopTimes times) =>
    [
        .. loops
            .OrderBy(l => !l.HasNumbers)
            .ThenByDescending(l => Value(l, sort, times) ?? double.NegativeInfinity)
            .ThenBy(l => l.Kind)
            .ThenBy(l => l.Start, PlaceNames.Order)
            .ThenBy(l => string.Join("|", l.Stations), StringComparer.Ordinal),
    ];

    /// <summary>The figure <paramref name="sort"/> ranks by; null without a reward.</summary>
    public static double? Value(LoopCandidate loop, LoopSort sort, LoopTimes times) => sort switch
    {
        LoopSort.PerHour => loop.PerHour(times),
        LoopSort.PerLoop => loop.Reward,
        LoopSort.PerLanding => loop.PerLanding,
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null),
    };

    /// <summary>
    /// The order of <paramref name="stations"/> from <paramref name="start"/> and back with the shortest sum of distances
    /// (design §4.7). Every trip ties with its reverse: a tie goes to the trip that reaches a station of
    /// <paramref name="oneStop"/> (a contract with one destination, which opens room for the way back) first, then to the
    /// place order. More than <see cref="MaxPermuted"/> stations keep the place order.
    /// </summary>
    internal static IReadOnlyList<string> Order(
        StarSystem system, string start, IReadOnlyList<string> stations, Positions positions, IReadOnlyCollection<string> oneStop)
    {
        string[] ordered = [.. stations.Distinct(StringComparer.Ordinal).Where(s => s != start).Order(PlaceNames.Order)];
        if (ordered.Length > MaxPermuted)
        {
            return ordered;
        }

        List<(double Gm, string[] Trip)> trips = [.. Permutations(ordered).Select(p => (Trip([start, .. p, start], system, positions).Sum(), p))];
        var shortest = trips.Min(t => t.Gm);
        return trips
            .Where(t => t.Gm <= shortest + TieGm)
            .Select(t => t.Trip)
            .OrderBy(trip => FirstIndex(trip, oneStop))
            .ThenBy(trip => trip, Comparer<string[]>.Create(InPlaceOrder))
            .First();
    }

    // A ring from start (a Stanton hub, or a Pyro station): the recipe from there, every station its roles may draw, and
    // the best way back from each; null when nothing from there fits the ship.
    private static LoopCandidate? Ring(Board board, string start, Context context)
    {
        // In Pyro a ring keeps to as many stations as a Stanton one: a contract that would take it further stays.
        var recipe = board.System == StarSystem.Pyro
            ? Recipe.At(board, start, context.Ship, context.Edges, roles => roles.SelectMany(r => Possible(r, start, board.System, context.Positions)).Distinct().Count() <= MaxRingStations)
            : Recipe.At(board, start, context.Ship, context.Edges);
        if (recipe.Outbound.Count == 0)
        {
            return null;
        }

        var places = recipe.Outbound.Select(r => (Role: r, Places: Possible(r, start, board.System, context.Positions))).ToArray();
        HashSet<string> oneStop = [.. places.Where(p => p.Role.Template.Shape != HaulShape.SingleToMulti && p.Places.Count == 1).Select(p => p.Places[0])];
        var stations = Order(board.System, start, [.. places.SelectMany(p => p.Places)], context.Positions, oneStop);
        RecipeRole[] returns = [.. stations.Select(s => Recipe.BestReturn(board, s, start, context.Ship, context.Cap)).OfType<RecipeRole>()];
        return Candidate(LoopKind.LocalRing, board.System, start, stations, [.. recipe.Outbound, .. returns], context);
    }

    // Per direct template from the hub (local, or system-wide with an edge a scan saw from it): to its farthest place and
    // back, with a way back if one fits; runs that differ only in their commodity are one row.
    private static IEnumerable<LoopCandidate> Directs(Board board, string hub, Context context)
    {
        IEnumerable<(HaulTemplate Template, IReadOnlyList<string> Places)> directs =
        [
            .. board.Local.Where(t => t.Direct && BoardHubs.Pickups(t).Places.Contains(hub))
                .Select(t => (t, Seen(t, hub, context.Edges) is { Count: > 0 } seen ? seen : BoardHubs.Dropoffs(t).Places)),
            .. board.Templates.Where(t => t.Direct && t.AreaKey == Board.StantonWide).Select(t => (t, Seen(t, hub, context.Edges))),
        ];

        List<LoopCandidate> runs = [];
        foreach (var (template, places) in directs.Where(d => Carried(d.Template)))
        {
            if (Farthest(board.System, hub, places, context.Positions) is not { } to)
            {
                continue;
            }

            var role = Recipe.Role(template, [to], context.Cap, hub);
            if (!Recipe.Fits(context.Ship, context.Cap, [role]))
            {
                continue;
            }

            var back = Recipe.BestReturn(board, to, hub, context.Ship, context.Cap);
            RecipeRole[] contracts = back is null ? [role] : [role, back];
            runs.Add(Candidate(LoopKind.Direct, board.System, hub, [to], contracts, context));
        }

        return runs
            .GroupBy(r => (r.Contracts[0].Template.Title, r.Reward, r.Stations[0], r.Crates, r.Observed, Back: r.Contracts.Count > 1 ? r.Contracts[1].Template.Uuid : null))
            .Select(g => g.MinBy(r => r.Contracts[0].Template.Uuid, StringComparer.Ordinal)! with { Alike = g.Count() });
    }

    // The four Stanton hubs from the player's, in the shortest order; at each the system-wide contracts a scan saw from
    // there to a hub still ahead, picked as the recipe picks, with what is still aboard (design §8.6). Without an edge
    // from a hub it names the first such hub.
    private static LoopCandidate LeoRing(Board board, string hub, Context context)
    {
        string[] hubs = [.. BoardHubs.ByArea.OrderBy(h => h.Key, StringComparer.Ordinal).Select(h => h.Value)];
        var start = hubs.Contains(hub) ? hub : hubs[0];
        HaulTemplate[] wide = [.. board.Templates.Where(t => t.AreaKey == Board.StantonWide && Carried(t))];
        var edges = context.Edges;

        HashSet<string> oneStop = [.. wide.Where(t => t.Shape != HaulShape.SingleToMulti).SelectMany(t => edges.Of(t)).Where(e => e.From == start).Select(e => e.To)];
        var stations = Order(StarSystem.Stanton, start, [.. hubs], context.Positions, oneStop);
        string[] trip = [start, .. stations];
        HashSet<string> observed = [.. hubs.Where(h => wide.Any(t => edges.Of(t).Any(e => e.From == h && hubs.Contains(e.To))))];

        List<RecipeRole> aboard = [];
        List<RecipeRole> taken = [];
        for (var i = 0; i < trip.Length; i++)
        {
            HashSet<string> ahead = [.. trip.Skip(i + 1), start];
            aboard = [.. aboard.Select(r => Ahead(r, ahead)).OfType<RecipeRole>()];
            RecipeRole[] candidates =
            [
                .. wide.Select(t => Recipe.Role(t, [.. edges.Of(t).Where(e => e.From == trip[i] && ahead.Contains(e.To)).Select(e => e.To)], context.Cap, trip[i]))
                    .Where(r => r.Destinations.Count > 0),
            ];
            var picked = Recipe.Pick(candidates, context.Ship, context.Cap, aboard);
            aboard.AddRange(picked);
            taken.AddRange(picked);
        }

        var loop = Candidate(LoopKind.SystemWide, StarSystem.Stanton, start, stations, taken, context);
        return loop with { Observed = observed.Count, Total = hubs.Length, NeedsScansAt = trip.FirstOrDefault(h => !observed.Contains(h)) };
    }

    // The figures of a loop from its contracts: rewards from the data or a scan, crates at the largest seen, long legs.
    private static LoopCandidate Candidate(LoopKind kind, StarSystem system, string start, IReadOnlyList<string> stations, IReadOnlyList<RecipeRole> contracts, Context context)
    {
        long?[] rewards = [.. contracts.Select(c => c.Template.Reward ?? context.Edges.RewardOf(c.Template))];
        var known = rewards.OfType<long>().ToArray();
        var legs = Trip([start, .. stations, start], system, context.Positions);
        return new(
            kind,
            system,
            start,
            stations,
            contracts,
            known.Length > 0 ? known.Sum() : null,
            known.Length > 0 && known.Length < rewards.Length,
            contracts.Sum(c => Crates(c.Template, context)),
            legs.Count(gm => gm > LongJumpGm),
            Travel(start, system, context),
            contracts.Count(c => context.Edges.ScuOf(c.Template) is not null),
            contracts.Count);
    }

    // Each leg at the largest SCU a scan saw of its commodity, at most its band; else its upper band.
    private static int Crates(HaulTemplate template, Context context)
    {
        var seen = context.Edges.ScuOf(template);
        return template.Legs.Sum(leg =>
        {
            var scu = seen?.FirstOrDefault(b => OcrText.AnchorForm(b.Commodity) == OcrText.AnchorForm(leg.Commodity)) is { } band
                ? Math.Min(leg.MaxScu, band.Max)
                : leg.MaxScu;
            return CrateMix.Decompose(scu, leg.Container, context.Cap).Count;
        });
    }

    private static double? Travel(string start, StarSystem system, Context context) =>
        context.Origin is { } origin && context.Positions.Find(system, start) is { } to ? Positions.DistanceGm(origin, to) : null;

    // The stations a role may take the ring to. Where the data name none (system-wide): where the scans saw it go. In
    // Stanton, a local block's pool is the planet's L stations, and any of them may be drawn (worst case, design §3); a
    // larger pool gives the recipe's places. In Pyro a pool names places all over the system, and a ring of their union
    // would visit every one: the player takes the instances that go near, so the nearest drop-offs, as many as the
    // stops, and for a multi-to-single its other pickups, the nearest too.
    private static IReadOnlyList<string> Possible(RecipeRole role, string start, StarSystem system, Positions positions)
    {
        var template = role.Template;
        string[] pool = [.. BoardHubs.Dropoffs(template).Places.Where(p => p != start)];
        if (pool.Length == 0)
        {
            return role.Destinations;
        }

        if (system != StarSystem.Pyro)
        {
            return pool.Length <= MaxRingStations ? pool : role.Destinations;
        }

        IEnumerable<string> Nearest(IEnumerable<string> places, int count) =>
            places.Where(p => p != start).OrderBy(p => Trip([start, p], system, positions)[0]).ThenBy(p => p, PlaceNames.Order).Take(count);

        return template.Shape switch
        {
            HaulShape.SingleToMulti => [.. Nearest(pool, template.Stops)],
            HaulShape.MultiToSingle => [.. Nearest(BoardHubs.Pickups(template).Places, template.Stops - 1), .. Nearest(pool, 1)],
            _ => [.. Nearest(pool, 1)],
        };
    }

    // The places a scan saw a template go to from hub.
    private static IReadOnlyList<string> Seen(HaulTemplate template, string hub, ObservedEdges edges) =>
        [.. edges.Of(template).Where(e => e.From == hub).Select(e => e.To).Distinct(StringComparer.Ordinal)];

    // The place of places farthest from hub, a tie to the place order: an instance may draw any of them (worst case).
    private static string? Farthest(StarSystem system, string hub, IReadOnlyList<string> places, Positions positions) =>
        places.Where(p => p != hub)
            .OrderByDescending(p => Trip([hub, p], system, positions)[0])
            .ThenBy(p => p, PlaceNames.Order)
            .FirstOrDefault();

    // The role with only its legs still to go to a place of ahead; null when none is left.
    private static RecipeRole? Ahead(RecipeRole role, IReadOnlySet<string> ahead)
    {
        var left = role.Template.Legs.Select((leg, i) => (Leg: leg, To: role.Destinations[i % role.Destinations.Count])).Where(x => ahead.Contains(x.To)).ToArray();
        return left.Length == 0
            ? null
            : role with { Template = role.Template with { Legs = [.. left.Select(x => x.Leg)] }, Destinations = [.. left.Select(x => x.To)] };
    }

    // Never a Large or Bulk size, as the recipe (design §3).
    private static bool Carried(HaulTemplate template) => template.Size is not (HaulSize.Large or HaulSize.Bulk);

    // A variant's two sides (faction twins, D122) once: the scan can't tell them apart, and both would count twice.
    private static IReadOnlyList<HaulTemplate> OneSide(IReadOnlyList<HaulTemplate> templates)
    {
        List<HaulTemplate> kept = [];
        foreach (var template in templates.OrderBy(t => t.Uuid, StringComparer.Ordinal))
        {
            if (!kept.Any(k => ObservedEdges.FactionTwins([k, template])))
            {
                kept.Add(template);
            }
        }

        return kept;
    }

    // The legs of a trip in Gm; a place without a position counts 0.
    private static double[] Trip(IReadOnlyList<string> places, StarSystem system, Positions positions) =>
    [
        .. places.Zip(places.Skip(1)).Select(p =>
            positions.Find(system, p.First) is { } a && positions.Find(system, p.Second) is { } b ? Positions.DistanceGm(a, b) ?? 0 : 0),
    ];

    private static int FirstIndex(string[] trip, IReadOnlyCollection<string> oneStop)
    {
        var index = Array.FindIndex(trip, oneStop.Contains);
        return index < 0 ? trip.Length : index;
    }

    private static int InPlaceOrder(string[] a, string[] b)
    {
        foreach (var (x, y) in a.Zip(b))
        {
            var order = PlaceNames.Order.Compare(x, y);
            if (order != 0)
            {
                return order;
            }
        }

        return a.Length.CompareTo(b.Length);
    }

    private static IEnumerable<string[]> Permutations(string[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (var i = 0; i < items.Length; i++)
        {
            string[] rest = [.. items[..i], .. items[(i + 1)..]];
            foreach (var tail in Permutations(rest))
            {
                yield return [items[i], .. tail];
            }
        }
    }

    private sealed record Context(Positions Positions, ObservedEdges Edges, Ship Ship, CrateCap Cap, StarPosition? Origin);
}
