using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using static CargoPlan.Core.Tests.HaulingFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The hauling loader (T13.2) against small files (A10): the three cases and the loader checks of
/// docs/hauling-data-format.md, each failing once; the pinned release is <see cref="BaselineHaulingDataTests"/>.
/// </summary>
public sealed class HaulingFilesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("CargoPlan.Hauling.").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // The release laid out as installed: both files next to the manifest, read through the manifest's offer.
    private HaulingLoad Load(ShipDataRelease release, Action<JsonObject>? change = null, bool lay = true)
    {
        var manifest = ShipDataManifestReader.Read(release.Manifest(change: change));
        if (lay)
        {
            foreach (var (file, data) in release.HaulingAssets)
            {
                File.WriteAllBytes(Path.Combine(_folder, file), data);
            }
        }

        return HaulingFiles.Load(_folder, manifest.Hauling, release.DataVersion);
    }

    private HaulingLoad Load(byte[] templates, byte[] positions) => Load(ShipDataRelease.Frozen().WithHauling((templates, positions)));

    private HaulingLoad LoadTemplates(params JsonObject[] templates) => Load(TemplatesFile(templates), PositionsFile(World()));

    private static HaulingData Loaded(HaulingLoad load) =>
        load.Should().BeOfType<HaulingLoad.Loaded>().Which.Data;

    [Fact]
    public void Reads_every_field_of_both_files()
    {
        var load = Load(ShipDataRelease.Frozen().WithHauling());

        load.Should().BeOfType<HaulingLoad.Loaded>().Which.Skipped.Should().BeEmpty();
        var data = Loaded(load);
        data.Templates.Should().HaveCount(2);
        data.Templates[0].Should().BeEquivalentTo(new HaulTemplate(
            "a", "HaulCargo_AToB_a", HaulContractor.Covalex, "Experienced Rank - Direct Medium Cargo Haul", HaulRank.Experienced,
            HaulSize.Medium, true, HaulShape.AToB, 1, StarSystem.Stanton, "Stanton1", HaulMissionType.Stellar, null, 160250,
            [new HaulLeg("Pressurized Ice", 90, 144, CrateSize.Sixteen)], [Everus], [HurL1, HurL3]));
        data.Templates[0].MaxScu.Should().Be(144);
        data.Templates[1].Should().Match<HaulTemplate>(t =>
            t.Contractor == HaulContractor.RedWind && t.Faction == HaulFaction.Cfp && t.Reward == null && t.System == StarSystem.Pyro);

        data.Positions.All.Should().HaveCount(7);
        var checkmate = data.Positions.Find(StarSystem.Pyro, Checkmate)!;
        checkmate.ContractNames.Should().Equal("Checkmate at the L4 Lagrange of Pyro II");
        checkmate.X.Should().Be(-41_255_000_000);
        data.Positions.Find(StarSystem.Stanton, Everus)!.ContractNames.Should().BeEmpty();
    }

    [Fact]
    public void Distances_are_in_gigametres_within_a_system_and_undefined_across()
    {
        var positions = Loaded(Load(ShipDataRelease.Frozen().WithHauling())).Positions;
        var everus = positions.Find(StarSystem.Stanton, Everus)!;

        Positions.DistanceGm(everus, positions.Find(StarSystem.Stanton, HurL1)!).Should().BeApproximately(1.3, 1e-9);
        Positions.DistanceGm(everus, positions.Find(StarSystem.Stanton, HurL3)!).Should().BeApproximately(25.7, 1e-9);
        Positions.DistanceGm(everus, positions.Find(StarSystem.Pyro, Ruin)!).Should().BeNull();
    }

    [Fact]
    public void A_release_without_the_section_or_with_null_has_no_hauling_data()
    {
        Load(ShipDataRelease.Frozen()).Should().BeOfType<HaulingLoad.None>().Which.Reason.Should().Contain("ships-2026.11.1");
        Load(ShipDataRelease.Frozen().WithHauling(), m => m["hauling"] = null).Should().BeOfType<HaulingLoad.None>();
    }

    [Fact]
    public void Files_the_install_did_not_get_are_no_hauling_data_not_a_broken_release() =>
        Load(ShipDataRelease.Frozen().WithHauling(), lay: false).Should().BeOfType<HaulingLoad.None>()
            .Which.Reason.Should().Contain("positions.json").And.Contain("not installed");

    public static TheoryData<string, string> BrokenSections => new()
    {
        { """{"schemaVersion": 2}""", "needs a newer app" },
        { """{"templates": {}}""", "schemaVersion" },
        { "\"yes\"", "neither null nor an object" },
        { """{"schemaVersion": 1, "templates": {"file": "t.json", "bytes": 1, "sha256": "x"}}""", "hauling" },
        { """{"schemaVersion": 1, "templates": {"file": "../t.json", "bytes": 1, "sha256": "aa"}, "positions": {"file": "p.json", "bytes": 1, "sha256": "aa"}}""", "file" },
    };

    [Theory]
    [MemberData(nameof(BrokenSections))]
    public void A_section_out_of_form_is_an_unknown_format_and_never_costs_the_manifest(string section, string reason)
    {
        var manifest = ShipDataManifestReader.Read(ShipDataRelease.Frozen().Manifest(change: m => m["hauling"] = JsonNode.Parse(section)));

        manifest.Files.Should().NotBeEmpty();
        manifest.Hauling.Should().BeOfType<HaulingOffer.UnknownFormat>().Which.Reason.Should().Contain(reason);
    }

    [Theory]
    [InlineData("file", "manifest.json")]
    [InlineData("file", "outlines.json")]
    [InlineData("file", "hauling-templates.json")]
    [InlineData("bytes", "0")]
    [InlineData("sha256", "\"not a hash\"")]
    public void An_asset_entry_out_of_form_is_an_unknown_format(string field, string value)
    {
        var load = Load(ShipDataRelease.Frozen().WithHauling(), m => m["hauling"]!["positions"]![field] = JsonNode.Parse(field == "file" ? $"\"{value}\"" : value));

        load.Should().BeOfType<HaulingLoad.UnknownFormat>();
    }

    [Fact]
    public void A_file_that_differs_from_its_hash_or_length_is_an_unknown_format()
    {
        var release = ShipDataRelease.Frozen().WithHauling();
        var manifest = ShipDataManifestReader.Read(release.Manifest());
        File.WriteAllBytes(Path.Combine(_folder, ShipDataRelease.TemplatesAsset), release.Hauling!.Value.Templates);
        var positions = (byte[])release.Hauling!.Value.Positions.Clone();
        positions[^2] ^= 1;
        File.WriteAllBytes(Path.Combine(_folder, ShipDataRelease.PositionsAsset), positions);

        HaulingFiles.Load(_folder, manifest.Hauling, release.DataVersion).Should().BeOfType<HaulingLoad.UnknownFormat>()
            .Which.Reason.Should().Contain("positions.json").And.Contain("hash");
    }

    [Fact]
    public void A_file_of_another_schema_than_the_section_is_an_unknown_format()
    {
        var templates = JsonNode.Parse(TemplatesFile(Template("a")))!;
        templates["schemaVersion"] = 2;

        Load(Encoding.UTF8.GetBytes(templates.ToJsonString()), PositionsFile(World())).Should().BeOfType<HaulingLoad.UnknownFormat>()
            .Which.Reason.Should().Contain("hauling-templates.json").And.Contain("schemaVersion");
    }

    [Fact]
    public void A_name_twice_in_one_system_is_an_unknown_format_once_per_system_is_fine()
    {
        Load(TemplatesFile(Template("a")), PositionsFile([.. World(), Position(HurL1, x: 1)])).Should().BeOfType<HaulingLoad.UnknownFormat>()
            .Which.Reason.Should().Contain(HurL1).And.Contain("twice");

        // The gateway stands in both systems (K3).
        Loaded(Load(TemplatesFile(Template("a")), PositionsFile(World()))).Positions.Find(StarSystem.Pyro, Gateway).Should().NotBeNull();
    }

    public static TheoryData<string, string> FailedChecks => new()
    {
        { """{"pickupPool": ["Port Olisar"]}""", "'Port Olisar' has no position in stanton" },
        { """{"dropoffPool": ["Ruin Station"]}""", "'Ruin Station' has no position in stanton" },
        { """{"system": null, "dropoffPool": ["Nowhere"]}""", "'Nowhere' has no position." },
        { """{"legs": []}""", "has no legs" },
        { """{"legs": [{"commodity": "Gold", "minScu": 0, "maxScu": 4, "containerScu": 4}]}""", "no band" },
        { """{"legs": [{"commodity": "Gold", "minScu": 5, "maxScu": 4, "containerScu": 4}]}""", "no band" },
        { """{"legs": [{"commodity": "Gold", "minScu": 1, "maxScu": 4, "containerScu": 3}]}""", "no crate size" },
        { """{"stops": 0}""", "stops" },
        { """{"title": null}""", "title" },
        { """{"legs": [{"commodity": "Gold", "minScu": 1, "maxScu": 4}]}""", "containerScu" },
    };

    [Theory]
    [MemberData(nameof(FailedChecks))]
    public void A_failed_check_is_an_unknown_format_of_the_whole_delivery(string change, string reason)
    {
        var template = Template("a", t =>
        {
            foreach (var (key, value) in JsonNode.Parse(change)!.AsObject())
            {
                t[key] = value?.DeepClone();
            }
        });

        LoadTemplates(template, RedWind("b")).Should().BeOfType<HaulingLoad.UnknownFormat>()
            .Which.Reason.Should().Contain("hauling-templates.json").And.ContainEquivalentOf(reason);
    }

    [Fact]
    public void A_template_without_a_system_finds_its_places_in_any_system() =>
        Loaded(LoadTemplates(Template("a", t =>
        {
            t["system"] = null;
            t["missionType"] = "interstellar";
            t["dropoffPool"] = new JsonArray(Ruin);
        }))).Templates.Should().ContainSingle().Which.System.Should().BeNull();

    [Theory]
    [InlineData("contractor", "deadSaints")]
    [InlineData("rank", "boss")]
    [InlineData("size", "huge")]
    [InlineData("shape", "loop")]
    [InlineData("missionType", "galactic")]
    [InlineData("faction", "xenothreat")]
    [InlineData("system", "castra")]
    public void A_word_this_app_does_not_know_costs_only_its_template(string field, string word)
    {
        var load = LoadTemplates(Template("a", t => t[field] = word), RedWind("b"));

        var loaded = load.Should().BeOfType<HaulingLoad.Loaded>().Subject;
        loaded.Data.Templates.Should().ContainSingle().Which.Uuid.Should().Be("b");
        loaded.Skipped.Should().ContainSingle().Which.Should().Contain("(a)").And.Contain($"'{word}'");
    }

    [Fact]
    public void A_position_in_a_system_this_app_does_not_know_costs_it_and_the_templates_naming_it()
    {
        var load = Load(
            TemplatesFile(Template("a"), Template("c", t => t["dropoffPool"] = new JsonArray(HurL1, "Castra Station"))),
            PositionsFile([.. World(), Position("Castra Station", "castra")]));

        var loaded = load.Should().BeOfType<HaulingLoad.Loaded>().Subject;
        loaded.Data.Templates.Select(t => t.Uuid).Should().Equal("a");
        loaded.Data.Positions.All.Should().HaveCount(7);
        loaded.Skipped.Should().HaveCount(2).And.Contain(s => s.Contains("'castra'")).And.Contain(s => s.Contains("(c)"));
    }

    [Fact]
    public void Fields_this_app_does_not_read_are_ignored() =>
        Loaded(LoadTemplates(Template("a", t => t["newField"] = new JsonObject { ["x"] = 1 }))).Templates.Should().ContainSingle();
}
