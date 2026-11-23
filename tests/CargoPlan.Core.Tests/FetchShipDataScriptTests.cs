using System.Diagnostics;
using System.Text;

namespace CargoPlan.Core.Tests;

/// <summary>
/// tools/fetch-ship-data.ps1 (T12.5, D113) run as a process against a data release in a local folder, laid out like the
/// release's download URLs (<c>&lt;source&gt;\&lt;tag&gt;\manifest.json</c>, <c>ships.zip</c>, <c>NOTICE.md</c>) and built
/// from the frozen ships (A10). Whole and checked, or nothing.
/// </summary>
public sealed class FetchShipDataScriptTests
{
    private const string Tag = "ships-2026.11.1";

    private static readonly string Script = Path.Combine(AppContext.BaseDirectory, "tools", "fetch-ship-data.ps1");

    [Fact]
    public async Task A_good_release_lands_in_the_download_layout_with_current_naming_it()
    {
        using var work = new Workspace();
        var release = ShipDataRelease.Frozen(Tag);
        work.Publish(release);

        var run = await work.Run();

        run.ExitCode.Should().Be(0, run.Error);
        var folder = Path.Combine(work.Target, Tag);
        File.ReadAllBytes(Path.Combine(folder, "manifest.json")).Should().Equal(release.Manifest());
        foreach (var (path, data) in release.Entries)
        {
            File.ReadAllBytes(Path.Combine(folder, path)).Should().Equal(data, path);
        }

        File.ReadAllText(Path.Combine(folder, "NOTICE.md")).Should().Be(Workspace.Notice);
        File.ReadAllText(Path.Combine(work.Target, "current")).Trim().Should().Be(folder);
        Directory.GetDirectories(work.Target).Should().Equal(folder);
        run.Output.Should().Contain(Tag).And.Contain("bytes").And.Contain($"{release.Entries.Count} files checked");
    }

    // T13.2: the hauling files come with the baseline where the manifest names them; checked offline on the next run.
    [Fact]
    public async Task The_hauling_files_land_next_to_the_manifest_and_are_checked_offline()
    {
        using var work = new Workspace();
        var release = ShipDataRelease.Frozen(Tag).WithHauling();
        work.Publish(release);

        var run = await work.Run();

        run.ExitCode.Should().Be(0, run.Error);
        var folder = Path.Combine(work.Target, Tag);
        foreach (var (file, data) in release.HaulingAssets)
        {
            File.ReadAllBytes(Path.Combine(folder, file)).Should().Equal(data, file);
        }

        run.Output.Should().Contain($"{release.Entries.Count + 2} files checked");

        File.AppendAllText(Path.Combine(folder, ShipDataRelease.PositionsAsset), " ");
        Directory.Delete(work.Source, recursive: true);
        var again = await work.Run();
        again.ExitCode.Should().Be(1);
        again.Output.Should().Contain("positions.json").And.Contain("fetching it again");
    }

    [Fact]
    public async Task A_hauling_file_that_fails_its_hash_leaves_nothing()
    {
        using var work = new Workspace();
        var release = ShipDataRelease.Frozen(Tag).WithHauling();
        work.Publish(release);
        File.AppendAllText(Path.Combine(work.Source, Tag, ShipDataRelease.TemplatesAsset), " ");

        var run = await work.Run();

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("hauling-templates.json");
        work.ShouldHoldNothing();
    }

    [Fact]
    public async Task A_second_run_needs_no_source()
    {
        using var work = new Workspace();
        work.Publish(ShipDataRelease.Frozen(Tag));
        (await work.Run()).ExitCode.Should().Be(0);
        Directory.Delete(work.Source, recursive: true);

        var run = await work.Run();

        run.ExitCode.Should().Be(0, run.Error);
        run.Output.Should().Contain("already there");
        File.Exists(Path.Combine(work.Target, "current")).Should().BeTrue();
    }

    [Fact]
    public async Task A_damaged_file_in_the_finished_folder_is_fetched_again()
    {
        using var work = new Workspace();
        var release = ShipDataRelease.Frozen(Tag);
        work.Publish(release);
        (await work.Run()).ExitCode.Should().Be(0);
        var hullA = Path.Combine(work.Target, Tag, "ships", "hull_a.json");
        File.AppendAllText(hullA, " ");

        var run = await work.Run();

        run.ExitCode.Should().Be(0, run.Error);
        run.Output.Should().Contain("fetching it again");
        File.ReadAllBytes(hullA).Should().Equal(release.Entries["ships/hull_a.json"]);
    }

    [Fact]
    public async Task A_changed_file_leaves_nothing()
    {
        using var work = new Workspace();
        var release = ShipDataRelease.Frozen(Tag);
        var manifest = release.Manifest();
        release.Entries["ships/hull_a.json"] = [.. release.Entries["ships/hull_a.json"], (byte)' '];
        work.Publish(release, manifest);

        var run = await work.Run();

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("ships/hull_a.json");
        work.ShouldHoldNothing();
    }

    [Fact]
    public async Task A_file_the_manifest_lists_but_the_zip_lacks_leaves_nothing()
    {
        using var work = new Workspace();
        var release = ShipDataRelease.Frozen(Tag);
        var manifest = release.Manifest();
        release.Entries.Remove("ships/modules/ts_module.json");
        work.Publish(release, manifest);

        var run = await work.Run();

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("ships/modules/ts_module.json");
        work.ShouldHoldNothing();
    }

