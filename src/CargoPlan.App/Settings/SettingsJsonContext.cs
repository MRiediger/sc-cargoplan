using System.Text.Json.Serialization;

namespace CargoPlan.App.Settings;

// Same strict options as Core's MissionJsonContext: settings.json may be edited by hand (README),
// a typo must not be silently ignored. A broken file means defaults plus a notice, see SettingsStore.
// Look files (K4.3) share the context: the same options for the same records, so the two can't drift apart.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(LookFileContent))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
