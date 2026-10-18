using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CargoPlan.App.ViewModels;
using CargoPlan.App.Win32;

namespace CargoPlan.App;

/// <summary>The overlay: shows the ship view and legend, applies the lock mode (click-through) to the native window.</summary>
public partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _viewModel;

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    // The HWND exists from SourceInitialized on; before that there is no native window to style.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Ticking a stop off at the terminal must not take the foreground from Star Citizen (D11, addendum T3.6).
        NativeMethods.PreventActivation(new WindowInteropHelper(this).Handle, ShowInTaskbar);
        ApplyLock(_viewModel.IsLocked);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OverlayViewModel.IsLocked))
        {
            ApplyLock(_viewModel.IsLocked);
        }
        else if (e.PropertyName == nameof(OverlayViewModel.IsOverlayHidden))
        {
            ApplyHidden(this, _viewModel.IsOverlayHidden);
        }
    }

    /// <summary>
    /// Hide keeps the window and its HWND (hotkeys stay registered), Show brings it back where it was; ShowActivated=False
    /// and WS_EX_NOACTIVATE keep the game in front. Code instead of a Visibility binding: Show() and Hide() set Visibility
    /// themselves, and the lock window needs the same call (D17).
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.window.hide
    /// </summary>
    internal static void ApplyHidden(Window window, bool hidden)
    {
        if (hidden)
        {
            window.Hide();
        }
        else
        {
            window.Show();
        }
    }

    /// <summary>Locked = click-through, see <see cref="NativeMethods.SetClickThrough"/>.</summary>
    private void ApplyLock(bool locked)
    {
        // Before the HWND check: the view must ignore wheel and drag even before the window shows.
        ShipView.IsNavigationEnabled = !locked;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0)
        {
            return; // Not shown yet; OnSourceInitialized applies the current state.
        }

        NativeMethods.SetClickThrough(hwnd, locked);
    }

    // Borderless window: drag on header, hint lines and border. The ship view (orbit) and the legend's check boxes
    // handle their own left clicks, so this never runs there (D15, D16).
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}
