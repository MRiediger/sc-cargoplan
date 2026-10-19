using System.Text.Json.Serialization;

namespace CargoPlan.Core.Plans;

// Same strict options as MissionJsonContext: a plan file that doesn't match exactly is broken, not half-read.
// WriteIndented keeps the saved file readable when a player opens it.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    WriteIndented = true)]
[JsonSerializable(typeof(PlanFileDto))]
[JsonSerializable(typeof(PlanFileV1Dto))]
internal sealed partial class PlanJsonContext : JsonSerializerContext;
