using CargoPlan.Core.Models;

namespace CargoPlan.Core.Ships;

/// <summary>File format of a rack position: "port"/"starboard" and "upper"/"lower".</summary>
internal sealed record PositionDto(string Side, string Level)
{
    // Plain strings and an explicit switch: STJ's enum converter would also accept numbers and other casing.
    public RackPosition ToPosition(string rackId) => new(
        Side switch
        {
            "port" => RackSide.Port,
            "starboard" => RackSide.Starboard,
            _ => throw new ArgumentException(
                $"Rack '{rackId}': side '{Side}' is unknown, use \"port\" or \"starboard\".", nameof(Side)),
        },
        Level switch
        {
            "upper" => RackLevel.Upper,
            "lower" => RackLevel.Lower,
            _ => throw new ArgumentException(
                $"Rack '{rackId}': level '{Level}' is unknown, use \"upper\" or \"lower\".", nameof(Level)),
        });
}
