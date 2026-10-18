using System.Windows;

namespace CargoPlan.App.Settings;

/// <summary>
/// Position and size of a window in WPF units (DIP). Its own record instead of <see cref="Rect"/>:
/// the serializer would also write Rect's computed properties (Right, Bottom, IsEmpty, ...).
/// </summary>
public sealed record WindowBounds(double Left, double Top, double Width, double Height)
{
    public Rect ToRect() => new(Left, Top, Width, Height);

    public static WindowBounds FromRect(Rect rect) => new(rect.Left, rect.Top, rect.Width, rect.Height);
}
