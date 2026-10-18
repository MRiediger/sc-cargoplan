using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CargoPlan.App.ViewModels;
using CargoPlan.App.Win32;

namespace CargoPlan.App;

/// <summary>The overlay: shows the plan text, applies the lock mode (click-through) to the native window.</summary>
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
    }

    /// <summary>Locked = click-through, see <see cref="NativeMethods.SetClickThrough"/>.</summary>
    private void ApplyLock(bool locked)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0)
        {
            return; // Not shown yet; OnSourceInitialized applies the current state.
        }

        NativeMethods.SetClickThrough(hwnd, locked);
    }

    // Borderless window: drag anywhere on the background.
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}
