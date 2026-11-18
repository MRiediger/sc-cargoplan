using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CargoPlan.Core.ShipData;

/// <summary>
/// What the app reads of a data release's manifest.json (sc-cargoplan-shipdata, docs/manifest-format.md, SD2): the tag,
/// the ship JSON schema, the archive to download and the hash of every file in it. Holograms are T12.4's.
/// </summary>
/// <param name="ShipsAsset">The asset name of <c>ships.zip</c>, flat: its URL is <c>…/releases/download/&lt;tag&gt;/&lt;asset&gt;</c>.</param>
/// <param name="Files">Path inside the zip → SHA-256 (hex): exactly the files the app unpacks.</param>
public sealed record ShipDataManifest(ShipDataVersion DataVersion, int SchemaVersion, string ShipsAsset, IReadOnlyDictionary<string, string> Files);

/// <summary>A manifest or a download that doesn't hold to the contract: nothing of it is used.</summary>
public sealed class ShipDataFormatException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>Reads manifest.json: the boundary to a repository of its own (A6), so every field the app uses is checked.</summary>
public static partial class ShipDataManifestReader
{
    public const string FileName = "manifest.json";

    /// <summary>The paths the app unpacks (E1 of the T12.3 approval): the outlines, ship files and module files, no deeper.</summary>
    public const string OutlinesPath = "outlines.json";

    /// <summary>Without the fallback ship the folder can't start the app (<see cref="Ships.ShipLoader.FallbackShipId"/>).</summary>
    public const string FallbackShipPath = "ships/" + Ships.ShipLoader.FallbackShipId + ".json";

    /// <exception cref="ShipDataFormatException">Not JSON, a field missing, a tag, asset, path or hash out of form, the fallback ship or the outlines not listed.</exception>
    public static ShipDataManifest Read(ReadOnlySpan<byte> json)
    {
        ManifestDto dto;
        try
        {
            dto = JsonSerializer.Deserialize(json, ShipDataJsonContext.Default.ManifestDto) ?? throw new JsonException("The file contains null.");
        }
        catch (JsonException ex)
        {
            throw new ShipDataFormatException($"{FileName}: {ex.Message}", ex);
        }

        if (!ShipDataVersion.TryParse(dto.DataVersion, out var version))
        {
            throw new ShipDataFormatException($"{FileName}: dataVersion '{dto.DataVersion}' is not a release tag like ships-2026.11.1.");
        }

        if (!AssetName().IsMatch(dto.Assets.Ships))
        {
            throw new ShipDataFormatException($"{FileName}: assets.ships '{dto.Assets.Ships}' is not a file name of the release.");
        }

        foreach (var (path, hash) in dto.Files)
        {
            if (!FilePath().IsMatch(path))
            {
                throw new ShipDataFormatException($"{FileName}: files has '{path}', which is not a path of ships.zip.");
            }

            if (!Sha256().IsMatch(hash))
            {
                throw new ShipDataFormatException($"{FileName}: the hash of '{path}' is not a SHA-256.");
            }
        }

        foreach (var required in (string[])[FallbackShipPath, OutlinesPath])
        {
            if (!dto.Files.ContainsKey(required))
            {
                throw new ShipDataFormatException($"{FileName}: files lacks '{required}'.");
            }
        }

        return new ShipDataManifest(version, dto.SchemaVersion, dto.Assets.Ships, dto.Files);
    }

    // A release asset: no folder, no drive, nothing that starts with a dot.
    [GeneratedRegex(@"^[A-Za-z0-9_-][A-Za-z0-9_.-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex AssetName();

    // E1: outlines.json, ships/<name>.json or ships/modules/<name>.json; '/' only, as zip entries are written.
    [GeneratedRegex(@"^(?:outlines\.json|ships/(?:modules/)?[A-Za-z0-9_-]+\.json)\z", RegexOptions.CultureInvariant)]
    private static partial Regex FilePath();

    [GeneratedRegex(@"^[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256();

    internal sealed record ManifestDto(string DataVersion, int SchemaVersion, AssetsDto Assets, Dictionary<string, string> Files);

    internal sealed record AssetsDto(string Ships);
}

// Lenient about unknown fields like HoloJsonContext: the pipeline writes builtFrom, holo, ships and more, and may add
// fields without an app release; strict about the ones the app reads.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(ShipDataManifestReader.ManifestDto))]
internal sealed partial class ShipDataJsonContext : JsonSerializerContext;
