using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using static CargoPlan.Core.Tests.TemplateFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Measured loop times (T13.6c, D127): the runs of routes.json per loop and ship, the median of the last seven, used from
/// three runs on; a direct loop keyed by its contract as well. Loops and runs built here (A10).
/// </summary>
public class MeasuredTimesTests
{
    private const string Everus = "Everus Harbor";
    private const string L1 = "HUR-L1 Green Glade Station";
    private const string L4 = "HUR-L4 Melodic Fields Station";

    private static readonly DateTime Friday = new(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);

    private static readonly LoopCandidate Ring = Loop(LoopKind.LocalRing, [L1, L4], T([], [L1, L4], 325_250));

    // A run of the ring, finished day days after Friday, minutes after its departure.
    private static RouteRun Run(double minutes, int day = 0, string ship = "hull_b", LoopCandidate? loop = null, DirectContract? direct = null)
    {
        loop ??= Ring;
        var finished = Friday.AddDays(day);
        return new RouteRun("run", finished.AddMinutes(-minutes - 10), finished.AddMinutes(-minutes), finished, loop.Start, loop.Kind, loop.System,
            loop.Landings, 30, 2, 500_000, 1, 1, 0, ship, loop.Stations, direct);
    }

    [Fact]
    public void Three_runs_give_their_median_not_their_mean()
    {
        var measured = MeasuredTimes.For([Run(70, 0), Run(80, 2), Run(150, 4)], "hull_b");

        var ring = measured.Usable(Ring)!;
        (ring.Count, ring.MedianMinutes, ring.LastUtc).Should().Be((3, 80, Friday.AddDays(4)), "one slow round moves the median not at all");
        ring.Recent.Select(r => r.Time.TotalMinutes).Should().Equal([150, 80, 70], "the newest first");
    }

    [Fact]
    public void Two_runs_are_counted_but_not_used()
    {
        var measured = MeasuredTimes.For([Run(70), Run(80, 1)], "hull_b");

        measured.Of(Ring).Should().Match<MeasuredLoop>(m => m.Count == 2 && !m.IsEnough);
        measured.Usable(Ring).Should().BeNull();
    }

    [Fact]
    public void Runs_of_another_ship_measure_nothing_for_this_one()
    {
        var measured = MeasuredTimes.For([Run(70, ship: "raft"), Run(80, 1, ship: "raft"), Run(150, 2, ship: "raft")], "hull_b");

        measured.Of(Ring).Should().BeNull();
        MeasuredTimes.For([Run(70, ship: "raft"), Run(80, 1, ship: "raft"), Run(150, 2, ship: "raft")], "raft").Usable(Ring).Should().NotBeNull();
    }

    [Fact]
    public void A_run_under_a_minute_or_over_six_hours_is_left_out_with_a_line()
    {
        // A "Finish plan" pressed at once, or one forgotten overnight: a line in the log, no error.
        var measured = MeasuredTimes.For([Run(70), Run(0, 1), Run(80, 2), Run(7 * 60, 3)], "hull_b");

        measured.Of(Ring)!.Count.Should().Be(2);
        measured.Usable(Ring).Should().BeNull();
        measured.Ignored.Should().HaveCount(2).And.OnlyContain(line => line.Contains("1 min – 6 h", StringComparison.Ordinal));
        measured.Ignored[0].Should().Contain("0 min");
    }

    [Fact]
    public void A_run_without_ship_or_stations_measured_no_loop()
    {
        // Written before T13.6c's addendum (gate 6b): no identity, no line.
        RouteRun[] runs = [Run(70) with { ShipId = null }, Run(80, 1) with { Stations = null }, Run(90, 2)];

        MeasuredTimes.For(runs, "hull_b").Of(Ring)!.Count.Should().Be(1);
        MeasuredTimes.For(runs, "hull_b").Ignored.Should().BeEmpty();
    }

    [Fact]
    public void The_median_of_an_even_count_is_the_mean_of_the_middle_two()
    {
        MeasuredTimes.For([Run(70), Run(80, 1), Run(90, 2), Run(150, 3)], "hull_b").Usable(Ring)!.MedianMinutes.Should().Be(85);
    }

    [Fact]
    public void The_median_takes_the_last_seven_runs_and_the_count_all()
    {
        // E1 of the T13.6c approval: the player got faster; the old slow rounds go out of the window, "measured n×" still
        // counts them. Over all fifteen the median would still be 120.
        RouteRun[] runs = [.. Enumerable.Range(0, 8).Select(day => Run(120, day)), .. Enumerable.Range(8, 7).Select(day => Run(60, day))];

        var ring = MeasuredTimes.For(runs, "hull_b").Usable(Ring)!;

        (ring.Count, ring.MedianMinutes).Should().Be((15, 60));
    }

    [Fact]
    public void Enough_counts_the_loops_with_three_runs_or_more()
    {
        // The settings window's line under the switch (gate finding T13.6c): a loop with two runs doesn't count yet.
        var reversed = Ring with { Stations = [L4, L1] };
        RouteRun[] runs = [Run(70), Run(80, 1), Run(90, 2), Run(70, loop: reversed), Run(80, 1, loop: reversed)];

        MeasuredTimes.For(runs, "hull_b").EnoughCount.Should().Be(1);
        MeasuredTimes.For([.. runs, Run(90, 2, loop: reversed)], "hull_b").EnoughCount.Should().Be(2);
        MeasuredTimes.Empty.EnoughCount.Should().Be(0);
    }

    [Fact]
    public void Another_loop_is_another_key()
    {
        var reversed = Ring with { Stations = [L4, L1] };

        MeasuredTimes.For([Run(70), Run(80, 1), Run(90, 2)], "hull_b").Of(reversed).Should().BeNull();
    }

    [Fact]
    public void A_direct_loop_is_measured_with_its_contracts_rank_size_and_reward_and_its_twins_share_it()
    {
        // P1: two direct rows to the same station are one loop only with the same rank, size and reward; the rows Loops
        // joins (another commodity only) share the measurement.
        var senior = Loop(LoopKind.Direct, [L1], T([], [L1], 90_000, rank: HaulRank.Senior, size: HaulSize.Small, direct: true));
        var twin = Loop(LoopKind.Direct, [L1], T([], [L1], 90_000, rank: HaulRank.Senior, size: HaulSize.Small, direct: true, legs: [Leg("Iron", 1, 20)]));
        var experienced = Loop(LoopKind.Direct, [L1], T([], [L1], 160_250, rank: HaulRank.Experienced, size: HaulSize.Medium, direct: true));
        var contract = new DirectContract(HaulRank.Senior, HaulSize.Small, 90_000);
        RouteRun[] runs = [Run(30, 0, loop: senior, direct: contract), Run(35, 1, loop: senior, direct: contract), Run(40, 2, loop: senior, direct: contract)];

        var measured = MeasuredTimes.For(runs, "hull_b");

        measured.Usable(senior)!.MedianMinutes.Should().Be(35);
        measured.Usable(twin).Should().NotBeNull();
        measured.Of(experienced).Should().BeNull();
        MeasuredTimes.For([.. runs.Select(r => r with { Direct = null })], "hull_b").Of(senior).Should().BeNull("a direct run without its contract measured none");
    }

    private static LoopCandidate Loop(LoopKind kind, IReadOnlyList<string> stations, HaulTemplate template) => new(
        kind, StarSystem.Stanton, Everus, stations, [new RecipeRole(RecipeKind.Small, template, stations, 20, 5)], template.Reward, false, 30, 2, 0, 0, 1);
}
