using CargoPlan.Core.Missions;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of a mission in a plan file (schema 3, D42): its stops in the mission file's format (D23), where it came
/// from, and whether it didn't fit. Stops in contract order; the route is the crates' order (<c>LoadPlan.UnloadOrder</c>).
/// </summary>
internal sealed record MissionDto(int Id, bool Unplaced, IReadOnlyList<MissionStopDto> Stops, ContractSourceDto? Contract = null)
{
    /// <exception cref="ArgumentException">A value violates the domain rules.</exception>
    public Mission ToMission() =>
        new(Id, [.. Stops.Select((s, index) => s.ToMissionStop(index + 1))], Contract?.ToContractSource(), Unplaced);

    public static MissionDto FromMission(Mission mission) => new(
        mission.Id,
        mission.Unplaced,
        [.. mission.Stops.OrderBy(s => s.Order).Select(s => MissionStopDto.FromMissionStop(s, withManual: true))],
        mission.Contract is { } contract ? ContractSourceDto.FromContractSource(contract) : null);
}
