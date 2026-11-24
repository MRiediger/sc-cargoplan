namespace CargoPlan.Core.Routing;

/// <summary>
/// A route the player finished at its hub (T13.6, D126), kept in routes.json: what it paid and how long the round took,
/// the measurement the loop times (<see cref="LoopTimes"/>) may one day learn from (backlog).
/// </summary>
/// <param name="DepartedUtc">When the player left the hub; null if the station bar never moved, then the time runs from <paramref name="StartedUtc"/>.</param>
/// <param name="Reward">The rewards of the filled placeholders and the extras, as the plan read them; never an open placeholder's.</param>
/// <param name="Filled">Placeholders filled at the finish, of <paramref name="Total"/>.</param>
/// <param name="Extras">Accepted contracts outside the placeholders.</param>
public sealed record RouteRun(
    string Name,
    DateTime StartedUtc,
    DateTime? DepartedUtc,
    DateTime FinishedUtc,
    string Hub,
    LoopKind Kind,
    StarSystem System,
    int Landings,
    int Crates,
    int LongJumps,
    long Reward,
    int Filled,
    int Total,
    int Extras)
{
    /// <summary>Runs routes.json keeps; the oldest goes first.</summary>
    public const int MaxKept = 50;

    /// <summary>The round: from leaving the hub, or from the start where the bar never moved, to the finish.</summary>
    public TimeSpan Time => FinishedUtc - (DepartedUtc ?? StartedUtc);
}
