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
    /// "#36", "#37–38" for consecutive ids, "#36–37, 40" otherwise: ContractTracker's <c>Contracts.Id</c>, as its window
    /// shows the capture. The one spelling for editor, legend header and notices (K6.1, K6.3). Empty for a typed mission.
    /// </summary>
    public static string Source(ContractSource? source)
    {
        if (source is null)
        {
            return "";
        }

        long[] ids = [.. source.ContractIds.Distinct().Order()];
        List<string> runs = [];
        for (var start = 0; start < ids.Length;)
        {
            var end = start;
            while (end + 1 < ids.Length && ids[end + 1] == ids[end] + 1)
            {
                end++;
            }

            runs.Add(end == start ? Invariant($"{ids[start]}") : Invariant($"{ids[start]}–{ids[end]}"));
            start = end + 1;
        }

        return "#" + string.Join(", ", runs);
    }
}
