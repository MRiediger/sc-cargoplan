// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
using System.Globalization;
using System.IO;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;

namespace CargoPlan.App.Plans;

/// <summary>Reads and writes plan.json. The file boundary: only here are file and format errors expected (D18).</summary>
public static class PlanStore
{
    /// <summary>
    /// %LocalAppData%\SC-CargoPlan\plan.json, next to settings.json but a file of its own: deleting the settings
    /// to reset the window must not throw away the plan, and the other way round.
    /// </summary>
    public static string DefaultPath => Path.Combine(AppFolders.LocalRoot, "plan.json");

    /// <summary>
    /// Missing file = no plan, no problem. Broken file, another ship or a plan that doesn't fit its missions = no plan
    /// plus a problem text for the overlay; the file is left as it is until the next save keeps it aside (B13, D106).
    /// </summary>
    /// <returns>
    /// The plan or a problem, the last scan number given (D102, 0 without a plan), and whether a file is there that couldn't
    /// be read: the saver keeps that one aside (B13, D106).
    /// </returns>
    public static PlanLoad Load(string path, Ship ship)
    {
        if (!File.Exists(path))
        {
            return new(null, null, 0, Unreadable: false);
        }

        try
        {
            using var stream = File.OpenRead(path);
            var (plan, lastScanNo) = PlanFile.ReadContent(stream, path, ship);
            return new(plan, null, lastScanNo, Unreadable: false);
        }
        catch (Exception ex) when (ex is PlanFormatException or IOException or UnauthorizedAccessException)
        {
            var problem = $"Plan not loaded ({ex.Message}). The next save keeps the file aside, it is never overwritten.";
            AppLog.Write("Plan", $"{problem} [{ex.GetType().Name}]");
            return new(null, problem, 0, Unreadable: true);
        }
    }

    /// <summary>
    /// The start plan: <paramref name="planPath"/> (--plan) wins over plan.json; a missing --plan file is a problem, a
    /// missing plan.json is the empty start. <see cref="PlanLoad.Unreadable"/> always speaks of plan.json
    /// (<paramref name="defaultPath"/>): the first save writes there, also the --plan plan (B13).
    /// </summary>
    public static PlanLoad LoadStart(string defaultPath, string? planPath, Ship ship)
    {
        if (planPath is null)
        {
            return Load(defaultPath, ship);
        }

        var loaded = Load(planPath, ship);
        if (loaded is { Plan: null, Problem: null })
        {
            loaded = loaded with { Problem = $"Plan file {planPath} not found." };
        }

        return loaded with { Unreadable = Load(defaultPath, ship).Unreadable };
    }

    /// <summary>
    /// Moves the file aside as "plan.json.unreadable-yyyyMMdd-HHmmss" in its folder, never over another: a second one in
    /// the same second gets "-2", "-3" … (B13, D106). Nothing is deleted, old ones stay.
    /// Source: https://learn.microsoft.com/dotnet/api/system.io.file.move (overwrite: false throws if the target exists)
    /// </summary>
    /// <returns>The new file name; null if there is no file (any more), so nothing to keep.</returns>
    /// <exception cref="IOException">The file is locked, e.g. by the program that made the load fail.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission to move the file.</exception>
    public static string? KeepUnreadable(string path, DateTime now)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var stem = $"{path}.unreadable-{now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
        var target = stem;
        for (var n = 2; File.Exists(target); n++)
        {
            target = $"{stem}-{n}";
        }

        File.Move(path, target, overwrite: false);
        return Path.GetFileName(target);
    }

    /// <summary>
    /// The plan's ship id, for the start without a ship in settings.json (T7.2); null without a file or if it can't be
    /// read. Reading problems are <see cref="Load"/>'s to report, so they aren't reported twice.
    /// </summary>
    public static string? PeekShipId(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            return PlanFile.ReadShipId(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Load opens the same file next and names the problem in the notice.
            return null;
        }
    }

    /// <summary>Replaces the file in one step (<see cref="AtomicFile"/>).</summary>
    /// <exception cref="IOException">The file can't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission for the folder or the file.</exception>
    /// <param name="lastScanNo">The last scan number given (D102), kept beside the plan.</param>
    public static void Save(string path, SavedPlan plan, int lastScanNo = 0) =>
        AtomicFile.Write(path, stream => PlanFile.Write(stream, plan, lastScanNo));
}

/// <summary>What <see cref="PlanStore.Load"/> read.</summary>
/// <param name="Plan">The plan; null without a file or if it couldn't be used.</param>
/// <param name="Problem">Why there is no plan, for the overlay's notice; null if none.</param>
/// <param name="LastScanNo">The last scan number given (D102), 0 without a plan.</param>
/// <param name="Unreadable">A file is there that couldn't be read: the first save keeps it aside (B13, D106).</param>
public sealed record PlanLoad(SavedPlan? Plan, string? Problem, int LastScanNo, bool Unreadable)
{
    /// <summary>Plan, problem and scan number alone, as every caller before B13 read them.</summary>
    public void Deconstruct(out SavedPlan? plan, out string? problem, out int lastScanNo) =>
        (plan, problem, lastScanNo) = (Plan, Problem, LastScanNo);
}
