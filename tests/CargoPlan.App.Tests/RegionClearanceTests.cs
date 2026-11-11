using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CargoPlan.App.Scanning;
using CargoPlan.App.Win32;
using CargoPlan.Ocr;

namespace CargoPlan.App.Tests;

// D100, B12: our windows over the scan region are cloaked for the capture, only those, one per owner chain, and always
// come back. The cloak itself is a fake that writes down its calls: the rule is logic, DWM is measured by hand
// (OverlayCaptureTests.Cloak).
public class RegionClearanceTests
{
    [Fact]
    public void Only_a_window_over_the_region_is_cloaked_and_comes_back() => Sta.Run(() =>
    {
        var over = Shown(Layered());
        var beside = Shown(Layered());
        var region = RegionInside(over);
        MoveAway(beside, region);
        var calls = new List<(nint, bool)>();
        var clearance = new RegionClearance(over.Dispatcher, () => [over, beside], (h, c) => calls.Add((h, c)));

        using (var cleared = clearance.Clear(region))
        {
            cleared.Should().NotBeNull();
            calls.Should().Equal((Handle(over), true));
        }

        calls.Should().Equal((Handle(over), true), (Handle(over), false));
        over.Close();
        beside.Close();
    });

    [Fact]
    public void A_window_with_a_title_bar_is_cloaked_too() => Sta.Run(() =>
    {
        // Opacity 0 didn't reach it (B12): no AllowsTransparency, the system title bar stayed in the picture.
        var chrome = Shown(new Window { WindowStyle = WindowStyle.SingleBorderWindow });
        var calls = new List<(nint, bool)>();
        var clearance = new RegionClearance(chrome.Dispatcher, () => [chrome], (h, c) => calls.Add((h, c)));

        using (clearance.Clear(RegionInside(chrome)))
        {
            calls.Should().Equal((Handle(chrome), true));
        }

        chrome.Opacity.Should().Be(1, "the cloak replaces the opacity, nothing else changes on the window");
        chrome.Close();
    });

    [Fact]
    public void A_capture_that_throws_still_brings_the_window_back() => Sta.Run(() =>
    {
        var over = Shown(Layered());
        var calls = new List<(nint, bool)>();
        var clearance = new RegionClearance(over.Dispatcher, () => [over], (h, c) => calls.Add((h, c)));

        var capture = () =>
        {
            using (clearance.Clear(RegionInside(over)))
            {
                throw new InvalidOperationException("capture failed");
            }
        };

        capture.Should().Throw<InvalidOperationException>();
        calls.Should().Equal((Handle(over), true), (Handle(over), false));
        over.Close();
    });

    [Fact]
    public void Without_a_window_over_the_region_nothing_happens() => Sta.Run(() =>
    {
        var beside = Shown(Layered());
        var region = new PixelRect(0, 0, 10, 10);
        MoveAway(beside, region);
        var calls = new List<(nint, bool)>();

        new RegionClearance(beside.Dispatcher, () => [beside], (h, c) => calls.Add((h, c))).Clear(region).Should().BeNull();
        calls.Should().BeEmpty();
        beside.Close();
    });

    [Fact]
    public void A_hidden_window_over_the_region_is_left_alone() => Sta.Run(() =>
    {
        var over = Shown(Layered());
        var region = RegionInside(over);
        over.Hide();
        var calls = new List<(nint, bool)>();

        new RegionClearance(over.Dispatcher, () => [over], (h, c) => calls.Add((h, c))).Clear(region).Should().BeNull();
        calls.Should().BeEmpty();
        over.Close();
    });

