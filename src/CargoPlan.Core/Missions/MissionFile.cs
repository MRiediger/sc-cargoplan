using CargoPlan.Core.Models;

namespace CargoPlan.Core.Missions;

/// <summary>
/// A mission file as read (docs/mission-format.md): the stops as entered, and the fill order (schema 3, D72), row axis and
/// grouping (schema 4, D88, D89) if the file names them. What it doesn't name stays as it is in the editor.
/// </summary>
public sealed record MissionFile(IReadOnlyList<MissionStop> Stops, FillOrder? FillOrder, FillRows? FillRows = null, PlanGrouping? Grouping = null);
