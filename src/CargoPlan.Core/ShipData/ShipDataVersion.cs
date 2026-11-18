using System.Globalization;
using System.Text.RegularExpressions;

namespace CargoPlan.Core.ShipData;

/// <summary>
/// The tag of a data release, <c>ships-YYYY.MM[.N]</c> (D111, SD4 of sc-cargoplan-shipdata): the month's run and its
/// re-runs. One spelling each: two-digit month, no <c>.0</c>, no leading zero in N, so the folder name is the tag.
/// </summary>
public readonly partial record struct ShipDataVersion(int Year, int Month, int Patch) : IComparable<ShipDataVersion>
{
    public const string Prefix = "ships-";

    /// <summary>
    /// The data next to the exe: the seed release, byte for byte the repo's ships/ (T12.0). A constant until T12.5 puts
    /// the release's manifest into the ZIP. A download older than this is never active.
    /// </summary>
    public static ShipDataVersion Baseline { get; } = new(2026, 11, 0);

    /// <summary>The tag without <see cref="Prefix"/>, as the overlay shows it: <c>2026.11.1</c>.</summary>
    public string Display => ToString()[Prefix.Length..];

    public static bool TryParse(string? text, out ShipDataVersion version)
    {
        version = default;
        if (text is null || Tag().Match(text) is not { Success: true } match)
        {
            return false;
        }

        var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
        if (month is < 1 or > 12)
        {
            return false;
        }

        var patch = match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture) : 0;
        version = new ShipDataVersion(int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture), month, patch);
        return true;
    }

    public int CompareTo(ShipDataVersion other) =>
        (Year, Month, Patch).CompareTo((other.Year, other.Month, other.Patch));

    public override string ToString() => Patch == 0
        ? string.Create(CultureInfo.InvariantCulture, $"{Prefix}{Year:D4}.{Month:D2}")
        : string.Create(CultureInfo.InvariantCulture, $"{Prefix}{Year:D4}.{Month:D2}.{Patch}");

    public static bool operator <(ShipDataVersion left, ShipDataVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(ShipDataVersion left, ShipDataVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(ShipDataVersion left, ShipDataVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(ShipDataVersion left, ShipDataVersion right) => left.CompareTo(right) >= 0;

    // ASCII digits only ([0-9], not \d, which takes every Unicode digit); N from 1 without a leading zero; \z, not $,
    // which would take a trailing line break.
    [GeneratedRegex(@"^ships-(?<year>[0-9]{4})\.(?<month>[0-9]{2})(?:\.(?<patch>[1-9][0-9]{0,3}))?\z", RegexOptions.CultureInvariant)]
    private static partial Regex Tag();
}
