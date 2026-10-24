using System.Windows.Media;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The fixed colours of the ship view in one place: one colour per stop, plus delivered, free and rack edges. What a
/// player can set (rack numbers, crate alpha) comes from settings.json through <c>ThemeResources</c> (D31).
/// </summary>
public static class MissionPalette
{
    /// <summary>
    /// Okabe-Ito, readable with the common colour vision deficiencies, without its black (invisible on the dark
    /// overlay) and with white as eighth colour (D15).
    /// Source: Okabe &amp; Ito (2008), Color Universal Design, https://jfly.uni-koeln.de/color/
    /// </summary>
    public static IReadOnlyList<Color> Colors { get; } =
    [
        Color.FromRgb(0xE6, 0x9F, 0x00), // orange
        Color.FromRgb(0x56, 0xB4, 0xE9), // sky blue
        Color.FromRgb(0x00, 0x9E, 0x73), // bluish green
        Color.FromRgb(0xF0, 0xE4, 0x42), // yellow
        Color.FromRgb(0x00, 0x72, 0xB2), // blue
        Color.FromRgb(0xD5, 0x5E, 0x00), // vermillion
        Color.FromRgb(0xCC, 0x79, 0xA7), // reddish purple
        Color.FromRgb(0xFF, 0xFF, 0xFF), // white
    ];

    /// <summary>Delivered cargo: dark, so it doesn't read as the white stop colour.</summary>
    public static Color Delivered { get; } = Color.FromRgb(0x5A, 0x5A, 0x5A);

    /// <summary>Edges of a free cell: no fill, a faint outline, so the cargo behind it shows (D21).</summary>
    public static Color FreeEdge { get; } = Color.FromArgb(0x50, 0xE6, 0xE6, 0xE6);

    /// <summary>Edges of a rack that <c>LoadPlanner.Validate</c> rejects (D24); only violations are red.</summary>
    public static Color Violation { get; } = Color.FromRgb(0xFF, 0x2A, 0x2A);

    /// <summary>
    /// Edges of the rack a dragged stop would go into and of a dragged crate's ghost where it fits (D25): cyan is in no
    /// stop colour and is not the red of a violation, so the target never reads as either.
    /// </summary>
    public static Color DragTarget { get; } = Color.FromRgb(0x00, 0xFF, 0xFF);

    /// <summary>
    /// Edges of a selected crate (D25): magenta is in no stop colour (the palette's reddish purple is much paler) and is
    /// neither the cyan of a target nor the red of a violation.
    /// </summary>
    public static Color Selection { get; } = Color.FromRgb(0xFF, 0x00, 0xFF);

    public static Color RackEdge { get; } = Color.FromArgb(0xC0, 0xE6, 0xE6, 0xE6);

    /// <summary>The bow wedge's faces (D52): a trace of fill, so it reads as a body without hiding the crates behind it.</summary>
    public static Color BowWedgeFill { get; } = Color.FromArgb(0x18, 0xE6, 0xE6, 0xE6);

    /// <summary>The frame around all racks (<c>Ship.Bounds</c>), faint so it stays behind the cargo.</summary>
    public static Color Frame { get; } = Color.FromArgb(0x60, 0xE6, 0xE6, 0xE6);

    // Rack numbers and their outline, the crates' fill and edge alpha are settings since T4.1 (CrateAppearance, D31).

    /// <summary>
    /// Opacity of the legend swatches: the game shows through a little, the colour still reads over bright clouds. The
    /// crates in the view take <c>crates.fillAlpha</c>, whose default is the same.
    /// </summary>
    private const byte FillAlpha = 0xD0;

    private const byte GhostAlpha = 0x50;

    // Frozen brushes are read-only and can be shared without copying on every render.
    // Source: https://learn.microsoft.com/dotnet/desktop/wpf/advanced/freezable-objects-overview
    private static readonly SolidColorBrush[] MissionBrushes = [.. Colors.Select(c => Frozen(c with { A = FillAlpha }))];

    /// <summary>The legend swatch of a delivered stop.</summary>
    public static SolidColorBrush DeliveredBrush { get; } = Frozen(Delivered with { A = FillAlpha });

    /// <summary>Violation messages in the legend, the same red as the rack edges.</summary>
    public static SolidColorBrush ViolationBrush { get; } = Frozen(Violation);

    /// <summary>
    /// Fill of the ghost a dragged crate shows at its target cell (D25): cyan where Core allows it, red where it refuses.
    /// Faint, so the cargo around the target still shows; the edges carry the colour.
    /// </summary>
    public static SolidColorBrush DropBrush { get; } = Frozen(DragTarget with { A = GhostAlpha });

    public static SolidColorBrush RefusedDropBrush { get; } = Frozen(Violation with { A = GhostAlpha });

    /// <summary>Legend line for <see cref="Repeats"/>.</summary>
    public static string RepeatNotice { get; } = $"More than {Colors.Count} stops: colors repeat.";

    /// <summary>The fill of a stop's colour index from <see cref="Assign"/>.</summary>
    public static SolidColorBrush Brush(int index) => MissionBrushes[index];

    /// <summary>A frozen fill of <paramref name="color"/> with <paramref name="alpha"/>, for the crates' configurable alpha.</summary>
    public static SolidColorBrush Fill(Color color, byte alpha) => Frozen(color with { A = alpha });

    /// <summary>
    /// Colour index per destination, in the order destinations first appear in the plan (as entered, D64), so a split
    /// stop has the same colour in every rack. From stop 9 on, colours repeat (<see cref="Repeats"/>).
    /// </summary>
    public static IReadOnlyDictionary<string, int> Assign(LoadPlan plan)
    {
        Dictionary<string, int> indices = [];
        foreach (var destination in plan.Placements.Select(p => p.Destination))
        {
            indices.TryAdd(destination, indices.Count % Colors.Count);
        }

        return indices;
    }

    /// <summary>True if the plan has more stops than colours.</summary>
    public static bool Repeats(LoadPlan plan) => plan.Placements.Select(p => p.Destination).Distinct().Count() > Colors.Count;

    private static SolidColorBrush Frozen(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }
}
