using System.Globalization;

namespace CargoPlan.App.Updates;

/// <summary>
/// A release's version in the form our tags have (<c>release.yml</c>): major.minor.patch, an optional pre-release after "-",
/// build metadata after "+" dropped. Not the whole of SemVer: <c>NuGet.Versioning</c> doesn't come with Velopack, whose own
/// <c>SemanticVersion</c> would tie the gate to Velopack. Where the two judge differently, Velopack's wins, since it decides
/// what the check returns at all (D109).
/// </summary>
/// <param name="Number">Major, minor, patch; the build and revision of <see cref="Version"/> stay unset.</param>
/// <param name="PreRelease">"rc.1" of "0.5.0-rc.1"; null for a stable release.</param>
public readonly record struct ReleaseVersion(Version Number, string? PreRelease) : IComparable<ReleaseVersion>
{
    public bool IsPreRelease => PreRelease is not null;

    /// <summary>"0.9.1", "0.5.0-rc.1", "0.9.1+abc"; not "v0.9.1", "0.9", "0.9.1.2", whitespace or an empty pre-release.</summary>
    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var withoutMetadata = text.Split('+')[0];
        var dash = withoutMetadata.IndexOf('-', StringComparison.Ordinal);
        var number = dash < 0 ? withoutMetadata : withoutMetadata[..dash];
        var preRelease = dash < 0 ? null : withoutMetadata[(dash + 1)..];
        if (preRelease is not null && (preRelease.Length == 0 || !preRelease.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.')))
        {
            return false;
        }

        var parts = number.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var values = new int[3];
        for (var i = 0; i < 3; i++)
        {
            // NumberStyles.None: digits only, no sign, no whitespace.
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        version = new ReleaseVersion(new Version(values[0], values[1], values[2]), preRelease);
        return true;
    }

    /// <summary>By number; at the same number the stable release is the newer (SemVer 2.0.0, rule 11).</summary>
    public int CompareTo(ReleaseVersion other)
    {
        var byNumber = Number.CompareTo(other.Number);
        return byNumber != 0
            ? byNumber
            : (PreRelease, other.PreRelease) switch
            {
                (null, null) => 0,
                (null, _) => 1,
                (_, null) => -1,
                var (mine, theirs) => string.CompareOrdinal(mine, theirs),
            };
    }

    public override string ToString() => PreRelease is null ? Number.ToString() : $"{Number}-{PreRelease}";
}
