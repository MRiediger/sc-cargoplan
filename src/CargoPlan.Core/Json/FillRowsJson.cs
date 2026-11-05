using CargoPlan.Core.Models;

namespace CargoPlan.Core.Json;

/// <summary>The words for the row axis in plan and mission files (D88), one spelling for both.</summary>
internal static class FillRowsJson
{
    public const string Lengthwise = "lengthwise";
    public const string Crosswise = "crosswise";

    public static string Text(FillRows fillRows) => fillRows == FillRows.Lengthwise ? Lengthwise : Crosswise;

    // A plain string and an explicit switch, as FillOrderJson.
    /// <exception cref="ArgumentException">Neither word.</exception>
    public static FillRows Parse(string fillRows) => fillRows switch
    {
        Lengthwise => FillRows.Lengthwise,
        Crosswise => FillRows.Crosswise,
        _ => throw new ArgumentException($"fillRows '{fillRows}' is unknown, use \"{Lengthwise}\" or \"{Crosswise}\".", nameof(FillRows)),
    };
}
