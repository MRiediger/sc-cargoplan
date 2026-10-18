using System.Windows;
using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

public class WindowBoundsRestoreTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);
    private static readonly Size MinSize = new(200, 100);

    [Fact]
    public void Bounds_inside_the_screen_stay_unchanged()
    {
        Rect saved = new(100, 200, 520, 200);

        WindowBoundsRestore.Clamp(saved, Screen, MinSize).Should().Be(saved);
    }

    [Fact]
    public void Bounds_over_the_right_and_bottom_edge_are_pushed_inside()
    {
        Rect saved = new(1800, 1000, 520, 200);

        WindowBoundsRestore.Clamp(saved, Screen, MinSize)
            .Should().Be(new Rect(Screen.Right - 520, Screen.Bottom - 200, 520, 200));
    }

    [Fact]
    public void Bounds_on_an_unplugged_monitor_are_clamped_to_the_nearest_edge()
    {
        // Saved on a second monitor right of the primary, which is gone now.
        Rect saved = new(9999, 300, 520, 200);

        WindowBoundsRestore.Clamp(saved, Screen, MinSize)
            .Should().Be(new Rect(Screen.Right - 520, 300, 520, 200));
    }

    [Fact]
    public void Bounds_above_and_left_of_the_screen_are_clamped_to_the_top_left()
    {
        Rect saved = new(-800, -500, 520, 200);

        WindowBoundsRestore.Clamp(saved, Screen, MinSize).Should().Be(new Rect(0, 0, 520, 200));
    }

    [Fact]
    public void Too_small_bounds_grow_to_the_minimum_size()
    {
        Rect saved = new(100, 200, 10, 5);

        WindowBoundsRestore.Clamp(saved, Screen, MinSize).Should().Be(new Rect(100, 200, 200, 100));
    }

    [Fact]
    public void Bounds_larger_than_the_screen_are_limited_to_the_screen()
    {
        // Saved at 4K, restarted at Full HD.
        Rect saved = new(100, 100, 3000, 2000);

        WindowBoundsRestore.Clamp(saved, Screen, MinSize).Should().Be(Screen);
    }

    [Fact]
    public void Respects_a_virtual_screen_that_does_not_start_at_zero()
    {
        // Second monitor left of the primary: the virtual screen starts at negative X.
        Rect screen = new(-1920, 0, 3840, 1080);
        Rect saved = new(-2500, 200, 520, 200);

        WindowBoundsRestore.Clamp(saved, screen, MinSize).Should().Be(new Rect(-1920, 200, 520, 200));
    }
}
