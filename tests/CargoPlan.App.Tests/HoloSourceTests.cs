using System.Net;
using System.Security.Cryptography;
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

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeHandler _handler = new();

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string CachedFile(string slug) => Path.Combine(_folder, slug + ".holo");

    private string CachedManifest => Path.Combine(_folder, "manifest.json");

    private HoloSource Source(TimeSpan? timeout = null) => new(_handler, _folder, new FixedClock(Now), timeout ?? TimeSpan.FromSeconds(5), "1.2.3");

    private static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    private static byte[] Manifest(params (string Slug, byte[] Data)[] ships) => Encoding.UTF8.GetBytes($$"""
        { "dataVersion": "holo-v1", "formatVersion": 2, "ships": [ {{string.Join(", ", ships.Select(s =>
            $$"""{ "slug": "{{s.Slug}}", "file": "{{s.Slug}}.holo", "bytes": {{s.Data.Length}}, "sha256": "{{Hash(s.Data)}}", "budget": 20000 }"""))}} ] }
        """);

    private void Cache(string path, byte[] data, TimeSpan age)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(path, data);
        File.SetLastWriteTimeUtc(path, (Now - age).UtcDateTime);
    }

    private void Serve(string file, byte[] data) => _handler.Answer(file, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });

    // --- Download ---

    [Fact]
    public async Task Fetches_manifest_and_file_from_the_release_and_caches_both()
    {
        Serve("manifest.json", Manifest((Slug, File1)));
        Serve(Slug + ".holo", File1);

        var result = await Source().GetAsync(Slug, download: true, CancellationToken.None);

        result.Should().BeOfType<HoloFetch.Found>().Which.Data.Should().Equal(File1);
        _handler.Requests.Select(r => r.Uri).Should().Equal(
            "https://github.com/MRiediger/sc-cargoplan/releases/download/holo-v1/manifest.json",
            "https://github.com/MRiediger/sc-cargoplan/releases/download/holo-v1/misc-hull-a.holo");
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
        Serve("manifest.json", Encoding.UTF8.GetBytes("{ \"dataVersion\": \"holo-v2\", \"formatVersion\": 2, \"ships\": [] }"));

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

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Answers per file name; logs every request. Unknown files get a 404.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> _answers = [];

        public List<(string Uri, string Method, string UserAgent)> Requests { get; } = [];

        public void Answer(string file, Func<HttpResponseMessage> answer) => _answers[file] = _ => Task.FromResult(answer());

        public void Hang(string file) => _answers[file] = async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("unreachable");
        };

        public void Fail(string file) => _answers[file] = _ => throw new HttpRequestException("No such host is known.");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add((uri, request.Method.Method, request.Headers.UserAgent.ToString()));
            return _answers.TryGetValue(uri[(uri.LastIndexOf('/') + 1)..], out var answer)
                ? answer(cancellationToken)
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
