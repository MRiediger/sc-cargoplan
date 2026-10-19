namespace CargoPlan.Core.Missions;

/// <summary>
/// File format of a mission JSON in schema version 1: one crate size per item. Read only; the writer always writes
/// version 2 (<see cref="MissionFileDto"/>).
/// </summary>
internal sealed record MissionFileV1Dto(int SchemaVersion, IReadOnlyList<StopDto> Stops)
{
    /// <exception cref="ArgumentException">A value violates the domain rules.</exception>
    public IReadOnlyList<MissionStop> ToMissionStops() =>
        MissionFileDto.CheckedRoute([.. Stops.Select((s, index) => s.ToMissionStop(index + 1))]);
}
