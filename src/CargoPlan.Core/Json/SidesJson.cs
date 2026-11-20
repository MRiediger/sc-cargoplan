using CargoPlan.Core.Models;

namespace CargoPlan.Core.Json;

/// <summary>The words for the sides in plan and mission files (D116), one spelling for both.</summary>
internal static class SidesJson
{
    public const string None = "none";
    public const string PortFirst = "portFirst";
    public const string StarboardFirst = "starboardFirst";

    public static string Text(Sides sides) => sides switch
    {
        Sides.PortFirst => PortFirst,
        Sides.StarboardFirst => StarboardFirst,
        _ => None,
    };

    // A plain string and an explicit switch, as FillOrderJson.
    /// <exception cref="ArgumentException">None of the words.</exception>
    public static Sides Parse(string sides) => sides switch
    {
        None => Sides.None,
        PortFirst => Sides.PortFirst,
        StarboardFirst => Sides.StarboardFirst,
        _ => throw new ArgumentException($"sides '{sides}' is unknown, use \"{None}\", \"{PortFirst}\" or \"{StarboardFirst}\".", nameof(Sides)),
    };
}
