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
    /// "ct 36", "ct 37–38" for consecutive ids, "ct 36, 40" otherwise: where ContractTracker shows the capture. Empty for
    /// a typed mission.
    /// </summary>
    public static string Source(ContractSource? source)
    {
        if (source is null)
        {
            return "";
        }

        long[] ids = [.. source.ContractIds.Order()];
        var consecutive = ids.Length > 1 && ids.Zip(ids.Skip(1)).All(p => p.Second == p.First + 1);
        return consecutive ? Invariant($"ct {ids[0]}–{ids[^1]}") : Invariant($"ct {string.Join(", ", ids)}");
    }
}
