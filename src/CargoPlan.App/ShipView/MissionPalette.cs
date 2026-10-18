using System.Windows.Media;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>All colours of the ship view in one place: one colour per stop, plus delivered, free and rack edges.</summary>
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

    /// <summary>Free space: barely filled, the rack edges show its outline.</summary>
    public static Color Free { get; } = Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF);

    /// <summary>Edges of a rack that <c>LoadPlanner.Validate</c> rejects (D10); only violations are red.</summary>
    public static Color Violation { get; } = Color.FromRgb(0xFF, 0x2A, 0x2A);

    public static Color RackEdge { get; } = Color.FromArgb(0xC0, 0xE6, 0xE6, 0xE6);

    /// <summary>The frame around all racks (<c>Ship.Bounds</c>), faint so it stays behind the cargo.</summary>
    public static Color Frame { get; } = Color.FromArgb(0x60, 0xE6, 0xE6, 0xE6);

    /// <summary>Rack numbers: white with a dark outline, readable on every stop colour and over bright clouds.</summary>
    public static Color LabelFill { get; } = Color.FromRgb(0xFF, 0xFF, 0xFF);

    public static Color LabelOutline { get; } = Color.FromArgb(0xE0, 0x14, 0x14, 0x14);

    /// <summary>Opacity of cargo fills: the game shows through a little, the colour still reads over bright clouds.</summary>
    private const byte FillAlpha = 0xD0;

    // Frozen brushes are read-only and can be shared without copying on every render.
    // Source: https://learn.microsoft.com/dotnet/desktop/wpf/advanced/freezable-objects-overview
    private static readonly SolidColorBrush[] MissionBrushes = [.. Colors.Select(c => Frozen(c with { A = FillAlpha }))];

    public static SolidColorBrush DeliveredBrush { get; } = Frozen(Delivered with { A = FillAlpha });

    public static SolidColorBrush FreeBrush { get; } = Frozen(Free);

    /// <summary>Violation messages in the legend, the same red as the rack edges.</summary>
    public static SolidColorBrush ViolationBrush { get; } = Frozen(Violation);

    /// <summary>Legend line for <see cref="Repeats"/>.</summary>
    public static string RepeatNotice { get; } = $"More than {Colors.Count} stops: colors repeat.";

    /// <summary>The fill of a stop's colour index from <see cref="Assign"/>.</summary>
    public static SolidColorBrush Brush(int index) => MissionBrushes[index];

    /// <summary>
    /// Colour index per destination, in the order destinations first appear in the plan (route order), so a split
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
