namespace CargoPlan.Core.Routing;

/// <summary>
/// A template as a scan names it (T13.1, U3): the title's parts and the reward, without the template file. T13.3 resolves
/// it to a template. Two scans of one key are one variant, whatever places and amounts each drew.
/// </summary>
/// <param name="Direct">Null where the title leaves it open (the Red Wind card, issue #22); unlike false.</param>
public sealed record ObservationKey(HaulContractor Contractor, HaulRank Rank, HaulSize Size, bool? Direct, long Reward);
