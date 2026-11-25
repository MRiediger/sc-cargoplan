using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Routing;

/// <summary>Whether a way back fits at its station (T13.6 6b, D126).</summary>
public enum ReturnFit
{
    /// <summary>Not simulated: only the stop the bar stands at is.</summary>
    Unchecked,

    /// <summary>The planner lays its worst case into the plan as it will be after the station's unloading.</summary>
    Fits,

    /// <summary>It doesn't: a hint, the contract can still be accepted (invariant 4).</summary>
    TooBig,

    /// <summary>An accepted scan filled it.</summary>
    Filled,
}

/// <summary>A way back accepted at a stop.</summary>
/// <param name="ScanNo">The scan number of the mission that filled it; null while open.</param>
public sealed record ReturnAt(Placeholder Placeholder, ReturnFit Fit, int? ScanNo);

/// <summary>
/// One stop of the station bar (K5): index 0 the hub at the start, 1 to n the stations as flown, n + 1 the hub at the end.
/// </summary>
/// <param name="Lines">Legend lines (a mission's crates for one place) unloaded here; the ways back at the end.</param>
/// <param name="LinesDone">Of them ticked off, every crate delivered.</param>
/// <param name="Held">Accepted contracts still held once this stop's lines and the earlier ones are unloaded.</param>
/// <param name="Returns">The ways back accepted here.</param>
/// <param name="Collects">The ways back of earlier stations that collect here.</param>
public sealed record RouteStop(
    int Index, string Name, bool IsHub, int Lines, int LinesDone, int Held, IReadOnlyList<ReturnAt> Returns, IReadOnlyList<Placeholder> Collects)
{
    public int Open => Lines - LinesDone;

    /// <summary>Contracts the player may still accept here ("ACCEPTED (n/10)", <see cref="Recipe.MaxContracts"/>).</summary>
    public int FreeSlots => Recipe.MaxContracts - Held;
}

/// <summary>
/// Where the route stands (T13.6 6b, D126), read from the plan as the block "Plan" is (D125): per stop what it unloads,
/// what is held and free, and the ways back; the stop the bar stands at (<see cref="Route.At"/>) simulates its ways back
/// with the load planner on the real plan. Pure: the bar's moves are <see cref="Route.Step"/> and <see cref="Route.Advance"/>.
/// </summary>
public sealed record RouteProgress(IReadOnlyList<RouteStop> Stops)
{
    /// <summary>Every stop but the start: the stations and the hub at the end.</summary>
    public int Landings => Stops.Count - 1;

    /// <summary>
    /// The stops of <paramref name="route"/> with the plan as it is. The stations are the route's and the places of its
    /// extras off the ring (K6): before leaving, the whole order anew as the loops order it (<see cref="Loops.Order"/>,
    /// the shortest round trip); after leaving, the order flown stays and a new place comes before the hub. A place of
    /// the plan is the station its position names (<see cref="Positions.Resolve"/>: the card writes "Green Glade
    /// Station"), else its name as read.
    /// </summary>
    /// <param name="plan">The overlay's plan; null without one (then nothing is aboard).</param>
    public static RouteProgress Of(Route route, SavedPlan? plan, Ship ship, Positions positions)
    {
        plan = plan is not null && plan.Plan.ShipId == ship.Id ? plan : new SavedPlan([], new LoadPlan(ship.Id, [], []), CrateCap.Of(ship));
        var stations = StationsOf(route, plan, positions);
        string[] names = [route.Hub, .. stations, route.Hub];
        // The hub's lines (the ways back) are the last stop's; a place off the route is no stop's (-1).
        int StopOf(string destination)
        {
            var place = Place(route, destination, positions);
            if (place == route.Hub)
            {
                return names.Length - 1;
            }

            var index = Array.IndexOf(names, place, 1, names.Length - 2);
            return index > 0 ? index : -1;
        }

        var lines = Lines(plan).Select(l => (l.Mission, l.Destination, l.Done, Stop: StopOf(l.Destination))).ToList();
        HashSet<string> keys = [.. plan.Missions.Select(m => m.Contract?.Scan.Key).OfType<string>()];
        var scanNo = plan.Missions.Where(m => m.Contract is not null).ToDictionary(m => m.Contract!.Scan.Key, m => m.Contract!.Scan.No);
        HashSet<int> accepted = [.. plan.Missions.Where(m => m.Contract?.Scan is { QuantitiesEstimated: false }).Select(m => m.Id)];

        var stops = new List<RouteStop>();
        for (var i = 0; i < names.Length; i++)
        {
            var here = lines.Where(l => l.Stop == i).ToList();
            var held = lines.Where(l => accepted.Contains(l.Mission) && !l.Done && (l.Stop < 1 || l.Stop > i)).Select(l => l.Mission).Distinct().Count();
            var station = i > 0 && i < names.Length - 1 ? names[i] : null;
            ReturnAt[] returns =
            [
                .. route.Back.Where(p => station is not null && p.AcceptAt == station).Select(p => Route.IsFilled(p, keys)
                    ? new ReturnAt(p, ReturnFit.Filled, scanNo[p.FilledBy!])
                    : new ReturnAt(p, i == route.At ? Simulate(route, plan, ship, lines.Where(l => l.Stop >= 1 && l.Stop <= i && !l.Done), p) : ReturnFit.Unchecked, null)),
            ];
            Placeholder[] collects = [.. route.Back.Where(p => station is not null && p.CollectAt is { } at && at.Contains(station))];
            stops.Add(new(i, names[i], station is null, here.Count, here.Count(l => l.Done), held, returns, collects));
        }

        return new(stops);
    }

