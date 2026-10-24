using CargoPlan.Core.Models;

namespace CargoPlan.Core.Planning;

/// <summary>
/// What a move gesture gives (D65): the plan after it, the stage that found room, and what the player should know
/// about cargo that moved without being dragged. One result for the overlay's preview and for the drop, so both agree.
/// </summary>
/// <param name="Plan">The plan after the move; null if <see cref="Stage"/> is <see cref="MoveStage.Pending"/> or <see cref="MoveStage.Refused"/>.</param>
/// <param name="Refusal">Why nothing moves ("no room for 4 SCU crate"); only with <see cref="MoveStage.Refused"/>.</param>
/// <param name="Notices">Lines for the player: cargo displaced to make room, cargo placed in another rack than aimed at.</param>
public sealed record MoveOutcome(LoadPlan? Plan, MoveStage Stage, string? Refusal, IReadOnlyList<string> Notices);
