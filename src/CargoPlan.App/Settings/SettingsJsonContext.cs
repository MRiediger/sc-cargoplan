using System.Text.Json.Serialization;

namespace CargoPlan.App.Settings;

// Same strict options as Core's MissionJsonContext: settings.json may be edited by hand (README),
// a typo must not be silently ignored. A broken file means defaults plus a notice, see SettingsStore.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
