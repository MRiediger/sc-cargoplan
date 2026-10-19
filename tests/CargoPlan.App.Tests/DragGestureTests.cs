using System.Windows.Input;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

public class DragGestureTests
{
    private static readonly Placement Titanium = new("Riker", "rack-0", new CargoItem("Titanium", 16, CrateSize.Four));

    private static readonly Crate Crate = new("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(0, 0, 0));

    private static readonly SceneHit Cargo = new("rack-0", Titanium, Crate);
    private static readonly SceneHit Delivered = new("rack-0", Titanium with { Delivered = true }, Crate with { Delivered = true });
    private static readonly SceneHit Free = new("rack-2", null, null);
    private static readonly SceneHit OtherRack = new("rack-1", null, null);

    private static readonly Drop Elsewhere = new("rack-1", new GridPosition(2, 1, 0));

    /// <summary>InlineData can't hold records, so the theories name the hit.</summary>
    private static SceneHit? Hit(string name) => name switch
    {
        "cargo" => Cargo,
        "delivered" => Delivered,
        "free" => Free,
        "empty" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    [Fact]
    public void Left_click_on_cargo_ticks_its_stop()
    {
        DragGesture.Resolve(MouseButton.Left, Cargo, moved: false, Cargo, drop: null).Should().Be(new Gesture.Click("Riker"));
    }

    [Fact]
    public void Left_click_on_delivered_cargo_ticks_it_back()
    {
        DragGesture.Resolve(MouseButton.Left, Delivered, moved: false, Delivered, drop: null).Should().Be(new Gesture.Click("Riker"));
    }

    [Theory]
    [InlineData("free")]
    [InlineData("empty")]
    public void Left_click_off_the_cargo_does_nothing(string hit)
    {
        DragGesture.Resolve(MouseButton.Left, Hit(hit), moved: false, Hit(hit), drop: null).Should().Be(new Gesture.None());
    }

    [Theory]
    [InlineData("cargo")]
    [InlineData("delivered")]
    [InlineData("free")]
    [InlineData("empty")]
    public void Left_drag_orbits_wherever_it_starts(string hit)
    {
        DragGesture.Resolve(MouseButton.Left, Hit(hit), moved: true, OtherRack, Elsewhere).Should().Be(new Gesture.Orbit());
    }

    [Fact]
    public void Right_drag_from_a_crate_onto_a_cell_moves_the_crate_there()
    {
        var gesture = DragGesture.Resolve(MouseButton.Right, Cargo, moved: true, OtherRack, Elsewhere);

        var move = gesture.Should().BeOfType<Gesture.MoveCrates>().Subject;
        (move.Anchor, move.RackId, move.Position).Should().Be((Crate, "rack-1", new GridPosition(2, 1, 0)));
        move.Crates.Should().Equal(Crate);
    }

    [Fact]
    public void Right_drag_from_a_crate_within_its_own_rack_moves_it_too()
    {
        DragGesture.Resolve(MouseButton.Right, Cargo, moved: true, Cargo, new Drop("rack-0", new GridPosition(2, 0, 1)))
            .Should().BeOfType<Gesture.MoveCrates>();
    }

    [Fact]
    public void Right_drag_from_a_crate_back_onto_its_own_place_does_nothing()
    {
        DragGesture.Resolve(MouseButton.Right, Cargo, moved: true, Cargo, new Drop("rack-0", Crate.Position)).Should().Be(new Gesture.None());
    }

    [Fact]
    public void Right_drag_from_a_crate_without_a_target_cell_does_nothing()
    {
        // Off the racks, or on top of a crate as high as the grid: not a pan either, the drag started on a crate.
        DragGesture.Resolve(MouseButton.Right, Cargo, moved: true, null, drop: null).Should().Be(new Gesture.None());
        DragGesture.Resolve(MouseButton.Right, Cargo, moved: true, OtherRack, drop: null).Should().Be(new Gesture.None());
    }

    [Theory]
    [InlineData("free")]
    [InlineData("empty")]
    [InlineData("delivered")]
    public void Right_drag_off_the_crates_on_board_pans(string hit)
    {
        // Delivered crates are gone from the ship (D16), so they can't be moved; they are a drop target, though.
        DragGesture.Resolve(MouseButton.Right, Hit(hit), moved: true, OtherRack, Elsewhere).Should().Be(new Gesture.Pan());
    }

    [Theory]
    [InlineData("cargo")]
    [InlineData("delivered")]
    [InlineData("free")]
    [InlineData("empty")]
    public void Right_press_without_moving_does_nothing(string hit)
    {
        DragGesture.Resolve(MouseButton.Right, Hit(hit), moved: false, Hit(hit), drop: null).Should().Be(new Gesture.None());
    }

    [Fact]
    public void Only_a_right_press_on_a_crate_on_board_picks_it_up()
    {
        DragGesture.DraggedCrate(MouseButton.Right, Cargo).Should().Be(Crate);
        DragGesture.DraggedCrate(MouseButton.Right, Delivered).Should().BeNull();
        DragGesture.DraggedCrate(MouseButton.Right, Free).Should().BeNull();
        DragGesture.DraggedCrate(MouseButton.Right, null).Should().BeNull();
        DragGesture.DraggedCrate(MouseButton.Left, Cargo).Should().BeNull();
    }

    [Fact]
    public void Other_buttons_do_nothing()
    {
        DragGesture.Resolve(MouseButton.Middle, Cargo, moved: true, OtherRack, Elsewhere).Should().Be(new Gesture.None());
    }

    // --- A stop line dragged from the legend (D25) ---

    [Theory]
    [InlineData("cargo")]
    [InlineData("delivered")]
    [InlineData("free")]
    public void A_stop_dropped_onto_a_rack_moves_there(string hit)
    {
        DragGesture.ResolveStop("Shubin", moved: true, Hit(hit)).Should().Be(new Gesture.MoveStop("Shubin", Hit(hit)!.RackId));
    }

    [Fact]
    public void A_stop_dropped_off_the_racks_does_nothing()
    {
        DragGesture.ResolveStop("Shubin", moved: true, null).Should().Be(new Gesture.None());
    }

    [Fact]
    public void A_stop_line_pressed_without_moving_does_nothing()
    {
        DragGesture.ResolveStop("Shubin", moved: false, OtherRack).Should().Be(new Gesture.None());
    }
}
