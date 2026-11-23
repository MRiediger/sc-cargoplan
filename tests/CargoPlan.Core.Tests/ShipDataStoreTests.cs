using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The downloaded ship data under <c>ships\&lt;dataVersion&gt;\</c> (T12.3): which one is active, how a release gets there
/// checked and whole or not at all, and what goes at the start. The baseline is the frozen ships (A10), not ships/.
/// </summary>
public sealed class ShipDataStoreTests : IDisposable
{
    // ships-2026.11: the seed release, older than the downloads below.
    private static readonly ActiveShipData Baseline = new(
        new ShipDataVersion(2026, 11, 0), ShipDataOrigin.Baseline, AppContext.BaseDirectory, FrozenShips.Folder, FrozenOutlines.PathOfFile, new HoloOffer.NotOffered(), new HaulingOffer.None());

    private readonly string _root = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"), "ships");

    public void Dispose()
    {
        var parent = Path.GetDirectoryName(_root)!;
        if (Directory.Exists(parent))
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    private ShipDataStore Store(long maxUnpackedBytes = ShipDataStore.MaxUnpackedBytes) => new(_root, Baseline, maxUnpackedBytes);

    private ActiveShipData Install(ShipDataRelease release, byte[]? zip = null, ShipDataStore? store = null, Action<JsonObject>? change = null)
    {
        var manifest = release.Manifest(change: change);
        using var stream = new MemoryStream(zip ?? release.Zip());
        return (store ?? Store()).Install(ShipDataManifestReader.Read(manifest), manifest, stream);
    }

    /// <summary>A folder as a download would leave it, but only its manifest: <see cref="ShipDataStore.Active"/> reads no more.</summary>
    private void Plant(string folder, string dataVersion, int schemaVersion = 6)
    {
        Directory.CreateDirectory(Path.Combine(_root, folder));
        var manifest = new ShipDataRelease { DataVersion = dataVersion, SchemaVersion = schemaVersion }.Manifest(
            files: new Dictionary<string, string> { ["outlines.json"] = new('a', 64), ["ships/hull_a.json"] = new('b', 64) });
        File.WriteAllBytes(Path.Combine(_root, folder, ShipDataStore.ManifestFileName), manifest);
    }

    private string[] Folders() =>
        Directory.Exists(_root) ? [.. Directory.EnumerateDirectories(_root).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)] : [];

    // --- Which data is active ---

    [Fact]
    public void Without_a_download_the_baseline_next_to_the_exe_is_active() => Store().Active().Should().Be(Baseline);

    [Fact]
    public void An_installed_release_is_active_and_loads_ships_modules_and_outlines()
    {
        var installed = Install(ShipDataRelease.Frozen());

        var active = Store().Active();
        active.Should().Be(installed);
        active.Version.ToString().Should().Be("ships-2026.11.1");
        active.Origin.Should().Be(ShipDataOrigin.Downloaded);
        active.Folder.Should().Be(Path.Combine(_root, "ships-2026.11.1"));
        active.ShipsFolder.Should().Be(Path.Combine(_root, "ships-2026.11.1", "ships"));
        active.OutlinesPath.Should().Be(Path.Combine(_root, "ships-2026.11.1", "outlines.json"));

        // E1 of the approval: the module lands in ships/modules/ and makes its catalog entry.
        var catalog = ShipLoader.LoadAll(active.ShipsFolder);
        catalog.Problems.Should().BeEmpty();
        catalog.Get("aurora_mk_ii_ts_module").Should().NotBeNull();
        catalog.Ships.Should().HaveCount(FrozenShips.LoadAll().Ships.Count);
        ShipOutlineLoader.Load(active.OutlinesPath).Problems.Should().BeEmpty();
        File.Exists(Path.Combine(active.Folder, ShipDataStore.ManifestFileName)).Should().BeTrue();
        Folders().Should().Equal("ships-2026.11.1");
    }

