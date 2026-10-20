using CargoPlan.Core.Models;

namespace CargoPlan.Import.ContractTracker;

/// <summary>What <see cref="ContractTextParser"/> found in one capture's raw OCR text.</summary>
/// <param name="Deliveries">In the order of the text; empty if no delivery line was readable.</param>
/// <param name="MaxCrateSize">From the contract's prose; null if it names none (or no valid crate size).</param>
/// <param name="ListedDestinationCount">Entries of the drop-off list; null if the contract has none.</param>
internal sealed record ParsedContract(
    IReadOnlyList<ParsedDelivery> Deliveries,
    CrateSize? MaxCrateSize,
    int? ListedDestinationCount);
