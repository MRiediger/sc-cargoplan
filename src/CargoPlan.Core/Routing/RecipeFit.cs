using CargoPlan.Core.Models;

namespace CargoPlan.Core.Routing;

/// <summary>
/// The check before accepting an offer (T13.3b, D123, design §4.3 step 3): whether the scanned template fits the recipe,
/// as its kind, or is too big for the ship.
/// </summary>
public abstract record RecipeFit
{
    private RecipeFit()
    {
    }

    /// <summary>It takes the place of the recipe's role of <paramref name="Kind"/> and the whole lies in the ship.</summary>
    public sealed record FitsAs(RecipeKind Kind) : RecipeFit;

    /// <summary>It doesn't: its worst case, its largest crate and the ship, for "too big for MISC Hull B (432 SCU in 16s)".</summary>
    public sealed record TooBig(int Scu, CrateSize Crate, string ShipName) : RecipeFit;

    /// <summary>
    /// The fit of what a scan matched: a template of the recipe fits as its role; any other takes the place of the recipe's
    /// last role of its kind (the bulk, the direct, the small that pays least per crate), or comes on top where the
    /// recipe has none, and the planner's first fit decides (<see cref="Recipe.Fits"/>). A Large or Bulk size never fits
    /// (design §3). Null where there is nothing to say: no single template, no recipe, a template outside Stanton.
    /// </summary>
    /// <param name="scanned">The drop-offs the scan read: where a system-wide template goes, whose data name no place.</param>
    public static RecipeFit? Of(MatchResult match, Recipe recipe, Ship ship, IReadOnlyList<string>? scanned = null)
    {
        if (match is not MatchResult.Matched { Template: var template } || recipe.Hub is not { } hub || template.System != StarSystem.Stanton)
        {
            return null;
        }

        var tooBig = new TooBig(template.MaxScu, template.Legs.Max(l => l.Container), ship.Name);
        if (template.Size is HaulSize.Large or HaulSize.Bulk)
        {
            return tooBig;
        }

        if (recipe.Outbound.FirstOrDefault(r => r.Template.Uuid == template.Uuid) is { } own)
        {
            return new FitsAs(own.Kind);
        }

        var cap = CrateCap.Of(ship);
        var role = Recipe.Role(template, PlacesOf(template, hub, scanned ?? []), cap, hub);
        var replaced = recipe.Outbound.LastOrDefault(r => r.Kind == role.Kind);
        List<RecipeRole> roles = [.. recipe.Outbound.Where(r => !ReferenceEquals(r, replaced)), role];
        return Recipe.Fits(ship, cap, roles) ? new FitsAs(role.Kind) : tooBig;
    }

    /// <summary>A stop's name where neither the data nor the scan name one; the planner only groups crates by it.</summary>
    private const string UnknownPlace = "?";

    // Where its worst case goes: its drop-off pool (the hub where it is empty, D121), else what the scan read.
    private static IReadOnlyList<string> PlacesOf(HaulTemplate template, string hub, IReadOnlyList<string> scanned)
    {
        var pool = BoardHubs.Dropoffs(template).Places.Where(p => p != hub).ToList();
        return pool.Count > 0 ? pool : scanned.Any(p => p != hub) ? scanned : [UnknownPlace];
    }
}
