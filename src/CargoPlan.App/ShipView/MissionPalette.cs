using System.Windows.Media;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The fixed colours of the ship view in one place: delivered, free, rack edges and the state colours (problem,
/// target, selection). The stop colours come from the chosen palette (<see cref="StopPalettes"/>, K4.2); what a player can
/// set (rack numbers, crate alpha, palette) comes from settings.json (D31).
/// </summary>
public static class MissionPalette
{
    /// <summary>Delivered cargo: dark, so it doesn't read as the white stop colour.</summary>
    public static Color Delivered { get; } = Color.FromRgb(0x5A, 0x5A, 0x5A);

    /// <summary>
    /// Edges of a loaded crate (K6.4): a bright solid line instead of the faint crate edge, so loaded reads apart from
    /// planned at a glance and from delivered grey. Look to be confirmed at Martin's look gate.
    /// </summary>
    public static Color LoadedEdge { get; } = Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF);

    /// <summary>Width of the loaded edge in screen pixels: three times the crate edge, below the red and cyan edges.</summary>
    public const double LoadedEdgeWidth = 1.5;

    /// <summary>Edges of a free cell: no fill, a faint outline, so the cargo behind it shows (D21).</summary>
    public static Color FreeEdge { get; } = Color.FromArgb(0x50, 0xE6, 0xE6, 0xE6);

    /// <summary>Edges of a rack that <c>LoadPlanner.Validate</c> rejects (D24); only violations are red.</summary>
    public static Color Violation { get; } = Color.FromRgb(0xFF, 0x2A, 0x2A);

    /// <summary>
    /// Edges of the rack a dragged stop would go into and of a dragged crate's ghost where it fits (D25): cyan is in no
    /// stop colour and is not the red of a violation, so the target never reads as either.
    /// </summary>
    public static Color DragTarget { get; } = Color.FromRgb(0x00, 0xFF, 0xFF);

    // The edge of a selected crate (D25) is a setting since K4.4: CrateAppearance.SelectionColor, magenta by default.

    /// <summary>Width of the red and cyan edges in screen pixels (D24, D25).</summary>
    public const double EdgeWidth = 2.5;

    /// <summary>
    /// Under the red and cyan edges on crates and ghosts (K4.2, P6): a dark line wider than the edge, so the edge reads on
    /// every stop colour of every palette. Accepted at K4.2's look gate; the selection's contour is a setting (K4.4).
    /// </summary>
    public static Color EdgeUnderStroke { get; } = Color.FromArgb(0xA0, 0x00, 0x00, 0x00);

    public const double EdgeUnderStrokeWidth = 4.5;

    /// <summary>
    /// Edges of every crate of a selected mission (T7.4d, D68), under the magenta of the selected crates themselves; white
    /// is in no stop colour either. A proposal for Martin's look gate.
    /// </summary>
    public static Color MissionFrame { get; } = Color.FromRgb(0xFF, 0xFF, 0xFF);

    public static Color RackEdge { get; } = Color.FromArgb(0xC0, 0xE6, 0xE6, 0xE6);

    /// <summary>The bow wedge's faces (D52): a trace of fill, so it reads as a body without hiding the crates behind it.</summary>
    public static Color BowWedgeFill { get; } = Color.FromArgb(0x18, 0xE6, 0xE6, 0xE6);

    // The hologram behind the racks (D63). Opaque colours: WPF 3D sorts no transparent triangles, so the whole backdrop
    // is faded instead (settings.json holoOpacity). Hull and edge colours are settings too (holoHullColor, holoEdgeColor);
    // what stays here was accepted in T7.4c-2's look gate.

    /// <summary>The hidden-line look's hull: as dark as the window, so it only hides what lies behind it.</summary>
    public static Color HoloHiddenFill { get; } = Color.FromRgb(0x14, 0x14, 0x14);

    // The perspective scene's lights (D83): fixed to the ship, so a face keeps its shade while the camera turns. Start values
    // from S8.0; set on the real element (T8.1).

    /// <summary>Light from everywhere, so faces turned away from both lights don't go black.</summary>
    public static Color SceneAmbient { get; } = Color.FromRgb(0x50, 0x50, 0x50);

    /// <summary>The main light, from above and the bow.</summary>
    public static Color SceneLight { get; } = Color.FromRgb(0xB0, 0xB0, 0xB0);

    /// <summary>A weak light from below: racks under the hull, seen from below, don't stand flat grey.</summary>
    public static Color SceneUnderLight { get; } = Color.FromRgb(0x40, 0x40, 0x40);

    // Rack numbers and their outline, the crates' fill and edge alpha are settings since T4.1 (CrateAppearance, D31).

    /// <summary>
    /// Opacity of the legend swatches: the game shows through a little, the colour still reads over bright clouds. The
    /// crates in the view take <c>crates.fillAlpha</c>, whose default is the same.
    /// </summary>
    public const byte SwatchAlpha = 0xD0;

    private const byte GhostAlpha = 0x50;

    // Frozen brushes are read-only and can be shared without copying on every render.
    // Source: https://learn.microsoft.com/dotnet/desktop/wpf/advanced/freezable-objects-overview

    /// <summary>The legend swatch of a delivered stop.</summary>
    public static SolidColorBrush DeliveredBrush { get; } = Frozen(Delivered with { A = SwatchAlpha });

    /// <summary>Violation messages in the legend, the same red as the rack edges.</summary>
    public static SolidColorBrush ViolationBrush { get; } = Frozen(Violation);

    /// <summary>
    /// Fill of the ghost a dragged crate shows at its target cell (D25): cyan where Core allows it, red where it refuses.
    /// Faint, so the cargo around the target still shows; the edges carry the colour.
    /// </summary>
    public static SolidColorBrush DropBrush { get; } = Frozen(DragTarget with { A = GhostAlpha });

    public static SolidColorBrush RefusedDropBrush { get; } = Frozen(Violation with { A = GhostAlpha });

    /// <summary>A frozen fill of <paramref name="color"/> with <paramref name="alpha"/>, for the crates' configurable alpha.</summary>
    public static SolidColorBrush Fill(Color color, byte alpha) => Frozen(color with { A = alpha });

    /// <summary>
    /// The ordinal per destination, in the order destinations first appear in the plan (as entered, D64), so a split stop
    /// has the same colour in every rack. No wrap-around (K4.2): the palette turns the ordinal into colour and hatch
    /// (<see cref="StopPalette.ColorOf"/>, <see cref="StopPalette.HatchOf"/>), so switching palettes keeps every stop's number.
    /// </summary>
    public static IReadOnlyDictionary<string, int> Assign(LoadPlan plan)
    {
        Dictionary<string, int> ordinals = [];
        foreach (var destination in plan.Placements.Select(p => p.Destination))
        {
            ordinals.TryAdd(destination, ordinals.Count);
        }

        return ordinals;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }
}
