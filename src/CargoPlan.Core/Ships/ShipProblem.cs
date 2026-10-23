namespace CargoPlan.Core.Ships;

/// <summary>
/// A ship that didn't load, by its catalog id, so the ship selection can show the reason at its entry (T7.2).
/// </summary>
/// <param name="Message">Names the file and the reason, e.g. "Ship file 'x.json' is invalid: …".</param>
public sealed record ShipProblem(string ShipId, string Message);
