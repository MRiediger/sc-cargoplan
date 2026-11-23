using CargoPlan.Core.Models;

namespace CargoPlan.Core.Routing;

/// <summary>One pickup and one drop-off, one pickup and n drop-offs, or n pickups and one drop-off (hauling-data-format.md, <c>shape</c>).</summary>
public enum HaulShape
{
    AToB,
    SingleToMulti,
    MultiToSingle,
}

/// <summary>Area of operations of a template (design §1.1); the file holds all four, the board uses stellar (T13.3).</summary>
public enum HaulMissionType
{
    Local,
    Planetary,
    Stellar,
    Interstellar,
}

/// <summary>Red Wind's side in Pyro: Headhunters or Citizens for Prosperity.</summary>
public enum HaulFaction
{
    Hh,
    Cfp,
}

/// <summary>A star system of the positions; coordinates are per system, a distance across systems is not defined.</summary>
public enum StarSystem
{
    Stanton,
    Pyro,
    Nyx,
}

/// <summary>One hauling order of a template: the SCU band an instance is drawn from and its largest crate.</summary>
public sealed record HaulLeg(string Commodity, int MinScu, int MaxScu, CrateSize Container);

/// <summary>
/// One hauling template of the game (docs/hauling-data-format.md, schema 1): fixed data, offered on a board or not. An
/// offer is one instance, its SCU and places drawn from the bands and pools.
/// </summary>
/// <param name="System">The board's system; null for <c>Interstellar</c>.</param>
/// <param name="Reward">aUEC; null where the game data have none (all of Red Wind stellar), never estimated.</param>
/// <param name="PickupPool">Starmap names of <see cref="Positions"/>; may be empty.</param>
/// <param name="DropoffPool">Starmap names of <see cref="Positions"/>; may be empty.</param>
public sealed record HaulTemplate(
    string Uuid,
    string DebugName,
    HaulContractor Contractor,
    string Title,
    HaulRank Rank,
    HaulSize Size,
    bool Direct,
    HaulShape Shape,
    int Stops,
    StarSystem? System,
    string AreaKey,
    HaulMissionType MissionType,
    HaulFaction? Faction,
    long? Reward,
    IReadOnlyList<HaulLeg> Legs,
    IReadOnlyList<string> PickupPool,
    IReadOnlyList<string> DropoffPool)
{
    /// <summary>The most an instance can carry: the sum of the legs' upper bands.</summary>
    public int MaxScu => Legs.Sum(l => l.MaxScu);
}
