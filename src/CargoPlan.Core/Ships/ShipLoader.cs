using System.Reflection;
using System.Text.Json;
using CargoPlan.Core.Json;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Ships;

/// <summary>Loads ship layouts embedded as "ships/&lt;id&gt;.json" resources.</summary>
public static class ShipLoader
{
    public const int SupportedSchemaVersion = 4;

    private const string ResourcePrefix = "ships/";
    private const string ResourceSuffix = ".json";

    private static Assembly ShipAssembly => typeof(ShipLoader).Assembly;

    /// <summary>Ids of all embedded ships, sorted.</summary>
    public static IReadOnlyList<string> ListShips() => ListShips(ShipAssembly);

    /// <exception cref="ShipNotFoundException">No ship with this id (case-sensitive).</exception>
    /// <exception cref="ShipFormatException">The ship file is broken.</exception>
    public static Ship Load(string shipId) => Load(ShipAssembly, shipId);

    // Assembly overloads let tests load their own (broken) ship files.
    internal static IReadOnlyList<string> ListShips(Assembly assembly) =>
    [
        .. assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                        && n.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .Select(n => n[ResourcePrefix.Length..^ResourceSuffix.Length])
            .Order(StringComparer.Ordinal),
    ];

    internal static Ship Load(Assembly assembly, string shipId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shipId);

        var resourceName = ResourcePrefix + shipId + ResourceSuffix;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new ShipNotFoundException(shipId, ListShips(assembly));

        try
        {
            using var document = JsonDocument.Parse(stream);
            SchemaVersion.Check(document.RootElement, SupportedSchemaVersion);

            var dto = document.RootElement.Deserialize(ShipJsonContext.Default.ShipDto)
                ?? throw new JsonException("The file contains null.");

            if (dto.Id != shipId)
            {
                throw new JsonException($"\"id\" is '{dto.Id}' but must match the file name '{shipId}'.");
            }

            return dto.ToShip();
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new ShipFormatException(resourceName, ex.Message, ex);
        }
    }
}
