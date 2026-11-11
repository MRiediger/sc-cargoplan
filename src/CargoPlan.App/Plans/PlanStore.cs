// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
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
    /// plus a problem text for the overlay; the file is left as it is until the player makes a new plan.
    /// </summary>
    /// <returns>The plan or a problem, and the last scan number given (D102, 0 without a plan).</returns>
    public static (SavedPlan? Plan, string? Problem, int LastScanNo) Load(string path, Ship ship)
    {
        if (!File.Exists(path))
        {
            return (null, null, 0);
        }

        try
        {
            using var stream = File.OpenRead(path);
            var (plan, lastScanNo) = PlanFile.ReadContent(stream, path, ship);
            return (plan, null, lastScanNo);
        }
        catch (Exception ex) when (ex is PlanFormatException or IOException or UnauthorizedAccessException)
        {
            var problem = $"Plan not loaded ({ex.Message}). The file stays until you make a new plan.";
            AppLog.Write("Plan", $"{problem} [{ex.GetType().Name}]");
            return (null, problem, 0);
        }
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
