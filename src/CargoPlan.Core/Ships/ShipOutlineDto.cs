using System.Text.Json.Serialization;

namespace CargoPlan.Core.Ships;

/// <summary>
/// One entry of ship-outlines.json (schema 1). Only the fields the app reads; the rest (name, dimensions, cargo) is
/// skipped, see <see cref="ShipOutlineJsonContext"/>.
/// </summary>
/// <param name="Outline">Points as [x, y] in metres.</param>
internal sealed record ShipOutlineDto(string Slug, IReadOnlyList<IReadOnlyList<double>> Outline);

// Source-generated like ShipJsonContext, but lenient about unknown fields: the file is written by the outline pipeline,
// not by hand, and carries fields the app doesn't read. Required fields and their types stay strict.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(ShipOutlineDto))]
internal sealed partial class ShipOutlineJsonContext : JsonSerializerContext;
