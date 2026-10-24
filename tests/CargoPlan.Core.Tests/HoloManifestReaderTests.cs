using System.Text;
using CargoPlan.Core.Holo;

namespace CargoPlan.Core.Tests;

public class HoloManifestReaderTests
{
    private const string Hash = "7304a2e0ab28d0570bd130985ab38e701637f1b1d15d97500d094971fb084345";

    // Shaped like the published holo-v1 manifest, with the fields the app doesn't read.
    private static byte[] Manifest(string dataVersion = "holo-v1", int formatVersion = 2, string ships = $$"""
        { "slug": "misc-hull-a", "file": "misc-hull-a.holo", "bytes": 396120, "sha256": "{{Hash}}", "rawSha256": "x",
          "budget": 20000, "triangles": 19878, "edges": [2472, 6140, 15000], "passed": true, "metrics": { "errorM": 0.05 } }
        """) => Encoding.UTF8.GetBytes($$"""
        { "dataVersion": "{{dataVersion}}", "formatVersion": {{formatVersion}}, "generatedAt": "2026-10-24",
          "pipelineCommit": "2959336", "tools": { "node": "24.16.0" }, "ships": [ {{ships}} ] }
        """);

    [Fact]
    public void Reads_the_entries_and_skips_the_fields_it_does_not_need()
    {
        var manifest = HoloManifestReader.Read(Manifest(), "holo-v1");

        manifest.Find("misc-hull-a").Should().Be(new HoloManifestEntry("misc-hull-a", "misc-hull-a.holo", 396120, Hash));
        manifest.Find("drak-caterpillar").Should().BeNull();
    }

    [Fact]
    public void An_empty_ship_list_is_valid_it_withdraws_every_hologram() =>
        HoloManifestReader.Read(Manifest(ships: ""), "holo-v1").Ships.Should().BeEmpty();

    [Fact]
    public void Rejects_another_data_version() =>
        ((Action)(() => HoloManifestReader.Read(Manifest(dataVersion: "holo-v2"), "holo-v1")))
            .Should().Throw<HoloFormatException>().WithMessage("*holo-v2*");

    [Fact]
    public void Rejects_a_format_version_it_does_not_know() =>
        ((Action)(() => HoloManifestReader.Read(Manifest(formatVersion: 3), "holo-v1")))
            .Should().Throw<HoloFormatException>().WithMessage("*3*");

    [Theory]
    [InlineData("{ \"dataVersion\": ")]
    [InlineData("null")]
    [InlineData("{ \"dataVersion\": \"holo-v1\", \"formatVersion\": 2 }")]
    public void Rejects_broken_json(string json) =>
        ((Action)(() => HoloManifestReader.Read(Encoding.UTF8.GetBytes(json), "holo-v1"))).Should().Throw<HoloFormatException>();

    [Fact]
    public void Rejects_an_entry_without_a_hash() =>
        ((Action)(() => HoloManifestReader.Read(Manifest(ships: """{ "slug": "a", "file": "a.holo", "bytes": 1, "sha256": "" }"""), "holo-v1")))
            .Should().Throw<HoloFormatException>().WithMessage("*sha256*");
}
