using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>
/// The legend by mission (B6, D69): a header "From A, B" or "From ?" over each mission once any mission has a pickup,
/// a line "To X · SCU" per mission and destination, coloured by destination (D29). A click on a line ticks that line;
/// a click on a crate in the ship view still ticks the whole stop. Without any pickup the lines stand without headers.
/// </summary>
public class LegendByMissionTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();
    private static readonly Ship HullA = Catalog.Get("hull_a");

    private static MissionItem Item(string commodity, int scu, CrateSize max, params string[] pickup) =>
        new(commodity, scu, max, Pickup: pickup.Length == 0 ? null : pickup);

    private static SavedPlan Planned(params Mission[] missions) =>
        LoadPlanner.Plan(HullA, missions, [.. missions.SelectMany(m => m.Stops).Select(s => s.Destination).Distinct()]);

    private static IReadOnlyList<LegendEntry> Legend(SavedPlan saved) =>
        ShipViewBuilder.Build(saved, HullA, LoadPlanner.Validate(saved.Plan, HullA)).Legend;

    /// <summary>Martin's plan of 25 Oct as the overlay shows it: loaded, so its pickups are cleaned (B6).</summary>
    private static OverlayViewModel HurstonOverlay()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TestPlans.HurstonPickups());
        return viewModel;
    }

    /// <summary>Riker from Lorville and from Everus Harbor, the same commodity and crate size.</summary>
    private static SavedPlan TwoRikers() => Planned(
        new Mission(1, [new("Riker", 1, [Item("Titanium", 16, CrateSize.Eight, "Lorville")])]),
        new Mission(2, [new("Riker", 1, [Item("Titanium", 16, CrateSize.Eight, "Everus Harbor")])]));

    // --- Headers ---

    [Fact]
    public void Martins_plan_has_one_header_per_mission_and_never_the_contract_text()
    {
        var legend = HurstonOverlay().Scene.Legend;

        legend.Where(e => e.Header is not null).Select(e => (e.Mission, e.Header))
            .Should().Equal((1, "From ?"), (2, "From ?"), (4, "From Everus Harbor"), (3, "From ?"));
        legend.Should().OnlyContain(e => e.Header == null || !(e.Header.Contains("Freight") || e.Header.Contains(" - ") || e.Header.Contains(". ")));
    }

    [Fact]
    public void Loading_Martins_plan_says_once_that_pickups_were_cleaned()
    {
        var viewModel = HurstonOverlay();

        viewModel.Notices.Should().Contain(OverlayViewModel.PickupsCleanedPrefix + "Everus Harbor");

        viewModel.ShowPlan(viewModel.Saved!);
        viewModel.Notices.Should().NotContain(n => n.StartsWith(OverlayViewModel.PickupsCleanedPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void Lines_are_per_mission_and_destination_in_order_of_first_appearance()
    {
        HurstonOverlay().Scene.Legend.Select(e => (e.Mission, e.Destination, e.Scu)).Should().Equal(
            (1, "High Course Station", 7),
            (2, "High Course Station", 5), (2, "Melodic Fields Station", 3), (2, "Faithful Dream Station", 3),
            (4, "Melodic Fields Station", 2), (4, "Faithful Dream Station", 3), (4, "Thundering Express Station", 3), (4, "Green Glade Station", 2),
            (3, "Everus Harbor", 8));
    }

    [Fact]
    public void The_header_stands_on_the_first_line_of_its_mission_only()
    {
        HurstonOverlay().Scene.Legend.Where(e => e.Mission == 2).Select(e => e.Header).Should().Equal("From ?", null, null);
    }

    [Fact]
    public void The_waste_mission_to_the_pickup_station_is_a_line_of_its_own()
    {
        // ct 4 (B6 finding): Waste is delivered to Everus Harbor, where the Stims are collected; the legend shows both.
        var waste = HurstonOverlay().Scene.Legend.Single(e => e.Mission == 3);

        waste.Label.Should().Be("To Everus Harbor · 8 SCU");
        waste.Header.Should().Be("From ?");
    }

    [Fact]
    public void A_mission_with_three_pickups_has_one_header_naming_all_of_them()
    {
        var saved = Planned(new Mission(1, [new("Everus Harbor", 1, [Item("Stims", 7, CrateSize.Four, "A", "B", "C")])]));

        Legend(saved).Select(e => (e.Header, e.Destination, e.Scu)).Should().Equal(("From A, B, C", "Everus Harbor", 7));
    }

    [Fact]
    public void The_header_names_the_pickups_of_every_item_of_its_mission_once()
    {
        var saved = Planned(new Mission(1,
        [
            new("Riker", 1, [Item("Titanium", 8, CrateSize.Eight, "Lorville")]),
            new("Shubin", 2, [Item("Quartz", 4, CrateSize.Four, "lorville", "Area18")]),
        ]));

        Legend(saved).Select(e => (e.Header, e.Destination)).Should().Equal(("From Lorville, Area18", "Riker"), (null, "Shubin"));
    }

    [Fact]
    public void Two_missions_from_one_pickup_have_a_header_each()
    {
        var saved = Planned(
            new Mission(1, [new("Riker", 1, [Item("Titanium", 8, CrateSize.Eight, "Lorville")])]),
            new Mission(2, [new("Shubin", 1, [Item("Quartz", 4, CrateSize.Four, "Lorville")])]));

        Legend(saved).Select(e => (e.Mission, e.Header)).Should().Equal((1, "From Lorville"), (2, "From Lorville"));
    }

    [Fact]
    public void Without_any_pickup_the_lines_stand_without_headers()
    {
        var legend = Legend(TestPlans.SplitAndShared());

        legend.Should().OnlyContain(e => e.Header == null);
        legend.Select(e => e.Label).Should().Equal("To Riker · 20 SCU", "To Shubin · 8 SCU", "To Orison · 14 SCU", "To Lorville · 12 SCU");
    }

    // --- Lines ---

    [Fact]
    public void A_destination_has_the_same_colour_under_every_header()
    {
        var legend = HurstonOverlay().Scene.Legend;

        legend.GroupBy(e => e.Destination).Should().OnlyContain(g => g.Select(e => e.PaletteIndex).Distinct().Count() == 1);
        legend.Select(e => e.PaletteIndex).Distinct().Should().HaveCount(6);
    }

    [Fact]
    public void A_line_holds_the_crates_of_its_mission_and_destination()
    {
        var viewModel = HurstonOverlay();

        var line = viewModel.Scene.Legend.Single(e => e is { Mission: 2, Destination: "High Course Station" });

        line.Crates.Should().BeEquivalentTo(viewModel.Plan!.Crates.Where(c => c is { Mission: 2, Destination: "High Course Station" }));
    }

    [Fact]
    public void Markings_belong_to_the_mission_of_their_line()
    {
        var saved = Planned(
            new Mission(1, [new("Riker", 1, [Item("Titanium", 8, CrateSize.Eight, "Lorville")])], new ContractSource([1], CrateSizeAssumed: true)),
            new Mission(2, [new("Riker", 1, [Item("Quartz", 4, CrateSize.Four, "Lorville")])]));

        Legend(saved).Select(e => e.Label).Should().Equal("To Riker · 8 SCU · 8 SCU?", "To Riker · 4 SCU");
    }

    // --- Ticks: the line is mission + destination, the ship view's click the stop (D69) ---

    [Fact]
    public void A_click_on_a_line_ticks_only_that_line()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TwoRikers());

        viewModel.ToggleLineCommand.Execute(viewModel.Scene.Legend.Single(e => e.Mission == 2));

        viewModel.Scene.Legend.Select(e => (e.Mission, e.Delivered)).Should().Equal((1, false), (2, true));
        viewModel.Plan!.Crates.Where(c => c.Delivered).Should().OnlyContain(c => c.Mission == 2);
    }

    [Fact]
    public void A_second_click_on_the_line_ticks_it_back()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TwoRikers());
        var before = viewModel.Plan!.Crates;

        viewModel.ToggleLineCommand.Execute(viewModel.Scene.Legend.Single(e => e.Mission == 2));
        viewModel.ToggleLineCommand.Execute(viewModel.Scene.Legend.Single(e => e.Mission == 2));

        viewModel.Plan!.Crates.Should().Equal(before);
    }

    [Fact]
    public void A_click_on_a_crate_still_ticks_the_whole_stop()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TwoRikers());

        viewModel.ToggleDeliveredCommand.Execute("Riker");

        viewModel.Scene.Legend.Should().HaveCount(2).And.OnlyContain(e => e.Delivered);
    }

    // --- Check boxes: the line's or the mission's crates on board as the selection (D25) ---

    /// <summary>Lorville: Riker and Shubin; Everus Harbor: Riker and Orison.</summary>
    private static OverlayViewModel TwoPickupMissions()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(Planned(
            new Mission(1, [new("Riker", 1, [Item("Titanium", 16, CrateSize.Eight, "Lorville")]), new("Shubin", 2, [Item("Quartz", 16, CrateSize.Four, "Lorville")])]),
            new Mission(2, [new("Riker", 1, [Item("Titanium", 16, CrateSize.Eight, "Everus Harbor")]), new("Orison", 2, [Item("Gold", 16, CrateSize.Sixteen, "Everus Harbor")])])));
        return viewModel;
    }

    private static IReadOnlySet<Crate> Selected(OverlayViewModel viewModel) => viewModel.Selection.In(viewModel.Scene);

    private static LegendEntry Line(OverlayViewModel viewModel, int mission, string destination) =>
        viewModel.Scene.Legend.Single(e => e.Mission == mission && e.Destination == destination);

    private static LegendEntry Header(OverlayViewModel viewModel, int mission) =>
        viewModel.Scene.Legend.Single(e => e.Mission == mission && e.Header is not null);

    [Fact]
    public void A_line_box_selects_its_crates_and_takes_them_out_again()
    {
        var viewModel = TwoPickupMissions();
        var riker = Line(viewModel, 2, "Riker");

        viewModel.ToggleCratesCommand.Execute(riker.Crates);

        Selected(viewModel).Should().BeEquivalentTo(riker.Crates);
        SelectionFocus.State(Selected(viewModel), riker.Crates).Should().BeTrue();

        viewModel.ToggleCratesCommand.Execute(riker.Crates);

        Selected(viewModel).Should().BeEmpty();
        SelectionFocus.State(Selected(viewModel), riker.Crates).Should().BeFalse();
    }

    [Fact]
    public void Two_ticked_boxes_select_both_lines()
    {
        var viewModel = TwoPickupMissions();
        var riker = Line(viewModel, 1, "Riker");
        var orison = Line(viewModel, 2, "Orison");

        viewModel.ToggleCratesCommand.Execute(riker.Crates);
        viewModel.ToggleCratesCommand.Execute(orison.Crates);

        Selected(viewModel).Should().BeEquivalentTo([.. riker.Crates, .. orison.Crates]);
    }

    [Fact]
    public void The_header_box_selects_every_crate_of_its_mission_on_board()
    {
        var viewModel = TwoPickupMissions();

        viewModel.ToggleCratesCommand.Execute(Header(viewModel, 1).MissionCrates);

        Selected(viewModel).Should().BeEquivalentTo(viewModel.Plan!.Crates.Where(c => c.Mission == 1));
    }

    [Fact]
    public void A_box_with_part_of_its_crates_selected_shows_part_and_a_click_selects_all_then_none()
    {
        // E1: null → true → false.
        var viewModel = TwoPickupMissions();
        var shubin = Line(viewModel, 1, "Shubin");
        viewModel.Selection = CrateSelection.Empty.Toggle(viewModel.Scene, shubin.Crates[0]);

        SelectionFocus.State(Selected(viewModel), shubin.Crates).Should().BeNull();
        viewModel.ToggleCratesCommand.Execute(shubin.Crates);
        SelectionFocus.State(Selected(viewModel), shubin.Crates).Should().BeTrue();
        viewModel.ToggleCratesCommand.Execute(shubin.Crates);
        SelectionFocus.State(Selected(viewModel), shubin.Crates).Should().BeFalse();
    }

    [Fact]
    public void A_right_click_in_the_view_shows_in_the_boxes()
    {
        var viewModel = TwoPickupMissions();
        var orison = Line(viewModel, 2, "Orison");

        viewModel.Selection = CrateSelection.Empty.Toggle(viewModel.Scene, orison.Crates.Single());

        SelectionFocus.State(Selected(viewModel), orison.Crates).Should().BeTrue();
        SelectionFocus.State(Selected(viewModel), Header(viewModel, 2).MissionCrates!).Should().BeNull();
        SelectionFocus.State(Selected(viewModel), Header(viewModel, 1).MissionCrates!).Should().BeFalse();
    }

    [Fact]
    public void Ticked_crates_are_never_in_a_box()
    {
        var viewModel = TwoPickupMissions();
        viewModel.ToggleLineCommand.Execute(Line(viewModel, 1, "Shubin"));

        Line(viewModel, 1, "Shubin").Crates.Should().BeEmpty();
        Header(viewModel, 1).MissionCrates.Should().OnlyContain(c => !c.Delivered).And.OnlyContain(c => c.Destination == "Riker");
    }

    [Fact]
    public void A_box_selects_and_ticks_nothing()
    {
        var viewModel = TwoPickupMissions();
        var crates = viewModel.Plan!.Crates;

        viewModel.ToggleCratesCommand.Execute(Line(viewModel, 1, "Riker").Crates);

        viewModel.Plan.Crates.Should().BeSameAs(crates);
    }

    [Fact]
    public void The_view_reads_the_box_through_the_converter()
    {
        var viewModel = TwoPickupMissions();
        var riker = Line(viewModel, 1, "Riker");
        var converter = new LegendCheckedConverter();
        object? State() => converter.Convert([riker.Crates, viewModel.Selection, viewModel.Scene], typeof(bool?), null, System.Globalization.CultureInfo.InvariantCulture);

        State().Should().Be(false);
        viewModel.ToggleCratesCommand.Execute(riker.Crates);
        State().Should().Be(true);
    }

    // --- Legend drag: the line's or the mission's crates, atomic (D65 addendum) ---

    [Fact]
    public void Dragging_a_line_moves_exactly_its_crates()
    {
        var viewModel = TwoPickupMissions();
        var riker = Line(viewModel, 2, "Riker");
        var others = viewModel.Plan!.Crates.Where(c => !riker.Crates.Contains(c)).ToArray();

        viewModel.MoveLegendCommand.Execute(new Gesture.MoveLegend(riker.Crates, "rack-3"));

        viewModel.Plan!.Crates.Where(c => c.Mission == 2 && c.Destination == "Riker").Should().OnlyContain(c => c.RackId == "rack-3");
        viewModel.Plan.Crates.Where(c => !(c.Mission == 2 && c.Destination == "Riker")).Should().HaveCount(others.Length);
    }

    [Fact]
    public void Dragging_a_header_moves_its_whole_mission()
    {
        var viewModel = TwoPickupMissions();
        var before = viewModel.Plan!;

        viewModel.MoveLegendCommand.Execute(new Gesture.MoveLegend(Header(viewModel, 2).MissionCrates!, "rack-3"));

        var outcome = CrateDrop.LegendOutcome(before, HullA, new Gesture.MoveLegend([.. before.Crates.Where(c => c.Mission == 2)], "rack-3"));
        viewModel.Plan.Should().BeEquivalentTo(outcome.Plan);
    }

    // K2.2: every ticked line keeps its tick on another ship, so the question counts lines, one per delivery.
    [Fact]
    public void The_ship_change_question_counts_a_stop_with_two_ticked_lines_as_two_deliveries()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TwoRikers());
        viewModel.ToggleDeliveredCommand.Execute("Riker");

        viewModel.ShipChangeQuestion(Catalog.Get("c2_hercules")).Should().EndWith(Environment.NewLine + "2 deliveries already ticked – ticks stay.");
    }

    [Fact]
    public void The_ship_change_question_counts_one_ticked_line_of_a_stop()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TwoRikers());
        viewModel.ToggleLineCommand.Execute(viewModel.Scene.Legend.Single(e => e.Mission == 2));

        viewModel.ShipChangeQuestion(Catalog.Get("c2_hercules")).Should().EndWith(Environment.NewLine + "1 delivery already ticked – ticks stay.");
    }

    [Fact]
    public void A_ticked_line_stays_ticked_on_another_ship_and_the_other_line_of_its_stop_stays_on_board()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TwoRikers());
        viewModel.ToggleLineCommand.Execute(viewModel.Scene.Legend.Single(e => e.Mission == 2));

        viewModel.ChangeShip(Catalog.Get("c2_hercules"));

        viewModel.Scene.Legend.Single(e => e.Mission == 2).Delivered.Should().BeTrue();
        viewModel.Scene.Legend.Where(e => e.Mission != 2).Should().OnlyContain(e => !e.Delivered);
    }
}
