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
using CargoPlan.Import.ContractTracker;
using Microsoft.Data.Sqlite;

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
        ContractsCommand contracts => Contracts(contracts),
        VersionCommand => Version(),
        _ => throw new UnreachableException($"Unknown command {command}."),
    };
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException
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
    Console.Error.WriteLine("       CargoPlan.Cli contracts <contracts.db> [--since <id>] [--raw]");
    Console.Error.WriteLine("       CargoPlan.Cli --version");
    Console.Error.WriteLine($"Ships: {string.Join(", ", LoadShips().Ships.Select(s => s.Id))} (default: {Command.DefaultShipId})");
    return ExitUsage;
}

int Plan(PlanCommand plan)
{
    var ship = LoadShips().Get(plan.ShipId);
    var stops = MissionFileReader.Read(plan.MissionsPath).ToStops(CrateCap.Of(ship));
    Console.WriteLine(PlanTextRenderer.Render(LoadPlanner.Plan(ship, stops), ship));
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

// For bug reports: the output with --raw holds what a test fixture needs (T5.3).
int Contracts(ContractsCommand contracts)
{
    // The reader reads nothing from a missing file and stays silent (the settings window says "not found", D44);
    // on the command line a mistyped path must not look like "no new contracts".
    if (!File.Exists(contracts.DatabasePath))
    {
        Console.Error.WriteLine($"File not found: {contracts.DatabasePath}");
        return ExitFailed;
    }

    var result = ContractTrackerReader.ReadSince(contracts.DatabasePath, contracts.SinceId);
    if (result.Notice is { } notice)
    {
        Console.Error.WriteLine(notice);
        return ExitFailed;
    }

    // Same rule as the overlay (D44): ids that never reached --since mean a new database, read from the start.
    if (result.DatabaseLooksNew)
    {
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture, $"Database looks new (no id ever reached {contracts.SinceId}): reading from the start."));
    }

    Console.WriteLine(result.Candidates.Count == 0
        ? string.Create(CultureInfo.InvariantCulture, $"No contracts above id {contracts.SinceId}.")
        : string.Join(Environment.NewLine, result.Candidates.Select(c => CandidateTextRenderer.Render(c, contracts.Raw))));
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
        ["contracts", var path, .. var options] when !IsOption(path) => ContractsCommand.Parse(path, options),
        ["--version"] => new VersionCommand(),
        _ => null,
    };

    private static bool IsOption(string arg) => arg.StartsWith("--", StringComparison.Ordinal);
}

/// <summary>plan &lt;missions.json&gt; [--ship &lt;id&gt;]</summary>
internal sealed record PlanCommand(string MissionsPath, string ShipId) : Command;

/// <summary>--version</summary>
internal sealed record VersionCommand : Command;

/// <summary>contracts &lt;contracts.db&gt; [--since &lt;id&gt;] [--raw], options in any order.</summary>
internal sealed record ContractsCommand(string DatabasePath, long SinceId, bool Raw) : Command
{
    public static ContractsCommand? Parse(string path, string[] options)
    {
        var command = new ContractsCommand(path, 0, false);
        for (var i = 0; i < options.Length; i++)
        {
            if (options[i] == "--raw" && !command.Raw)
            {
                command = command with { Raw = true };
            }
            else if (options[i] == "--since" && i + 1 < options.Length
                     && long.TryParse(options[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var sinceId))
            {
                command = command with { SinceId = sinceId };
                i++;
            }
            else
            {
                return null;
            }
        }

        return command;
    }
}
