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
        DragGesture.Resolve(MouseButton.Left, Cargo, moved: false, Cargo).Should().Be(new Gesture.Click("Riker"));
    }

    [Fact]
    public void Left_click_on_delivered_cargo_ticks_it_back()
    {
        DragGesture.Resolve(MouseButton.Left, Delivered, moved: false, Delivered).Should().Be(new Gesture.Click("Riker"));
    }

    [Theory]
    [InlineData("free")]
    [InlineData("empty")]
    public void Left_click_off_the_cargo_does_nothing(string hit)
    {
        DragGesture.Resolve(MouseButton.Left, Hit(hit), moved: false, Hit(hit)).Should().Be(new Gesture.None());
    }

    [Theory]
    [InlineData("cargo")]
    [InlineData("delivered")]
    [InlineData("free")]
    [InlineData("empty")]
    public void Left_drag_orbits_wherever_it_starts(string hit)
    {
        DragGesture.Resolve(MouseButton.Left, Hit(hit), moved: true, OtherRack).Should().Be(new Gesture.Orbit());
    }

    [Fact]
    public void Right_drag_from_cargo_onto_another_rack_moves_it()
    {
        DragGesture.Resolve(MouseButton.Right, Cargo, moved: true, new SceneHit("rack-1", Titanium with { RackId = "rack-1" }, null))
            .Should().Be(new Gesture.Move(Titanium, "rack-1"));
    }

    [Fact]
    public void Right_drag_from_cargo_onto_free_space_of_another_rack_moves_it()
    {
        DragGesture.Resolve(MouseButton.Right, Cargo, moved: true, OtherRack).Should().Be(new Gesture.Move(Titanium, "rack-1"));
    }

    [Fact]
    public void Right_drag_from_cargo_back_onto_its_own_rack_does_nothing()
    {
        DragGesture.Resolve(MouseButton.Right, Cargo, moved: true, new SceneHit("rack-0", null, null)).Should().Be(new Gesture.None());
    }

    [Fact]
    public void Right_drag_from_cargo_off_the_racks_does_nothing()
    {
        // Not a pan either: the drag started on cargo, so it stays a move until released.
        DragGesture.Resolve(MouseButton.Right, Cargo, moved: true, null).Should().Be(new Gesture.None());
    }

    [Theory]
    [InlineData("free")]
    [InlineData("empty")]
    [InlineData("delivered")]
    public void Right_drag_off_the_cargo_on_board_pans(string hit)
    {
        // Delivered crates are gone from the ship (D16), so they can't be moved.
        DragGesture.Resolve(MouseButton.Right, Hit(hit), moved: true, OtherRack).Should().Be(new Gesture.Pan());
    }

    [Theory]
    [InlineData("cargo")]
    [InlineData("free")]
    public void Right_press_without_moving_does_nothing(string hit)
    {
        DragGesture.Resolve(MouseButton.Right, Hit(hit), moved: false, Hit(hit)).Should().Be(new Gesture.None());
    }

    [Fact]
    public void Only_a_right_press_on_cargo_on_board_picks_it_up()
    {
        DragGesture.DraggedPlacement(MouseButton.Right, Cargo).Should().Be(Titanium);
        DragGesture.DraggedPlacement(MouseButton.Right, Delivered).Should().BeNull();
        DragGesture.DraggedPlacement(MouseButton.Right, Free).Should().BeNull();
        DragGesture.DraggedPlacement(MouseButton.Right, null).Should().BeNull();
        DragGesture.DraggedPlacement(MouseButton.Left, Cargo).Should().BeNull();
    }

    [Fact]
    public void Other_buttons_do_nothing()
    {
        DragGesture.Resolve(MouseButton.Middle, Cargo, moved: true, OtherRack).Should().Be(new Gesture.None());
    }
}
