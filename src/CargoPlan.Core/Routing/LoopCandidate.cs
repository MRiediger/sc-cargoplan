namespace CargoPlan.Core.Routing;

/// <summary>The classes of loop the ranking compares (T13.4, design §4.7).</summary>
public enum LoopKind
{
    /// <summary>A hub's recipe out to the stations of its pools and the way back from each (a Pyro station counts as a hub).</summary>
    LocalRing,

    /// <summary>One direct contract to its place and back to the hub, with a return when one fits.</summary>
    Direct,

    /// <summary>The four Stanton hubs in turn, with the system-wide contracts the scans saw between them (design §8.6).</summary>
    SystemWide,
}

/// <summary>What the loop list is sorted by: aUEC per hour, per loop or per landing.</summary>
public enum LoopSort
{
    PerHour,
    PerLoop,
    PerLanding,
}

/// <summary>
/// The player's times for the hourly figure (design §4.7, defaults 10 min / 20 s / 4 min): per landing, per crate and
/// per long jump. A short quantum leg (≤ <see cref="Loops.LongJumpGm"/>) is part of the landing's time.
/// </summary>
public sealed record LoopTimes(int StationMinutes, int CrateSeconds, int JumpMinutes)
{
    public const int DefaultStationMinutes = 10;

    public const int DefaultCrateSeconds = 20;

    public const int DefaultJumpMinutes = 4;

    public static LoopTimes Default { get; } = new(DefaultStationMinutes, DefaultCrateSeconds, DefaultJumpMinutes);
}

/// <summary>
/// One loop of the ranking (T13.4, D124): from <see cref="Start"/> through <see cref="Stations"/> and back to the start.
/// Every figure is a worst case of the templates (every leg at its upper band, every station its pools may draw) unless
/// a scan saw less; rewards come from the data or from a scan, never estimated (D118).
/// </summary>
/// <param name="Stations">The stations in the order flown, without the start; the loop ends at the start.</param>
/// <param name="Contracts">The contracts taken: out from the start and along the way, then the ways back.</param>
/// <param name="Reward">The sum of the known rewards; null where none is known (Red Wind unscanned) or there are no contracts.</param>
/// <param name="RewardPartial">Some contracts' rewards are unknown: <see cref="Reward"/> is a lower bound.</param>
/// <param name="Crates">The contracts' crates, each leg at the largest SCU a scan saw, else its upper band.</param>
/// <param name="LongJumps">Legs of the loop longer than <see cref="Loops.LongJumpGm"/>.</param>
/// <param name="TravelGm">The direct jump from the saved hub to the start: 0 at the same hub, null in another system.</param>
/// <param name="Observed">Contracts a scan was matched to; for <see cref="LoopKind.SystemWide"/> the hubs with an edge seen to another.</param>
/// <param name="Total">Contracts; for <see cref="LoopKind.SystemWide"/> the four hubs.</param>
/// <param name="NeedsScansAt">The first hub of the system-wide loop no scan has shown an edge from; null otherwise.</param>
/// <param name="Alike">Direct runs that differ only in their commodity, shown as one row.</param>
public sealed record LoopCandidate(
    LoopKind Kind,
    StarSystem System,
    string Start,
    IReadOnlyList<string> Stations,
    IReadOnlyList<RecipeRole> Contracts,
    long? Reward,
    bool RewardPartial,
    int Crates,
    int LongJumps,
    double? TravelGm,
    int Observed,
    int Total,
    string? NeedsScansAt = null,
    int Alike = 1)
{
    /// <summary>Every station and the start again at the end.</summary>
    public int Landings => Stations.Count + 1;

    /// <summary>Quantum legs: one into each landing.</summary>
    public int Legs => Stations.Count + 1;

    /// <summary>False for a system-wide loop no scan has given a contract yet: then it shows where to scan, no figures.</summary>
    public bool HasNumbers => Contracts.Count > 0;

    public double? PerLanding => Reward is { } reward ? (double)reward / Landings : null;

    /// <summary>The loop's time under <paramref name="times"/>: landings, crates and long jumps (design §4.7).</summary>
    public double Hours(LoopTimes times) =>
        ((Landings * times.StationMinutes * 60.0) + (Crates * times.CrateSeconds) + (LongJumps * times.JumpMinutes * 60.0)) / 3600;

    /// <summary>~aUEC/h; null without a reward. The travel to the start is not in it (D124).</summary>
    public double? PerHour(LoopTimes times) => Reward is { } reward ? reward / Hours(times) : null;
}