    [Fact]
    public void Owner_and_owned_window_both_over_the_region_cloak_only_the_owner() => Sta.Run(() =>
    {
        // DWM hides the owned window with its owner, and uncloaking it while the owner is cloaked fails with
        // E_INVALIDARG (B12, R5c, R5d): one cloak per owner chain.
        var owner = Shown(Layered());
        var owned = Shown(Layered(), owner);
        var calls = new List<(nint, bool)>();
        var clearance = new RegionClearance(owner.Dispatcher, () => [owner, owned], (h, c) => calls.Add((h, c)));

        using (clearance.Clear(RegionInside(owner)))
        {
            calls.Should().Equal((Handle(owner), true));
        }

        calls.Should().Equal((Handle(owner), true), (Handle(owner), false));
        owned.Close();
        owner.Close();
    });

    [Fact]
    public void An_owned_window_over_the_region_with_its_owner_beside_is_cloaked_itself() => Sta.Run(() =>
    {
        var owner = Shown(Layered());
        var owned = Shown(Layered(), owner);
        var region = RegionInside(owned);
        MoveAway(owner, region);
        var calls = new List<(nint, bool)>();
        var clearance = new RegionClearance(owner.Dispatcher, () => [owner, owned], (h, c) => calls.Add((h, c)));

        using (clearance.Clear(region))
        {
            calls.Should().Equal((Handle(owned), true));
        }

        calls.Should().Equal((Handle(owned), true), (Handle(owned), false));
        owned.Close();
        owner.Close();
    });

    [Fact]
    public void An_owned_window_over_the_region_with_its_owner_hidden_is_cloaked_itself() => Sta.Run(() =>
    {
        // Hiding the owner leaves the owned window on screen; a hidden owner isn't cloaked, so nothing is inherited.
        var owner = Shown(Layered());
        var owned = Shown(Layered(), owner);
        var region = RegionInside(owned);
        owner.Hide();
        var calls = new List<(nint, bool)>();
        var clearance = new RegionClearance(owner.Dispatcher, () => [owner, owned], (h, c) => calls.Add((h, c)));

        using (clearance.Clear(region))
        {
            calls.Should().Equal((Handle(owned), true));
        }

        calls.Should().Equal((Handle(owned), true), (Handle(owned), false));
        owned.Close();
        owner.Close();
    });

    [Fact]
    public void A_window_closed_during_the_capture_is_passed_over() => Sta.Run(() =>
    {
        var over = Shown(Layered());
        var handle = Handle(over);
        var calls = new List<(nint, bool)>();
        var clearance = new RegionClearance(over.Dispatcher, () => [over], (h, c) => calls.Add((h, c)));

        var capture = () =>
        {
            using (clearance.Clear(RegionInside(over)))
            {
                over.Close();
            }
        };

        capture.Should().NotThrow();
        calls.Should().Equal((handle, true));
    });

    [Fact]
    public void A_second_clear_before_the_first_is_disposed_cloaks_nothing_twice() => Sta.Run(() =>
    {
        var over = Shown(Layered());
        var region = RegionInside(over);
        var calls = new List<(nint, bool)>();
        var clearance = new RegionClearance(over.Dispatcher, () => [over], (h, c) => calls.Add((h, c)));

        using (clearance.Clear(region))
        {
            clearance.Clear(region).Should().BeNull("the window is ours already, the second has nothing to bring back");
            calls.Should().Equal((Handle(over), true));
        }

        calls.Should().Equal((Handle(over), true), (Handle(over), false));
        over.Close();
    });

    // Like the overlay: layered, no title bar.
    private static Window Layered() => new() { WindowStyle = WindowStyle.None, AllowsTransparency = true };

    private static Window Shown(Window window, Window? owner = null)
    {
        window.Background = Brushes.Magenta;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Width = 200;
        window.Height = 150;
        window.Left = 100;
        window.Top = 100;
        window.Owner = owner; // WPF wants the owner shown first
        window.Show();
        return window;
    }

    private static nint Handle(Window window) => new WindowInteropHelper(window).Handle;

    // A small region in the middle of the window, in physical pixels as the scanner has it.
    private static PixelRect RegionInside(Window window)
    {
        using var physical = NativeMethods.PhysicalPixelsScope.Enter();
        var bounds = NativeMethods.GetWindowBounds(Handle(window))!.Value;
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
