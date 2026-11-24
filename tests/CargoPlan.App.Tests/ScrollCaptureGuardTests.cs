namespace CargoPlan.App.Tests;

/// <summary>
/// The scroll bars' thumb and track buttons let go when the button is up though no WM_LBUTTONUP came (T13.3a): over the
/// game the release can go to the game. As the ship view's drags (D45): a "not down" counts only after a "down", since the
/// call fails towards an elevated game and then always says "not down".
/// </summary>
public class ScrollCaptureGuardTests
{
    [Fact]
    public void Up_after_down_releases_the_capture()
    {
        var watch = new ScrollCaptureGuard.ButtonWatch();

        watch.ShouldRelease(buttonDown: true).Should().BeFalse("held");
        watch.ShouldRelease(buttonDown: true).Should().BeFalse("still held");
        watch.ShouldRelease(buttonDown: false).Should().BeTrue("released without the message");
    }

    [Fact]
    public void Up_before_any_down_keeps_the_capture()
    {
        // GetAsyncKeyState fails towards an elevated window: false forever, and the thumb would never drag.
        var watch = new ScrollCaptureGuard.ButtonWatch();

        watch.ShouldRelease(buttonDown: false).Should().BeFalse();
        watch.ShouldRelease(buttonDown: false).Should().BeFalse();
        watch.ShouldRelease(buttonDown: true).Should().BeFalse();
        watch.ShouldRelease(buttonDown: false).Should().BeTrue();
    }
}
