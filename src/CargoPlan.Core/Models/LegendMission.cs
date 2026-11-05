namespace CargoPlan.Core.Models;

/// <summary>A mission as the legend shows it (B6, D69): its crates and its lines, one per destination.</summary>
public sealed record LegendMission(int Mission, IReadOnlyList<Crate> Crates, IReadOnlyList<LegendLine> Lines);

/// <summary>One line of a mission in the legend: its crates for one destination.</summary>
public sealed record LegendLine(string Destination, IReadOnlyList<Crate> Crates);
