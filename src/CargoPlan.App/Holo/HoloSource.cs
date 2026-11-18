// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using CargoPlan.App.ShipData;
using CargoPlan.Core.Holo;
using CargoPlan.Core.ShipData;
using static System.FormattableString;

namespace CargoPlan.App.Holo;

/// <summary>What <see cref="HoloSource.GetAsync"/> found for a ship.</summary>
public abstract record HoloFetch
{
    private HoloFetch()
    {
    }

    /// <summary>The file, checked against the manifest.</summary>
    /// <param name="Problem">The cache couldn't be written: the hologram shows, the notice says why it will be downloaded again.</param>
    public sealed record Found(byte[] Data, string? Problem = null) : HoloFetch;

    /// <summary>No hologram, and nothing the player must hear about: none offered, a release that is gone, or downloads off.</summary>
    public sealed record Missing : HoloFetch;

    /// <summary>Something went wrong; the reason goes into the one notice of the run.</summary>
    public sealed record Failed(string Reason) : HoloFetch;
}

/// <summary>The cache held another tag or data version at the start and the first request with downloads on came (D95).</summary>
/// <param name="Problem">Why the other folder couldn't be deleted; null when it is gone.</param>
public sealed record HoloTagChange(string? Problem);

/// <summary>
/// Network access 1 (D63, D114): a ship's hologram from the cache or from the release of the ship data in use,
/// <c>…/releases/download/&lt;dataVersion&gt;/&lt;file&gt;</c>. The list, lengths and hashes come from that data's manifest,
/// read with it at the start; nothing else is asked for. A file is downloaded when the cache lacks it or its hash differs,
/// and kept only if length and SHA256 match. A 404 is a release that is gone (only the last two stay): the outline, one
/// log line per run, the ship isn't asked for again in the run. The network and file boundary: HTTP and IO errors end here,
/// as a reason. One request at a time; a cancelled call throws <see cref="OperationCanceledException"/>. Only this data
/// version's cache stays (D95): the folder of another tag or data version found at the start goes at the first request with
/// downloads on, before any download, and <see cref="TagChanged"/> tells it.
/// </summary>
public sealed class HoloSource : IDisposable
{
    /// <summary>Per request; a hanging connection must not keep the outline from the player for long.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // The largest file of ships-2026.11.1 is 661 KB: more is not a hologram.
    private const long MaxResponseBytes = 4 * 1024 * 1024;

    // The cache folders of the hologram releases up to v0.5.x were named "holo-v<n>" (D63); they are all another tag's now.
    private const string TagPrefix = "holo-v";

    private const string LogContext = "Holo";

    private readonly HttpClient _client;
    private readonly string _root;
    private readonly string _folder;
    private readonly string _dataVersion;
    private readonly HoloManifest? _manifest;
    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    // Slugs whose file the release answered 404 for in this run: not asked for again (Festlegung 5 of T12.4).
    private readonly HashSet<string> _gone = new(StringComparer.Ordinal);

    // The first 404 of the run is logged, the rest isn't.
    private bool _goneLogged;

    // Another folder lay in the root at the start; the first request with downloads on retires it and tells so (D95).
    private bool _tagChangePending;

    /// <param name="handler">Sends the requests; a fake in the tests. Disposed with this source.</param>
    /// <param name="cacheRoot">Holds a folder per data version, each with the &lt;slug&gt;.holo files.</param>
    /// <param name="repository">The data release's repository (<see cref="ShipDataSource.RepositoryFor"/>).</param>
    /// <param name="data">The ship data in use: its tag names the release and the cache folder, its manifest the files.</param>
    /// <param name="version">For the User-Agent, "SC-CargoPlan/&lt;version&gt;".</param>
    public HoloSource(HttpMessageHandler handler, string cacheRoot, Uri repository, ActiveShipData data, TimeSpan timeout, string version)
    {
        // Source: https://learn.microsoft.com/dotnet/api/system.net.http.httpclient.maxresponsecontentbuffersize
        _client = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = ShipDataSource.ReleaseFolder(repository, data.Version),
            MaxResponseContentBufferSize = MaxResponseBytes,
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SC-CargoPlan", version));
        _root = cacheRoot;
        _dataVersion = data.Version.ToString();
        _folder = CacheFolder(cacheRoot, data.Version);
        _manifest = data.Holo is HoloOffer.Offered offered ? offered.Manifest : null;
        _timeout = timeout;
        _tagChangePending = HasOtherTags();
    }