    // T12.4: the holograms come with the data in use, read once with its manifest.
    [Fact]
    public void The_install_and_the_next_start_carry_the_holograms_of_the_manifest()
    {
        var hash = new string('c', 64);
        var installed = Install(ShipDataRelease.Frozen(), change: m => m["holo"] = new JsonObject
        {
            ["formatVersion"] = 2,
            ["ships"] = new JsonObject { ["misc-hull-a"] = new JsonObject { ["file"] = "misc-hull-a.holo", ["bytes"] = 3, ["sha256"] = hash } },
        });

        var holo = installed.Holo.Should().BeOfType<HoloOffer.Offered>().Which.Manifest;
        holo.DataVersion.Should().Be("ships-2026.11.1");
        holo.Ships.Should().Equal(new HoloManifestEntry("misc-hull-a", "misc-hull-a.holo", 3, hash));
        Store().Active().Holo.Should().Be(installed.Holo);
    }

    // T13.2: the two hauling assets come on the same click; they never cost the ship data.
    [Fact]
    public void The_install_lays_the_hauling_files_next_to_the_manifest()
    {
        var release = ShipDataRelease.Frozen().WithHauling();
        var manifest = release.Manifest();
        using var zip = new MemoryStream(release.Zip());

        var installed = Store().Install(ShipDataManifestReader.Read(manifest), manifest, zip, release.HaulingAssets);

        Store().Active().Should().Be(installed);
        installed.Hauling.Should().BeOfType<HaulingOffer.Offered>();
        File.ReadAllBytes(Path.Combine(installed.Folder, ShipDataRelease.PositionsAsset)).Should().Equal(release.Hauling!.Value.Positions);
        HaulingFiles.Load(installed).Should().BeOfType<HaulingLoad.Loaded>().Which.Data.Templates.Should().HaveCount(2);
    }

    [Fact]
    public void A_hauling_asset_that_fails_its_hash_stays_out_and_the_ships_install()
    {
        var release = ShipDataRelease.Frozen().WithHauling();
        var manifest = release.Manifest();
        var broken = release.HaulingAssets.ToDictionary(a => a.Key, a => a.Key == ShipDataRelease.TemplatesAsset ? [.. a.Value, (byte)' '] : a.Value);
        using var zip = new MemoryStream(release.Zip());

        var installed = Store().Install(ShipDataManifestReader.Read(manifest), manifest, zip, broken);

        Store().Active().Should().Be(installed);
        ShipLoader.LoadAll(installed.ShipsFolder).Problems.Should().BeEmpty();
        File.Exists(Path.Combine(installed.Folder, ShipDataRelease.TemplatesAsset)).Should().BeFalse();
        HaulingFiles.Load(installed).Should().BeOfType<HaulingLoad.None>().Which.Reason.Should().Contain("not installed");
    }

    [Fact]
    public void An_install_without_the_hauling_assets_has_none_at_the_next_start()
    {
        var installed = Install(ShipDataRelease.Frozen().WithHauling());

        installed.Hauling.Should().BeOfType<HaulingOffer.Offered>();
        HaulingFiles.Load(Store().Active()).Should().BeOfType<HaulingLoad.None>();
    }

    [Fact]
    public void A_release_with_the_kill_switch_is_active_without_holograms()
    {
        Install(ShipDataRelease.Frozen(), change: m => m["holo"] = null);

        var active = Store().Active();
        active.Origin.Should().Be(ShipDataOrigin.Downloaded);
        active.Holo.Should().Be(new HoloOffer.NotOffered());
    }

    [Fact]
    public void The_highest_readable_release_wins()
    {
        Plant("ships-2026.11.1", "ships-2026.11.1");
        Plant("ships-2026.12", "ships-2026.12");
        Plant("ships-2027.01", "ships-2027.01", schemaVersion: 7);

        Store().Active().Version.ToString().Should().Be("ships-2026.12");
    }

    [Fact]
    public void A_release_older_than_the_baseline_is_never_active()
    {
        Plant("ships-2026.10", "ships-2026.10");

        Store().Active().Should().Be(Baseline);
    }

    [Fact]
    public void A_release_equal_to_the_baseline_loses_to_it()
    {
        Plant("ships-2026.11", "ships-2026.11");

        Store().Active().Should().Be(Baseline);
    }

    // R2 of the T12.5 approval: a download equal to the baseline is worth nothing and goes.
    [Fact]
    public void Clean_up_with_the_baseline_active_removes_a_release_equal_to_it()
    {
        Plant("ships-2026.11", "ships-2026.11");
        var store = Store();

        store.CleanUp(store.Active()).Should().Equal("removed ships-2026.11");

        Folders().Should().BeEmpty();
    }

