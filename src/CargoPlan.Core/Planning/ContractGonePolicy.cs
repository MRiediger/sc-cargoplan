namespace CargoPlan.Core.Planning;

/// <summary>What a sync does with a contract ContractTracker no longer lists (D81, settings <c>contractTracker.onContractGone</c>).</summary>
public enum ContractGonePolicy
{
    /// <summary>It stays in the plan as it is, marked gone; the player deletes it.</summary>
    Keep,

    /// <summary>
    /// Its planned and delivered deliveries leave when it goes, its loaded ones never; later it leaves only once nothing of
    /// it is planned or loaded.
    /// </summary>
    Remove,
}
