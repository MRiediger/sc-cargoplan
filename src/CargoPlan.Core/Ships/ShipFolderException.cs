namespace CargoPlan.Core.Ships;

/// <summary>
/// The ships folder can't be used: it is missing, or the fallback ship (<see cref="ShipLoader.FallbackShipId"/>) is
/// missing or broken. The app can't start without it; the message tells the player what to do (D56).
/// </summary>
public sealed class ShipFolderException(string message, Exception? innerException = null) : Exception(message, innerException);
