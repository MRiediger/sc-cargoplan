namespace CargoPlan.Core.Ships;

/// <summary>A ship file exists but is not valid JSON, not in the expected format, or violates domain rules.</summary>
public sealed class ShipFormatException(string fileName, string detail, Exception? innerException = null)
    : Exception($"Ship file '{fileName}' is invalid: {detail}", innerException)
{
    /// <summary>The file name in the ships folder, e.g. "hull_a.json".</summary>
    public string FileName { get; } = fileName;
}