    // The way back's worst case (every leg at its upper band, its crates, to the hub) laid into the plan with every line up
    // to this station unloaded: a hint on the real cargo, not the recipe's empty ship (D126).
    private static ReturnFit Simulate(Route route, SavedPlan plan, Ship ship, IEnumerable<(int Mission, string Destination, bool Done, int Stop)> unload, Placeholder way)
    {
        var after = plan.Plan;
        foreach (var line in unload.Where(l => plan.Plan.Crates.Any(c => c.Mission == l.Mission && c.Destination == l.Destination)))
        {
            after = LoadPlanner.SetDelivered(after, ship, line.Destination, line.Mission, true);
        }

        MissionStop[] worst = [new(route.Hub, 1, [.. way.Legs.Select(l => new MissionItem(l.Commodity, l.MaxScu, l.Container))])];
        var laid = LoadPlanner.PlaceInto(new SavedPlan(plan.Missions, after, plan.CrateCap), ship, worst);
        return laid.Missions[^1].Unplaced ? ReturnFit.TooBig : ReturnFit.Fits;
    }

    // One line per mission and place: done when it has crates and all are delivered; an unplaced mission's never is.
    private static IEnumerable<(int Mission, string Destination, bool Done)> Lines(SavedPlan plan) =>
        plan.Missions.SelectMany(m => m.Stops.Select(s => s.Destination).Distinct(StringComparer.OrdinalIgnoreCase).Select(d =>
        {
            var crates = plan.Plan.Crates.Where(c => c.Mission == m.Id && string.Equals(c.Destination, d, StringComparison.OrdinalIgnoreCase)).ToList();
            return (m.Id, crates.FirstOrDefault()?.Destination ?? d, crates.Count > 0 && crates.All(c => c.Delivered));
        }));

    private static string Place(Route route, string destination, Positions positions) =>
        positions.Resolve(route.System, destination)?.Name ?? destination;

    // K6: the route's stations and the extras' places off the ring.
    private static IReadOnlyList<string> StationsOf(Route route, SavedPlan plan, Positions positions)
    {
        HashSet<string> extras = [.. route.Extras];
        string[] more =
        [
            .. plan.Missions.Where(m => m.Contract is { } c && extras.Contains(c.Scan.Key)).SelectMany(m => m.Stops)
                .Select(s => Place(route, s.Destination, positions))
                .Where(p => p != route.Hub && !route.Stations.Contains(p)).Distinct().Order(PlaceNames.Order),
        ];
        if (more.Length == 0)
        {
            return route.Stations;
        }

        // A place no position knows has no distance: it comes before the hub, as after leaving.
        var known = more.Where(p => positions.Find(route.System, p) is not null).ToList();
        var unknown = more.Except(known).ToList();
        if (route.DepartedUtc is not null || known.Count == 0)
        {
            return [.. route.Stations, .. more];
        }

        HashSet<string> oneStop = [.. route.Outbound.Where(p => p.Kind == RecipeKind.Direct).SelectMany(p => p.Destinations)];
        return [.. Loops.Order(route.System, route.Hub, [.. route.Stations, .. known], positions, oneStop), .. unknown];
    }
}
