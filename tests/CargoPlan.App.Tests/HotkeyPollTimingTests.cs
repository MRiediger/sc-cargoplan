using System.Diagnostics;
using System.Globalization;
using CargoPlan.App.Hotkeys;
using CargoPlan.App.Win32;
using Xunit.Abstractions;

namespace CargoPlan.App.Tests;

/// <summary>
/// What one hotkey poll costs on the UI thread (D97), 30 times a second: the foreground window query and a poll of all eight
/// hotkeys against the real key state. No time is asserted; the frame time over the game is Martin's gate. The table goes
/// to the test output:
/// <c>dotnet test tests/CargoPlan.App.Tests -c Release -p:RunSettingsFilePath= --filter Category=Timing --logger "console;verbosity=detailed"</c>.
/// </summary>
[Trait("Category", "Timing")]
public class HotkeyPollTimingTests(ITestOutputHelper output)
{
    private const int Rounds = 7;
    private const int TicksPerRound = 3000;

    private static readonly string[] EightHotkeys =
        ["Ctrl+Alt+L", "Ctrl+Alt+H", "Ctrl+Alt+S", "Ctrl+Alt+R", "Ctrl+Alt+D", "Ctrl+Alt+Z", "Ctrl+Alt+Y", "Ctrl+Alt+V"];

    [Fact]
    public void Poll_time()
    {
        var poller = new HotkeyPoller<HotkeyAction>(NativeMethods.IsKeyDown);
        foreach (var (text, action) in EightHotkeys.Zip(Enum.GetValues<HotkeyAction>()))
        {
            HotkeyParser.TryParse(text, out var hotkey, out _).Should().BeTrue();
            poller.Set(action, hotkey);
        }

        output.WriteLine("| Case | µs per tick (median) |");
        Row("poll, 8 hotkeys", () => poller.Poll(armed: true));
        Row("tick without a window change: foreground window + poll", () =>
        {
            NativeMethods.GetForegroundWindow();
            poller.Poll(armed: true);
        });
        Row("on a window change only: process id + name", () =>
            CursorStateMonitor.ProcessNameOf(NativeMethods.GetForegroundWindow()));
    }

    private void Row(string name, Action tick)
    {
        List<double> runs = [];
        for (var round = 0; round < Rounds; round++)
        {
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < TicksPerRound; i++)
            {
                tick();
            }

            runs.Add(watch.Elapsed.TotalMicroseconds / TicksPerRound);
        }

        // The first round warms up.
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"| {name} | {runs.Skip(1).Order().ElementAt(2):F2} |"));
    }
}
