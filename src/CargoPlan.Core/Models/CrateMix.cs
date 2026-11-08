using System.Globalization;
using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>
/// Which crates a contract item comes in (D23). A contract names only the SCU and a max crate size; the freight
/// elevator delivers greedily: the largest allowed crate as often as it fits, the rest in smaller ones.
/// </summary>
public static class CrateMix
{
    /// <summary>
    /// The sizes the elevator uses, largest first. If the game changes its rule, this list or <see cref="Decompose"/>
    /// is the place to touch. Most likely to change: whether a max of 32 really gives 24s (24 SCU at max 32 → one 24).
    /// </summary>
    public static IReadOnlyList<CrateSize> Sizes { get; } =
    [
        CrateSize.ThirtyTwo,
        CrateSize.TwentyFour,
        CrateSize.Sixteen,
        CrateSize.Eight,
        CrateSize.Four,
        CrateSize.Two,
        CrateSize.One,
    ];

    /// <summary>Greedy: every size up to <paramref name="max"/>, largest first, as often as it fits.</summary>
    /// <exception cref="ArgumentOutOfRangeException">No SCU, or an unknown max crate size.</exception>
    public static IReadOnlyList<CrateSize> Decompose(int scu, CrateSize max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scu);
        if (!Enum.IsDefined(max))
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "Unknown crate size.");
        }

        List<CrateSize> crates = [];
        var rest = scu;
        foreach (var size in Sizes.Where(s => s <= max))
        {
            for (; rest >= (int)size; rest -= (int)size)
            {
                crates.Add(size);
            }
        }

        return crates;
    }

    /// <summary>
    /// The mix the elevator delivers for a ship (D70, K3.3): greedy over the sizes up to <paramref name="max"/> that some
    /// rack takes (<see cref="CrateCap.Accepts"/>), never above the contract's max. If a rest is left that no such size
    /// covers (Hull C: 40 at max 32 is 32 and 8), there is no mix for this ship: the contract's mix up to the cap is
    /// returned, whose too small crate no rack takes, so the mission waits and says why.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">No SCU, or an unknown max crate size.</exception>
    public static IReadOnlyList<CrateSize> Decompose(int scu, CrateSize max, CrateCap cap)
    {
        var crates = Decompose(scu, cap.Limit(max));
        List<CrateSize> accepted = [];
        var rest = scu;
        foreach (var size in Sizes.Where(s => s <= max && cap.Accepts(s)))
        {
            for (; rest >= (int)size; rest -= (int)size)
            {
                accepted.Add(size);
            }
        }

        return rest == 0 ? accepted : crates;
    }

    /// <summary>
    /// What one click on Split makes of a crate (D27 addendum): halves if half is a crate size, else the largest size that
    /// fits plus the rest (24 → 16 + 8). So 32 → 16 + 16, although 24 + 8 fits the 8×2×2 too (D57). Finer is splitting
    /// again. Empty for 1 SCU.
    /// </summary>
    public static IReadOnlyList<CrateSize> DefaultSplit(CrateSize size) => size switch
    {
        CrateSize.One => [],
        CrateSize.Two => [CrateSize.One, CrateSize.One],
        CrateSize.Four => [CrateSize.Two, CrateSize.Two],
        CrateSize.Eight => [CrateSize.Four, CrateSize.Four],
        CrateSize.Sixteen => [CrateSize.Eight, CrateSize.Eight],
        CrateSize.TwentyFour => [CrateSize.Sixteen, CrateSize.Eight],
        CrateSize.ThirtyTwo => [CrateSize.Sixteen, CrateSize.Sixteen],
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown crate size."),
    };

    private const char Plus = '+';

    /// <summary>The sign between count and size; typed also as x or X, because nobody types × (D96).</summary>
    private const char Times = '×';

    /// <summary>The most crates one part of a typed list may count: a bound at the input, no ship holds this many.</summary>
    private const int MaxCount = 9999;

    /// <summary>
    /// One spelling of a crate mix everywhere (D96): sizes largest first, a size that repeats as count×size, "+" between:
    /// "6×32+16+4+2+1". Plain numbers, so a typed 3 shows as typed and <see cref="Problem"/> names it.
    /// </summary>
    public static string Format(IEnumerable<int> crates) =>
        string.Join(Plus, crates.GroupBy(size => size).OrderByDescending(g => g.Key)
            .Select(g => g.Count() == 1 ? Invariant($"{g.Key}") : Invariant($"{g.Count()}{Times}{g.Key}")));

    /// <inheritdoc cref="Format(IEnumerable{int})"/>
    public static string Format(IEnumerable<CrateSize> crates) => Format(crates.Select(c => (int)c));

    /// <summary>
    /// A typed crate list (D96): parts joined by "+", each a number or count×size ("2x32+16"), spaces allowed, count
    /// first. Null if a part is neither, or counts 0 or more than <see cref="MaxCount"/>. Numbers only: whether they are
    /// crate sizes and add up is <see cref="Problem"/>'s check.
    /// </summary>
    public static List<int>? Parse(string text)
    {
        List<int> crates = [];
        foreach (var part in text.Split(Plus))
        {
            var factors = part.Split([Times, 'x', 'X']);
            switch (factors)
            {
                case [var size] when Number(size) is { } crate:
                    crates.Add(crate);
                    break;
                case [var count, var size] when Number(count) is >= 1 and <= MaxCount and var n && Number(size) is { } crate:
                    crates.AddRange(Enumerable.Repeat(crate, n));
                    break;
                default:
                    return null;
            }
        }

        return crates;
    }

    // NumberStyles.None: digits only, no sign, no decimal point, no thousands separator.
    private static int? Number(string text) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>
    /// What is wrong with a given list of crates for an item; null = they fit. Plain numbers, so an unknown size like 3
    /// can be named. One rule for the mission file, <c>MissionItem</c> and the editor.
    /// </summary>
    public static string? Problem(int scu, CrateSize max, IReadOnlyList<int> crates)
    {
        if (crates.Count == 0)
        {
            return "No crates given.";
        }

        foreach (var crate in crates)
        {
            if (!Enum.IsDefined((CrateSize)crate))
            {
                return Invariant($"{crate} SCU is not a crate size.");
            }

            if (crate > (int)max)
            {
                return Invariant($"{crate} SCU crates are larger than the max crate size of {(int)max} SCU.");
            }
        }

        var sum = crates.Sum();
        return sum == scu ? null : Invariant($"The crates add up to {sum} SCU, not {scu}.");
    }
}
