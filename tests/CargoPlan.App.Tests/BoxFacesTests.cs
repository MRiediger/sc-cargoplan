using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

/// <summary>Which faces of a box the perspective camera sees (D83), looking at the box's middle.</summary>
public sealed class BoxFacesTests
{
    private static readonly ShipBox Box = new(new ShipPoint(0, 0, 0), new ShipPoint(4, 2, 2));

    private static IEnumerable<BoxFace> Seen(double yaw, double pitch) =>
        BoxFaces.Visible(Box, new CameraMath(new IsoProjection(yaw, pitch), 30, ScreenPoints.Centre(Box), new Size(400, 400))).Select(f => f.Side);

    [Fact]
    public void Default_view_sees_top_port_and_bow()
    {
        Seen(IsoProjection.DefaultYaw, IsoProjection.DefaultPitch).Should().Equal(BoxFace.Top, BoxFace.Port, BoxFace.Bow);
    }

    [Fact]
    public void From_below_the_bottom_replaces_the_top()
    {
        Seen(IsoProjection.DefaultYaw, -IsoProjection.DefaultPitch).Should().Equal(BoxFace.Bottom, BoxFace.Port, BoxFace.Bow);
    }

    [Fact]
    public void Turned_half_round_sees_top_starboard_and_aft()
    {
        Seen(IsoProjection.DefaultYaw + 180, IsoProjection.DefaultPitch).Should().Equal(BoxFace.Top, BoxFace.Starboard, BoxFace.Aft);
    }

    [Fact]
    public void Level_view_sees_neither_top_nor_bottom()
    {
        Seen(IsoProjection.DefaultYaw, 0).Should().Equal(BoxFace.Port, BoxFace.Bow);
    }
}
