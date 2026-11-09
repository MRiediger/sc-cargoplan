using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace CargoPlan.App;

/// <summary>
/// The overlay's log of failures it survived or died of (D82), and of every hotkey that fired (D97): cargoplan.log next
/// to settings.json, for a bug report. Append only; a full log moves to cargoplan.log.1, one older file is enough to see
/// what led up to a crash.
/// </summary>
public static class AppLog
{
    /// <summary>%LocalAppData%\SC-CargoPlan\cargoplan.log, where settings.json and plan.json are (D13).</summary>
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SC-CargoPlan", "cargoplan.log");

    /// <summary>The size at which the log moves to the older file before the next entry.</summary>
    public const long MaxBytes = 1024 * 1024;

    public static void Write(string context, Exception error) => WriteTo(Path, context, error);

    /// <summary>
    /// One entry: UTC time, context and the whole exception (type, message, inner exceptions, stack traces). Never throws:
    /// it runs in the sync's catch and in the crash handler, where a failing log would take the overlay down with it.
    /// </summary>
    public static void WriteTo(string path, string context, Exception error) => Append(
        path, $"{Now()} {context}{Environment.NewLine}{error}{Environment.NewLine}{Environment.NewLine}");

    public static void Write(string context, string message) => WriteTo(Path, context, message);

    /// <summary>One line: UTC time, context and message, e.g. a hotkey that fired. Never throws, like an exception entry.</summary>
    public static void WriteTo(string path, string context, string message) => Append(
        path, $"{Now()} {context}: {message}{Environment.NewLine}");

    private static string Now() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    private static void Append(string path, string entry)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            if (new FileInfo(path) is { Exists: true, Length: >= MaxBytes })
            {
                File.Move(path, path + ".1", overwrite: true);
            }

            File.AppendAllText(path, entry);
        }
        catch (Exception logFailure)
        {
            // Anything at all (D82, Martin: "logging never throws"): disk full, file locked, a folder in its place.
            Debug.WriteLine($"AppLog: {path} not written: {logFailure}{Environment.NewLine}{entry}");
        }
    }
}
