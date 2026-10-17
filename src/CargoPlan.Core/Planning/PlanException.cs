namespace CargoPlan.Core.Planning;

/// <summary>The stops can't be loaded into the ship. Thrown instead of returning a partial plan.</summary>
public sealed class PlanException(string message) : Exception(message);
