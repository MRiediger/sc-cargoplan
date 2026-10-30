using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Tests;

/// <summary>
/// How WPF lays a tiled brush on a 3D face (T8.2): with <c>ViewportUnits.Absolute</c> the tile is measured in the mesh's
/// texture coordinates, so texture coordinates in cells give a hatch tile in cells. The documentation doesn't say so;
/// this render checks it. Source: https://learn.microsoft.com/dotnet/api/system.windows.media.media3d.meshgeometry3d.texturecoordinates
/// </summary>
public class HatchRenderTests
{
    private const int Pixels = 400;
    private const double Side = 4;

    [Theory]
    [InlineData(1.0, 4)]
    [InlineData(0.5, 8)]
    [InlineData(2.0, 2)]
    public void An_absolute_tile_is_measured_in_texture_coordinates(double tileCells, int expectedPeriods)
    {
        var periods = 0;
        Sta.Run(() => periods = Periods(Render(tileCells)));

        periods.Should().Be(expectedPeriods);
    }

    /// <summary>A face of 4 × 4 cells at x = 0 facing the camera, texture coordinates in cells, a tile half black half white.</summary>
    private static RenderTargetBitmap Render(double tileCells)
    {
        Rect tile = new(0, 0, StopBrushes.TileSize, StopBrushes.TileSize);
        DrawingGroup drawing = new();
        drawing.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(tile)));
        drawing.Children.Add(new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(0, 0, StopBrushes.TileSize / 2, StopBrushes.TileSize))));
        DrawingBrush brush = new(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, tileCells, tileCells),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = tile,
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill,
        };

        // Seen from the bow (yaw 0, pitch 0): right is +y, up is +z.
        MeshGeometry3D face = new()
        {
            Positions = [new(0, 0, Side), new(0, Side, Side), new(0, Side, 0), new(0, 0, 0)],
            TextureCoordinates = [new(0, 0), new(Side, 0), new(Side, Side), new(0, Side)],
            TriangleIndices = [0, 2, 1, 0, 3, 2],
        };

        var view = new Size(Pixels, Pixels);
        var camera = new CameraMath(new CameraOrientation(0, 0), 2 * Side, new Point3D(0, Side / 2, Side / 2), view);
        var wpf = new PerspectiveCamera();
        camera.ApplyTo(wpf);
        var viewport = new Viewport3D { Camera = wpf };
        viewport.Children.Add(new ModelVisual3D
        {
            Content = new Model3DGroup
            {
                Children = [new AmbientLight(Colors.White), new GeometryModel3D(face, new DiffuseMaterial(brush)) { BackMaterial = new DiffuseMaterial(brush) }],
            },
        });
        viewport.Measure(view);
        viewport.Arrange(new Rect(view));

        var bitmap = new RenderTargetBitmap(Pixels, Pixels, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(viewport);
        return bitmap;
    }

    /// <summary>Dark-to-light changes along the middle row, inside the face.</summary>
    private static int Periods(RenderTargetBitmap bitmap)
    {
        var row = new byte[Pixels * 4];
        bitmap.CopyPixels(new Int32Rect(0, Pixels / 2, Pixels, 1), row, Pixels * 4, 0);
        bool? dark = null;
        var changes = 0;
        for (var x = 0; x < Pixels; x++)
        {
            var (b, a) = (row[4 * x], row[4 * x + 3]);
            if (a < 200)
            {
                continue;
            }

            var isDark = b < 128;
            if (dark == true && !isDark)
            {
                changes++;
            }

            dark = isDark;
        }

        // Every tile is black then white: one change per tile.
        return changes;
    }
}
