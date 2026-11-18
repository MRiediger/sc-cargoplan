using System.Net;
using CargoPlan.App.Holo;
using CargoPlan.Core.Holo;

namespace CargoPlan.App.Tests;

/// <summary>
/// The download and cache rules of D63 and D114 without a network: the holograms come from the manifest of the ship data
/// in use, a fake handler answers for the files. A fresh temp folder per test, as in <see cref="PlanStoreTests"/>.
/// </summary>
public sealed class HoloSourceTests : IDisposable
{
    private const string Slug = "misc-hull-a";
    private const string OtherSlug = "drak-caterpillar";
    private const string Release = "https://github.com/MRiediger/sc-cargoplan-shipdata/releases/download/ships-2026.11.1/";
    private static readonly byte[] File1 = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] File2 = [9, 9, 9];

    // The cache root of this test alone: retiring other folders looks at the root's folders (D95).
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeHttp.FakeHandler _handler = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CurrentFolder => Path.Combine(_root, "ships-2026.11.1");

    private string OldTag => Path.Combine(_root, "holo-v3");

    private string CachedFile(string slug) => Path.Combine(CurrentFolder, slug + ".holo");

    private static HoloOffer Offer(params (string Slug, byte[] Data)[] ships) => FakeHttp.Holo(ships);

    private HoloSource Source(HoloOffer? holo = null, TimeSpan? timeout = null, string repository = "https://github.com/MRiediger/sc-cargoplan-shipdata/") =>
        new(_handler, _root, new Uri(repository), FakeHttp.Data(holo ?? Offer((Slug, File1), (OtherSlug, File2))), timeout ?? TimeSpan.FromSeconds(5), "1.2.3");

    /// <summary>The cache of v0.5.1: holo\holo-v3 with a file in it.</summary>
    private void OldCache()
    {
        Directory.CreateDirectory(OldTag);
        File.WriteAllBytes(Path.Combine(OldTag, Slug + ".holo"), File1);
    }

    private void Cache(string slug, byte[] data)
    {
        Directory.CreateDirectory(CurrentFolder);
        File.WriteAllBytes(CachedFile(slug), data);
    }

    private void Serve(string file, byte[] data) => _handler.Serve(file, data);

    // --- Download: one GET per file, from the release of the ship data in use; the manifest is local (D114) ---

    [Fact]
    public async Task Fetches_the_file_from_the_release_of_the_ship_data_in_use_and_caches_it_under_its_tag()
    {
        Serve(Slug + ".holo", File1);

        var result = await Source().GetAsync(Slug, download: true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File1);
        _handler.Requests.Select(r => r.Uri).Should().Equal(Release + "misc-hull-a.holo");
        _handler.Requests.Should().OnlyContain(r => r.UserAgent == "SC-CargoPlan/1.2.3" && r.Method == "GET");
        File.ReadAllBytes(CachedFile(Slug)).Should().Equal(File1);
        Directory.EnumerateFiles(CurrentFolder).Select(Path.GetFileName).Should().Equal("misc-hull-a.holo");
    }

    // Was A_second_ship_in_the_same_run_fetches_no_second_manifest: now there is no manifest request at all.
    [Fact]
    public async Task Each_ship_sends_only_its_own_file()
    {
        Serve(Slug + ".holo", File1);
        Serve(OtherSlug + ".holo", File2);
        var source = Source();

        await source.GetAsync(Slug, true, CancellationToken.None);
        var second = await source.GetAsync(OtherSlug, true, CancellationToken.None);

        second.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File2);
        _handler.Requests.Select(r => r.Uri).Should().Equal(Release + "misc-hull-a.holo", Release + "drak-caterpillar.holo");
    }

    // R2 of the T12.4 approval: --ship-data-source with another repository takes that repository's holograms too.
    [Fact]
    public async Task Another_repository_gives_the_holograms_of_its_own_release()
    {
        Serve(Slug + ".holo", File1);

        await Source(repository: "https://github.com/someone/fork/").GetAsync(Slug, true, CancellationToken.None);

        _handler.Requests.Select(r => r.Uri).Should().Equal("https://github.com/someone/fork/releases/download/ships-2026.11.1/misc-hull-a.holo");
    }

    [Fact]
    public async Task A_cached_file_with_the_manifests_hash_needs_no_download()
    {
        Cache(Slug, File1);

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File1);
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_cached_file_with_another_hash_is_downloaded_again()
    {
        Serve(Slug + ".holo", File2);
        Cache(Slug, File1);

        var result = await Source(Offer((Slug, File2))).GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File2);
        File.ReadAllBytes(CachedFile(Slug)).Should().Equal(File2);
    }

    // --- What the manifest offers (D63 kill switch, R1 unknown format, a ship without hologram) ---

    [Fact]
    public async Task The_kill_switch_sends_nothing_and_shows_no_cached_file()
    {
        Cache(Slug, File1);
        var source = Source(new HoloOffer.NotOffered());

        var result = await source.GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Missing>();
        source.OffersHolograms.Should().BeFalse();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_format_this_app_does_not_read_sends_nothing()
    {
        var source = Source(new HoloOffer.UnknownFormat(3));

        (await source.GetAsync(Slug, true, CancellationToken.None)).Should().BeOfType<HoloFetch.Missing>();
        source.OffersHolograms.Should().BeFalse();
        _handler.Requests.Should().BeEmpty();
    }

    // Was A_slug_missing_from_a_fetched_manifest_withdraws_the_cached_file and An_empty_manifest_withdraws_every_hologram:
    // the manifest stays for the run and its folder is new, so there is nothing to withdraw.
    [Fact]
    public async Task A_ship_the_manifest_does_not_list_shows_the_outline_without_a_request()
    {
        (await Source(Offer((OtherSlug, File2))).GetAsync(Slug, true, CancellationToken.None)).Should().BeOfType<HoloFetch.Missing>();
        (await Source(Offer()).GetAsync(OtherSlug, true, CancellationToken.None)).Should().BeOfType<HoloFetch.Missing>();
        _handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, "ship data 2026.11.1 offers no holograms (holo: null) – outlines only", "Holograms: not offered by ship data 2026.11.1")]
    [InlineData(3, "ship data 2026.11.1 has holograms in format 3, this app reads 2 – outlines only", "Holograms: ship data 2026.11.1 needs a newer app (hologram format 3)")]
    public void Without_holograms_on_offer_the_log_and_the_settings_say_why(int? format, string log, string settings)
    {
        var data = FakeHttp.Data(format is { } n ? new HoloOffer.UnknownFormat(n) : new HoloOffer.NotOffered());

        HoloSource.UnavailableLog(data).Should().Be(log);
        HoloSource.UnavailableText(data).Should().Be(settings);
    }

    [Fact]
    public void With_holograms_on_offer_there_is_nothing_to_say()
    {
        HoloSource.UnavailableLog(FakeHttp.Data(Offer())).Should().BeNull();
        HoloSource.UnavailableText(FakeHttp.Data(Offer())).Should().BeNull();
    }

    // --- Failures: each ends with a reason or the outline; the cache stays where the rule says so ---

    [Fact]
    public async Task A_partial_answer_fails_the_hash_and_writes_nothing()
    {
        _handler.Answer(Slug + ".holo", () => new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(File1[..4]) });

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>().Which.Reason.Should().Contain("damaged");
        File.Exists(CachedFile(Slug)).Should().BeFalse();
    }

    [Fact]
    public async Task A_wrong_hash_removes_the_file()
    {
        Serve(Slug + ".holo", [8, 7, 6, 5, 4, 3, 2, 1]);
        Cache(Slug, File2);

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>();
        File.Exists(CachedFile(Slug)).Should().BeFalse();
    }

    // Festlegung 5 of T12.4: a release that is gone (the last two stay) is no fault; the update line is the way back.
    [Fact]
    public async Task A_404_shows_the_outline_keeps_the_cached_file_and_is_not_asked_again_in_the_run()
    {
        Cache(Slug, File2);
        var source = Source();

        (await source.GetAsync(Slug, true, CancellationToken.None)).Should().BeOfType<HoloFetch.Missing>();
        (await source.GetAsync(Slug, true, CancellationToken.None)).Should().BeOfType<HoloFetch.Missing>();

        _handler.Requests.Should().ContainSingle();
        File.ReadAllBytes(CachedFile(Slug)).Should().Equal(File2);
    }

    [Fact]
    public async Task A_404_for_one_ship_leaves_the_others()
    {
        Serve(OtherSlug + ".holo", File2);
        var source = Source();

        (await source.GetAsync(Slug, true, CancellationToken.None)).Should().BeOfType<HoloFetch.Missing>();
        (await source.GetAsync(OtherSlug, true, CancellationToken.None)).Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File2);
    }

    // Was A_404_for_the_file_names_the_status: any other answer is a failure with its status.
    [Fact]
    public async Task Another_status_for_the_file_names_it()
    {
        _handler.Answer(Slug + ".holo", () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>().Which.Reason.Should().Contain("503");
    }

    [Fact]
    public async Task A_download_without_an_answer_times_out_with_a_reason()
    {
        _handler.Hang(Slug + ".holo");

        var result = await Source(timeout: TimeSpan.FromMilliseconds(50)).GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>().Which.Reason.Should().Contain("no answer");
    }

    // Was Offline_without_manifest_and_without_cache_fails.
    [Fact]
    public async Task Offline_without_a_cached_file_fails_with_a_reason()
    {
        _handler.Fail(Slug + ".holo");

        var result = await Source().GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Failed>().Which.Reason.Should().Contain("no connection");
    }

    // Was Offline_without_any_manifest_shows_a_cached_file: the manifest is always there now.
    [Fact]
    public async Task Offline_a_cached_file_shows_without_a_request()
    {
        _handler.Fail(Slug + ".holo");
        Cache(Slug, File1);

        (await Source().GetAsync(Slug, true, CancellationToken.None)).Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File1);
        _handler.Requests.Should().BeEmpty();
    }

    // --- The switch and cancelling ---

    [Fact]
    public async Task Downloads_off_shows_only_cached_files_that_match_the_manifest_and_sends_nothing()
    {
        Cache(Slug, File1);
        Cache(OtherSlug, File1);
        var source = Source();

        var cached = await source.GetAsync(Slug, download: false, CancellationToken.None);
        var stale = await source.GetAsync(OtherSlug, download: false, CancellationToken.None);

        cached.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File1);
        stale.Should().BeOfType<HoloFetch.Missing>();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_cancelled_request_throws_and_the_next_one_loads()
    {
        _handler.Hang(Slug + ".holo");
        var source = Source();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var first = () => source.GetAsync(Slug, true, cancel.Token);

        await first.Should().ThrowAsync<OperationCanceledException>();
        Serve(Slug + ".holo", File1);
        (await source.GetAsync(Slug, true, CancellationToken.None)).Should().BeOfType<HoloFetch.Found>();
    }

    // --- Cache change (D95): the folders of other tags and data versions go at the first request, never the current one ---

    [Fact]
    public async Task Other_tags_and_data_versions_go_at_the_first_request_and_the_current_folder_stays()
    {
        OldCache();
        var olderData = Path.Combine(_root, "ships-2026.11");
        Directory.CreateDirectory(olderData);
        Cache(OtherSlug, File2);
        Serve(Slug + ".holo", File1);
        var source = Source();
        var changes = new List<HoloTagChange>();
        source.TagChanged += changes.Add;

        var result = await source.GetAsync(Slug, true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Found>();
        Directory.Exists(OldTag).Should().BeFalse();
        Directory.Exists(olderData).Should().BeFalse();
        changes.Should().Equal(new HoloTagChange(null));
        File.Exists(CachedFile(OtherSlug)).Should().BeTrue("the current data version's folder is never touched");
    }

    // Was An_unreachable_manifest_keeps_the_other_tag: the manifest is local, so offline is checked too.
    [Fact]
    public async Task Offline_the_other_tag_goes_too()
    {
        OldCache();
        _handler.Fail(Slug + ".holo");
        var source = Source();
        var changes = new List<HoloTagChange>();
        source.TagChanged += changes.Add;

        await source.GetAsync(Slug, true, CancellationToken.None);

        Directory.Exists(OldTag).Should().BeFalse();
        changes.Should().Equal(new HoloTagChange(null));
    }

    [Fact]
    public async Task A_damaged_file_still_retires_the_other_tag()
    {
        OldCache();
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
        Serve(Slug + ".holo", File1);
        Serve(OtherSlug + ".holo", File2);
        var source = Source();
        var requestsAtChange = new List<int>();
        source.TagChanged += _ => requestsAtChange.Add(_handler.Requests.Count);

        await source.GetAsync(Slug, true, CancellationToken.None);
        await source.GetAsync(OtherSlug, true, CancellationToken.None);

        requestsAtChange.Should().Equal(0);
    }

    [Fact]
    public async Task Without_another_folder_at_the_start_nothing_is_told()
    {
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
        Directory.Exists(OldTag).Should().BeTrue("only RetireOtherTags or a request with downloads on deletes");
    }

    // Without holograms on offer the loader retires at the start, silently (HoloLoader.RetireUnneeded).
    [Fact]
    public async Task Without_holograms_on_offer_no_tag_change_is_told()
    {
        OldCache();
        var source = Source(new HoloOffer.NotOffered());
        var changes = new List<HoloTagChange>();
        source.TagChanged += changes.Add;

        await source.GetAsync(Slug, true, CancellationToken.None);

        changes.Should().BeEmpty();
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

    // Retired at the start (switch off or no hologram look), the change is still told at the first request.
    [Fact]
    public async Task Retired_at_the_start_the_change_is_still_told_at_the_first_request()
    {
        OldCache();
        Serve(Slug + ".holo", File1);
        var source = Source();
        var changes = new List<HoloTagChange>();
        source.TagChanged += changes.Add;
        source.RetireOtherTags();

        await source.GetAsync(Slug, true, CancellationToken.None);

        changes.Should().Equal(new HoloTagChange(null));
    }

    [Fact]
    public void Only_folders_named_like_a_tag_or_another_data_version_are_deleted()
    {
        OldCache();
        Cache(Slug, File1);
        string[] gone = [Path.Combine(_root, "holo-v1"), Path.Combine(_root, "ships-2026.11"), Path.Combine(_root, "ships-2026.12")];
        string[] kept = [Path.Combine(_root, "notes"), Path.Combine(_root, "holo-v2-backup"), Path.Combine(_root, "holo-vx"), Path.Combine(_root, "ships-x")];
        foreach (var folder in gone.Concat(kept))
        {
            Directory.CreateDirectory(folder);
        }

        Source().RetireOtherTags();

        Directory.Exists(OldTag).Should().BeFalse();
        gone.Should().OnlyContain(f => !Directory.Exists(f));
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

    [Fact]
    public void The_cache_of_a_data_version_is_named_after_its_tag() =>
        HoloSource.CacheFolder(_root, FakeHttp.DataVersion).Should().Be(CurrentFolder);
}
