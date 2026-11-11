using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CargoPlan.App.Settings;
using CargoPlan.App.Theme;
using CargoPlan.App.Win32;
using CargoPlan.Ocr;
using SkiaSharp;
using TextBox = System.Windows.Controls.TextBox;

namespace CargoPlan.App.Tests;

// B12: the D100 measurement for a window with a title bar like the editor's, and DWM cloaking instead of Opacity 0. Rows
// R0–R5b of the B12 plan. A chrome window's leak is read twice: magenta in the picture, and the OCR of the region finding
// the title, which is what broke the contract key. Window events per trial show whether cloaking hides or deactivates.
public sealed partial class OverlayCaptureTests
{
    private const string ChromeTitle = "CARGOPLAN CLOAK TEST";
    private const string TitleWord = "CLOAK";
    private const double Gap = 40;
    private const double OwnedOffset = 60;
    private static readonly TimeSpan FocusWait = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FocusPoll = TimeSpan.FromMilliseconds(200);

    private int _events;

    [Fact]
    public void Chrome_and_layered_windows_over_the_scan_region_per_clearance()
    {
        var (folder, _, region, report) = Setup();
        report.AppendLine("capture = window away: from the first change to our windows until they are back; title = a block of the region's OCR contains "
            + TitleWord + " (blocks, not the contract's lines: those need a contract heading); a chrome window at Opacity 0 shows a black client, not magenta");
        using var engine = OcrEngine.Load();

        Sta.Run(() =>
        {
            var ui = Dispatcher.CurrentDispatcher;
            var worker = Task.Run(() => MeasureCloak(ui, region, folder, engine, report));
            WaitPumping(worker);
        });

        output.WriteLine(report.ToString());
        File.WriteAllText(Path.Combine(folder, "overlay-cloak.txt"), report.ToString());
    }

    private void MeasureCloak(Dispatcher ui, PixelRect region, string folder, OcrEngine reader, StringBuilder report)
    {
        List<Window> made = [];
        Window Track(Window window)
        {
            window.IsVisibleChanged += (_, _) => Interlocked.Increment(ref _events);
            window.Activated += (_, _) => Interlocked.Increment(ref _events);
            window.Deactivated += (_, _) => Interlocked.Increment(ref _events);
            made.Add(window);
            return window;
        }

        try
        {
            // Controls first: the background alone must not hold the title, a window left in place must be found.
            report.AppendLine(Row(ui, region, folder, "Ref no window, background only", [], Way.None, 0, reader));

            // R0, R2: the chrome marker over the region; its title bar has to lie inside, or the OCR criterion means nothing.
            var chrome = ui.Invoke(() => Track(ChromeMarker(activatable: false).Window));
            Show(ui, chrome, w => Place(w, region));
            var chromeBounds = BoundsOf(ui, chrome);
            report.AppendLine(Inv($"chrome marker {chromeBounds}, DWM frame {NativeMethods.GetFrameBounds(Handle(ui, chrome))} (physical px)"));
            if (!Inside(region, chromeBounds))
            {
                throw new InvalidOperationException(Inv($"The chrome marker {chromeBounds} isn't inside the region {region}: its title bar would miss the OCR."));
            }

            report.AppendLine(Row(ui, region, folder, "C0 chrome left in place", [chrome], Way.None, 0, reader));
            report.AppendLine(Row(ui, region, folder, "R0 chrome, Opacity 0, 1x DwmFlush", [chrome], Way.Opacity, 1, reader));
            report.AppendLine(Row(ui, region, folder, "R2a chrome, cloak, no DwmFlush", [chrome], Way.Cloak, 0, reader));
            report.AppendLine(Row(ui, region, folder, "R2b chrome, cloak, 1x DwmFlush", [chrome], Way.Cloak, 1, reader));
            report.AppendLine(Row(ui, region, folder, "R2c chrome, cloak, 2x DwmFlush", [chrome], Way.Cloak, 2, reader));
            Hide(ui, chrome);

            // R1, R3: the layered marker like the overlay, D100's reference.
            var layered = ui.Invoke(() => Track(Marker()));
            Show(ui, layered, w => Place(w, region));
            report.AppendLine(Inv($"layered marker {BoundsOf(ui, layered)} (physical px)"));
            report.AppendLine(Row(ui, region, folder, "C1 layered left in place", [layered], Way.None, 0, null));
            report.AppendLine(Row(ui, region, folder, "R1 layered, Opacity 0, 1x DwmFlush", [layered], Way.Opacity, 1, null));
            report.AppendLine(Row(ui, region, folder, "R3a layered, cloak, 1x DwmFlush", [layered], Way.Cloak, 1, null));
            report.AppendLine(Row(ui, region, folder, "R3b layered, cloak, 2x DwmFlush", [layered], Way.Cloak, 2, null));
            Hide(ui, layered);

            report.AppendLine(FocusRow(ui, region, folder, reader, Track));
            report.AppendLine(OwnerRow(ui, region, folder, Track, ownedOverRegion: false));
            report.AppendLine(OwnerRow(ui, region, folder, Track, ownedOverRegion: true));
        }
        finally
        {
            // Owned windows were made after their owner: closing in reverse never closes one twice.
            ui.Invoke(() =>
            {
                for (var i = made.Count - 1; i >= 0; i--)
                {
                    made[i].Close();
                }
            });
        }
    }

