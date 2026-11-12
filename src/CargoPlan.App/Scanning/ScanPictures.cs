using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CargoPlan.Import.Contracts;

namespace CargoPlan.App.Scanning;

/// <summary>
/// <c>--keep-scans</c> (T10.5, D108): the region each scan read, as a PNG in <see cref="DefaultFolder"/>, for a bug report
/// from a screen size nobody checked. The last <see cref="MaxPictures"/> stay; the name says when, which view and what came of
/// it, so a picture finds its entry in scans.log. Off by default: the pictures are CIG material and stay with the player.
/// Writing never throws: a failure goes to cargoplan.log (D82) and the scan goes on.
/// </summary>
/// <param name="now">The local time for the name; <see cref="DateTime.Now"/> in the app.</param>
internal sealed partial class ScanPictures(string folder, Func<DateTime>? now = null)
{
    public const int MaxPictures = 20;

    private readonly Func<DateTime> _now = now ?? (() => DateTime.Now);

    public static string DefaultFolder => Path.Combine(AppFolders.LocalRoot, "scans");

    /// <summary>"20261112-155012-offer-added-3.png"; without a read text no view: "20261112-155012-nocontractinframe.png".</summary>
    public static string Name(DateTime time, ContractView? view, ScanReport report) => string.Concat(
        time.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
        view is { } v ? "-" + v.ToString().ToLowerInvariant() : "",
        "-",
        report.Kind.ToString().ToLowerInvariant(),
        report.ScanNo is { } no ? "-" + no.ToString(CultureInfo.InvariantCulture) : "",
        ".png");

    /// <summary>Writes <paramref name="png"/> and drops the oldest beyond <see cref="MaxPictures"/>; the name, or null when nothing was written.</summary>
    public string? Save(byte[]? png, ContractView? view, ScanReport report)
    {
        if (png is null)
        {
            return null;
        }

        var name = Name(_now(), view, report);
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name), png);
        }
        catch (Exception ex)
        {
            // Anything at all (D82): a picture that can't be written costs the picture, never the scan.
            AppLog.Write("Scan picture", ex);
            return null;
        }

        DropOldest();
        return name;
    }

    /// <summary>Only our names, oldest first by name (the time leads it); a file of another name stays.</summary>
    private void DropOldest()
    {
        try
        {
            var ours = Directory.EnumerateFiles(folder, "*.png")
                .Where(path => OurName().IsMatch(Path.GetFileName(path)))
                .Order(StringComparer.Ordinal)
                .ToList();
            foreach (var path in ours.Take(Math.Max(0, ours.Count - MaxPictures)))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write("Scan picture", ex);
        }
    }

    // Source: https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-source-generators
    [GeneratedRegex(@"^\d{8}-\d{6}-[a-z0-9-]+\.png$", RegexOptions.CultureInvariant)]
    private static partial Regex OurName();
}
