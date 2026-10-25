using System.Text;
using System.Windows.Input;
using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;

namespace CargoPlan.App.Tests;

/// <summary>
/// The wheel turns a crate while it is dragged alone from the ship view (K3.2, D71): each notch turns it 90° about the
/// rack's stack axis. A selection keeps how its crates lie (B3), a legend drag leaves it to the planner. Preview and drop
/// are one resolution, so the ghost shows the turned crate.
/// </summary>
public class CrateTurnTests
{
    /// <summary>One 2×2×1 rack: a 2 lies along it at [0, 0, 0], turned it lies across.</summary>
    private static readonly Ship Square = new("square", "Square test ship", 4,
    [
        new Rack("rack-0", 0, 4, new Dimensions(2, 2, 1), null, new ShipPoint(0, 0, 0)),
    ]);

    private static readonly Placement Titanium = new("Riker", "rack-0", new CargoItem("Titanium", 2, CrateSize.Two));
    private static readonly Crate Two = new("Riker", "Titanium", CrateSize.Two, "rack-0", new GridPosition(0, 0, 0));
    private static readonly Crate Four = new("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(0, 0, 0));
    private static readonly Drop OwnCell = new("rack-0", new GridPosition(0, 0, 0));
    private static readonly IReadOnlySet<Crate> NoSelection = new HashSet<Crate>();

    private static Gesture Resolve(Crate crate, Drop drop, bool turned, IReadOnlySet<Crate>? selection = null) =>
        DragGesture.Resolve(MouseButton.Right, new SceneHit("rack-0", Titanium, crate), moved: true, new SceneHit("rack-0", Titanium, crate), drop, selection ?? NoSelection, turned);

    private static LoadPlan PlanOf(params Crate[] crates) => new(Square.Id, crates, []);

    // --- Who the wheel turns ---

    [Fact]
    public void The_wheel_turns_a_crate_dragged_alone_from_the_view()
    {
        DragGesture.CanTurn([Two], isLegend: false).Should().BeTrue();
    }

    [Fact]
    public void The_wheel_turns_no_selection_and_no_legend_drag()
    {
        DragGesture.CanTurn([Two, Two with { Position = new GridPosition(0, 1, 0) }], isLegend: false).Should().BeFalse();
        DragGesture.CanTurn([Two], isLegend: true).Should().BeFalse();
        DragGesture.CanTurn([], isLegend: false).Should().BeFalse();
    }

    // --- What a turned drag means ---

    [Fact]
    public void A_turned_crate_dropped_back_onto_its_own_cell_moves()
    {
        // Field by field: the crate list is compared by reference in record equality.
        var move = Resolve(Two, OwnCell, turned: true).Should().BeOfType<Gesture.MoveCrates>().Subject;

        (move.Anchor, move.RackId, move.Position, move.Turn).Should().Be((Two, "rack-0", OwnCell.Position, true));
        move.Crates.Should().Equal(Two);
    }

    [Fact]
    public void Turned_back_onto_its_own_cell_does_nothing()
    {
        // Two notches: the control's flag is back to unturned.
        Resolve(Two, OwnCell, turned: false).Should().Be(new Gesture.None());
    }

    [Fact]
    public void A_square_crate_turned_onto_its_own_cell_does_nothing()
    {
        Resolve(Four, OwnCell, turned: true).Should().Be(new Gesture.None());
    }

    [Fact]
    public void A_selection_ignores_the_turn()
    {
        var other = Two with { Position = new GridPosition(0, 1, 0) };

        var move = Resolve(Two, new Drop("rack-0", new GridPosition(1, 0, 0)), turned: true, new HashSet<Crate> { Two, other })
            .Should().BeOfType<Gesture.MoveCrates>().Subject;

        move.Turn.Should().BeFalse();
    }

    // --- Preview and drop ---

    [Fact]
    public void A_turned_drag_turns_the_crate_in_Cores_moves_and_has_a_key_of_its_own()
    {
        Gesture.MoveCrates turned = new(Two, [Two], "rack-0", OwnCell.Position, Turn: true);

        CrateDrop.Moves(Square, turned).Should().Equal(new CrateMove(Two, "rack-0", OwnCell.Position, Turn: true));
        CrateDrop.KeyOf(Square, turned).Should().NotBe(CrateDrop.KeyOf(Square, turned with { Turn = false }));
    }

    [Fact]
    public void A_crate_turned_in_place_lies_turned_and_its_ghost_shows_it()
    {
        var plan = PlanOf(Two);

        var outcome = CrateDrop.Outcome(plan, Square, new Gesture.MoveCrates(Two, [Two], "rack-0", OwnCell.Position, Turn: true));

        outcome.Stage.Should().Be(MoveStage.Gravity);
        CrateDrop.Changed(plan, outcome).Should().Equal((Two, Two with { Rotated = true }));
    }

    [Fact]
    public void MoveCratesCommand_saves_the_turned_crate()
    {
        var viewModel = new OverlayViewModel(Square);
        IReadOnlyList<MissionStop> missions = [new("Riker", 1, [new MissionItem("Titanium", 2, CrateSize.Two)])];
        viewModel.ShowPlan(SavedPlan.FromStops(missions, PlanOf(Two), CrateCap.Of(Square)));
        var crate = viewModel.Plan!.Crates.Single();

        viewModel.MoveCratesCommand.Execute(new Gesture.MoveCrates(crate, [crate], "rack-0", crate.Position, Turn: true));

        viewModel.Plan!.Crates.Single().Rotated.Should().BeTrue();
        using var stream = new MemoryStream();
        PlanFile.Write(stream, viewModel.Saved!);
        PlanFile.Read(new MemoryStream(stream.ToArray()), "plan.json", Square).Plan.Crates.Single().Rotated.Should().BeTrue();
        Encoding.UTF8.GetString(stream.ToArray()).Should().Contain("\"rotated\": true");
    }
}