    // One row of plain trials: our windows away by the given way, the region copied, the windows back.
    private string Row(Dispatcher ui, PixelRect region, string folder, string name, Window[] windows, Way way, int flushes, OcrEngine? reader, Func<bool>? focused = null)
    {
        var handles = ui.Invoke(() => windows.Select(w => new WindowInteropHelper(w).Handle).ToArray());
        List<CloakTrial> trials = [];
        for (var i = 0; i < Trials; i++)
        {
            var before = focused?.Invoke();
            var events = Volatile.Read(ref _events);
            var clock = Stopwatch.StartNew();
            ui.Invoke(() => Away(windows, handles, way, gone: true));
            for (var f = 0; f < flushes; f++)
            {
                NativeMethods.DwmFlush();
            }

            var picture = BitBltRegion(region, NativeMethods.SRCCOPY);
            ui.Invoke(() => Away(windows, handles, way, gone: false));
            var ms = clock.Elapsed.TotalMilliseconds;

            // Activated and Deactivated arrive as messages: they are counted after the settle, not at once.
            Thread.Sleep(Settle);
            var (trial, title) = Measured(picture, ms, i, folder, name, reader);
            trials.Add(new CloakTrial(trial, title, Volatile.Read(ref _events) - events, before, focused?.Invoke(), null));
        }

        return Summary(name, trials);
    }

    // R4: the editor's case, a chrome window in front with the cursor in a text box, scanning by its own button.
    private string FocusRow(Dispatcher ui, PixelRect region, string folder, OcrEngine reader, Func<Window, Window> track)
    {
        const string name = "R4 chrome active, text box focused, cloak, 1x DwmFlush";
        var (window, box) = ui.Invoke(() =>
        {
            var marker = ChromeMarker(activatable: true);
            track(marker.Window);
            return marker;
        });
        Show(ui, window, w => Place(w, region));
        var handle = Handle(ui, window);
        bool Focused() => ui.Invoke(() => window.IsActive && NativeMethods.GetForegroundWindow() == handle && Keyboard.FocusedElement == box);

        ui.Invoke(() =>
        {
            window.Activate();
            box.Focus();
        });
        Thread.Sleep(Settle);
        if (!Focused())
        {
            // Windows may refuse the foreground to a process the user didn't touch: then a click into the box does it.
            ui.Invoke(() => box.Text = "Click here: R4 starts");
            var wait = Stopwatch.StartNew();
            while (!Focused() && wait.Elapsed < FocusWait)
            {
                Thread.Sleep(FocusPoll);
            }
        }

        var line = Focused()
            ? Row(ui, region, folder, name, [window], Way.Cloak, 1, reader, Focused)
            : Inv($"{name}: skipped, the window wasn't in front with the box focused within {FocusWait.TotalSeconds:0} s");
        Hide(ui, window);
        return line;
    }

