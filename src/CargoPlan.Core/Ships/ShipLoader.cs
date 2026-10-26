using System.Text.Json;
using CargoPlan.Core.Json;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Ships;

/// <summary>Loads ship layouts from "&lt;id&gt;.json" files in the ships folder next to the exe (D56).</summary>
public static class ShipLoader
{
    /// <summary>Schema 5 marks <c>maxCrateSize</c> as the door limit (D70); the field reads the same in schema 4, which stays readable.</summary>
    public const int SupportedSchemaVersion = 5;

    private const int PreviousSchemaVersion = 4;

    private static readonly int[] ReadableSchemaVersions = [PreviousSchemaVersion, SupportedSchemaVersion];

    /// <summary>The folder's name next to the exe, and in the repo root.</summary>
    public const string FolderName = "ships";

    /// <summary>The ship the app falls back to; without it the ships folder counts as broken.</summary>
    public const string FallbackShipId = "hull_a";

    /// <summary>The subfolder of cargo module files (K5.3, D59); without it there are no modules.</summary>
    public const string ModulesFolderName = "modules";

    private const int ModuleSchemaVersion = 1;

    private const string FileSuffix = ".json";

    /// <summary>The ships folder next to the running exe (or test assembly); no single-file publish, so that is its folder (D46).</summary>
    public static string DefaultFolder => Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>
    /// Every ship in <paramref name="folder"/> (default <see cref="DefaultFolder"/>), and every ship with a cargo module
    /// of <see cref="ModulesFolderName"/> (K5.3): a file that can't be read or isn't valid is skipped, its reason goes to
    /// <see cref="ShipCatalog.Problems"/>; a broken module costs only its own entries.
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
        List<ShipProblem> problems = [];
        Dictionary<string, ShipDto> files = [];
        foreach (var id in ids)
        {
            try
            {
                var file = ReadFile(folder, id);
                ships.Add(ToShip(file, id));
                files.Add(id, file);
            }
            catch (Exception ex) when (ex is ShipFormatException or IOException or UnauthorizedAccessException)
            {
                var problem = ex is ShipFormatException ? ex.Message : $"Ship file '{id}{FileSuffix}' can't be read: {ex.Message}";
                if (id == FallbackShipId)
                {
                    throw new ShipFolderException($"Ship file {FallbackShipId}{FileSuffix} is invalid – unzip the release again. {problem}", ex);
                }

                problems.Add(new ShipProblem(id, problem));
            }
        }

        AddModules(Path.Combine(folder, ModulesFolderName), files, ships, problems);
        ships.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return new ShipCatalog(ships, problems);
    }

    // One entry per ship a module names, checked as one ship file; every failure is a problem line, never the start.
    private static void AddModules(string folder, IReadOnlyDictionary<string, ShipDto> files, List<Ship> ships, List<ShipProblem> problems)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var moduleId in ListShips(folder))
        {
            var fileName = moduleId + FileSuffix;
            ModuleDto module;
            try
            {
                module = ReadModule(folder, moduleId);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or IOException or UnauthorizedAccessException)
            {
                problems.Add(new ShipProblem(moduleId, $"Module file '{fileName}' is invalid: {ex.Message}"));
                continue;
            }

            foreach (var shipId in module.Ships)
            {
                var id = module.ConfigurationId(shipId);
                if (!files.TryGetValue(shipId, out var ship) || files.ContainsKey(id))
                {
                    var reason = files.ContainsKey(id) ? $"its id '{id}' is a ship file's" : $"ship '{shipId}' isn't loaded";
                    problems.Add(new ShipProblem(id, $"Module file '{fileName}' can't be added to '{shipId}': {reason}."));
                    continue;
                }

                try
                {
                    ships.Add(module.On(ship).ToShip());
                }
                catch (ArgumentException ex)
                {
                    problems.Add(new ShipProblem(id, $"Module file '{fileName}' doesn't fit ship '{shipId}': {ex.Message}"));
                }
            }
        }
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

    private static Ship LoadFile(string folder, string shipId) => ToShip(ReadFile(folder, shipId), shipId);

    // The file as written, schema and id checked; the domain rules are ToShip's.
    private static ShipDto ReadFile(string folder, string shipId)
    {
        var fileName = shipId + FileSuffix;
        using var stream = File.OpenRead(Path.Combine(folder, fileName));

        try
        {
            using var document = JsonDocument.Parse(stream);
            SchemaVersion.Check(document.RootElement, ReadableSchemaVersions);

            var dto = document.RootElement.Deserialize(ShipJsonContext.Default.ShipDto)
                ?? throw new JsonException("The file contains null.");

            return dto.Id == shipId ? dto : throw new JsonException($"\"id\" is '{dto.Id}' but must match the file name '{shipId}'.");
        }
        catch (JsonException ex)
        {
            throw new ShipFormatException(fileName, ex.Message, ex);
        }
    }

    private static Ship ToShip(ShipDto dto, string shipId)
    {
        try
        {
            return dto.ToShip();
        }
        catch (ArgumentException ex)
        {
            throw new ShipFormatException(shipId + FileSuffix, ex.Message, ex);
        }
    }

    // A module file (K5.3): its own schema, the same strict reading as a ship file.
    private static ModuleDto ReadModule(string folder, string moduleId)
    {
        using var stream = File.OpenRead(Path.Combine(folder, moduleId + FileSuffix));
        using var document = JsonDocument.Parse(stream);
        SchemaVersion.Check(document.RootElement, ModuleSchemaVersion);

        var module = document.RootElement.Deserialize(ShipJsonContext.Default.ModuleDto)
            ?? throw new JsonException("The file contains null.");
        if (module.Id != moduleId)
        {
            throw new JsonException($"\"id\" is '{module.Id}' but must match the file name '{moduleId}'.");
        }

        module.Check();
        return module;
    }
}
