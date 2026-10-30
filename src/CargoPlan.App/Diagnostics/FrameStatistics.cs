using System.Globalization;

namespace CargoPlan.App.Diagnostics;

/// <summary>
/// Frame intervals and hit-test times as one line of the measurement log (Phase 8: every task measures before and
/// after, D83). Taken over from the S8.0 spike, so the numbers compare with its tables.
/// </summary>
public static class FrameStatistics
{
    /// <summary>Fewer frames than this make a run invalid (Phase 8 rules).</summary>
    public const int MinFrames = 300;

    /// <summary>Nearest rank: the smallest value with at least <paramref name="share"/> of all values at or below it.</summary>
    public static double Percentile(IReadOnlyCollection<double> values, double share)
    {
        var sorted = values.Order().ToArray();
        return sorted[Math.Clamp((int)Math.Ceiling(share * sorted.Length) - 1, 0, sorted.Length - 1)];
    }

    public static bool IsValid(IReadOnlyCollection<double> intervals) => intervals.Count >= MinFrames;

    /// <summary>fps is frames over the nominal duration, as in S8.0, not over the measured time.</summary>
    public static string Summary(IReadOnlyCollection<double> intervals, TimeSpan duration) => Inv(
        $"n {intervals.Count}{(IsValid(intervals) ? "" : " INVALID")}, median {Percentile(intervals, 0.5):0.00} ms, p95 {Percentile(intervals, 0.95):0.00} ms, max {intervals.Max():0.0} ms, fps {intervals.Count / duration.TotalSeconds:0.0}");

    /// <summary>Hit-test times, finer: they lie far below a frame.</summary>
    public static string HitSummary(IReadOnlyCollection<double> times, int hits) => Inv(
        $"n {times.Count}, hit {hits}, median {Percentile(times, 0.5):0.000} ms, p95 {Percentile(times, 0.95):0.000} ms, max {times.Max():0.000} ms");

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
