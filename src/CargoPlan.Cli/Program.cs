using System.Text;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Rendering;
using CargoPlan.Core.Ships;

const int ExitSuccess = 0;
const int ExitFailed = 1;
const int ExitUsage = 2;

if (Options.Parse(args) is not { } options)
{
    Console.Error.WriteLine("Usage: CargoPlan.Cli plan <missions.json> [--ship <id>]");
    Console.Error.WriteLine($"Ships: {string.Join(", ", ShipLoader.ListShips())} (default: {Options.DefaultShipId})");
    return ExitUsage;
}

// The renderer writes "·", "×" and "–"; the default console code page (e.g. 850) has no "–".
// The setter calls SetConsoleOutputCP, which changes the console shared with the calling shell, so restore it.
// Without a console (output redirected) the call fails silently.
// Source: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Console/src/System/ConsolePal.Windows.cs
var originalEncoding = Console.OutputEncoding;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
try
{
    var stops = MissionFileReader.Read(options.MissionsPath);
    var ship = ShipLoader.Load(options.ShipId);
    var plan = LoadPlanner.Plan(ship, stops);
    Console.WriteLine(PlanTextRenderer.Render(plan, ship));
    return ExitSuccess;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                               or MissionFormatException or ShipNotFoundException or ShipFormatException or PlanException)
{
    Console.Error.WriteLine(ex.Message);
    return ExitFailed;
}
finally
{
    Console.OutputEncoding = originalEncoding;
}

/// <summary>Command line: plan &lt;missions.json&gt; [--ship &lt;id&gt;]. Parsed by hand, see D8.</summary>
internal sealed record Options(string MissionsPath, string ShipId)
{
    public const string DefaultShipId = "hull_a";

    /// <summary>null = not a valid command line.</summary>
    public static Options? Parse(string[] args) => args switch
    {
        ["plan", var path] when !IsOption(path) => new Options(path, DefaultShipId),
        ["plan", var path, "--ship", var shipId] when !IsOption(path) && !IsOption(shipId) => new Options(path, shipId),
        _ => null,
    };

    private static bool IsOption(string arg) => arg.StartsWith("--", StringComparison.Ordinal);
}
