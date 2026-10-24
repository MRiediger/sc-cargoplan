using CargoPlan.Core.Models;

namespace CargoPlan.Core.Missions;

/// <summary>
/// One contract item as entered (D23): SCU and max crate size, optionally the crates the kiosk really delivered.
/// Input, not model: the planner sees the <see cref="CargoItem"/>s of <see cref="ToCargoItems"/>.
/// </summary>
/// <param name="Crates">Override of the derived mix; null = <see cref="CrateMix.Decompose"/>.</param>
/// <param name="Pickup">Where its crates are collected, without amounts (D68); null = not known. <see cref="Pickups.Normalized"/>.</param>
public sealed record MissionItem(
    string Commodity, int Scu, CrateSize MaxCrateSize, IReadOnlyList<CrateSize>? Crates = null, IReadOnlyList<string>? Pickup = null)
{
    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    public int Scu { get; } = ValidatedScu(Scu);
    public CrateSize MaxCrateSize { get; } = ValidatedMax(MaxCrateSize);
    public IReadOnlyList<CrateSize>? Crates { get; } = ValidatedCrates(Scu, MaxCrateSize, Crates);
    public IReadOnlyList<string>? Pickup { get; } = ValidatedPickup(Pickup);

    /// <summary>The crates of this item: the override if there is one, else the derived mix.</summary>
    public IReadOnlyList<CrateSize> CrateList => Crates ?? CrateMix.Decompose(Scu, MaxCrateSize);

    /// <summary>The one place an item becomes cargo: one item per crate size, largest first (15 / max 4 → 12, 2, 1).</summary>
    public IReadOnlyList<CargoItem> ToCargoItems() =>
    [
        .. CrateList.GroupBy(c => c).OrderByDescending(g => g.Key)
            .Select(g => new CargoItem(Commodity, g.Count() * (int)g.Key, g.Key)),
    ];

    /// <summary>
    /// The item with these crates, as split and merge leave it (D27): an override largest first, or none if the crates
    /// are the derived mix. Unlike an override typed in the editor (kept as typed, D23), nobody typed this one.
    /// </summary>
    /// <exception cref="ArgumentException">The crates don't fit the item (<see cref="CrateMix.Problem"/>).</exception>
    public MissionItem WithCrateList(IReadOnlyList<CrateSize> crates)
    {
        CrateSize[] sorted = [.. crates.OrderDescending()];
        return new(Commodity, Scu, MaxCrateSize, sorted.SequenceEqual(CrateMix.Decompose(Scu, MaxCrateSize)) ? null : sorted, Pickup);
    }

    private static int ValidatedScu(int scu)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scu, nameof(Scu));
        return scu;
    }

    private static CrateSize ValidatedMax(CrateSize max) =>
        Enum.IsDefined(max) ? max : throw new ArgumentOutOfRangeException(nameof(MaxCrateSize), max, "Unknown crate size.");

    private static IReadOnlyList<CrateSize>? ValidatedCrates(int scu, CrateSize max, IReadOnlyList<CrateSize>? crates)
    {
        if (crates is not null && CrateMix.Problem(scu, max, [.. crates.Select(c => (int)c)]) is { } problem)
        {
            throw new ArgumentException(problem, nameof(Crates));
        }

        return crates;
    }

    /// <summary>An empty list is no pickup; otherwise the list must already be normalized, the callers' boundary does that (A6).</summary>
    private static IReadOnlyList<string>? ValidatedPickup(IReadOnlyList<string>? pickup)
    {
        if (pickup is null || pickup.Count == 0)
        {
            return null;
        }

        if (!pickup.SequenceEqual(Pickups.Normalized(pickup) ?? []))
        {
            throw new ArgumentException($"Pickup '{string.Join(Pickups.Separator, pickup)}' has a blank, untrimmed or repeated place.", nameof(Pickup));
        }

        return pickup;
    }
}
