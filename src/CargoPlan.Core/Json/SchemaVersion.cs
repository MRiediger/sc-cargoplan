using System.Text.Json;
using static System.FormattableString;

namespace CargoPlan.Core.Json;

internal static class SchemaVersion
{
    // Checked before deserializing, so a newer file fails with a clear message
    // instead of "unknown property" errors for fields added in later versions.
    public static void Check(JsonElement root, int supportedVersion)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("schemaVersion", out var version)
            || version.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException("Missing numeric \"schemaVersion\".");
        }

        if (!version.TryGetInt32(out var value) || value != supportedVersion)
        {
            throw new JsonException(Invariant(
                $"schemaVersion {version.GetRawText()} is not supported (expected {supportedVersion})."));
        }
    }
}
