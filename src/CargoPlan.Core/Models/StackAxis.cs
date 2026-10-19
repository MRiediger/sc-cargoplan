namespace CargoPlan.Core.Models;

/// <summary>
/// The ship direction a rack's crates stack in, away from its plate (D26): x towards the bow, y towards port, z up (D14).
/// Hull A spindle plates stack outwards (port +y, starboard −y), floors up (+z), hull undersides down (−z).
/// </summary>
public enum StackAxis
{
    PlusX,
    MinusX,
    PlusY,
    MinusY,
    PlusZ,
    MinusZ,
}
