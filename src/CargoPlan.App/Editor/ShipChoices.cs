using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Editor;

/// <summary>
/// The ship the overlay starts with (T7.2). A ship is a catalog entry, never a file name (D59); a ship that didn't load
/// never stops the start: the Hull A takes its place and a notice says why.
/// </summary>
public static class ShipChoices
{
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
