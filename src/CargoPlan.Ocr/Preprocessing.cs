using SkiaSharp;

namespace CargoPlan.Ocr;

/// <summary>
/// What happens to the region before the OCR reads it (S10.0): "up2-gray", twice the size and grey. At 1080p it read
/// 98.1 % of the fields where the raw region read 94.9 % (small print: two max sizes, one delivery), and with
/// parallelism 8 it was the fastest variant. A constant, not a setting.
/// </summary>
public static class Preprocessing
{
    private const int Scale = 2;

    /// <summary>
    /// A new bitmap, twice as wide and high, one grey byte per pixel. RapidOcrNet reads Gray8 as it is
    /// (<c>OcrUtils.SubtractMeanNormalize</c> has a Gray8 branch; source:
    /// https://github.com/BobLd/RapidOcrNet/blob/master/RapidOcrNet/OcrUtils.cs). The caller disposes it.
    /// </summary>
    public static SKBitmap Up2Gray(SKBitmap source)
    {
        // Resize scales and converts in one pass into the target's colour type; linear filtering, the spike's resampler
        // isn't recorded. Source: https://learn.microsoft.com/dotnet/api/skiasharp.skbitmap.resize
        var target = new SKImageInfo(source.Width * Scale, source.Height * Scale, SKColorType.Gray8, SKAlphaType.Opaque);
        return source.Resize(target, new SKSamplingOptions(SKFilterMode.Linear))
            ?? throw new InvalidOperationException($"SkiaSharp couldn't scale a {source.ColorType} bitmap to Gray8.");
    }
}
