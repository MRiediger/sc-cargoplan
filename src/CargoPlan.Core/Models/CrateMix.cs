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
    /// What one click on Split makes of a crate (D27): the two largest sizes that add up to it and lie in its footprint
    /// (32 → 16 + 16, not 24 + 8: a 24 is longer than a 32). Finer is splitting again. Empty for 1 SCU.
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
