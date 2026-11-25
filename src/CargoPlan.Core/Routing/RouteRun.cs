namespace CargoPlan.Core.Routing;

/// <summary>
/// A route the player finished at its hub (T13.6, D126), kept in routes.json: what it paid and how long the round took,
/// the measurement the loop times (<see cref="LoopTimes"/>) may one day learn from (backlog).
/// </summary>
/// <param name="DepartedUtc">When the player left the hub; null if the station bar never moved, then the time runs from <paramref name="StartedUtc"/>.</param>
/// <param name="Reward">The rewards of the filled placeholders and the extras, as the plan read them; never an open placeholder's.</param>
/// <param name="Filled">Placeholders filled at the finish, of <paramref name="Total"/>.</param>
/// <param name="Extras">Accepted contracts outside the placeholders.</param>
/// <param name="ShipId">The overlay's ship at the finish: with <paramref name="Stations"/> the loop's identity for measured times
/// (T13.6c). Written always; null in a run written before (the branch's gate 6b).</param>
/// <param name="Stations">The route's stations as Start plan made them from the loop (<see cref="Route.Stations"/>), without
/// the extras' places; null as <paramref name="ShipId"/>.</param>
/// <param name="Direct">A direct run's outbound contract (T13.6c, P1): with the loop's identity the key of a direct loop;
/// null for every other kind, and for a direct run whose template the data no longer have.</param>
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
    int Extras,
    string? ShipId = null,
    IReadOnlyList<string>? Stations = null,
    DirectContract? Direct = null)
{
    /// <summary>Runs routes.json keeps; the oldest goes first.</summary>
    public const int MaxKept = 50;

    /// <summary>The round: from leaving the hub, or from the start where the bar never moved, to the finish.</summary>
    public TimeSpan Time => FinishedUtc - (DepartedUtc ?? StartedUtc);
}

/// <summary>
/// What tells one direct loop from another to the same station (T13.6c, P1): its outbound template's rank, size and
/// reward, as <see cref="Loops"/> joins direct rows that differ only in their commodity.
/// </summary>
/// <param name="Reward">The template's; null where the data have none (Red Wind).</param>
public sealed record DirectContract(HaulRank Rank, HaulSize Size, long? Reward);
