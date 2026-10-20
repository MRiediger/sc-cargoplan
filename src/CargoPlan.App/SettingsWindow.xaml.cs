using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CargoPlan.App.Hotkeys;
using CargoPlan.App.Settings;
using CargoPlan.App.Theme;
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

    private readonly HotkeysViewModel _hotkeys;

    /// <param name="hotkeys">The Hotkeys section's own data context: hotkeys are no part of the look (D35).</param>
    /// <param name="theme">The tool windows' mode (D36); the overlay switches it later through <see cref="ToolWindowThemer"/>.</param>
    /// <param name="owner">Must already be shown: Owner rejects a window that was never shown.</param>
    public SettingsWindow(SettingsViewModel viewModel, HotkeysViewModel hotkeys, OverlayViewModel overlay, ToolWindowTheme theme, Window owner)
    {
        // Before InitializeComponent, so the own styles in the XAML find Fluent's with BasedOn (D36).
        ToolWindowThemer.Apply(this, theme);
        InitializeComponent();
        DataContext = viewModel;
        _hotkeys = hotkeys;
        HotkeysSection.DataContext = hotkeys;
        Owner = owner;
        OverlayWindow.HideWithOverlay(this, overlay);

        // A popup is a window of its own and stays when this one hides. Hidden means Star Citizen just took the cursor:
        // the tractor-beam grab, and a colour pad left standing would catch exactly that click (T4.2c). A recording
        // ends too, or our hotkeys would stay suspended in flight (D35).
        IsVisibleChanged += (_, _) =>
        {
            ClosePicker();
            CancelRecording();
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        CancelRecording();
        base.OnClosed(e);
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

    /// <summary>
    /// While a hotkey records, every key belongs to it: no Esc closing the window, no Space pressing the button. Esc
    /// cancels; a modifier alone waits for the key after it.
    /// </summary>
    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: HotkeyEditor { IsRecording: true } editor })
        {
            return;
        }

        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            editor.Cancel();
        }
        else if (RecordedKey.From(e.Key, e.SystemKey, Keyboard.Modifiers) is var (modifiers, virtualKey))
        {
            editor.Capture(modifiers, virtualKey);
        }
    }

    // Clicked elsewhere, Alt+Tab, window hidden: the recording ends and our hotkeys come back.
    private void OnHotkeyLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HotkeyEditor editor })
        {
            editor.Cancel();
        }
    }

    private void CancelRecording()
    {
        _hotkeys.Lock.Cancel();
        _hotkeys.Overlay.Cancel();
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
