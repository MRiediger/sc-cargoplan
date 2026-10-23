using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

public sealed class FreeCellGridTests
{
    private static SceneBox Free(bool inLoadedLayer) =>
        new(new ShipBox(new ShipPoint(0, 0, 0), new ShipPoint(1, 1, 1)), CellFill.Free, null, null, null, null, IsViolated: false, inLoadedLayer);

    [Fact]
    public void The_limit_is_the_smallest_rack_number()
    {
        // One screen-size rule for numbers, bow label and grid (D32, D61).
        FreeCellGrid.MinCellSize.Should().Be(RackNumberSize.MinSize);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void From_the_limit_up_every_free_cell_is_drawn(bool inLoadedLayer)
    {
        FreeCellGrid.Draws(Free(inLoadedLayer), FreeCellGrid.MinCellSize).Should().BeTrue();
        FreeCellGrid.Draws(Free(inLoadedLayer), 40).Should().BeTrue();
    }

    [Fact]
    public void Below_the_limit_only_the_layers_with_cargo_keep_their_free_cells()
    {
        FreeCellGrid.Draws(Free(inLoadedLayer: true), FreeCellGrid.MinCellSize - 0.1).Should().BeTrue();
        FreeCellGrid.Draws(Free(inLoadedLayer: false), FreeCellGrid.MinCellSize - 0.1).Should().BeFalse();
    }
}