    // R5: only the layered owner over the region is cloaked; does its owned chrome window beside the region go too?
    // R5b: owner and owned both over the region, both cloaked by us; does uncloaking the owner take the owned one's along?
    private string OwnerRow(Dispatcher ui, PixelRect region, string folder, Func<Window, Window> track, bool ownedOverRegion)
    {
        var name = ownedOverRegion
            ? "R5b layered owner + owned chrome both over the region, both cloaked, 1x DwmFlush, owner uncloaked first"
            : "R5 layered owner over the region, owned chrome beside it, owner cloaked, 1x DwmFlush";
        var owner = ui.Invoke(() => track(Marker()));
        Show(ui, owner, w => Place(w, region));
        var owned = ui.Invoke(() =>
        {
            var marker = track(ChromeMarker(activatable: false).Window);
            marker.Owner = owner; // WPF wants the owner shown first
            return marker;
        });
        Show(ui, owned, w =>
        {
            Place(w, region);
            if (ownedOverRegion)
            {
                w.Top += OwnedOffset;
            }
            else
            {
                PlaceLeftOf(w, region);
            }
        });

        var ownedBounds = BoundsOf(ui, owned);
        if (!ownedOverRegion && Overlaps(region, ownedBounds))
        {
            throw new InvalidOperationException(Inv($"The owned marker {ownedBounds} cuts the region {region}; R5 needs it beside."));
        }

        var (ownerHandle, ownedHandle) = (Handle(ui, owner), Handle(ui, owned));
        List<CloakTrial> trials = [];
        for (var i = 0; i < Trials; i++)
        {
            var events = Volatile.Read(ref _events);
            var clock = Stopwatch.StartNew();
            ui.Invoke(() =>
            {
                Cloak(ownerHandle, true);
                if (ownedOverRegion)
                {
                    Cloak(ownedHandle, true);
                }
            });
            NativeMethods.DwmFlush();
            var during = NativeMethods.Cloaked(ownedHandle);
            var picture = BitBltRegion(region, NativeMethods.SRCCOPY);

            // Beside the region the owned window is checked in its own rectangle: does it really leave the screen?
            bool? ownedSeen = null;
            if (!ownedOverRegion)
            {
                using var ownedPicture = BitBltRegion(new PixelRect(ownedBounds.X, ownedBounds.Y, ownedBounds.Width, ownedBounds.Height), NativeMethods.SRCCOPY);
                ownedSeen = ownedPicture is null ? null : Inspect(ownedPicture, 0, 0).Magenta > 0;
            }

            ui.Invoke(() => Cloak(ownerHandle, false));
            var afterOwner = NativeMethods.Cloaked(ownedHandle);
            if (ownedOverRegion)
            {
                ui.Invoke(() => Cloak(ownedHandle, false));
            }

            var ms = clock.Elapsed.TotalMilliseconds;
            var afterAll = NativeMethods.Cloaked(ownedHandle);
            Thread.Sleep(Settle);
            var settled = NativeMethods.Cloaked(ownedHandle);

            var (trial, _) = Measured(picture, ms, i, folder, name, null);
            var state = ownedOverRegion
                ? Inv($"owned cloaked: during {during}, owner uncloaked {afterOwner}, owned uncloaked {afterAll}, settled {settled}")
                : Inv($"owned cloaked: during {during}, owner uncloaked {afterAll}, settled {settled}; owned on screen while the owner is cloaked: {ownedSeen}");
            trials.Add(new CloakTrial(trial, null, Volatile.Read(ref _events) - events, null, null, state));
        }

        Hide(ui, owner); // hides the owned one with it
        return Inv($"{Summary(name, trials)}; owned {ownedBounds}");
    }