    [Theory]
    [InlineData("ships-2026.12.tmp", "ships-2026.12")]
    [InlineData("ships-2026.12", "ships-2026.12.1")]
    [InlineData("latest", "ships-2026.12")]
    public void A_folder_that_is_not_its_releases_is_ignored(string folder, string dataVersion)
    {
        Plant(folder, dataVersion);

        Store().Active().Should().Be(Baseline);
    }

    [Fact]
    public void A_folder_with_a_broken_or_missing_manifest_is_ignored()
    {
        Plant("ships-2026.12", "ships-2026.12");
        File.WriteAllText(Path.Combine(_root, "ships-2026.12", ShipDataStore.ManifestFileName), "{ broken");
        Directory.CreateDirectory(Path.Combine(_root, "ships-2027.01"));

        Store().Active().Should().Be(Baseline);
    }

    // --- Install: checked, whole or not at all ---

    [Fact]
    public void A_changed_file_installs_nothing_and_the_old_data_stays_active()
    {
        var before = Install(ShipDataRelease.Frozen("ships-2026.11.1"));
        var release = ShipDataRelease.Frozen("ships-2026.12");
        var manifest = release.Manifest();
        release.Entries["ships/hull_a.json"] = [.. release.Entries["ships/hull_a.json"], (byte)' '];

        var act = () => Store().Install(ShipDataManifestReader.Read(manifest), manifest, new MemoryStream(release.Zip()));

        act.Should().Throw<ShipDataFormatException>().WithMessage("*ships/hull_a.json*");
        Store().Active().Should().Be(before);
        Folders().Should().Equal("ships-2026.11.1");
    }

    [Fact]
    public void A_file_the_manifest_lists_but_the_zip_lacks_installs_nothing()
    {
        var release = ShipDataRelease.Frozen();
        var manifest = release.Manifest();
        release.Entries.Remove("ships/modules/ts_module.json");

        var act = () => Store().Install(ShipDataManifestReader.Read(manifest), manifest, new MemoryStream(release.Zip()));

        act.Should().Throw<ShipDataFormatException>().WithMessage("*ships/modules/ts_module.json*");
        Store().Active().Should().Be(Baseline);
        Folders().Should().BeEmpty();
    }

