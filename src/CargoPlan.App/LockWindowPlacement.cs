using System.Windows;

namespace CargoPlan.App;

/// <summary>Where the lock window goes. Pure geometry in WPF units (DIP), no window needed, so it is testable.</summary>
public static class LockWindowPlacement
{
    /// <summary>Distance from the overlay's top and right edges to the lock window.</summary>
    public const double Inset = 4;

    /// <summary>
    /// Inside the overlay's top-right corner, away from the resize grip (bottom right). A window of its
    /// own, not a button in the overlay: the locked overlay passes every click through (D11).
    /// An owned window always stays above its owner, so the corner can't cover it.
    /// Kept inside <paramref name="screen"/> for an overlay dragged partly off screen.
    /// </summary>
    public static Point Compute(Rect overlay, Size lockSize, Rect screen)
    {
        var x = overlay.Right - Inset - lockSize.Width;
        var y = overlay.Top + Inset;

        // Min first, then Max: the left/top edge wins. Math.Clamp would throw if the lock were larger than the screen.
        x = Math.Max(Math.Min(x, screen.Right - lockSize.Width), screen.Left);
        y = Math.Max(Math.Min(y, screen.Bottom - lockSize.Height), screen.Top);

        return new Point(x, y);
    }
}
