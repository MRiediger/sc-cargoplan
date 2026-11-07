using System.Diagnostics;
using System.Text.Json.Nodes;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// tools/import-motherlode.ps1 (v0.3.0 session 4a, D92) run as a process against a synthetic grid catalog
/// (TestMotherlode/catalog.json, the field names of Motherlode's API, made-up numbers): a Hull A pattern, a RAFT stacking
/// down, a Prowler with racks rolled by 14 degrees, a grid turned by a right angle, a ship without transforms, a variant
/// and the TS module's Aurora. Frozen copies (FrozenShips/) stand for the ships' files before the import.
/// </summary>
public class ImportMotherlodeScriptTests
{
    private const int Deviation = 1, Failure = 2;
    private const double Cell = 1.25;
    private const string Source = "cryon.rocks (Motherlode) 4.10.2-LIVE.12913470, 2026-11-07";

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "TestMotherlode");
    private static readonly string Script = Path.Combine(AppContext.BaseDirectory, "tools", "import-motherlode.ps1");
    private static readonly string[] Frozen = ["hull_a", "raft", "prowler_utility", "135c", "mustang_alpha", "aurora_mk_ii"];

    // The Hull A pattern: plates 3 m from the middle (game x), floor centres at z 2.15 and -1.25. The port upper grid
    // leans 0.5 degrees: under the 1 degree threshold, so straight. Laid out by the game, 5 cells lie between the plates.
    [Fact]
    public async Task Hull_A_keeps_side_level_and_order_and_takes_the_games_distances()
    {
        using var work = new Workspace();

        var run = await work.Run();

        run.ExitCode.Should().Be(Deviation, run.Error);
        var racks = work.Racks("hull_a");
        racks.Select(Text).Should().Equal(
            "rack-0 port/upper [-9,3,1] +y 4x2x2",
            "rack-1 port/lower [-9,3,-2] +y 4x2x2",
            "rack-2 starboard/upper [-9,-4,1] -y 4x2x2",
            "rack-3 starboard/lower [-9,-4,-2] -y 4x2x2");
        racks.Should().OnlyContain(r => r["rotation"] == null);
        ShipLoader.Load(work.Ships, "hull_a").TotalScu.Should().Be(64);
    }

    [Fact]
    public async Task The_RAFT_stacks_down()
    {
        using var work = new Workspace();

        await work.Run();

        work.Racks("raft").Select(Text).Should().Equal("rack-0 - [-12,-4,-2] -z 12x8x2");
    }

    // A right angle is no rotation (D91): the grid's axes swap and the rack stays straight.
    [Fact]
    public async Task A_right_angle_turns_the_grid_not_the_rack()
    {
        using var work = new Workspace();

        await work.Run();

        var racks = work.Racks("turned_ship");
        racks.Select(Text).Should().Equal("rack-0 - [-1,-3,0] +z 2x6x2", "rack-1 - [3,-1,0] +z 2x2x1");
        racks.Should().OnlyContain(r => r["rotation"] == null);
    }

    // The racks in the file, turned as RackTransform turns them, lie as the game's grids: each grid axis along a rack axis
    // of the same length (within 0.2 degrees), and both racks off their grids by the same shift (within rounding).
    [Fact]
    public async Task The_Prowlers_racks_turn_where_the_game_says()
    {
        using var work = new Workspace();

        await work.Run();

        var racks = work.Racks("prowler_utility");
        racks.Should().HaveCount(2).And.OnlyContain(r => r["rotation"] != null && r["stackAxis"]!.GetValue<string>() == "-z");
        var grids = Catalog("Prowler Utility");
        var offsets = new List<double[]>();
        foreach (var rack in racks)
        {
            var (centre, axes) = TurnedRack(rack);
            // The port grid (game x < 0) belongs to the rack further to port (ship y > 0).
            var grid = grids.Single(g => g.Translation[0] < 0 == centre[1] > 0);
            foreach (var (direction, size) in grid.Axes)
            {
                axes.Should().Contain(a => a.Length == size && Math.Abs(Dot(a.Direction, direction)) > Math.Cos(double.DegreesToRadians(0.2)),
                    $"{rack["id"]}: a rack axis of {size} cells along the grid axis {string.Join(", ", direction)}");
            }

            offsets.Add([.. Enumerable.Range(0, 3).Select(i => centre[i] - grid.Centre[i])]);
        }

        for (var i = 0; i < 3; i++)
        {
            Math.Abs(offsets[0][i] - offsets[1][i]).Should().BeLessThanOrEqualTo(1.0, "both racks share the ship's shift");
        }

        ShipLoader.Load(work.Ships, "prowler_utility").Racks.Should().OnlyContain(r => r.Rotation != null);
    }

    // maxCrateSize = min(maxContainerScu, hand value); the hand value is the file's limit, or what the grid takes (D70).
    [Fact]
    public async Task The_door_limit_takes_the_smaller_of_catalog_and_hand_value()
    {
        using var work = new Workspace();

        await work.Run();

        var racks = work.Racks("turned_ship");
        racks[0]["maxCrateSize"]!.GetValue<int>().Should().Be(4, "the catalog's 4 is below the 24 the grid takes");
        racks[1]["maxCrateSize"]!.GetValue<int>().Should().Be(2, "the hand value 2 is below the catalog's 4");
        racks[0]["minCrateSize"]!.GetValue<int>().Should().Be(2, "a minimum the catalog doesn't name stays");
        work.Racks("mustang_alpha")[0]["maxCrateSize"].Should().BeNull("the catalog's 2 is what the grid takes anyway");
    }

    [Fact]
    public async Task Source_names_the_build_notes_keep_the_hand_text_and_anchors_stay()
    {
        using var work = new Workspace();
        var before = work.Read("turned_ship");

        await work.Run();

        var ship = work.Ship("turned_ship");
        ship["source"]!.GetValue<string>().Should().Be(Source);
        ship["notes"]!.GetValue<string>().Should().Be(
            "rack positions from game files (Motherlode); door limits from Fleetyards limits.max.capacity; door limits from game files (Motherlode)");
        var after = work.Read("turned_ship");
        foreach (var field in (string[])["\"holoOrigin\": [1.5000000000000002, 0.25, -0.5],", "\"outlineOrigin\": [1.5, 0.25, -0.75],", "\"outlineScale\": 1.1199999999999999,"])
        {
            before.Should().Contain(field);
            after.Should().Contain(field, "anchors wait for session 4b, digit for digit");
        }

        work.Ship("raft")["notes"]!.GetValue<string>().Should().StartWith("rack positions from game files (Motherlode)");
    }

    [Fact]
    public async Task A_ship_without_transforms_stays_as_it_is()
    {
        using var work = new Workspace();
        var before = work.Read("135c");

        var run = await work.Run();

        work.Read("135c").Should().Be(before);
        run.Output.Should().MatchRegex(@"kept\s+135c\s+.*transform");
    }

    // K1: grids that overlap aren't laid by the script, a person looks at them.
    [Fact]
    public async Task Overlapping_grids_leave_the_file_alone_and_ask_for_a_hand_check()
    {
        using var work = new Workspace("overlap.json");
        work.AddFixtureShip("overlap_ship");
        var before = work.Snapshot();

        var run = await work.Run();

        run.ExitCode.Should().Be(Deviation, run.Error);
        run.Output.Should().MatchRegex(@"handcheck\s+overlap_ship\s+.*rack-0.*rack-1");
        work.Snapshot().Should().Equal(before);
    }

    [Fact]
    public async Task A_new_variant_is_reported_and_written_only_with_Create()
    {
        using var work = new Workspace();

        var run = await work.Run();

        run.ExitCode.Should().Be(Deviation);
        run.Output.Should().MatchRegex(@"new\s+mustang_alpha_vindicator");
        work.ShipIds().Should().NotContain("mustang_alpha_vindicator");
    }

    // A variant takes its base ship's hologram and outline: slug and anchors, digit for digit, and its racks.
    [Fact]
    public async Task Create_writes_a_variant_with_its_base_ships_hologram()
    {
        using var work = new Workspace();

        var run = await work.Run("-Create");

        run.ExitCode.Should().Be(0, run.Error + run.Output);
        var variant = work.Ship("mustang_alpha_vindicator");
        variant["name"]!.GetValue<string>().Should().Be("CNOU Mustang Alpha Vindicator");
        variant["fleetyardsSlug"]!.GetValue<string>().Should().Be("cnou-mustang-alpha");
        variant["source"]!.GetValue<string>().Should().Be(Source);
        var baseText = work.Read("mustang_alpha");
        var variantText = work.Read("mustang_alpha_vindicator");
        foreach (var field in (string[])["holoOrigin", "outlineOrigin"])
        {
            Line(variantText, field).Should().Be(Line(baseText, field));
        }

        work.Racks("mustang_alpha_vindicator").Select(Text).Should().Equal(work.Racks("mustang_alpha").Select(Text));
        ShipLoader.Load(work.Ships, "mustang_alpha_vindicator").TotalScu.Should().Be(4);
    }

    // A new ship without a base has no hologram: the anchors go to the racks' middle, as docs/ship-format.md says by hand.
    [Fact]
    public async Task Create_writes_a_ship_without_base_with_anchors_at_the_racks_middle()
    {
        using var work = new Workspace();

        await work.Run("-Create");

        var ship = work.Ship("test_buggy");
        ship["fleetyardsSlug"].Should().BeNull();
        ship["name"]!.GetValue<string>().Should().Be("TEST Test Buggy");
        work.Racks("test_buggy").Select(Text).Should().Equal("rack-0 - [0,0,0] +z 1x1x1");
        foreach (var field in (string[])["holoOrigin", "outlineOrigin"])
        {
            ship[field]!.AsArray().Select(v => v!.GetValue<double>()).Should().Equal(0.5, 0.5, 0.5);
        }
    }

    // The TS module lies relative to the Aurora's racks: it moves by what Rack 1 moved.
    [Fact]
    public async Task The_TS_module_moves_with_the_Auroras_first_rack()
    {
        using var work = new Workspace();
        var before = Origin(work.Racks("aurora_mk_ii")[0]);
        var moduleBefore = Origin(work.Module("ts_module")["racks"]![0]!);

        await work.Run();

        work.Racks("aurora_mk_ii").Select(Text).Should().Equal("rack-0 - [-1,0,0] +z 1x1x1", "rack-1 - [-1,-1,0] +z 1x1x1");
        var shift = Origin(work.Racks("aurora_mk_ii")[0]).Zip(before, (a, b) => a - b).ToArray();
        Origin(work.Module("ts_module")["racks"]![0]!).Should().Equal(moduleBefore.Zip(shift, (a, b) => a + b));
        ShipLoader.Load(work.Ships, "aurora_mk_ii").Should().NotBeNull();
    }

    [Fact]
    public async Task A_second_run_changes_nothing()
    {
        using var work = new Workspace();
        (await work.Run("-Create")).ExitCode.Should().Be(0);
        var after = work.Snapshot();

        var run = await work.Run("-Create");

        run.ExitCode.Should().Be(0, run.Output);
        work.Snapshot().Should().Equal(after);
        run.Output.Should().NotMatchRegex(@"(?m)^(updated|created)\s");
    }

    [Fact]
    public async Task A_broken_catalog_writes_nothing()
    {
        using var work = new Workspace("bad_transform.json");
        var before = work.Snapshot();

        var run = await work.Run();

        run.ExitCode.Should().Be(Failure);
        run.Error.Should().Contain("transform");
        work.Snapshot().Should().Equal(before);
    }

    // Without a grid id, a catalog that lists the same grids in another order can't be told from swapped racks.
    [Fact]
    public async Task Grids_in_another_order_than_the_previous_catalog_leave_the_file_alone()
    {
        using var work = new Workspace();
        var before = work.Read("hull_a");

        var run = await work.Run("-Previous", Path.Combine(Fixtures, "previous_reordered.json"));

        run.ExitCode.Should().Be(Deviation);
        run.Output.Should().MatchRegex(@"order\s+hull_a");
        work.Read("hull_a").Should().Be(before);
        work.Ship("raft")["source"]!.GetValue<string>().Should().Be(Source, "other ships are written as usual");
    }

    [Fact]
    public async Task The_same_order_as_the_previous_catalog_is_no_warning()
    {
        using var work = new Workspace();

        var run = await work.Run("-Previous", Path.Combine(Fixtures, "catalog.json"));

        run.Output.Should().NotMatchRegex(@"(?m)^order\s");
        work.Ship("hull_a")["source"]!.GetValue<string>().Should().Be(Source);
    }

    // The rounding protocol (D92): the ship's fraction s and every rack's rounding in cells and metres.
    [Fact]
    public async Task The_report_names_the_shift_and_every_racks_rounding()
    {
        using var work = new Workspace();

        var run = await work.Run();

        run.Output.Should().MatchRegex(@"(?m)^shift\s+hull_a\s+s=\[");
        foreach (var id in (string[])["rack-0", "rack-1", "rack-2", "rack-3"])
        {
            run.Output.Should().MatchRegex($@"(?m)^round\s+hull_a\s+{id}\s+e=\[.*cells.*m");
        }

        run.Output.Should().MatchRegex(@"(?m)^axis\s+raft\s+rack-0\s+-z\s+->\s+-z").And.MatchRegex(@"(?m)^rotation\s+prowler_utility\s+rack-0\s+\[");
    }

    private static string Text(JsonNode rack)
    {
        var position = rack["position"] is { } p ? $"{p["side"]}/{p["level"]}" : "-";
        var grid = rack["grid"]!;
        return $"{rack["id"]} {position} [{string.Join(",", Origin(rack))}] {rack["stackAxis"]} {grid["length"]}x{grid["width"]}x{grid["stack"]}";
    }

    private static int[] Origin(JsonNode rack) => [.. rack["origin"]!.AsArray().Select(v => v!.GetValue<int>())];

    private static string? Line(string text, string field) =>
        text.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith($"\"{field}\"", StringComparison.Ordinal))?.Trim();

    private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

    private sealed record Grid(double[] Translation, double[] Centre, IReadOnlyList<(double[] Direction, int Size)> Axes);

    /// <summary>A catalog ship's grids in ship cells: x bow, y port, z up = (game y, -game x, game z) / 1.25.</summary>
    private static IReadOnlyList<Grid> Catalog(string name)
    {
        var catalog = JsonNode.Parse(File.ReadAllText(Path.Combine(Fixtures, "catalog.json")))!;
        var ship = catalog["ships"]!.AsArray().Single(s => s!["name"]!.GetValue<string>() == name)!;
        return [.. ship["grids"]!.AsArray().Select(g =>
        {
            var t = g!["transform"]!.AsArray().Select(v => v!.GetValue<double>()).ToArray();
            var size = g["size"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray();
            double[] Column(int c) => [t[4 + c], -t[c], t[8 + c]];
            double[] translation = [t[7], -t[3], t[11]];
            var up = Column(2);
            double[] centre = [.. Enumerable.Range(0, 3).Select(i => (translation[i] + up[i] * size[2] * Cell / 2) / Cell)];
            return new Grid([t[3], t[7], t[11]], centre, [.. Enumerable.Range(0, 3).Select(c => (Column(c), size[c]))]);
        })];
    }

    /// <summary>
    /// A rack's centre and its three axes turned by R = Rz(yaw) · Ry(pitch) · Rx(roll), the formula of RackTransform in the
    /// App (D91; RackTransformTests pin its conventions). Frames as docs/ship-format.md: ±x stacks along x with length
    /// along z, ±y along y with width along z, ±z along z.
    /// </summary>
    private static (double[] Centre, IReadOnlyList<(double[] Direction, int Length)> Axes) TurnedRack(JsonNode rack)
    {
        var grid = rack["grid"]!;
        int[] dims = [grid["length"]!.GetValue<int>(), grid["width"]!.GetValue<int>(), grid["stack"]!.GetValue<int>()];
        int[] frame = rack["stackAxis"]!.GetValue<string>()[1] switch
        {
            'x' => [2, 1, 0],
            'y' => [0, 2, 1],
            _ => [0, 1, 2],
        };
        var extent = new int[3];
        for (var i = 0; i < 3; i++)
        {
            extent[frame[i]] = dims[i];
        }

        var origin = Origin(rack);
        double[] centre = [.. Enumerable.Range(0, 3).Select(i => origin[i] + extent[i] / 2.0)];
        var angles = rack["rotation"]!.AsArray().Select(v => double.DegreesToRadians(v!.GetValue<double>())).ToArray();
        var (sy, cy) = Math.SinCos(angles[0]);
        var (sp, cp) = Math.SinCos(angles[1]);
        var (sr, cr) = Math.SinCos(angles[2]);
        double[,] r =
        {
            { cy * cp, cy * sp * sr - sy * cr, cy * sp * cr + sy * sr },
            { sy * cp, sy * sp * sr + cy * cr, sy * sp * cr - cy * sr },
            { -sp, cp * sr, cp * cr },
        };
        return (centre, [.. Enumerable.Range(0, 3).Select(i => (new[] { r[0, i], r[1, i], r[2, i] }, extent[i]))]);
    }

    private sealed record ScriptRun(int ExitCode, string Output, string Error);

    /// <summary>A temporary folder with a copy of the catalog and a ships folder with copies of the files it updates.</summary>
    private sealed class Workspace : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("cargoplan-motherlode-");

        public Workspace(string catalog = "catalog.json")
        {
            Catalog = Path.Combine(_root.FullName, catalog);
            File.Copy(Path.Combine(Fixtures, catalog), Catalog);
            Ships = Directory.CreateDirectory(Path.Combine(_root.FullName, "ships")).FullName;
            Directory.CreateDirectory(Path.Combine(Ships, "modules"));
            if (catalog == "catalog.json")
            {
                // The files as they were before the game-file racks (FrozenShips/), never ships/: those are this import's output.
                foreach (var id in Frozen)
                {
                    File.Copy(FrozenShips.PathOf(id), Path.Combine(Ships, id + ".json"));
                }

                File.Copy(Path.Combine(FrozenShips.Folder, "modules", "ts_module.json"), Path.Combine(Ships, "modules", "ts_module.json"));
                AddFixtureShip("turned_ship");
            }
        }

        public string Catalog { get; }

        public string Ships { get; }

        public void AddFixtureShip(string id) =>
            File.Copy(Path.Combine(Fixtures, "ships", id + ".json"), Path.Combine(Ships, id + ".json"));

        public IReadOnlyList<string> ShipIds() =>
            [.. Directory.EnumerateFiles(Ships, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.Ordinal)];

        public string Read(string id) => File.ReadAllText(Path.Combine(Ships, id + ".json")).ReplaceLineEndings("\n");

        public JsonNode Ship(string id) => JsonNode.Parse(Read(id))!;

        public JsonNode Module(string id) => JsonNode.Parse(File.ReadAllText(Path.Combine(Ships, "modules", id + ".json")))!;

        public IReadOnlyList<JsonNode> Racks(string id) => [.. Ship(id)["racks"]!.AsArray().Select(r => r!)];

        /// <summary>Every file's bytes and time, modules included, to prove a run wrote nothing.</summary>
        public IReadOnlyList<string> Snapshot() =>
        [
            .. Directory.EnumerateFiles(Ships, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                .Select(f => $"{f} {File.GetLastWriteTimeUtc(f):O} {Convert.ToHexString(File.ReadAllBytes(f))}"),
        ];

        public async Task<ScriptRun> Run(params string[] arguments)
        {
            ProcessStartInfo start = new(Shell())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in (string[])["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Script,
                         "-Catalog", Catalog, "-ShipsFolder", Ships, .. arguments])
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
