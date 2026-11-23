using System.IO;
using System.Net;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipData;
using CargoPlan.App.Updates;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// Network access 3 (T12.3, D112): the manifest from the latest data release, ships.zip from the release it names, the
/// install through the store. A fake handler answers per file name; nothing leaves the machine.
/// </summary>
public sealed class ShipDataSourceTests : IDisposable
{
    private const string LatestManifest = "https://github.com/MRiediger/sc-cargoplan-shipdata/releases/latest/download/manifest.json";

    private const string ShipsZip = "https://github.com/MRiediger/sc-cargoplan-shipdata/releases/download/ships-2026.11.1/ships.zip";

    private static readonly ActiveShipData Baseline = new(
        new ShipDataVersion(2026, 11, 0), ShipDataOrigin.Baseline, AppContext.BaseDirectory, FrozenShips.Folder, FrozenOutlines.PathOfFile,
        new Core.Holo.HoloOffer.NotOffered(), new HaulingOffer.None());

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    private readonly FakeHttp.FakeHandler _handler = new();

    private readonly ShipDataRelease _release = ShipDataRelease.Frozen();

    private readonly List<int> _progress = [];

    public ShipDataSourceTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private ShipDataStore NewStore() => new(ShipDataStore.RootIn(_folder), Baseline);

    private ShipDataSource Source(string? source = null, TimeSpan? timeout = null) =>
        new(_handler, NewStore(), source, timeout ?? TimeSpan.FromSeconds(5), timeout ?? TimeSpan.FromSeconds(5), "1.2.3");

    private void Publish(ShipDataRelease? release = null, byte[]? manifest = null, byte[]? zip = null)
    {
        release ??= _release;
        _handler.Serve("manifest.json", manifest ?? release.Manifest());
        _handler.Serve("ships.zip", zip ?? release.Zip());
    }

    private static UpdateSettings On => UpdateSettings.Default;

    private static ShipDataVersion V(string tag) => ShipDataVersion.TryParse(tag, out var v) ? v : throw new ArgumentException(tag);

    // --- The check at the start ---

