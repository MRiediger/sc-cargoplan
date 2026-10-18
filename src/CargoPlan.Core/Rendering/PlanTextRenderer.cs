using System.Globalization;
using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Rendering;

/// <summary>Renders a load plan as plain text for the terminal and for pasting into chats.</summary>
public static class PlanTextRenderer
{
    private const string ColumnGap = "  ";
    private const string EmptyRack = "-";
    private const string WarningMarker = "! ";
    private const string DeliveredSuffix = " (delivered)";

    /// <summary>
    /// One block per rack, in rack order, with its placements and warnings; below, every distinct warning once.
    /// Lines are separated by "\n" on every OS, without a trailing newline.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    public static string Render(LoadPlan plan, Ship ship)
    {
        plan.EnsureBelongsTo(ship);

        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        var numberWidth = Width(racks.Max(r => r.Number));
        var capacityWidth = Width(racks.Max(r => r.CapacityScu));
        var destinationWidth = plan.Placements.Select(p => p.Destination.Length).DefaultIfEmpty(0).Max();
        var scuWidth = plan.Placements.Select(p => Width(p.Item.Scu)).DefaultIfEmpty(0).Max();

        List<string> lines = [];
        foreach (var rack in racks)
        {
            var placements = plan.PlacementsForRack(rack.Id);
            var used = placements.Sum(p => p.Item.Scu);
            var header = Invariant($"[Rack {Pad(rack.Number, numberWidth)} · {Pad(used, capacityWidth)}/{rack.CapacityScu}]");

            List<string> body = placements.Count == 0
                ? [EmptyRack]
                : [.. placements.Select(p => PlacementLine(p, destinationWidth, scuWidth))];
            body.AddRange(plan.WarningsForRack(rack.Id).Select(w => WarningMarker + w.Message));

            var indent = new string(' ', header.Length + ColumnGap.Length);
            lines.Add(header + ColumnGap + body[0]);
            lines.AddRange(body.Skip(1).Select(line => indent + line));
        }

        // A split stop warns in every rack it spans; the summary names it once.
        string[] summary = [.. plan.Warnings.Select(w => w.Message).Distinct()];
        if (summary.Length > 0)
        {
            lines.Add("");
            lines.Add("Warnings:");
            lines.AddRange(summary.Select(message => "- " + message));
        }

        return string.Join('\n', lines);
    }

    /// <summary>"Riker  12 SCU Titanium (3×4)": crate count × crate size; "(delivered)" behind, as in the ship view's tool tip.</summary>
    private static string PlacementLine(Placement placement, int destinationWidth, int scuWidth)
    {
        var item = placement.Item;
        var crateScu = (int)item.CrateSize;
        var line = Invariant(
            $"{placement.Destination.PadRight(destinationWidth)}{ColumnGap}{Pad(item.Scu, scuWidth)} SCU {item.Commodity} ({item.Scu / crateScu}×{crateScu})");
        return placement.Delivered ? line + DeliveredSuffix : line;
    }

    // PadLeft instead of interpolation alignment: {x,5} only takes a constant width.
    private static string Pad(int value, int width) => value.ToString(CultureInfo.InvariantCulture).PadLeft(width);

    private static int Width(int value) => value.ToString(CultureInfo.InvariantCulture).Length;
}
