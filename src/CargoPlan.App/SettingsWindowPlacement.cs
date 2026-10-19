using System.Windows;
using CargoPlan.App.Settings;

namespace CargoPlan.App;

/// <summary>Where the settings window opens. Pure geometry in DIP like <see cref="LockWindowPlacement"/>, testable.</summary>
public static class SettingsWindowPlacement
{
    /// <summary>Space between overlay and settings window.</summary>
    public const double Gap = 8;

    /// <summary>
    /// Next to the overlay, top edges aligned, so the window covers neither the overlay nor, usually, the game in the
    /// middle of the screen: right if it fits, else left, else pushed on screen. Not saved (the editor's position isn't
    /// either, backlog).
    /// </summary>
    public static Point NextTo(Rect overlay, Size window, Rect virtualScreen)
    {
        Rect right = new(overlay.Right + Gap, overlay.Top, window.Width, window.Height);
        Rect left = new(overlay.Left - Gap - window.Width, overlay.Top, window.Width, window.Height);
        var side = right.Right <= virtualScreen.Right || left.Left < virtualScreen.Left ? right : left;

        return WindowBoundsRestore.Clamp(side, virtualScreen, window).TopLeft;
    }
}
