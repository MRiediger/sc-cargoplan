namespace CargoPlan.Core.Ships;

/// <summary>A ship file exists but is not valid JSON, not in the expected format, or violates domain rules.</summary>
public sealed class ShipFormatException(string resourceName, string detail, Exception? innerException = null)
    : Exception($"Ship resource '{resourceName}' is invalid: {detail}", innerException)
{
    public string ResourceName { get; } = resourceName;
}
