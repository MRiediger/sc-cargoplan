namespace CargoPlan.Ocr;

/// <summary>
/// Where the contract is read in a game window (S10.0): one region relative to the centred 16:9 area, for both mobiGlas
/// views (OFFERS, ACCEPTED). Measured at 3440×1440 as [1300, 210, 1560, 1000] with the area at x 440…3000, and it hit
/// 1920×1080 at [645, 157, 1170, 750] without adjusting. 16:10 and 32:9 follow the formula but are unchecked (T10.5).
/// </summary>
public static class ScanRegion
{
    // In fractions of the 16:9 area, exact for both measurements: left 860/2560, top 210/1440, right (860 + 1560)/2560,
    // bottom (210 + 1000)/1440.
    private const double Left = 43.0 / 128;
    private const double Top = 7.0 / 48;
    private const double Right = 121.0 / 128;
    private const double Bottom = 121.0 / 144;

    /// <summary>The region in a window's client area of <paramref name="width"/> × <paramref name="height"/> pixels.</summary>
    public static PixelRect Of(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        // The game keeps the mobiGlas inside a centred 16:9 area: limited by the height on wide screens, by the width
        // on narrow ones.
        var areaWidth = Math.Min(width, height * 16.0 / 9);
        var areaHeight = areaWidth * 9 / 16;
        var areaX = (width - areaWidth) / 2;
        var areaY = (height - areaHeight) / 2;

        // Both edges rounded down: 1080p's top is 157.5 and its bottom 907.5, measured 157 and 907.
        var left = (int)Math.Floor(areaX + areaWidth * Left);
        var top = (int)Math.Floor(areaY + areaHeight * Top);
        var right = (int)Math.Floor(areaX + areaWidth * Right);
        var bottom = (int)Math.Floor(areaY + areaHeight * Bottom);
        return new(left, top, right - left, bottom - top);
    }
}
