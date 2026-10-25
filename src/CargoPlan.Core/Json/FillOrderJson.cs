using CargoPlan.Core.Models;

namespace CargoPlan.Core.Json;

/// <summary>The words for the fill order in plan files (D24) and mission files (D72), one spelling for both.</summary>
internal static class FillOrderJson
{
    public const string BowFirst = "bowFirst";
    public const string SternFirst = "sternFirst";

    public static string Text(FillOrder fillOrder) => fillOrder == FillOrder.BowFirst ? BowFirst : SternFirst;

    // A plain string and an explicit switch: STJ's enum converter would also accept numbers and other casing (as PositionDto).
    /// <exception cref="ArgumentException">Neither word.</exception>
    public static FillOrder Parse(string fillOrder) => fillOrder switch
    {
        BowFirst => FillOrder.BowFirst,
        SternFirst => FillOrder.SternFirst,
        _ => throw new ArgumentException($"fillOrder '{fillOrder}' is unknown, use \"{BowFirst}\" or \"{SternFirst}\".", nameof(FillOrder)),
    };
}
