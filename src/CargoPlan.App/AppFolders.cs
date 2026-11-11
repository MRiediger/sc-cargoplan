using System.IO;

namespace CargoPlan.App;

/// <summary>
/// Where the overlay keeps the player's files: settings, plan, log, hologram cache (D13) and, from T10.2b, the scan log.
/// One place for the folder, so a new file doesn't copy the path (review F14).
/// </summary>
public static class AppFolders
{
    /// <summary>%LocalAppData%\SC-CargoPlan, not the roaming %AppData%: window bounds belong to this machine (D13).</summary>
    public static string LocalRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SC-CargoPlan");
}
