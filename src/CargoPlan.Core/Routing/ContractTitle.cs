namespace CargoPlan.Core.Routing;

/// <summary>
/// What a hauling contract's title says (T13.1): with the reward, a template's identity in a scan. Read whole or not at
/// all, so no part is a guess.
/// </summary>
/// <param name="Contractor">From the form of the title: Covalex "… Rank - … Cargo Haul", Red Wind the other two.</param>
/// <param name="Rank">The rank word.</param>
/// <param name="Size">The size words.</param>
/// <param name="Direct">"Direct" in the title. Null where the form leaves it open: the Red Wind card without the word
/// (issue #22), since no capture shows a direct one.</param>
/// <param name="Area">The level the Red Wind card names; null for the other forms or a word not known.</param>
public sealed record ContractTitle(HaulContractor Contractor, HaulRank Rank, HaulSize Size, bool? Direct, AreaLevel? Area = null);
