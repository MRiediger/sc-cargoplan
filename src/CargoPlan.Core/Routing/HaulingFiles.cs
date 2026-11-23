using System.Text.Json;
using System.Text.Json.Serialization;
using CargoPlan.Core.Json;
using CargoPlan.Core.Models;
using CargoPlan.Core.ShipData;
using static System.FormattableString;

namespace CargoPlan.Core.Routing;

/// <summary>Templates and positions of the active ship data, for the run (a data change counts from the next start).</summary>
public sealed record HaulingData(IReadOnlyList<HaulTemplate> Templates, Positions Positions);

/// <summary>What the start found of hauling data (T13.2): the three cases of docs/hauling-data-format.md.</summary>
public abstract record HaulingLoad
{
    private HaulingLoad()
    {
    }

    /// <summary>No hauling data: the release has none, or the files were not installed. No notice (K2).</summary>
    public sealed record None(string Reason) : HaulingLoad;

    /// <summary>A schema this app doesn't read, a file that fails its length, hash or a loader check: one log line, a hint in the tab.</summary>
    public sealed record UnknownFormat(string Reason) : HaulingLoad;

    /// <param name="Skipped">One line per template or position left out for a word this app doesn't know.</param>
    public sealed record Loaded(HaulingData Data, IReadOnlyList<string> Skipped) : HaulingLoad;
}

/// <summary>
/// Reads <c>hauling-templates.json</c> and <c>positions.json</c> next to the manifest of the active data (T13.2): the
/// boundary to the pipeline's files (A6), with the loader checks of docs/hauling-data-format.md. Never throws: the
/// hauling data never cost the ship data (K2).
/// </summary>
public static class HaulingFiles
{
    public const int SupportedSchemaVersion = 1;

    public static HaulingLoad Load(ActiveShipData data) => Load(data.Folder, data.Hauling, data.Version.ToString());

    public static HaulingLoad Load(string folder, HaulingOffer offer, string dataVersion)
    {
        if (offer is HaulingOffer.None)
        {
            return new HaulingLoad.None($"{dataVersion} has no hauling data");
        }

        if (offer is HaulingOffer.UnknownFormat unknown)
        {
            return new HaulingLoad.UnknownFormat(unknown.Reason);
        }

        var manifest = ((HaulingOffer.Offered)offer).Manifest;
        byte[][] files = new byte[2][];
        ReleaseAsset[] assets = [manifest.Positions, manifest.Templates];
        for (var i = 0; i < assets.Length; i++)
        {
            try
            {
                files[i] = File.ReadAllBytes(Path.Combine(folder, assets[i].File));
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                // An install whose hauling download failed keeps the ships (T13.2): the files simply aren't there.
                return new HaulingLoad.None($"{assets[i].File} of {dataVersion} is not installed");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new HaulingLoad.UnknownFormat($"{assets[i].File} can't be read: {ex.Message}");
            }

            if (!assets[i].Matches(files[i]))
            {
                return new HaulingLoad.UnknownFormat($"{assets[i].File} doesn't match its length and hash in manifest.json");
            }
        }

        List<string> skipped = [];
        var current = assets[0].File;
        try
        {
            var (positions, unknownPlaces) = ReadPositions(files[0], manifest.SchemaVersion, skipped);
            current = assets[1].File;
            var templates = ReadTemplates(files[1], manifest.SchemaVersion, positions, unknownPlaces, skipped);
            return new HaulingLoad.Loaded(new HaulingData(templates, positions), skipped);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return new HaulingLoad.UnknownFormat($"{current}: {ex.Message}");
        }
    }

    // Positions of a system this app doesn't know are left out; their names, so a pool naming one skips its template.
    private static (Positions Positions, HashSet<string> UnknownPlaces) ReadPositions(byte[] json, int schemaVersion, List<string> skipped)
    {
        using var document = JsonDocument.Parse(json);
        SchemaVersion.Check(document.RootElement, schemaVersion);
        List<StarPosition> positions = [];
        HashSet<string> unknownPlaces = new(StringComparer.Ordinal);
        var number = 0;
        foreach (var entry in List(document.RootElement, "positions"))
        {
            number++;
            var dto = entry.Deserialize(HaulingJsonContext.Default.PositionDto) ?? throw new JsonException(Invariant($"position {number} is null."));
            if (!EnumWords.TryParse<StarSystem>(dto.System, out var system))
            {
                skipped.Add(Invariant($"Position '{dto.Name}': {EnumWords.Unknown<StarSystem>(dto.System)}"));
                unknownPlaces.Add(dto.Name);
                continue;
            }

            positions.Add(new StarPosition(dto.Uuid, dto.Name, system, dto.Type, dto.Parent, dto.X, dto.Y, dto.Z, dto.ContractNames ?? []));
        }

        // K3: a name is unique per system; the constructor refuses a name twice in one.
        return (new Positions(positions), unknownPlaces);
    }

