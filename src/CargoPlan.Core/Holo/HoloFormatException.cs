namespace CargoPlan.Core.Holo;

/// <summary>A hologram file or the manifest breaks a rule of docs/holo-format.md.</summary>
public sealed class HoloFormatException(string detail, Exception? innerException = null)
    : Exception($"Ship hologram data is invalid: {detail}", innerException);
