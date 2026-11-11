using System.Diagnostics;

namespace CargoPlan.App.Hotkeys;

/// <summary>
/// ContractTracker reacts to F3 too (S10.0 K2): polled hotkeys hold no key, so both programs act on it (D97, D104). With
/// F3 as the scan key and ContractTracker running, a notice says so, once per run, while it is true. Pure but for the
/// process list, a delegate for the tests; asked only when the scan key is F3.
/// </summary>
/// <param name="contractTrackerRunning"><see cref="IsContractTrackerRunning"/> in the app.</param>
public sealed class ScanKeyCheck(Func<bool> contractTrackerRunning)
{
    /// <summary>ContractTracker 2.2's process: its exe is "SC Contract Tracker.exe" (checked on Martin's install, T10.3).</summary>
    public const string ContractTrackerProcess = "SC Contract Tracker";

    public const string Text = "ContractTracker also reacts to F3 – close it or change the scan hotkey.";

    // ContractTracker's own key, by the name the parser gives it.
    private const string ContractTrackerKey = "F3";

    private bool _shown;
    private bool _showing;

    /// <summary>Looks again (at the start, on each scan press, when the scan key changes): the notice, or null.</summary>
    public string? Check(Hotkey? scanKey)
    {
        var shared = scanKey?.ToString() == ContractTrackerKey && contractTrackerRunning();
        _showing = shared && (_showing || !_shown);
        _shown |= _showing;
        return _showing ? Text : null;
    }

    /// <summary>Whether a ContractTracker process runs; the processes are disposed at once.</summary>
    // Source: https://learn.microsoft.com/dotnet/api/system.diagnostics.process.getprocessesbyname (name without ".exe")
    public static bool IsContractTrackerRunning()
    {
        var processes = Process.GetProcessesByName(ContractTrackerProcess);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }
}
