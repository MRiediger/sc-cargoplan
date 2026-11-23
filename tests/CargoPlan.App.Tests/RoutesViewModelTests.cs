using CargoPlan.App.Routing;
using CargoPlan.App.Settings;
using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;

namespace CargoPlan.App.Tests;

/// <summary>
/// The Plan tab's view model (T13.2): the port matrix as rows and cells, the filter as buttons that cycle, rank and
/// faction saved in settings.json, a sentence instead of the matrix without hauling data. The matrix is Core's.
/// </summary>
public sealed class RoutesViewModelTests
{
    private static readonly ShipDataVersion Version = new(2026, 11, 2);

    private readonly List<RoutesSettings> _saved = [];

    private static HaulTemplate T(
        string[] pickups,
        string[] dropoffs,
        long? reward = 100_000,
        int min = 10,
        int max = 64,
        HaulRank rank = HaulRank.Senior,
        HaulContractor contractor = HaulContractor.Covalex,
        StarSystem? system = StarSystem.Stanton,
        string areaKey = "Stanton1",
        HaulMissionType type = HaulMissionType.Stellar,
        HaulFaction? faction = null,
        string title = "Senior Rank - Medium Cargo Haul") => new(
        "u", "d", contractor, title, rank, HaulSize.Medium, false, HaulShape.AToB, 1, system, areaKey, type, faction, reward,
        [new HaulLeg("Gold", min, max, CrateSize.Sixteen)], pickups, dropoffs);

    private RoutesViewModel Model(IReadOnlyList<HaulTemplate> templates, RoutesSettings? settings = null) =>
        new(new HaulingLoad.Loaded(new HaulingData(templates, Positions.Empty), []), Version, settings ?? RoutesSettings.Default, _saved.Add);

    private static PortCellView? Cell(RoutesViewModel model, string pickup, string dropoff) =>
        model.Rows.Single(r => r.Pickup == pickup).Cells[model.Columns.ToList().IndexOf(dropoff)];

    [Fact]
    public void Rows_are_pickups_columns_drop_offs_and_a_cell_reads_count_reward_and_scu()
    {
        var model = Model([T(["A"], ["B"], reward: 100_000, max: 64), T(["A"], ["B"], reward: 250_250, max: 144), T(["A"], ["C"])]);

        model.HasData.Should().BeTrue();
        model.StatusText.Should().BeNull();
        model.Columns.Should().Equal("B", "C");
        model.Rows.Select(r => r.Pickup).Should().Equal("A");
        Cell(model, "A", "B")!.Text.Should().Be("2 · 350k · 144");
        Cell(model, "A", "C")!.Text.Should().Be("1 · 100k · 64");
        model.Legend.Should().Be("Each cell: contracts · aUEC together · most SCU of one");
    }

    [Fact]
    public void A_cell_without_rewards_shows_a_dash_and_one_with_some_shows_a_plus()
    {
        var redWind = T(["P"], ["Q"], reward: null, max: 600, contractor: HaulContractor.RedWind, system: StarSystem.Pyro, areaKey: "Solar", faction: HaulFaction.Hh);
        var model = Model([redWind]);

        model.NextCommand.Execute("system");

        model.SystemLabel.Should().Be("Pyro");
        Cell(model, "P", "Q")!.Text.Should().Be("1 · – · 600");
        RoutesViewModel.CellText(new PortCell([new(T(["A"], ["B"]), false, false), new(redWind, false, false)])).Should().Be("2 · 100k+ · 600");
    }

    [Theory]
    [InlineData(950L, "950")]
    [InlineData(57_000L, "57k")]
    [InlineData(482_250L, "482k")]
    [InlineData(1_234_567L, "1.2M")]
    public void Rewards_are_short(long reward, string text) => RoutesViewModel.Short(reward).Should().Be(text);

