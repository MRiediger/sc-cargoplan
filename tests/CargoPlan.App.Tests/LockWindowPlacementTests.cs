using System.Windows;

namespace CargoPlan.App.Tests;

public class LockWindowPlacementTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);
    private static readonly Size LockSize = new(36, 36);

    [Fact]
    public void Sits_inside_the_overlay_top_right_corner()
    {
        Rect overlay = new(100, 200, 520, 200);

        var position = LockWindowPlacement.Compute(overlay, LockSize, Screen);

        position.Should().Be(new Point(
            overlay.Right - LockWindowPlacement.Inset - LockSize.Width,
            overlay.Top + LockWindowPlacement.Inset));
    }

    [Fact]
    public void Stays_in_the_corner_when_the_overlay_touches_the_right_edge()
    {
        Rect overlay = new(Screen.Right - 520, 200, 520, 200);

        var position = LockWindowPlacement.Compute(overlay, LockSize, Screen);

        position.X.Should().Be(overlay.Right - LockWindowPlacement.Inset - LockSize.Width);
    }

    [Fact]
    public void Stays_on_screen_when_the_overlay_hangs_over_the_right_edge()
    {
        Rect overlay = new(Screen.Right - 100, 200, 520, 200);

        var position = LockWindowPlacement.Compute(overlay, LockSize, Screen);

        (position.X + LockSize.Width).Should().BeLessThanOrEqualTo(Screen.Right);
    }

    [Fact]
    public void Stays_on_screen_when_the_overlay_is_dragged_above_the_top_edge()
    {
        Rect overlay = new(100, -50, 520, 200);

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
