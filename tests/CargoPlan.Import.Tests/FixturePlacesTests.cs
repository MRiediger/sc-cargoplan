using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using CargoPlan.Import.Contracts;

namespace CargoPlan.Import.Tests;

/// <summary>
/// Data test (T13.3): every place the RawText fixtures name, as the parser reads it, against the positions of the pinned
/// release (D113). A pin bump or a parser change can fail it, which is its job. Places the parser leaves torn stay
/// unresolved by name; they are listed here, not guessed.
/// </summary>
public sealed class FixturePlacesTests
{
    private static readonly Lazy<Positions> Pinned = new(() =>
        HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder)).Should().BeOfType<HaulingLoad.Loaded>().Which.Data.Positions);

    // Fixture → places the pin can't resolve, as read: names torn by the line break or the capture, which B1 doesn't join.
    private static readonly Dictionary<string, string[]> Unresolved = new(StringComparer.Ordinal)
    {
        [ImportFixtures.StimsAcceptedTab] = ["Everus H"],
        [ImportFixtures.WasteTornAbov] = ["HuR-L5 High Course St", "HuR-L4 Melodic Fiel", "HUR-L1 Green Gle", "HUR-L3 Thundg"],
        [ImportFixtures.WasteTornAboy] = ["HUR-L1 Green Glad"],
    };

    [Theory]
    [MemberData(nameof(ContractTextParserTests.AllRawTextFixtures), MemberType = typeof(ContractTextParserTests))]
    public void Every_place_of_a_fixture_resolves_or_is_listed_as_torn(string fixture)
    {
        var scanned = ScannedContract.Read(ImportFixtures.RawText(fixture));
        var system = scanned.Title?.Contractor == HaulContractor.RedWind ? StarSystem.Pyro : StarSystem.Stanton;
        string[] places = [.. scanned.Pickups.Concat(scanned.Stops.Select(s => s.Destination)).Distinct(StringComparer.Ordinal)];

        string[] unresolved = [.. places.Where(p => Pinned.Value.Resolve(system, p) is null)];

        unresolved.Should().BeEquivalentTo(Unresolved.GetValueOrDefault(fixture, []),
            $"{fixture} reads {string.Join(" | ", places.Select(p => $"{p} → {Pinned.Value.Resolve(system, p)?.Name ?? "-"}"))}");
    }
}
