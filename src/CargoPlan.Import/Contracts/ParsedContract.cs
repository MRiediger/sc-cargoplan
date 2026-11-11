using CargoPlan.Core.Models;

namespace CargoPlan.Import.Contracts;

/// <summary>What <see cref="ContractTextParser"/> found in one capture's raw OCR text.</summary>
/// <param name="Deliveries">In the order of the text; empty if no delivery line was readable.</param>
/// <param name="MaxCrateSize">From the contract's prose; null if it names none (or no valid crate size).</param>
/// <param name="ListedDestinationCount">Entries of the drop-off list; null if the contract has none.</param>
/// <param name="Reward">"Reward" with its amount at the start of the text or on a line of its own; null if missing or not
/// readable as a number.</param>
/// <param name="Pickups">The Collect lines in the order of the text, a place named again kept (T10.2a); empty without any.</param>
/// <param name="View">Offers or accepted, from the buttons and the availability or deadline line (T10.2a).</param>
/// <param name="HasObjectives">The text has the "PRIMARY OBJECTIVES" heading (T10.2b2).</param>
/// <param name="Unread">Objective lines that read as nothing: a delivery's count without its destination, a Collect line
/// without its place (T10.2b2); empty for a text all there.</param>
internal sealed record ParsedContract(
    IReadOnlyList<ParsedDelivery> Deliveries,
    CrateSize? MaxCrateSize,
    int? ListedDestinationCount,
    long? Reward,
    IReadOnlyList<ParsedPickup> Pickups,
    ContractView View,
    bool HasObjectives = false,
    IReadOnlyList<string>? Unread = null);
