using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using CargoPlan.App.ViewModels;
using CargoPlan.App.Win32;

namespace CargoPlan.App;

/// <summary>
/// Small always-clickable switch for the overlay's lock. Same view model as the overlay, owned by it:
/// closes with it and always stays above it. Sits inside the overlay's top-right corner and follows it.
/// </summary>
public partial class LockWindow : Window
{
    private readonly OverlayViewModel _viewModel;
    private readonly Window _overlay;

    /// <param name="overlay">Must already be shown: Owner rejects a window that was never shown.</param>
    public LockWindow(OverlayViewModel viewModel, Window overlay)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _overlay = overlay;
        Owner = overlay;
        overlay.LocationChanged += OnOverlayMoved;
        overlay.SizeChanged += OnOverlayMoved;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    // The HWND exists from SourceInitialized on.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeMethods.PreventActivation(new WindowInteropHelper(this).Handle, showInTaskbar: false);
        ApplyAutoLock();
        FollowOverlay();
    }

    protected override void OnClosed(EventArgs e)
    {
        _overlay.LocationChanged -= OnOverlayMoved;
        _overlay.SizeChanged -= OnOverlayMoved;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OverlayViewModel.IsAutoLocked))
        {
            ApplyAutoLock();
        }
        else if (e.PropertyName == nameof(OverlayViewModel.IsOverlayHidden))
        {
            // Hiding an owner leaves its owned windows visible (only minimizing hides them), so the lock hides itself.
            // Source: https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#owned-windows
            OverlayWindow.ApplyHidden(this, _viewModel.IsOverlayHidden);
        }
    }

    /// <summary>
    /// Click-through only while auto-locked: the hidden cursor follows the view and could sit over this
    /// window too, catching the tractor-beam click (D11). Nobody can aim at the button with a hidden cursor,
    /// and once the cursor shows, the button is clickable again, so the way back stays open.
    /// The manual lock never makes it click-through.
    /// </summary>
    private void ApplyAutoLock()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0)
        {
            return; // Not shown yet; OnSourceInitialized applies the current state.
        }

        NativeMethods.SetClickThrough(hwnd, _viewModel.IsAutoLocked);
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