    private static List<HaulTemplate> ReadTemplates(byte[] json, int schemaVersion, Positions positions, HashSet<string> unknownPlaces, List<string> skipped)
    {
        using var document = JsonDocument.Parse(json);
        SchemaVersion.Check(document.RootElement, schemaVersion);
        List<HaulTemplate> templates = [];
        var number = 0;
        foreach (var entry in List(document.RootElement, "templates"))
        {
            number++;
            var dto = entry.Deserialize(HaulingJsonContext.Default.TemplateDto) ?? throw new JsonException(Invariant($"template {number} is null."));
            var name = Invariant($"Template {number} ({dto.Uuid})");
            if (Unknown(dto) is { } word)
            {
                // A new rank or size in a game build costs only its templates.
                skipped.Add($"{name}: {word}");
                continue;
            }

            var template = ToTemplate(dto, name);
            if (template.PickupPool.Concat(template.DropoffPool).FirstOrDefault(p => !Known(template.System, p)) is { } place)
            {
                if (!unknownPlaces.Contains(place))
                {
                    throw new JsonException($"{name}: '{place}' has no position{(template.System is { } s ? $" in {EnumWords.Word(s)}" : "")}.");
                }

                skipped.Add($"{name}: '{place}' lies in a system this app doesn't know.");
                continue;
            }

            templates.Add(template);
        }

        return templates;

        bool Known(StarSystem? system, string place) =>
            system is { } s ? positions.Find(s, place) is not null : positions.AnySystemHas(place);
    }

    // The first enum word this app doesn't know, as a sentence; null if it knows them all.
    private static string? Unknown(TemplateDto dto)
    {
        if (!EnumWords.TryParse<HaulContractor>(dto.Contractor, out _))
        {
            return EnumWords.Unknown<HaulContractor>(dto.Contractor);
        }

        if (!EnumWords.TryParse<HaulRank>(dto.Rank, out _))
        {
            return EnumWords.Unknown<HaulRank>(dto.Rank);
        }

        if (!EnumWords.TryParse<HaulSize>(dto.Size, out _))
        {
            return EnumWords.Unknown<HaulSize>(dto.Size);
        }

        if (!EnumWords.TryParse<HaulShape>(dto.Shape, out _))
        {
            return EnumWords.Unknown<HaulShape>(dto.Shape);
        }

        if (!EnumWords.TryParse<HaulMissionType>(dto.MissionType, out _))
        {
            return EnumWords.Unknown<HaulMissionType>(dto.MissionType);
        }

        if (dto.Faction is { } faction && !EnumWords.TryParse<HaulFaction>(faction, out _))
        {
            return EnumWords.Unknown<HaulFaction>(faction);
        }

        return dto.System is { } system && !EnumWords.TryParse<StarSystem>(system, out _) ? EnumWords.Unknown<StarSystem>(system) : null;
    }

    /// <exception cref="JsonException">A loader check failed: legs, SCU band, crate size, stops.</exception>
    private static HaulTemplate ToTemplate(TemplateDto dto, string name)
    {
        if (dto.Legs.Count < 1)
        {
            throw new JsonException($"{name} has no legs.");
        }

        if (dto.Stops < 1)
        {
            throw new JsonException(Invariant($"{name}: stops is {dto.Stops}, at least 1."));
        }

        List<HaulLeg> legs = [];
        foreach (var leg in dto.Legs)
        {
            if (leg.MinScu < 1 || leg.MinScu > leg.MaxScu)
            {
                throw new JsonException(Invariant($"{name}: the band of {leg.Commodity} ({leg.MinScu}–{leg.MaxScu} SCU) is no band from 1 SCU."));
            }

            if (!Enum.IsDefined((CrateSize)leg.ContainerScu))
            {
                throw new JsonException(Invariant($"{name}: containerScu {leg.ContainerScu} of {leg.Commodity} is no crate size (1, 2, 4, 8, 16, 24, 32)."));
            }

            legs.Add(new HaulLeg(leg.Commodity, leg.MinScu, leg.MaxScu, (CrateSize)leg.ContainerScu));
        }

        return new HaulTemplate(
            dto.Uuid, dto.DebugName, EnumWords.Parse<HaulContractor>(dto.Contractor), dto.Title, EnumWords.Parse<HaulRank>(dto.Rank),
            EnumWords.Parse<HaulSize>(dto.Size), dto.Direct, EnumWords.Parse<HaulShape>(dto.Shape), dto.Stops,
            dto.System is null ? null : EnumWords.Parse<StarSystem>(dto.System), dto.AreaKey,
            EnumWords.Parse<HaulMissionType>(dto.MissionType), dto.Faction is null ? null : EnumWords.Parse<HaulFaction>(dto.Faction),
            dto.Reward, legs, dto.PickupPool, dto.DropoffPool);
    }

    private static JsonElement.ArrayEnumerator List(JsonElement root, string field) =>
        root.TryGetProperty(field, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
            : throw new JsonException($"\"{field}\" must be a list.");

    internal sealed record PositionDto(
        string Uuid,
        string Name,
        string System,
        string Type,
        string? Parent,
        double X,
        double Y,
        double Z,
        IReadOnlyList<string>? ContractNames = null);

    internal sealed record TemplateDto(
        string Uuid,
        string DebugName,
        string Contractor,
        string Title,
        string Rank,
        string Size,
        bool Direct,
        string Shape,
        int Stops,
        string? System,
        string AreaKey,
        string MissionType,
        string? Faction,
        long? Reward,
        IReadOnlyList<LegDto> Legs,
        IReadOnlyList<string> PickupPool,
        IReadOnlyList<string> DropoffPool);

    internal sealed record LegDto(string Commodity, int MinScu, int MaxScu, int ContainerScu);
}

// Unknown fields are ignored (docs/hauling-data-format.md), every field the app reads is required as written.
// Source: https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonsourcegenerationoptionsattribute
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(HaulingFiles.PositionDto))]
[JsonSerializable(typeof(HaulingFiles.TemplateDto))]
internal sealed partial class HaulingJsonContext : JsonSerializerContext;
