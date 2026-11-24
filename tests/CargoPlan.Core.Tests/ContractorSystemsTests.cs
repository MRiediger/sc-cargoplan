using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>The system each contractor hauls in (E3 of the T13.3a approval); the data's side is in <see cref="BaselineHaulingDataTests"/>.</summary>
public class ContractorSystemsTests
{
    [Fact]
    public void Every_contractor_has_its_system()
    {
        ContractorSystems.All.Keys.Should().BeEquivalentTo(Enum.GetValues<HaulContractor>(), "a new contractor needs its line in the table");
        ContractorSystems.Of(HaulContractor.Covalex).Should().Be(StarSystem.Stanton);
        ContractorSystems.Of(HaulContractor.RedWind).Should().Be(StarSystem.Pyro);
    }

    [Fact]
    public void A_key_is_in_its_contractors_system()
    {
        var key = new ObservationKey(HaulContractor.RedWind, HaulRank.Member, HaulSize.Medium, null, 532_250, ["Carbon"]);

        key.System.Should().Be(StarSystem.Pyro);
        (key with { Contractor = HaulContractor.Covalex }).System.Should().Be(StarSystem.Stanton);
    }
}
