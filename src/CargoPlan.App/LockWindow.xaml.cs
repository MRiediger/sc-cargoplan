using System.Windows;
using System.Windows.Interop;
using CargoPlan.App.ViewModels;
using CargoPlan.App.Win32;

namespace CargoPlan.App;

/// <summary>
/// Small always-clickable switch for the overlay's lock. Same view model as the overlay, owned by it:
/// closes with it and always stays above it. Follows the overlay's top-right corner.
/// </summary>
public partial class LockWindow : Window
{
    private readonly Window _overlay;

    /// <param name="overlay">Must already be shown: Owner rejects a window that was never shown.</param>
    public LockWindow(OverlayViewModel viewModel, Window overlay)
    {
        InitializeComponent();
        DataContext = viewModel;
        _overlay = overlay;
        Owner = overlay;
        overlay.LocationChanged += OnOverlayMoved;
        overlay.SizeChanged += OnOverlayMoved;
    }

    // The HWND exists from SourceInitialized on.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        PreventActivation();
        FollowOverlay();
    }

    protected override void OnClosed(EventArgs e)
    {
        _overlay.LocationChanged -= OnOverlayMoved;
        _overlay.SizeChanged -= OnOverlayMoved;
        base.OnClosed(e);
    }

    /// <summary>
    /// WS_EX_NOACTIVATE: a click reaches the button but doesn't make this window the foreground window,
    /// so Star Citizen keeps the foreground and the free cursor (D11, addendum T3.2).
    /// </summary>
    private void PreventActivation()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetExStyle(hwnd);
        NativeMethods.SetExStyle(hwnd, style | NativeMethods.WS_EX_NOACTIVATE);
    }

    private void OnOverlayMoved(object? sender, EventArgs e) => FollowOverlay();

    private void FollowOverlay()
    {
        Rect overlay = new(_overlay.Left, _overlay.Top, _overlay.ActualWidth, _overlay.ActualHeight);
        Rect screen = new(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        var position = LockWindowPlacement.Compute(overlay, new Size(Width, Height), screen);
        Left = position.X;
        Top = position.Y;
    }
}
