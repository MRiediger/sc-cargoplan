namespace CargoPlan.Core.Routing;

/// <summary>
/// The rank a hauling contract needs, the first word of its title (docs/hauling-data-format.md, <c>rank</c>). The order
/// is the game's: a board shows every rank up to the player's (design §1.2).
/// </summary>
public enum HaulRank
{
    Trainee,
    Rookie,
    Junior,
    Member,
    Experienced,
    Senior,
    Master,
}
