using System.Text.Json.Serialization;

namespace CargoPlan.Core.Missions;

// Same strict options as ShipJsonContext: mission files are written by hand, a typo must fail loudly.
// WriteIndented only affects writing (export), so exported files stay readable and editable.
// WhenWritingNull leaves out "crates" of an item without override instead of writing null (D23); only version 2 is
// ever written. Source: https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonsourcegenerationoptionsattribute.defaultignorecondition
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(MissionFileDto))]
[JsonSerializable(typeof(MissionFileV1Dto))]
internal sealed partial class MissionJsonContext : JsonSerializerContext;
