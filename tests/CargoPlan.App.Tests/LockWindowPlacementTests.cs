using System.Windows;

namespace CargoPlan.App.Tests;

public class LockWindowPlacementTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);
    private static readonly Size LockSize = new(36, 36);

    [Fact]
    public void Sits_above_the_overlay_right_aligned()
    {
        Rect overlay = new(100, 200, 520, 200);

        var position = LockWindowPlacement.Compute(overlay, LockSize, Screen);

        position.Should().Be(new Point(overlay.Right - LockSize.Width, overlay.Top - LockWindowPlacement.Gap - LockSize.Height));
    }

    [Fact]
    public void Stays_above_the_overlay_when_it_touches_the_right_edge()
    {
        Rect overlay = new(Screen.Right - 520, 200, 520, 200);

        var position = LockWindowPlacement.Compute(overlay, LockSize, Screen);

        position.X.Should().Be(Screen.Right - LockSize.Width);
        (position.Y + LockSize.Height).Should().BeLessThanOrEqualTo(overlay.Top);
    }

    [Fact]
    public void Stays_on_screen_when_the_overlay_hangs_over_the_right_edge()
    {
        Rect overlay = new(Screen.Right - 100, 200, 520, 200);

        var position = LockWindowPlacement.Compute(overlay, LockSize, Screen);

        (position.X + LockSize.Width).Should().BeLessThanOrEqualTo(Screen.Right);
    }

    [Fact]
    public void Stays_on_screen_when_the_overlay_touches_the_top_edge()
    {
        // No room above: the lock moves down onto the overlay's corner.
        Rect overlay = new(100, 0, 520, 200);

        var position = LockWindowPlacement.Compute(overlay, LockSize, Screen);

        position.Y.Should().Be(Screen.Top);
    }

    [Fact]
    public void Respects_a_virtual_screen_that_does_not_start_at_zero()
    {
        // Second monitor left of the primary: the virtual screen starts at negative X.
        // The overlay hangs over its left edge so far that even its right side is off screen.
        Rect screen = new(-1920, 0, 3840, 1080);
        Rect overlay = new(-2500, 200, 520, 200);

        var position = LockWindowPlacement.Compute(overlay, LockSize, screen);

        position.X.Should().Be(screen.Left);
    }
}
