namespace CargoPlan.Import.Contracts;

/// <summary>
/// Which mobiGlas view a contract was read from (T10.2a). The game rolls the amounts again when a contract is accepted
/// (S10.0: 6 of 10 pairs), so only the accepted view's SCU are the ones the freight elevator hands out.
/// </summary>
public enum ContractView
{
    /// <summary>Neither view's signals, or both: never a guess.</summary>
    Unknown,

    /// <summary>The contract offers ("ACCEPT OFFER", "Contract Availability"): its amounts are an estimate.</summary>
    Offer,

    /// <summary>An accepted contract ("ABANDON", "SHARE TRACK" / "SHARE UNTRACK", "Contract Deadline").</summary>
    Accepted,
}
