using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Import.ContractTracker;

namespace CargoPlan.App.Tests;

/// <summary>
/// The overlay's side of the loaded state (K6.4, D79): a click on a legend line goes planned → loaded → delivered →
/// planned, a click on a crate still delivers the whole stop (B6); every change of state is a step of the undo (D74
/// addendum). Loaded crates look loaded, the header counts them, and what settles or is cleared says so.
/// </summary>
public class LoadedOverlayTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();
    private static readonly Ship HullA = Catalog.Get("hull_a");

    private static MissionStop Stop(string destination, string commodity, int scu, CrateSize max) =>
        new(destination, 1, [new MissionItem(commodity, scu, max)]);

    private static SavedPlan Placed(params MissionStop[] missions) =>
        missions.Aggregate(new SavedPlan([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA)), (saved, stop) => LoadPlanner.PlaceInto(saved, HullA, [stop]));

    private static OverlayViewModel Showing(SavedPlan saved)
    {
        var overlay = new OverlayViewModel(HullA, Catalog) { ConfirmReplan = _ => true };
        overlay.ShowPlan(saved);
        return overlay;
    }

    /// <summary>Riker (8, mission 1), Riker (4, mission 2), Shubin (4, mission 3).</summary>
    private static OverlayViewModel ThreeLines() =>
        Showing(Placed(Stop("Riker", "Titanium", 8, CrateSize.Four), Stop("Riker", "Gold", 4, CrateSize.Four), Stop("Shubin", "Quartz", 4, CrateSize.Four)));

    private static LegendEntry Line(OverlayViewModel overlay, int mission, string destination) =>
        overlay.Scene.Legend.Single(e => e.Mission == mission && e.Destination == destination);

    private static void Click(OverlayViewModel overlay, int mission, string destination) =>
        overlay.ToggleLineCommand.Execute(Line(overlay, mission, destination));

    private static IEnumerable<Crate> Of(OverlayViewModel overlay, int mission, string destination) =>
        overlay.Plan!.Crates.Where(c => c.Mission == mission && c.Destination == destination);

    private static (string, GridPosition)[] Places(OverlayViewModel overlay) => [.. overlay.Plan!.Crates.Select(c => (c.RackId, c.Position))];

    // --- The cycle in the legend ---

    [Fact]
    public void A_line_click_goes_planned_loaded_delivered_planned()
    {
        var overlay = ThreeLines();
        var places = Places(overlay);

        Click(overlay, 1, "Riker");
        (Line(overlay, 1, "Riker").Loaded, Line(overlay, 1, "Riker").Delivered).Should().Be((true, false));
        Of(overlay, 1, "Riker").Should().OnlyContain(c => c.Loaded);
        Places(overlay).Should().Equal(places, "loading moves nothing");

        Click(overlay, 1, "Riker");
        (Line(overlay, 1, "Riker").Loaded, Line(overlay, 1, "Riker").Delivered).Should().Be((false, true));

        Click(overlay, 1, "Riker");
        (Line(overlay, 1, "Riker").Loaded, Line(overlay, 1, "Riker").Delivered).Should().Be((false, false));
        Of(overlay, 2, "Riker").Should().OnlyContain(c => !c.Loaded && !c.Delivered, "the other mission's line is its own");
    }

    [Fact]
    public void A_crate_click_still_delivers_the_whole_stop()
    {
        var overlay = ThreeLines();
        Click(overlay, 1, "Riker");

        overlay.ToggleDeliveredCommand.Execute("Riker");

        overlay.Plan!.Crates.Where(c => c.Destination == "Riker").Should().OnlyContain(c => c.Delivered && !c.Loaded);
    }

    // --- Every change of state is a step ---

    [Fact]
    public void Undo_of_a_line_click_brings_the_state_back_and_moves_nothing()
    {
        var overlay = ThreeLines();
        var places = Places(overlay);

        Click(overlay, 1, "Riker");
        overlay.UndoCommand.Execute(null);

        Of(overlay, 1, "Riker").Should().OnlyContain(c => !c.Loaded);
        Places(overlay).Should().Equal(places);

        overlay.RedoCommand.Execute(null);
        Of(overlay, 1, "Riker").Should().OnlyContain(c => c.Loaded);
    }

    [Fact]
    public void Undo_of_a_crate_click_brings_back_every_delivery_of_the_stop()
    {
        var overlay = ThreeLines();
        Click(overlay, 1, "Riker");

        overlay.ToggleDeliveredCommand.Execute("Riker");
        overlay.UndoCommand.Execute(null);

        Of(overlay, 1, "Riker").Should().OnlyContain(c => c.Loaded && !c.Delivered);
        Of(overlay, 2, "Riker").Should().OnlyContain(c => !c.Loaded && !c.Delivered);
    }

    // --- How loaded looks ---

    [Fact]
    public void A_loaded_line_carries_the_mark_that_never_shortens()
    {
        var overlay = ThreeLines();
        Click(overlay, 1, "Riker");

        var line = Line(overlay, 1, "Riker");

        line.Segments[^1].Should().Be(new LegendSegment(LegendEntry.LoadedMark, LegendSegment.Fixed));
        Line(overlay, 2, "Riker").Segments.Should().NotContain(s => s.Text == LegendEntry.LoadedMark);
    }

    [Fact]
    public void Loaded_crates_are_drawn_as_loaded()
    {
        var overlay = ThreeLines();
        Click(overlay, 1, "Riker");

        SceneBox[] boxes = [.. overlay.Scene.Racks.SelectMany(r => r.Boxes).Where(b => b.Crate is not null)];

        boxes.Where(b => b.Crate!.Mission == 1).Should().NotBeEmpty().And.OnlyContain(b => b.Fill == CellFill.Loaded);
        boxes.Where(b => b.Crate!.Mission != 1).Should().OnlyContain(b => b.Fill == CellFill.Mission);
        boxes.Single(b => b.Crate!.Mission == 2).ToolTip.Should().NotContain("loaded");
        boxes.First(b => b.Crate!.Mission == 1).ToolTip.Should().EndWith(" (loaded)");
    }

    [Fact]
    public void The_header_counts_the_loaded_SCU()
    {
        var overlay = ThreeLines();
        overlay.Scene.CapacityText.Should().Be("16/64 · 48 free");

        Click(overlay, 1, "Riker");

        overlay.Scene.CapacityText.Should().Be("16/64 · 48 free · 8 loaded");
    }

    // --- Notices ---

    /// <summary>Rack 1: Riker's 4 on the plate, Shubin's 4 loaded on it.</summary>
    private static OverlayViewModel ShubinOnRiker()
    {
        Mission[] missions = [new(1, [Stop("Riker", "Titanium", 4, CrateSize.Four)]), new(2, [Stop("Shubin", "Quartz", 4, CrateSize.Four)])];
        Crate[] crates =
        [
            new("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(0, 0, 0), Mission: 1),
            new("Shubin", "Quartz", CrateSize.Four, "rack-0", new GridPosition(0, 0, 1), Mission: 2, Loaded: true),
        ];
        return Showing(new SavedPlan(missions, new LoadPlan(HullA.Id, crates, []), CrateCap.Of(HullA)));
    }

    [Fact]
    public void Delivering_the_cargo_below_says_what_settled()
    {
        var overlay = ShubinOnRiker();

        overlay.ToggleDeliveredCommand.Execute("Riker");

        Of(overlay, 2, "Shubin").Single().Position.Stack.Should().Be(0);
        overlay.Notices.Should().Contain("1 loaded crate settled");
    }

    [Fact]
    public void A_ship_change_clears_loaded_and_says_so()
    {
        var overlay = ThreeLines();
        Click(overlay, 1, "Riker");
        var c2 = Catalog.Get("c2_hercules");

        overlay.ChangeShip(c2);

        overlay.Plan!.Crates.Should().OnlyContain(c => !c.Loaded);
        overlay.Notices.Should().Contain($"2 crates were marked loaded – cleared for {c2.Name}");
    }

    [Fact]
    public void The_editors_Plan_keeps_loaded_crates_where_they_lie()
    {
        var overlay = ThreeLines();
        overlay.MoveLegendCommand.Execute(new Gesture.MoveLegend([.. Of(overlay, 1, "Riker")], "rack-3"));
        Click(overlay, 1, "Riker");
        var riker = Of(overlay, 1, "Riker").Select(c => (c.RackId, c.Position)).ToArray();
        var editor = new MissionEditorViewModel(overlay);
        editor.AddRowCommand.Execute(null);
        var row = editor.Rows[^1];
        (row.Destination, row.Commodity, row.Scu, row.MaxCrateSize) = ("Orison", "Gold", "8", 8);

        editor.PlanCommand.Execute(null);

        overlay.Plan!.Crates.Where(c => c.Destination == "Riker" && c.Commodity == "Titanium").Select(c => (c.RackId, c.Position, c.Loaded))
            .Should().Equal(riker.Select(p => (p.RackId, p.Position, true)));
        overlay.Plan.Crates.Should().Contain(c => c.Destination == "Orison");
    }

    [Fact]
    public void A_replan_that_finds_no_better_layout_keeps_it_and_says_so()
    {
        // Rack 1 Riker 16 loaded; racks 3 and 4 an 8 loaded and one of Area18's 8s each; rack 2 Shubin's 16.
        Mission[] missions =
        [
            new(1, [Stop("Riker", "Titanium", 16, CrateSize.Sixteen)]),
            new(2, [Stop("Orison", "Quartz", 8, CrateSize.Eight)]),
            new(3, [Stop("Lorville", "Tin", 8, CrateSize.Eight)]),
            new(4, [Stop("Area18", "Stims", 16, CrateSize.Eight)]),
            new(5, [Stop("Shubin", "Gold", 16, CrateSize.Sixteen)]),
        ];
        Crate[] crates =
        [
            new("Riker", "Titanium", CrateSize.Sixteen, "rack-0", new GridPosition(0, 0, 0), Mission: 1, Loaded: true),
            new("Shubin", "Gold", CrateSize.Sixteen, "rack-1", new GridPosition(0, 0, 0), Mission: 5),
            new("Orison", "Quartz", CrateSize.Eight, "rack-2", new GridPosition(0, 0, 0), Mission: 2, Loaded: true),
            new("Area18", "Stims", CrateSize.Eight, "rack-2", new GridPosition(2, 0, 0), Mission: 4),
            new("Lorville", "Tin", CrateSize.Eight, "rack-3", new GridPosition(0, 0, 0), Mission: 3, Loaded: true),
            new("Area18", "Stims", CrateSize.Eight, "rack-3", new GridPosition(2, 0, 0), Mission: 4),
        ];
        var overlay = Showing(new SavedPlan(missions, new LoadPlan(HullA.Id, crates, []), CrateCap.Of(HullA)));
        var before = overlay.Saved;

        overlay.ReplanCommand.Execute(null);

        overlay.Saved.Should().BeSameAs(before);
        overlay.Notices.Should().Contain(OverlayViewModel.ReplanNoBetterText);
    }

    [Fact]
    public void A_sync_takes_a_handed_in_loaded_mission_and_says_what_settled()
    {
        // Capture 36 to Riker lies on the plate, capture 37 to Shubin loaded on it; 36 is handed in in the game.
        Mission[] missions =
        [
            new(1, [Stop("Riker", "Titanium", 4, CrateSize.Four)], new ContractSource([36])),
            new(2, [Stop("Shubin", "Quartz", 4, CrateSize.Four)], new ContractSource([37])),
        ];
        Crate[] crates =
        [
            new("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(0, 0, 0), Mission: 1, Loaded: true),
            new("Shubin", "Quartz", CrateSize.Four, "rack-0", new GridPosition(0, 0, 1), Mission: 2, Loaded: true),
        ];
        var overlay = Showing(new SavedPlan(missions, new LoadPlan(HullA.Id, crates, []), CrateCap.Of(HullA), 37));

        overlay.ApplyContractTracker(new ContractTrackerReadResult([], null, new HashSet<long> { 37 }));

        overlay.Missions.Select(m => m.Id).Should().Equal(2);
        Of(overlay, 2, "Shubin").Single().Should().Match<Crate>(c => c.Position.Stack == 0 && c.Loaded);
        overlay.Notices.Should().Contain("1 loaded crate settled");
    }
}
