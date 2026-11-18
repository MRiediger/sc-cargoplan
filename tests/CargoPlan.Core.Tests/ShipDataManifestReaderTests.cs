using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.ShipData;

namespace CargoPlan.Core.Tests;

public class ShipDataManifestReaderTests
{
    private const string Hash = "f05c9c4f0000000000000000000000000000000000000000000000000000beef";

    private static readonly ShipDataRelease Release = ShipDataRelease.Frozen();

    private static ShipDataManifest Read(byte[] json) => ShipDataManifestReader.Read(json);

    private static Action Reading(Action<JsonObject> change) => () => Read(Release.Manifest(change: change));

    private static Action ReadingFiles(params string[] paths) => () =>
        Read(Release.Manifest(files: Release.Entries.Keys.Concat(paths).Distinct().ToDictionary(p => p, _ => Hash)));

    [Fact]
    public void Reads_what_the_app_needs_and_skips_the_rest()
    {
        var manifest = Read(Release.Manifest());

        manifest.DataVersion.ToString().Should().Be("ships-2026.11.1");
        manifest.SchemaVersion.Should().Be(6);
        manifest.ShipsAsset.Should().Be("ships.zip");
        manifest.Files.Should().ContainKey("ships/hull_a.json").And.ContainKey("ships/modules/ts_module.json").And.ContainKey("outlines.json");
        manifest.Files["outlines.json"].Should().Be(ShipDataRelease.Sha256(Release.Entries["outlines.json"]));
    }

    [Fact]
    public void An_unknown_schema_version_still_reads_the_app_decides_what_it_means() =>
        Read(new ShipDataRelease { SchemaVersion = 7 }.Manifest(files: new Dictionary<string, string>
        {
            ["outlines.json"] = Hash,
            ["ships/hull_a.json"] = Hash,
        })).SchemaVersion.Should().Be(7);

    [Theory]
    [InlineData("{ \"dataVersion\": ")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Rejects_what_is_not_a_manifest(string json) =>
        ((Action)(() => Read(Encoding.UTF8.GetBytes(json)))).Should().Throw<ShipDataFormatException>().WithMessage("manifest.json*");

    [Theory]
    [InlineData("dataVersion")]
    [InlineData("schemaVersion")]
    [InlineData("assets")]
    [InlineData("files")]
    public void Rejects_a_manifest_without_a_field_it_needs(string field) =>
        Reading(m => m.Remove(field)).Should().Throw<ShipDataFormatException>().WithMessage($"*{field}*");

    [Theory]
    [InlineData("holo-v3")]
    [InlineData("ships-2026.11.0")]
    [InlineData("")]
    public void Rejects_a_data_version_that_is_no_release_tag(string dataVersion) =>
        Reading(m => m["dataVersion"] = dataVersion).Should().Throw<ShipDataFormatException>().WithMessage($"*'{dataVersion}'*");

    [Theory]
    [InlineData("")]
    [InlineData("data/ships.zip")]
    [InlineData("..\\ships.zip")]
    [InlineData("..")]
    [InlineData(".ships.zip")]
    [InlineData("C:ships.zip")]
    public void Rejects_an_asset_that_is_not_a_flat_name(string asset) =>
        Reading(m => m["assets"] = new JsonObject { ["ships"] = asset }).Should().Throw<ShipDataFormatException>().WithMessage("*assets.ships*");

    // E1 of the approval: exactly outlines.json, ships/<name>.json and ships/modules/<name>.json; anything else could
    // write outside the data folder or into a place the loader never reads.
    [Theory]
    [InlineData("../evil.json")]
    [InlineData("ships/../../evil.json")]
    [InlineData("/ships/hull_b.json")]
    [InlineData("C:/ships/hull_b.json")]
    [InlineData("ships\\hull_b.json")]
    [InlineData("ships/sub/z.json")]
    [InlineData("ships/modules/x/y.json")]
    [InlineData("ships/hull_b.txt")]
    [InlineData("ships/.json")]
    [InlineData("ships/hull b.json")]
    [InlineData("outlines/ship-outlines.json")]
    [InlineData("NOTICE.md")]
    [InlineData("Ships/hull_b.json")]
    public void Rejects_a_file_path_outside_the_layout_of_ships_zip(string path) =>
        ReadingFiles(path).Should().Throw<ShipDataFormatException>().WithMessage($"*'{path}'*");

    [Theory]
    [InlineData("ships/modules/ts_module.json")]
    [InlineData("ships/600i_executive_edition.json")]
    public void Takes_the_paths_of_ships_zip(string path) =>
        ReadingFiles(path).Should().NotThrow();

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("f05c9c4f0000000000000000000000000000000000000000000000000000beeg")]
    [InlineData("f05c9c4f0000000000000000000000000000000000000000000000000000beef0")]
    public void Rejects_a_hash_that_is_not_sha256_hex(string hash) =>
        Reading(m => m["files"]!["ships/hull_a.json"] = hash).Should().Throw<ShipDataFormatException>().WithMessage("*ships/hull_a.json*");

    [Theory]
    [InlineData("ships/hull_a.json")]
    [InlineData("outlines.json")]
    public void Rejects_a_release_without_the_fallback_ship_or_the_outlines(string path) =>
        Reading(m => m["files"]!.AsObject().Remove(path)).Should().Throw<ShipDataFormatException>().WithMessage($"*{path}*");
}
