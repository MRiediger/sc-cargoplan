namespace CargoPlan.App.Updates;

/// <summary>What the update check came to: an update to offer, or why not.</summary>
public abstract record UpdateVerdict;

/// <summary>A newer release the player hasn't skipped.</summary>
/// <param name="Version">Normalized, without build metadata: "0.9.1".</param>
/// <param name="Notes">The release notes in Markdown as packed (<c>vpk pack --releaseNotes</c>); null if none (T11.2 shows them).</param>
public sealed record UpdateAvailable(string Version, string? Notes) : UpdateVerdict;

/// <summary>Nothing to offer; <paramref name="Reason"/> is the check's one log line.</summary>
public sealed record NoUpdate(string Reason) : UpdateVerdict;

/// <summary>
/// The update check's decisions without Velopack (T11.1, D109), so they are testable alone: whether to ask at all, before
/// any network, and what to make of the answer.
/// </summary>
public static class UpdateGate
{
    /// <summary>
    /// Off in settings.json, or a developer's build (<see cref="AppVersion.DevVersion"/>): no check, no network, no line (E6).
    /// </summary>
    public static bool ShouldCheck(bool checkOnStartup, bool isDevBuild) => checkOnStartup && !isDevBuild;

    /// <param name="current">The app's version (<see cref="AppVersion.Current"/>).</param>
    /// <param name="found">The newest release the feed has; null = none newer than the installed one.</param>
    /// <param name="notes">Its notes; blank counts as none.</param>
    /// <param name="skipped"><c>updates.skippedVersion</c>: exactly this version is not offered, a newer one is (E7).</param>
    public static UpdateVerdict Offer(string current, string? found, string? notes, string? skipped)
    {
        if (!ReleaseVersion.TryParse(current, out var installed))
        {
            return new NoUpdate($"the app's version \"{current}\" is not a version");
        }

        if (found is null)
        {
            return new NoUpdate($"nothing newer than {installed}");
        }

        if (!ReleaseVersion.TryParse(found, out var offered))
        {
            return new NoUpdate($"the feed's version \"{found}\" is not a version");
        }

        // GithubSource filters by GitHub's pre-release flag, not by the version (S11.0 G7): an rc published without it.
        if (offered.IsPreRelease)
        {
            return new NoUpdate($"{offered} is a pre-release");
        }

        if (offered.CompareTo(installed) <= 0)
        {
            return new NoUpdate($"nothing newer than {installed}");
        }

        if (ReleaseVersion.TryParse(skipped, out var skip) && skip == offered)
        {
            return new NoUpdate($"{offered} skipped (updates.skippedVersion)");
        }

        return new UpdateAvailable(offered.ToString(), string.IsNullOrWhiteSpace(notes) ? null : notes);
    }
}
