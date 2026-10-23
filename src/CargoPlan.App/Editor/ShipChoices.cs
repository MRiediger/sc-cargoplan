using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Editor;

/// <summary>One entry of the header's ship selection (T7.2b): a catalog entry (D59), or one that didn't load.</summary>
/// <param name="Group">The manufacturer's code ("CRUS"), <see cref="ShipChoices.OtherGroup"/> or <see cref="ShipChoices.NotLoadedGroup"/>.</param>
/// <param name="Text">The name without the code the group shows already ("C2 Hercules"); id and reason for one that didn't load.</param>
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

    /// <summary>
    /// The catalog's ships by manufacturer, then by shown name; ships without a manufacturer under
    /// <see cref="OtherGroup"/>, files that didn't load last. Sorted here: grouping keeps the order of first appearance.
    /// </summary>
    public static IReadOnlyList<ShipChoice> Of(ShipCatalog catalog) =>
    [
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
