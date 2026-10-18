using System.Windows;

namespace CargoPlan.App.Settings;

/// <summary>Brings saved bounds back on screen. Pure geometry in DIP like <see cref="LockWindowPlacement"/>, testable.</summary>
public static class WindowBoundsRestore
{
    /// <summary>
    /// Saved bounds can lie on an unplugged monitor or outside a lower resolution. Size first (at least
    /// <paramref name="minSize"/>, at most the screen), then pushed fully inside <paramref name="virtualScreen"/>.
    /// The whole virtual desktop, not the nearest monitor: same frame as the lock window (T3.2), and it never
    /// loses the window, only a gap between monitors of different heights could hide part of it.
    /// </summary>
    public static Rect Clamp(Rect saved, Rect virtualScreen, Size minSize)
    {
        // Min first, then Max: the minimum size wins over a tiny screen, WPF enforces MinWidth anyway.
        var width = Math.Max(Math.Min(saved.Width, virtualScreen.Width), minSize.Width);
        var height = Math.Max(Math.Min(saved.Height, virtualScreen.Height), minSize.Height);

        // Same order as LockWindowPlacement: the left/top edge wins. Math.Clamp would throw if min > max.
        var left = Math.Max(Math.Min(saved.Left, virtualScreen.Right - width), virtualScreen.Left);
        var top = Math.Max(Math.Min(saved.Top, virtualScreen.Bottom - height), virtualScreen.Top);

        return new Rect(left, top, width, height);
    }
}
