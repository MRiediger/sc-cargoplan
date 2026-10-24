// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using CargoPlan.Core.Holo;

namespace CargoPlan.App.Holo;

/// <summary>What <see cref="HoloSource.GetAsync"/> found for a ship.</summary>
public abstract record HoloFetch
{
    private HoloFetch()
    {
    }

    /// <summary>The file, checked against the manifest when one was there.</summary>
    /// <param name="Problem">The cache couldn't be written: the hologram shows, the notice says why it will be downloaded again.</param>
    public sealed record Found(byte[] Data, string? Problem = null) : HoloFetch;

    /// <summary>No hologram, and nothing went wrong: not offered, withdrawn, or downloads off without a cached file.</summary>
    public sealed record Missing : HoloFetch;

    /// <summary>Something went wrong; the reason goes into the one notice of the run.</summary>
    public sealed record Failed(string Reason) : HoloFetch;
}

/// <summary>
/// Gets a ship's hologram from the cache or the release (D63, docs/holo-format.md). The manifest is asked for at most
/// once per run, and not at all while the cached one is younger than <see cref="HoloRelease.ManifestMaxAge"/>; a
/// manifest fetched now, or within that age, withdraws a ship it doesn't list (the cached file is deleted), an older one
/// only stands in while the release can't be reached. A file is downloaded when the cache lacks it or its hash differs,
/// and kept only if length and SHA256 match. The network and file boundary: HTTP and IO errors end here, as a reason.
/// One request at a time; a cancelled call throws <see cref="OperationCanceledException"/>.
/// </summary>
public sealed class HoloSource : IDisposable
{
    // The largest file of holo-v1 is 799 KB, the manifest about 60 KB: more is not a hologram.
    private const long MaxResponseBytes = 4 * 1024 * 1024;

    private readonly HttpClient _client;
    private readonly string _folder;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    private HoloManifest? _manifest;

    // Fetched in this run or younger than the maximum age: it may withdraw a ship.
    private bool _manifestIsCurrent;

    // The one fetch of the run has been tried (a cancelled one doesn't count).
    private bool _manifestTried;

    // The reason the fetch failed, for a ship that has no cached file either.
    private string? _manifestProblem;

    // The manifest couldn't be cached; handed on with the next hologram found.
    private string? _writeProblem;

    /// <param name="handler">Sends the requests; a fake in the tests. Disposed with this source.</param>
    /// <param name="cacheFolder">Holds manifest.json and the &lt;slug&gt;.holo files.</param>
    /// <param name="version">For the User-Agent, "SC-CargoPlan/&lt;version&gt;".</param>
    public HoloSource(HttpMessageHandler handler, string cacheFolder, TimeProvider clock, TimeSpan timeout, string version)
    {
        // Source: https://learn.microsoft.com/dotnet/api/system.net.http.httpclient.maxresponsecontentbuffersize
        _client = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = HoloRelease.BaseUri,
            MaxResponseContentBufferSize = MaxResponseBytes,
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SC-CargoPlan", version));
        _folder = cacheFolder;
        _clock = clock;
        _timeout = timeout;
    }

    /// <summary>The app's source: the system's proxy settings, redirects followed (github.com sends the asset host).</summary>
    public static HoloSource CreateDefault() =>
        new(new SocketsHttpHandler(), HoloRelease.DefaultCacheFolder, TimeProvider.System, HoloRelease.Timeout, HoloRelease.UserAgentVersion);

