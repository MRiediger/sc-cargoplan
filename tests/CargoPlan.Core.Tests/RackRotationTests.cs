using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A rack's rotation is data for the view (D91): Core keeps computing where the rack lies before it, in whole cells. The one
/// exception is <see cref="CrateMove.Shift"/>: boxes of turned racks may overlap there, so two crates could land on one cell.
/// </summary>
public class RackRotationTests
{
    private static readonly RackRotation Tilted = new(30, 40, 10);
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static Ship Turned(Ship ship) =>
        new(ship.Id, ship.Name, ship.TotalScu, [.. ship.Racks.Select(r => r with { Rotation = Tilted })]);

    private static Crate One(string rackId, int length) => new("Riker", "Titanium", CrateSize.One, rackId, new GridPosition(length, 0, 0));

    // Racks a and b take the same box before their rotation, c lies ten cells to the bow. Built in code: the loader allows it only with a rotation.
    private static Ship Overlapping(RackRotation? rotation)
    {
        Rack RackAt(string id, int index, int x, RackRotation? turned) =>
            new(id, index, 16, new Dimensions(4, 2, 2), null, new ShipPoint(x, 0, 0), Rotation: turned);

        return new Ship("overlapping", "Overlapping", 48, [RackAt("a", 0, 0, rotation), RackAt("b", 1, 0, rotation), RackAt("c", 2, 10, null)]);
    }

    [Fact]
    public void A_rack_lies_where_it_did_before_its_rotation()
    {
        foreach (var (plain, turned) in HullA.Racks.Zip(Turned(HullA).Racks))
        {
            turned.Bounds.Should().Be(plain.Bounds);
            turned.CellBox(new GridPosition(1, 1, 1), CrateSize.One).Should().Be(plain.CellBox(new GridPosition(1, 1, 1), CrateSize.One));
        }

        Turned(HullA).Bounds.Should().Be(HullA.Bounds);
    }

    [Fact]
    public void The_layout_doesnt_know_the_rotation()
    {
        Placement[] placements = [.. HullA.Racks.Select(r => new Placement("Riker", r.Id, new CargoItem("Titanium", r.CapacityScu / 2, CrateSize.Four)))];

        CrateLayout.LayOut(Turned(HullA), placements, FillOrder.BowFirst)
            .Should().Equal(CrateLayout.LayOut(HullA, placements, FillOrder.BowFirst));
    }

    [Fact]
    public void A_shift_across_racks_keeps_its_offset_on_a_ship_with_turned_racks()
    {
        var anchor = new Crate("Riker", "Titanium", CrateSize.Two, "rack-0", new GridPosition(2, 0, 0));
        var below = new Crate("Riker", "Titanium", CrateSize.Two, "rack-1", new GridPosition(0, 0, 0));

        CrateMove.Shift(Turned(HullA), [anchor, below], anchor, "rack-2", new GridPosition(2, 0, 0))
            .Should().Equal(CrateMove.Shift(HullA, [anchor, below], anchor, "rack-2", new GridPosition(2, 0, 0)));
    }

    [Fact]
    public void A_shift_is_null_if_two_crates_would_land_on_one_cell()
    {
        var (first, second) = (One("a", 0), One("b", 0));

        CrateMove.Shift(Overlapping(Tilted), [first, second], first, "c", new GridPosition(0, 0, 0)).Should().BeNull();
    }

    [Fact]
    public void A_shift_keeps_crates_that_land_on_cells_of_their_own()
    {
        var (first, second) = (One("a", 0), One("b", 1));

        CrateMove.Shift(Overlapping(Tilted), [first, second], first, "c", new GridPosition(0, 0, 0)).Should().Equal(
            new CrateMove(first, "c", new GridPosition(0, 0, 0)),
            new CrateMove(second, "c", new GridPosition(1, 0, 0)));
    }

    [Fact]
    public void A_ship_without_a_rotation_doesnt_pay_for_the_check()
    {
        // Built in code; a file can't overlap without a rotation. Pins main's path: no check, the crates land as computed.
        var (first, second) = (One("a", 0), One("b", 0));

        CrateMove.Shift(Overlapping(null), [first, second], first, "c", new GridPosition(0, 0, 0)).Should().Equal(
            new CrateMove(first, "c", new GridPosition(0, 0, 0)),
            new CrateMove(second, "c", new GridPosition(0, 0, 0)));
    }

    [Fact]
    public void Crates_that_would_share_a_cell_take_the_fallback_without_cells()
    {
        var ship = Overlapping(Tilted);
        var (first, second) = (One("a", 0), One("b", 0));
        var plan = new LoadPlan(ship.Id, [first, second], []);

        var outcome = LoadPlanner.ResolveMove(plan, ship, [first, second], first, "c", new GridPosition(0, 0, 0));

        outcome.Plan.Should().NotBeNull();
        outcome.Plan!.Crates.Should().OnlyContain(c => c.RackId == "c");
        outcome.Plan.Crates.SelectMany(c => c.Cells).Should().OnlyHaveUniqueItems();
        LoadPlanner.Validate(outcome.Plan, ship).Should().BeEmpty();
    }
}
