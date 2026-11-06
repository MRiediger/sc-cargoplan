using static System.FormattableString;

namespace CargoPlan.Core.Ships;

/// <summary>
/// File format of a cargo module (docs/ship-format.md, "Cargo modules"; K5.3, D59): the racks a ship gains with a module
/// that changes its hold in the game. Every ship in <see cref="Ships"/> gets one more catalog entry, its own racks first,
/// the module's after them, checked as one ship file (<see cref="On"/>, <see cref="ShipDto.ToShip"/>).
/// </summary>
internal sealed record ModuleDto(
    int SchemaVersion, string Id, string Name, IReadOnlyList<string> Ships, IReadOnlyList<RackDto> Racks,
    string? Source = null, string? Notes = null,
    IReadOnlyList<double>? HoloOrigin = null, IReadOnlyList<double>? OutlineOrigin = null, double? OutlineScale = null)
{
    /// <exception cref="ArgumentException">The module names no ship or has no racks.</exception>
    public void Check()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id, nameof(Id));
        ArgumentException.ThrowIfNullOrWhiteSpace(Name, nameof(Name));
        if (Ships.Count == 0)
        {
            throw new ArgumentException(Invariant($"Module '{Id}' names no ship."), nameof(Ships));
        }

        if (Racks.Count == 0)
        {
            throw new ArgumentException(Invariant($"Module '{Id}' has no racks."), nameof(Racks));
        }

        // The ship's outlineOrigin wouldn't do: a factor belongs to the place it was set for.
        if (OutlineScale is not null && OutlineOrigin is null)
        {
            throw new ArgumentException(Invariant($"Module '{Id}': outlineScale needs outlineOrigin."), nameof(OutlineScale));
        }

        // A module's schema is its own; a rotation there would slip past the ship's schema check (D91).
        if (Racks.FirstOrDefault(r => r.Rotation is not null) is { } turned)
        {
            throw new ArgumentException(Invariant($"Module '{Id}': rack '{turned.Id}' has a rotation, which a module can't say yet."), nameof(Racks));
        }
    }

    /// <summary>The catalog id of <paramref name="shipId"/> with this module: "aurora_mk_ii" and "ts_module" give "aurora_mk_ii_ts_module".</summary>
    public string ConfigurationId(string shipId) => $"{shipId}_{Id}";

    /// <summary>
    /// <paramref name="ship"/> with this module, as its ship file would say it: name "RSI Aurora Mk II · TS module", the
    /// racks added up, the ship's manufacturer and Fleetyards slug, so outline and hologram stay the ship's. They lie where
    /// the ship says unless the module says its own places (D90); a module adds racks, it doesn't move the hull.
    /// </summary>
    public ShipDto On(ShipDto ship) => ship with
    {
        Id = ConfigurationId(ship.Id),
        Name = $"{ship.Name} · {Name}",
        TotalScu = ship.TotalScu + Racks.Sum(r => r.CapacityScu),
        Racks = [.. ship.Racks, .. Racks],
        Source = Joined(ship.Source, Source),
        Notes = Joined(ship.Notes, Notes),
        HoloOrigin = HoloOrigin ?? ship.HoloOrigin,
        OutlineOrigin = OutlineOrigin ?? ship.OutlineOrigin,
        OutlineScale = OutlineOrigin is null ? ship.OutlineScale : OutlineScale,
    };

    // The ship's text, then the module's under its name: "Fleetyards API …; TS module: in game (Martin, …)".
    private string? Joined(string? shipText, string? moduleText) => (shipText, moduleText) switch
    {
        (_, null) => shipText,
        (null, _) => $"{Name}: {moduleText}",
        _ => $"{shipText}; {Name}: {moduleText}",
    };
}
