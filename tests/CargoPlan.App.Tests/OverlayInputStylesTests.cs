using System.Windows;
using System.Windows.Interop;
using CargoPlan.App.ViewModels;
using CargoPlan.App.Win32;

namespace CargoPlan.App.Tests;

/// <summary>
/// The overlay's ex-styles from lock and tab (D11 addendum T13.3a), on a real HWND: locked always click-through and never
/// activated; unlocked on the Cargo tab clickable without activation as since T3.6; unlocked on the Plan tab clickable
/// and activated by a click, so the scroll bars get capture and wheel over the game. Never SetForegroundWindow.
/// </summary>
public class OverlayInputStylesTests
{
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, true)]
    public void Lock_and_tab_set_the_overlays_ex_styles(bool locked, bool planTab, bool transparent, bool noActivate) => Sta.Run(() =>
    {
        var window = new Window { AllowsTransparency = true, WindowStyle = WindowStyle.None, ShowInTaskbar = true };
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        NativeMethods.PreventActivation(hwnd, showInTaskbar: true); // as the overlay's OnSourceInitialized
        try
        {
            // From the opposite state first: every bit has to be switched, both ways.
            NativeMethods.SetOverlayInput(hwnd, !locked, !planTab);
            NativeMethods.SetOverlayInput(hwnd, locked, planTab);

            var style = NativeMethods.GetExStyle(hwnd);
            ((style & NativeMethods.WS_EX_TRANSPARENT) != 0).Should().Be(transparent, "click-through only while locked");
            ((style & NativeMethods.WS_EX_NOACTIVATE) != 0).Should().Be(noActivate, "activated by a click only unlocked on the Plan tab");
            ((style & NativeMethods.WS_EX_LAYERED) != 0).Should().BeTrue("WPF owns it for AllowsTransparency (D11)");
            ((style & NativeMethods.WS_EX_APPWINDOW) != 0).Should().BeTrue("the taskbar button stays");
        }
        finally
        {
            window.Close();
        }
    });

    [Theory]
    [InlineData(nameof(OverlayViewModel.IsLocked), true)]
    [InlineData(nameof(OverlayViewModel.IsRoutesTabSelected), true)]
    [InlineData(nameof(OverlayViewModel.IsManuallyLocked), false)]
    [InlineData(nameof(OverlayViewModel.IsOverlayHidden), false)]
    [InlineData(null, false)]
    public void Lock_and_tab_changes_apply_the_styles_again(string? property, bool applies) =>
        OverlayWindow.AffectsInputStyles(property).Should().Be(applies);
}
