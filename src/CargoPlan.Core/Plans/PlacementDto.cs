using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Plans;

/// <summary>File format of a placement. The item uses the mission file's item format and its checks.</summary>
internal sealed record PlacementDto(string Destination, string RackId, CargoItemDto Item, bool Delivered)
{
    public Placement ToPlacement() => new(Destination, RackId, Item.ToCargoItem(Destination), Delivered);

    public static PlacementDto FromPlacement(Placement placement) =>
        new(placement.Destination, placement.RackId, CargoItemDto.FromCargoItem(placement.Item), placement.Delivered);
}
