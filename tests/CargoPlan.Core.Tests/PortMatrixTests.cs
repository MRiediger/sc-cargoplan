using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The port matrix (T13.2, design §8.5) and its filter: pickup × drop-off from the templates' pools, the board's hub where
/// a stellar Covalex pool side is empty (D121). Templates built here, not read (A10).
/// </summary>
public sealed class PortMatrixTests
{
    private static int _uuid;

    private static HaulTemplate T(
        string[] pickups,
        string[] dropoffs,
        long? reward = 100_000,
        int maxScu = 64,
        HaulContractor contractor = HaulContractor.Covalex,
        HaulRank rank = HaulRank.Senior,
        HaulSize size = HaulSize.Medium,
        bool direct = false,
        StarSystem? system = StarSystem.Stanton,
        string areaKey = "Stanton1",
        HaulMissionType type = HaulMissionType.Stellar,
        HaulFaction? faction = null,
        HaulShape shape = HaulShape.AToB) => new(
        $"t{Interlocked.Increment(ref _uuid)}", "debug", contractor, "title", rank, size, direct, shape, 1, system, areaKey, type, faction,
        reward, [new HaulLeg("Gold", 1, maxScu, CrateSize.Sixteen)], pickups, dropoffs);

    private static readonly RouteFilter All = RouteFilter.Default;

    [Fact]
    public void A_template_counts_once_in_every_cell_of_its_pools()
    {
        var matrix = PortMatrix.Build([T(["A"], ["B", "C"]), T(["A", "D"], ["B"])], All);

        matrix.Pickups.Should().Equal("A", "D");
        matrix.Dropoffs.Should().Equal("B", "C");
        matrix.Cell("A", "B")!.Count.Should().Be(2);
        matrix.Cell("A", "C")!.Count.Should().Be(1);
        matrix.Cell("D", "B")!.Count.Should().Be(1);
        matrix.Cell("D", "C").Should().BeNull();
        matrix.WithoutPlace.Should().Be(0);
    }

    [Fact]
    public void Rows_and_columns_are_alphabetical_whatever_the_case()
    {
        var matrix = PortMatrix.Build([T(["port Tressler", "Baijini Point"], ["everus Harbor", "ARC-L1"])], All);

        matrix.Pickups.Should().Equal("Baijini Point", "port Tressler");
        matrix.Dropoffs.Should().Equal("ARC-L1", "everus Harbor");
    }

    [Fact]
    public void A_cell_sums_the_known_rewards_and_counts_the_unknown_ones()
    {
        var matrix = PortMatrix.Build(
        [
            T(["A"], ["B"], reward: 100_000, maxScu: 64),
            T(["A"], ["B"], reward: 250_000, maxScu: 144),
            T(["A"], ["C"], reward: null, maxScu: 600, contractor: HaulContractor.RedWind, faction: HaulFaction.Hh),
        ], All with { Contractor = null });

        var known = matrix.Cell("A", "B")!;
        known.RewardSum.Should().Be(350_000);
        known.UnknownRewards.Should().Be(0);
        known.MaxScu.Should().Be(144);

        var redWind = matrix.Cell("A", "C")!;
        redWind.RewardSum.Should().BeNull("Red Wind has no reward in the game data, never estimated");
        redWind.UnknownRewards.Should().Be(1);
        redWind.MaxScu.Should().Be(600);
    }

    [Fact]
    public void The_most_a_template_carries_is_the_sum_of_its_legs()
    {
        var template = T(["A"], ["B"]) with
        {
            Legs = [new HaulLeg("Gold", 10, 40, CrateSize.Four), new HaulLeg("Tin", 5, 30, CrateSize.Two)],
        };

        PortMatrix.Build([template], All).Cell("A", "B")!.MaxScu.Should().Be(70);
    }

    [Fact]
    public void A_place_never_leads_to_itself()
    {
        var matrix = PortMatrix.Build([T(["A", "B"], ["B"])], All);

        matrix.Cell("B", "B").Should().BeNull();
        matrix.Cell("A", "B")!.Count.Should().Be(1);
        matrix.Pickups.Should().Equal("A");
    }

    // D121: in the local blocks the hub side of the pool is empty; the board's hub stands in for it, marked.
    [Theory]
    [InlineData("Stanton1", "Everus Harbor")]
    [InlineData("Stanton2", "Seraphim Station")]
    [InlineData("Stanton3", "Baijini Point")]
    [InlineData("Stanton4", "Port Tressler")]
    public void An_empty_side_of_a_local_stellar_block_is_its_boards_hub(string areaKey, string hub)
    {
        // "X-L…" sorts after every hub's name.
        var matrix = PortMatrix.Build(
        [
            T([], ["X-L1", "X-L2"], areaKey: areaKey, shape: HaulShape.SingleToMulti),
            T(["X-L3"], [], areaKey: areaKey, shape: HaulShape.MultiToSingle),
        ], All);

        matrix.Pickups.Should().Equal(hub, "X-L3");
        matrix.Dropoffs.Should().Equal(hub, "X-L1", "X-L2");
        var outbound = matrix.Cell(hub, "X-L1")!.Templates.Should().ContainSingle().Subject;
        outbound.PickupFromHub.Should().BeTrue();
        outbound.DropoffFromHub.Should().BeFalse();
        matrix.Cell("X-L3", hub)!.Templates.Single().DropoffFromHub.Should().BeTrue();
        matrix.WithoutPlace.Should().Be(0);
    }

