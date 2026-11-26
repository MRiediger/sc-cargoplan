using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Routing;

/// <summary>One contract the player clicked on the map (T13.7b): its template on the pair of places of the edge's cell.</summary>
public sealed record CustomPick(string Pickup, string Dropoff, HaulTemplate Template);

/// <summary>A pick as a role of the route, and whether its worst case lies in the ship with what is aboard there.</summary>
public sealed record CustomRole(CustomPick Pick, RecipeRole Role, bool Fits);

/// <summary>What a custom route is worked out with: the map's system and places, what the scans saw, the overlay's ship.</summary>
public sealed record CustomContext(StarSystem System, Positions Positions, ObservedEdges Edges, Ship Ship, FitCache? Fits);

/// <summary>The custom route as a loop (same figures as the ranking's) and its roles in the order they are accepted.</summary>
/// <param name="Fits">Every role fits where it is accepted.</param>
public sealed record CustomResult(LoopCandidate Loop, IReadOnlyList<CustomRole> Roles)
{
    public bool Fits => Roles.All(r => r.Fits);
}

/// <summary>
/// The player's own route from the map (T13.7b, D129), pure: the picks in click order from <see cref="Start"/>. A click
/// on an edge (<see cref="Add"/>) takes its best-paying template not yet picked that fits where it is accepted with what
/// is aboard there (the first fit of the recipe, <c>Recipe.Fits</c>), else the best-paying, marked too big; a right
/// click (<see cref="Remove"/>) takes the edge's last pick back. <see cref="Evaluate"/>: the stations in the order the
/// clicked edges' ends came, places only a multi-stop contract names before the way home in place order (no
/// permutation, the player chose); a pick from the start leaves the hub, one only to the start is a way back, any other
/// is accepted on the way (acceptAt, D125 addendum).
/// </summary>
public sealed record CustomPlan(string Start, IReadOnlyList<CustomPick> Picks)
{
    public static CustomPlan Empty(string start) => new(start, []);

    /// <summary>The edge's next contract; this plan when the edge has none left.</summary>
    public CustomPlan Add(MapEdge edge, CustomContext context)
    {
        HashSet<string> taken = [.. Picks.Select(p => p.Template.Uuid)];
        CustomPick[] candidates =
        [
            .. edge.Parts
                .SelectMany(part => part.Cell.Templates.Select(t => (Pick: new CustomPick(part.Pickup, part.Dropoff, t.Template), t.Reward)))
                .Where(c => !taken.Contains(c.Pick.Template.Uuid))
                .DistinctBy(c => c.Pick.Template.Uuid)
                .OrderByDescending(c => c.Reward ?? 0)
                .ThenBy(c => c.Pick.Template.Uuid, StringComparer.Ordinal)
                .Select(c => c.Pick),
        ];
        if (candidates.Length == 0)
        {
            return this;
        }

        foreach (var candidate in candidates)
        {
            var tried = this with { Picks = [.. Picks, candidate] };
            if (tried.Evaluate(context).Roles.Single(r => r.Pick == candidate).Fits)
            {
                return tried;
            }
        }

        return this with { Picks = [.. Picks, candidates[0]] };
    }

    /// <summary>Without the last pick on one of the edge's pairs of places; this plan when it has none.</summary>
    public CustomPlan Remove(MapEdge edge)
    {
        var last = Picks.LastOrDefault(p => edge.Parts.Any(part => part.Pickup == p.Pickup && part.Dropoff == p.Dropoff));
        return last is null ? this : this with { Picks = [.. Picks.Where(p => p != last)] };
    }

    public CustomResult Evaluate(CustomContext context)
    {
        var cap = CrateCap.Of(context.Ship);

        // The clicked ends in turn, the start left out.
        List<string> stations = [];
        void Visit(string place)
        {
            if (place != Start && !stations.Contains(place))
            {
                stations.Add(place);
            }
        }

        foreach (var pick in Picks)
        {
            Visit(pick.Pickup);
            Visit(pick.Dropoff);
        }

        var roles = Picks.Select(p => (Pick: p, Role: Role(p, stations, context, cap))).ToList();
        foreach (var place in roles.SelectMany(r => r.Role.Destinations).Where(d => d != Start && !stations.Contains(d)).Distinct().Order(PlaceNames.Order).ToList())
        {
            stations.Add(place);
        }

        // Station by station as flown, the picks there in click order, each checked with what is still aboard.
        string[] trip = [Start, .. stations];
        List<RecipeRole> aboard = [];
        List<CustomRole> done = [];
        for (var i = 0; i < trip.Length; i++)
        {
            HashSet<string> ahead = [.. trip.Skip(i + 1), Start];
            aboard = [.. aboard.Select(r => Loops.Ahead(r, ahead)).OfType<RecipeRole>()];
            foreach (var (pick, role) in roles.Where(r => r.Pick.Pickup == trip[i]))
            {
                var here = i == 0 ? role : Recipe.AcceptedAt(role, trip[i], Start);
                var fits = Recipe.Fits(context.Ship, cap, [.. aboard, here], context.Fits);
                aboard.Add(here);
                done.Add(new CustomRole(pick, here, fits));
            }
        }

        var loop = Loops.Custom(context.System, Start, stations, [.. done.Select(r => r.Role)], context);
        return new CustomResult(loop, done);
    }

    // The role of a pick: its template's kind and crates (Recipe.Role), to the clicked drop-off first, then, for a
    // multi-stop one, the other places it may draw from the pickup (its pool, else what the scans saw), the route's
    // stations first in their order, then place order.
    private RecipeRole Role(CustomPick pick, IReadOnlyList<string> stations, CustomContext context, CrateCap cap)
    {
        var template = pick.Template;
        var stops = template.Shape == HaulShape.SingleToMulti ? template.Stops : 1;
        var pool = BoardHubs.Dropoffs(template).Places is { Count: > 0 } places
            ? places
            : [.. context.Edges.Of(template).Where(e => e.From == pick.Pickup).Select(e => e.To)];
        string[] others =
        [
            .. pool.Where(p => p != pick.Pickup && p != pick.Dropoff).Distinct()
                .OrderBy(p => stations.Contains(p) ? 0 : 1)
                .ThenBy(p => stations.Contains(p) ? stations.ToList().IndexOf(p) : 0)
                .ThenBy(p => p, PlaceNames.Order),
        ];
        return Recipe.Role(template, [pick.Dropoff], cap, pick.Pickup) with { Destinations = [pick.Dropoff, .. others.Take(stops - 1)] };
    }
}
