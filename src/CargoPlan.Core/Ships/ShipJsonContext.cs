using System.Text.Json.Serialization;

namespace CargoPlan.Core.Ships;

// Source-generated serializer: no runtime reflection, so it also works trimmed/AOT.
// Strict on purpose: a typo in a community PR must fail loudly, not be ignored.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(ShipDto))]
[JsonSerializable(typeof(ModuleDto))]
internal sealed partial class ShipJsonContext : JsonSerializerContext;
