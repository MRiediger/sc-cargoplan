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

        // row_ship's rack-2 (2x2x2, takes an 8) says limits.max.capacity 1: maybe a default, so not written, only named.
        run.Output.Should().MatchRegex(@"same\s+row_ship.*\(limits\.max 1 unverified: rack-2\)");
        work.Snapshot().Should().Equal(before);
    }

    [Fact]
    public async Task Compare_reports_a_grid_change_with_both_values()
    {
        using var work = new Workspace(withExpected: true);
        work.Edit("row_ship", t => t
            .Replace("\"capacityScu\": 8,", "\"capacityScu\": 12,")
            .Replace("{ \"length\": 2, \"width\": 2, \"stack\": 2 }", "{ \"length\": 3, \"width\": 2, \"stack\": 2 }"));
        var before = work.Snapshot();

        var run = await work.Run();

        run.ExitCode.Should().Be(Deviation);
        run.Output.Should().MatchRegex(@"different\s+row_ship.*rack-2.*grid.*3x2x2.*2x2x2");
        work.Snapshot().Should().Equal(before);
    }

    // A generated row is no Fleetyards data: its origins and stack axes are hand work, not compared (K5.2).
    [Fact]
    public async Task Compare_ignores_the_arrangement_of_a_generated_row()
    {
        using var work = new Workspace(withExpected: true);
        work.Edit("row_ship", t => t.Replace("[-3, 0, 0]", "[-4, 0, 0]").Replace("\"+z\"", "\"-y\""));

        var run = await work.Run();

        run.ExitCode.Should().Be(0, run.Output);
        run.Output.Should().MatchRegex(@"same\s+row_ship");
    }

    // Holo and camera hang on the middle of the ship's bounds (D63, D76), so Fleetyards' arrangement is compared up to a
    // shift of the whole ship; a stack axis that lays the grid in the same box is the player's (K5.2, D78).
    [Fact]
    public async Task Compare_accepts_a_shifted_arrangement_and_a_stack_axis_that_fills_the_same_box()
    {
        using var work = new Workspace(withExpected: true);
        work.Edit("offset_ship", t => t.Replace("[0, -2, 0]", "[5, 0, 1]").Replace("[0, -6, 3]", "[5, -4, 4]")
            .Replace("\"stackAxis\": \"+z\"\n    },", "\"stackAxis\": \"-z\"\n    },"));

        var run = await work.Run();

        run.ExitCode.Should().Be(0, run.Output);
        run.Output.Should().MatchRegex(@"same\s+offset_ship");
    }

    [Fact]
    public async Task Compare_reports_a_rack_that_lies_elsewhere_than_in_Fleetyards()
    {
        using var work = new Workspace(withExpected: true);
        work.Edit("offset_ship", t => t.Replace("[0, -6, 3]", "[0, -4, 3]"));
        var before = work.Snapshot();

        var run = await work.Run();

        run.ExitCode.Should().Be(Deviation, run.Output);
        run.Output.Should().MatchRegex(@"different\s+offset_ship.*arrangement");
        work.Snapshot().Should().Equal(before);
    }

    // A hold turned 90° about Fleetyards y (the Hull C's upright arms) swaps its x and z extents; unturned the two holds
    // overlap, turned they touch. The grid stays as the hold gives it; the stack axis is one that lays it in its box.
    [Fact]
    public async Task Create_lays_a_turned_hold_in_its_turned_box()
    {
        using var work = new Workspace("rotated.json");

        var run = await work.Run("-Create");

        run.ExitCode.Should().Be(0, run.Error);
        var ship = ShipLoader.Load(work.Ships, "cross_ship");
        ship.Notes.Should().StartWith("rack positions from Fleetyards;");
        ship.Racks.Select(r => (r.Id, r.Grid, r.Origin, r.StackAxis)).Should().Equal(
            ("rack-0", new Dimensions(2, 2, 1), new ShipPoint(0, -5, 0), StackAxis.PlusZ),
            ("rack-1", new Dimensions(2, 2, 1), new ShipPoint(0, -3, 0), StackAxis.PlusY));
        ship.Racks[1].Bounds.Should().Be(new ShipBox(new ShipPoint(0, -3, 0), new ShipPoint(2, -2, 2)));
    }

    // -Force lays the racks where Fleetyards has them; a stack axis that fits the box stays, one that doesn't is replaced
    // by the first that does, same sign first (K5.2, D78).
    [Fact]
    public async Task Force_lays_a_Fleetyards_arrangement_and_keeps_only_a_stack_axis_that_fits()
    {
        using var work = new Workspace("rotated.json");
        (await work.Run("-Create")).ExitCode.Should().Be(0);
        work.Edit("cross_ship", t => t.Replace("\"stackAxis\": \"+y\"", "\"stackAxis\": \"-y\"")
            .Replace("[0, -5, 0]", "[4, 0, 0]").Replace("\"stackAxis\": \"+z\"", "\"stackAxis\": \"+y\""));
        (await work.Run()).Output.Should().MatchRegex(@"different\s+cross_ship.*arrangement");

        var run = await work.Run("-Force", "cross_ship");

        run.ExitCode.Should().Be(0, run.Error);
        ShipLoader.Load(work.Ships, "cross_ship").Racks.Select(r => (r.Origin, r.StackAxis)).Should().Equal(
            (new ShipPoint(0, -5, 0), StackAxis.PlusZ),
            (new ShipPoint(0, -3, 0), StackAxis.MinusY));
        (await work.Run()).Output.Should().MatchRegex(@"same\s+cross_ship");
    }

    // A rack whose id and grid Fleetyards still has keeps all hand work; new racks join behind the last one that takes more
    // than 2 SCU, vanished ones go and are named; a generated row is laid anew (K5.2, Fortune and Moth).
    [Fact]
    public async Task Force_with_a_changed_rack_set_keeps_the_racks_it_can_map()
    {
        using var work = new Workspace(withExpected: true);
        File.WriteAllText(Path.Combine(work.Ships, "row_ship.json"), """
            {
              "schemaVersion": 5,
              "id": "row_ship",
              "name": "TSTM Row Ship",
              "manufacturer": "TSTM",
              "fleetyardsSlug": "tstm-row-ship",
              "source": "Fleetyards API (CIG ship matrix), export 2026-01-01",
              "notes": "rack arrangement generated; grids from Fleetyards",
              "totalScu": 20,
              "racks": [
                { "id": "rack-0", "capacityScu": 2, "grid": { "length": 2, "width": 1, "stack": 1 }, "origin": [0, 0, 0], "stackAxis": "+z" },
                { "id": "rack-1", "capacityScu": 16, "grid": { "length": 2, "width": 4, "stack": 2 }, "origin": [-3, 0, 0], "stackAxis": "-z", "maxCrateSize": 8 },
                { "id": "rack-9", "capacityScu": 2, "grid": { "length": 2, "width": 1, "stack": 1 }, "origin": [-9, 0, 0], "stackAxis": "+z" }
              ]
            }
            """);

        var run = await work.Run("-Force", "row_ship");

        run.ExitCode.Should().Be(0, run.Error);
        run.Output.Should().MatchRegex(@"updated\s+row_ship.*rack-9");
        var ship = ShipLoader.Load(work.Ships, "row_ship");
        ship.TotalScu.Should().Be(26);
        ship.Racks.Select(r => (r.Id, r.Origin, r.StackAxis, r.MaxCrateSize)).Should().Equal(
            ("rack-0", new ShipPoint(0, 0, 0), StackAxis.PlusZ, (CrateSize?)null),
            ("rack-1", new ShipPoint(-5, 0, 0), StackAxis.MinusZ, CrateSize.Eight),
            ("rack-2", new ShipPoint(-8, 0, 0), StackAxis.PlusZ, (CrateSize?)null));
        ship.Notes.Should().EndWith("; racks rack-2 added, rack-9 dropped (Fleetyards export 2026-01-15)");
        (await work.Run()).Output.Should().MatchRegex(@"same\s+row_ship");
    }

    [Fact]
    public async Task Force_regenerates_a_file_none_of_whose_racks_map()
    {
        using var work = new Workspace(withExpected: true);
        File.WriteAllText(Path.Combine(work.Ships, "row_ship.json"), """
            {
              "schemaVersion": 5,
              "id": "row_ship",
              "name": "TSTM Row Ship",
              "totalScu": 2,
              "racks": [
                { "id": "rack-7", "capacityScu": 2, "grid": { "length": 2, "width": 1, "stack": 1 }, "origin": [0, 0, 0], "stackAxis": "+z" }
              ]
            }
            """);

        var run = await work.Run("-Force", "row_ship");

        run.ExitCode.Should().Be(0, run.Error);
        work.Read("row_ship").Should().Be(ExpectedText("row_ship").Replace(
            "\"notes\": \"rack arrangement generated; grids from Fleetyards\"",
            "\"notes\": \"rack arrangement generated; grids from Fleetyards; regenerated from Fleetyards export 2026-01-15 (rack set changed)\""));
    }

    // offset_ship's rack-1 (2x4x2 cells, takes a 16) has limits.max.capacity 2: the door comes from the export (D70).
    [Fact]
    public async Task Compare_reports_a_door_limit_missing_from_the_file()
    {
        using var work = new Workspace(withExpected: true);
        work.Edit("offset_ship", t => Regex.Replace(t, @",\s*""maxCrateSize"": 2", ""));

        var run = await work.Run();

        run.ExitCode.Should().Be(Deviation, run.Output);
        run.Output.Should().MatchRegex(@"different\s+offset_ship.*rack-1: maxCrateSize not in file, Fleetyards 2");
    }

    [Fact]
    public async Task Force_adds_a_missing_door_limit_and_keeps_one_that_differs()
    {
        using var work = new Workspace(withExpected: true);
        work.Edit("offset_ship", t => Regex.Replace(t, @",\s*""maxCrateSize"": 2", "").Replace("; door limits from Fleetyards limits.max.capacity", ""));
        work.Edit("overlap_ship", t => t.Replace("\"stackAxis\": \"+z\"\n    }\n  ]", "\"stackAxis\": \"+z\",\n      \"maxCrateSize\": 4\n    }\n  ]"));

        (await work.Run("-Force", "offset_ship")).ExitCode.Should().Be(0);
        var compare = await work.Run();

        ShipLoader.Load(work.Ships, "offset_ship").Racks.Select(r => r.MaxCrateSize).Should().Equal(null, CrateSize.Two);
        ShipLoader.Load(work.Ships, "offset_ship").Notes.Should().EndWith("; door limits from Fleetyards limits.max.capacity", "a door from the export says so, as -Create's notes do (K5.2)");
        compare.Output.Should().MatchRegex(@"same\s+offset_ship");
        ShipLoader.Load(work.Ships, "overlap_ship").Racks[^1].MaxCrateSize.Should().Be(CrateSize.Four);
    }

    // overlap_ship's rack-0 says limits.min.capacity 8 (K3.3); row_ship's wide hold says 0, which counts as none.
    [Fact]
    public async Task Compare_reports_a_minimum_missing_from_the_file()
    {
        using var work = new Workspace(withExpected: true);
        work.Edit("overlap_ship", t => Regex.Replace(t, @",\s*""minCrateSize"": 8", ""));

        var run = await work.Run();

        run.ExitCode.Should().Be(Deviation, run.Output);
        run.Output.Should().MatchRegex(@"different\s+overlap_ship.*rack-0: minCrateSize not in file, Fleetyards 8");
    }

    // A minimum above the app's largest crate is clamped to 32 like a door; a door of 32 says nothing on an 8x2x2 grid.
    [Fact]
    public async Task Create_clamps_a_minimum_above_32()
    {
        using var work = new Workspace("min_clamped.json");

        var run = await work.Run("-Create");

        run.ExitCode.Should().Be(0, run.Error);
        var rack = ShipLoader.Load(work.Ships, "big_spindle").Racks.Single();
        (rack.MinCrateSize, rack.MaxCrateSize).Should().Be((CrateSize.ThirtyTwo, (CrateSize?)null));
    }

    // A door limit Fleetyards doesn't name is the player's (a hand value), no deviation.
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
    [InlineData("bad_limit.json", "limits.max.capacity 3")]
    [InlineData("bad_rotation.json", "rotation 45")]
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

    // The calibration (K5.2, D78): the Hull A's four Fleetyards holds, laid by the import's mapping, give the shipped
    // hull_a.json up to a shift of the whole ship. Compared, never written.
    [Fact]
    public async Task Compare_finds_the_shipped_hull_a_where_Fleetyards_lays_it()
    {
        using var work = new Workspace("with_hull_a.json");
        work.CopyShipped("hull_a");
        var before = work.Snapshot();

        var run = await work.Run();

        run.Output.Should().MatchRegex(@"same\s+hull_a");
        work.Snapshot().Should().Equal(before);
    }

    // The same check catches the Hull A of before K5.2, one cell between the plates: the compare isn't empty.
    [Fact]
    public async Task Compare_reports_the_Hull_A_of_before_K52_as_laid_elsewhere()
    {
        using var work = new Workspace("with_hull_a.json");
        work.CopyShipped("hull_a");
        work.Edit("hull_a", t => t.Replace("[0, -4, 3]", "[0, -2, 3]").Replace("[0, -4, 0]", "[0, -2, 0]"));

        var run = await work.Run();

        run.Output.Should().MatchRegex(@"different\s+hull_a.*arrangement");
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

        /// <summary>The shipped file of this ship (ships/ next to the test assembly) into the ships folder.</summary>
        public void CopyShipped(string id) =>
            File.Copy(Path.Combine(ShipLoader.DefaultFolder, id + ".json"), Path.Combine(Ships, id + ".json"));

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
