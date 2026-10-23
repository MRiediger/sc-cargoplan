using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Editor;

/// <summary>One entry of the header's ship selection (T7.2b): a catalog entry (D59), or one that didn't load.</summary>
/// <param name="Group">The manufacturer's code ("CRUS"), <see cref="ShipChoices.RecentGroup"/>, <see cref="ShipChoices.OtherGroup"/> or <see cref="ShipChoices.NotLoadedGroup"/>.</param>
/// <param name="Text">The name without the code the group shows already ("C2 Hercules"), the full name under Recent; id and reason for one that didn't load.</param>
/// <param name="Ship">null = didn't load, not selectable.</param>
/// <param name="Problem">Why it didn't load, for the entry's tool tip.</param>
public sealed record ShipChoice(string Id, string Group, string Text, Ship? Ship, string? Problem = null)
{
    public bool IsSelectable => Ship is not null;
}

/// <summary>
/// The ship the overlay starts with and the header's selection (T7.2, moved from the editor in T7.2b). A ship is a catalog entry, never a file name
/// (D59); a ship that didn't load never stops the start: the Hull A takes its place and a notice says why.
/// </summary>
public static class ShipChoices
{
    public const string OtherGroup = "Other";

    public const string NotLoadedGroup = "Not loaded";

    public const string RecentGroup = "Recent";

    /// <summary>How many ships <see cref="RecentGroup"/> shows; the current ship isn't one of them (T7.2c).</summary>
    public const int RecentCount = 3;

    /// <summary>
    /// The catalog's ships by manufacturer, then by shown name; ships without a manufacturer under
    /// <see cref="OtherGroup"/>, files that didn't load last. Sorted here: grouping keeps the order of first appearance.
    /// On top <see cref="RecentGroup"/> (T7.2c): up to <see cref="RecentCount"/> of <paramref name="recent"/> that load,
    /// without the current ship, with their full names, since no manufacturer heading stands above them. They stay in
    /// their groups too, so the groups never change.
    /// </summary>
    /// <param name="recent">Catalog ids, latest first (<see cref="Remember"/>); null = none yet.</param>
    public static IReadOnlyList<ShipChoice> Of(ShipCatalog catalog, IReadOnlyList<string>? recent = null, string? currentId = null) =>
    [
        .. (recent ?? [])
            .Where(id => id != currentId)
            .Select(catalog.Find)
            .OfType<Ship>()
            .Take(RecentCount)
            .Select(s => new ShipChoice(s.Id, RecentGroup, s.Name, s)),
        .. catalog.Ships
            .Select(s => s.Manufacturer is { } code
                ? new ShipChoice(s.Id, code, s.Name.StartsWith(code + " ", StringComparison.Ordinal) ? s.Name[(code.Length + 1)..] : s.Name, s)
                : new ShipChoice(s.Id, OtherGroup, s.Name, s))
            .OrderBy(c => c.Group == OtherGroup)
            .ThenBy(c => c.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Text, StringComparer.OrdinalIgnoreCase),
        .. catalog.Problems.Select(p => new ShipChoice(p.ShipId, NotLoadedGroup, $"{p.ShipId} – {p.Message}", null, p.Message)),
    ];

    /// <summary>
    /// The recent ships after a change from <paramref name="fromId"/> to <paramref name="toId"/>: both in front, no
    /// duplicates, the current ship and <see cref="RecentCount"/> more. The ship left behind counts as chosen, also when
    /// the start set it (T7.2c).
    /// </summary>
    public static IReadOnlyList<string> Remember(IReadOnlyList<string> recent, string fromId, string toId) =>
        [.. new[] { toId, fromId }.Concat(recent).Distinct().Take(RecentCount + 1)];

    /// <summary>
    /// The header's search (T7.2c): part of the name or the manufacturer's code, ignoring case and the spaces around it;
    /// empty matches every entry. Never the group's name, or "rec" would find every recent ship. A ship that didn't load
    /// is found by its id and reason.
    /// </summary>
    public static bool Matches(ShipChoice choice, string query)
    {
        var text = query.Trim();
        var searched = choice.Ship is { } ship ? $"{ship.Manufacturer} {ship.Name}" : choice.Text;
        return searched.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The one selectable ship among <paramref name="shown"/>, also when Recent lists it twice; null for none or more.</summary>
    public static ShipChoice? SingleMatch(IEnumerable<ShipChoice> shown) =>
        shown.Where(c => c.IsSelectable).DistinctBy(c => c.Id).ToList() is [var single] ? single : null;

    /// <summary>
    /// The ship of settings.json; without one the ship of plan.json, if it loaded; otherwise the Hull A. The notice says
    /// when the settings' ship didn't load or the plan's was taken; null when it is what the player chose or the default.
    /// </summary>
    /// <param name="wanted">settings.json's <c>ship</c>; null = none chosen yet.</param>
    /// <param name="planShipId">plan.json's <c>shipId</c>; null = no plan or none readable.</param>
    public static (Ship Ship, string? Notice) Resolve(ShipCatalog catalog, string? wanted, string? planShipId)
    {
        var fallback = catalog.Get(ShipLoader.FallbackShipId);
        if (wanted is null)
        {
            return planShipId is not null && planShipId != fallback.Id && catalog.Find(planShipId) is { } fromPlan
                ? (fromPlan, $"Ship set to {fromPlan.Name} from your plan.")
                : (fallback, null);
        }

        if (catalog.Find(wanted) is { } ship)
        {
            return (ship, null);
        }

        var reason = catalog.Problems.FirstOrDefault(p => p.ShipId == wanted)?.Message ?? "not found";
        return (fallback, $"Ship '{wanted}' not loaded: {reason} – using {fallback.Name}.");
    }
}
