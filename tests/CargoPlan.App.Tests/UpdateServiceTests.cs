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
    /// A feed index as <c>vpk pack</c> 1.2.161 writes it (format taken from a real pack, T11.1), with a package whose size and
    /// SHA256 match: the download checks both (UpdateManager.cs 482–506). Its content is no real package; Velopack only logs
    /// that it can't take Update.exe out of it.
    /// </summary>
    private void PublishToFeed(string version)
    {
        Directory.CreateDirectory(Feed);
        var fileName = $"CargoPlan-{version}-full.nupkg";
        var bytes = System.Text.Encoding.UTF8.GetBytes($"package {version}");
        File.WriteAllBytes(Path.Combine(Feed, fileName), bytes);
        var sha1 = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(bytes));
        var sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        File.WriteAllText(
            Path.Combine(Feed, "releases.win.json"),
            $$"""{"Assets":[{"PackageId":"CargoPlan","Version":"{{version}}","Type":"Full","FileName":"{{fileName}}","SHA1":"{{sha1}}","SHA256":"{{sha256}}","Size":{{bytes.Length}}}]}""");
    }

    /// <summary>A full package left in packages\ by an earlier download (TestVelopackLocator's localPackage).</summary>
    private static VelopackAsset Lying(string version) => new()
    {
        PackageId = "CargoPlan",
        Version = SemanticVersion.Parse(version),
        Type = VelopackAssetType.Full,
        FileName = $"CargoPlan-{version}-full.nupkg",
    };

    /// <summary>The app around the service: elevated or not, its root writable or not, a package already in packages\.</summary>
    private sealed record Setup(
        IUpdateSource? Source = null, TimeSpan? Timeout = null, VelopackAsset? LocalPackage = null, bool Elevated = false, bool RootWritable = true);

    private string Root => Path.Combine(_folder, "root");

    private string Packages => Path.Combine(_folder, "packages");

    private UpdateService Service(IUpdateSource? source = null, TimeSpan? timeout = null) => Service(new Setup(source, timeout));

    private UpdateService Service(Setup setup) =>
        new(
            () =>
            {
                _managersMade++;
                Directory.CreateDirectory(Packages);
                var locator = new TestVelopackLocator(
                    "CargoPlan", "0.9.0", Packages, appDir: null, rootDir: Root, updateExe: Path.Combine(Root, "Update.exe"), localPackage: setup.LocalPackage);
                return new UpdateManager(setup.Source ?? new SimpleFileSource(new DirectoryInfo(Feed)), null, locator);
            },
            setup.Timeout ?? UpdateService.Timeout,
            _lines.Add,
            new UpdateEnvironment(
                FeedMissing: () => !Directory.Exists(Feed),
                IsElevated: () => setup.Elevated,
                RootFolder: () => Root,
                IsWritable: _ => setup.RootWritable));

    private static readonly UpdateSettings On = UpdateSettings.Default;

    // T11.3 (R8, R9): stable installs never see an rc; --updates-prerelease does, for the gate over GitHub.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GitHub_is_the_source_with_or_without_pre_releases(bool prerelease)
    {
        var source = UpdateService.CreateSource(null, prerelease);

        var github = source.Should().BeOfType<GithubSource>().Which;
        github.Prerelease.Should().Be(prerelease);
        github.RepoUri.Should().Be(new Uri(UpdateService.RepositoryUrl));
    }

    [Fact]
    public void A_local_feed_is_the_source_when_given()
    {
        UpdateService.CreateSource(Feed, prerelease: false).Should().BeOfType<SimpleFileSource>();
    }

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

    [Fact]
    public async Task Check_now_runs_after_the_start_check_and_again()
    {
        // U9: the start's one check per run doesn't bind the button.
        PublishToFeed("0.9.1");
        var service = Service();
        await service.CheckAsync(On, "0.9.0", false, CancellationToken.None);

        var first = await service.CheckNowAsync(null, "0.9.0", isDevBuild: false, CancellationToken.None);
        var second = await service.CheckNowAsync(null, "0.9.0", isDevBuild: false, CancellationToken.None);

        first.Should().Be(new ManualCheck(new UpdateAvailable("0.9.1", null), "Update available: v0.9.1"));
        second.Offer.Should().NotBeNull();
        _managersMade.Should().Be(3);
        (await service.CheckAsync(On, "0.9.0", false, CancellationToken.None)).Should().BeNull();
        _managersMade.Should().Be(3);
    }

    [Fact]
    public async Task Check_now_runs_with_the_start_check_off()
    {
        PublishToFeed("0.9.1");

        var result = await Service().CheckNowAsync(null, "0.9.0", false, CancellationToken.None);

        result.Offer.Should().Be(new UpdateAvailable("0.9.1", null));
    }

    [Fact]
    public async Task Check_now_respects_the_skipped_version()
    {
        PublishToFeed("0.9.1");

        var result = await Service().CheckNowAsync("0.9.1", "0.9.0", false, CancellationToken.None);

        result.Should().Be(new ManualCheck(null, "v0.9.1 is skipped"));
    }

    [Fact]
    public async Task Check_now_says_up_to_date()
    {
        PublishToFeed("0.9.0");

        var result = await Service().CheckNowAsync(null, "0.9.0", false, CancellationToken.None);

        result.Should().Be(new ManualCheck(null, UpdateService.UpToDateText));
    }

    [Fact]
    public async Task Check_now_in_a_dev_build_asks_nothing()
    {
        PublishToFeed("0.9.1");

        var result = await Service().CheckNowAsync(null, "0.0.0-dev", isDevBuild: true, CancellationToken.None);

        result.Should().Be(new ManualCheck(null, "Dev build – updates disabled"));
        _managersMade.Should().Be(0);
    }

    [Fact]
    public async Task Check_now_that_fails_says_where_to_look()
    {
        var result = await Service(new SilentSource(), TimeSpan.FromMilliseconds(100)).CheckNowAsync(null, "0.9.0", false, CancellationToken.None);

        result.Should().Be(new ManualCheck(null, UpdateService.CheckFailedText));
    }

    [Fact]
    public async Task The_offered_release_downloads_into_packages_with_progress()
    {
        PublishToFeed("0.9.1");
        var service = Service();
        await service.CheckAsync(On, "0.9.0", false, CancellationToken.None);
        List<int> progress = [];

        var reason = await service.DownloadAsync("0.9.1", progress.Add, CancellationToken.None);

        reason.Should().BeNull();
        File.Exists(Path.Combine(Packages, "CargoPlan-0.9.1-full.nupkg")).Should().BeTrue();
        progress.Should().NotBeEmpty().And.OnlyContain(p => p >= 0 && p <= 100);
        progress[^1].Should().Be(100);
    }

    [Fact]
    public async Task A_package_already_lying_there_is_taken_without_a_download()
    {
        // Velopack returns at File.Exists: no hash, no progress (UpdateManager.cs 254–256). Retry after a failed apply.
        PublishToFeed("0.9.1");
        var service = Service();
        await service.CheckAsync(On, "0.9.0", false, CancellationToken.None);
        await service.DownloadAsync("0.9.1", _ => { }, CancellationToken.None);
        File.Delete(Path.Combine(Feed, "CargoPlan-0.9.1-full.nupkg"));
        List<int> progress = [];

        var reason = await service.DownloadAsync("0.9.1", progress.Add, CancellationToken.None);

        reason.Should().BeNull();
        progress.Should().BeEmpty();
    }

    [Fact]
    public async Task A_feed_gone_after_the_check_is_no_network()
    {
        // Gate 6 with --updates-source.
        PublishToFeed("0.9.1");
        var service = Service();
        await service.CheckAsync(On, "0.9.0", false, CancellationToken.None);
        Directory.Move(Feed, Feed + "-gone");

        var reason = await service.DownloadAsync("0.9.1", _ => { }, CancellationToken.None);

        reason.Should().Be(UpdateFailure.NoNetwork);
        _lines.Should().Contain(line => line.StartsWith("download failed (no network): ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Elevated_with_a_root_it_cant_write_downloads_nothing()
    {
        // Update.exe would stop with "The update cannot continue." (apply_windows_impl.rs 63–64): said before the download.
        PublishToFeed("0.9.1");
        var service = Service(new Setup(Elevated: true, RootWritable: false));
        await service.CheckAsync(On, "0.9.0", false, CancellationToken.None);

        var reason = await service.DownloadAsync("0.9.1", _ => { }, CancellationToken.None);

        reason.Should().Be(UpdateFailure.FolderNotWritable);
        Directory.EnumerateFiles(Packages, "*.nupkg").Should().BeEmpty();
    }

    [Fact]
    public async Task Not_elevated_with_a_root_it_cant_write_still_downloads()
    {
        // Update.exe asks for UAC itself when it swaps (Freigabe: UAC is allowed).
        PublishToFeed("0.9.1");
        var service = Service(new Setup(RootWritable: false));
        await service.CheckAsync(On, "0.9.0", false, CancellationToken.None);

        var reason = await service.DownloadAsync("0.9.1", _ => { }, CancellationToken.None);

        reason.Should().BeNull();
    }

    [Fact]
    public async Task Closing_during_the_download_is_said_once_and_never_null()
    {
        // null would mean "apply now"; the app is closing instead.
        PublishToFeed("0.9.1");
        var service = Service();
        await service.CheckAsync(On, "0.9.0", false, CancellationToken.None);
        using var closing = new CancellationTokenSource();
        await closing.CancelAsync();

        var reason = await service.DownloadAsync("0.9.1", _ => { }, closing.Token);

        reason.Should().Be(UpdateFailure.Cancelled);
        _lines.Should().Contain("download cancelled: the app is closing");
    }

    [Fact]
    public async Task A_version_never_offered_is_not_downloaded()
    {
        PublishToFeed("0.9.1");

        var reason = await Service().DownloadAsync("0.9.1", _ => { }, CancellationToken.None);

        reason.Should().Be(UpdateFailure.SeeLog);
    }

    [Fact]
    public async Task Apply_without_Update_exe_fails_for_the_log()
    {
        PublishToFeed("0.9.1");
        var service = Service();
        await service.CheckAsync(On, "0.9.0", false, CancellationToken.None);
        await service.DownloadAsync("0.9.1", _ => { }, CancellationToken.None);

        var reason = service.Apply("0.9.1", ["--locked"]);

        reason.Should().Be(UpdateFailure.SeeLog);
        _lines.Should().Contain(line => line.StartsWith("apply failed (see cargoplan.log): ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_restart_with_a_newer_package_lying_there_is_a_failed_apply()
    {
        // Update.exe started the old app again (apply.rs 57–61); the package stays because auto-apply is off. Not writable:
        // Update.exe had to ask for UAC, and that was declined.
        var service = Service(new Setup(LocalPackage: Lying("0.9.1"), RootWritable: false));

        var failed = await service.FailedApplyAsync(restartedByUpdate: true, "0.9.0");

        failed.Should().Be(new FailedApply("0.9.1", UpdateFailure.Cancelled));
    }

    [Fact]
    public async Task A_failed_apply_in_a_writable_root_points_to_the_log()
    {
        var failed = await Service(new Setup(LocalPackage: Lying("0.9.1"))).FailedApplyAsync(true, "0.9.0");

        failed.Should().Be(new FailedApply("0.9.1", UpdateFailure.SeeLog));
    }

    [Fact]
    public async Task A_restart_after_a_good_update_is_no_failure()
    {
        var failed = await Service(new Setup(LocalPackage: Lying("0.9.0"))).FailedApplyAsync(true, "0.9.0");

        failed.Should().BeNull();
    }

    [Fact]
    public async Task Without_a_restart_nothing_is_looked_at()
    {
        var failed = await Service(new Setup(LocalPackage: Lying("0.9.1"))).FailedApplyAsync(restartedByUpdate: false, "0.9.0");

        failed.Should().BeNull();
        _managersMade.Should().Be(0);
    }

    [Fact]
    public async Task After_a_failed_apply_the_lying_package_is_ready_without_a_check()
    {
        // Retry applies it again: no check, no download.
        var service = Service(new Setup(LocalPackage: Lying("0.9.1")));
        await service.FailedApplyAsync(true, "0.9.0");

        var reason = await service.DownloadAsync("0.9.1", _ => { }, CancellationToken.None);

        reason.Should().BeNull();
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
