using System.Collections.Concurrent;
using System.Diagnostics;
using CargoPlan.App.Vr;

namespace CargoPlan.App.Tests;

// T14.2, D131: the capture thread's timing without a window: at most 15 Hz, nothing at rest, once more after the render
// thread (K2), the cursor (K3), the scan (K10), 4096 px (K4), the 10-s line (K5), the forced mode (K7), and a stop that
// never waits for a hung capture (a hung UI thread holds PrintWindow, S14.0).
public sealed class VrFeedTests : IDisposable
{
    // Longer than a start's two captures (now and after the trailing delay) with the timer's slack.
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(400);

    private readonly FakeCapture _capture = new();
    private readonly ConcurrentQueue<string> _log = new();
    private readonly VrScanPause _pause = new();
    private readonly VrFeed _feed;

    public VrFeedTests() => _feed = _capture.Feed(_pause, _log.Enqueue);

    public void Dispose()
    {
        _capture.Block?.Set();
        _feed.Stop();
        WaitFor(() => !_feed.IsRunning);
    }

    [Fact]
    public void Never_started_it_has_no_thread_and_captures_nothing()
    {
        _feed.MarkDirty(VrFrameStyle.None);
        Thread.Sleep(100);

        _feed.IsRunning.Should().BeFalse();
        _capture.Captures.Should().BeEmpty();
    }

    [Fact]
    public void Start_captures_now_and_once_more_after_the_trailing_delay_then_rests()
    {
        _feed.Start();
        Thread.Sleep(Settle);

        _capture.Captures.Should().HaveCount(2);
        var times = _capture.Times;
        (times[1] - times[0]).Should().BeGreaterThan(VrFeed.TrailingDelay.TotalMilliseconds - 20);

        // At rest ≈ 0 captures a second (Auflage 3 S14.0).
        Thread.Sleep(600);
        _capture.Captures.Should().HaveCount(2);
    }

    [Fact]
    public void A_signal_captures_and_once_more_after_the_render_thread()
    {
        StartAndSettle();

        _feed.MarkDirty(VrFrameStyle.None);
        Thread.Sleep(Settle);

        _capture.Captures.Should().HaveCount(4);
    }

    [Fact]
    public void A_steady_signal_captures_at_most_15_times_a_second()
    {
        StartAndSettle();
        var before = _capture.Captures.Count;

        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 1000)
        {
            _feed.MarkDirty(VrFrameStyle.None);
            Thread.Sleep(5);
        }

