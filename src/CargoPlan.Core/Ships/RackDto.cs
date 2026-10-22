using CargoPlan.Core.Models;
using static System.FormattableString;
using Axis = CargoPlan.Core.Models.StackAxis;

namespace CargoPlan.Core.Ships;

/// <summary>
/// File format of a rack. The index is not stored; it comes from the position in the array. <see cref="MaxCrateSize"/>
/// is optional (D56): a parameter with a default is not required, even with RespectRequiredConstructorParameters.
/// Source: https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions.respectrequiredconstructorparameters
/// </summary>
internal sealed record RackDto(
    string Id, int CapacityScu, GridDto Grid, PositionDto Position, IReadOnlyList<int> Origin, string StackAxis, int? MaxCrateSize = null)
{
    private const int OriginAxes = 3;

    public Rack ToRack(int index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id, nameof(Id));

        // Rack validates this too, but without naming the rack.
        if (CapacityScu <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CapacityScu), CapacityScu, $"Rack '{Id}' needs a positive capacityScu.");
        }

        return new Rack(Id, index, CapacityScu, Grid.ToDimensions(Id), Position.ToPosition(Id), ToOrigin(), ToStackAxis(index), ToMaxCrateSize(index));
    }

    private CrateSize? ToMaxCrateSize(int index) =>
        MaxCrateSize is not { } max || Enum.IsDefined((CrateSize)max)
            ? (CrateSize?)MaxCrateSize
            : throw new ArgumentException(
                Invariant($"Rack {index + 1} ('{Id}'): maxCrateSize {max} is not a crate size, use {string.Join(", ", Enum.GetValues<CrateSize>().Select(s => (int)s))}."),
                nameof(MaxCrateSize));

    // STJ already rejects null and non-integers; only the length is left.
    private ShipPoint ToOrigin() =>
        Origin is [var x, var y, var z]
            ? new ShipPoint(x, y, z)
            : throw new ArgumentException(
                Invariant($"Rack '{Id}': origin needs {OriginAxes} values [x, y, z], got {Origin.Count}."), nameof(Origin));

    // Plain strings and an explicit switch, like PositionDto: STJ's enum converter can't read "+y".
    private Axis ToStackAxis(int index) => StackAxis switch
    {
        "+x" => Axis.PlusX,
        "-x" => Axis.MinusX,
        "+y" => Axis.PlusY,
        "-y" => Axis.MinusY,
        "+z" => Axis.PlusZ,
        "-z" => Axis.MinusZ,
        _ => throw new ArgumentException(
            Invariant($"Rack {index + 1} ('{Id}'): stackAxis '{StackAxis}' is unknown, use \"+x\", \"-x\", \"+y\", \"-y\", \"+z\" or \"-z\"."),
            nameof(StackAxis)),
    };

    internal static void ThrowOnDuplicateIds(IEnumerable<string> ids, string owner)
    {
        var duplicate = ids.GroupBy(id => id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Id '{duplicate.Key}' is used more than once in {owner}.");
        }
    }
}