    /// <param name="slug">The ship's <c>fleetyardsSlug</c>.</param>
    /// <param name="download">false = the switch is off: only a cached file, no request at all (Martin, T7.4c-2).</param>
    /// <exception cref="OperationCanceledException"><paramref name="cancel"/> was cancelled: another ship was chosen.</exception>
    public async Task<HoloFetch> GetAsync(string slug, bool download, CancellationToken cancel)
    {
        await _oneAtATime.WaitAsync(cancel);
        try
        {
            if (!download)
            {
                return ReadCached(slug) is { } offline ? new HoloFetch.Found(offline) : new HoloFetch.Missing();
            }

            if (await ManifestAsync(cancel) is not { } manifest)
            {
                return ReadCached(slug) is { } cached ? new HoloFetch.Found(cached) : new HoloFetch.Failed(_manifestProblem ?? "no manifest");
            }

            if (manifest.Find(slug) is not { } entry)
            {
                return _manifestIsCurrent && Delete(slug) is { } problem ? new HoloFetch.Failed(problem) : new HoloFetch.Missing();
            }

            var found = ReadCached(slug) is { } current && Matches(current, entry) ? new HoloFetch.Found(current) : await DownloadAsync(slug, entry, cancel);
            if (found is HoloFetch.Found { Problem: null } shown && _writeProblem is { } writeProblem)
            {
                _writeProblem = null;
                return shown with { Problem = writeProblem };
            }

            return found;
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _oneAtATime.Dispose();
    }

    private async Task<HoloFetch> DownloadAsync(string slug, HoloManifestEntry entry, CancellationToken cancel)
    {
        var (data, problem) = await FetchAsync(entry.File, cancel);
        if (data is null)
        {
            return new HoloFetch.Failed(problem!);
        }

        if (!Matches(data, entry))
        {
            // A stale copy goes too: it would only be downloaded again.
            const string damaged = "the download was damaged (length or SHA256 differs from the manifest)";
            return new HoloFetch.Failed(Delete(slug) is { } notDeleted ? $"{damaged}; {notDeleted}" : damaged);
        }

        return new HoloFetch.Found(data, Write(slug + ".holo", data));
    }

    private async Task<HoloManifest?> ManifestAsync(CancellationToken cancel)
    {
        if (_manifest is not null && (_manifestIsCurrent || _manifestTried))
        {
            return _manifest;
        }

        var path = CachePath(HoloRelease.ManifestFile);
        if (!_manifestTried)
        {
            if (File.Exists(path) && _clock.GetUtcNow() - File.GetLastWriteTimeUtc(path) < HoloRelease.ManifestMaxAge && ParseCached(path) is { } young)
            {
                (_manifest, _manifestIsCurrent) = (young, true);
                return young;
            }

            var (data, problem) = await FetchAsync(HoloRelease.ManifestFile, cancel);
            _manifestTried = true;
            if (data is not null)
            {
                try
                {
                    (_manifest, _manifestIsCurrent) = (HoloManifestReader.Read(data, HoloRelease.Tag), true);
                    _writeProblem = Write(HoloRelease.ManifestFile, data);
                    return _manifest;
                }
                catch (HoloFormatException ex)
                {
                    problem = $"the manifest is invalid ({ex.Message})";
                }
            }

            _manifestProblem = problem;
        }

        // The release can't be reached or answered nonsense: an older manifest stands in, withdrawing nothing.
        (_manifest, _manifestIsCurrent) = (File.Exists(path) ? ParseCached(path) : null, false);
        return _manifest;
    }

    /// <summary>One GET with the timeout; the bytes, or null and the reason. Only a cancel by the caller throws.</summary>
    private async Task<(byte[]? Data, string? Problem)> FetchAsync(string file, CancellationToken cancel)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(_timeout);
        try
        {
            using var response = await _client.GetAsync(file, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return (null, $"github.com answered {(int)response.StatusCode} for {file}");
            }

            return (await response.Content.ReadAsByteArrayAsync(timeout.Token), null);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return (null, $"no answer from github.com within {_timeout.TotalSeconds:0.#} s");
        }
        catch (HttpRequestException ex)
        {
            return (null, $"no connection to github.com ({ex.Message})");
        }
    }

    private HoloManifest? ParseCached(string path)
    {
        try
        {
            return HoloManifestReader.Read(File.ReadAllBytes(path), HoloRelease.Tag);
        }
        catch (Exception ex) when (ex is HoloFormatException or IOException or UnauthorizedAccessException)
        {
            // A broken cached manifest is as good as none: the fetch replaces it.
            return null;
        }
    }

    private byte[]? ReadCached(string slug)
    {
        var path = CachePath(slug + ".holo");
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked or unreadable: treated as not cached, so a download replaces it, or the reason shows with downloads off.
            return null;
        }
    }

    /// <summary>Removes a cached file; the reason if it can't, null otherwise.</summary>
    private string? Delete(string slug)
    {
        try
        {
            File.Delete(CachePath(slug + ".holo"));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"the cached file can't be deleted ({ex.Message})";
        }
    }

    /// <summary>Caches a downloaded file (<see cref="AtomicFile"/>); the reason if it can't, null otherwise.</summary>
    private string? Write(string file, byte[] data)
    {
        try
        {
            AtomicFile.Write(CachePath(file), stream => stream.Write(data));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"the cache can't be written ({ex.Message})";
        }
    }

    private static bool Matches(byte[] data, HoloManifestEntry entry) =>
        data.Length == entry.Bytes && string.Equals(Convert.ToHexStringLower(SHA256.HashData(data)), entry.Sha256, StringComparison.OrdinalIgnoreCase);

    private string CachePath(string file) => Path.Combine(_folder, file);
}