    private static (Trial Trial, bool? Title) Measured(SKBitmap? picture, double ms, int index, string folder, string name, OcrEngine? reader)
    {
        using (picture)
        {
            if (picture is null)
            {
                return (new Trial(ms, ms, -1, 0, 0), null);
            }

            var file = Path.Combine(folder, $"cloak-{name.Split(' ')[0]}");
            if (index == 0)
            {
                using var png = picture.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(file + ".png", png.ToArray());
            }

            var trial = Inspect(picture, ms, ms);
            if (reader is null)
            {
                return (trial, null);
            }

            using var gray = Preprocessing.Up2Gray(picture);
            var text = string.Join('\n', reader.Read(gray).Select(b => b.Text));
            if (index == 0)
            {
                File.WriteAllText(file + ".txt", text);
            }

            return (trial, text.Contains(TitleWord, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string Summary(string name, List<CloakTrial> trials)
    {
        var text = new StringBuilder(Line(name, [.. trials.Select(t => t.Trial)]));
        var read = trials.Where(t => t.Title is not null).ToList();
        if (read.Count > 0)
        {
            text.Append(Inv($", title read in {read.Count(t => t.Title == true)}/{read.Count}"));
        }

        text.Append(Inv($", window events {trials.Sum(t => t.Events)}"));
        var focus = trials.Where(t => t.FocusedBefore is not null).ToList();
        if (focus.Count > 0)
        {
            text.Append(Inv($", focused before {focus.Count(t => t.FocusedBefore == true)}/{focus.Count} after {focus.Count(t => t.FocusedAfter == true)}/{focus.Count}"));
        }

        foreach (var group in trials.Where(t => t.Cloaked is not null).GroupBy(t => t.Cloaked))
        {
            text.Append(Inv($"; {group.Key} ×{group.Count()}"));
        }

        return text.ToString();
    }

    private static void Away(Window[] windows, nint[] handles, Way way, bool gone)
    {
        for (var i = 0; i < windows.Length; i++)
        {
            if (way == Way.Opacity)
            {
                windows[i].Opacity = gone ? 0 : 1;
            }
            else if (way == Way.Cloak)
            {
                Cloak(handles[i], gone);
            }
        }
    }

    // A failed call is a finding of the measurement, not a trial without the window.
    private static void Cloak(nint handle, bool cloak)
    {
        var result = NativeMethods.SetCloak(handle, cloak);
        if (result != 0)
        {
            throw new InvalidOperationException(Inv($"DwmSetWindowAttribute(DWMWA_CLOAK, {cloak}) failed: 0x{result:X8}"));
        }
    }

    // Like the editor: system title bar, Fluent dark (D36), no AllowsTransparency, so Opacity doesn't reach it.
    private static (Window Window, TextBox Box) ChromeMarker(bool activatable)
    {
        var window = new Window
        {
            Title = ChromeTitle,
            WindowStyle = WindowStyle.SingleBorderWindow,
            ResizeMode = ResizeMode.NoResize,
            Topmost = true,
            ShowActivated = activatable,
            ShowInTaskbar = false,
            Width = MarkerWidth,
            Height = MarkerHeight,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        ToolWindowThemer.Apply(window, ToolWindowTheme.Dark);
        var box = new TextBox
        {
            Width = 240,
            Margin = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        window.Content = new Border { Background = new SolidColorBrush(Color.FromRgb(255, 0, 255)), Child = box };
        if (!activatable)
        {
            window.SourceInitialized += (_, _) => NativeMethods.PreventActivation(new WindowInteropHelper(window).Handle, showInTaskbar: false);
        }

        return (window, box);
    }

    // Left of the region at its middle height, in device-independent units.
    private static void PlaceLeftOf(Window window, PixelRect region)
    {
        var scale = VisualTreeHelper.GetDpi(window).DpiScaleX;
        window.Left = (region.X / scale) - MarkerWidth - Gap;
        window.Top = ((region.Y + (region.Height / 2.0)) / scale) - (MarkerHeight / 2);
    }

    private static void Show(Dispatcher ui, Window window, Action<Window> place)
    {
        ui.Invoke(() =>
        {
            window.Show();
            place(window);
        });
        Thread.Sleep(Settle);
    }

    private static void Hide(Dispatcher ui, Window window)
    {
        ui.Invoke(window.Hide);
        Thread.Sleep(Settle);
    }

    private static nint Handle(Dispatcher ui, Window window) => ui.Invoke(() => new WindowInteropHelper(window).Handle);

    // In physical pixels like the region (H1): the worker enters the scope, as RegionClearance does.
    private static (int X, int Y, int Width, int Height) BoundsOf(Dispatcher ui, Window window)
    {
        var handle = Handle(ui, window);
        using var physical = NativeMethods.PhysicalPixelsScope.Enter();
        return NativeMethods.GetWindowBounds(handle) ?? throw new InvalidOperationException("The marker has no window rectangle.");
    }

    private static bool Inside(PixelRect r, (int X, int Y, int Width, int Height) b) =>
        b.X >= r.X && b.Y >= r.Y && b.X + b.Width <= r.X + r.Width && b.Y + b.Height <= r.Y + r.Height;

    private static bool Overlaps(PixelRect r, (int X, int Y, int Width, int Height) b) =>
        b.X < r.X + r.Width && r.X < b.X + b.Width && b.Y < r.Y + r.Height && r.Y < b.Y + b.Height;

    private enum Way
    {
        None,
        Opacity,
        Cloak,
    }

    private sealed record CloakTrial(Trial Trial, bool? Title, int Events, bool? FocusedBefore, bool? FocusedAfter, string? Cloaked);
}
