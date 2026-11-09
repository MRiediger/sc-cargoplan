namespace CargoPlan.App.Hotkeys;

/// <summary>
/// Hotkeys read from the key state instead of registered (D97): RegisterHotKey's WM_HOTKEY never arrives while Star Citizen
/// is in front, a key state query does (precedent D45). Pure: the key state comes in as a delegate, the timer, the
/// foreground window and the focus are <see cref="HotkeyService"/>'s job.
/// A hotkey fires on its key's press (up → down) while exactly its modifiers are down; holding fires nothing more.
/// </summary>
/// <param name="isKeyDown">Whether the virtual key is down now; GetAsyncKeyState in the app, a set of keys in tests.</param>
public sealed class HotkeyPoller(Func<uint, bool> isKeyDown)
{
    // Source: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    private const uint VK_SHIFT = 0x10;   // either Shift
    private const uint VK_CONTROL = 0x11; // either Ctrl
    private const uint VK_MENU = 0x12;    // either Alt
    private const uint VK_LWIN = 0x5B;    // the Windows keys have no shared code
    private const uint VK_RWIN = 0x5C;

    private readonly Dictionary<HotkeyAction, Hotkey> _hotkeys = [];

    // Actions whose key was down at the last poll, armed or not: a key held while switching to the game is no press.
    private readonly HashSet<HotkeyAction> _keyDown = [];

    /// <summary>
    /// Star Citizen or one of our windows in front, not recording, no text box of ours with the focus. Another program in
    /// front never: a polled key can't be taken from it, so "Ctrl+Alt+L" in Excel would switch the lock too.
    /// </summary>
    /// <param name="textInputFocused">Counts only with our own window in front: WPF may still name the editor cell after
    /// the switch to the game, where nobody types into it (K2).</param>
    public static bool IsArmed(bool suspended, bool textInputFocused, bool ownProcessInFront, string? foregroundProcessName) =>
        !suspended && (ownProcessInFront
            ? !textInputFocused
            : string.Equals(foregroundProcessName, AutoLockRule.GameProcessName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The hotkey of <paramref name="action"/>; null removes it. A key held now fires only on its next press: at the end of a
    /// recording the keys just recorded are still down.
    /// </summary>
    public void Set(HotkeyAction action, Hotkey? hotkey)
    {
        if (hotkey is null)
        {
            _hotkeys.Remove(action);
            _keyDown.Remove(action);
            return;
        }

        _hotkeys[action] = hotkey;
        if (isKeyDown(hotkey.VirtualKey))
        {
            _keyDown.Add(action);
        }
        else
        {
            _keyDown.Remove(action);
        }
    }

    /// <summary>Reads the keys once; returns what fires now, nothing while not <paramref name="armed"/>.</summary>
    public IReadOnlyList<HotkeyAction> Poll(bool armed)
    {
        List<HotkeyAction> fired = [];
        if (_hotkeys.Count == 0)
        {
            return fired;
        }

        var modifiers = ModifiersDown();
        foreach (var (action, hotkey) in _hotkeys)
        {
            var down = isKeyDown(hotkey.VirtualKey);
            var pressed = down && !_keyDown.Contains(action);
            if (down)
            {
                _keyDown.Add(action);
            }
            else
            {
                _keyDown.Remove(action);
            }

            // Exactly the saved modifiers, as RegisterHotKey did: Ctrl+Alt+Shift+L is not Ctrl+Alt+L.
            if (armed && pressed && modifiers == (hotkey.Modifiers & ~HotkeyModifiers.NoRepeat))
            {
                fired.Add(action);
            }
        }

        return fired;
    }

    private HotkeyModifiers ModifiersDown()
    {
        var modifiers = HotkeyModifiers.None;
        if (isKeyDown(VK_CONTROL))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (isKeyDown(VK_MENU))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (isKeyDown(VK_SHIFT))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (isKeyDown(VK_LWIN) || isKeyDown(VK_RWIN))
        {
            modifiers |= HotkeyModifiers.Win;
        }

        return modifiers;
    }
}