    [Fact]
    public void Without_a_hub_an_empty_side_keeps_the_template_out_and_counts_it()
    {
        var matrix = PortMatrix.Build(
        [
            T(["Everus Harbor"], [], areaKey: "Stanton"),                                  // system-wide: any hub
            T([], ["L1"], type: HaulMissionType.Planetary),                                 // no evidence where its board stands
            T([], ["L1"], type: HaulMissionType.Local),
            T(["A"], ["B"]),
        ], All);

        matrix.Cell("A", "B").Should().NotBeNull();
        matrix.WithoutPlace.Should().Be(3);
        matrix.Pickups.Should().Equal("A");

        PortMatrix.Build([T(["Checkmate"], [], contractor: HaulContractor.RedWind, system: StarSystem.Pyro, areaKey: "Solar")], All with { System = StarSystem.Pyro })
            .WithoutPlace.Should().Be(1);
    }

    [Fact]
    public void The_filter_counts_only_what_it_lets_through_also_below_the_matrix()
    {
        var matrix = PortMatrix.Build([T(["A"], [], areaKey: "Stanton", rank: HaulRank.Master), T(["A"], ["B"])], All with { MaxRank = HaulRank.Senior });

        matrix.WithoutPlace.Should().Be(0);
        matrix.Templates.Should().Be(1);
    }

    [Fact]
    public void Rank_shows_every_rank_up_to_the_chosen_one()
    {
        HaulTemplate[] templates = [T(["A"], ["B"], rank: HaulRank.Trainee), T(["A"], ["B"], rank: HaulRank.Senior), T(["A"], ["B"], rank: HaulRank.Master)];

        PortMatrix.Build(templates, All with { MaxRank = HaulRank.Senior }).Cell("A", "B")!.Count.Should().Be(2);
        PortMatrix.Build(templates, All with { MaxRank = HaulRank.Trainee }).Cell("A", "B")!.Count.Should().Be(1);
        PortMatrix.Build(templates, All).Cell("A", "B")!.Count.Should().Be(3);
    }

    [Fact]
    public void Sizes_are_a_choice_of_several()
    {
        HaulTemplate[] templates = [T(["A"], ["B"], size: HaulSize.Xs), T(["A"], ["B"], size: HaulSize.Medium), T(["A"], ["B"], size: HaulSize.Bulk)];

        PortMatrix.Build(templates, All with { Sizes = new HashSet<HaulSize> { HaulSize.Xs, HaulSize.Bulk } }).Cell("A", "B")!.Count.Should().Be(2);
        PortMatrix.Build(templates, All with { Sizes = new HashSet<HaulSize>() }).Pickups.Should().BeEmpty();
    }

    [Theory]
    [InlineData(DirectFilter.All, 2)]
    [InlineData(DirectFilter.Only, 1)]
    [InlineData(DirectFilter.Without, 1)]
    public void Direct_all_only_or_without(DirectFilter direct, int count) =>
        PortMatrix.Build([T(["A"], ["B"], direct: true), T(["A"], ["B"], direct: false)], All with { Direct = direct })
            .Cell("A", "B")!.Count.Should().Be(count);

    [Fact]
    public void Contractor_faction_mission_type_and_system_narrow_it_down()
    {
        HaulTemplate[] templates =
        [
            T(["A"], ["B"]),
            T(["A"], ["B"], type: HaulMissionType.Planetary),
            T(["P"], ["Q"], contractor: HaulContractor.RedWind, system: StarSystem.Pyro, areaKey: "Solar", faction: HaulFaction.Hh, reward: null),
            T(["P"], ["Q"], contractor: HaulContractor.RedWind, system: StarSystem.Pyro, areaKey: "Solar", faction: HaulFaction.Cfp, reward: null),
            T(["X"], ["Y"], system: null, areaKey: "Interstellar", type: HaulMissionType.Interstellar),
        ];

        PortMatrix.Build(templates, All).Pickups.Should().Equal("A");
        PortMatrix.Build(templates, All).Cell("A", "B")!.Count.Should().Be(2);
        PortMatrix.Build(templates, All with { MissionType = HaulMissionType.Stellar }).Cell("A", "B")!.Count.Should().Be(1);
        PortMatrix.Build(templates, All with { System = StarSystem.Pyro }).Cell("P", "Q")!.Count.Should().Be(2);
        PortMatrix.Build(templates, All with { System = StarSystem.Pyro, Faction = HaulFaction.Cfp }).Cell("P", "Q")!.Count.Should().Be(1);
        PortMatrix.Build(templates, All with { System = StarSystem.Pyro, Contractor = HaulContractor.Covalex }).Pickups.Should().BeEmpty();
        PortMatrix.Build(templates, All with { System = null }).Pickups.Should().Equal("X");

        // A faction narrows Red Wind only; Covalex has none.
        PortMatrix.Build(templates, All with { Faction = HaulFaction.Hh }).Cell("A", "B")!.Count.Should().Be(2);
    }

    [Fact]
    public void The_default_filter_is_Stanton_every_rank_size_type_and_contractor()
    {
        RouteFilter.Default.Should().BeEquivalentTo(new
        {
            MaxRank = HaulRank.Master,
            Sizes = Enum.GetValues<HaulSize>(),
            Direct = DirectFilter.All,
            Contractor = (HaulContractor?)null,
            Faction = (HaulFaction?)null,
            MissionType = (HaulMissionType?)null,
            System = (StarSystem?)StarSystem.Stanton,
        });
    }
}
