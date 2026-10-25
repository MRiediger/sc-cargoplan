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

    [Fact]
    public void The_ship_change_question_counts_a_stop_with_two_ticked_lines_once()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TwoRikers());
        viewModel.ToggleDeliveredCommand.Execute("Riker");

        viewModel.ShipChangeQuestion(Catalog.Get("c2_hercules")).Should().EndWith(Environment.NewLine + "1 stop already ticked – ticks stay.");
    }
}
