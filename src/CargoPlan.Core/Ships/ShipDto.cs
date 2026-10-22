using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Ships;

/// <summary>
/// File format of a ship JSON (docs/ship-format.md). Mapped to <see cref="Ship"/> via <see cref="ToShip"/>. The fields
/// after <see cref="Racks"/> are optional (D56), see <see cref="RackDto"/>.
/// </summary>
internal sealed record ShipDto(
    int SchemaVersion, string Id, string Name, int TotalScu, IReadOnlyList<RackDto> Racks,
    string? Manufacturer = null, string? FleetyardsSlug = null, string? Source = null, string? Notes = null)
{
    /// <exception cref="ArgumentException">A value violates the domain rules.</exception>
    public Ship ToShip()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id, nameof(Id));
        ArgumentException.ThrowIfNullOrWhiteSpace(Name, nameof(Name));
        ThrowIfBlank(Manufacturer, "manufacturer");
        ThrowIfBlank(FleetyardsSlug, "fleetyardsSlug");
        ThrowIfBlank(Source, "source");
        ThrowIfBlank(Notes, "notes");
        if (Racks.Count == 0)
        {
            throw new ArgumentException($"Ship '{Id}' has no racks.", nameof(Racks));
        }

        RackDto.ThrowOnDuplicateIds(Racks.Select(r => r.Id), $"ship '{Id}'");

        // Array order defines the index and the rack order: first rack = Rack 1.
        IReadOnlyList<Rack> racks = [.. Racks.Select((r, index) => r.ToRack(index))];
        ThrowOnOverlap(racks);
        return new Ship(Id, Name, TotalScu, racks, Manufacturer, FleetyardsSlug, Source, Notes);
    }

    // An optional field is either left out or says something; "" would show as an empty label.
    private void ThrowIfBlank(string? value, string field)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Ship '{Id}': {field} is empty; leave the field out instead.", field);
        }
    }

    // Checked here, not in Ship: geometry matters for drawing, and ships built in tests may ignore it (D14).
    private void ThrowOnOverlap(IReadOnlyList<Rack> racks)
    {
        for (var i = 0; i < racks.Count; i++)
        {
            for (var j = i + 1; j < racks.Count; j++)
            {
                var (a, b) = (racks[i], racks[j]);
                if (a.Bounds.Overlaps(b.Bounds))
                {
                    throw new ArgumentException(Invariant(
                        $"Ship '{Id}': Rack {a.Number} ('{a.Id}') at {a.Origin} and Rack {b.Number} ('{b.Id}') at {b.Origin} overlap."),
                        nameof(Racks));
                }
            }
        }
    }
}