    [Fact]
    public async Task The_check_asks_the_latest_release_once_and_offers_a_newer_one()
    {
        Publish();
        using var source = Source();

        var verdict = await source.CheckAsync(On, Baseline.Version, isDevBuild: false, CancellationToken.None);

        verdict.Should().Be(new ShipDataOffer(V("ships-2026.11.1")));
        _handler.Requests.Should().ContainSingle().Which.Should().Be((LatestManifest, "GET", "SC-CargoPlan/1.2.3"));
        (await source.CheckAsync(On, Baseline.Version, false, CancellationToken.None)).Should().BeNull();
        _handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Switched_off_it_sends_nothing()
    {
        Publish();
        using var source = Source();

        (await source.CheckAsync(On with { CheckOnStartup = false }, Baseline.Version, false, CancellationToken.None)).Should().BeNull();

        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_dev_build_without_a_source_sends_nothing()
    {
        Publish();
        using var source = Source();

        (await source.CheckAsync(On, Baseline.Version, isDevBuild: true, CancellationToken.None)).Should().BeNull();

        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task The_same_release_as_the_active_one_is_no_offer()
    {
        Publish();
        using var source = Source();

        (await source.CheckAsync(On, V("ships-2026.11.1"), false, CancellationToken.None)).Should().BeOfType<NoShipDataUpdate>();
    }

    [Fact]
    public async Task A_release_of_an_unknown_schema_needs_an_app_update()
    {
        var release = ShipDataRelease.Frozen("ships-2026.12", schemaVersion: 7);
        Publish(release);
        using var source = Source();

        (await source.CheckAsync(On, Baseline.Version, false, CancellationToken.None)).Should().Be(new ShipDataNeedsApp(V("ships-2026.12"), 7));
    }

    [Fact]
    public async Task Offline_a_404_a_broken_manifest_and_a_timeout_show_nothing()
    {
        foreach (var setUp in (Action[])[
            () => _handler.Fail("manifest.json"),
            () => _handler.Answer("manifest.json", () => new HttpResponseMessage(HttpStatusCode.NotFound)),
            () => _handler.Serve("manifest.json", "<html>rate limit</html>"u8.ToArray()),
            () => _handler.Hang("manifest.json")])
        {
            setUp();
            using var source = Source(timeout: TimeSpan.FromMilliseconds(50));

            (await source.CheckAsync(On, Baseline.Version, false, CancellationToken.None)).Should().BeNull();
        }

        _handler.Cancelled.Should().Be(1);
    }

    [Fact]
    public async Task Closing_the_app_before_the_check_sends_nothing()
    {
        Publish();
        using var source = Source();
        using var closing = new CancellationTokenSource();
        await closing.CancelAsync();

        (await source.CheckAsync(On, Baseline.Version, false, closing.Token)).Should().BeNull();

        _handler.Requests.Should().BeEmpty();
    }

    // --- Download and install ---

    [Fact]
    public async Task Update_downloads_ships_zip_of_that_release_and_installs_it()
    {
        Publish();
        using var source = Source();
        await source.CheckAsync(On, Baseline.Version, false, CancellationToken.None);

        var reason = await source.DownloadAsync("ships-2026.11.1", _progress.Add, CancellationToken.None);

        reason.Should().BeNull();
        _handler.Requests.Select(r => r.Uri).Should().Equal(LatestManifest, ShipsZip);
        _progress.Should().EndWith(100);
        var active = NewStore().Active();
        active.Version.Should().Be(V("ships-2026.11.1"));
        active.Origin.Should().Be(ShipDataOrigin.Downloaded);
        source.Installed.Should().Be(V("ships-2026.11.1"));
    }

    // T13.2, D112 addendum: two more files on the same click, from the same release folder.
    [Fact]
    public async Task Update_downloads_the_hauling_assets_on_the_same_click()
    {
        var release = ShipDataRelease.Frozen().WithHauling();
        Publish(release);
        foreach (var (file, data) in release.HaulingAssets)
        {
            _handler.Serve(file, data);
        }

        using var source = Source();
        await source.CheckAsync(On, Baseline.Version, false, CancellationToken.None);

        (await source.DownloadAsync("ships-2026.11.1", _progress.Add, CancellationToken.None)).Should().BeNull();

        const string folder = "https://github.com/MRiediger/sc-cargoplan-shipdata/releases/download/ships-2026.11.1/";
        _handler.Requests.Select(r => r.Uri).Should().Equal(LatestManifest, ShipsZip, folder + "hauling-templates.json", folder + "positions.json");
        HaulingFiles.Load(NewStore().Active()).Should().BeOfType<HaulingLoad.Loaded>();
    }

    [Fact]
    public async Task A_hauling_asset_that_fails_costs_only_the_hauling_data()
    {
        var release = ShipDataRelease.Frozen().WithHauling();
        Publish(release);
        _handler.Serve("hauling-templates.json", [.. release.Hauling!.Value.Templates, (byte)' ']);
        _handler.Answer("positions.json", () => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var source = Source();
        await source.CheckAsync(On, Baseline.Version, false, CancellationToken.None);

        (await source.DownloadAsync("ships-2026.11.1", _progress.Add, CancellationToken.None)).Should().BeNull();

        var active = NewStore().Active();
        active.Version.Should().Be(V("ships-2026.11.1"));
        HaulingFiles.Load(active).Should().BeOfType<HaulingLoad.None>();
    }

    [Fact]
    public async Task A_zip_that_does_not_match_the_manifest_is_damaged_and_installs_nothing()
    {
        var manifest = _release.Manifest();
        _release.Entries["ships/hull_a.json"] = [.. _release.Entries["ships/hull_a.json"], (byte)' '];
        Publish(manifest: manifest);
        using var source = Source();
        await source.CheckAsync(On, Baseline.Version, false, CancellationToken.None);

        var reason = await source.DownloadAsync("ships-2026.11.1", _progress.Add, CancellationToken.None);

        reason.Should().Be(ShipDataSource.Damaged);
        NewStore().Active().Should().Be(Baseline);
        source.Installed.Should().BeNull();
    }

    [Fact]
    public async Task A_download_that_fails_says_no_network_and_a_404_says_see_the_log()
    {
        Publish();
        using var source = Source();
        await source.CheckAsync(On, Baseline.Version, false, CancellationToken.None);

        _handler.Fail("ships.zip");
        (await source.DownloadAsync("ships-2026.11.1", _progress.Add, CancellationToken.None)).Should().Be(UpdateFailure.NoNetwork);

        _handler.Answer("ships.zip", () => new HttpResponseMessage(HttpStatusCode.NotFound));
        (await source.DownloadAsync("ships-2026.11.1", _progress.Add, CancellationToken.None)).Should().Be(UpdateFailure.SeeLog);

        NewStore().Active().Should().Be(Baseline);
    }

    [Fact]
    public async Task A_version_that_was_never_offered_is_not_downloaded()
    {
        Publish();
        using var source = Source();

        (await source.DownloadAsync("ships-2026.11.1", _progress.Add, CancellationToken.None)).Should().Be(UpdateFailure.SeeLog);

        _handler.Requests.Should().BeEmpty();
    }

    // --- --ship-data-source (P5) ---

    [Fact]
    public async Task A_local_folder_stands_in_for_the_release_and_sends_nothing()
    {
        var feed = Path.Combine(_folder, "feed");
        Directory.CreateDirectory(feed);
        File.WriteAllBytes(Path.Combine(feed, "manifest.json"), _release.Manifest());
        File.WriteAllBytes(Path.Combine(feed, "ships.zip"), _release.Zip());
        using var source = Source(feed);

        (await source.CheckAsync(On, Baseline.Version, isDevBuild: true, CancellationToken.None)).Should().Be(new ShipDataOffer(V("ships-2026.11.1")));
        (await source.DownloadAsync("ships-2026.11.1", _progress.Add, CancellationToken.None)).Should().BeNull();

        _handler.Requests.Should().BeEmpty();
        NewStore().Active().Version.Should().Be(V("ships-2026.11.1"));
    }

    [Fact]
    public async Task A_local_folder_that_is_gone_shows_nothing_and_its_download_says_no_network()
    {
        var feed = Path.Combine(_folder, "feed");
        Directory.CreateDirectory(feed);
        File.WriteAllBytes(Path.Combine(feed, "manifest.json"), _release.Manifest());
        using var source = Source(feed);
        await source.CheckAsync(On, Baseline.Version, false, CancellationToken.None);

        (await source.DownloadAsync("ships-2026.11.1", _progress.Add, CancellationToken.None)).Should().Be(UpdateFailure.NoNetwork);

        Directory.Delete(feed, recursive: true);
        using var again = Source(feed);
        (await again.CheckAsync(On, Baseline.Version, false, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Another_repository_is_asked_the_same_way()
    {
        Publish();
        using var source = Source("https://github.com/someone/test-ship-data");

        await source.CheckAsync(On, Baseline.Version, false, CancellationToken.None);
        await source.DownloadAsync("ships-2026.11.1", _progress.Add, CancellationToken.None);

        _handler.Requests.Select(r => r.Uri).Should().Equal(
            "https://github.com/someone/test-ship-data/releases/latest/download/manifest.json",
            "https://github.com/someone/test-ship-data/releases/download/ships-2026.11.1/ships.zip");
    }

    [Theory]
    [InlineData("https://github.com/someone/test-ship-data", true)]
    [InlineData("https://github.com/someone/test-ship-data/", true)]
    [InlineData(@"D:\run\ship-data", true)]
    [InlineData("http://github.com/someone/test-ship-data", false)]
    [InlineData("https://example.com/someone/test-ship-data", false)]
    [InlineData("https://github.com/someone", false)]
    [InlineData("ship-data", false)]
    public void A_source_is_a_full_folder_or_a_github_repository(string source, bool valid) =>
        ShipDataSource.IsValidSource(source).Should().Be(valid);

    // T12.4, R2: the holograms come from the source's repository; a folder (or none) gives the data channel's.
    [Theory]
    [InlineData(null, "https://github.com/MRiediger/sc-cargoplan-shipdata/")]
    [InlineData(@"D:\run\ship-data", "https://github.com/MRiediger/sc-cargoplan-shipdata/")]
    [InlineData("https://github.com/someone/test-ship-data", "https://github.com/someone/test-ship-data/")]
    public void The_repository_of_a_source_is_its_own_or_the_data_channels(string? source, string repository) =>
        ShipDataSource.RepositoryFor(source).ToString().Should().Be(repository);

    [Fact]
    public void A_release_folder_holds_the_assets_of_its_tag() =>
        new Uri(ShipDataSource.ReleaseFolder(ShipDataSource.Repository, V("ships-2026.11.1")), "gama-hyun.holo").ToString()
            .Should().Be("https://github.com/MRiediger/sc-cargoplan-shipdata/releases/download/ships-2026.11.1/gama-hyun.holo");

    // --- Check now (P8) ---

    [Fact]
    public async Task Check_now_says_what_it_found_and_runs_as_often_as_asked()
    {
        Publish();
        using var source = Source();

        (await source.CheckNowAsync(null, Baseline.Version, isDevBuild: false, CancellationToken.None))
            .Should().Be(new ShipDataCheckNow(new ShipDataOffer(V("ships-2026.11.1")), "Ship data update available: 2026.11.1"));
        (await source.CheckNowAsync("ships-2026.11.1", Baseline.Version, false, CancellationToken.None)).Status
            .Should().Be("2026.11.1 is skipped");
        (await source.CheckNowAsync(null, V("ships-2026.11.1"), false, CancellationToken.None)).Status
            .Should().Be("Up to date · 2026.11.1");
        _handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task Check_now_in_a_dev_build_without_a_source_sends_nothing()
    {
        using var source = Source();

        (await source.CheckNowAsync(null, Baseline.Version, isDevBuild: true, CancellationToken.None))
            .Should().Be(new ShipDataCheckNow(null, ShipDataSource.DevBuildText));
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Check_now_offline_says_it_failed()
    {
        _handler.Fail("manifest.json");
        using var source = Source();

        (await source.CheckNowAsync(null, Baseline.Version, false, CancellationToken.None)).Status.Should().Be(UpdateService.CheckFailedText);
    }

    [Fact]
    public async Task Check_now_names_a_release_that_needs_an_app_update()
    {
        Publish(ShipDataRelease.Frozen("ships-2026.12", schemaVersion: 7));
        using var source = Source();

        (await source.CheckNowAsync(null, Baseline.Version, false, CancellationToken.None)).Status
            .Should().Be("Ship data 2026.12 needs an app update");
    }
}
