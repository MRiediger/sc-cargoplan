using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

/// <summary>
/// The legend's row (K6.1): children side by side at their own width while it fits; short of room, rank 1 gives up its
/// width first, then rank 2, rank 0 never. Children are fixed-width borders, so the slots don't depend on a font.
/// </summary>
public class LegendRowPanelTests
{
    private static Border Child(double width, int rank)
    {
        var child = new Border { Width = width, Height = 10 };
        LegendRowPanel.SetRank(child, rank);
        return child;
    }

    /// <summary>Destination 100 (rank 2), quantity 50 (rank 0), commodity 80 (rank 1): 230 wide in all.</summary>
    private static (LegendRowPanel Panel, Border Destination, Border Quantity, Border Commodity) Line()
    {
        var (destination, quantity, commodity) = (Child(100, LegendSegment.TrimSecond), Child(50, LegendSegment.Fixed), Child(80, LegendSegment.TrimFirst));
        var panel = new LegendRowPanel { Children = { destination, quantity, commodity } };
        return (panel, destination, quantity, commodity);
    }

    private static void Layout(LegendRowPanel panel, double width)
    {
        panel.Measure(new Size(width, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, Math.Min(width, panel.DesiredSize.Width), panel.DesiredSize.Height));
    }

    private static Rect Slot(UIElement child) => LayoutInformation.GetLayoutSlot((FrameworkElement)child);

    [Fact]
    public void With_room_every_child_has_its_own_width_side_by_side()
    {
        OnSta(() =>
        {
            var (panel, destination, quantity, commodity) = Line();

            Layout(panel, 1000);

            panel.DesiredSize.Width.Should().Be(230);
            new[] { Slot(destination), Slot(quantity), Slot(commodity) }.Select(r => (r.X, r.Width)).Should().Equal((0, 100), (100, 50), (150, 80));
        });
    }

    [Fact]
    public void Without_a_limit_nothing_shortens()
    {
        OnSta(() =>
        {
            var (panel, _, _, _) = Line();

            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            panel.DesiredSize.Width.Should().Be(230);
        });
    }

    [Fact]
    public void A_little_short_only_rank_1_shortens()
    {
        OnSta(() =>
        {
            var (panel, destination, quantity, commodity) = Line();

            Layout(panel, 190);

            panel.DesiredSize.Width.Should().Be(190);
            (Slot(destination).Width, Slot(quantity).Width, Slot(commodity).Width).Should().Be((100, 50, 40));
        });
    }

    [Fact]
    public void Shorter_still_rank_1_is_gone_and_rank_2_shortens()
    {
        OnSta(() =>
        {
            var (panel, destination, quantity, commodity) = Line();

            Layout(panel, 130);

            (Slot(destination).Width, Slot(quantity).Width, Slot(commodity).Width).Should().Be((80, 50, 0));
        });
    }

    [Fact]
    public void Rank_0_keeps_its_width_even_when_nothing_else_is_left()
    {
        OnSta(() =>
        {
            var (panel, destination, quantity, commodity) = Line();

            Layout(panel, 20);

            panel.DesiredSize.Width.Should().Be(50);
            (Slot(destination).Width, Slot(quantity).Width, Slot(commodity).Width).Should().Be((0, 50, 0));
        });
    }

    [Fact]
    public void Two_children_of_one_rank_shorten_in_proportion()
    {
        // Several commodities in one line: "(8+2) Titanium, (2) Gold", both names rank 1.
        OnSta(() =>
        {
            var (titanium, gold) = (Child(60, LegendSegment.TrimFirst), Child(20, LegendSegment.TrimFirst));
            var panel = new LegendRowPanel { Children = { Child(50, LegendSegment.Fixed), titanium, gold } };

            Layout(panel, 90);

            (Slot(titanium).Width, Slot(gold).Width).Should().Be((30, 10));
        });
    }

    [Fact]
    public void A_rank_set_later_measures_the_row_again()
    {
        // K6.4 adds a fixed segment ("loaded"); a rank is data like any other.
        OnSta(() =>
        {
            var (panel, _, _, commodity) = Line();
            Layout(panel, 190);

            LegendRowPanel.SetRank(commodity, LegendSegment.Fixed);

            panel.IsMeasureValid.Should().BeFalse();
        });
    }

    // WPF controls need a single-threaded apartment; xUnit's threads aren't.
    private static void OnSta(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
