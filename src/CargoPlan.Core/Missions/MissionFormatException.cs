namespace CargoPlan.Core.Missions;

/// <summary>A mission file is not valid JSON, not in the expected format, or violates domain rules.</summary>
public sealed class MissionFormatException(string sourceName, string detail, Exception? innerException = null)
    : Exception($"Mission file '{sourceName}' is invalid: {detail}", innerException)
{
    /// <summary>File path or resource name, whatever the caller passed in.</summary>
    public string SourceName { get; } = sourceName;
}
