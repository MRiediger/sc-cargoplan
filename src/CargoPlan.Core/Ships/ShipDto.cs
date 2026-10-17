using CargoPlan.Core.Models;

namespace CargoPlan.Core.Ships;

/// <summary>File format of a ship JSON (schema version 1). Mapped to <see cref="Ship"/> via <see cref="ToShip"/>.</summary>
internal sealed record ShipDto(int SchemaVersion, string Id, string Name, int TotalScu, IReadOnlyList<RackDto> Racks)
{
    /// <exception cref="ArgumentException">A value violates the domain rules.</exception>
    public Ship ToShip()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id, nameof(Id));
        ArgumentException.ThrowIfNullOrWhiteSpace(Name, nameof(Name));
        if (Racks.Count == 0)
        {
            throw new ArgumentException($"Ship '{Id}' has no racks.", nameof(Racks));
        }

        RackDto.ThrowOnDuplicateIds(Racks.Select(r => r.Id), $"ship '{Id}'");

        // Array order defines the index and the rack order: first rack = Rack 1.
        return new Ship(Id, Name, TotalScu, [.. Racks.Select((r, index) => r.ToRack(index))]);
    }
}
