using System.Globalization;
using System.IO;
using System.Security.Principal;
using CargoPlan.App.Settings;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace CargoPlan.App.Updates;

/// <summary>What Check now in the settings window found (T11.2, U9): the offer, if any, and the status line's text.</summary>
public sealed record ManualCheck(UpdateAvailable? Offer, string Status);

/// <summary>An update Update.exe couldn't swap in; it started the old app again (T11.2, D109).</summary>
/// <param name="Reason">One of <see cref="UpdateFailure"/>'s texts.</param>
public sealed record FailedApply(string Version, string Reason);

/// <summary>
/// What <see cref="UpdateService"/> asks of the machine outside Velopack, so tests can set it (T11.2).
/// </summary>
/// <param name="FeedMissing">A local feed (<c>--updates-source</c>) whose folder is gone.</param>
/// <param name="IsElevated">The app runs as administrator.</param>
/// <param name="RootFolder">The portable folder Update.exe swaps; null outside a Velopack package.</param>
/// <param name="IsWritable">Whether the app can write into a folder.</param>
internal sealed record UpdateEnvironment(Func<bool> FeedMissing, Func<bool> IsElevated, Func<string?> RootFolder, Func<string, bool> IsWritable)
{
    private const string WriteProbeName = ".cargoplan_write_test";

    public static UpdateEnvironment Default(string? localSource) => new(
        () => localSource is not null && !Directory.Exists(localSource),
        IsAdministrator,
        PortableRoot,
        CanWrite);

    /// <summary>
    /// Under UAC a non-elevated administrator's token holds the group as deny-only, so this is true only when elevated. The
    /// start line says it too (E1): after a swap with UAC the app should run as before, not elevated.
    /// Source: https://learn.microsoft.com/dotnet/api/system.security.principal.windowsprincipal.isinrole
    /// </summary>
    internal static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// The locator <c>VelopackApp.Run()</c> set (UpdateManager's own is not public in 1.2.161). Source: Velopack.xml 1.2.161,
    /// VelopackLocator.IsCurrentSet, VelopackLocator.Current, IVelopackLocator.RootAppDir.
    /// </summary>
    private static string? PortableRoot() => VelopackLocator.IsCurrentSet ? VelopackLocator.Current.RootAppDir : null;

