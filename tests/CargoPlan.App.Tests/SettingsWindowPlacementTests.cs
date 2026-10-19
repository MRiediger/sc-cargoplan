using System.Windows;

namespace CargoPlan.App.Tests;

public class SettingsWindowPlacementTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);
    private static readonly Size WindowSize = new(380, 600);

    [Fact]
    public void Opens_right_of_the_overlay()
    {
        Rect overlay = new(100, 200, 420, 460);

        var position = SettingsWindowPlacement.NextTo(overlay, WindowSize, Screen);

        position.Should().Be(new Point(overlay.Right + SettingsWindowPlacement.Gap, overlay.Top));
    }

    [Fact]
    public void Opens_left_when_the_right_side_is_too_narrow()
    {
        Rect overlay = new(1400, 200, 420, 460);

        var position = SettingsWindowPlacement.NextTo(overlay, WindowSize, Screen);

        position.Should().Be(new Point(overlay.Left - SettingsWindowPlacement.Gap - WindowSize.Width, overlay.Top));
    }

    [Fact]
    public void Stays_on_screen_when_neither_side_fits()
    {
        Rect overlay = new(100, 700, 1700, 300);

        var position = SettingsWindowPlacement.NextTo(overlay, WindowSize, Screen);

        (position.X + WindowSize.Width).Should().BeLessThanOrEqualTo(Screen.Right);
        (position.Y + WindowSize.Height).Should().BeLessThanOrEqualTo(Screen.Bottom);
        position.X.Should().BeGreaterThanOrEqualTo(Screen.Left);
    }
}
