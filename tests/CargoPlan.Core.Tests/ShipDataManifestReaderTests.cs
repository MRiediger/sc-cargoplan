using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Holo;
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

    // --- Holograms (T12.4, D114): the holo section, read where the rest of the manifest is (was HoloManifestReaderTests) ---

    private const string HoloHash = "7304a2e0ab28d0570bd130985ab38e701637f1b1d15d97500d094971fb084345";

    // Shaped like the published section, with the judge's fields the app doesn't read.
    private static JsonObject HoloSection(JsonObject? ships = null) => new()
    {
        ["formatVersion"] = 2,
        ["ships"] = ships ?? new JsonObject { ["misc-hull-a"] = HoloEntry() },
    };

    private static JsonObject HoloEntry(string file = "misc-hull-a.holo", long bytes = 185882, string sha256 = HoloHash) => new()
    {
        ["file"] = file,
        ["bytes"] = bytes,
        ["sha256"] = sha256,
        ["model"] = "MISC_Hull_A",
        ["rawSha256"] = "da8168ea3720",
        ["budget"] = 20000,
        ["triangles"] = 13085,
        ["edges"] = new JsonArray(1445, 3411, 9620),
        ["passed"] = true,
        ["fails"] = new JsonArray(),
        ["metrics"] = new JsonObject { ["errorM"] = 0.0, ["topIou"] = 1.0 },
    };

    private static HoloOffer ReadHolo(JsonNode? holo) => Read(Release.Manifest(change: m => m["holo"] = holo)).Holo;

    private static Action ReadingHolo(JsonNode? holo) => Reading(m => m["holo"] = holo);

    [Fact]
    public void Reads_the_holograms_under_the_release_tag_sorted_by_slug_and_skips_the_judges_fields()
    {
        var offer = ReadHolo(HoloSection(new JsonObject
        {
            ["misc-hull-a"] = HoloEntry(),
            ["drak-cutlass-black"] = HoloEntry("drak-cutlass-black.holo", 1, new string('0', 64)),
        }));

        var manifest = offer.Should().BeOfType<HoloOffer.Offered>().Which.Manifest;
        manifest.DataVersion.Should().Be("ships-2026.11.1");
        manifest.Ships.Select(s => s.Slug).Should().Equal("drak-cutlass-black", "misc-hull-a");
        manifest.Find("misc-hull-a").Should().Be(new HoloManifestEntry("misc-hull-a", "misc-hull-a.holo", 185882, HoloHash));
        manifest.Find("gama-hyun").Should().BeNull();
    }

    [Fact]
    public void Reads_the_hauling_section_and_a_release_without_it_offers_none()
    {
        var release = ShipDataRelease.Frozen().WithHauling();
        var (templates, positions) = release.Hauling!.Value;

        Read(release.Manifest()).Hauling.Should().Be(new HaulingOffer.Offered(new HaulingManifest(1,
            new ReleaseAsset("hauling-templates.json", templates.Length, ShipDataRelease.Sha256(templates)),
            new ReleaseAsset("positions.json", positions.Length, ShipDataRelease.Sha256(positions)))));
        Read(Release.Manifest()).Hauling.Should().Be(new HaulingOffer.None());
    }

    [Fact]
    public void A_release_asset_matches_by_length_and_hash()
    {
        byte[] data = [1, 2, 3];
        var asset = new ReleaseAsset("x.json", 3, ShipDataRelease.Sha256(data).ToUpperInvariant());

        asset.Matches(data).Should().BeTrue();
        asset.Matches([1, 2, 4]).Should().BeFalse();
        (asset with { Bytes = 4 }).Matches(data).Should().BeFalse();
    }

    [Fact]
    public void Two_readings_of_the_same_section_are_equal() =>
        ReadHolo(HoloSection()).Should().Be(ReadHolo(HoloSection()));

    [Fact]
    public void An_empty_map_is_valid_and_offers_no_hologram() =>
        ReadHolo(HoloSection(new JsonObject())).Should().BeOfType<HoloOffer.Offered>().Which.Manifest.Ships.Should().BeEmpty();

    [Fact]
    public void Holo_null_is_the_kill_switch() => ReadHolo(null).Should().Be(new HoloOffer.NotOffered());

    [Fact]
    public void Rejects_a_manifest_without_the_holo_field() =>
        Reading(m => m.Remove("holo")).Should().Throw<ShipDataFormatException>().WithMessage("*holo*");

    // R1 of the T12.4 approval: another format means no holograms for this app, not a broken release; the ships still
    // load, and the entries of a format this app doesn't know aren't read at all.
    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    public void Another_format_version_offers_this_app_no_holograms_and_its_entries_are_not_read(int formatVersion) =>
        ReadHolo(new JsonObject { ["formatVersion"] = formatVersion, ["ships"] = new JsonArray("not", "read") })
            .Should().Be(new HoloOffer.UnknownFormat(formatVersion));

    [Theory]
    [InlineData("""{ "ships": {} }""")]
    [InlineData("""{ "formatVersion": "2", "ships": {} }""")]
    [InlineData("""{ "formatVersion": 2 }""")]
    [InlineData("""{ "formatVersion": 2, "ships": [] }""")]
    [InlineData("""{ "formatVersion": 2, "ships": { "misc-hull-a": { "file": "misc-hull-a.holo", "bytes": 1 } } }""")]
    [InlineData("""[]""")]
    [InlineData("\"holo-v3\"")]
    public void Rejects_a_holo_section_out_of_form(string holo) =>
        ReadingHolo(JsonNode.Parse(holo)).Should().Throw<ShipDataFormatException>().WithMessage("manifest.json*holo*");

    [Theory]
    [InlineData("")]
    [InlineData("holo/misc-hull-a.holo")]
    [InlineData("..\\misc-hull-a.holo")]
    [InlineData("..")]
    [InlineData(".holo")]
    [InlineData("C:misc-hull-a.holo")]
    public void Rejects_a_hologram_file_that_is_not_a_flat_asset_name(string file) =>
        ReadingHolo(HoloSection(new JsonObject { ["misc-hull-a"] = HoloEntry(file: file) }))
            .Should().Throw<ShipDataFormatException>().WithMessage("*holo*misc-hull-a*");

    [Theory]
    [InlineData("")]
    [InlineData("../misc-hull-a")]
    [InlineData("misc/hull-a")]
    public void Rejects_a_slug_that_is_not_a_flat_name(string slug) =>
        ReadingHolo(HoloSection(new JsonObject { [slug] = HoloEntry() })).Should().Throw<ShipDataFormatException>().WithMessage("*holo*");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_hologram_without_bytes(long bytes) =>
        ReadingHolo(HoloSection(new JsonObject { ["misc-hull-a"] = HoloEntry(bytes: bytes) }))
            .Should().Throw<ShipDataFormatException>().WithMessage("*holo*misc-hull-a*bytes*");

    [Theory]
    [InlineData("")]
    [InlineData("7304a2e0ab28d0570bd130985ab38e701637f1b1d15d97500d094971fb08434")]
    [InlineData("7304a2e0ab28d0570bd130985ab38e701637f1b1d15d97500d094971fb08434g")]
    public void Rejects_a_hologram_hash_that_is_not_sha256_hex(string hash) =>
        ReadingHolo(HoloSection(new JsonObject { ["misc-hull-a"] = HoloEntry(sha256: hash) }))
            .Should().Throw<ShipDataFormatException>().WithMessage("*holo*misc-hull-a*SHA-256*");
}
