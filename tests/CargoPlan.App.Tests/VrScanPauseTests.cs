using CargoPlan.App.Vr;
using CargoPlan.Ocr;

namespace CargoPlan.App.Tests;

// T14.2, K10: the whole scan capture counts as a pause of the feed, from clearing the region until it is given back.
public class VrScanPauseTests
{
    private readonly VrScanPause _pause = new();

    [Fact]
    public void The_pause_lasts_from_the_clearing_until_its_release()
    {
        var inner = new Released();
        var bracketed = _pause.Bracket(_ => inner);

        _pause.Active.Should().BeFalse();
        var held = bracketed(new PixelRect(0, 0, 10, 10));
        _pause.Active.Should().BeTrue();

        held!.Dispose();

        _pause.Active.Should().BeFalse();
        inner.Disposed.Should().BeTrue("the windows come back as before");
    }

    [Fact]
    public void Nothing_to_cloak_still_pauses_the_capture()
    {
        var held = _pause.Bracket(_ => null)(default);

        _pause.Active.Should().BeTrue();
        held!.Dispose();
        _pause.Active.Should().BeFalse();
    }

    [Fact]
    public void A_clearing_that_throws_ends_the_pause()
    {
        var bracketed = _pause.Bracket(_ => throw new InvalidOperationException("gone"));

        var act = () => bracketed(default);

        act.Should().Throw<InvalidOperationException>();
        _pause.Active.Should().BeFalse();
    }

    [Fact]
    public void A_second_dispose_releases_nothing_twice()
    {
        var first = _pause.Bracket(_ => null)(default)!;
        var second = _pause.Bracket(_ => null)(default)!;

        first.Dispose();
        first.Dispose();

        _pause.Active.Should().BeTrue("the second scan still holds it");
        second.Dispose();
        _pause.Active.Should().BeFalse();
    }

    private sealed class Released : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
