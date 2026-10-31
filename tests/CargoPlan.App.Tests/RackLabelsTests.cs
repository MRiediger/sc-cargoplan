using System.Windows;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>
/// Where the rack numbers stand in the perspective view (D32, D61, D83): on the largest face the camera sees, above the
/// rack on a long ship, hidden where another rack lies between the camera and the number (T8.2, R2: the hologram hides none).
/// </summary>
public class RackLabelsTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();
    private static readonly Size View = new(800, 600);

    private static ShipScene Scene(string shipId)
    {
        var ship = Catalog.Get(shipId);
        return ShipViewBuilder.Build(new LoadPlan(ship.Id, [], []), ship, []);
    }

    private static CameraMath Camera(ShipScene scene, double yaw, double pitch, double distance = 60) =>
        new(new CameraOrientation(yaw, pitch), distance, ScreenPoints.Centre(scene.Bounds), View);

    [Fact]
    public void Every_rack_of_a_compact_ship_gets_a_number_on_its_face_in_the_default_view()
    {
        var scene = Scene("hull_a");
        var labels = RackLabels.Place(scene, Camera(scene, CameraOrientation.DefaultYaw, CameraOrientation.DefaultPitch));

        labels.Select(l => l.Number).Should().BeEquivalentTo(scene.Racks.Select(r => r.Number));
        labels.Should().OnlyContain(l => !l.Above && !l.Hidden);
    }

    [Fact]
    public void Looking_almost_straight_down_puts_the_number_on_the_top_face()
    {
        var scene = Scene("hull_a");
        var camera = Camera(scene, 30, CameraOrientation.MaxPitch);
        var rack = scene.Racks[0];
        var (min, max) = (rack.Bounds.Min, rack.Bounds.Max);

        var label = RackLabels.Place(scene, camera).Single(l => l.RackId == rack.RackId);

        (label.Anchor - camera.Project(new((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, max.Z))).Length.Should().BeLessThan(1e-9);
    }

    [Theory]
    [InlineData(45, 80, "upper")]
    [InlineData(45, -80, "lower")]
    public void From_above_or_below_the_far_level_is_hidden_behind_the_near_one(double yaw, double pitch, string visibleLevel)
    {
        // Hull A: racks 1 and 3 upper, 2 and 4 lower, one above the other.
        var scene = Scene("hull_a");
        int[] upper = [1, 3];

        var labels = RackLabels.Place(scene, Camera(scene, yaw, pitch));

        foreach (var label in labels)
        {
            var isUpper = upper.Contains(label.Number);
            label.Hidden.Should().Be(visibleLevel == "upper" ? !isUpper : isUpper, $"rack {label.Number}");
        }
    }

    [Fact]
    public void On_a_long_ship_the_number_stands_above_its_rack()
    {
        var scene = Scene("c2_hercules");
        CameraOrientation.IsLong(scene.Bounds).Should().BeTrue();
        var camera = Camera(scene, CameraOrientation.LongShipYaw, CameraOrientation.DefaultPitch, distance: 150);

        foreach (var label in RackLabels.Place(scene, camera))
        {
            var rack = scene.Racks.Single(r => r.RackId == label.RackId);
            Point[] outline = [.. BoxFaces.Visible(rack.Bounds, camera).SelectMany(f => f.Corners).Select(camera.Project)];

            label.Above.Should().BeTrue();
            label.Anchor.Y.Should().BeApproximately(outline.Min(p => p.Y), 1e-9);
            label.Anchor.X.Should().BeApproximately((outline.Min(p => p.X) + outline.Max(p => p.X)) / 2, 1e-9);
        }
    }

    [Fact]
    public void A_covered_largest_face_moves_the_number_to_the_next_visible_face()
    {
        // Default view: a lower rack's top lies under the upper rack, its port side is about as large and free.
        var scene = Scene("hull_a");
        var camera = Camera(scene, CameraOrientation.DefaultYaw, CameraOrientation.DefaultPitch);
        var lower = scene.Racks.Single(r => r.Number == 2);
        var port = BoxFaces.Visible(lower.Bounds, camera).Single(f => f.Side == BoxFace.Port);

        var label = RackLabels.Place(scene, camera).Single(l => l.Number == 2);

        label.Hidden.Should().BeFalse();
        (label.Anchor - camera.Project(port.Centre)).Length.Should().BeLessThan(1e-9);
    }

    [Fact]
    public void A_number_behind_the_camera_is_hidden()
    {
        var scene = Scene("hull_c");

        var labels = RackLabels.Place(scene, Camera(scene, 45, 20, distance: 1));

        labels.Should().Contain(l => l.Hidden);
    }
}