    // E1 of the approval: the zip-slip rule. Entries outside the manifest's files never reach the disk.
    [Theory]
    [InlineData("../evil.json")]
    [InlineData("ships/../../evil.json")]
    [InlineData("ships/sub/z.json")]
    [InlineData("ships/modules/x/y.json")]
    [InlineData("ships/hull_b.json")]
    public void An_entry_the_manifest_does_not_list_installs_nothing(string path)
    {
        var release = ShipDataRelease.Frozen();

        var act = () => Install(release, release.Zip((path, Encoding.UTF8.GetBytes("{}"))));

        act.Should().Throw<ShipDataFormatException>().WithMessage($"*{path}*");
        Store().Active().Should().Be(Baseline);
        Folders().Should().BeEmpty();
        File.Exists(Path.Combine(Path.GetDirectoryName(_root)!, "evil.json")).Should().BeFalse();
        File.Exists(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_root)!)!, "evil.json")).Should().BeFalse();
    }

    [Fact]
    public void A_download_that_breaks_off_while_unpacking_leaves_the_old_data_active()
    {
        var before = Install(ShipDataRelease.Frozen("ships-2026.11.1"));
        var release = ShipDataRelease.Frozen("ships-2026.12");
        var manifest = release.Manifest();
        var zip = release.Zip();
        using var breaking = new BreakingStream(zip, zip.Length / 2);

        var act = () => Store().Install(ShipDataManifestReader.Read(manifest), manifest, breaking);

        act.Should().Throw<IOException>();
        breaking.Broke.Should().BeTrue();
        Store().Active().Should().Be(before);
        Folders().Should().Equal("ships-2026.11.1");
    }

    [Fact]
    public void Something_that_is_not_a_zip_installs_nothing()
    {
        var release = ShipDataRelease.Frozen();

        var act = () => Install(release, Encoding.UTF8.GetBytes("<html>rate limit</html>"));

        act.Should().Throw<ShipDataFormatException>().WithMessage("*ships.zip*");
        Folders().Should().BeEmpty();
    }

    [Fact]
    public void A_release_bigger_than_the_limit_unpacked_installs_nothing()
    {
        var release = ShipDataRelease.Frozen();

        var act = () => Install(release, store: Store(maxUnpackedBytes: 4096));

        act.Should().Throw<ShipDataFormatException>().WithMessage("*4096*");
        Folders().Should().BeEmpty();
    }

    [Fact]
    public void A_release_of_a_schema_the_app_cannot_read_is_not_installed()
    {
        var release = ShipDataRelease.Frozen(schemaVersion: 7);

        var act = () => Install(release);

        act.Should().Throw<ShipDataFormatException>().WithMessage("*schemaVersion 7*");
        Folders().Should().BeEmpty();
    }

    [Fact]
    public void A_leftover_of_an_earlier_attempt_does_not_stop_the_install()
    {
        Directory.CreateDirectory(Path.Combine(_root, "ships-2026.11.1.tmp", "ships"));
        File.WriteAllText(Path.Combine(_root, "ships-2026.11.1.tmp", "ships", "stale.json"), "{}");

        var installed = Install(ShipDataRelease.Frozen());

        File.Exists(Path.Combine(installed.ShipsFolder, "stale.json")).Should().BeFalse();
        Folders().Should().Equal("ships-2026.11.1");
    }

    [Fact]
    public void A_folder_of_the_same_release_without_a_valid_manifest_is_replaced()
    {
        Directory.CreateDirectory(Path.Combine(_root, "ships-2026.11.1", "ships"));
        File.WriteAllText(Path.Combine(_root, "ships-2026.11.1", "ships", "stale.json"), "{}");

        var installed = Install(ShipDataRelease.Frozen());

        Store().Active().Should().Be(installed);
        File.Exists(Path.Combine(installed.ShipsFolder, "stale.json")).Should().BeFalse();
    }

    // --- Clean-up at the start: older than the active one goes, one stays ---

    [Fact]
    public void Clean_up_removes_leftovers_and_releases_older_than_the_active_one()
    {
        Plant("ships-2026.10", "ships-2026.10");
        Plant("ships-2026.11.1", "ships-2026.11.1");
        Plant("ships-2026.12", "ships-2026.12");
        Plant("ships-2027.01", "ships-2027.01", schemaVersion: 7);
        Directory.CreateDirectory(Path.Combine(_root, "ships-2027.02.tmp"));
        Directory.CreateDirectory(Path.Combine(_root, "notes"));
        var store = Store();
        var active = store.Active();

        var lines = store.CleanUp(active);

        active.Version.ToString().Should().Be("ships-2026.12");
        Folders().Should().Equal("notes", "ships-2026.12", "ships-2027.01");
        lines.Should().HaveCount(3).And.Contain(l => l.Contains("ships-2026.10", StringComparison.Ordinal));
    }

    [Fact]
    public void Clean_up_with_the_baseline_active_keeps_newer_releases_it_cannot_read()
    {
        Plant("ships-2026.10", "ships-2026.10");
        Plant("ships-2027.01", "ships-2027.01", schemaVersion: 7);
        var store = Store();

        store.CleanUp(store.Active());

        Folders().Should().Equal("ships-2027.01");
    }

    [Fact]
    public void Clean_up_without_a_folder_does_nothing() => Store().CleanUp(Baseline).Should().BeEmpty();

    [Fact]
    public void The_root_is_ships_in_the_data_folder() =>
        ShipDataStore.RootIn(Path.Combine("C:", "data")).Should().Be(Path.Combine("C:", "data", "ships"));

    [Fact]
    public void Installing_writes_the_manifest_as_downloaded()
    {
        var release = ShipDataRelease.Frozen();
        var manifest = release.Manifest(change: m => m["generatedAt"] = "kept as it came");

        var installed = Store().Install(ShipDataManifestReader.Read(manifest), manifest, new MemoryStream(release.Zip()));

        File.ReadAllBytes(Path.Combine(installed.Folder, ShipDataStore.ManifestFileName)).Should().Equal(manifest);
        JsonNode.Parse(manifest)!["generatedAt"]!.GetValue<string>().Should().Be("kept as it came");
    }
}
