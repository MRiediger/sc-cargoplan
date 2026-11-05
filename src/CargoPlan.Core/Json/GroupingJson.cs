using CargoPlan.Core.Models;

namespace CargoPlan.Core.Json;

/// <summary>The words for the grouping in plan and mission files (D89), one spelling for both.</summary>
internal static class GroupingJson
{
    public const string None = "none";
    public const string ByDestination = "byDestination";

    public static string Text(PlanGrouping grouping) => grouping == PlanGrouping.None ? None : ByDestination;

    // A plain string and an explicit switch, as FillOrderJson.
    /// <exception cref="ArgumentException">Neither word.</exception>
    public static PlanGrouping Parse(string grouping) => grouping switch
    {
        None => PlanGrouping.None,
        ByDestination => PlanGrouping.ByDestination,
        _ => throw new ArgumentException($"grouping '{grouping}' is unknown, use \"{None}\" or \"{ByDestination}\".", nameof(PlanGrouping)),
    };
}
