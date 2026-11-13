using CargoPlan.App.Settings;
using CargoPlan.App.Updates;
using Velopack;
using Velopack.Locators;
using Velopack.Logging;
using Velopack.Sources;

namespace CargoPlan.App.Tests;

/// <summary>
/// The check against a local feed, as <c>--updates-source</c> runs it (T11.1, D109): Velopack's own
/// <see cref="TestVelopackLocator"/> stands in for an installed app. Every result is exactly one log line, collected here
/// instead of the shared test log, which parallel tests write too.
/// </summary>
public sealed class UpdateServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    private readonly List<string> _lines = [];

    private int _managersMade;

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string Feed => Path.Combine(_folder, "feed");

    /// <summary>
    /// A feed index as <c>vpk pack</c> 1.2.161 writes it (format taken from a real pack, T11.1); the check reads only the
    /// index, the package itself is needed for the download (T11.2).
    /// </summary>
    private void PublishToFeed(string version)
    {
        Directory.CreateDirectory(Feed);
        File.WriteAllText(
            Path.Combine(Feed, "releases.win.json"),
            $$"""{"Assets":[{"PackageId":"CargoPlan","Version":"{{version}}","Type":"Full","FileName":"CargoPlan-{{version}}-full.nupkg","SHA1":"8547BB968BB7CCB034B9AB3EA38FA29EA5DCEAF8","SHA256":"7AB2D86F72ECE5292119F5221C99BA60403AEDC794396125FBA082EA10B83316","Size":2107805}]}""");
    }

    private UpdateService Service(IUpdateSource? source = null, TimeSpan? timeout = null) =>
        new(
            () =>
            {
                _managersMade++;
                var packages = Directory.CreateDirectory(Path.Combine(_folder, "packages")).FullName;
                return new UpdateManager(
                    source ?? new SimpleFileSource(new DirectoryInfo(Feed)), null, new TestVelopackLocator("CargoPlan", "0.9.0", packages));
            },
            timeout ?? UpdateService.Timeout,
            _lines.Add);

    private static readonly UpdateSettings On = UpdateSettings.Default;

    [Fact]
    public async Task A_newer_release_in_the_feed_is_offered()
    {
        PublishToFeed("0.9.1");

        var offer = await Service().CheckAsync(On, "0.9.0", isDevBuild: false, CancellationToken.None);

        offer.Should().Be(new UpdateAvailable("0.9.1", null));
        _lines.Should().ContainSingle().Which.Should().Be("0.9.1 available (installed 0.9.0)");
    }

    [Fact]
    public async Task The_same_release_is_no_update_and_one_line()
    {
        PublishToFeed("0.9.0");

        var offer = await Service().CheckAsync(On, "0.9.0", false, CancellationToken.None);

        offer.Should().BeNull();
        _lines.Should().ContainSingle().Which.Should().Be("nothing newer than 0.9.0");
    }

    [Fact]
    public async Task A_skipped_release_is_one_line_and_no_offer()
    {
        PublishToFeed("0.9.1");

        var offer = await Service().CheckAsync(On with { SkippedVersion = "0.9.1" }, "0.9.0", false, CancellationToken.None);

        offer.Should().BeNull();
        _lines.Should().ContainSingle().Which.Should().Be("0.9.1 skipped (updates.skippedVersion)");
    }

    [Fact]
    public async Task An_empty_feed_folder_fails_quietly_with_one_line()
    {
        // Gate 4: the source isn't usable; the player sees nothing (E8), the log says why.
        Directory.CreateDirectory(Feed);

        var offer = await Service().CheckAsync(On, "0.9.0", false, CancellationToken.None);

        offer.Should().BeNull();
        _lines.Should().ContainSingle();
    }

    [Fact]
    public async Task Check_off_asks_nothing_and_logs_nothing()
    {
        // Gate 2: no manager, so no network and no Velopack log either.
        PublishToFeed("0.9.1");

        var offer = await Service().CheckAsync(On with { CheckOnStartup = false }, "0.9.0", false, CancellationToken.None);

        offer.Should().BeNull();
        _managersMade.Should().Be(0);
        _lines.Should().BeEmpty();
    }

    [Fact]
    public async Task A_dev_build_asks_nothing_and_logs_nothing()
    {
        PublishToFeed("0.9.1");

        var offer = await Service().CheckAsync(On, "0.0.0-dev", isDevBuild: true, CancellationToken.None);

        offer.Should().BeNull();
        _managersMade.Should().Be(0);
        _lines.Should().BeEmpty();
    }

    [Fact]
    public async Task One_check_per_run()
    {
        PublishToFeed("0.9.1");
        var service = Service();

        await service.CheckAsync(On, "0.9.0", false, CancellationToken.None);
        var second = await service.CheckAsync(On, "0.9.0", false, CancellationToken.None);

        second.Should().BeNull();
        _managersMade.Should().Be(1);
        _lines.Should().ContainSingle();
    }

    [Fact]
    public async Task A_source_that_never_answers_ends_at_the_timeout()
    {
        var offer = await Service(new SilentSource(), TimeSpan.FromMilliseconds(100)).CheckAsync(On, "0.9.0", false, CancellationToken.None);

        offer.Should().BeNull();
        _lines.Should().ContainSingle().Which.Should().StartWith("check failed: no answer within");
    }

    [Fact]
    public async Task Closing_the_app_before_the_answer_cancels_with_one_line()
    {
        // Freigabe E1: the result must not reach a window that is gone.
        using var closing = new CancellationTokenSource();
        var check = Service(new SilentSource()).CheckAsync(On, "0.9.0", false, closing.Token);

        await closing.CancelAsync();
        var offer = await check;

        offer.Should().BeNull();
        _lines.Should().ContainSingle().Which.Should().Be("check cancelled: the app is closing");
    }

    [Fact]
    public async Task Cancelled_before_the_start_still_says_so_once()
    {
        using var closed = new CancellationTokenSource();
        await closed.CancelAsync();

        var offer = await Service().CheckAsync(On, "0.9.0", false, closed.Token);

        offer.Should().BeNull();
        _managersMade.Should().Be(0);
        _lines.Should().ContainSingle().Which.Should().Be("check cancelled: the app is closing");
    }

    /// <summary>A feed that never answers, like a connection that hangs.</summary>
    private sealed class SilentSource : IUpdateSource
    {
        public Task<VelopackAssetFeed> GetReleaseFeed(
            IVelopackLogger logger, string? appId, string channel, Guid? stagingId = null, VelopackAsset? latestLocalRelease = null) =>
            new TaskCompletionSource<VelopackAssetFeed>().Task;

        public Task DownloadReleaseEntry(
            IVelopackLogger logger, VelopackAsset releaseEntry, string localFile, Action<int> progress, CancellationToken cancelToken = default) =>
            throw new NotSupportedException("The check never downloads.");
    }
}
