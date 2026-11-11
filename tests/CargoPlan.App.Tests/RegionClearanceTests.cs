using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CargoPlan.App.Scanning;
using CargoPlan.App.Win32;
using CargoPlan.Ocr;

namespace CargoPlan.App.Tests;

// D100: our windows over the scan region go to Opacity 0 for the capture, only those, and always come back.
public class RegionClearanceTests
{
    [Fact]
    public void Only_a_window_over_the_region_turns_invisible_and_gets_its_own_opacity_back() => Sta.Run(() =>
    {
        var over = Shown(new Window { Opacity = 0.8 });
        var beside = Shown(new Window { Opacity = 0.9 });
        var region = RegionInside(over);
        MoveAway(beside, region);
        var clearance = new RegionClearance(over.Dispatcher, () => [over, beside]);

        using (var cleared = clearance.Clear(region))
        {
            cleared.Should().NotBeNull();
            over.Opacity.Should().Be(0);
            beside.Opacity.Should().Be(0.9, "it doesn't cut the region");
        }

        over.Opacity.Should().Be(0.8);
        beside.Opacity.Should().Be(0.9);
        over.Close();
        beside.Close();
    });

    [Fact]
    public void A_capture_that_throws_still_brings_the_window_back() => Sta.Run(() =>
    {
        var over = Shown(new Window { Opacity = 1 });
        var clearance = new RegionClearance(over.Dispatcher, () => [over]);

        var capture = () =>
        {
            using (clearance.Clear(RegionInside(over)))
            {
                throw new InvalidOperationException("capture failed");
            }
        };

        capture.Should().Throw<InvalidOperationException>();
        over.Opacity.Should().Be(1);
        over.Close();
    });

    [Fact]
    public void Without_a_window_over_the_region_nothing_happens() => Sta.Run(() =>
    {
        var beside = Shown(new Window { Opacity = 1 });
        var region = new PixelRect(0, 0, 10, 10);
        MoveAway(beside, region);

        new RegionClearance(beside.Dispatcher, () => [beside]).Clear(region).Should().BeNull();
        beside.Opacity.Should().Be(1);
        beside.Close();
    });

    private static Window Shown(Window window)
    {
        window.WindowStyle = WindowStyle.None;
        window.AllowsTransparency = true;
        window.Background = Brushes.Magenta;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Width = 200;
        window.Height = 150;
        window.Left = 100;
        window.Top = 100;
        window.Show();
        return window;
    }

    // A small region in the middle of the window, in physical pixels as the scanner has it.
    private static PixelRect RegionInside(Window window)
    {
        using var physical = NativeMethods.PhysicalPixelsScope.Enter();
        var bounds = NativeMethods.GetWindowBounds(new WindowInteropHelper(window).Handle)!.Value;
        return new PixelRect(bounds.X + (bounds.Width / 2) - 5, bounds.Y + (bounds.Height / 2) - 5, 10, 10);
    }

    // Far to the right of the region, in device-independent units WPF places by.
    private static void MoveAway(Window window, PixelRect region)
    {
        var scale = VisualTreeHelper.GetDpi(window).DpiScaleX;
        window.Left = ((region.X + region.Width) / scale) + 400;
        window.Top = region.Y / scale;
    }
}
