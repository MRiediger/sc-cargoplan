namespace CargoPlan.Import.Contracts;

/// <summary>
/// Why a scan may have caught the contract while the game was still typing it in (T10.2b2, D103). Any doubt asks for a
/// second read; a hard one left after it means "still loading", a soft one is taken in as read.
/// </summary>
public enum ContractDoubt
{
    /// <summary>Hard: the objectives heading is there, no delivery was read (S10.0 c06).</summary>
    NoDeliveryRead,

    /// <summary>Hard: a "Deliver 0/9 SCU of …" or "Collect … from" line that reads as nothing: cut off while typing in.</summary>
    ObjectiveLineUnread,

    /// <summary>Soft: no reward was read; the key holds "?" (D101).</summary>
    NoReward,

    /// <summary>Soft: a delivered commodity has no Collect line, though others have one.</summary>
    CommodityWithoutPickup,

    /// <summary>Soft (E1): the drop-off list names more destinations than were read; the legend warns as for a capture.</summary>
    FewerDestinationsThanListed,
}