    /// <summary>As Velopack's own test (<c>.velopack_dir_test</c>, PathUtil.cs 93–101): write a file and delete it.</summary>
    private static bool CanWrite(string folder)
    {
        var probe = Path.Combine(folder, WriteProbeName);
        try
        {
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>
/// The update check, download and apply (T11.1, T11.2, D109). The app's second network access next to the holograms (D63):
/// one GitHub API request per check, the package only on the player's click. The start's check runs once per run, on a pool
/// thread, cut off after <see cref="Timeout"/>, cancelled when the app closes; Check now runs whenever asked. Every outcome
/// is one line in cargoplan.log, never an exception; a download or an apply that fails comes back as one of
/// <see cref="UpdateFailure"/>'s texts.
/// </summary>
public sealed class UpdateService
{
    /// <summary>The releases <see cref="GithubSource"/> reads: the first API page of 10, pre-releases left out (D109).</summary>
    public const string RepositoryUrl = "https://github.com/MRiediger/sc-cargoplan";

    public const string UpToDateText = "Up to date";

    public const string CheckFailedText = "Check failed · see cargoplan.log";

    public const string DevBuildText = "Dev build – updates disabled";

    public const string NotInstalledText = "Not an update package – download the ZIP";

    /// <summary>E5: the overlay never waits for it; a hanging connection ends here, not at Velopack's 30 minutes.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const string LogContext = "Update";

    private const string ManualPrefix = "check now: ";

    private readonly Func<UpdateManager> _createManager;

    private readonly TimeSpan _timeout;

    private readonly Action<string> _log;

    private readonly UpdateEnvironment _environment;

    private readonly Lock _gate = new();

    private int _started;

    // The last check's offer, for the download; and the package that is on disk, for the apply. Set on the pool, read on
    // the UI thread, so under _gate.
    private (UpdateManager Manager, UpdateInfo Info)? _offered;

    private (UpdateManager Manager, VelopackAsset Asset)? _ready;

    /// <param name="localSource"><c>--updates-source</c>: a feed folder <c>vpk pack</c> wrote, instead of GitHub (E10).</param>
    /// <param name="prerelease"><c>--updates-prerelease</c>: GitHub's pre-releases count too (T11.3, R9).</param>
    public UpdateService(string? localSource, bool prerelease = false)
        : this(() => CreateManager(localSource, prerelease), Timeout, line => AppLog.Write(LogContext, line), UpdateEnvironment.Default(localSource))
    {
    }

    /// <summary>For tests: Velopack's <c>TestVelopackLocator</c> instead of an installed app, a short timeout, the lines collected.</summary>
    internal UpdateService(Func<UpdateManager> createManager, TimeSpan timeout, Action<string> log, UpdateEnvironment? environment = null) =>
        (_createManager, _timeout, _log, _environment) = (createManager, timeout, log, environment ?? UpdateEnvironment.Default(null));

    /// <summary>
    /// The start's check: the newer release to offer, or null for every other outcome, each logged once. A second call in
    /// the same run asks nothing and returns null. Runs on the pool from the first line: making the manager reads the
    /// package's files.
    /// </summary>
    /// <param name="settings">Read on the caller's thread before the call: the session lives on the UI thread.</param>
    /// <param name="currentVersion"><see cref="AppVersion.Current"/>.</param>
    /// <param name="isDevBuild"><see cref="AppVersion.IsDevBuild"/>.</param>
    /// <param name="closing">Cancelled when the app closes (Freigabe E1): one line, no result.</param>
    public Task<UpdateAvailable?> CheckAsync(UpdateSettings settings, string currentVersion, bool isDevBuild, CancellationToken closing) =>
        Task.Run(
            async () =>
            {
                // Off or a dev build: no manager, so no network and no Velopack log, and no line either (Gate 2, 3).
                if (!UpdateGate.ShouldCheck(settings.CheckOnStartup, isDevBuild) || Interlocked.Exchange(ref _started, 1) == 1)
                {
                    return null;
                }

                return (await AskAsync(settings.SkippedVersion, currentVersion, closing, prefix: "").ConfigureAwait(false)).Offer;
            },
            CancellationToken.None);

    /// <summary>
    /// The settings window's Check now (U9): as often as asked and with the start's check off, but the skipped version stays
    /// skipped and a dev build asks nothing.
    /// </summary>
    public Task<ManualCheck> CheckNowAsync(string? skippedVersion, string currentVersion, bool isDevBuild, CancellationToken closing)
    {
        if (isDevBuild)
        {
            _log(ManualPrefix + "a dev build checks nothing");
            return Task.FromResult(new ManualCheck(null, DevBuildText));
        }

        return Task.Run(() => AskAsync(skippedVersion, currentVersion, closing, ManualPrefix), CancellationToken.None);
    }

    private async Task<ManualCheck> AskAsync(string? skippedVersion, string currentVersion, CancellationToken closing, string prefix)
    {
        var abandoned = false;
        try
        {
            closing.ThrowIfCancellationRequested();
            var manager = _createManager();

            // Started from the build or publish folder, not from a portable package: Velopack can't check (NotInstalledException).
            if (!manager.IsInstalled)
            {
                _log(prefix + "no check: not a Velopack package (started from a build or publish folder)");
                return new ManualCheck(null, NotInstalledText);
            }

            if (manager.CurrentVersion?.ToString() is { } packaged && AppVersion.WithoutMetadata(packaged) != currentVersion)
            {
                _log(prefix + $"the package says {packaged}, the app {currentVersion}; comparing with the app's");
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
                        _log(prefix + $"the abandoned check failed later: {task.Exception?.GetBaseException().Message}");
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            var info = await check.WaitAsync(_timeout, closing).ConfigureAwait(false);
            var target = info?.TargetFullRelease;
            switch (UpdateGate.Offer(currentVersion, target?.Version.ToString(), target?.NotesMarkdown, skippedVersion))
            {
                case UpdateAvailable offer:
                    _log(prefix + $"{offer.Version} available (installed {currentVersion})");
                    lock (_gate)
                    {
                        _offered = (manager, info!);
                    }

                    return new ManualCheck(offer, $"Update available: v{offer.Version}");
                case NoUpdate none:
                    _log(prefix + none.Reason);
                    return new ManualCheck(null, none.Skipped is { } skipped ? $"v{skipped} is skipped" : UpToDateText);
                default:
                    throw new InvalidOperationException("An update verdict that is neither available nor none.");
            }
        }
        catch (TimeoutException)
        {
            Volatile.Write(ref abandoned, true);
            _log(prefix + string.Create(CultureInfo.InvariantCulture, $"check failed: no answer within {_timeout.TotalSeconds:0.#} s"));
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested)
        {
            Volatile.Write(ref abandoned, true);
            _log(prefix + "check cancelled: the app is closing");
        }
        catch (Exception ex)
        {
            // Offline, rate limit (403), no feed, a broken index: all the same to the player, who sees nothing (E8).
            _log(prefix + $"check failed: {ex.GetType().Name}: {ex.Message}");
        }

        return new ManualCheck(null, CheckFailedText);
    }

    /// <summary>
    /// Downloads the offered release into packages\ (U4): null when it lies there, else why not. A package already on disk
    /// is taken as it is; Velopack checks only that the file exists, which is safe because only a checked download gets the
    /// final name (UpdateManager.cs 254–256, 284–286).
    /// </summary>
    /// <param name="progress">0–100 from a pool thread, even numbers, only on change (Velopack 1.2.161).</param>
    /// <param name="closing">The app closes: never null then, so nothing applies.</param>
    public Task<string?> DownloadAsync(string version, Action<int> progress, CancellationToken closing) =>
        Task.Run(() => DownloadOnPoolAsync(version, progress, closing), CancellationToken.None);

    private async Task<string?> DownloadOnPoolAsync(string version, Action<int> progress, CancellationToken closing)
    {
        // Elevated already and the folder still can't be written: Update.exe would stop with "The update cannot continue."
        // (apply_windows_impl.rs 63–64). Not elevated it asks for UAC itself, so that one goes on (Freigabe).
        if (_environment.IsElevated() && _environment.RootFolder() is { } root && !_environment.IsWritable(root))
        {
            _log($"not downloading {version}: the app runs elevated and can't write {root}, so Update.exe couldn't swap it");
            return UpdateFailure.FolderNotWritable;
        }

        (UpdateManager Manager, VelopackAsset Asset)? ready;
        (UpdateManager Manager, UpdateInfo Info)? offered;
        lock (_gate)
        {
            (ready, offered) = (_ready, _offered);
        }

        if (ready is { } lying && Same(lying.Asset.Version, version))
        {
            _log($"{version} is downloaded already");
            return null;
        }

        if (offered is not { } offer || !Same(offer.Info.TargetFullRelease.Version, version))
        {
            _log($"download failed (see cargoplan.log): {version} was never offered in this run");
            return UpdateFailure.SeeLog;
        }

        try
        {
            // Our own checks before and after: SimpleFileSource copies without looking at the token (SimpleFileSource.cs
            // 76–82), and a download finished while the app closes must not come back as "apply now".
            closing.ThrowIfCancellationRequested();
            _log($"downloading {version}");
            await offer.Manager.DownloadUpdatesAsync(offer.Info, progress, closing).ConfigureAwait(false);
            lock (_gate)
            {
                _ready = (offer.Manager, offer.Info.TargetFullRelease);
            }

            _log($"{version} downloaded");
            closing.ThrowIfCancellationRequested();
            return null;
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested)
        {
            _log("download cancelled: the app is closing");
            return UpdateFailure.Cancelled;
        }
        catch (Exception ex)
        {
            var reason = UpdateFailure.Classify(ex, _environment.FeedMissing());
            _log($"download failed ({reason}): {ex.GetType().Name}: {ex.GetBaseException().Message}");
            return reason;
        }
    }

    /// <summary>
    /// Starts Update.exe, which waits up to 60 s for this process to end, swaps <c>current\</c> (asking for UAC if the folder
    /// can't be written) and starts the app with <paramref name="restartArgs"/>: the new one, or the old one if the swap
    /// failed (apply.rs 46–61). Returns at once; null = the caller shuts down now (U4), else why not.
    /// Not <c>ApplyUpdatesAndRestart</c>: it ends the process with Environment.Exit, before Closed flushes anything.
    /// Source: Velopack.xml 1.2.161, UpdateManager.WaitExitThenApplyUpdates(VelopackAsset, bool silent, bool restart, string[]).
    /// </summary>
    public string? Apply(string version, IReadOnlyList<string> restartArgs)
    {
        (UpdateManager Manager, VelopackAsset Asset)? ready;
        lock (_gate)
        {
            ready = _ready;
        }

        if (ready is not { } lying || !Same(lying.Asset.Version, version))
        {
            _log($"apply failed (see cargoplan.log): {version} is not downloaded");
            return UpdateFailure.SeeLog;
        }

        try
        {
            // silent: false shows Velopack's progress window during the swap, so the player sees why the overlay is gone.
            lying.Manager.WaitExitThenApplyUpdates(lying.Asset, silent: false, restart: true, [.. restartArgs]);
            _log($"applying {version} once the app has closed, restarting with [{string.Join(' ', restartArgs)}]");
            return null;
        }
        catch (Exception ex)
        {
            var reason = UpdateFailure.Classify(ex);
            _log($"apply failed ({reason}): {ex.GetType().Name}: {ex.Message}");
            return reason;
        }
    }

    /// <summary>
    /// After Update.exe started this app (<c>VelopackApp.OnRestarted</c>): a package newer than the app still lying in
    /// packages\ means the swap failed and Update.exe started the old app again (apply.rs 57–61); auto-apply is off, so it
    /// stays (D109). The reason is in Velopack's log only: a folder the app can't write means Update.exe needed UAC, and
    /// that was declined. No network; runs whatever <c>checkOnStartup</c> says, the player asked for this update.
    /// </summary>
    public Task<FailedApply?> FailedApplyAsync(bool restartedByUpdate, string currentVersion)
    {
        if (!restartedByUpdate)
        {
            return Task.FromResult<FailedApply?>(null);
        }

        return Task.Run(
            () =>
            {
                try
                {
                    var manager = _createManager();
                    if (!manager.IsInstalled || manager.UpdatePendingRestart is not { } pending)
                    {
                        _log($"restarted by Update.exe as {currentVersion}");
                        return null;
                    }

                    if (!ReleaseVersion.TryParse(pending.Version.ToString(), out var lying)
                        || !ReleaseVersion.TryParse(currentVersion, out var installed)
                        || lying.CompareTo(installed) <= 0)
                    {
                        _log($"restarted by Update.exe as {currentVersion}");
                        return null;
                    }

                    var reason = _environment.RootFolder() is { } root && !_environment.IsWritable(root) ? UpdateFailure.Cancelled : UpdateFailure.SeeLog;
                    lock (_gate)
                    {
                        _ready = (manager, pending);
                    }

                    _log($"the swap to {lying} failed, Update.exe started {currentVersion} again ({reason}; the reason is in %LocalAppData%\\velopack)");
                    return new FailedApply(lying.ToString(), reason);
                }
                catch (Exception ex)
                {
                    _log($"after the restart: {ex.GetType().Name}: {ex.Message}");
                    return null;
                }
            },
            CancellationToken.None);
    }

    private static bool Same(SemanticVersion packaged, string version) =>
        ReleaseVersion.TryParse(packaged.ToString(), out var a) && ReleaseVersion.TryParse(version, out var b) && a == b;

    /// <summary>
    /// Without a locator Velopack takes the one <c>VelopackApp.Run()</c> set in <c>Program.Main</c>. The pre-release line
    /// comes with each check, so the gate's log shows which releases were asked for.
    /// </summary>
    private static UpdateManager CreateManager(string? localSource, bool prerelease)
    {
        if (prerelease)
        {
            AppLog.Write(LogContext, $"checking GitHub with pre-releases ({StartOptions.UpdatesPrereleaseSwitch})");
        }

        return new UpdateManager(CreateSource(localSource, prerelease));
    }

    /// <summary>
    /// GitHub without a token (60 API requests an hour per IP, one per check), pre-releases only with
    /// <paramref name="prerelease"/> (GitHub's release flag, not the tag's name; D109), or a local folder.
    /// Source: Velopack.xml 1.2.161, GithubSource(repoUrl, accessToken, prerelease, downloader), SimpleFileSource(DirectoryInfo).
    /// </summary>
    internal static IUpdateSource CreateSource(string? localSource, bool prerelease) =>
        localSource is null
            ? new GithubSource(RepositoryUrl, accessToken: null, prerelease)
            : new SimpleFileSource(new DirectoryInfo(localSource));
}
