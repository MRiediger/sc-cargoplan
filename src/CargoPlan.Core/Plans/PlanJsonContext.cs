using System.Text.Json.Serialization;

namespace CargoPlan.Core.Plans;

// Same strict options as MissionJsonContext: a plan file that doesn't match exactly is broken, not half-read.
// WriteIndented keeps the saved file readable when a player opens it. WhenWritingNull leaves out what a mission doesn't
// have (contract, drop-off list, an item's crate override) instead of writing null, as the mission file does (D23).
// Source: https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonsourcegenerationoptionsattribute.defaultignorecondition
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(PlanFileDto))]
[JsonSerializable(typeof(PlanFileV2Dto))]
[JsonSerializable(typeof(PlanFileV1Dto))]
internal sealed partial class PlanJsonContext : JsonSerializerContext;
