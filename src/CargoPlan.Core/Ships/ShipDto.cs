using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Ships;

/// <summary>
/// File format of a ship JSON (docs/ship-format.md). Mapped to <see cref="Ship"/> via <see cref="ToShip"/>. The fields
/// after <see cref="Racks"/> are optional (D56), see <see cref="RackDto"/>. <see cref="FleetyardsSlug"/> is
/// <see cref="ModelKey"/>'s name before v0.3.0, still read for files written by hand (D94); never both.
/// </summary>
internal sealed record ShipDto(
    int SchemaVersion, string Id, string Name, int TotalScu, IReadOnlyList<RackDto> Racks,
    string? Manufacturer = null, string? ModelKey = null, string? Source = null, string? Notes = null,
    IReadOnlyList<int>? HoloOffset = null,
    IReadOnlyList<double>? HoloOrigin = null, IReadOnlyList<double>? OutlineOrigin = null, double? OutlineScale = null,
    string? FleetyardsSlug = null)
{
    /// <exception cref="ArgumentException">A value violates the domain rules.</exception>
    public Ship ToShip()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id, nameof(Id));
        ArgumentException.ThrowIfNullOrWhiteSpace(Name, nameof(Name));
        ThrowIfBlank(Manufacturer, "manufacturer");
        ThrowIfBlank(ModelKey, "modelKey");
        ThrowIfBlank(FleetyardsSlug, "fleetyardsSlug");
        if (ModelKey is not null && FleetyardsSlug is not null)
        {
            throw new ArgumentException($"Ship '{Id}': modelKey and fleetyardsSlug exclude each other; fleetyardsSlug is modelKey's old name, keep modelKey (D94).", nameof(FleetyardsSlug));
        }

        ThrowIfBlank(Source, "source");
        ThrowIfBlank(Notes, "notes");
        if (Racks.Count == 0)
        {
            throw new ArgumentException($"Ship '{Id}' has no racks.", nameof(Racks));
        }

        RackDto.ThrowOnDuplicateIds(Racks.Select(r => r.Id), $"ship '{Id}'");
        if (SchemaVersion <= ShipLoader.NoRotationSchemaVersion && Racks.FirstOrDefault(r => r.Rotation is not null) is { } turned)
        {
            throw new ArgumentException(
                Invariant($"Ship '{Id}': rack '{turned.Id}': rotation is new in schema {ShipLoader.NoRotationSchemaVersion + 1} (the file says {SchemaVersion})."), nameof(Racks));
        }

        // Array order defines the index and the rack order: first rack = Rack 1.
        IReadOnlyList<Rack> racks = [.. Racks.Select((r, index) => r.ToRack(index))];
        ThrowOnOverlap(racks);
        if (HoloOffset is not null && HoloOrigin is not null)
        {
            throw new ArgumentException($"Ship '{Id}': holoOffset and holoOrigin exclude each other; holoOrigin says where the hologram lies (D90).", nameof(HoloOffset));
        }

        ThrowOnBadOutlineScale();
        return new Ship(
            Id, Name, TotalScu, racks, Manufacturer, ModelKey ?? FleetyardsSlug, Source, Notes, ToHoloOffset(),
            ToCoordinates(HoloOrigin, "holoOrigin"), ToCoordinates(OutlineOrigin, "outlineOrigin"), OutlineScale);
    }

    private void ThrowOnBadOutlineScale()
    {
        if (OutlineScale is not { } scale)
        {
            return;
        }

        if (OutlineOrigin is null)
        {
            throw new ArgumentException($"Ship '{Id}': outlineScale needs outlineOrigin.", nameof(OutlineScale));
        }

        if (scale <= 0)
        {
            throw new ArgumentException(Invariant($"Ship '{Id}': outlineScale must be greater than 0, got {scale}."), nameof(OutlineScale));
        }
    }

    // STJ already rejects null entries, strings and what isn't a finite number; only the length is left.
    private ShipCoordinates? ToCoordinates(IReadOnlyList<double>? values, string field) => values switch
    {
        null => null,
        [var x, var y, var z] => new ShipCoordinates(x, y, z),
        _ => throw new ArgumentException(Invariant($"Ship '{Id}': {field} needs 3 values [x, y, z], got {values.Count}."), field),
    };

    // Whole cells like an origin; STJ already rejects null entries and non-integers, only the length is left (K5.3b).
    private ShipPoint? ToHoloOffset() => HoloOffset switch
    {
        null => null,
        [var x, var y, var z] => new ShipPoint(x, y, z),
        _ => throw new ArgumentException(Invariant($"Ship '{Id}': holoOffset needs 3 values [x, y, z], got {HoloOffset.Count}."), nameof(HoloOffset)),
    };

    // An optional field is either left out or says something; "" would show as an empty label.
    private void ThrowIfBlank(string? value, string field)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Ship '{Id}': {field} is empty; leave the field out instead.", field);
        }
    }

    // Checked here, not in Ship: geometry matters for drawing, and ships built in tests may ignore it (D14). A turned rack
    // doesn't lie in its box, so racks that turn past each other may share one; only the picture can tell (D91).
    private void ThrowOnOverlap(IReadOnlyList<Rack> racks)
    {
        for (var i = 0; i < racks.Count; i++)
        {
            for (var j = i + 1; j < racks.Count; j++)
            {
                var (a, b) = (racks[i], racks[j]);
                if (a.Rotation is null && b.Rotation is null && a.Bounds.Overlaps(b.Bounds))
                {
                    throw new ArgumentException(Invariant(
                        $"Ship '{Id}': Rack {a.Number} ('{a.Id}') at {a.Origin} and Rack {b.Number} ('{b.Id}') at {b.Origin} overlap."),
                        nameof(Racks));
                }
            }
        }
    }
}
