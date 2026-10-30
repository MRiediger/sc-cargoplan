using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The crates' fills in the 3D view (T8.2): one frozen material per stop, state and hatch tile, shared by every crate
/// that wears it, never one per crate. The colour and the hatch are the legend's (<see cref="StopBrushes"/>, K4.2): the
/// first round plain, the later ones hatched, the tile laid in cells on the crate's faces (texture coordinates in
/// cells), its side chosen so it looks about as large on screen as in the 2D view, in powers of two, so turning never
/// changes it and zooming only at a step.
/// </summary>
public static class CrateMaterials
{
    /// <summary>
    /// What a dimmed crate keeps of its alpha: crates of other missions while one is selected (D68), the empty racks of a
    /// long ship (D61); the 2D view pushed the same opacity.
    /// </summary>
    public const double DimmedShare = 0.4;

    /// <summary>
    /// What a crate keeps of its alpha while a drag holds it or its outcome moves it: it stays where it is, faded, so the
    /// rack shows what it holds without it (D20, D25); the 2D view's opacity. A dimmed crate fades further (T8.4).
    /// </summary>
    public const double FadedShare = 0.35;

    private static readonly ConcurrentDictionary<(StopPalette Palette, int Ordinal, byte Alpha, double Tile), Material> Fills = new();
    private static readonly ConcurrentDictionary<byte, Material> DeliveredFills = new();

    /// <summary>The fill of a stop's crates, <paramref name="ordinal"/> as <c>MissionPalette.Assign</c> gives it.</summary>
    public static Material Fill(StopPalette palette, int ordinal, byte alpha, bool dimmed, double tileCells, bool faded = false)
    {
        var shown = Shown(alpha, dimmed, faded);

        // A plain fill has no tile: one material for every zoom.
        var tile = palette.HatchOf(ordinal) == StopHatch.None ? 0 : tileCells;
        return Fills.GetOrAdd((palette, ordinal % (palette.Colors.Count * StopPalette.Rounds), shown, tile), key => Diffuse(
            StopBrushes.HatchTile(key.Palette, key.Ordinal, key.Alpha) is { } drawing
                ? StopBrushes.Tiled(drawing, key.Tile)
                : Frozen(new SolidColorBrush(key.Palette.ColorOf(key.Ordinal) with { A = key.Alpha }))));
    }

    /// <summary>Delivered cargo: grey at the crates' alpha, as in the 2D view.</summary>
    public static Material Delivered(byte alpha, bool dimmed, bool faded = false) =>
        DeliveredFills.GetOrAdd(Shown(alpha, dimmed, faded), shown => Diffuse(MissionPalette.Fill(MissionPalette.Delivered, shown)));

    /// <summary>
    /// The hatch tile's side in cells at <paramref name="pxPerCellAtTarget"/>: <see cref="StopBrushes.TileSize"/> DIPs
    /// in the target's plane, rounded to a power of two, so the tile lies within √2 of its 2D size.
    /// </summary>
    public static double TileCells(double pxPerCellAtTarget) =>
        Math.Pow(2, Math.Round(Math.Log2(StopBrushes.TileSize / pxPerCellAtTarget)));

    /// <summary><paramref name="alpha"/> times the shares that apply: dimmed, faded, or both.</summary>
    public static byte Shown(byte alpha, bool dimmed, bool faded) =>
        (byte)Math.Round(alpha * (dimmed ? DimmedShare : 1) * (faded ? FadedShare : 1));

    private static Material Diffuse(Brush brush) => Frozen(new DiffuseMaterial(brush));

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
