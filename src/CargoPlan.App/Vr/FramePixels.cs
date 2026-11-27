using System.Runtime.InteropServices;

namespace CargoPlan.App.Vr;

/// <summary>
/// What the capture thread does to every picture (T14.2, D131), pure and testable without a window. PrintWindow gives the
/// layered window as BGRA premultiplied on black with alpha 255 (S14.0 G2): the key gives the window background its alpha
/// back, the mask clears the rounded corners (E1 of the approval), and the copy for SteamVR swaps red and blue, because
/// SetOverlayRaw reads RGBA (S14.0 G1).
/// </summary>
internal static class FramePixels
{
    /// <summary>Per channel: ±1 is the background, ±2 is content (anti-aliasing next to it, Auflage 2 S14.0).</summary>
    public const int KeyTolerance = 1;

    /// <summary>
    /// Pixels in the background colour <paramref name="argb"/> (#AARRGGBB as settings.json writes it), as PrintWindow
    /// gives it premultiplied on black, get the background's alpha; their colour stays premultiplied, as in G5 (S14.0).
    /// </summary>
    public static void ApplyKey(Span<byte> bgra, uint argb)
    {
        var alpha = (int)(argb >> 24);
        var (r, g, b) = (Premultiplied(argb >> 16, alpha), Premultiplied(argb >> 8, alpha), Premultiplied(argb, alpha));
        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            if (Math.Abs(bgra[i] - b) <= KeyTolerance && Math.Abs(bgra[i + 1] - g) <= KeyTolerance && Math.Abs(bgra[i + 2] - r) <= KeyTolerance)
            {
                bgra[i + 3] = (byte)alpha;
            }
        }
    }

    /// <summary>Copies BGRA into RGBA for SetOverlayRaw; the source stays as it is for the next comparison.</summary>
    public static void CopyAsRgba(ReadOnlySpan<byte> bgra, Span<byte> rgba)
    {
        var from = MemoryMarshal.Cast<byte, uint>(bgra);
        var to = MemoryMarshal.Cast<byte, uint>(rgba);
        for (var i = 0; i < from.Length; i++)
        {
            // Little-endian: a pixel B, G, R, A reads as 0xAARRGGBB; red and blue change places, green and alpha stay.
            var p = from[i];
            to[i] = (p & 0xFF00FF00) | ((p & 0xFF) << 16) | ((p >> 16) & 0xFF);
        }
    }

    /// <summary>
    /// The pixels outside a rectangle with corners rounded by <paramref name="radius"/> (physical pixels): a pixel is outside
    /// when its centre lies outside the corner's quarter circle. One mask per window size, no cost per picture.
    /// </summary>
    public static int[] CornerMask(int width, int height, double radius)
    {
        radius = Math.Min(radius, Math.Min(width, height) / 2.0);
        if (radius <= 0)
        {
            return [];
        }

        HashSet<int> outside = [];
        var reach = (int)Math.Ceiling(radius);
        for (var y = 0; y < reach; y++)
        {
            for (var x = 0; x < reach; x++)
            {
                var (dx, dy) = (radius - (x + 0.5), radius - (y + 0.5));
                if (dx <= 0 || dy <= 0 || (dx * dx) + (dy * dy) <= radius * radius)
                {
                    continue;
                }

                var (right, bottom) = (width - 1 - x, height - 1 - y);
                outside.UnionWith([(y * width) + x, (y * width) + right, (bottom * width) + x, (bottom * width) + right]);
            }
        }

        return [.. outside.Order()];
    }

    /// <summary>The masked pixels turn clear: colour and alpha 0, premultiplied transparent.</summary>
    public static void ClearCorners(Span<byte> bgra, int[] mask)
    {
        var pixels = MemoryMarshal.Cast<byte, uint>(bgra);
        foreach (var i in mask)
        {
            pixels[i] = 0;
        }
    }

    private static int Premultiplied(uint channel, int alpha) => ((int)(channel & 0xFF) * alpha + 127) / 255;
}
