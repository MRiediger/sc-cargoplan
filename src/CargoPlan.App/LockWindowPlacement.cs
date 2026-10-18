using System.Windows;

namespace CargoPlan.App;

/// <summary>Where the lock window goes. Pure geometry in WPF units (DIP), no window needed, so it is testable.</summary>
public static class LockWindowPlacement
{
    /// <summary>Space between the overlay's right edge and the lock window.</summary>
    public const double Gap = 4;

    /// <summary>
    /// Right of the overlay's top-right corner, away from the resize grip (bottom right).
    /// Kept inside <paramref name="screen"/>; at the right edge it then covers the overlay's corner,
    /// which is fine because an owned window always stays above its owner.
    /// </summary>
    public static Point Compute(Rect overlay, Size lockSize, Rect screen)
    {
        var x = overlay.Right + Gap;
        var y = overlay.Top;

        // Min first, then Max: the left/top edge wins. Math.Clamp would throw if the lock were larger than the screen.
        x = Math.Max(Math.Min(x, screen.Right - lockSize.Width), screen.Left);
        y = Math.Max(Math.Min(y, screen.Bottom - lockSize.Height), screen.Top);

        return new Point(x, y);
    }
}
