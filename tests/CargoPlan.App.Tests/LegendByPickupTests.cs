using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>
/// The legend by pickup (T7.4d, D68): a header "From A, B" over the stop lines of that pickup list, a destination once per
/// list, ticks per stop (D16). Without any pickup in the plan the legend is as before.
/// </summary>
public class LegendByPickupTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();
    private static readonly Ship HullA = Catalog.Get("hull_a");

    private static MissionItem Item(string commodity, int scu, CrateSize max, params string[] pickup) =>
        new(commodity, scu, max, Pickup: pickup.Length == 0 ? null : pickup);

    private static SavedPlan Planned(params Mission[] missions) =>
        LoadPlanner.Plan(HullA, missions, [.. missions.SelectMany(m => m.Stops).Select(s => s.Destination).Distinct()]);

    private static IReadOnlyList<LegendEntry> Legend(SavedPlan saved) =>
        ShipViewBuilder.Build(saved, HullA, LoadPlanner.Validate(saved.Plan, HullA)).Legend;

    /// <summary>Martin's case: Riker from Lorville and from Everus Harbor, the same commodity and crate size.</summary>
    private static SavedPlan TwoRikers() => Planned(
        new Mission(1, [new("Riker", 1, [Item("Titanium", 16, CrateSize.Eight, "Lorville")])]),
        new Mission(2, [new("Riker", 1, [Item("Titanium", 16, CrateSize.Eight, "Everus Harbor")])]));

    [Fact]
    public void Two_missions_to_one_destination_from_two_pickups_give_two_headers()
    {
        Legend(TwoRikers()).Select(e => (e.Header, e.Destination, e.Scu))
            .Should().Equal(("From Lorville", "Riker", 16), ("From Everus Harbor", "Riker", 16));
    }

    [Fact]
    public void A_mission_with_three_pickups_has_one_header_naming_all_of_them()
    {
        var saved = Planned(new Mission(1, [new("Everus Harbor", 1, [Item("Stims", 7, CrateSize.Four, "A", "B", "C")])]));

        Legend(saved).Select(e => (e.Header, e.Destination, e.Scu)).Should().Equal(("From A, B, C", "Everus Harbor", 7));
    }

    [Fact]
    public void Destinations_of_one_pickup_list_share_its_header()
    {
        var saved = Planned(new Mission(1,
        [
            new("Riker", 1, [Item("Titanium", 8, CrateSize.Eight, "Lorville")]),
            new("Shubin", 2, [Item("Quartz", 4, CrateSize.Four, "lorville")]),
        ]));

        Legend(saved).Select(e => (e.Header, e.Destination)).Should().Equal(("From Lorville", "Riker"), (null, "Shubin"));
    }

    [Fact]
    public void The_same_places_in_another_order_or_case_are_one_list()
    {
        var saved = Planned(
            new Mission(1, [new("Riker", 1, [Item("Titanium", 8, CrateSize.Eight, "A", "B")])]),
            new Mission(2, [new("Shubin", 1, [Item("Quartz", 4, CrateSize.Four, "b", "a")])]));

        Legend(saved).Select(e => e.Header).Should().Equal("From A, B", null);
    }

    [Fact]
    public void Items_without_pickup_stand_under_an_empty_header_next_to_others()
    {
        var saved = Planned(
            new Mission(1, [new("Riker", 1, [Item("Titanium", 8, CrateSize.Eight, "Lorville")])]),
            new Mission(2, [new("Shubin", 1, [Item("Quartz", 4, CrateSize.Four)])]));

        Legend(saved).Select(e => (e.Header, e.Destination)).Should().Equal(("From Lorville", "Riker"), ("", "Shubin"));
    }

    [Fact]
    public void Without_any_pickup_the_legend_has_no_header_and_stays_as_before()
    {
        var legend = Legend(TestPlans.SplitAndShared());

        legend.Should().OnlyContain(e => e.Header == null);
        legend.Select(e => e.Label).Should().Equal("Riker · 20 SCU", "Shubin · 8 SCU", "Orison · 14 SCU", "Lorville · 12 SCU");
    }

    [Fact]
    public void A_tick_on_one_line_ticks_the_stop_under_every_header()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TwoRikers());

        viewModel.ToggleDeliveredCommand.Execute("Riker");

        viewModel.Scene.Legend.Should().HaveCount(2).And.OnlyContain(e => e.Delivered);
    }

    [Fact]
    public void The_ship_change_question_counts_a_stop_under_two_headers_once()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TwoRikers());
        viewModel.ToggleDeliveredCommand.Execute("Riker");

        viewModel.ShipChangeQuestion(Catalog.Get("c2_hercules")).Should().EndWith(Environment.NewLine + "1 stop already ticked – ticks stay.");
    }

    // --- Check box at a pickup header: selects that pickup's crates ---

    /// <summary>Lorville: Riker and Shubin; Everus Harbor: Riker and Orison; nothing typed without pickup.</summary>
    private static SavedPlan TwoPickupMissions() => Planned(
        new Mission(1, [new("Riker", 1, [Item("Titanium", 16, CrateSize.Eight, "Lorville")]), new("Shubin", 2, [Item("Quartz", 16, CrateSize.Four, "Lorville")])]),
        new Mission(2, [new("Riker", 1, [Item("Titanium", 16, CrateSize.Eight, "Everus Harbor")]), new("Orison", 2, [Item("Gold", 16, CrateSize.Sixteen, "Everus Harbor")])]));

    [Fact]
    public void A_pickup_header_holds_the_crates_on_board_of_its_pickup()
    {
        var saved = TwoPickupMissions();

        var lorville = Legend(saved).Single(e => e.Header == "From Lorville");

        lorville.PickupCrates.Should().BeEquivalentTo(saved.Plan.Crates.Where(c => c.Mission == 1));
        Legend(saved).Where(e => e.Header is null).Should().OnlyContain(e => e.PickupCrates == null);
    }

    [Fact]
    public void Delivered_crates_are_left_out_of_the_check_box()
    {
        var saved = TwoPickupMissions();
        SavedPlan ticked = new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, "Shubin", true));

        Legend(ticked).Single(e => e.Header == "From Lorville").PickupCrates
            .Should().BeEquivalentTo(saved.Plan.Crates.Where(c => c.Mission == 1 && c.Destination == "Riker"));
    }

    [Fact]
    public void One_mission_gets_no_check_box_nor_does_the_header_without_pickup()
    {
        var one = Planned(new Mission(1, [new("Riker", 1, [Item("Titanium", 8, CrateSize.Eight, "Lorville")]), new("Shubin", 2, [Item("Quartz", 4, CrateSize.Four, "Area18")])]));
        var mixed = Planned(
            new Mission(1, [new("Riker", 1, [Item("Titanium", 8, CrateSize.Eight, "Lorville")])]),
            new Mission(2, [new("Shubin", 1, [Item("Quartz", 4, CrateSize.Four)])]));

        Legend(one).Should().OnlyContain(e => e.PickupCrates == null);
        Legend(mixed).Single(e => e.Header == "").PickupCrates.Should().BeNull();
        Legend(mixed).Single(e => e.Header == "From Lorville").PickupCrates.Should().NotBeNull();
    }

    [Fact]
    public void The_check_box_selects_its_crates_and_takes_them_out_again()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TwoPickupMissions());
        var orison = viewModel.Plan!.Crates.Single(c => c.Destination == "Orison");
        viewModel.Selection = CrateSelection.Empty.Toggle(viewModel.Scene, orison);
        var lorville = viewModel.Scene.Legend.Single(e => e.Header == "From Lorville");

        viewModel.TogglePickupCommand.Execute(lorville);

        viewModel.Selection.In(viewModel.Scene).Should().BeEquivalentTo([.. lorville.PickupCrates!, orison]);
        SelectionFocus.HasAll(viewModel.Selection.In(viewModel.Scene), lorville.PickupCrates!).Should().BeTrue();

        viewModel.TogglePickupCommand.Execute(lorville);

        viewModel.Selection.In(viewModel.Scene).Should().BeEquivalentTo([orison]);
    }

    [Fact]
    public void A_check_box_with_part_of_its_crates_selected_selects_the_rest()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TwoPickupMissions());
        var lorville = viewModel.Scene.Legend.Single(e => e.Header == "From Lorville");
        viewModel.Selection = CrateSelection.Empty.Toggle(viewModel.Scene, lorville.PickupCrates![0]);

        SelectionFocus.HasAll(viewModel.Selection.In(viewModel.Scene), lorville.PickupCrates).Should().BeFalse();
        viewModel.TogglePickupCommand.Execute(lorville);

        viewModel.Selection.In(viewModel.Scene).Should().BeEquivalentTo(lorville.PickupCrates);
    }
}
