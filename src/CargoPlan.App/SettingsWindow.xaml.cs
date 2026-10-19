using System.Windows;
using System.Windows.Input;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App;

/// <summary>
/// The settings window (D30, D34): the state lives in <see cref="SettingsViewModel"/>, applying and saving in the
/// session. Owned by the overlay: closes with it, stays above it, hides with it and while auto-locked.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <param name="owner">Must already be shown: Owner rejects a window that was never shown.</param>
    public SettingsWindow(SettingsViewModel viewModel, OverlayViewModel overlay, Window owner)
    {
        InitializeComponent();
        DataContext = viewModel;
        Owner = owner;
        OverlayWindow.HideWithOverlay(this, overlay);
    }

    /// <summary>
    /// Esc closes, like X: there is nothing to cancel (D30). KeyDown, not PreviewKeyDown: an open combo box closes its list
    /// on Esc first and marks the key handled, so the window stays.
    /// </summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
