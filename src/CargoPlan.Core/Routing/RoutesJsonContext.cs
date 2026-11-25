using System.Text.Json.Serialization;

namespace CargoPlan.Core.Routing;

// The options of PlanJsonContext: a routes file that doesn't match exactly is broken, not half-read; null left out.
// Source: https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonsourcegenerationoptionsattribute
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(RoutesFileDto))]
// Each run alone (K1 of T13.6c): the file keeps them as JsonElements until they are read.
[JsonSerializable(typeof(RunDto))]
internal sealed partial class RoutesJsonContext : JsonSerializerContext;
