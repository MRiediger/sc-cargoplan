namespace CargoPlan.Core.Routing;

/// <summary>
/// The level a Red Wind card names in its title ("Member | Stellar Medium Haul | …", issue #22), as the game data's
/// mission types ("Hauling - Stellar", "Hauling - Planetary"). Not part of a template's identity.
/// </summary>
public enum AreaLevel
{
    Planetary,
    Stellar,
}
