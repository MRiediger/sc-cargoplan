using CargoPlan.Core.Models;

namespace CargoPlan.Core.Missions;

/// <summary>
/// A mission file as read (docs/mission-format.md): the stops as entered, and the fill order if the file names one
/// (schema 3, D72). Without one the plan's fill order stays.
/// </summary>
public sealed record MissionFile(IReadOnlyList<MissionStop> Stops, FillOrder? FillOrder);
