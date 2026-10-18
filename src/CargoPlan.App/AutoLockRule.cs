namespace CargoPlan.App;

/// <summary>When the overlay locks itself. Pure decision, no Win32 needed, so it is testable (D11, addendum T3.3).</summary>
public static class AutoLockRule
{
    /// <summary>Star Citizen's process name as <see cref="System.Diagnostics.Process.ProcessName"/> returns it: without ".exe".</summary>
    public const string GameProcessName = "StarCitizen";

    /// <summary>
    /// Lock while Star Citizen is in the foreground and has hidden the cursor: the hidden cursor follows the
    /// view, and a click over the overlay would be lost to the game (tractor beam). Both signals are needed:
    /// the game alone shows the cursor at terminals, other programs hide it too (typing in a terminal).
    /// </summary>
    public static bool ShouldLock(string? foregroundProcessName, bool cursorShowing) =>
        !cursorShowing && string.Equals(foregroundProcessName, GameProcessName, StringComparison.OrdinalIgnoreCase);
}