        var times = _capture.Times[before..];
        times.Length.Should().BeInRange(5, 16);
        times.Zip(times.Skip(1), (a, b) => b - a).Should().OnlyContain(gap => gap > VrFeed.MinInterval.TotalMilliseconds - 2);
    }

    [Fact]
    public void Without_a_signal_a_moving_cursor_captures_and_a_still_one_does_not()
    {
        // K3: the cursor is drawn into the picture, and WPF doesn't render for it.
        StartAndSettle();
        var before = _capture.Captures.Count;

        _capture.Cursor = new CursorSpot(10, 10, 1);
        Thread.Sleep(200);
        var moved = _capture.Captures.Count;
        Thread.Sleep(300);

        moved.Should().BeGreaterThan(before);
        _capture.Captures.Count.Should().Be(moved);

        // Leaving the window takes it out of the picture.
        _capture.Cursor = null;
        Thread.Sleep(200);
        _capture.Captures.Count.Should().BeGreaterThan(moved);
    }

    [Fact]
    public void During_a_scan_the_feed_skips_and_takes_the_picture_afterwards()
    {
        // D100 addendum T14.0: the headset never shows a cloaked frame.
        StartAndSettle();
        var before = _capture.Captures.Count;

        using (_pause.Bracket(_ => null)(default))
        {
            _feed.MarkDirty(VrFrameStyle.None);
            Thread.Sleep(Settle);
            _capture.Captures.Count.Should().Be(before);
        }

        Thread.Sleep(Settle);
        _capture.Captures.Count.Should().BeGreaterThan(before);
    }

    [Fact]
    public void A_cloaked_window_is_taken_again()
    {
        _capture.Next.Enqueue(VrShotKind.Cloaked);
        _capture.Otherwise = VrShotKind.Unchanged;

        _feed.Start();
        WaitFor(() => _capture.Captures.Count >= 2);

        // The second capture came from the retry, before the trailing delay was due.
        var times = _capture.Times;
        (times[1] - times[0]).Should().BeLessThan(VrFeed.TrailingDelay.TotalMilliseconds);
    }

    [Fact]
    public void The_newest_frame_replaces_the_one_still_waiting()
    {
        StartAndSettle();

        _capture.Frames.Should().HaveCount(2);
        _feed.Take().Should().BeSameAs(_capture.Frames.Last());
        _feed.Take().Should().BeNull();
    }

    [Fact]
    public void A_returned_frame_of_the_same_size_is_used_again()
    {
        // Three buffers at most, so 15 Hz of ≈ 3 MB doesn't fill the large object heap.
        StartAndSettle();
        var taken = _feed.Take()!;
        _feed.Return(taken);

        _feed.MarkDirty(VrFrameStyle.None);
        WaitFor(() => _capture.Frames.Count >= 3);

        _capture.Frames.Skip(2).Should().Contain(f => ReferenceEquals(f, taken) || ReferenceEquals(f, _capture.Frames.First()));
    }

    [Fact]
    public void Unchanged_pictures_hand_nothing_over()
    {
        _capture.Otherwise = VrShotKind.Unchanged;

        StartAndSettle();

        _feed.Take().Should().BeNull();
    }

    [Fact]
    public void A_window_above_4096_px_is_too_large_until_it_fits_again()
    {
        _capture.Otherwise = VrShotKind.TooLarge;
        _feed.Start();
        WaitFor(() => _feed.TooLarge);

        _capture.Otherwise = VrShotKind.Captured;
        _feed.MarkDirty(VrFrameStyle.None);

        WaitFor(() => !_feed.TooLarge);
    }

    [Fact]
    public void The_style_of_the_last_signal_goes_with_the_capture()
    {
        var style = new VrFrameStyle(0xA0141414, 4.4);
        _feed.MarkDirty(style);

        _feed.Start();
        WaitFor(() => !_capture.Captures.IsEmpty);

        _capture.Captures.First().Request.Style.Should().Be(style);
    }

    [Fact]
    public void Stop_returns_at_once_while_a_capture_hangs_and_the_thread_ends_after_it()
    {
        _capture.Block = new ManualResetEventSlim(false);
        _feed.Start();
        WaitFor(() => !_capture.Captures.IsEmpty);

        var watch = Stopwatch.StartNew();
        _feed.Stop();

        watch.ElapsedMilliseconds.Should().BeLessThan(100);
        _feed.IsRunning.Should().BeTrue("the capture still hangs");
        _capture.Block.Set();
        WaitFor(() => !_feed.IsRunning);

        // The capture thread frees its own GDI objects.
        _capture.Releases.Should().Equal(_capture.Captures.First().Thread);
    }

    [Fact]
    public void Starting_again_while_a_capture_hangs_keeps_one_capture_thread()
    {
        _capture.Block = new ManualResetEventSlim(false);
        _feed.Start();
        WaitFor(() => !_capture.Captures.IsEmpty);

        _feed.Stop();
        _feed.Start();
        _capture.Block.Set();
        Thread.Sleep(Settle);

        _feed.IsRunning.Should().BeTrue();
        _capture.MostAtOnce.Should().Be(1);
        _capture.Captures.Select(c => c.Thread).Distinct().Should().ContainSingle();
        _capture.Releases.Should().BeEmpty();
    }

    [Fact]
    public void Forced_captures_every_tick_without_a_signal()
    {
        // K7: the memory plateau and the flicker check need a picture on every tick, changed or not.
        StartAndSettle();
        var before = _capture.Captures.Count;

        _feed.Forced = true;
        Thread.Sleep(500);

        var forced = _capture.Captures.Skip(before).ToList();
        forced.Count.Should().BeGreaterThanOrEqualTo(4);
        forced.Should().OnlyContain(c => c.Request.Force);
    }

    [Fact]
    public void Every_window_logs_one_line_also_at_rest()
    {
        // K5: one line per window, so the log shows the rest (captures=0) and the memory (ws, gdi).
        using var feed = new Disposing(_capture.Feed(_pause, _log.Enqueue, TimeSpan.FromMilliseconds(250)));
        feed.Feed.Start();

        WaitFor(() => _log.Count(l => l.StartsWith("feed:", StringComparison.Ordinal)) >= 3);

        var lines = _log.Where(l => l.StartsWith("feed:", StringComparison.Ordinal)).ToList();
        lines[0].Should().Contain("captures=2").And.Contain("printWindow=").And.Contain("ws=").And.Contain("gdi=");
        lines[^1].Should().Contain("captures=0");
    }

    [Fact]
    public void A_PrintWindow_wait_above_100_ms_gets_a_line_of_its_own()
    {
        _capture.PrintMilliseconds = 511;

        _feed.Start();

        WaitFor(() => _log.Contains("PrintWindow waited 511.0 ms"));
    }

    private void StartAndSettle()
    {
        _feed.Start();
        Thread.Sleep(Settle);
        _capture.Captures.Should().HaveCount(2, "a start captures now and after the trailing delay");
    }

    private static void WaitFor(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "the capture thread should have got there");
            Thread.Sleep(5);
        }
    }

    private sealed class Disposing(VrFeed feed) : IDisposable
    {
        public VrFeed Feed { get; } = feed;

        public void Dispose()
        {
            Feed.Stop();
            WaitFor(() => !Feed.IsRunning);
        }
    }
}
