using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.Core.Routing;

/// <summary>What a role does in a recipe (design §3): the bulk Medium, the direct with one stop, a small, the way back.</summary>
public enum RecipeKind
{
    Bulk,
    Direct,
    Small,
    Return,
}

/// <summary>
/// One contract of a recipe: its template, where its worst case goes, and how much of the ship that takes.
/// </summary>
/// <param name="Destinations">The places the role's legs go to: the first stops of its pool (or of the drop-offs the scans
/// saw) in <see cref="PlaceNames.Order"/>; the hub for a way back.</param>
/// <param name="WorstScu">Every leg at its upper band.</param>
/// <param name="Crates">The crates of <paramref name="WorstScu"/> as the elevator delivers them to this ship (D70).</param>
/// <param name="AcceptAt">For a way back: the ring station where it is accepted; null otherwise.</param>
public sealed record RecipeRole(RecipeKind Kind, HaulTemplate Template, IReadOnlyList<string> Destinations, int WorstScu, int Crates, string? AcceptAt = null);

/// <summary>
/// The recipe of a hub's board for a ship (T13.3b, D123, design §3), a hint, never a choice for the player: what leaves the
/// hub (<see cref="Outbound"/>, checked together by the load planner) and the best way back per station of the ring
/// (<see cref="Returns"/>, each alone in the empty ship; slots along the ring are T13.6).
/// </summary>
/// <param name="Note">Why there is none or what is missing; null for a recipe.</param>
public sealed record Recipe(string? Hub, IReadOnlyList<RecipeRole> Outbound, IReadOnlyList<RecipeRole> Returns, string? Note = null)
{
    /// <summary>Contracts one player holds at once (design §3, §4.1: "ACCEPTED (n/10)").</summary>
    public const int MaxContracts = 10;

    /// <summary>The known rewards of what leaves the hub.</summary>
    public long RewardSum => Outbound.Sum(r => r.Template.Reward ?? 0);

    public int WorstScu => Outbound.Sum(r => r.WorstScu);

    public int Crates => Outbound.Sum(r => r.Crates);

    /// <summary>
    /// The recipe of <paramref name="board"/> at its hub for <paramref name="ship"/> (design §3, approval of T13.3):
    /// <list type="number">
    /// <item>Candidates: the local templates with a pickup at the hub (pool or D121), the system-wide ones only with an
    /// edge a scan saw from the hub (<paramref name="edges"/>); never a Large or Bulk size.</item>
    /// <item>Bulk: the best-paying Medium that fits; a second only when both take at most half the ship.</item>
    /// <item>Direct: the one with the best aUEC per crate that fits.</item>
    /// <item>Smalls: by aUEC per crate, each that fits, up to <see cref="MaxContracts"/> in all.</item>
    /// </list>
    /// "Fits" is the load planner's first fit on the worst case of all roles so far, never a sum: racks lay crates, not SCU. Ties go to
    /// the template's uuid, so a board always gives the same recipe.
    /// </summary>
    public static Recipe For(Board board, Ship ship, ObservedEdges edges) =>
        board.Hub is { } hub ? At(board, hub, ship, edges) : new(null, [], [], "Recipe: Stanton hubs only");

    /// <summary>
    /// The recipe of <paramref name="board"/> from <paramref name="hub"/>, a place of its pools (T13.4): <see cref="For"/>
    /// with the board's hub, or a Pyro station as the start of a loop, whose board has none. <paramref name="allowed"/>
    /// narrows the greedy (<see cref="Pick"/>).
    /// </summary>
    internal static Recipe At(Board board, string hub, Ship ship, ObservedEdges edges, Func<IReadOnlyList<RecipeRole>, bool>? allowed = null)
    {
        var cap = CrateCap.Of(ship);
        RecipeRole[] candidates =
        [
            .. board.Local.Where(t => BoardHubs.Pickups(t).Places.Contains(hub)).Select(t => Role(t, BoardHubs.Dropoffs(t).Places, cap, hub)),
            .. board.Templates.Where(t => t.AreaKey == Board.StantonWide)
                .Select(t => Role(t, [.. edges.Of(t).Where(e => e.From == hub).Select(e => e.To)], cap, hub)),
        ];
        candidates = [.. candidates.Where(r => r.Destinations.Count > 0 && r.Template.Size is not (HaulSize.Large or HaulSize.Bulk))];

        var chosen = Pick(candidates, ship, cap, [], allowed);

        // The way back: per ring station the best-paying local template to the hub that fits the empty ship alone.
        var ring = chosen.SelectMany(r => r.Destinations).Distinct().Order(PlaceNames.Order);
        RecipeRole[] returns = [.. ring.Select(station => BestReturn(board, station, hub, ship, cap)).OfType<RecipeRole>()];

        return new(hub, chosen, returns, chosen.Count == 0 ? "Recipe: nothing from this hub fits the ship" : null);
    }

