using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CargoPlan.App.Hotkeys;
using CargoPlan.App.Scanning;
using CargoPlan.App.Win32;
using CargoPlan.Ocr;
using SkiaSharp;

namespace CargoPlan.App.Diagnostics;

/// <summary>
/// The scan probe (<c>--scan-probe [N]</c>, T10.1b): next to the running overlay, F8 scans once and F9 scans N times in a row,
/// through the same <see cref="ContractScanner"/> the scan hotkey will use (no measuring path of its own). One line per scan
/// in scan-probe.log next to cargoplan.log: stage times, the time from the key to the raw text, memory, GDI objects, the
/// window and region, then the raw text (T10.2's fixtures). The first region of a run, or every one with
/// <c>--scan-probe-images</c>, goes to scan-probe\ as a PNG; captures are CIG material and stay out of the repo.
/// Always armed, whatever is in front: a developer's tool started on purpose.
/// </summary>
internal sealed class ScanProbe
{
    public const string LogFile = "scan-probe.log";
    public const string ImageFolder = "scan-probe";

    // Source: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    private const uint VK_F8 = 0x77;
    private const uint VK_F9 = 0x78;

    private const long Megabyte = 1024 * 1024;

    private readonly ContractScanner _scanner;
    private readonly int _series;
    private readonly bool _everyImage;
    private readonly HotkeyPoller<ProbeKey> _poller = new(NativeMethods.IsKeyDown);
    private readonly DispatcherTimer _timer = new() { Interval = HotkeyService.PollInterval };
    private readonly string _logPath = Path.Combine(Path.GetDirectoryName(AppLog.Path)!, LogFile);
    private readonly string _imageFolder = Path.Combine(Path.GetDirectoryName(AppLog.Path)!, ImageFolder);

    private int _scans;
    private bool _running;

    private enum ProbeKey
    {
        Once,
        Series,
    }

    /// <param name="window">The overlay: the probe stops polling when it closes.</param>
    /// <param name="series">How many scans F9 runs.</param>
    public ScanProbe(ContractScanner scanner, Window window, int series, bool everyImage)
    {
        (_scanner, _series, _everyImage) = (scanner, series, everyImage);
        _poller.Set(ProbeKey.Once, new Hotkey(HotkeyModifiers.None, VK_F8));
        _poller.Set(ProbeKey.Series, new Hotkey(HotkeyModifiers.None, VK_F9));
        WriteHeader();
        _timer.Tick += OnTick;
        _timer.Start();
        window.Closed += (_, _) => _timer.Stop();
    }

    // async void: an event handler (CLAUDE.md). A failure here is a bug and reaches the dispatcher's crash log.
    private async void OnTick(object? sender, EventArgs e)
    {
        foreach (var key in _poller.Poll(armed: true))
        {
            if (_running)
            {
                AppLog.WriteTo(_logPath, "key", $"{key} ignored: a probe run is going on");
                continue;
            }

            _running = true;
            try
            {
                await Run(key == ProbeKey.Once ? 1 : _series);
            }
            finally
            {
                _running = false;
            }
        }
    }

    private async Task Run(int count)
    {
        AppLog.WriteTo(_logPath, "run", Inv($"{count} scan(s)"));
        for (var i = 0; i < count; i++)
        {
            var trigger = Stopwatch.StartNew();
            SKBitmap? picture = null;
            var keep = _scans == 0 || _everyImage;
            var attempt = await _scanner.ScanAsync(keep ? region => picture = region.Copy() : null);
            var toText = trigger.Elapsed;
            _scans++;

            var image = picture is null ? "" : $"{DateTime.Now:yyyyMMdd-HHmmss}-{_scans:000}.png";
            AppLog.WriteTo(_logPath, "scan", Line(attempt, toText, image));
            if (attempt.RawText.Length > 0)
            {
                AppLog.WriteTo(_logPath, "raw", Inv($"{_scans}{Environment.NewLine}{attempt.RawText}{Environment.NewLine}--- end ---"));
            }

            if (picture is not null)
            {
                await Task.Run(() => Save(picture, image));
            }
        }
    }

