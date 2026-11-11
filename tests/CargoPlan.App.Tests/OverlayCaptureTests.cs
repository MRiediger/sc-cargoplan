using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CargoPlan.App.Diagnostics;
using CargoPlan.App.Win32;
using CargoPlan.Ocr;
using SkiaSharp;
using Xunit.Abstractions;

namespace CargoPlan.App.Tests;

/// <summary>
/// By hand only, with Star Citizen running (T10.1b, D100): what a capture of the scan region holds when one of our windows
/// lies over it, per way of capturing. A magenta window, layered, topmost and never activated like the overlay, sits over
/// the middle of the region; each variant runs 20 times on a worker thread, our window is hidden and shown through its
/// dispatcher (H2, so the dispatcher's latency counts as flicker). Nothing is asserted; the table and one picture per
/// variant are the result. CARGOPLAN_SC_CAPTURE = an output folder outside the repo (the pictures are CIG material);
/// CARGOPLAN_SC_CAPTURE_PROCESS = another process name for a dry run without the game. The cloak measurement (B12) is in
/// OverlayCaptureTests.Cloak.cs.
/// </summary>
[Trait("Category", "Slow")]
public sealed partial class OverlayCaptureTests(ITestOutputHelper output)
{
    private const int Trials = 20;
    private const double MarkerWidth = 400;
    private const double MarkerHeight = 300;

    // Long enough for WPF to draw the window again after a show, so the next trial starts with it on screen.
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(200);

    [Fact]
    public void Our_window_over_the_scan_region_per_capture_variant()
    {
        var (folder, game, region, report) = Setup();

        Sta.Run(() =>
        {
            var marker = Marker();
            marker.Show();
            Place(marker, region);
            Pump(Settle);
            var bounds = NativeMethods.GetWindowBounds(new WindowInteropHelper(marker).Handle);
            report.AppendLine(Inv($"marker window {bounds} (physical px, H1)"));

            var worker = Task.Run(() => MeasureAll(marker, game, region, folder, report));
            WaitPumping(worker);
            marker.Close();
        });

        output.WriteLine(report.ToString());
        File.WriteAllText(Path.Combine(folder, "overlay-capture.txt"), report.ToString());
    }

    // The output folder, the game's window (or another process's for a dry run) and the report's first line.
    private static (string Folder, GameWindow Game, PixelRect Region, StringBuilder Report) Setup()
    {
        var folder = Environment.GetEnvironmentVariable("CARGOPLAN_SC_CAPTURE") is { Length: > 0 } value ? value
            : throw new InvalidOperationException("CARGOPLAN_SC_CAPTURE is not set; this test runs by hand, see CLAUDE.md, Befehle.");
        Directory.CreateDirectory(folder);
        // Another process's window for a dry run of the harness without the game (e.g. "Code").
        var process = Environment.GetEnvironmentVariable("CARGOPLAN_SC_CAPTURE_PROCESS") is { Length: > 0 } other ? other : AutoLockRule.GameProcessName;
        var game = GameWindow.Find(process) ?? throw new InvalidOperationException($"No window of {process} found: start the game, open a contract in the mobiGlas.");
        var region = game.ScanRegionOnScreen;

        var report = new StringBuilder();
        report.AppendLine(Inv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}, client {game.Client}, frame bounds {NativeMethods.GetFrameBounds(game.Handle)}, region {region}, trials {Trials}"));
        return (folder, game, region, report);
    }

