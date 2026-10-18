// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
using System.IO;

namespace CargoPlan.App;

/// <summary>Replaces a file in one step, for settings.json and plan.json (D13, D18).</summary>
public static class AtomicFile
{
    /// <summary>
    /// Writes to a temp file first, then replaces the real one: a crash while writing leaves the old file,
    /// never half a file. Creates the folder if needed.
    /// Source: https://learn.microsoft.com/dotnet/api/system.io.file.move (overwrite overload, .NET Core 3.0+)
    /// </summary>
    /// <exception cref="IOException">The file can't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission for the folder or the file.</exception>
    public static void Write(string path, Action<Stream> write)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var tempPath = path + ".tmp";
        using (var stream = File.Create(tempPath))
        {
            write(stream);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}
