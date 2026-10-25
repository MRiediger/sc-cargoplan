using System.Diagnostics;
using System.Text.RegularExpressions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// tools/import-fleetyards.ps1 (T7.1b) run as a process against a synthetic export (TestFleetyards/): one ship with
/// usable offsets, one without, one with overlapping offsets, one without a cargo grid.
/// </summary>
public class ImportFleetyardsScriptTests
{
    private const int Deviation = 1, Failure = 2;

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "TestFleetyards");
    private static readonly string Expected = Path.Combine(Fixtures, "expected");
    private static readonly string Script = Path.Combine(AppContext.BaseDirectory, "tools", "import-fleetyards.ps1");
    private static readonly string[] ImportedIds = ["offset_ship", "overlap_ship", "row_ship"];

    // The export date in "source" comes from the file's time; fixed here so the expected files stay valid.
    private static readonly DateTime ExportTime = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Local);

    [Fact]
    public async Task Create_writes_the_expected_ship_files()
    {
        using var work = new Workspace();

        var run = await work.Run("-Create");

        run.ExitCode.Should().Be(0, run.Error);
        work.ShipIds().Should().Equal(ImportedIds);
        foreach (var id in ImportedIds)
        {
            work.Read(id).Should().Be(ExpectedText(id), $"{id}.json");
            ShipLoader.Load(work.Ships, id).Id.Should().Be(id);
        }

        run.Output.Should().MatchRegex(@"skipped\s+no_grid");
    }

    [Fact]
    public async Task New_ships_without_Create_are_reported_not_written()
    {
        using var work = new Workspace();

        var run = await work.Run();

        run.ExitCode.Should().Be(Deviation);
        run.Output.Should().MatchRegex(@"new\s+row_ship");
        work.ShipIds().Should().BeEmpty();
    }

    // Without -ShipsFolder the script takes ships/ next to its tools/ folder: the repo layout, and the test output's
    // (Core copies ships/ there). Only a compare, so nothing is written next to the test assembly.
    [Fact]
    public async Task Without_ShipsFolder_the_ships_folder_next_to_tools_is_used()
    {
        using var work = new Workspace();

        var run = await work.Run(withShipsFolder: false);

        run.ExitCode.Should().Be(Deviation, run.Error);
        run.Output.Should().MatchRegex(@"new\s+row_ship");
        File.Exists(Path.Combine(ShipLoader.DefaultFolder, "row_ship.json")).Should().BeFalse();
    }

    [Fact]
    public async Task Compare_reports_same_and_writes_nothing()
    {
        using var work = new Workspace(withExpected: true);
        var before = work.Snapshot();

        var run = await work.Run();

        run.ExitCode.Should().Be(0, run.Error);
        foreach (var id in ImportedIds)
        {
            run.Output.Should().MatchRegex($@"same\s+{id}");
        }

        work.Snapshot().Should().Equal(before);
    }

    // Origin and stack axis are hand work (hull_b, hull_c); only grid data is compared.
    [Fact]
    public async Task Compare_reports_a_grid_change_with_both_values_and_ignores_the_arrangement()
    {
        using var work = new Workspace(withExpected: true);
        work.Edit("row_ship", t => t
            .Replace("\"capacityScu\": 8,", "\"capacityScu\": 12,")
            .Replace("{ \"length\": 2, \"width\": 2, \"stack\": 2 }", "{ \"length\": 3, \"width\": 2, \"stack\": 2 }"));
        work.Edit("offset_ship", t => t.Replace("[0, -6, 3]", "[0, -4, 3]").Replace("\"+z\"", "\"-y\""));
        var before = work.Snapshot();

        var run = await work.Run();

        run.ExitCode.Should().Be(Deviation);
        run.Output.Should().MatchRegex(@"different\s+row_ship.*rack-2.*grid.*3x2x2.*2x2x2");
        run.Output.Should().MatchRegex(@"same\s+offset_ship");
        work.Snapshot().Should().Equal(before);
    }

    // A door limit is hand work until Fleetyards names its field (D70): Fleetyards' maxContainerSize only repeats the grid.
    [Fact]
    public async Task Compare_accepts_a_door_limit_set_by_hand()
    {
        using var work = new Workspace(withExpected: true);
        work.Edit("row_ship", WithDoorLimit);

        var run = await work.Run();

        run.ExitCode.Should().Be(0, run.Output);
        run.Output.Should().MatchRegex(@"same\s+row_ship");
    }

    [Fact]
    public async Task Force_rewrites_the_grid_data_and_keeps_the_hand_work()
    {
        using var work = new Workspace(withExpected: true);
        work.Edit("row_ship", t => WithDoorLimit(t)
            .Replace("\"totalScu\": 26", "\"totalScu\": 30")
            .Replace("\"capacityScu\": 8,", "\"capacityScu\": 12,")
            .Replace("{ \"length\": 2, \"width\": 2, \"stack\": 2 }", "{ \"length\": 3, \"width\": 2, \"stack\": 2 }")
            .Replace("[-3, 0, 0]", "[-4, 0, 0]")
            .Replace("rack arrangement generated", "checked by hand"));

        var run = await work.Run("-Force", "row_ship");

        run.ExitCode.Should().Be(0, run.Error);
        var ship = ShipLoader.Load(work.Ships, "row_ship");
        ship.TotalScu.Should().Be(26);
        var rack = ship.Racks.Single(r => r.Id == "rack-2");
        (rack.Grid, rack.CapacityScu, rack.MaxCrateSize).Should().Be((new Dimensions(2, 2, 2), 8, CrateSize.Four));
        rack.Origin.Should().Be(new ShipPoint(-4, 0, 0));
        ship.Notes.Should().StartWith("checked by hand");
        ship.Racks.Where(r => r.Id != "rack-2").Should().OnlyContain(r => r.MaxCrateSize == null);
    }

    /// <summary>row_ship's rack-2 (2×2×2, which takes an 8 by its grid) with a door that takes 4 SCU at most.</summary>
    private static string WithDoorLimit(string text) => Regex.Replace(
        text, @"(""origin"": \[-3, 0, 0\],\s*""stackAxis"": ""\+z"")", "$1,\n      \"maxCrateSize\": 4");

    [Theory]
    [InlineData("duplicate_id.json", "twin")]
    [InlineData("uneven.json", "hardpoint_cargo_bad")]
    public async Task A_broken_export_stops_before_writing(string export, string named)
    {
        using var work = new Workspace(export);

        var run = await work.Run("-Create");

        run.ExitCode.Should().Be(Failure);
        run.Error.Should().Contain(named);
        work.ShipIds().Should().BeEmpty();
    }

    // hull_a.json is measured in game and the fallback ship (D56): the import never writes it, not even where it's missing.
    [Fact]
    public async Task Create_never_writes_hull_a()
    {
        using var work = new Workspace("with_hull_a.json");

        var run = await work.Run("-Create");

        run.ExitCode.Should().Be(0, run.Error);
        work.ShipIds().Should().Equal("small");
        run.Output.Should().MatchRegex(@"skipped\s+hull_a\s+.*never written");
    }

    [Fact]
    public async Task Force_refuses_hull_a()
    {
        using var work = new Workspace();

        var run = await work.Run("-Force", "hull_a");

        run.ExitCode.Should().Be(Failure);
        run.Error.Should().Contain("hull_a");
    }

    private static string ExpectedText(string id) => File.ReadAllText(Path.Combine(Expected, id + ".json")).ReplaceLineEndings("\n");

    private sealed record ScriptRun(int ExitCode, string Output, string Error);

    /// <summary>A temporary folder with a copy of the export and an empty (or expected) ships folder.</summary>
    private sealed class Workspace : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("cargoplan-fleetyards-");

        public Workspace(string export = "ships.json", bool withExpected = false)
        {
            Export = Path.Combine(_root.FullName, export);
            File.Copy(Path.Combine(Fixtures, export), Export);
            File.SetLastWriteTime(Export, ExportTime);
            Ships = Directory.CreateDirectory(Path.Combine(_root.FullName, "ships")).FullName;
            if (withExpected)
            {
                foreach (var id in ImportedIds)
                {
                    File.WriteAllText(Path.Combine(Ships, id + ".json"), ExpectedText(id));
                }
            }
        }

        public string Export { get; }

        public string Ships { get; }

        public IReadOnlyList<string> ShipIds() =>
            [.. Directory.EnumerateFiles(Ships, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.Ordinal)];

        public string Read(string id) => File.ReadAllText(Path.Combine(Ships, id + ".json")).ReplaceLineEndings("\n");

        public void Edit(string id, Func<string, string> change)
        {
            var path = Path.Combine(Ships, id + ".json");
            var text = File.ReadAllText(path);
            var changed = change(text);
            changed.Should().NotBe(text, "the test edit must change the file");
            File.WriteAllText(path, changed);
        }

        /// <summary>Every file's bytes and time, to prove a run wrote nothing.</summary>
        public IReadOnlyList<string> Snapshot() =>
        [
            .. Directory.EnumerateFiles(Ships).Order(StringComparer.Ordinal)
                .Select(f => $"{f} {File.GetLastWriteTimeUtc(f):O} {Convert.ToHexString(File.ReadAllBytes(f))}"),
        ];

        public Task<ScriptRun> Run(params string[] arguments) => Run(withShipsFolder: true, arguments);

        public async Task<ScriptRun> Run(bool withShipsFolder, params string[] arguments)
        {
            ProcessStartInfo start = new(Shell())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            string[] folder = withShipsFolder ? ["-ShipsFolder", Ships] : [];
            foreach (var argument in (string[])["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Script,
                         "-Source", Export, .. folder, .. arguments])
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start) ?? throw new InvalidOperationException($"{start.FileName} didn't start.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new ScriptRun(process.ExitCode, await output, await error);
        }

        public void Dispose() => _root.Delete(recursive: true);

        // pwsh where installed (the Linux runner has it), else Windows PowerShell 5.1: the script supports both.
        private static string Shell()
        {
            var pwsh = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";
            var onPath = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                .Any(dir => dir.Length > 0 && File.Exists(Path.Combine(dir, pwsh)));
            return onPath ? pwsh
                : OperatingSystem.IsWindows() ? "powershell.exe"
                : throw new InvalidOperationException("The import script test needs pwsh (PowerShell 7) on the PATH.");
        }
    }
}
