using CargoPlan.App.Diagnostics;

namespace CargoPlan.App.Tests;

public class FrameStatisticsTests
{
    [Theory]
    [InlineData(0.5, 3)]
    [InlineData(0.95, 5)]
    [InlineData(1.0, 5)]
    [InlineData(0.0, 1)]
    public void Percentile_is_the_nearest_rank(double share, double expected)
    {
        FrameStatistics.Percentile([5, 1, 4, 2, 3], share).Should().Be(expected);
    }

    [Fact]
    public void Percentile_of_twenty_frames_takes_the_nineteenth_for_p95()
    {
        double[] values = [.. Enumerable.Range(1, 20).Select(i => (double)i)];

        FrameStatistics.Percentile(values, 0.95).Should().Be(19);
    }

    [Fact]
    public void Summary_marks_fewer_than_the_minimum_frames_invalid()
    {
        double[] intervals = [.. Enumerable.Repeat(16.67, FrameStatistics.MinFrames - 1)];

        FrameStatistics.Summary(intervals, TimeSpan.FromSeconds(30)).Should().Contain("INVALID");
        FrameStatistics.IsValid(intervals).Should().BeFalse();
    }

    [Fact]
    public void Summary_gives_median_p95_max_and_fps_in_invariant_culture()
    {
        double[] intervals = [.. Enumerable.Repeat(16.67, 1800)];

        FrameStatistics.Summary(intervals, TimeSpan.FromSeconds(30))
            .Should().Be("n 1800, median 16.67 ms, p95 16.67 ms, max 16.7 ms, fps 60.0");
    }
}
