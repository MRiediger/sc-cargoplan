namespace CargoPlan.Core;

/// <summary>
/// The player's folder of the app, %LocalAppData%\SC-CargoPlan (D13): settings, plan, logs, holograms and, from T12.3, the
/// downloaded ship data. Here, not in the app, so the CLI finds the same ship data as the overlay.
/// </summary>
public static class AppProfile
{
    public const string FolderName = "SC-CargoPlan";

    /// <summary>The folder for this user; the app's <c>--data-dir</c> moves a run elsewhere.</summary>
    public static string Folder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);
}
