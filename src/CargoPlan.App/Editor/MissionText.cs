using CargoPlan.Core.Missions;
using static System.FormattableString;

namespace CargoPlan.App.Editor;

/// <summary>How the player sees a mission: its name in legend and notices, its source in the editor (T5.4).</summary>
public static class MissionText
{
    /// <summary>"Riker", or "Hurston + 1" for a mission with more stops; the first in contract order.</summary>
    public static string Name(Mission mission)
    {
        var stops = mission.Stops.OrderBy(s => s.Order).ToArray();
        return stops.Length > 1 ? Invariant($"{stops[0].Destination} + {stops.Length - 1}") : stops[0].Destination;
    }

    /// <summary>
    /// A scanned contract's number, "#3" (D102), also for an older file's ContractTracker capture, which became a scan on
    /// reading (D107). The one spelling for editor, legend header and notices (K6.1, K6.3). Empty for a typed mission.
    /// </summary>
    public static string Source(ContractSource? source) => source is null ? "" : Invariant($"#{source.Scan.No}");
}
