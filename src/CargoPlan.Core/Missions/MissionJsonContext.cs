using System.Text.Json.Serialization;

namespace CargoPlan.Core.Missions;

// Same strict options as ShipJsonContext: mission files are written by hand, a typo must fail loudly.
// WriteIndented only affects writing (export), so exported files stay readable and editable.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    WriteIndented = true)]
[JsonSerializable(typeof(MissionFileDto))]
internal sealed partial class MissionJsonContext : JsonSerializerContext;
