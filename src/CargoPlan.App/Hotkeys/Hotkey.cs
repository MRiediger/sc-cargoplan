namespace CargoPlan.App.Hotkeys;

/// <summary>The fsModifiers values of RegisterHotKey, so a parsed hotkey goes to Win32 unchanged.</summary>
// Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,      // MOD_ALT
    Control = 0x0002,  // MOD_CONTROL
    Shift = 0x0004,    // MOD_SHIFT
    Win = 0x0008,      // MOD_WIN
    NoRepeat = 0x4000, // MOD_NOREPEAT: holding the keys fires once, not once per auto-repeat
}

/// <summary>What a hotkey does; the names in settings.json are toggleLock and toggleOverlay (D17).</summary>
public enum HotkeyAction
{
    /// <summary>Switches the manual lock, like the padlock.</summary>
    ToggleLock,

    /// <summary>Hides the overlay and shows it again.</summary>
    ToggleOverlay,
}

/// <summary>A key combination as RegisterHotKey takes it. Built by <see cref="HotkeyParser"/>.</summary>
/// <param name="VirtualKey">Win32 virtual-key code.</param>
public sealed record Hotkey(HotkeyModifiers Modifiers, uint VirtualKey)
{
    /// <summary>Canonical text, e.g. "Ctrl+Alt+L": modifiers in a fixed order, NoRepeat left out. Parses back.</summary>
    public override string ToString() => string.Join('+', [
        .. HotkeyParser.ModifierNames.Where(m => Modifiers.HasFlag(m.Flag)).Select(m => m.Name),
        HotkeyParser.KeyName(VirtualKey),
    ]);
}
