using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using CargoPlan.App.Settings;
using CargoPlan.App.Updates;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.ShipData;

namespace CargoPlan.App.ShipData;

/// <summary>Check now's result: what it found, and its status line in the Ship data section.</summary>
public sealed record ShipDataCheckNow(ShipDataVerdict? Verdict, string Status);

/// <summary>
/// Network access 3 (T12.3, D112): once per start, after the app's update check, one GET of the latest data release's
/// manifest.json from <c>MRiediger/sc-cargoplan-shipdata</c>; on the player's click one GET of that release's
/// <c>ships.zip</c>, installed by <see cref="ShipDataStore"/>. Nothing else is sent; the User-Agent names the app and its
/// version. <c>--ship-data-source</c> (P5) puts a local folder or another repository in its place.
/// </summary>
public sealed class ShipDataSource : IDisposable
{
    /// <summary>The data channel's repository (D111); its releases are <c>ships-YYYY.MM[.N]</c>.</summary>
    public static readonly Uri Repository = new("https://github.com/MRiediger/sc-cargoplan-shipdata/");

    /// <summary>The manifest is about 130 KB; as long as the app's update check (D109).</summary>
    public static readonly TimeSpan ManifestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>ships.zip is about 100 KB; slow lines get more than the manifest.</summary>
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The zip or a file in it doesn't match the manifest: nothing installed (the store's check).</summary>
    public const string Damaged = "download damaged";

    /// <summary>Check now in a developer's build without <c>--ship-data-source</c> (P4).</summary>
    public const string DevBuildText = "Dev build – ship data checks need --ship-data-source";

    // Twice the largest file we expect (manifest, zip) times eight: more is not a data release.
    private const long MaxResponseBytes = 16 * 1024 * 1024;

    private const string LogContext = "Ship data";

    private const string ManualPrefix = "check now: ";

    private readonly HttpClient _client;
    private readonly ShipDataStore _store;
    private readonly string? _folder;
    private readonly Uri _repository;
    private readonly TimeSpan _manifestTimeout;
    private readonly TimeSpan _downloadTimeout;
    private readonly Lock _gate = new();

    // The last release a check offered, with the manifest as it came: the download installs exactly that one.
    private (ShipDataManifest Manifest, byte[] Json)? _offered;

    private int _started;

    /// <param name="handler">Sends the requests; a fake in the tests. Disposed with this source.</param>
    /// <param name="source"><c>--ship-data-source</c>: a full folder path or a GitHub repository URL; null = <see cref="Repository"/>.</param>
    /// <param name="version">For the User-Agent, "SC-CargoPlan/&lt;version&gt;".</param>
    public ShipDataSource(HttpMessageHandler handler, ShipDataStore store, string? source, TimeSpan manifestTimeout, TimeSpan downloadTimeout, string version)
    {
        // Redirects are followed by default (github.com sends the latest tag, then the asset host; measured in T12.3).
        // Source: https://learn.microsoft.com/dotnet/api/system.net.http.httpclient.maxresponsecontentbuffersize
        _client = new HttpClient(handler, disposeHandler: true)
        {
            MaxResponseContentBufferSize = MaxResponseBytes,
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SC-CargoPlan", version));
        _store = store;
        HasSource = source is not null;
        _repository = RepositoryFor(source);
        _folder = source is not null && RepositoryUri(source) is null ? source : null;
        _manifestTimeout = manifestTimeout;
        _downloadTimeout = downloadTimeout;
    }

    /// <summary>The line's words (T12.3): "Ship data update available · 2026.11.1", versions compared as release tags.</summary>
    public static UpdateLineTexts LineTexts { get; } = new(
        tag => $"Ship data update available · {Display(tag)}",
        (_, _) => "Downloading ship data",
        _ => "Ship data updated · restarting",
        (_, reason) => $"Ship data update failed · {reason}",
        (tag, than) => ShipDataVersion.TryParse(tag, out var a) && ShipDataVersion.TryParse(than, out var b) && a > b);

    /// <summary>Whether <c>--ship-data-source</c> came: a dev build checks only then (P4).</summary>
    public bool HasSource { get; }

    /// <summary>The release installed in this run; the app restarts to load it (R2).</summary>
    public ShipDataVersion? Installed { get; private set; }

    /// <summary>The app's source: the system's proxy settings, redirects followed.</summary>
    public static ShipDataSource CreateDefault(ShipDataStore store, string? source) =>
        new(new SocketsHttpHandler(), store, source, ManifestTimeout, DownloadTimeout, AppVersion.Current);

    /// <summary>The Ship data section's skipped line: "Skipped: 2026.11.1".</summary>
    public static string SkippedText(string tag) => $"Skipped: {Display(tag)}";

    /// <summary>The Ship data section's line for the data in use: "In use: 2026.11.1 (downloaded)".</summary>
    public static string ActiveText(ActiveShipData active) =>
        $"In use: {active.Version.Display} ({(active.Origin == ShipDataOrigin.Baseline ? "built in" : "downloaded")})";

    /// <summary><c>--ship-data-source</c>: a full folder path, or <c>https://github.com/&lt;owner&gt;/&lt;repo&gt;</c>.</summary>
    public static bool IsValidSource(string source) => RepositoryUri(source) is not null || (Path.IsPathFullyQualified(source) && !source.Contains("://", StringComparison.Ordinal));

    /// <summary>
    /// The repository of <c>--ship-data-source</c> when it names one, else <see cref="Repository"/>. The holograms come from
    /// here too (T12.4, R2): a folder source gives the data channel's, holograms are never read from a folder.
    /// </summary>
    public static Uri RepositoryFor(string? source) => source is not null && RepositoryUri(source) is { } other ? other : Repository;

    /// <summary>A release's assets, flat: <c>…/releases/download/&lt;tag&gt;/</c>, with a trailing slash so file names append (SD2, SD4).</summary>
    public static Uri ReleaseFolder(Uri repository, ShipDataVersion version) => new(repository, $"releases/download/{version}/");

    /// <summary>
    /// The start's check: once per run, on the pool, with the switch and the dev build rule (P4). null = nothing to show:
    /// switched off, already asked, failed (one log line) or cancelled because the app closes.
    /// </summary>
    public Task<ShipDataVerdict?> CheckAsync(UpdateSettings settings, ShipDataVersion active, bool isDevBuild, CancellationToken closing)
    {
        if (!ShipDataGate.ShouldCheck(settings.CheckOnStartup, isDevBuild, HasSource) || Interlocked.Exchange(ref _started, 1) == 1)
        {
            return Task.FromResult<ShipDataVerdict?>(null);
        }

        return Task.Run(async () => (await AskAsync(settings.SkippedVersion, active, "", closing)).Verdict, CancellationToken.None);
    }

    /// <summary>The settings window's Check now (P8): as often as asked, whatever the switch says; a skipped release stays skipped.</summary>
    public Task<ShipDataCheckNow> CheckNowAsync(string? skipped, ShipDataVersion active, bool isDevBuild, CancellationToken closing)
    {
        if (isDevBuild && !HasSource)
        {
            return Task.FromResult(new ShipDataCheckNow(null, DevBuildText));
        }

        return Task.Run(() => AskAsync(skipped, active, ManualPrefix, closing), CancellationToken.None);
    }

    /// <summary>
    /// Downloads ships.zip of the release a check offered and installs it (T12.3): null when it is in place, else the
    /// line's reason. The data in use change only at the next start (R2).
    /// </summary>
    /// <param name="progress">0 before, 100 after; the file is small (about 100 KB).</param>
    public async Task<string?> DownloadAsync(string version, Action<int> progress, CancellationToken closing)
    {
        (ShipDataManifest Manifest, byte[] Json)? offered;
        lock (_gate)
        {
            offered = _offered;
        }

        if (offered is not { } release || release.Manifest.DataVersion.ToString() != version)
        {
            AppLog.Write(LogContext, $"download of {version}: not offered in this run");
            return UpdateFailure.SeeLog;
        }

        if (closing.IsCancellationRequested)
        {
            return UpdateFailure.Cancelled;
        }

        progress(0);
        var asset = release.Manifest.ShipsAsset;
        var zip = await ReadAsync(asset, new Uri(ReleaseFolder(_repository, release.Manifest.DataVersion), asset), _downloadTimeout, closing);
        if (zip.Data is null)
        {
            AppLog.Write(LogContext, $"download of {version} failed: {zip.Problem}");
            return zip.NoNetwork ? UpdateFailure.NoNetwork : UpdateFailure.SeeLog;
        }

        try
        {
            var installed = await Task.Run(() => _store.Install(release.Manifest, release.Json, new MemoryStream(zip.Data)), CancellationToken.None);
            Installed = installed.Version;
            AppLog.Write(LogContext, $"installed {installed}");
        }
        catch (Exception ex) when (ex is ShipDataFormatException || ex.InnerException is ShipDataFormatException)
        {
            AppLog.Write(LogContext, $"install of {version} refused: {ex.Message}");
            return Damaged;
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.Write(LogContext, $"install of {version} failed: {ex.Message} [{ex.GetType().Name}]");
            return UpdateFailure.FolderNotWritable;
        }
        catch (IOException ex)
        {
            AppLog.Write(LogContext, $"install of {version} failed: {ex.Message} [{ex.GetType().Name}]");
            return UpdateFailure.SeeLog;
        }

        progress(100);
        return closing.IsCancellationRequested ? UpdateFailure.Cancelled : null;
    }

    public void Dispose() => _client.Dispose();

    private async Task<ShipDataCheckNow> AskAsync(string? skipped, ShipDataVersion active, string prefix, CancellationToken closing)
    {
        if (closing.IsCancellationRequested)
        {
            return new(null, UpdateService.CheckFailedText);
        }

        var fetched = await ReadAsync(ShipDataStore.ManifestFileName, new Uri(_repository, $"releases/latest/download/{ShipDataStore.ManifestFileName}"), _manifestTimeout, closing);
        if (fetched.Data is not { } json)
        {
            AppLog.Write(LogContext, $"{prefix}check failed: {fetched.Problem}");
            return new(null, UpdateService.CheckFailedText);
        }

        ShipDataManifest manifest;
        try
        {
            manifest = ShipDataManifestReader.Read(json);
        }
        catch (ShipDataFormatException ex)
        {
            AppLog.Write(LogContext, $"{prefix}check failed: {ex.Message}");
            return new(null, UpdateService.CheckFailedText);
        }

        var verdict = ShipDataGate.Offer(active, manifest.DataVersion, manifest.SchemaVersion, skipped);
        if (verdict is ShipDataOffer)
        {
            lock (_gate)
            {
                _offered = (manifest, json);
            }
        }

        var (log, status) = verdict switch
        {
            ShipDataOffer offer => ($"{offer.Version} available (in use: {active})", $"Ship data update available: {offer.Version.Display}"),
            ShipDataNeedsApp needs => ($"{needs.Version} needs schemaVersion {needs.SchemaVersion}, an app update", ShipDataGate.NeedsAppText(needs.Version)),
            NoShipDataUpdate { Skipped: { } skip } none => (none.Reason, $"{skip.Display} is skipped"),
            NoShipDataUpdate none => (none.Reason, $"Up to date · {active.Display}"),
            _ => throw new InvalidOperationException($"Unknown verdict {verdict}."),
        };
        AppLog.Write(LogContext, prefix + log);
        return new(verdict, status);
    }

    /// <summary>
    /// One file: from the local folder of <c>--ship-data-source</c>, else one GET with the timeout. The bytes, or null and
    /// the reason; <c>NoNetwork</c> says whether it was the connection (or a folder that is gone) rather than an answer.
    /// Only the app closing ends it early, without a reason.
    /// </summary>
    private async Task<(byte[]? Data, string? Problem, bool NoNetwork)> ReadAsync(string file, Uri uri, TimeSpan timeout, CancellationToken closing)
    {
        if (_folder is not null)
        {
            try
            {
                return (await File.ReadAllBytesAsync(Path.Combine(_folder, file), closing), null, false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return (null, $"{file} in {_folder}: {ex.Message}", true);
            }
            catch (OperationCanceledException) when (closing.IsCancellationRequested)
            {
                return (null, "the app closed", false);
            }
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(closing);
        limit.CancelAfter(timeout);
        try
        {
            using var response = await _client.GetAsync(uri, limit.Token);
            if (!response.IsSuccessStatusCode)
            {
                return (null, $"{uri.Host} answered {(int)response.StatusCode} for {uri}", false);
            }

            return (await response.Content.ReadAsByteArrayAsync(limit.Token), null, false);
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested)
        {
            return (null, "the app closed", false);
        }
        catch (OperationCanceledException ex)
        {
            return (null, $"no answer from {uri.Host} within {timeout.TotalSeconds:0.#} s [{ex.GetType().Name}]", true);
        }
        catch (HttpRequestException ex)
        {
            // Also a response over MaxResponseContentBufferSize; then it is no data release, and the log says so.
            return (null, $"no connection to {uri.Host} ({ex.Message}) [{ex.GetType().Name}]", ex.HttpRequestError != HttpRequestError.ConfigurationLimitExceeded);
        }
    }

    private static string Display(string tag) => ShipDataVersion.TryParse(tag, out var version) ? version.Display : tag;

    // https://github.com/<owner>/<repo>, with a trailing slash so relative release paths append.
    private static Uri? RepositoryUri(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return null;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? new Uri($"https://github.com/{parts[0]}/{parts[1]}/") : null;
    }
}