    /// <summary>
    /// The way back from <paramref name="station"/>: the best-paying local template of <paramref name="board"/> to
    /// <paramref name="hub"/> that fits the empty ship alone, never a Large or Bulk size; ties to the uuid. Null if none.
    /// </summary>
    internal static RecipeRole? BestReturn(Board board, string station, string hub, Ship ship, CrateCap cap) =>
        board.Local
            .Where(t => t.Size is not (HaulSize.Large or HaulSize.Bulk)
                && BoardHubs.Pickups(t).Places.Contains(station) && BoardHubs.Dropoffs(t).Places.Contains(hub))
            .Select(t => Role(t, [], cap, hub) with { Kind = RecipeKind.Return, Destinations = [hub], AcceptAt = station })
            .OrderByDescending(r => r.Template.Reward ?? 0)
            .ThenBy(r => r.Template.Uuid, StringComparer.Ordinal)
            .FirstOrDefault(r => Fits(ship, cap, [r])); // the first that fits of the best-paying: the same one, fewer first fits

    /// <summary>
    /// The greedy of the recipe over <paramref name="candidates"/>, bulk → direct → smalls (the smalls would take the
    /// direct's room otherwise, approval of T13.3, precision 8), each checked by the first fit together with
    /// <paramref name="aboard"/>, what the ship still carries (a loop's later station, T13.4); at most
    /// <see cref="MaxContracts"/> held at once, and only where <paramref name="allowed"/> says yes to the new roles so far
    /// (a Pyro ring: at most five stations, T13.4). Returns the new roles only.
    /// </summary>
    internal static IReadOnlyList<RecipeRole> Pick(
        IReadOnlyList<RecipeRole> candidates, Ship ship, CrateCap cap, IReadOnlyList<RecipeRole> aboard, Func<IReadOnlyList<RecipeRole>, bool>? allowed = null)
    {
        List<RecipeRole> chosen = [];
        bool TryAdd(RecipeRole role)
        {
            if (aboard.Count + chosen.Count >= MaxContracts
                || (allowed is not null && !allowed([.. chosen, role]))
                || !Fits(ship, cap, [.. aboard, .. chosen, role]))
            {
                return false;
            }

            chosen.Add(role);
            return true;
        }

        var half = ship.TotalScu / 2;
        foreach (var bulk in Ranked(candidates, RecipeKind.Bulk, r => r.Template.Reward ?? 0))
        {
            switch (chosen.Count)
            {
                case 0:
                    TryAdd(bulk);
                    break;
                case 1 when chosen[0].WorstScu <= half && bulk.WorstScu <= half:
                    TryAdd(bulk);
                    break;
            }
        }

        foreach (var direct in Ranked(candidates, RecipeKind.Direct, PerCrate))
        {
            if (TryAdd(direct))
            {
                break;
            }
        }

        foreach (var small in Ranked(candidates, RecipeKind.Small, PerCrate))
        {
            TryAdd(small);
        }

        return chosen;
    }

    private static IEnumerable<RecipeRole> Ranked(IEnumerable<RecipeRole> candidates, RecipeKind kind, Func<RecipeRole, double> value) =>
        candidates.Where(r => r.Kind == kind).OrderByDescending(value).ThenBy(r => r.Template.Uuid, StringComparer.Ordinal);

    private static double PerCrate(RecipeRole role) => (double)(role.Template.Reward ?? 0) / role.Crates;

    /// <summary>
    /// The role of <paramref name="template"/> to <paramref name="places"/>: the first of them it draws, in place order, the
    /// hub left out; its kind from the title (direct, else Medium = bulk, else small).
    /// </summary>
    internal static RecipeRole Role(HaulTemplate template, IReadOnlyList<string> places, CrateCap cap, string hub)
    {
        var stops = template.Shape == HaulShape.SingleToMulti ? template.Stops : 1;
        string[] destinations = [.. places.Where(p => p != hub).Distinct().Order(PlaceNames.Order).Take(stops)];
        var kind = template.Direct ? RecipeKind.Direct : template.Size == HaulSize.Medium ? RecipeKind.Bulk : RecipeKind.Small;
        var crates = template.Legs.Sum(l => CrateMix.Decompose(l.MaxScu, l.Container, cap).Count);
        return new(kind, template, destinations, template.MaxScu, crates);
    }

    /// <summary>
    /// Whether the worst case of <paramref name="roles"/> lies in the ship: each role's legs in turn to its destinations,
    /// one stop per place for all roles, laid by the planner's first fit (<see cref="LoadPlanner.Lays"/>): where it says
    /// yes the planner lays it too; its search would take about a second per question.
    /// </summary>
    internal static bool Fits(Ship ship, CrateCap cap, IReadOnlyList<RecipeRole> roles)
    {
        var items = roles.SelectMany(r => r.Template.Legs.Select((leg, i) => (
            Destination: r.Destinations[i % r.Destinations.Count],
            Item: new MissionItem(leg.Commodity, leg.MaxScu, leg.Container))));
        Stop[] stops =
        [
            .. items.GroupBy(x => x.Destination, StringComparer.Ordinal)
                .OrderBy(g => g.Key, PlaceNames.Order)
                .Select((g, order) => new MissionStop(g.Key, order + 1, [.. g.Select(x => x.Item)]).ToStop(cap)),
        ];

        return LoadPlanner.Lays(ship, stops);
    }
}
