using System.Text.Json;
using System.Text.Json.Serialization;
using static System.FormattableString;

namespace CargoPlan.Core.Holo;

/// <summary>
/// The release's manifest.json (docs/holo-format.md): every hologram it offers. A ship that isn't listed has none or is
/// withdrawn (D63); an empty list withdraws all of them.
/// </summary>
public sealed record HoloManifest(string DataVersion, int FormatVersion, IReadOnlyList<HoloManifestEntry> Ships)
{
    /// <summary>The entry of <paramref name="slug"/> (a ship's <c>fleetyardsSlug</c>, case-sensitive), or null.</summary>
    public HoloManifestEntry? Find(string slug) => Ships.FirstOrDefault(s => s.Slug == slug);
}

/// <param name="File">The asset's name in the release.</param>
/// <param name="Bytes">The file's length; a download of another length is broken.</param>
/// <param name="Sha256">Lower-case hex; a cached file with another hash is stale.</param>
public sealed record HoloManifestEntry(string Slug, string File, long Bytes, string Sha256);

/// <summary>Reads manifest.json; the file boundary like <see cref="HoloReader"/>.</summary>
public static class HoloManifestReader
{
    /// <param name="dataVersion">The release tag the app was built for; another one is not this app's data.</param>
    /// <exception cref="HoloFormatException">Not JSON, a required field missing, another data or format version, an entry without slug, file or hash.</exception>
    public static HoloManifest Read(ReadOnlySpan<byte> json, string dataVersion)
    {
        HoloManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(json, HoloJsonContext.Default.HoloManifest) ?? throw new JsonException("The file contains null.");
        }
        catch (JsonException ex)
        {
            throw new HoloFormatException($"manifest.json: {ex.Message}", ex);
        }

        if (manifest.DataVersion != dataVersion)
        {
            throw new HoloFormatException($"manifest.json is for {manifest.DataVersion}, this app reads {dataVersion}.");
        }

        if (manifest.FormatVersion != HoloReader.FormatVersion)
        {
            throw new HoloFormatException(Invariant($"manifest.json lists format version {manifest.FormatVersion}, this app reads {HoloReader.FormatVersion}."));
        }

        if (manifest.Ships.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.Slug) || string.IsNullOrWhiteSpace(s.File) || string.IsNullOrWhiteSpace(s.Sha256)) is { } bad)
        {
            throw new HoloFormatException($"manifest.json: the entry '{bad.Slug}' needs slug, file and sha256.");
        }

        return manifest;
    }
}

// Source-generated like ShipOutlineJsonContext: lenient about unknown fields (the pipeline writes budget, metrics and
// more), strict about the ones the app reads.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(HoloManifest))]
internal sealed partial class HoloJsonContext : JsonSerializerContext;
