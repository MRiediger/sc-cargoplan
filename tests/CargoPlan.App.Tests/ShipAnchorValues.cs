using System.Globalization;
using System.Text.RegularExpressions;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>What a ship file says about its hologram and outline (D90), as it goes into the file.</summary>
internal sealed record ShipAnchors(ShipCoordinates HoloOrigin, ShipCoordinates OutlineOrigin, double? OutlineScale);

/// <summary>
/// The calculation behind the anchors in the ship files (F7, D90). <see cref="Freeze"/> writes down where the older rule
/// on the racks' middle puts a ship; once, or for a new ship. <see cref="Carry"/> takes a ship's hologram over to a new
/// holo version without moving the hull; it must not run <see cref="Freeze"/> again, which would undo a corrected rack.
/// </summary>
internal static class ShipAnchorValues
{
    private const double MetresPerCell = 1.25;

    // The fields as written, one per line; holoOffset goes, it can't stand beside holoOrigin.
    private static readonly Regex FieldLine = new(@"^  ""(holoOffset|holoOrigin|outlineOrigin|outlineScale)"":[^\n]*\n", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <param name="ship">A ship as the older rule draws it, whatever its file says about its places.</param>
    public static ShipAnchors Freeze(Ship ship, HoloMesh mesh, ShipOutline outline)
    {
        var offset = HoloPlacement.Place(mesh, ship.Bounds, ship.HoloOffset).Offset;
        var plain = ship with { OutlineOrigin = null, OutlineScale = null };
        var (min, max) = (ship.Bounds.Min, ship.Bounds.Max);
        var scale = OutlinePlacement.FitScale(outline, ship.Bounds);
        return new ShipAnchors(
            new ShipCoordinates(offset.X, offset.Y, offset.Z),
            new ShipCoordinates((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, OutlinePlacement.Floor(outline, plain)),
            scale > 1 ? scale : null);
    }

    /// <summary>The origin that keeps the middle of the hologram's box where it was, when the mesh changes to <paramref name="next"/>.</summary>
    public static ShipCoordinates Carry(ShipCoordinates origin, HoloBounds previous, HoloBounds next)
    {
        static double Middle(float min, float max) => ((double)min + max) / 2;
        return new ShipCoordinates(
            origin.X + (Middle(previous.Min.X, previous.Max.X) - Middle(next.Min.X, next.Max.X)) / MetresPerCell,
            origin.Y + (Middle(previous.Min.Y, previous.Max.Y) - Middle(next.Min.Y, next.Max.Y)) / MetresPerCell,
            origin.Z + (Middle(previous.Min.Z, previous.Max.Z) - Middle(next.Min.Z, next.Max.Z)) / MetresPerCell);
    }

    /// <summary>A ship or module file's text with these anchors in place of any it had, before <paramref name="before"/>; nothing else changes.</summary>
    public static string WriteInto(string text, ShipAnchors anchors, string before)
    {
        // The file's own line ends: a Windows checkout has CRLF.
        var end = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var scale = anchors.OutlineScale is { } s ? $"  \"outlineScale\": {Exact(s)},{end}" : "";
        var fields = $"  \"holoOrigin\": {Exact(anchors.HoloOrigin)},{end}  \"outlineOrigin\": {Exact(anchors.OutlineOrigin)},{end}{scale}";
        var rest = FieldLine.Replace(text, "");
        return rest.Insert(rest.IndexOf($"  \"{before}\":", StringComparison.Ordinal), fields);
    }

    private static string Exact(ShipCoordinates c) => $"[{Exact(c.X)}, {Exact(c.Y)}, {Exact(c.Z)}]";

    // The shortest text that reads back as the same bits, so the picture stays the same to the pixel.
    private static string Exact(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
