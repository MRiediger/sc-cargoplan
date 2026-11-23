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

    private RoutesViewModel Model(IReadOnlyList<HaulTemplate> templates, RoutesSettings? settings = null, ObservedEdges? observed = null) =>
        new(new HaulingLoad.Loaded(new HaulingData(templates, Positions.Empty), []), Version, settings ?? RoutesSettings.Default, _saved.Add, observed);

    private static readonly DateTime Monday = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Positions Places = new(
    [
        .. new[] { "Everus Harbor", "Baijini Point" }.Select(n => new StarPosition(n, n, StarSystem.Stanton, "Manmade", null, 0, 0, 0, [])),
        .. new[] { "P", "Q" }.Select(n => new StarPosition(n, n, StarSystem.Pyro, "Manmade", null, 0, 0, 0, [])),
    ]);

    // Seen twice: a Senior Medium for the reward, Gold, from the first place to the second.
    private static ObservedEdges Seen(HaulTemplate template, HaulContractor contractor, long reward, string from, string to)
    {
        var key = new ObservationKey(contractor, HaulRank.Senior, HaulSize.Medium, contractor == HaulContractor.Covalex ? false : null, reward, ["Gold"]);
        var scan = new ObservedScan(key, null, [from], [new ObservedDelivery(to, "Gold", 30)], Monday);
        return ObservedEdges.From(new Observations([Observation.Of(scan).With(scan)]), [template], Places);
    }

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
        Cell(model, "A", "B")!.Text.Should().Be("2 · 350k");
        Cell(model, "A", "C")!.Text.Should().Be("1 · 100k");
        model.Legend.Should().Be("Each cell: contracts · aUEC together; ● seen in your scans");
    }

    [Fact]
    public void A_cell_without_rewards_shows_a_dash_and_one_with_some_shows_a_plus()
    {
        var redWind = T(["P"], ["Q"], reward: null, max: 600, contractor: HaulContractor.RedWind, system: StarSystem.Pyro, areaKey: "Solar", faction: HaulFaction.Hh);
        var model = Model([redWind]);

        model.NextCommand.Execute("system");

        model.SystemLabel.Should().Be("Pyro");
        Cell(model, "P", "Q")!.Text.Should().Be("1 · –");
        RoutesViewModel.CellText(new PortCell([new(T(["A"], ["B"]), false, false), new(redWind, false, false)])).Should().Be("2 · 100k+");
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

        tip.Should().StartWith("Everus Harbor → HUR-L1 · most SCU in one contract: 144");
        tip.Should().Contain("Experienced Rank - Direct Medium Cargo Haul · 160,250 aUEC · 90–144 SCU");
        tip.Should().Contain("pickup: hub of the board, not in the game data");
    }

    // Picture gate: L stations by their code, hubs in full; the full name stays in the row and the cell's tool tip.
    [Fact]
    public void L_stations_show_by_their_code_and_keep_their_full_name()
    {
        var model = Model([T(["HUR-L1 Green Glade Station"], ["Everus Harbor"]), T([], ["HUR-L3 Thundering Express Station"])]);

        model.Rows.Select(r => (r.Label, r.Pickup)).Should().Equal(("Everus Harbor", "Everus Harbor"), ("HUR-L1", "HUR-L1 Green Glade Station"));
        model.Columns.Should().Equal("Everus Harbor", "HUR-L3 Thundering Express Station");
        Cell(model, "Everus Harbor", "HUR-L3 Thundering Express Station")!.ToolTip.Should().StartWith("Everus Harbor → HUR-L3 Thundering Express Station");
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
    public void A_pair_a_scan_saw_carries_a_mark_and_says_when_in_its_tool_tip()
    {
        // T13.3: a system-wide template without a place in the data gets its cell from the scans.
        var wide = T([], [], areaKey: "Stanton", title: "Senior Rank - Medium Cargo Haul") with { Uuid = "wide" };
        var model = Model([wide]);
        model.WithoutPlaceText.Should().NotBeNull();

        model.ShowObserved(Seen(wide, HaulContractor.Covalex, 100_000, "Everus Harbor", "Baijini Point"));

        var cell = Cell(model, "Everus Harbor", "Baijini Point")!;
        cell.Text.Should().Be("● 1 · 100k");
        cell.ToolTip.Should().Contain("Senior Rank - Medium Cargo Haul · 100,000 aUEC · 10–64 SCU · observed 2× · last 23 Nov");
        model.WithoutPlaceText.Should().BeNull("the template has a cell now");
    }

    [Fact]
    public void A_Red_Wind_cell_shows_the_reward_a_scan_saw()
    {
        var redWind = T(["P"], ["Q"], reward: null, contractor: HaulContractor.RedWind, system: StarSystem.Pyro, areaKey: "Solar", faction: HaulFaction.Hh) with { Uuid = "rw" };
        var model = Model([redWind], observed: Seen(redWind, HaulContractor.RedWind, 532_500, "P", "Q"));

        model.NextCommand.Execute("system");

        var cell = Cell(model, "P", "Q")!;
        cell.Text.Should().Be("● 1 · 532k");
        cell.ToolTip.Should().Contain("532,500 aUEC observed");
    }

    [Fact]
    public void Rank_and_faction_set_in_the_settings_window_show_in_the_filter_and_are_not_saved_again()
    {
        var model = Model([T(["A"], ["B"], rank: HaulRank.Senior)]);

        model.Follow(RoutesSettings.Default with { Rank = "junior", Faction = "cfp" });

        model.RankLabel.Should().Be("Rank ≤ Junior");
        model.FactionLabel.Should().Be("CFP");
        model.Rows.Should().BeEmpty("a Senior template is above the rank");
        _saved.Should().BeEmpty();
    }

    [Fact]
    public void Pyro_says_its_regional_templates_are_not_shown_yet()
    {
        var model = Model([T(["A"], ["B"])]);
        model.RegionsText.Should().BeNull();

        model.NextCommand.Execute("system");

        model.SystemLabel.Should().Be("Pyro");
        model.RegionsText.Should().Be("Red Wind's regional templates are not shown yet: no scan has placed them.");
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

        model.DirectLabel.Should().Be("Direct: any");
        model.NextCommand.Execute("direct");
        model.DirectLabel.Should().Be("Direct only");
        model.PreviousCommand.Execute("direct");
        model.PreviousCommand.Execute("direct");
        model.DirectLabel.Should().Be("No direct");

        model.ContractorLabel.Should().Be("Any contractor");
        model.AreaLabel.Should().Be("Stellar", "the matrix starts with stellar (picture gate)");
        model.NextCommand.Execute("area");
        model.AreaLabel.Should().Be("Interstellar");
        model.NextCommand.Execute("area");
        model.AreaLabel.Should().Be("All areas");
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
