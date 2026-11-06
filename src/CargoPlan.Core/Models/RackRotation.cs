namespace CargoPlan.Core.Models;

/// <summary>
/// How a rack is turned in the ship, in degrees (D91): about the middle of its box, right-handed about the ship's axes,
/// roll about x first, then pitch about y, then yaw about z. Positive yaw turns the bow end to port, positive pitch the bow
/// end down, positive roll the port side up. Data for the view: Core computes where the rack lies before it, in whole cells.
/// </summary>
public sealed record RackRotation(double Yaw, double Pitch, double Roll);