    /// <summary>Raised once per run, on the caller's thread, at the first request with downloads on (D95).</summary>
    public event Action<HoloTagChange>? TagChanged;

    /// <summary>%LocalAppData%\SC-CargoPlan\holo, next to settings.json: one folder per data version, only the one in use stays (D95).</summary>
    public static string DefaultCacheRoot => Path.Combine(AppFolders.LocalRoot, "holo");

    /// <summary>Whether the ship data in use offer holograms in a format this app reads.</summary>
    public bool OffersHolograms => _manifest is not null;

    /// <summary>The cache of a data version: <c>holo\ships-2026.11.1\</c>.</summary>
    public static string CacheFolder(string cacheRoot, ShipDataVersion version) => Path.Combine(cacheRoot, version.ToString());

    /// <summary>The app's source: the system's proxy settings, redirects followed (github.com sends the asset host).</summary>
    /// <param name="shipDataSource"><c>--ship-data-source</c>: its repository's holograms (R2 of T12.4); a folder or none gives the data channel's.</param>
    public static HoloSource CreateDefault(ActiveShipData data, string? shipDataSource) =>
        new(new SocketsHttpHandler(), DefaultCacheRoot, ShipDataSource.RepositoryFor(shipDataSource), data, Timeout, AppVersion.Current);

    /// <summary>The start's log line when the data in use offer no holograms this app reads; null when they do.</summary>
    public static string? UnavailableLog(ActiveShipData data) => data.Holo switch
    {
        HoloOffer.NotOffered => $"ship data {data.Version.Display} offers no holograms (holo: null) – outlines only",
        HoloOffer.UnknownFormat unknown => Invariant(
            $"ship data {data.Version.Display} has holograms in format {unknown.FormatVersion}, this app reads {HoloReader.FormatVersion} – outlines only"),
        _ => null,
    };

    /// <summary>The line under the switch "Download ship holograms" when no holograms come (R3 of T12.4); null when they do.</summary>
    public static string? UnavailableText(ActiveShipData data) => data.Holo switch
    {
        HoloOffer.NotOffered => $"Holograms: not offered by ship data {data.Version.Display}",
        HoloOffer.UnknownFormat unknown => Invariant($"Holograms: ship data {data.Version.Display} needs a newer app (hologram format {unknown.FormatVersion})"),
        _ => null,
    };

