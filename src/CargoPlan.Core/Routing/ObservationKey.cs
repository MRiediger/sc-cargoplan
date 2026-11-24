using System.Text;
using CargoPlan.Core.Text;

namespace CargoPlan.Core.Routing;

/// <summary>
/// A template as a scan names it (T13.1, U3): the title's parts, the reward and the commodities, without the template
/// file. Two scans of one key are one variant, whatever places and amounts each drew. The commodities are in the key
/// since T13.3 (K1 of the approval): rank, size, direct and reward alone are shared by templates that differ only in
/// their commodity (four Rookie Direct Medium for 50,500 at Everus), and a template's commodities are fixed.
/// </summary>
/// <param name="Direct">Null where the title leaves it open (the Red Wind card, issue #22); unlike false.</param>
/// <param name="Commodities">Every commodity of the delivery lines; kept in their anchor form
/// (<see cref="OcrText.AnchorForm"/>), sorted and each once, so the order and an OCR misreading of l/1/0 don't matter.</param>
public sealed record ObservationKey(HaulContractor Contractor, HaulRank Rank, HaulSize Size, bool? Direct, long Reward, IReadOnlyList<string> Commodities)
{
    private readonly IReadOnlyList<string> _commodities = Normalized(Commodities);

    /// <inheritdoc cref="ObservationKey" path="/param[@name='Commodities']"/>
    public IReadOnlyList<string> Commodities
    {
        get => _commodities;
        init => _commodities = Normalized(value);
    }

    /// <summary>The system the scan's places are in: its contractor's (<see cref="ContractorSystems"/>).</summary>
    public StarSystem System => ContractorSystems.Of(Contractor);

    // A record compares a list by reference; the key compares the commodities by content.
    public bool Equals(ObservationKey? other) =>
        other is not null
        && Contractor == other.Contractor
        && Rank == other.Rank
        && Size == other.Size
        && Direct == other.Direct
        && Reward == other.Reward
        && Commodities.SequenceEqual(other.Commodities, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Contractor);
        hash.Add(Rank);
        hash.Add(Size);
        hash.Add(Direct);
        hash.Add(Reward);
        foreach (var commodity in Commodities)
        {
            hash.Add(commodity, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Contractor = {Contractor}, Rank = {Rank}, Size = {Size}, Direct = {Direct}, Reward = {Reward}, Commodities = [{string.Join(", ", Commodities)}]");
        return true;
    }

    private static string[] Normalized(IEnumerable<string> commodities) =>
        [.. commodities.Select(OcrText.AnchorForm).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
