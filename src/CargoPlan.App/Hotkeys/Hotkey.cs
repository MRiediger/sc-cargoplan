namespace CargoPlan.App.Hotkeys;

/// <summary>
/// Modifiers of a hotkey. The values are RegisterHotKey's fsModifiers from before D97; settings.json stores the text
/// ("Ctrl+Alt+L"), so they never leave the process.
/// </summary>
// Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,      // MOD_ALT
    Control = 0x0002,  // MOD_CONTROL
    Shift = 0x0004,    // MOD_SHIFT
    Win = 0x0008,      // MOD_WIN
}

/// <summary>
/// What a hotkey does; the names in settings.json are toggleLock, toggleOverlay, replan, discardPlan, undo, redo,
/// resetView and scanContracts (D17, D66, K2.3, K2.4, K1.3, D104).
/// </summary>
public enum HotkeyAction
{
    /// <summary>Switches the manual lock, like the padlock.</summary>
    ToggleLock,

    /// <summary>Hides the overlay and shows it again.</summary>
    ToggleOverlay,

    /// <summary>Lays out the cargo on board anew, like the editor's Re-plan button (D66).</summary>
    Replan,

    /// <summary>Empties the plan, so the next scan starts a new one, like the editor's Discard plan button (K2.3).</summary>
    DiscardPlan,

    /// <summary>Goes back one step, like the header's ↶ (K2.4).</summary>
    Undo,

    /// <summary>Goes forward one undone step, like the header's ↷ (K2.4).</summary>
    Redo,

    /// <summary>Fits the ship view from the ship's default camera, like the header's reset or a double-click (K1.3).</summary>
    ResetView,

    /// <summary>Reads the contract open in the mobiGlas into the plan, like the editor's Scan button (T10.3, D104).</summary>
    ScanContracts,
}

/// <summary>A key combination: modifiers plus one key. Built by <see cref="HotkeyParser"/>, polled by <see cref="HotkeyPoller{TAction}"/>.</summary>
/// <param name="VirtualKey">Win32 virtual-key code.</param>
public sealed record Hotkey(HotkeyModifiers Modifiers, uint VirtualKey)
{
    /// <summary>Canonical text, e.g. "Ctrl+Alt+L" or "F3": modifiers in a fixed order. Parses back.</summary>
    public override string ToString() => string.Join('+', [
        .. HotkeyParser.ModifierNames.Where(m => Modifiers.HasFlag(m.Flag)).Select(m => m.Name),
        HotkeyParser.KeyName(VirtualKey),
    ]);
}
