using System.Text.Json;
using CargoPlan.Core.Json;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Ships;

/// <summary>Loads ship layouts from "&lt;id&gt;.json" files in the ships folder next to the exe (D56).</summary>
public static class ShipLoader
{
    public const int SupportedSchemaVersion = 4;

    /// <summary>The folder's name next to the exe, and in the repo root.</summary>
    public const string FolderName = "ships";

    /// <summary>The ship the app falls back to; without it the ships folder counts as broken.</summary>
    public const string FallbackShipId = "hull_a";

    private const string FileSuffix = ".json";

    /// <summary>The ships folder next to the running exe (or test assembly); no single-file publish, so that is its folder (D46).</summary>
    public static string DefaultFolder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>
    /// Every ship in <paramref name="folder"/> (default <see cref="DefaultFolder"/>): a file that can't be read or
    /// isn't valid is skipped, its reason goes to <see cref="ShipCatalog.Problems"/>.
    /// </summary>
    /// <exception cref="ShipFolderException">The folder is missing, or the fallback ship is missing or broken.</exception>
    public static ShipCatalog LoadAll(string? folder = null)
    {
        folder ??= DefaultFolder;
        var ids = ListShips(folder);
        if (!ids.Contains(FallbackShipId))
        {
            throw new ShipFolderException(
                $"Ship file {FallbackShipId}{FileSuffix} missing – unzip the release again. Expected: {Path.Combine(folder, FallbackShipId + FileSuffix)}");
        }

        List<Ship> ships = [];
        List<string> problems = [];
        foreach (var id in ids)
        {
            try
            {
                ships.Add(LoadFile(folder, id));
            }
            catch (Exception ex) when (ex is ShipFormatException or IOException or UnauthorizedAccessException)
            {
                var problem = ex is ShipFormatException ? ex.Message : $"Ship file '{id}{FileSuffix}' can't be read: {ex.Message}";
                if (id == FallbackShipId)
                {
                    throw new ShipFolderException($"Ship file {FallbackShipId}{FileSuffix} is invalid – unzip the release again. {problem}", ex);
                }

                problems.Add(problem);
            }
        }

        return new ShipCatalog(ships, problems);
    }

    /// <exception cref="ShipFolderException">The ships folder is missing.</exception>
    /// <exception cref="ShipNotFoundException">No ship with this id (case-sensitive).</exception>
    /// <exception cref="ShipFormatException">The ship file is broken.</exception>
    public static Ship Load(string shipId) => Load(DefaultFolder, shipId);

    /// <inheritdoc cref="Load(string)"/>
    public static Ship Load(string folder, string shipId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shipId);

        // Looked up in the listing, not with File.Exists: Windows would find "Hull_A.json" for "hull_a".
        var ids = ListShips(folder);
        return ids.Contains(shipId) ? LoadFile(folder, shipId) : throw new ShipNotFoundException(shipId, ids);
    }

    /// <summary>Ids of the "*.json" files in <paramref name="folder"/>, sorted ordinal; subfolders and other files are ignored.</summary>
    /// <exception cref="ShipFolderException">The folder is missing.</exception>
    internal static IReadOnlyList<string> ListShips(string folder)
    {
        if (!Directory.Exists(folder))
        {
            throw new ShipFolderException($"Ships folder missing next to the exe – unzip the release again. Expected: {folder}");
        }

        // EndsWith on top of the pattern: on Windows the pattern ignores case and would take "X.JSON" too.
        return
        [
            .. Directory.EnumerateFiles(folder, "*" + FileSuffix)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(n => n.EndsWith(FileSuffix, StringComparison.Ordinal))
                .Select(n => n[..^FileSuffix.Length])
                .Order(StringComparer.Ordinal),
        ];
    }

    private static Ship LoadFile(string folder, string shipId)
    {
        var fileName = shipId + FileSuffix;
        using var stream = File.OpenRead(Path.Combine(folder, fileName));

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
            throw new ShipFormatException(fileName, ex.Message, ex);
        }
    }
}