    [Fact]
    public void The_tool_tip_lists_the_templates_and_marks_the_boards_hub()
    {
        var model = Model([T([], ["HUR-L1"], reward: 160_250, min: 90, max: 144, title: "Experienced Rank - Direct Medium Cargo Haul")]);

        var tip = Cell(model, "Everus Harbor", "HUR-L1")!.ToolTip;

        tip.Should().StartWith("Everus Harbor → HUR-L1");
        tip.Should().Contain("Experienced Rank - Direct Medium Cargo Haul · 160,250 aUEC · 90–144 SCU");
        tip.Should().Contain("pickup: hub of the board, not in the game data");
    }

    [Fact]
    public void Templates_without_a_place_are_counted_below_the_matrix()
    {
        Model([T(["A"], [], areaKey: "Stanton"), T(["A"], ["B"])]).WithoutPlaceText
            .Should().Be("1 template not shown: the game data name no pickup or destination for it.");
        Model([T(["A"], [], areaKey: "Stanton"), T(["A"], [], areaKey: "Stanton")]).WithoutPlaceText
            .Should().Be("2 templates not shown: the game data name no pickup or destination for them.");
        Model([T(["A"], ["B"])]).WithoutPlaceText.Should().BeNull();
    }

    [Fact]
    public void A_filter_that_lets_nothing_through_says_so()
    {
        var model = Model([T(["A"], ["B"])]);

        model.ShowMedium = false;

        model.Rows.Should().BeEmpty();
        model.EmptyText.Should().Be("No templates for this filter.");
    }

    [Fact]
    public void Rank_and_faction_start_from_the_settings_and_are_saved_on_every_change()
    {
        var model = Model([T(["A"], ["B"], rank: HaulRank.Junior), T(["A"], ["B"], rank: HaulRank.Master)], new RoutesSettings("plan", "senior", "cfp"));

        model.RankLabel.Should().Be("Rank ≤ Senior");
        model.FactionLabel.Should().Be("CFP");
        Cell(model, "A", "B")!.Text.Should().StartWith("1 ·");

        model.NextCommand.Execute("rank");
        model.RankLabel.Should().Be("Rank ≤ Master");
        Cell(model, "A", "B")!.Text.Should().StartWith("2 ·");
        model.PreviousCommand.Execute("faction");

        _saved.Should().Equal(new RoutesSettings("plan", "master", "cfp"), new RoutesSettings("plan", "master", "hh"));
    }

    [Fact]
    public void Every_filter_cycles_round_and_back()
    {
        var model = Model([T(["A"], ["B"])]);

        model.DirectLabel.Should().Be("Direct and not");
        model.NextCommand.Execute("direct");
        model.DirectLabel.Should().Be("Direct only");
        model.PreviousCommand.Execute("direct");
        model.PreviousCommand.Execute("direct");
        model.DirectLabel.Should().Be("No direct");

        model.ContractorLabel.Should().Be("Covalex and Red Wind");
        model.AreaLabel.Should().Be("All areas");
        model.NextCommand.Execute("area");
        model.AreaLabel.Should().Be("Local");
        model.SystemLabel.Should().Be("Stanton");
        model.PreviousCommand.Execute("system");
        model.SystemLabel.Should().Be("Interstellar");
        _saved.Should().BeEmpty("only rank and faction are kept");
    }

    [Fact]
    public void Without_hauling_data_the_tab_says_why_and_shows_no_matrix()
    {
        var none = new RoutesViewModel(new HaulingLoad.None("ships-2026.11.1 has no hauling data"), new ShipDataVersion(2026, 11, 1), RoutesSettings.Default, null);
        var unknown = new RoutesViewModel(new HaulingLoad.UnknownFormat("positions.json doesn't match"), Version, RoutesSettings.Default, null);

        none.HasData.Should().BeFalse();
        none.StatusText.Should().Be("No hauling data in ship data 2026.11.1 – update ship data");
        none.Rows.Should().BeEmpty();
        none.EmptyText.Should().BeNull();
        unknown.StatusText.Should().Be("The hauling data in ship data 2026.11.2 can't be used by this app – see cargoplan.log");
    }
}
