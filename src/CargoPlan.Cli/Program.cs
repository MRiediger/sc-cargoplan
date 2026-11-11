using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using CargoPlan.Cli;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Rendering;
using CargoPlan.Core.Ships;
using CargoPlan.Import.Contracts;
using CargoPlan.Ocr;
using SkiaSharp;

const int ExitSuccess = 0;
const int ExitFailed = 1;
const int ExitUsage = 2;

var command = Command.Parse(args);

// The renderers write "·", "×", "–" and "⚠"; the default console code page (e.g. 850) has no "–".
// The setter calls SetConsoleOutputCP, which changes the console shared with the calling shell, so restore it.
// Without a console (output redirected) the call fails silently.
// Source: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Console/src/System/ConsolePal.Windows.cs
var originalEncoding = Console.OutputEncoding;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
try
{
    return command switch
    {
        null => Usage(),
        PlanCommand plan => Plan(plan),
        ScanCommand scan => Scan(scan),
        VersionCommand => Version(),
        _ => throw new UnreachableException($"Unknown command {command}."),
    };
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                               or MissionFormatException or ShipFolderException or ShipNotFoundException or ShipFormatException
                               or PlanException)
{
    Console.Error.WriteLine(ex.Message);
    return ExitFailed;
}
finally
{
    Console.OutputEncoding = originalEncoding;
}

// Inside the try: the ships line needs the ships folder, and a missing one is the same message as for plan (D56).
int Usage()
{
    Console.Error.WriteLine("Usage: CargoPlan.Cli plan <missions.json> [--ship <id>]");
    Console.Error.WriteLine("       CargoPlan.Cli scan <screenshot.png>");
    Console.Error.WriteLine("       CargoPlan.Cli --version");
    Console.Error.WriteLine($"Ships: {string.Join(", ", LoadShips().Ships.Select(s => s.Id))} (default: {Command.DefaultShipId})");
    return ExitUsage;
}

int Plan(PlanCommand plan)
{
    var ship = LoadShips().Get(plan.ShipId);
    // The file's fill order, so a shared route shows here as in the overlay (D72); without one bow first, as before.
    var file = MissionFileReader.Read(plan.MissionsPath);
    var stops = file.Stops.ToStops(CrateCap.Of(ship));
    Console.WriteLine(PlanTextRenderer.Render(LoadPlanner.Plan(ship, stops, file.FillOrder ?? FillOrder.BowFirst, file.FillRows ?? FillRows.Lengthwise, file.Grouping ?? PlanGrouping.None, file.SectionGap ?? 0), ship));
    return ExitSuccess;
}

// A broken ship file costs only its ship; the reason goes to stderr, so a skipped --ship is explained (D56).
ShipCatalog LoadShips()
{
    var catalog = ShipLoader.LoadAll();
    foreach (var problem in catalog.Problems)
    {
        Console.Error.WriteLine($"Skipped: {problem.Message}");
    }

    return catalog;
}

// The offline path of the scan (T10.1a): a screenshot of the game window's client area instead of the screen, the same
// region and OCR as in the overlay. The raw text goes to stdout for tests and bug reports, then the contract the parser
// read from it with its content key (T10.2a); the stage times go to stderr.
int Scan(ScanCommand scan)
{
    if (!File.Exists(scan.PicturePath))
    {
        Console.Error.WriteLine($"File not found: {scan.PicturePath}");
        return ExitFailed;
    }

    // Decode returns null for a file it can't read. Source: https://learn.microsoft.com/dotnet/api/skiasharp.skbitmap.decode
    using var picture = SKBitmap.Decode(scan.PicturePath);
    if (picture is null)
    {
        Console.Error.WriteLine($"Not a picture: {scan.PicturePath}");
        return ExitFailed;
    }

    using var engine = OcrEngine.Load();
    var result = new ScanContractSource(engine).Read(picture);
    var t = result.Timings;
    Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{picture.Width}x{picture.Height}: {t.Total.TotalMilliseconds:0} ms (region {t.Region.TotalMilliseconds:0}, up2-gray {t.Preprocess.TotalMilliseconds:0}, OCR {t.Ocr.TotalMilliseconds:0}, lines {t.Lines.TotalMilliseconds:0})"));
    if (result.Outcome == ScanOutcome.NoContractInFrame)
    {
        Console.Error.WriteLine("No contract in frame.");
        return ExitFailed;
    }

    Console.WriteLine(result.RawText);
    Console.WriteLine("--- parsed ---");
    Console.Write(ScannedContractTextRenderer.Render(ScannedContract.Read(result.RawText)));
    return ExitSuccess;
}

// Only the version, like `dotnet --version`: the same string as in the settings window's footer, for bug reports.
// The commit hash after "+" stays: it names the exact build.
// Source: https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/8.0/source-link
int Version()
{
    Console.WriteLine(Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? "unknown");
    return ExitSuccess;
}

/// <summary>Command line, parsed by hand (D8).</summary>
internal abstract record Command
{
    public const string DefaultShipId = ShipLoader.FallbackShipId;

    /// <summary>null = not a valid command line.</summary>
    public static Command? Parse(string[] args) => args switch
    {
        ["plan", var path] when !IsOption(path) => new PlanCommand(path, DefaultShipId),
        ["plan", var path, "--ship", var shipId] when !IsOption(path) && !IsOption(shipId) => new PlanCommand(path, shipId),
        ["scan", var path] when !IsOption(path) => new ScanCommand(path),
        ["--version"] => new VersionCommand(),
        _ => null,
    };

    private static bool IsOption(string arg) => arg.StartsWith("--", StringComparison.Ordinal);
}

/// <summary>plan &lt;missions.json&gt; [--ship &lt;id&gt;]</summary>
internal sealed record PlanCommand(string MissionsPath, string ShipId) : Command;

/// <summary>scan &lt;screenshot.png&gt;</summary>
internal sealed record ScanCommand(string PicturePath) : Command;

/// <summary>--version</summary>
internal sealed record VersionCommand : Command;
