namespace CargoPlan.Core.Routing;

/// <summary>routes.json is not valid JSON or not in its format (docs/routes-format.md).</summary>
public sealed class RoutesFormatException(string sourceName, string detail, Exception? innerException = null)
    : Exception($"Routes file '{sourceName}' is invalid: {detail}", innerException)
{
    /// <summary>File path or resource name, whatever the caller passed in.</summary>
    public string SourceName { get; } = sourceName;
}
