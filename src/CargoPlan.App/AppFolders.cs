using System.IO;

namespace CargoPlan.App;

/// <summary>
/// Where the overlay keeps the player's files: settings, plan, log, hologram cache (D13) and, from T10.2b, the scan log.
/// One place for the folder, so a new file doesn't copy the path (review F14). <c>--data-dir</c> moves all of them for a
/// run (T10.4a): every path built on <see cref="LocalRoot"/> is computed when read, never kept from before the move.
/// </summary>
public static class AppFolders
{
    /// <summary>%LocalAppData%\SC-CargoPlan, not the roaming %AppData%: window bounds belong to this machine (D13).</summary>
    public static string Profile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SC-CargoPlan");

    /// <summary>The folder of this run: <see cref="Profile"/>, or the one <see cref="Use"/> named.</summary>
    public static string LocalRoot { get; private set; } = Profile;

    /// <summary>
    /// Moves every file of the run into <paramref name="folder"/> and creates it: a test or gate run that never touches the
    /// player's profile (T10.1b wrote into it). Called once at start, before anything reads a path.
    /// </summary>
    /// <exception cref="IOException">The folder can't be created.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission for the folder.</exception>
    public static void Use(string folder)
    {
        Directory.CreateDirectory(folder);
        LocalRoot = folder;
        AppLog.Path = Path.Combine(folder, AppLog.FileName);
    }

    /// <summary>Back to <see cref="Profile"/> without creating anything; for the tests of <see cref="Use"/>.</summary>
    internal static void UseProfile() => LocalRoot = Profile;
}
