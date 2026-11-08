using System.Net;
using System.Text;
using CargoPlan.App.Holo;

namespace CargoPlan.App.Tests;

/// <summary>
/// The download and cache rules of D63 without a network: a fake handler answers, a fixed clock ages the cached
/// manifest. A fresh temp folder per test, as in <see cref="PlanStoreTests"/>.
/// </summary>
public sealed class HoloSourceTests : IDisposable
{
    private const string Slug = "misc-hull-a";
    private const string OtherSlug = "drak-caterpillar";
    private static readonly byte[] File1 = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] File2 = [9, 9, 9];
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    // The cache root of this test alone: retiring other tags looks at the root's folders (D95).
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeHttp.FakeHandler _handler = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string TagFolder => Path.Combine(_root, HoloRelease.Tag);

    private string OldTag => Path.Combine(_root, "holo-v1");

    private string CachedFile(string slug) => Path.Combine(TagFolder, slug + ".holo");

    private string CachedManifest => Path.Combine(TagFolder, "manifest.json");

    private HoloSource Source(TimeSpan? timeout = null) => new(_handler, _root, new FakeHttp.FixedClock(Now), timeout ?? TimeSpan.FromSeconds(5), "1.2.3");

    /// <summary>The cache of v0.2.0: holo\holo-v1 with a file in it.</summary>
    private void OldCache()
    {
        Directory.CreateDirectory(OldTag);
        File.WriteAllBytes(Path.Combine(OldTag, Slug + ".holo"), File1);
    }

    private static byte[] Manifest(params (string Slug, byte[] Data)[] ships) => FakeHttp.Manifest(ships);

    private void Cache(string path, byte[] data, TimeSpan age)
    {
        Directory.CreateDirectory(TagFolder);
        File.WriteAllBytes(path, data);
        File.SetLastWriteTimeUtc(path, (Now - age).UtcDateTime);
    }

    private void Serve(string file, byte[] data) => _handler.Serve(file, data);

    // --- Download ---

    [Fact]
    public async Task Fetches_manifest_and_file_from_the_release_and_caches_both()
    {
        Serve("manifest.json", Manifest((Slug, File1)));
        Serve(Slug + ".holo", File1);

        var result = await Source().GetAsync(Slug, download: true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File1);
        _handler.Requests.Select(r => r.Uri).Should().Equal(
            "https://github.com/MRiediger/sc-cargoplan/releases/download/holo-v2/manifest.json",
            "https://github.com/MRiediger/sc-cargoplan/releases/download/holo-v2/misc-hull-a.holo");
        _handler.Requests.Should().OnlyContain(r => r.UserAgent == "SC-CargoPlan/1.2.3" && r.Method == "GET");
        File.ReadAllBytes(CachedFile(Slug)).Should().Equal(File1);
        File.Exists(CachedManifest).Should().BeTrue();
    }

    [Fact]
    public async Task A_second_ship_in_the_same_run_fetches_no_second_manifest()
    {
        Serve("manifest.json", Manifest((Slug, File1), (OtherSlug, File2)));
        Serve(Slug + ".holo", File1);
        Serve(OtherSlug + ".holo", File2);
        var source = Source();

        await source.GetAsync(Slug, true, CancellationToken.None);
        var second = await source.GetAsync(OtherSlug, true, CancellationToken.None);

        second.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File2);
        _handler.Requests.Count(r => r.Uri.EndsWith("manifest.json")).Should().Be(1);
    }

    [Fact]
    public async Task A_cached_manifest_younger_than_24_hours_needs_no_request()
    {
        Cache(CachedManifest, Manifest((Slug, File1)), TimeSpan.FromHours(23));
        Serve(Slug + ".holo", File1);

        await Source().GetAsync(Slug, true, CancellationToken.None);

        _handler.Requests.Select(r => r.Uri).Should().ContainSingle().Which.Should().EndWith(Slug + ".holo");
    }

    [Fact]
    public async Task A_cached_manifest_older_than_24_hours_is_fetched_again()
    {
        Cache(CachedManifest, Manifest((Slug, File1)), TimeSpan.FromHours(25));
        Cache(CachedFile(Slug), File1, TimeSpan.FromHours(25));
        Serve("manifest.json", Manifest((Slug, File1)));

        await Source().GetAsync(Slug, true, CancellationToken.None);

        _handler.Requests.Select(r => r.Uri).Should().ContainSingle().Which.Should().EndWith("manifest.json");
    }

    [Fact]
    public async Task A_cached_file_with_the_manifests_hash_needs_no_download()
    {
        Serve("manifest.json", Manifest((Slug, File1)));
        Cache(CachedFile(Slug), File1, TimeSpan.FromDays(30));

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File1);
        _handler.Requests.Should().NotContain(r => r.Uri.EndsWith(".holo"));
    }

    [Fact]
    public async Task A_cached_file_with_another_hash_is_downloaded_again()
    {
        Serve("manifest.json", Manifest((Slug, File2)));
        Serve(Slug + ".holo", File2);
        Cache(CachedFile(Slug), File1, TimeSpan.FromDays(1));

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File2);
        File.ReadAllBytes(CachedFile(Slug)).Should().Equal(File2);
    }

    // --- Failures: each ends with a reason, the cache stays where the rule says so ---

    [Fact]
    public async Task A_partial_answer_fails_the_hash_and_writes_nothing()
    {
        Serve("manifest.json", Manifest((Slug, File1)));
        _handler.Answer(Slug + ".holo", () => new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(File1[..4]) });

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>().Which.Reason.Should().Contain("damaged");
        File.Exists(CachedFile(Slug)).Should().BeFalse();
    }

    [Fact]
    public async Task A_wrong_hash_removes_the_file()
    {
        Serve("manifest.json", Manifest((Slug, File1)));
        Serve(Slug + ".holo", [8, 7, 6, 5, 4, 3, 2, 1]);
        Cache(CachedFile(Slug), File2, TimeSpan.FromDays(1));

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>();
        File.Exists(CachedFile(Slug)).Should().BeFalse();
    }

    [Fact]
    public async Task A_404_for_the_file_names_the_status()
    {
        Serve("manifest.json", Manifest((Slug, File1)));
        _handler.Answer(Slug + ".holo", () => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>().Which.Reason.Should().Contain("404");
    }

    [Fact]
    public async Task A_download_without_an_answer_times_out_with_a_reason()
    {
        Serve("manifest.json", Manifest((Slug, File1)));
        _handler.Hang(Slug + ".holo");

        var result = await Source(timeout: TimeSpan.FromMilliseconds(50)).GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>().Which.Reason.Should().Contain("no answer");
    }

    [Fact]
    public async Task An_unreachable_manifest_falls_back_to_the_old_one_and_the_cached_file()
    {
        _handler.Hang("manifest.json");
        Cache(CachedManifest, Manifest((Slug, File1)), TimeSpan.FromDays(3));
        Cache(CachedFile(Slug), File1, TimeSpan.FromDays(3));

        var result = await Source(timeout: TimeSpan.FromMilliseconds(50)).GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File1);
    }

    [Fact]
    public async Task An_old_manifest_without_the_slug_withdraws_nothing()
    {
        _handler.Hang("manifest.json");
        Cache(CachedManifest, Manifest(), TimeSpan.FromDays(3));
        Cache(CachedFile(Slug), File1, TimeSpan.FromDays(3));

        var result = await Source(timeout: TimeSpan.FromMilliseconds(50)).GetAsync(Slug, true, CancellationToken.None);

        // Only a manifest fetched now, or within 24 hours, withdraws; this one shows the outline and leaves the file.
        result.Should().BeOfType<HoloFetch.Missing>();
        File.Exists(CachedFile(Slug)).Should().BeTrue();
    }

    [Fact]
    public async Task Offline_without_any_manifest_shows_a_cached_file()
    {
        _handler.Fail("manifest.json");
        Cache(CachedFile(Slug), File1, TimeSpan.FromDays(3));

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File1);
    }

    [Fact]
    public async Task Offline_without_manifest_and_without_cache_fails()
    {
        _handler.Fail("manifest.json");

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>().Which.Reason.Should().Contain("no connection");
    }

    [Fact]
    public async Task A_failed_manifest_fetch_is_not_tried_again_in_the_same_run()
    {
        _handler.Fail("manifest.json");
        var source = Source();

        await source.GetAsync(Slug, true, CancellationToken.None);
        await source.GetAsync(OtherSlug, true, CancellationToken.None);

        _handler.Requests.Count(r => r.Uri.EndsWith("manifest.json")).Should().Be(1);
    }

    [Fact]
    public async Task An_invalid_manifest_fails_with_a_reason()
    {
        // holo-v1's manifest is valid for v0.1 and v0.2, but not for this app (D93).
        Serve("manifest.json", Encoding.UTF8.GetBytes("{ \"dataVersion\": \"holo-v1\", \"formatVersion\": 2, \"ships\": [] }"));

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>().Which.Reason.Should().Contain("manifest");
    }

    // --- Withdrawal and the switch ---

    [Fact]
    public async Task A_slug_missing_from_a_fetched_manifest_withdraws_the_cached_file()
    {
        Serve("manifest.json", Manifest((OtherSlug, File2)));
        Cache(CachedFile(Slug), File1, TimeSpan.FromDays(3));

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Missing>();
        File.Exists(CachedFile(Slug)).Should().BeFalse();
    }

    [Fact]
    public async Task An_empty_manifest_withdraws_every_hologram()
    {
        Serve("manifest.json", Manifest());
        Cache(CachedFile(Slug), File1, TimeSpan.FromDays(3));

        (await Source().GetAsync(Slug, true, CancellationToken.None)).Should().BeOfType<HoloFetch.Missing>();
        File.Exists(CachedFile(Slug)).Should().BeFalse();
    }

    [Fact]
    public async Task Downloads_off_reads_the_cache_and_sends_nothing()
    {
        Cache(CachedFile(Slug), File1, TimeSpan.FromDays(300));
        var source = Source();

        var cached = await source.GetAsync(Slug, download: false, CancellationToken.None);
        var notCached = await source.GetAsync(OtherSlug, download: false, CancellationToken.None);

        cached.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File1);
        notCached.Should().BeOfType<HoloFetch.Missing>();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_cancelled_request_throws_and_leaves_the_manifest_for_the_next_one()
    {
        _handler.Hang("manifest.json");
        var source = Source();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var first = () => source.GetAsync(Slug, true, cancel.Token);

        await first.Should().ThrowAsync<OperationCanceledException>();
        Serve("manifest.json", Manifest((Slug, File1)));
        Serve(Slug + ".holo", File1);
        (await source.GetAsync(Slug, true, CancellationToken.None)).Should().BeOfType<HoloFetch.Found>();
    }

    // --- Tag change (D95): other tags' folders go once the current tag's manifest is checked, never the current one ---

    [Fact]
    public async Task Another_tag_goes_once_the_manifest_is_fetched_and_the_current_folder_stays()
    {
        OldCache();
        Cache(CachedFile(OtherSlug), File2, TimeSpan.FromDays(3));
        Serve("manifest.json", Manifest((Slug, File1), (OtherSlug, File2)));
        Serve(Slug + ".holo", File1);
        var source = Source();
        var changes = new List<HoloTagChange>();
        source.TagChanged += changes.Add;

        var result = await source.GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Found>();
        Directory.Exists(OldTag).Should().BeFalse();
        changes.Should().Equal(new HoloTagChange(null));
        File.Exists(CachedFile(OtherSlug)).Should().BeTrue("the current tag's folder is never touched");
        File.Exists(CachedManifest).Should().BeTrue();
    }

    [Fact]
    public async Task A_cached_manifest_younger_than_24_hours_counts_as_checked()
    {
        OldCache();
        Cache(CachedManifest, Manifest((Slug, File1)), TimeSpan.FromHours(1));
        Serve(Slug + ".holo", File1);

        await Source().GetAsync(Slug, true, CancellationToken.None);

        Directory.Exists(OldTag).Should().BeFalse();
    }

    [Fact]
    public async Task An_unreachable_manifest_keeps_the_other_tag()
    {
        OldCache();
        _handler.Fail("manifest.json");
        var source = Source();
        var changes = new List<HoloTagChange>();
        source.TagChanged += changes.Add;

        await source.GetAsync(Slug, true, CancellationToken.None);

        Directory.Exists(OldTag).Should().BeTrue();
        changes.Should().BeEmpty();
    }

    [Fact]
    public async Task An_invalid_manifest_keeps_the_other_tag()
    {
        OldCache();
        Serve("manifest.json", Encoding.UTF8.GetBytes("{ \"dataVersion\": \"holo-v1\", \"formatVersion\": 2, \"ships\": [] }"));

        await Source().GetAsync(Slug, true, CancellationToken.None);

        Directory.Exists(OldTag).Should().BeTrue();
    }

    // Martin: a checked manifest is enough; a file that fails after it is tried again on the next start.
    [Fact]
    public async Task A_damaged_file_after_a_checked_manifest_still_retires_the_other_tag()
    {
        OldCache();
        Serve("manifest.json", Manifest((Slug, File1)));
        Serve(Slug + ".holo", [8, 7, 6, 5, 4, 3, 2, 1]);

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>();
        Directory.Exists(OldTag).Should().BeFalse();
    }

    // The notice must stand while the first file downloads, so the event comes before that request.
    [Fact]
    public async Task The_tag_change_is_told_once_per_run_before_the_first_download()
    {
        OldCache();
        Serve("manifest.json", Manifest((Slug, File1), (OtherSlug, File2)));
        Serve(Slug + ".holo", File1);
        Serve(OtherSlug + ".holo", File2);
        var source = Source();
        var requestsAtChange = new List<int>();
        source.TagChanged += _ => requestsAtChange.Add(_handler.Requests.Count);

        await source.GetAsync(Slug, true, CancellationToken.None);
        await source.GetAsync(OtherSlug, true, CancellationToken.None);

        requestsAtChange.Should().Equal(1);
    }

    [Fact]
    public async Task Without_another_tag_at_the_start_nothing_is_told()
    {
        Serve("manifest.json", Manifest((Slug, File1)));
        Serve(Slug + ".holo", File1);
        var source = Source();
        var changes = new List<HoloTagChange>();
        source.TagChanged += changes.Add;

        await source.GetAsync(Slug, true, CancellationToken.None);

        changes.Should().BeEmpty();
    }

    [Fact]
    public async Task Downloads_off_tells_no_tag_change()
    {
        OldCache();
        var source = Source();
        var changes = new List<HoloTagChange>();
        source.TagChanged += changes.Add;

        await source.GetAsync(Slug, download: false, CancellationToken.None);

        changes.Should().BeEmpty();
        Directory.Exists(OldTag).Should().BeTrue("only RetireOtherTags or a checked manifest deletes");
    }

    [Fact]
    public void RetireOtherTags_deletes_at_once_without_a_request()
    {
        OldCache();

        var problem = Source().RetireOtherTags();

        problem.Should().BeNull();
        Directory.Exists(OldTag).Should().BeFalse();
        _handler.Requests.Should().BeEmpty();
    }

    // Retired at the start (switch off or no hologram look), the tag change is still told at the first checked manifest.
    [Fact]
    public async Task Retired_at_the_start_the_change_is_still_told_at_the_first_checked_manifest()
    {
        OldCache();
        Serve("manifest.json", Manifest((Slug, File1)));
        Serve(Slug + ".holo", File1);
        var source = Source();
        var changes = new List<HoloTagChange>();
        source.TagChanged += changes.Add;
        source.RetireOtherTags();

        await source.GetAsync(Slug, true, CancellationToken.None);

        changes.Should().Equal(new HoloTagChange(null));
    }

    [Fact]
    public void Only_folders_named_like_a_tag_are_deleted()
    {
        OldCache();
        Cache(CachedFile(Slug), File1, TimeSpan.FromDays(1));
        string[] kept = [Path.Combine(_root, "notes"), Path.Combine(_root, "holo-v2-backup"), Path.Combine(_root, "holo-vx")];
        foreach (var folder in kept)
        {
            Directory.CreateDirectory(folder);
        }

        Source().RetireOtherTags();

        Directory.Exists(OldTag).Should().BeFalse();
        kept.Should().OnlyContain(f => Directory.Exists(f));
        File.Exists(CachedFile(Slug)).Should().BeTrue();
    }

    [Fact]
    public void A_locked_file_gives_a_reason_instead_of_an_exception()
    {
        OldCache();
        using var locked = File.Open(Path.Combine(OldTag, Slug + ".holo"), FileMode.Open, FileAccess.Read, FileShare.None);

        var problem = Source().RetireOtherTags();

        problem.Should().Contain("the old cache can't be removed");
    }
}
