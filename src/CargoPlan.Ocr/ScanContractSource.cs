using System.Diagnostics;
using SkiaSharp;

namespace CargoPlan.Ocr;

/// <summary>
/// Reads a contract from a picture of the game window: region → up2-gray → OCR → reading order → raw text (T10.1a).
/// The picture comes from a PNG (Cli "scan") or, from T10.1b, from the App's capture; parsing it is T10.2.
/// A class without an interface: one implementation, tested directly (A4).
/// </summary>
public sealed class ScanContractSource(OcrEngine engine)
{
    /// <summary>Reads <paramref name="frame"/>, the client area of the game window in full.</summary>
    public ScanResult Read(SKBitmap frame)
    {
        var clock = Stopwatch.StartNew();
        var region = ScanRegion.Of(frame.Width, frame.Height);
        // ExtractSubset shares the frame's pixels, no copy. Source:
        // https://learn.microsoft.com/dotnet/api/skiasharp.skbitmap.extractsubset
        using var cropped = new SKBitmap();
        if (!frame.ExtractSubset(cropped, SKRectI.Create(region.X, region.Y, region.Width, region.Height)))
        {
            throw new InvalidOperationException($"The region {region} doesn't fit a {frame.Width}×{frame.Height} frame.");
        }

        var regionTime = clock.Elapsed;

        clock.Restart();
        using var gray = Preprocessing.Up2Gray(cropped);
        var preprocessTime = clock.Elapsed;

        clock.Restart();
        var blocks = engine.Read(gray);
        var ocrTime = clock.Elapsed;

        clock.Restart();
        var lines = ReadingOrder.Lines(blocks);
        var timings = new ScanTimings(regionTime, preprocessTime, ocrTime, clock.Elapsed);

        return lines.Length == 0
            ? new ScanResult(ScanOutcome.NoContractInFrame, "", timings)
            : new ScanResult(ScanOutcome.Read, string.Join('\n', lines), timings);
    }
}
