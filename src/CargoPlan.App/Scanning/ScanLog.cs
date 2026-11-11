using System.Globalization;
using System.IO;
using System.Text;

namespace CargoPlan.App.Scanning;

/// <summary>
/// scans.log next to plan.json (T10.2b2, D103): the last <see cref="MaxEntries"/> reads, each with its result and raw text,
/// for a bug report and for new fixtures. The raw text stays out of plan.json. Writing never throws: a failure goes to
/// cargoplan.log (D82) and the scan goes on.
/// </summary>
internal sealed class ScanLog(string path)
{
    public const int MaxEntries = 50;

    /// <summary>The line that starts an entry: "=== &lt;UTC&gt; &lt;result&gt;", the raw text below it.</summary>
    private const string EntryStart = "=== ";

    /// <summary>One writer at a time: a scan on the pool, a test in parallel.</summary>
    private static readonly Lock Gate = new();

    public static string DefaultPath { get; } = Path.Combine(AppFolders.LocalRoot, "scans.log");

    /// <summary>Appends an entry and drops the oldest beyond <see cref="MaxEntries"/>; the file replaced in one step.</summary>
    public void Write(string rawText, string result)
    {
        try
        {
            lock (Gate)
            {
                List<string> entries = [.. Entries(File.Exists(path) ? File.ReadAllText(path) : "")];
                entries.Add(Invariant($"{EntryStart}{DateTime.UtcNow:O} {result}\n{rawText.ReplaceLineEndings("\n")}\n"));
                var kept = string.Concat(entries.Skip(Math.Max(0, entries.Count - MaxEntries)));
                AtomicFile.Write(path, stream => stream.Write(Encoding.UTF8.GetBytes(kept)));
            }
        }
        catch (Exception ex)
        {
            // Anything at all (D82): a scan log that can't be written costs the log, never the scan.
            AppLog.Write("Scan log", ex);
        }
    }

    /// <summary>The file split at each entry's first line. A raw line that starts the same way would split too; OCR text has none.</summary>
    private static IEnumerable<string> Entries(string text)
    {
        var entry = new StringBuilder();
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith(EntryStart, StringComparison.Ordinal) && entry.Length > 0)
            {
                yield return entry.ToString();
                entry.Clear();
            }

            if (line.Length > 0 || entry.Length > 0)
            {
                entry.Append(line).Append('\n');
            }
        }

        if (entry.Length > 0)
        {
            yield return entry.ToString().TrimEnd('\n') + "\n";
        }
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
