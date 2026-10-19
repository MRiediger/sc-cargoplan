using System.Windows.Input;

namespace CargoPlan.App.Hotkeys;

/// <summary>
/// A key pressed in the settings window, as <see cref="HotkeyParser.TryCreate"/> takes it (T4.4). A normal WPF key event
/// in our own focused window: no hook, nothing that watches other programs (D17).
/// </summary>
public static class RecordedKey
{
    private static readonly HashSet<Key> ModifierKeysAlone =
        [Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin];

    /// <param name="key">KeyEventArgs.Key; Key.System while Alt is held without Ctrl.</param>
    /// <param name="systemKey">KeyEventArgs.SystemKey: the real key behind Key.System.</param>
    /// <param name="modifiers">Keyboard.Modifiers at the moment of the key.</param>
    /// <returns>null for a modifier key alone: the recording waits for the key after it.</returns>
    // Sources: https://learn.microsoft.com/dotnet/api/system.windows.input.keyeventargs.systemkey,
    // https://learn.microsoft.com/dotnet/api/system.windows.input.keyinterop.virtualkeyfromkey
    public static (HotkeyModifiers Modifiers, uint VirtualKey)? From(Key key, Key systemKey, ModifierKeys modifiers)
    {
        var real = key == Key.System ? systemKey : key;
        if (ModifierKeysAlone.Contains(real))
        {
            return null;
        }

        var flags = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            flags |= HotkeyModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            flags |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            flags |= HotkeyModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            flags |= HotkeyModifiers.Win;
        }

        return (flags, (uint)KeyInterop.VirtualKeyFromKey(real));
    }
}
