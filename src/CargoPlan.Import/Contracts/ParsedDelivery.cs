namespace CargoPlan.Import.Contracts;

/// <summary>One "Deliver N SCU of commodity to destination" line, names normalised (<see cref="ContractText"/>).</summary>
internal sealed record ParsedDelivery(string Destination, string Commodity, int Scu);
