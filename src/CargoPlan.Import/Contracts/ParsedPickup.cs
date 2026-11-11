namespace CargoPlan.Import.Contracts;

/// <summary>One "Collect commodity from place." line (T10.2a), names normalised (<see cref="ContractText"/>).</summary>
internal sealed record ParsedPickup(string Commodity, string Place);