    private string Line(ScanAttempt attempt, TimeSpan toText, string image)
    {
        using var process = Process.GetCurrentProcess();
        var read = attempt.Read;
        var game = attempt.Game;
        return string.Join('\t',
            Inv($"n={_scans}"),
            Inv($"outcome={attempt.Outcome}"),
            Inv($"client={Rect(game?.Client)}"),
            Inv($"frame={(game is null ? "-" : Rect(NativeMethods.GetFrameBounds(game.Handle)))}"),
            Inv($"region={Rect(game?.ScanRegionOnScreen)}"),
            Inv($"gameDpi={(game is null ? 0 : NativeMethods.GetDpiForWindow(game.Handle))}"),
            Inv($"ownWindowOverRegion={(game is null ? "-" : OwnWindowOver(game.ScanRegionOnScreen) ? "yes" : "no")}"),
            Inv($"waitMs={Ms(attempt.Wait)}"),
            Inv($"captureMs={Ms(attempt.Capture)}"),
            Inv($"preprocessMs={Ms(read?.Preprocess)}"),
            Inv($"ocrMs={Ms(read?.Ocr)}"),
            Inv($"linesMs={Ms(read?.Lines)}"),
            Inv($"totalMs={Ms(attempt.Total)}"),
            Inv($"keyToTextMs={Ms(toText)}"),
            Inv($"workingSetMB={process.WorkingSet64 / Megabyte}"),
            Inv($"privateMB={process.PrivateMemorySize64 / Megabyte}"),
            Inv($"gdi={NativeMethods.GdiObjectCount()}"),
            Inv($"captureError={attempt.CaptureError}"),
            Inv($"image={image}"));
    }

    private void WriteHeader()
    {
        string captureDpi;
        using (NativeMethods.PhysicalPixelsScope.Enter())
        {
            captureDpi = NativeMethods.ThreadDpiAwareness();
        }

        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        AppLog.WriteTo(_logPath, "start", string.Join('\t',
            Inv($"version={version}"),
            Inv($"args={string.Join(' ', Environment.GetCommandLineArgs().Skip(1))}"),
            Inv($"uiDpiAwareness={NativeMethods.ThreadDpiAwareness()}"),
            Inv($"captureDpiAwareness={captureDpi}"),
            Inv($"cores={Environment.ProcessorCount}"),
            Inv($"series={_series}"),
            Inv($"everyImage={_everyImage}")));
    }

    // Our visible windows (overlay, lock window, editor, settings) against the region, in physical pixels (H1).
    private static bool OwnWindowOver(PixelRect region)
    {
        using var physical = NativeMethods.PhysicalPixelsScope.Enter();
        return Application.Current.Windows.Cast<Window>()
            .Where(w => w.IsVisible)
            .Select(w => NativeMethods.GetWindowBounds(new WindowInteropHelper(w).Handle))
            .Any(b => b is { } r && r.X < region.X + region.Width && region.X < r.X + r.Width && r.Y < region.Y + region.Height && region.Y < r.Y + r.Height);
    }

    private void Save(SKBitmap picture, string name)
    {
        using (picture)
        {
            try
            {
                Directory.CreateDirectory(_imageFolder);
                using var png = picture.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(_imageFolder, name), png.ToArray());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLog.WriteTo(_logPath, "image", $"{name} not written: {ex.Message}");
            }
        }
    }

    private static string Rect(PixelRect? r) => r is { } v ? Inv($"{v.X},{v.Y},{v.Width}x{v.Height}") : "-";

    private static string Rect((int X, int Y, int Width, int Height)? r) => r is { } v ? Inv($"{v.X},{v.Y},{v.Width}x{v.Height}") : "-";

    private static string Ms(TimeSpan? time) => time is { } t ? t.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) : "-";

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
