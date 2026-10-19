using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App;

/// <summary>
/// The settings window (D30, D34): the state lives in <see cref="SettingsViewModel"/>, applying and saving in the
/// session. Owned by the overlay: closes with it, stays above it, hides with it and while auto-locked.
/// </summary>
public partial class SettingsWindow : Window
{
    // The colour picker that is open, if any: only one, a click elsewhere closes it (StaysOpen=False).
    private Popup? _openPicker;

    /// <param name="owner">Must already be shown: Owner rejects a window that was never shown.</param>
    public SettingsWindow(SettingsViewModel viewModel, OverlayViewModel overlay, Window owner)
    {
        InitializeComponent();
        DataContext = viewModel;
        Owner = owner;
        OverlayWindow.HideWithOverlay(this, overlay);

        // A popup is a window of its own and stays when this one hides. Hidden means Star Citizen just took the cursor:
        // the tractor-beam grab, and a colour pad left standing would catch exactly that click (T4.2c).
        IsVisibleChanged += (_, _) => ClosePicker();
    }

    /// <summary>
    /// Esc closes an open colour picker, otherwise the window, like X: there is nothing to cancel (D30). The picker is
    /// checked here because its key events bubble from the popup into this window, and with the focus on the swatch they
    /// start here anyway. KeyDown, not PreviewKeyDown: an open combo box closes its list on Esc first and marks the key
    /// handled, so the window stays.
    /// </summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (_openPicker is not null)
        {
            ClosePicker();
        }
        else
        {
            Close();
        }

        e.Handled = true;
    }

    private void OnPickerOpened(object? sender, EventArgs e) => _openPicker = (Popup?)sender;

    private void OnPickerClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _openPicker))
        {
            _openPicker = null;
        }
    }

    // IsOpen is bound to the swatch button both ways; SetCurrentValue keeps that binding.
    private void ClosePicker() => _openPicker?.SetCurrentValue(Popup.IsOpenProperty, false);
}
