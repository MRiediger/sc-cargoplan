using System.IO;

namespace CargoPlan.App;

/// <summary>The overlay's log of failures it survived or died of (D82): cargoplan.log next to settings.json.</summary>
public static class AppLog
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SC-CargoPlan", "cargoplan.log");

    public const long MaxBytes = 1024 * 1024;

    public static void Write(string context, Exception error) => WriteTo(Path, context, error);

    public static void WriteTo(string path, string context, Exception error)
    {
    }
}