    private static void MeasureAll(Window marker, GameWindow game, PixelRect region, string folder, StringBuilder report)
    {
        // The region without our window: what the game shows there, and no magenta of its own.
        Ui(marker, () => marker.Visibility = Visibility.Hidden);
        Thread.Sleep(Settle);
        report.AppendLine(Line("reference (window hidden)", Run(1, folder, "reference", () => (BitBltRegion(region, NativeMethods.SRCCOPY), 0))));
        Ui(marker, () => marker.Visibility = Visibility.Visible);
        Thread.Sleep(Settle);

        (string Name, Func<(SKBitmap?, double)> Capture)[] variants =
        [
            ("V0 BitBlt SRCCOPY", () => (BitBltRegion(region, NativeMethods.SRCCOPY), 0)),
            ("V0b BitBlt SRCCOPY|CAPTUREBLT", () => (BitBltRegion(region, NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT), 0)),
            ("V1 PrintWindow CLIENTONLY|RENDERFULLCONTENT", () => (PrintWindowRegion(game, region), 0)),
            ("V2a Hidden, no DwmFlush", () => Hidden(marker, region, flushes: 0, opacity: false)),
            ("V2b Hidden, 1x DwmFlush", () => Hidden(marker, region, flushes: 1, opacity: false)),
            ("V2c Hidden, 2x DwmFlush", () => Hidden(marker, region, flushes: 2, opacity: false)),
            ("V2d Opacity 0, 1x DwmFlush", () => Hidden(marker, region, flushes: 1, opacity: true)),
        ];
        foreach (var (name, capture) in variants)
        {
            report.AppendLine(Line(name, Run(Trials, folder, name.Split(' ')[0], capture)));
        }
    }

    // One trial: the capture's own time around the given call, the time our window was away, what the picture holds.
    private static List<Trial> Run(int trials, string folder, string pictureName, Func<(SKBitmap? Picture, double HiddenMs)> capture)
    {
        List<Trial> results = [];
        for (var i = 0; i < trials; i++)
        {
            var clock = Stopwatch.StartNew();
            var (picture, hiddenMs) = capture();
            var captureMs = clock.Elapsed.TotalMilliseconds;
            using (picture)
            {
                if (picture is null)
                {
                    results.Add(new Trial(captureMs, hiddenMs, -1, 0, 0));
                    continue;
                }

                if (i == 0)
                {
                    using var png = picture.Encode(SKEncodedImageFormat.Png, 100);
                    File.WriteAllBytes(Path.Combine(folder, $"overlay-{pictureName}.png"), png.ToArray());
                }

                results.Add(Inspect(picture, captureMs, hiddenMs));
            }

            Thread.Sleep(Settle);
        }

        return results;
    }

    private static (SKBitmap?, double) Hidden(Window marker, PixelRect region, int flushes, bool opacity)
    {
        var away = Stopwatch.StartNew();
        Ui(marker, () =>
        {
            if (opacity)
            {
                marker.Opacity = 0;
            }
            else
            {
                marker.Visibility = Visibility.Hidden;
            }
        });
        for (var i = 0; i < flushes; i++)
        {
            NativeMethods.DwmFlush();
        }

        var picture = BitBltRegion(region, NativeMethods.SRCCOPY);
        Ui(marker, () =>
        {
            marker.Opacity = 1;
            marker.Visibility = Visibility.Visible;
        });
        return (picture, away.Elapsed.TotalMilliseconds);
    }

    // A copy, so the DIB section is gone before the picture is inspected, as in the product path after Preprocessing.
    private static SKBitmap? BitBltRegion(PixelRect region, uint rasterOperation)
    {
        using var captured = ScreenCapture.Region(region, out _, rasterOperation);
        return captured?.Bitmap.Copy();
    }

    // The whole client area through the game's own WM_PRINT handling, then the region cut out of it.
    private static SKBitmap? PrintWindowRegion(GameWindow game, PixelRect region)
    {
        var dib = NativeMethods.CreateTopDownDib(game.Client.Width, game.Client.Height, out var bits);
        var memoryDc = NativeMethods.CreateCompatibleDC(0);
        var previous = NativeMethods.SelectObject(memoryDc, dib);
        try
        {
            if (!NativeMethods.PrintWindow(game.Handle, memoryDc, NativeMethods.PW_CLIENTONLY | NativeMethods.PW_RENDERFULLCONTENT))
            {
                return null;
            }

            NativeMethods.GdiFlush();
            using var frame = new SKBitmap();
            frame.InstallPixels(new SKImageInfo(game.Client.Width, game.Client.Height, SKColorType.Bgra8888, SKAlphaType.Opaque), bits, game.Client.Width * 4);
            using var cut = new SKBitmap();
            frame.ExtractSubset(cut, SKRectI.Create(region.X - game.Client.X, region.Y - game.Client.Y, region.Width, region.Height));
            return cut.Copy();
        }
        finally
        {
            NativeMethods.SelectObject(memoryDc, previous);
            NativeMethods.DeleteDC(memoryDc);
            NativeMethods.DeleteObject(dib);
        }
    }

