using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CargoPlan.App.Theme;

namespace CargoPlan.App.Vr;

/// <summary>
/// What the capture needs from the window's look (T14.2): the key, the window background of the Preferences as #AARRGGBB
/// (K9; null = no key), and the rounding of the root border in physical pixels (E1). Read on the UI thread with every
/// signal, so a change in the settings window reaches the next picture.
/// </summary>
internal readonly record struct VrFrameStyle(uint? Key, double CornerRadiusPixels)
{
    public static VrFrameStyle None { get; } = new(null, 0);

    /// <summary>UI thread only. The overlay's root border and the measuring window both draw WindowBackgroundBrush.</summary>
    public static VrFrameStyle Of(Window window)
    {
        uint? key = window.TryFindResource(ThemeResources.Keys.WindowBackgroundBrush) is SolidColorBrush { Color: var c }
            ? ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B
            : null;
        var radius = window.Content is Border border ? border.CornerRadius.TopLeft : 0;
        return new VrFrameStyle(key, radius * VisualTreeHelper.GetDpi(window).DpiScaleX);
    }
}
