using CargoPlan.App.ShipView;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>
/// The legend line at the elevator (K6.1, D69 addendum): "To X · 5 SCU (4+1) · Pressurized Ice", the mix as the plan
/// holds it, repeats as count×size; the header names the ContractTracker id ("From Lorville · #47"). The commodity
/// shortens first, then the name; SCU, mix, id and markings never.
/// </summary>
public class LegendLineTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();
    private static readonly Ship HullA = Catalog.Get("hull_a");

    private static MissionItem Item(string commodity, int scu, CrateSize max, params string[] pickup) =>
        new(commodity, scu, max, Pickup: pickup.Length == 0 ? null : pickup);

    private static SavedPlan Planned(params Mission[] missions) =>
        LoadPlanner.Plan(HullA, missions, [.. missions.SelectMany(m => m.Stops).Select(s => s.Destination).Distinct()]);

    private static ShipScene Scene(SavedPlan saved) => ShipViewBuilder.Build(saved, HullA, LoadPlanner.Validate(saved.Plan, HullA));

    private static IReadOnlyList<LegendEntry> Legend(SavedPlan saved) => Scene(saved).Legend;

    private static Mission One(string destination, MissionItem item, ContractSource? contract = null) =>
        new(1, [new(destination, 1, [item])], contract);

    // --- The line ---

    [Fact]
    public void A_line_names_its_crate_mix_largest_first_and_its_commodity()
    {
        var saved = Planned(One("Green Glade Station", Item("Pressurized Ice", 5, CrateSize.Four)));

        Legend(saved).Single().Label.Should().Be("To Green Glade Station · 5 SCU (4+1) · Pressurized Ice");
    }

    [Fact]
    public void A_single_crate_has_no_brackets()
    {
        var saved = Planned(One("Riker", Item("Agricium", 8, CrateSize.Eight)));

        Legend(saved).Single().Label.Should().Be("To Riker · 8 SCU · Agricium");
    }

    [Fact]
    public void Repeated_sizes_read_as_count_times_size()
    {
        var saved = Planned(One("Riker", Item("Stims", 9, CrateSize.Four)));

        Legend(saved).Single().Label.Should().Be("To Riker · 9 SCU (2×4+1) · Stims");
    }

    [Fact]
    public void A_split_crate_shows_in_the_mix()
    {
        var saved = Planned(One("Riker", Item("Stims", 4, CrateSize.Four)));

        var split = LoadPlanner.SplitCrate(saved, HullA, saved.Plan.Crates.Single(), [CrateSize.Two, CrateSize.One, CrateSize.One]);

        Legend(split).Single().Label.Should().Be("To Riker · 4 SCU (2+2×1) · Stims");
    }

    [Fact]
    public void Several_commodities_each_carry_their_mix()
    {
        var saved = Planned(new Mission(1, [new("Riker", 1, [Item("Titanium", 10, CrateSize.Eight), Item("Gold", 2, CrateSize.Two)])]));

        Legend(saved).Single().Label.Should().Be("To Riker · 12 SCU (8+2) Titanium, (2) Gold");
    }

    [Fact]
    public void Markings_stay_at_the_end()
    {
        var saved = Planned(One("Riker", Item("Titanium", 8, CrateSize.Eight), new ContractSource([1], CrateSizeAssumed: true)));

        Legend(saved).Single().Label.Should().Be("To Riker · 8 SCU · Titanium · 8 SCU?");
    }

    [Fact]
    public void A_ticked_line_keeps_its_mix()
    {
        var saved = Planned(One("Riker", Item("Stims", 5, CrateSize.Four)));
        var ticked = new SavedPlan(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, "Riker", true), saved.CrateCap, saved.Watermark);

        Legend(ticked).Single().Label.Should().Be("To Riker · 5 SCU (4+1) · Stims");
    }

    [Fact]
    public void The_commodity_shortens_first_then_the_destination_never_the_quantity()
    {
        var line = Legend(Planned(One("Green Glade Station", Item("Pressurized Ice", 5, CrateSize.Four)))).Single();

        line.Segments.Select(s => (s.Text, s.Rank)).Should().Equal(
            ("To Green Glade Station", LegendSegment.TrimSecond),
            (" · 5 SCU (4+1)", LegendSegment.Fixed),
            (" · Pressurized Ice", LegendSegment.TrimFirst));
    }

    [Fact]
    public void Between_several_commodities_only_the_names_shorten()
    {
        var line = Legend(Planned(new Mission(1, [new("Riker", 1, [Item("Titanium", 10, CrateSize.Eight), Item("Gold", 2, CrateSize.Two)])]))).Single();

        line.Segments.Where(s => s.Rank == LegendSegment.TrimFirst).Select(s => s.Text).Should().Equal("Titanium", "Gold");
    }

    [Fact]
    public void The_markings_segment_keeps_the_reserve()
    {
        // D16: a tick that takes a marking away keeps the line's width; the reserve now sits on the markings alone.
        var line = Legend(Planned(One("Riker", Item("Titanium", 8, CrateSize.Eight), new ContractSource([1], CrateSizeAssumed: true)))).Single();

        line.Segments[^1].Should().Be(new LegendSegment(" · 8 SCU?", LegendSegment.Fixed, " · 8 SCU?"));
    }

    // --- The header ---

    [Fact]
    public void A_contract_header_names_its_ContractTracker_id()
    {
        var saved = Planned(One("Riker", Item("Titanium", 8, CrateSize.Eight, "Lorville"), new ContractSource([47])));

        var line = Legend(saved).Single();

        line.HeaderLabel.Should().Be("From Lorville · #47");
        line.HeaderSegments.Select(s => (s.Text, s.Rank)).Should().Equal(("From Lorville", LegendSegment.TrimFirst), (" · #47", LegendSegment.Fixed));
    }

    [Theory]
    [InlineData(new long[] { 47, 48 }, "#47–48")]
    [InlineData(new long[] { 47, 48, 49 }, "#47–49")]
    [InlineData(new long[] { 47, 49 }, "#47, 49")]
    [InlineData(new long[] { 49, 47, 48 }, "#47–49")]
    public void Consecutive_ids_read_as_a_range(long[] ids, string text)
    {
        LegendText.ContractIds(ids).Should().Be(text);
    }

    [Fact]
    public void A_typed_mission_has_no_id()
    {
        var saved = Planned(One("Riker", Item("Titanium", 8, CrateSize.Eight, "Lorville")));

        Legend(saved).Single().HeaderLabel.Should().Be("From Lorville");
    }

    [Fact]
    public void A_contract_without_pickup_still_gets_a_header()
    {
        var saved = Planned(One("Riker", Item("Titanium", 8, CrateSize.Eight), new ContractSource([47])));

        Legend(saved).Single().HeaderLabel.Should().Be("From ? · #47");
    }

    [Fact]
    public void Martins_plan_names_the_ids_of_its_contracts()
    {
        var viewModel = new ViewModels.OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(TestPlans.HurstonPickups());

        viewModel.Scene.Legend.Where(e => e.Header is not null).Select(e => (e.Mission, e.HeaderLabel)).Should().Equal(
            (1, "From ? · #1"), (2, "From ? · #2–3"), (4, "From Everus Harbor · #37–38"), (3, "From ? · #4"));
        viewModel.Scene.Legend.Single(e => e.Mission == 3).Label.Should().Be("To Everus Harbor · 8 SCU (8×1) · Waste");
    }

    // --- Waiting missions (E1): the mix from the mission's crate list ---

    [Fact]
    public void A_waiting_line_shows_its_mix_from_the_mission()
    {
        var full = Planned(new Mission(1, [.. Enumerable.Range(1, 4).Select(i => new MissionStop($"Stop {i}", i, [Item("Gold", 16, CrateSize.Sixteen)]))]));
        var saved = LoadPlanner.PlaceInto(full, HullA, [new("Hurston", 1, [Item("Gold", 9, CrateSize.Four)])], new ContractSource([5]));

        Scene(saved).Unplaced.Should().ContainSingle().Which.Label.Should().StartWith("Hurston · 9 SCU (2×4+1) · Gold · ⚠ ");
    }
}
