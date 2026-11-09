using SkiaSharp;

namespace CargoPlan.Ocr.Tests;

/// <summary>Synthetic pictures for the OCR tests: dark text on a light background, drawn with SkiaSharp. No real
/// capture goes into the repo (CIG material, not MIT).</summary>
internal static class TextImages
{
    public const float TextSize = 28;

    /// <summary>A light bitmap of the given size with each text drawn at its top-left point.</summary>
    public static SKBitmap Draw(int width, int height, params (string Text, float X, float Y)[] texts) =>
        Draw(width, height, TextSize, texts);

    /// <summary>As above with another text size, for small frames.</summary>
    public static SKBitmap Draw(int width, int height, float textSize, params (string Text, float X, float Y)[] texts)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var font = new SKFont(SKTypeface.Default, textSize);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        foreach (var (text, x, y) in texts)
        {
            // DrawText places the baseline; the caller gives the top.
            canvas.DrawText(text, x, y + textSize, font, paint);
        }

        return bitmap;
    }
}
