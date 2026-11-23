using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Data tests of the pinned release's hauling files (T13.2): what the build laid next to this assembly. A pin bump can
/// fail them, which is their job (D113); the loader's mechanics are <see cref="HaulingFilesTests"/> (A10). Numbers of
/// ships-2026.11.2, T13.0a and design §7.3, §9.3.
/// </summary>
public sealed class BaselineHaulingDataTests
{
    private static readonly Lazy<HaulingLoad> Baseline = new(() => HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder)));

    private static HaulingData Data => Baseline.Value.Should().BeOfType<HaulingLoad.Loaded>().Which.Data;

    [Fact]
    public void The_pinned_release_loads_every_template_and_position_without_skipping_one()
    {
        var loaded = Baseline.Value.Should().BeOfType<HaulingLoad.Loaded>().Subject;

        loaded.Skipped.Should().BeEmpty();
        loaded.Data.Templates.Should().HaveCountGreaterThanOrEqualTo(587);
        loaded.Data.Positions.All.Should().HaveCount(157);
    }

    [Fact]
    public void Every_pool_name_has_a_position_in_the_templates_system()
    {
        foreach (var template in Data.Templates)
        {
            foreach (var place in template.PickupPool.Concat(template.DropoffPool))
            {
                (template.System is { } system ? Data.Positions.Find(system, place) is not null : Data.Positions.AnySystemHas(place))
                    .Should().BeTrue($"{template.Uuid} names '{place}'");
            }
        }
    }

    [Fact]
    public void No_name_is_there_twice_in_one_system() =>
        Data.Positions.All.GroupBy(p => (p.System, p.Name)).Should().OnlyContain(g => g.Count() == 1);

    // Design §7.3 (T13.0a): the boards as the terminal shows them to a Senior, Covalex stellar.
    [Theory]
    [InlineData(new[] { "Stanton", "Stanton1" }, 92)]
    [InlineData(new[] { "Stanton2" }, 19)]
    [InlineData(new[] { "Stanton3" }, 22)]
    public void The_boards_count_their_templates(string[] areaKeys, int count) =>
        Data.Templates.Count(t => t.Contractor == HaulContractor.Covalex && t.MissionType == HaulMissionType.Stellar
            && areaKeys.Contains(t.AreaKey) && t.Rank <= HaulRank.Senior).Should().Be(count);

    // Design §9.3: the distances the design measured from starmap_positions.json.
    [Theory]
    [InlineData("HUR-L1 Green Glade Station", 1.3)]
    [InlineData("Baijini Point", 22.9)]
    public void Distances_from_Everus_Harbor_match_the_design(string place, double gm)
    {
        var everus = Data.Positions.Find(StarSystem.Stanton, "Everus Harbor")!;

        Positions.DistanceGm(everus, Data.Positions.Find(StarSystem.Stanton, place)!).Should().BeApproximately(gm, 0.1);
    }

    // D121: the hub table is hand-kept; a hub renamed by CIG fails here, not silently in the matrix.
    [Fact]
    public void Every_board_hub_is_a_station_of_Stanton() =>
        BoardHubs.ByArea.Values.Should().OnlyContain(hub => Data.Positions.Find(StarSystem.Stanton, hub) != null);

    // With the hubs, the local stellar blocks have both sides; only the system-wide ones stay without a place (T13.3).
    [Fact]
    public void Only_the_system_wide_stellar_templates_stay_without_a_place_in_Stanton()
    {
        var matrix = PortMatrix.Build(Data.Templates, RouteFilter.Default with { MissionType = HaulMissionType.Stellar });

        matrix.WithoutPlace.Should().Be(75);
        matrix.Pickups.Take(4).Should().Equal("Everus Harbor", "Seraphim Station", "Baijini Point", "Port Tressler");
        matrix.Dropoffs.Take(4).Should().Equal("Everus Harbor", "Seraphim Station", "Baijini Point", "Port Tressler");
        matrix.Dropoffs.Skip(4).First().Should().StartWith("HUR-L1 ", "the L stations follow the hubs, Hurston's first");
    }

    [Fact]
    public void Red_Wind_carries_no_reward_and_Covalex_always_one()
    {
        Data.Templates.Where(t => t.Contractor == HaulContractor.RedWind).Should().OnlyContain(t => t.Reward == null);
        Data.Templates.Where(t => t.Contractor == HaulContractor.Covalex).Should().OnlyContain(t => t.Reward > 0);
    }
}
