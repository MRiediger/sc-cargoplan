using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The one place a stop's ordinal becomes a brush, for the ship view's crates and the legend's swatches alike (K4.2, P8),
/// so a hatch means the same crate in both. Every brush is frozen and built once per palette, ordinal and alpha.
/// </summary>
public static class StopBrushes
{
    /// <summary>
    /// Side of one hatch tile in DIPs, fixed to the screen: the polygons are in screen pixels already (D32), so the hatch
    /// keeps its size at every zoom and runs on across faces. Proposal for Martin's look gate.
    /// </summary>
    public const double TileSize = 6;

    /// <summary>Width of one stripe in DIPs; proposal for Martin's look gate.</summary>
    public const double StripeWidth = 1.5;

    // Rounds repeat after StopPalette.Rounds, so an ordinal beyond that shares the brush of its round's first pass.
    private static readonly ConcurrentDictionary<(StopPalette Palette, int Ordinal, byte Alpha), Brush> Brushes = new();

    // The hatched tiles, shared by the 2D brushes and the 3D view's materials (T8.2), so a hatch is one drawing in both.
    private static readonly ConcurrentDictionary<(StopPalette Palette, int Ordinal, byte Alpha), Drawing> Tiles = new();

    private static readonly Geometry ForwardStripes = Lines(t => [(new(-t, t), new(t, -t)), (new(-t, 2 * t), new(2 * t, -t)), (new(0, 2 * t), new(2 * t, 0))]);

    private static readonly Geometry BackwardStripes = Lines(t => [(new(-t, -t), new(2 * t, 2 * t)), (new(0, -t), new(2 * t, t)), (new(-t, 0), new(t, 2 * t))]);

    /// <summary>A crate's fill: the plain colour in the first round, hatched in the later ones.</summary>
    public static Brush Fill(StopPalette palette, int ordinal, byte alpha) =>
        Brushes.GetOrAdd((palette, ordinal % (palette.Colors.Count * StopPalette.Rounds), alpha), key => Build(key.Palette, key.Ordinal, key.Alpha));

    /// <summary>The legend's swatch: the crate's brush at the legend's own alpha (D31: the swatches keep theirs).</summary>
    public static Brush Swatch(StopPalette palette, int ordinal) => Fill(palette, ordinal, MissionPalette.SwatchAlpha);

    /// <summary>The stripes of one hatch in a tile of <see cref="TileSize"/>, three diagonals so the tiles join without gaps.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="StopHatch.None"/> has no stripes.</exception>
    public static Geometry Stripes(StopHatch hatch) => hatch switch
    {
        StopHatch.Forward => ForwardStripes,
        StopHatch.Backward => BackwardStripes,
        _ => throw new ArgumentOutOfRangeException(nameof(hatch), hatch, "No stripes for a plain fill."),
    };

    /// <summary>
    /// Black or white, whichever has the higher contrast to <paramref name="fill"/> after WCAG's formula, so stripes read on
    /// yellow and on blue without a threshold of our own.
    /// Source: https://www.w3.org/TR/WCAG22/#dfn-contrast-ratio, https://www.w3.org/TR/WCAG22/#dfn-relative-luminance
    /// </summary>
    public static Color StripeColour(Color fill)
    {
        static double Linear(byte value)
        {
            var share = value / 255.0;
            return share <= 0.04045 ? share / 12.92 : Math.Pow((share + 0.055) / 1.055, 2.4);
        }

        var luminance = 0.2126 * Linear(fill.R) + 0.7152 * Linear(fill.G) + 0.0722 * Linear(fill.B);
        var againstWhite = 1.05 / (luminance + 0.05);
        var againstBlack = (luminance + 0.05) / 0.05;
        return againstWhite > againstBlack ? Colors.White : Colors.Black;
    }

    /// <summary>
    /// The hatched tile of <see cref="TileSize"/> for a later round: the fill with one hatch over it; null in the first
    /// round, which is a plain colour. One frozen drawing per palette, ordinal and alpha.
    /// </summary>
    public static Drawing? HatchTile(StopPalette palette, int ordinal, byte alpha) =>
        palette.HatchOf(ordinal) == StopHatch.None
            ? null
            : Tiles.GetOrAdd((palette, ordinal % (palette.Colors.Count * StopPalette.Rounds), alpha), key => BuildTile(key.Palette, key.Ordinal, key.Alpha));

    /// <summary>
    /// A brush tiling <paramref name="tile"/> over squares of <paramref name="side"/>, in absolute units: DIPs on a 2D
    /// polygon, texture coordinates on a 3D face (T8.2). Viewbox = tile, stretched to the viewport.
    /// Source: https://learn.microsoft.com/dotnet/desktop/wpf/graphics-multimedia/tilebrush-overview
    /// </summary>
    public static DrawingBrush Tiled(Drawing tile, double side)
    {
        Rect box = new(0, 0, TileSize, TileSize);
        return Frozen(new DrawingBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, side, side),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = box,
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill,
        });
    }

    private static Brush Build(StopPalette palette, int ordinal, byte alpha) =>
        HatchTile(palette, ordinal, alpha) is { } tile
            ? Tiled(tile, TileSize)
            : Frozen(new SolidColorBrush(palette.ColorOf(ordinal) with { A = alpha }));

    // The stripes take the crates' alpha too, so turning the fill down fades them alike.
    private static Drawing BuildTile(StopPalette palette, int ordinal, byte alpha)
    {
        var colour = palette.ColorOf(ordinal);
        DrawingGroup drawing = new();
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(colour with { A = alpha }), null, new RectangleGeometry(new Rect(0, 0, TileSize, TileSize))));
        drawing.Children.Add(new GeometryDrawing(null, new Pen(new SolidColorBrush(StripeColour(colour) with { A = alpha }), StripeWidth), Stripes(palette.HatchOf(ordinal))));
        return Frozen(drawing);
    }

    private static Geometry Lines(Func<double, (Point From, Point To)[]> segments)
    {
        StreamGeometry geometry = new();
        using (var context = geometry.Open())
        {
            foreach (var (from, to) in segments(TileSize))
            {
                context.BeginFigure(from, isFilled: false, isClosed: false);
                context.LineTo(to, isStroked: true, isSmoothJoin: false);
            }
        }

        return Frozen(geometry);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
