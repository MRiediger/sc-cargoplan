using System.Globalization;
using System.IO;
using CargoPlan.App.Settings;
using Velopack;
using Velopack.Sources;

namespace CargoPlan.App.Updates;

/// <summary>
/// The update check (T11.1, D109): once per run, on a pool thread, cut off after <see cref="Timeout"/>, cancelled when the
/// app closes. The app's second network access next to the holograms (D63): one GitHub API request, no download (T11.2).
/// Every outcome is exactly one line in cargoplan.log and never an exception or a notice of its own (E8); only an update to
/// offer comes back. A package whose version differs from the app's adds one line (a release mistake, T11.3 guards it).
/// </summary>
public sealed class UpdateService
{
    /// <summary>The releases <see cref="GithubSource"/> reads: the first API page of 10, pre-releases left out (D109).</summary>
    public const string RepositoryUrl = "https://github.com/MRiediger/sc-cargoplan";

    /// <summary>E5: the overlay never waits for it; a hanging connection ends here, not at Velopack's 30 minutes.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const string LogContext = "Update";

    private readonly Func<UpdateManager> _createManager;

    private readonly TimeSpan _timeout;

    private readonly Action<string> _log;

    private int _started;

    /// <param name="localSource"><c>--updates-source</c>: a feed folder <c>vpk pack</c> wrote, instead of GitHub (E10).</param>
    public UpdateService(string? localSource)
        : this(() => CreateManager(localSource), Timeout, line => AppLog.Write(LogContext, line))
    {
    }

    /// <summary>For tests: Velopack's <c>TestVelopackLocator</c> instead of an installed app, a short timeout, the lines collected.</summary>
    internal UpdateService(Func<UpdateManager> createManager, TimeSpan timeout, Action<string> log) =>
        (_createManager, _timeout, _log) = (createManager, timeout, log);

    /// <summary>
    /// The newer release to offer, or null for every other outcome, each logged once. A second call in the same run asks
    /// nothing and returns null. Runs on the pool from the first line: making the manager reads the package's files.
    /// </summary>
    /// <param name="settings">Read on the caller's thread before the call: the session lives on the UI thread.</param>
    /// <param name="currentVersion"><see cref="AppVersion.Current"/>.</param>
    /// <param name="isDevBuild"><see cref="AppVersion.IsDevBuild"/>.</param>
    /// <param name="closing">Cancelled when the app closes (Freigabe E1): one line, no result.</param>
    public Task<UpdateAvailable?> CheckAsync(UpdateSettings settings, string currentVersion, bool isDevBuild, CancellationToken closing) =>
        Task.Run(() => CheckOnPoolAsync(settings, currentVersion, isDevBuild, closing), CancellationToken.None);

    private async Task<UpdateAvailable?> CheckOnPoolAsync(UpdateSettings settings, string currentVersion, bool isDevBuild, CancellationToken closing)
    {
        // Off or a dev build: no manager, so no network and no Velopack log, and no line either (Gate 2, 3).
        if (!UpdateGate.ShouldCheck(settings.CheckOnStartup, isDevBuild) || Interlocked.Exchange(ref _started, 1) == 1)
        {
            return null;
        }

        var abandoned = false;
        try
        {
            closing.ThrowIfCancellationRequested();
            var manager = _createManager();

            // Started from the build or publish folder, not from a portable package: Velopack can't check (NotInstalledException).
            if (!manager.IsInstalled)
            {
                _log("no check: not a Velopack package (started from a build or publish folder)");
                return null;
            }

            if (manager.CurrentVersion?.ToString() is { } packaged && AppVersion.WithoutMetadata(packaged) != currentVersion)
            {
                _log($"the package says {packaged}, the app {currentVersion}; comparing with the app's");
            }

            // CheckForUpdatesAsync takes no token (S11.0 G7): WaitAsync ends our wait, the request itself runs on until
            // Velopack's own timeout. Its late failure is observed and logged, never left to UnobservedTaskException.
            // Source: https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1.waitasync (TimeSpan, CancellationToken)
            var check = manager.CheckForUpdatesAsync();
            _ = check.ContinueWith(
                task =>
                {
                    if (Volatile.Read(ref abandoned))
                    {
                        _log($"the abandoned check failed later: {task.Exception?.GetBaseException().Message}");
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            var info = await check.WaitAsync(_timeout, closing).ConfigureAwait(false);
            var target = info?.TargetFullRelease;
            switch (UpdateGate.Offer(currentVersion, target?.Version.ToString(), target?.NotesMarkdown, settings.SkippedVersion))
            {
                case UpdateAvailable offer:
                    _log($"{offer.Version} available (installed {currentVersion})");
                    return offer;
                case NoUpdate none:
                    _log(none.Reason);
                    return null;
                default:
                    throw new InvalidOperationException("An update verdict that is neither available nor none.");
            }
        }
        catch (TimeoutException)
        {
            Volatile.Write(ref abandoned, true);
            _log(string.Create(CultureInfo.InvariantCulture, $"check failed: no answer within {_timeout.TotalSeconds:0.#} s"));
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested)
        {
            Volatile.Write(ref abandoned, true);
            _log("check cancelled: the app is closing");
        }
        catch (Exception ex)
        {
            // Offline, rate limit (403), no feed, a broken index: all the same to the player, who sees nothing (E8).
            _log($"check failed: {ex.GetType().Name}: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// GitHub without a token (60 API requests an hour per IP, one per check) and without pre-releases, or a local folder.
    /// Without a locator Velopack takes the one <c>VelopackApp.Run()</c> set in <c>Program.Main</c>.
    /// Source: Velopack.xml 1.2.161, GithubSource(repoUrl, accessToken, prerelease, downloader), SimpleFileSource(DirectoryInfo).
    /// </summary>
    private static UpdateManager CreateManager(string? localSource) =>
        localSource is null
            ? new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false))
            : new UpdateManager(new SimpleFileSource(new DirectoryInfo(localSource)));
}
