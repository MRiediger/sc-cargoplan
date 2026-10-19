using static System.FormattableString;

namespace CargoPlan.Core.Missions;

/// <summary>File format of a stop (schema version 2). The order is not stored; it comes from the position in the array.</summary>
internal sealed record MissionStopDto(string Destination, IReadOnlyList<MissionItemDto> Items)
{
    public MissionStop ToMissionStop(int order)
    {
        if (string.IsNullOrWhiteSpace(Destination))
        {
            throw new ArgumentException(Invariant($"Stop {order} has no destination."), nameof(Destination));
        }

        // An empty stop is always a mistake in a file.
        if (Items.Count == 0)
        {
            throw new ArgumentException($"Stop '{Destination}' has no items.", nameof(Items));
        }

        return new MissionStop(Destination, order, [.. Items.Select(i => i.ToMissionItem(Destination))]);
    }

    public static MissionStopDto FromMissionStop(MissionStop stop) =>
        new(stop.Destination, [.. stop.Items.Select(MissionItemDto.FromMissionItem)]);
}