    /// <summary>
    /// Deletes the cache folders of other tags and data versions (D95): only folders directly in the root named like a
    /// hologram tag ("holo-v" and digits) or a data version, never the one in use. The reason if one can't be deleted,
    /// null otherwise.
    /// </summary>
    public string? RetireOtherTags()
    {
        try
        {
            foreach (var folder in OtherTags())
            {
                Directory.Delete(folder, recursive: true);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var problem = $"the old cache can't be removed ({ex.Message})";
            AppLog.Write(LogContext, $"{problem} [{ex.GetType().Name}]");
            return problem;
        }
    }

    /// <param name="slug">The ship's <c>modelKey</c>.</param>
    /// <param name="download">false = the switch is off: only a cached file, no request at all (Martin, T7.4c-2).</param>
    /// <exception cref="OperationCanceledException"><paramref name="cancel"/> was cancelled: another ship was chosen.</exception>
    public async Task<HoloFetch> GetAsync(string slug, bool download, CancellationToken cancel)
    {
        await _oneAtATime.WaitAsync(cancel);
        try
        {
            if (_manifest is not { } manifest)
            {
                return new HoloFetch.Missing();
            }

            if (download)
            {
                RetireAfterCheck();
            }

            if (manifest.Find(slug) is not { } entry)
            {
                return new HoloFetch.Missing();
            }

            if (ReadCached(slug) is { } cached && Matches(cached, entry))
            {
                return new HoloFetch.Found(cached);
            }

            return !download || _gone.Contains(slug) ? new HoloFetch.Missing() : await DownloadAsync(slug, entry, cancel);
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

    /// <summary>"holo-v" and digits, or a data version other than the one in use; Windows names ignore case.</summary>
    private bool IsOtherTag(string name) =>
        (name.Length > TagPrefix.Length && name.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase) && name[TagPrefix.Length..].All(char.IsAsciiDigit))
        || (ShipDataVersion.TryParse(name, out _) && !string.Equals(name, _dataVersion, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<string> OtherTags() =>
        Directory.Exists(_root) ? [.. Directory.EnumerateDirectories(_root).Where(d => IsOtherTag(Path.GetFileName(d)))] : [];

    /// <summary>Whether another folder lies in the root at the start.</summary>
    private bool HasOtherTags()
    {
        try
        {
            return OtherTags().Count > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable root holds nothing to retire later; RetireOtherTags at the start names it.
            return false;
        }
    }

    /// <summary>The first request with downloads on: another folder found at the start goes now, and the loader is told once.</summary>
    private void RetireAfterCheck()
    {
        if (_tagChangePending)
        {
            _tagChangePending = false;

            // First, not inside ?.Invoke(...): without a subscriber that would skip its argument, and the folder would stay.
            var problem = RetireOtherTags();
            TagChanged?.Invoke(new HoloTagChange(problem));
        }
    }

    private async Task<HoloFetch> DownloadAsync(string slug, HoloManifestEntry entry, CancellationToken cancel)
    {
        var (data, problem, gone) = await FetchAsync(entry.File, cancel);
        if (gone)
        {
            _gone.Add(slug);
            if (!_goneLogged)
            {
                _goneLogged = true;
                AppLog.Write(LogContext, $"{entry.File} is not in release {_dataVersion} (404): the outline; later 404s of this run aren't logged");
            }

            return new HoloFetch.Missing();
        }

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

    /// <summary>One GET with the timeout; the bytes, or null and the reason, or that the release has no such file. Only a cancel by the caller throws.</summary>
    private async Task<(byte[]? Data, string? Problem, bool Gone)> FetchAsync(string file, CancellationToken cancel)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(_timeout);
        try
        {
            using var response = await _client.GetAsync(file, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (null, null, true);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (null, $"github.com answered {(int)response.StatusCode} for {file}", false);
            }

            return (await response.Content.ReadAsByteArrayAsync(timeout.Token), null, false);
        }
        catch (OperationCanceledException ex) when (!cancel.IsCancellationRequested)
        {
            var problem = $"no answer from github.com within {_timeout.TotalSeconds:0.#} s";
            AppLog.Write(LogContext, $"{problem} [{ex.GetType().Name}]");
            return (null, problem, false);
        }
        catch (HttpRequestException ex)
        {
            var problem = $"no connection to github.com ({ex.Message})";
            AppLog.Write(LogContext, $"{problem} [{ex.GetType().Name}]");
            return (null, problem, false);
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
            // Locked or unreadable: treated as not cached, so a download replaces it, or the outline shows with downloads off.
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
            var problem = $"the cached file can't be deleted ({ex.Message})";
            AppLog.Write(LogContext, $"{problem} [{ex.GetType().Name}]");
            return problem;
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
            var problem = $"the cache can't be written ({ex.Message})";
            AppLog.Write(LogContext, $"{problem} [{ex.GetType().Name}]");
            return problem;
        }
    }

    private static bool Matches(byte[] data, HoloManifestEntry entry) =>
        data.Length == entry.Bytes && string.Equals(Convert.ToHexStringLower(SHA256.HashData(data)), entry.Sha256, StringComparison.OrdinalIgnoreCase);

    private string CachePath(string file) => Path.Combine(_folder, file);
}
