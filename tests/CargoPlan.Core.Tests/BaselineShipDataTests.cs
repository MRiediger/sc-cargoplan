using System.Text.Json.Nodes;
using System.Xml.Linq;
using CargoPlan.Core.ShipData;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The ship data next to the exe (T12.5, D113): a data release in the download layout, <c>ship-data\</c>, whose manifest
/// names its version. The build fetches the release that build/ship-data.json pins.
/// </summary>
public sealed class BaselineShipDataTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("cargoplan-baseline-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>The frozen release unpacked like a download into <c>ship-data\</c>, with its manifest.</summary>
    private string Unpacked(ShipDataRelease release, byte[]? manifest = null)
    {
        var folder = Path.Combine(_root, ActiveShipData.BaselineFolderName);
        foreach (var (path, data) in release.Entries)
        {
            var file = Path.Combine(folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, data);
        }

        File.WriteAllBytes(Path.Combine(folder, ShipDataManifestReader.FileName), manifest ?? release.Manifest());
        return folder;
    }

    // --- ReadBaseline ---

    [Fact]
    public void The_baseline_names_the_version_of_its_manifest_and_its_folders()
    {
        var folder = Unpacked(ShipDataRelease.Frozen("ships-2026.11.1"));

        var baseline = ActiveShipData.ReadBaseline(folder);

        baseline.Version.ToString().Should().Be("ships-2026.11.1");
        baseline.Origin.Should().Be(ShipDataOrigin.Baseline);
        baseline.Folder.Should().Be(folder);
        baseline.ShipsFolder.Should().Be(Path.Combine(folder, "ships"));
        baseline.OutlinesPath.Should().Be(Path.Combine(folder, "outlines.json"));
        baseline.ToString().Should().StartWith("ships-2026.11.1 (baseline) ");
        ShipLoader.LoadAll(baseline.ShipsFolder).Get("aurora_mk_ii_ts_module").Should().NotBeNull();
    }

    [Fact]
    public void Without_a_manifest_the_baseline_does_not_read_and_says_which_file()
    {
        var folder = Unpacked(ShipDataRelease.Frozen());
        File.Delete(Path.Combine(folder, ShipDataManifestReader.FileName));

        var act = () => ActiveShipData.ReadBaseline(folder);

        act.Should().Throw<ShipDataFormatException>().WithMessage(Path.Combine("ship-data", "manifest.json") + ": missing*");
    }

    [Fact]
    public void Without_its_folder_the_baseline_does_not_read() =>
        FluentActions.Invoking(() => ActiveShipData.ReadBaseline(Path.Combine(_root, "nowhere")))
            .Should().Throw<ShipDataFormatException>().WithMessage("*manifest.json: missing*");

    [Fact]
    public void A_baseline_of_a_schema_the_app_cannot_read_does_not_read()
    {
        var folder = Unpacked(ShipDataRelease.Frozen(schemaVersion: 7));

        var act = () => ActiveShipData.ReadBaseline(folder);

        act.Should().Throw<ShipDataFormatException>().WithMessage("*manifest.json*schemaVersion 7*");
    }

    [Fact]
    public void A_baseline_whose_manifest_has_no_release_tag_does_not_read()
    {
        var release = ShipDataRelease.Frozen();
        var folder = Unpacked(release, release.Manifest(change: m => m["dataVersion"] = "latest"));

        var act = () => ActiveShipData.ReadBaseline(folder);

        act.Should().Throw<ShipDataFormatException>().WithMessage("*manifest.json*latest*");
    }

    // --- The store against a baseline read from its manifest (R2 of the T12.5 approval) ---

    [Fact]
    public void A_download_older_than_the_baseline_is_ignored_and_goes_at_the_start()
    {
        var baseline = ActiveShipData.ReadBaseline(Unpacked(ShipDataRelease.Frozen("ships-2026.11.1")));
        var root = Path.Combine(_root, "profile", "ships");
        var older = new ShipDataStore(root, baseline).Install(
            ShipDataManifestReader.Read(ShipDataRelease.Frozen("ships-2026.11").Manifest()),
            ShipDataRelease.Frozen("ships-2026.11").Manifest(),
            new MemoryStream(ShipDataRelease.Frozen("ships-2026.11").Zip()));
        var store = new ShipDataStore(root, baseline);

        var active = store.Active();
        var lines = store.CleanUp(active);

        active.Should().Be(baseline);
        lines.Should().Equal("removed ships-2026.11");
        Directory.Exists(older.Folder).Should().BeFalse();
    }

    [Fact]
    public void A_download_equal_to_the_baseline_loses_to_it_and_goes_at_the_start()
    {
        var release = ShipDataRelease.Frozen("ships-2026.11.1");
        var baseline = ActiveShipData.ReadBaseline(Unpacked(release));
        var root = Path.Combine(_root, "profile", "ships");
        var equal = new ShipDataStore(root, baseline).Install(ShipDataManifestReader.Read(release.Manifest()), release.Manifest(), new MemoryStream(release.Zip()));
        var store = new ShipDataStore(root, baseline);

        var active = store.Active();
        var lines = store.CleanUp(active);

        active.Should().Be(baseline);
        active.Origin.Should().Be(ShipDataOrigin.Baseline);
        lines.Should().Equal("removed ships-2026.11.1");
        Directory.Exists(equal.Folder).Should().BeFalse();
    }

    // --- The pin and what the build laid next to this assembly ---

    [Fact]
    public void The_pin_is_a_release_tag_and_the_baseline_next_to_the_assembly_is_that_release()
    {
        var pin = JsonNode.Parse(File.ReadAllText(RepoFile("build", "ship-data.json")))!["dataVersion"]!.GetValue<string>();

        ShipDataVersion.TryParse(pin, out var pinned).Should().BeTrue(pin);
        ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder).Version.Should().Be(pinned, "the build copies the pinned release; a stale ship-data folder fails here");
    }

    [Fact]
    public void The_build_checks_the_schemas_this_app_reads()
    {
        var property = XDocument.Load(RepoFile("src", "CargoPlan.Core", "CargoPlan.Core.csproj"))
            .Descendants("ShipDataReadableSchemas").Single().Value;

        property.Split(',').Select(int.Parse).Should().Equal(ShipLoader.ReadableSchemaVersions);
    }

    [Fact]
    public void The_baseline_has_its_licence_note_next_to_the_outlines() =>
        new FileInfo(Path.Combine(ActiveShipData.BaselineFolder, "NOTICE.md")).Length.Should().BePositive();

    // The repository's file, found from the test assembly upwards by the solution file.
    private static string RepoFile(params string[] parts)
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "SC-CargoPlan.sln")))
            {
                return Path.Combine([folder.FullName, .. parts]);
            }
        }

        throw new InvalidOperationException($"No SC-CargoPlan.sln above {AppContext.BaseDirectory}.");
    }
}
