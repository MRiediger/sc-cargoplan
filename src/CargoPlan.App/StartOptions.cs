// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
using System.IO;

namespace CargoPlan.App;

/// <summary>Start switches of the overlay. Unknown arguments are ignored, so nothing keeps the overlay from starting.</summary>
/// <param name="Locked">Start with the manual lock on (click-through); the lock window unlocks it.</param>
/// <param name="PlanPath">Load this plan file instead of the saved plan; it becomes the saved plan on the next save.</param>
/// <param name="Problem">A switch that couldn't be used, for the notice line; null = all fine.</param>
/// <param name="ThemeSample">Also open the tool window theme sample (T4.7a); only a Debug build has that window.</param>
/// <param name="MeasureFrames">
/// Measure the ship view's frame time for this ship id, or "empty" for the bare window, instead of starting the overlay
/// (Phase 8, D83); a developer's tool, it reads and writes neither settings.json nor plan.json.
/// </param>
/// <param name="OverGame">
/// The measuring window lies over the game like the overlay: topmost, never the foreground window, the run starting
/// after a lead time (T8.7, M5). Only with <paramref name="MeasureFrames"/>.
/// </param>
/// <param name="ScanProbe">
/// The scan probe (T10.1b), a developer's tool next to the overlay: F8 scans once, F9 scans this many times in a row; null =
/// off. Every scan goes to scan-probe.log with its stage times, memory and raw text.
/// </param>
/// <param name="ScanProbeImages">Save every scanned region as a PNG, not only the first. Only with <paramref name="ScanProbe"/>.</param>
/// <param name="ScanLoad">
/// Scan the game in a loop while the frames are measured (T10.1b, K3): the frame time under scans, for T10.3. Only with
/// <paramref name="MeasureFrames"/>; without it the measurement runs with the engine warm and idle.
/// </param>
/// <param name="DataDir">
/// Every file of the run in this folder instead of the profile: settings, plan, logs, hologram cache (T10.4a, B1). For test
/// and gate runs, so they never touch the player's files; null = the profile.
/// </param>
/// <param name="DataDirRefused">
/// <c>--data-dir</c> without a usable folder: the app ends instead of falling back to the profile, which the switch is
/// there to keep out of the run.
/// </param>
/// <param name="KeepScans">
/// Keep the region of each scan as a PNG in scans\ next to plan.json, the last 20 (T10.5, D108): for a bug report from a
/// screen size nobody checked. Off by default; the pictures are CIG material.
/// </param>
/// <param name="UpdatesSource">
/// The update check reads this local feed folder (what <c>vpk pack</c> wrote) instead of GitHub (T11.1, D109, E10): for
/// gates without a real release. null = GitHub. A path that isn't full is a problem, not a stop: it protects no file.
/// </param>
public sealed record StartOptions(
    bool Locked,
    string? PlanPath,
    string? Problem,
    bool ThemeSample = false,
    string? MeasureFrames = null,
    bool OverGame = false,
    int? ScanProbe = null,
    bool ScanProbeImages = false,
    bool ScanLoad = false,
    string? DataDir = null,
    bool DataDirRefused = false,
    bool KeepScans = false,
    string? UpdatesSource = null)
{
    public const string LockedSwitch = "--locked";

    public const string PlanSwitch = "--plan";

    public const string ThemeSampleSwitch = "--theme-sample";

    public const string MeasureFramesSwitch = "--measure-frames";

    public const string OverGameSwitch = "--over-game";

    public const string ScanProbeSwitch = "--scan-probe";

    public const string ScanProbeImagesSwitch = "--scan-probe-images";

    public const string ScanLoadSwitch = "--scan-load";

    public const string DataDirSwitch = "--data-dir";

    public const string KeepScansSwitch = "--keep-scans";

    public const string UpdatesSourceSwitch = "--updates-source";

    /// <summary>F9's series without a number: Gate 2's 100 scans.</summary>
    public const int ScanProbeSeries = 100;

    public static StartOptions Parse(IReadOnlyList<string> args)
    {
        var locked = false;
        var themeSample = false;
        var overGame = false;
        var scanProbeImages = false;
        var scanLoad = false;
        var keepScans = false;
        string? planPath = null;
        string? measureFrames = null;
        int? scanProbe = null;
        string? dataDir = null;
        var dataDirRefused = false;
        string? updatesSource = null;
        string? problem = null;

        for (var i = 0; i < args.Count; i++)
        {
            if (Is(args[i], LockedSwitch))
            {
                locked = true;
            }
            else if (Is(args[i], ThemeSampleSwitch))
            {
                themeSample = true;
            }
            else if (Is(args[i], OverGameSwitch))
            {
                overGame = true;
            }
            else if (Is(args[i], PlanSwitch))
            {
                // "--plan --locked" is a forgotten path, not a file named "--locked".
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    planPath = args[++i];
                }
                else
                {
                    problem = $"{PlanSwitch} needs the path of a plan file.";
                }
            }
            else if (Is(args[i], MeasureFramesSwitch))
            {
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    measureFrames = args[++i];
                }
                else
                {
                    problem = $"{MeasureFramesSwitch} needs a ship id or \"empty\".";
                }
            }
            else if (Is(args[i], DataDirSwitch))
            {
                // A relative path would depend on where the app was started from.
                // Source: https://learn.microsoft.com/dotnet/api/system.io.path.ispathfullyqualified
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal) && Path.IsPathFullyQualified(args[i + 1]))
                {
                    dataDir = args[++i];
                }
                else
                {
                    (dataDirRefused, problem) = (true, $"{DataDirSwitch} needs the full path of a folder.");
                }
            }
            else if (Is(args[i], UpdatesSourceSwitch))
            {
                // Full like --data-dir, for the same reason; the update check then asks GitHub as without the switch.
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal) && Path.IsPathFullyQualified(args[i + 1]))
                {
                    updatesSource = args[++i];
                }
                else
                {
                    problem = $"{UpdatesSourceSwitch} needs the full path of a folder.";
                }
            }
            else if (Is(args[i], ScanProbeImagesSwitch))
            {
                scanProbeImages = true;
            }
            else if (Is(args[i], ScanLoadSwitch))
            {
                scanLoad = true;
            }
            else if (Is(args[i], KeepScansSwitch))
            {
                keepScans = true;
            }
            else if (Is(args[i], ScanProbeSwitch))
            {
                scanProbe = ScanProbeSeries;

                // The series' length is optional; the next switch is no number.
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    if (int.TryParse(args[++i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var series) && series > 0)
                    {
                        scanProbe = series;
                    }
                    else
                    {
                        problem = $"{ScanProbeSwitch} takes a positive number of scans for F9; {ScanProbeSeries} it is.";
                    }
                }
            }
        }

        if (overGame && measureFrames is null)
        {
            (overGame, problem) = (false, problem ?? $"{OverGameSwitch} only goes with {MeasureFramesSwitch}.");
        }

        if (scanProbeImages && scanProbe is null)
        {
            (scanProbeImages, problem) = (false, problem ?? $"{ScanProbeImagesSwitch} only goes with {ScanProbeSwitch}.");
        }

        if (scanLoad && measureFrames is null)
        {
            (scanLoad, problem) = (false, problem ?? $"{ScanLoadSwitch} only goes with {MeasureFramesSwitch}.");
        }

        return new StartOptions(locked, planPath, problem, themeSample, measureFrames, overGame, scanProbe, scanProbeImages, scanLoad, dataDir, dataDirRefused, keepScans, updatesSource);
    }

    /// <summary>
    /// The arguments the app restarts with after an update (T11.2, U5): the same as this start (lock, data folder, feed,
    /// scans), without <c>--plan</c> and its path. That file became plan.json at the first save; loaded again it would
    /// replace the run's ticks and moves (Freigabe). Update.exe passes them after <c>--</c> (UpdateExe.cs 63–93).
    /// </summary>
    public static IReadOnlyList<string> RestartArguments(IReadOnlyList<string> args)
    {
        List<string> kept = [];
        for (var i = 0; i < args.Count; i++)
        {
            if (Is(args[i], PlanSwitch))
            {
                // Its path only if one follows, as Parse reads it.
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    i++;
                }

                continue;
            }

            kept.Add(args[i]);
        }

        return kept;
    }

    /// <summary>
    /// Whether the OCR loads after the first frame (D108): with a valid scan key, or for the probe, which measures a warm
    /// engine. Without either nothing loads until a key is set or the editor's Scan button is pressed.
    /// </summary>
    public bool WarmsOcrAtStart(bool scanKeySet) => scanKeySet || ScanProbe is not null;

    private static bool Is(string arg, string name) => arg.Equals(name, StringComparison.OrdinalIgnoreCase);
}
