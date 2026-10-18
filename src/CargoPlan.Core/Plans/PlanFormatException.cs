namespace CargoPlan.Core.Plans;

/// <summary>A plan file is not valid JSON, not in the expected format, or doesn't fit its missions or the ship.</summary>
public sealed class PlanFormatException(string sourceName, string detail, Exception? innerException = null)
    : Exception($"Plan file '{sourceName}' is invalid: {detail}", innerException)
{
    /// <summary>File path or resource name, whatever the caller passed in.</summary>
    public string SourceName { get; } = sourceName;
}