    // The zip-slip rule of the store (T12.3, E1): entries outside the manifest's files never reach the disk.
    [Theory]
    [InlineData("../evil.json")]
    [InlineData("ships/sub/z.json")]
    [InlineData("ships/hull_b.json")]
    public async Task An_entry_the_manifest_does_not_list_leaves_nothing(string path)
    {
        using var work = new Workspace();
        var release = ShipDataRelease.Frozen(Tag);
        work.Publish(release, zip: release.Zip((path, Encoding.UTF8.GetBytes("{}"))));

        var run = await work.Run();

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain(path);
        work.ShouldHoldNothing();
        File.Exists(Path.Combine(work.Root, "evil.json")).Should().BeFalse();
    }

    [Fact]
    public async Task A_manifest_of_another_release_leaves_nothing()
    {
        using var work = new Workspace();
        work.Publish(ShipDataRelease.Frozen("ships-2026.12"), folder: Tag);

        var run = await work.Run();

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("dataVersion").And.Contain("ships-2026.12");
        work.ShouldHoldNothing();
    }

    [Fact]
    public async Task A_schema_the_app_cannot_read_leaves_nothing()
    {
        using var work = new Workspace();
        work.Publish(ShipDataRelease.Frozen(Tag, schemaVersion: 7));

        var run = await work.Run();

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("schemaVersion 7");
        work.ShouldHoldNothing();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("big")]
    public async Task A_notice_missing_empty_or_too_big_leaves_nothing(string? notice)
    {
        using var work = new Workspace();
        work.Publish(ShipDataRelease.Frozen(Tag), notice: notice == "big" ? new string('x', 65 * 1024) : notice);

        var run = await work.Run();

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("NOTICE.md");
        work.ShouldHoldNothing();
    }

    [Theory]
    [InlineData("""{ "dataVersion": "latest" }""", "latest")]
    [InlineData("""{ "dataVersion": "ships-2026.13" }""", "ships-2026.13")]
    [InlineData("""{ }""", "dataVersion")]
    public async Task A_pin_that_is_no_release_tag_fetches_nothing(string pin, string named)
    {
        using var work = new Workspace(pin);
        work.Publish(ShipDataRelease.Frozen(Tag));

        var run = await work.Run();

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain(named);
        work.ShouldHoldNothing();
    }

    [Fact]
    public async Task A_release_not_in_the_source_fetches_nothing()
    {
        using var work = new Workspace();

        var run = await work.Run();

        run.ExitCode.Should().Be(1);
        run.Error.Should().Contain("manifest.json");
        work.ShouldHoldNothing();
    }

    private sealed record ScriptRun(int ExitCode, string Output, string Error);

    /// <summary>A temporary folder with a pin file, a source folder as the release's URLs and an empty target.</summary>
    private sealed class Workspace : IDisposable
    {
        public const string Notice = "# Notice\n\nShip outlines and holograms: not under the MIT licence.\n";

        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("cargoplan-fetch-");

        public Workspace(string pin = $$"""{ "dataVersion": "{{Tag}}" }""")
        {
            Pin = Path.Combine(_root.FullName, "ship-data.json");
            File.WriteAllText(Pin, pin);
            Source = Directory.CreateDirectory(Path.Combine(_root.FullName, "source")).FullName;
            Target = Path.Combine(_root.FullName, "target");
        }

        public string Root => _root.FullName;

        public string Pin { get; }

        public string Source { get; }

        public string Target { get; }

        /// <summary>The release's three assets under <c>source\&lt;folder&gt;\</c> (default its own tag); null notice = none.</summary>
        public void Publish(ShipDataRelease release, byte[]? manifest = null, byte[]? zip = null, string? folder = null, string? notice = Notice)
        {
            var assets = Directory.CreateDirectory(Path.Combine(Source, folder ?? release.DataVersion)).FullName;
            File.WriteAllBytes(Path.Combine(assets, "manifest.json"), manifest ?? release.Manifest());
            File.WriteAllBytes(Path.Combine(assets, "ships.zip"), zip ?? release.Zip());
            if (notice is not null)
            {
                File.WriteAllText(Path.Combine(assets, "NOTICE.md"), notice);
            }

            foreach (var (file, data) in release.HaulingAssets)
            {
                File.WriteAllBytes(Path.Combine(assets, file), data);
            }
        }

        /// <summary>No release folder, no unfinished one, no <c>current</c>: nothing half.</summary>
        public void ShouldHoldNothing()
        {
            if (Directory.Exists(Target))
            {
                Directory.GetFileSystemEntries(Target).Should().BeEmpty();
            }
        }

        public async Task<ScriptRun> Run()
        {
            ProcessStartInfo start = new(Shell())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in (string[])["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Script,
                         "-Pin", Pin, "-Source", Source, "-Target", Target, "-ReadableSchemaVersions", "4,5,6"])
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

        // Windows PowerShell 5.1 on Windows, as the build calls it (R1 of the T12.5 approval); pwsh elsewhere.
        private static string Shell() => OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";
    }
}
