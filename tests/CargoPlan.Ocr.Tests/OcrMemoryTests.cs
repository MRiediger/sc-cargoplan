using System.Diagnostics;
using System.Globalization;
using SkiaSharp;
using Xunit.Abstractions;

namespace CargoPlan.Ocr.Tests;

/// <summary>
/// By hand only (T10.1a): one real capture read 100 times, memory after each read. The early, offline signal for the
/// T10.1b gate "100 scans, the working set reaches a plateau" (S10.0: 407 → 684 MB in 20 scans over the game, no
/// plateau). Nothing is asserted; the table is the result. CARGOPLAN_OCR_SAMPLES = the sc-ocr-samples folder.
/// </summary>
[Trait("Category", "Timing")]
public sealed class OcrMemoryTests(ITestOutputHelper output)
{
    private const int Reads = 100;
    private const long Megabyte = 1024 * 1024;

    [Fact]
    public void Memory_over_100_reads()
    {
        var samples = Environment.GetEnvironmentVariable("CARGOPLAN_OCR_SAMPLES") is { Length: > 0 } value ? value
            : throw new InvalidOperationException("CARGOPLAN_OCR_SAMPLES is not set; this test runs by hand, see CLAUDE.md, Befehle.");
        var file = Directory.GetFiles(Path.Combine(samples, "set"), "*manager.png").Order().First();

        using var engine = OcrEngine.Load();
        engine.Warmup();
        var source = new ScanContractSource(engine);
        using var process = Process.GetCurrentProcess();
        output.WriteLine($"{Path.GetFileName(file)}, after load and warm-up: {Line(process)}");
        output.WriteLine("| Read | ms | Working set MB | Private MB |");
        output.WriteLine("|---|---|---|---|");
        for (var read = 1; read <= Reads; read++)
        {
            // Decoded per read, as the App will capture a new frame per scan.
            using var frame = SKBitmap.Decode(file);
            var result = source.Read(frame);
            process.Refresh();
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"| {read} | {result.Timings.Total.TotalMilliseconds:0} | {process.WorkingSet64 / Megabyte} | {process.PrivateMemorySize64 / Megabyte} |"));
        }
    }

    private static string Line(Process process)
    {
        process.Refresh();
        return string.Create(CultureInfo.InvariantCulture,
            $"working set {process.WorkingSet64 / Megabyte} MB, private {process.PrivateMemorySize64 / Megabyte} MB");
    }
}