    // Magenta pixels = our window in the picture; the luminance's spread tells a picture from a black or flat one.
    private static Trial Inspect(SKBitmap picture, double captureMs, double hiddenMs)
    {
        var pixels = picture.GetPixelSpan();
        var (magenta, sum, sumOfSquares) = (0, 0.0, 0.0);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var (b, g, r) = (pixels[i], pixels[i + 1], pixels[i + 2]);
            if (r > 200 && b > 200 && g < 60)
            {
                magenta++;
            }

            var luminance = (0.299 * r) + (0.587 * g) + (0.114 * b);
            sum += luminance;
            sumOfSquares += luminance * luminance;
        }

        var count = pixels.Length / 4;
        var mean = sum / count;
        return new Trial(captureMs, hiddenMs, magenta, mean, Math.Sqrt(Math.Max(0, (sumOfSquares / count) - (mean * mean))));
    }

    private static string Line(string name, List<Trial> trials)
    {
        var captured = trials.Where(t => t.Magenta >= 0).ToList();
        if (captured.Count == 0)
        {
            return $"{name}: no picture in {trials.Count} trials";
        }

        static double P(IEnumerable<double> values, double share) => FrameStatistics.Percentile([.. values], share);
        return Inv($"{name}: n {trials.Count}, failed {trials.Count - captured.Count}, ")
            + Inv($"capture median {P(captured.Select(t => t.CaptureMs), 0.5):0.0} ms p95 {P(captured.Select(t => t.CaptureMs), 0.95):0.0} ms, ")
            + Inv($"window away median {P(captured.Select(t => t.HiddenMs), 0.5):0.0} ms p95 {P(captured.Select(t => t.HiddenMs), 0.95):0.0} ms, ")
            + Inv($"magenta in {captured.Count(t => t.Magenta > 0)}/{captured.Count} (max {captured.Max(t => t.Magenta)} px), ")
            + Inv($"luminance mean {P(captured.Select(t => t.Mean), 0.5):0.0} spread {P(captured.Select(t => t.Spread), 0.5):0.0}");
    }

    private static Window Marker()
    {
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Color.FromRgb(255, 0, 255)),
            Topmost = true,
            ShowActivated = false,
            ShowInTaskbar = false,
            Width = MarkerWidth,
            Height = MarkerHeight,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        // Like the overlay (D11): never the foreground window, so the game keeps it.
        window.SourceInitialized += (_, _) => NativeMethods.PreventActivation(new WindowInteropHelper(window).Handle, showInTaskbar: false);
        return window;
    }

    // WPF places in device-independent units; the region is in physical pixels.
    private static void Place(Window window, PixelRect region)
    {
        var scale = VisualTreeHelper.GetDpi(window).DpiScaleX;
        window.Left = ((region.X + (region.Width / 2.0)) / scale) - (MarkerWidth / 2);
        window.Top = ((region.Y + (region.Height / 2.0)) / scale) - (MarkerHeight / 2);
    }

    private static void Ui(Window window, Action action) => window.Dispatcher.Invoke(action);

    private static void Pump(TimeSpan duration)
    {
        DispatcherFrame frame = new();
        DispatcherTimer timer = new() { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    // The STA thread keeps its dispatcher running while the worker measures, so the worker's Invoke calls get through.
    private static void WaitPumping(Task worker)
    {
        DispatcherFrame frame = new();
        worker.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        if (worker.Exception is { } failure)
        {
            ExceptionDispatchInfo.Throw(failure.InnerException ?? failure);
        }
    }

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private sealed record Trial(double CaptureMs, double HiddenMs, int Magenta, double Mean, double Spread);
}
