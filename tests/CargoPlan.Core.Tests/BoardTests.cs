using CargoPlan.Core.Routing;
using static CargoPlan.Core.Tests.TemplateFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>The board of a hub (T13.3, design §7.3, D119): what the terminal shows for a system, area, rank and faction.</summary>
public sealed class BoardTests
{
    private static readonly HaulTemplate EverusLocal = T([], ["HUR-L1"], rank: HaulRank.Junior);
    private static readonly HaulTemplate EverusMaster = T([], ["HUR-L1"], rank: HaulRank.Master);
    private static readonly HaulTemplate StantonWide = T([], [], areaKey: "Stanton");
    private static readonly HaulTemplate SeraphimLocal = T([], ["CRU-L1"], areaKey: "Stanton2");
    private static readonly HaulTemplate EverusPlanetary = T(["Everus Harbor"], ["HDPC-Cassillo"], type: HaulMissionType.Planetary);
    private static readonly HaulTemplate RedWindHh = RedWind(HaulFaction.Hh, "Solar");
    private static readonly HaulTemplate RedWindCfp = RedWind(HaulFaction.Cfp, "Solar");
    private static readonly HaulTemplate RedWindRegion = RedWind(HaulFaction.Hh, "RegionA");

    private static readonly HaulTemplate[] All = [EverusLocal, EverusMaster, StantonWide, SeraphimLocal, EverusPlanetary, RedWindHh, RedWindCfp, RedWindRegion];

    [Fact]
    public void A_Stanton_board_is_its_local_block_and_the_system_wide_templates_up_to_the_rank()
    {
        var board = Board.For(All, StarSystem.Stanton, "Stanton1", HaulRank.Senior, faction: null);

        board.Templates.Should().Equal(EverusLocal, StantonWide);
        board.Local.Should().Equal(EverusLocal);
        board.Hub.Should().Be("Everus Harbor");
    }

    [Fact]
    public void Every_rank_up_to_the_players_shows()
    {
        Board.For(All, StarSystem.Stanton, "Stanton1", HaulRank.Master, null).Templates.Should().Contain(EverusMaster);
        Board.For(All, StarSystem.Stanton, "Stanton1", HaulRank.Rookie, null).Templates.Should().BeEmpty();
    }

    [Fact]
    public void The_Pyro_board_is_Solar_of_the_players_faction_without_the_regions()
    {
        Board.For(All, StarSystem.Pyro, Board.PyroSolar, HaulRank.Senior, HaulFaction.Hh).Templates.Should().Equal(RedWindHh);
        Board.For(All, StarSystem.Pyro, Board.PyroSolar, HaulRank.Senior, null).Templates.Should().Equal(RedWindHh, RedWindCfp);
        Board.For(All, StarSystem.Pyro, "RegionA", HaulRank.Senior, null).Templates.Should().BeEmpty("the regions wait for a scan that places them");
        Board.For(All, StarSystem.Pyro, Board.PyroSolar, HaulRank.Senior, null).Hub.Should().BeNull();
    }

    [Fact]
    public void A_scan_may_be_any_template_of_its_contractors_boards_whatever_rank()
    {
        Board.Matchable(All, HaulContractor.Covalex).Should().Equal(EverusLocal, EverusMaster, StantonWide, SeraphimLocal);
        Board.Matchable(All, HaulContractor.RedWind).Should().Equal(RedWindHh, RedWindCfp);
    }

    private static HaulTemplate RedWind(HaulFaction faction, string areaKey) =>
        T(["Checkmate"], ["Ruin Station"], reward: null, contractor: HaulContractor.RedWind, system: StarSystem.Pyro, areaKey: areaKey, faction: faction);
}
