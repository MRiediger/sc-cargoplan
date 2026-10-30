using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

/// <summary>
/// Where things of a ship scene land on screen, through <see cref="CameraMath"/>, the view's only projection (D83): the
/// click points of view tests (gestures from T8.3) and the samples of the conformity test.
/// </summary>
public static class ScreenPoints
{
    public static Point3D Centre(ShipBox box) => new(
        (box.Min.X + box.Max.X) / 2.0, (box.Min.Y + box.Max.Y) / 2.0, (box.Min.Z + box.Max.Z) / 2.0);

    public static IEnumerable<Point3D> Corners(ShipBox box) =>
        from x in new[] { box.Min.X, box.Max.X }
        from y in new[] { box.Min.Y, box.Max.Y }
        from z in new[] { box.Min.Z, box.Max.Z }
        select new Point3D(x, y, z);

    /// <summary>Rack corners and crate centres of a scene: what the eye checks against the picture.</summary>
    public static IEnumerable<Point3D> Samples(ShipScene scene) =>
        scene.Racks.SelectMany(r => Corners(r.Bounds))
            .Concat(scene.Racks.SelectMany(r => r.Boxes).Where(b => b.Crate is not null).Select(b => Centre(b.Bounds)));

    /// <summary>The middle of a crate on screen, for a click on it.</summary>
    public static Point Of(Crate crate, ShipScene scene, CameraMath camera) =>
        camera.Project(Centre(scene.Racks.SelectMany(r => r.Boxes).Single(b => b.Crate == crate).Bounds));

    /// <summary>
    /// WPF's own mapping of ship points onto a view of <paramref name="view"/> seen by <paramref name="camera"/>: a probe
    /// visual in a viewport of its own with a copy of the camera, independent of <see cref="CameraMath"/> (STA).
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.media.media3d.visual3d.transformtoancestor
    /// </summary>
    public static GeneralTransform3DTo2D Wpf(PerspectiveCamera camera, Size view)
    {
        var visual = new ModelVisual3D();
        var viewport = new Viewport3D { Camera = camera.Clone(), Children = { visual } };
        viewport.Measure(view);
        viewport.Arrange(new Rect(view));
        return visual.TransformToAncestor(viewport) ?? throw new InvalidOperationException("The probe has no 2D ancestor.");
    }

    /// <summary>Where <paramref name="point"/> lies in the picture <paramref name="view"/> shows now, as WPF draws it (STA).</summary>
    public static Point InPicture(ShipViewControl view, Point3D point)
    {
        var viewport = (Viewport3D)VisualTreeHelper.GetChild(view, 0);
        Wpf((PerspectiveCamera)viewport.Camera, view.RenderSize).TryTransform(point, out var screen).Should().BeTrue();
        return screen;
    }
}
