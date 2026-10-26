using System.Windows;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

public sealed class RackNumberSizeTests
{
    private static readonly (Ship Ship, LoadPlan Plan) Sample = TestPlans.Load();

    private static readonly IReadOnlyList<SceneRack> HullA =
        ShipViewBuilder.Build(Sample.Plan, Sample.Ship, LoadPlanner.Validate(Sample.Plan, Sample.Ship)).Racks;

    private static readonly double Default = CrateAppearance.Default.RackNumberFontSize;

    /// <summary>
    /// The ship view of the default overlay (420×460) with the sample plan: 10 padding all round, a 30 header and a
    /// legend of four stop lines and three hints at the Windows font size 11 (6 + 7 × 14.6).
    /// </summary>
    private static readonly Size DefaultView = new(400, 302);

    private static double FitScale() => FitScale(Sample.Ship);

    private static double FitScale(Ship ship)
    {
        var scene = ShipViewBuilder.Build(Sample.Plan, ship, LoadPlanner.Validate(Sample.Plan, ship));
        var extent = SceneProjector.Project(scene, IsoProjection.Default).Extent;
        return ViewTransform.Fit(extent, DefaultView, margin: 8).Scale;
    }

    [Fact]
    public void Default_is_the_size_at_zoom_1_before_it_was_a_setting()
    {
        // Up to T4.5 the number was 0.9 × the scale: in the default view the setting must not change the look. Measured
        // on the Hull A of that time; K5.2's wider spindle gap fits smaller and leaves the default as it was (D78).
        Default.Should().Be(Math.Round(0.9 * FitScale(TestPlans.HullABeforeK52())));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Size_stays_the_same_over_the_zoom_range_in_the_default_view(double zoom)
    {
        RackNumberSize.For(Default, FitScale() * zoom, HullA).Should().Be(Default);
    }

    [Fact]
    public void Far_out_the_number_shrinks_to_the_shortest_rack_side()
    {
        // Hull A racks are 4 × 2 × 2: two cells of 10 pixels.
        RackNumberSize.For(Default, scale: 10, HullA).Should().Be(20);
    }

    [Fact]
    public void Shrinking_stops_at_the_minimum()
    {
        RackNumberSize.For(Default, scale: 3, HullA).Should().Be(RackNumberSize.MinSize);
    }

    [Fact]
    public void A_setting_below_the_minimum_wins()
    {
        RackNumberSize.For(8, scale: 3, HullA).Should().Be(8);
    }

    [Fact]
    public void Shortest_side_comes_from_the_rack_bounds()
    {
        SceneRack flat = new("flat", 1, IsViolated: false, new ShipBox(new ShipPoint(0, 0, 0), new ShipPoint(4, 3, 1)), [], new RackLoad(1, 0, 12));

        RackNumberSize.ShortestSide(HullA).Should().Be(2);
        RackNumberSize.ShortestSide([.. HullA, flat]).Should().Be(1);
    }
}
