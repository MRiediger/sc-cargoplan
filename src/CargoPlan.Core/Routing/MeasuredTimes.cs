using static System.FormattableString;

namespace CargoPlan.Core.Routing;

/// <summary>
/// Which loop a run measured (T13.6c, D127): kind, system, start and the stations as flown, as <see cref="Route.From"/>
/// keeps them from the candidate; a direct loop also by its contract (<see cref="DirectContract"/>, P1). The rank and the
/// ship are not in it: the ship filters the runs (<see cref="MeasuredTimes.For"/>), the rank only picks the contracts.
/// </summary>
/// <param name="Stations">The stations joined, so that two keys with the same stations are equal.</param>
public sealed record LoopKey(LoopKind Kind, StarSystem System, string Start, string Stations, DirectContract? Direct)
{
    public static LoopKey Of(LoopCandidate loop) => new(
        loop.Kind, loop.System, loop.Start, Join(loop.Stations),
        loop.Kind == LoopKind.Direct && loop.Contracts.Count > 0 && loop.Contracts[0].Template is var t ? new DirectContract(t.Rank, t.Size, t.Reward) : null);

    /// <summary>The run's loop; null for a run that names none (written before T13.6c, or a direct run without its contract).</summary>
    public static LoopKey? Of(RouteRun run) =>
        run is { ShipId: not null, Stations: { } stations } && (run.Kind != LoopKind.Direct || run.Direct is not null)
            ? new(run.Kind, run.System, run.Hub, Join(stations), run.Direct)
            : null;

    private static string Join(IEnumerable<string> stations) => string.Join('\n', stations);
}

/// <summary>A loop's runs with one ship: how many, the median of the last <see cref="MeasuredTimes.MedianWindow"/>, the newest three.</summary>
/// <param name="Count">Every run of the loop that counts, also those before the window (E1 of the T13.6c approval).</param>
/// <param name="Recent">The newest runs first, at most three, for the row's tool tip.</param>
public sealed record MeasuredLoop(int Count, double MedianMinutes, IReadOnlyList<RouteRun> Recent)
{
    /// <summary>When the newest run finished.</summary>
    public DateTime LastUtc => Recent[0].FinishedUtc;

    /// <summary>Enough runs for the row to rank by them (<see cref="MeasuredTimes.MinRuns"/>).</summary>
    public bool IsEnough => Count >= MeasuredTimes.MinRuns;
}

/// <summary>
/// The player's own loop times (T13.6c, D127): from the finished runs of routes.json, per loop, for one ship. A loop flown
/// <see cref="MinRuns"/> times or more ranks by the median of its last <see cref="MedianWindow"/> times instead of the
/// model (<see cref="LoopTimes"/>); a row is one or the other, never a mix (<see cref="Loops.Rank"/>). Pure: the same
/// runs give the same times.
/// </summary>
public sealed class MeasuredTimes
{
    /// <summary>Runs before a loop ranks by them: one round is chance, three show a habit.</summary>
    public const int MinRuns = 3;

    /// <summary>The median takes the newest this many (E1): an old slow round goes out of it once the player is faster.</summary>
    public const int MedianWindow = 7;

    /// <summary>A shorter round is a "Finish plan" pressed at once.</summary>
    public static readonly TimeSpan MinTime = TimeSpan.FromMinutes(1);

    /// <summary>A longer round is a route left open overnight.</summary>
    public static readonly TimeSpan MaxTime = TimeSpan.FromHours(6);

    private readonly IReadOnlyDictionary<LoopKey, MeasuredLoop> _loops;

    private MeasuredTimes(IReadOnlyDictionary<LoopKey, MeasuredLoop> loops, IReadOnlyList<string> ignored) => (_loops, Ignored) = (loops, ignored);

    public static MeasuredTimes Empty { get; } = new(new Dictionary<LoopKey, MeasuredLoop>(), []);

    /// <summary>One line per run of the ship left out for its time, for the log (Core doesn't log).</summary>
    public IReadOnlyList<string> Ignored { get; }

    /// <summary>
    /// The loops <paramref name="runs"/> measured with the ship <paramref name="shipId"/>: a run of another ship measures
    /// nothing for it (a RAFT's round is no Hull B's). A run that names no loop counts silently not; one out of
    /// <see cref="MinTime"/> to <see cref="MaxTime"/> not either, with a line.
    /// </summary>
    public static MeasuredTimes For(IEnumerable<RouteRun> runs, string shipId)
    {
        List<string> ignored = [];
        List<(LoopKey Key, RouteRun Run)> counted = [];
        foreach (var run in runs.Where(r => r.ShipId == shipId))
        {
            if (LoopKey.Of(run) is not { } key)
            {
                continue;
            }

            if (run.Time < MinTime || run.Time > MaxTime)
            {
                ignored.Add(Invariant($"run \"{run.Name}\" finished {run.FinishedUtc:yyyy-MM-dd HH:mm}Z took {run.Time.TotalMinutes:0} min, outside 1 min – 6 h: not measured"));
                continue;
            }

            counted.Add((key, run));
        }

        var loops = counted.GroupBy(c => c.Key).ToDictionary(g => g.Key, g =>
        {
            RouteRun[] newest = [.. g.Select(c => c.Run).OrderByDescending(r => r.FinishedUtc)];
            return new MeasuredLoop(newest.Length, Median([.. newest.Take(MedianWindow).Select(r => r.Time.TotalMinutes)]), [.. newest.Take(3)]);
        });
        return new(loops, ignored);
    }

    /// <summary>The runs of <paramref name="loop"/>, however few; null for none.</summary>
    public MeasuredLoop? Of(LoopCandidate loop) => _loops.GetValueOrDefault(LoopKey.Of(loop));

    /// <summary>The runs of <paramref name="loop"/> when there are enough to rank by (<see cref="MeasuredLoop.IsEnough"/>); else null.</summary>
    public MeasuredLoop? Usable(LoopCandidate loop) => Of(loop) is { IsEnough: true } measured ? measured : null;

    // The middle value; of an even count the mean of the middle two.
    private static double Median(double[] minutes)
    {
        Array.Sort(minutes);
        var middle = minutes.Length / 2;
        return minutes.Length % 2 == 1 ? minutes[middle] : (minutes[middle - 1] + minutes[middle]) / 2;
    }
}
