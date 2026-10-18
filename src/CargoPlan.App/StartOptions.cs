namespace CargoPlan.App;

/// <summary>Start switches of the overlay. Unknown arguments are ignored, so nothing keeps the overlay from starting.</summary>
/// <param name="Locked">Start with the manual lock on (click-through); the lock window unlocks it.</param>
/// <param name="PlanPath">Load this plan file instead of the saved plan; it becomes the saved plan on the next save.</param>
/// <param name="Problem">A switch that couldn't be used, for the notice line; null = all fine.</param>
public sealed record StartOptions(bool Locked, string? PlanPath, string? Problem)
{
    public const string LockedSwitch = "--locked";

    public const string PlanSwitch = "--plan";

    public static StartOptions Parse(IReadOnlyList<string> args)
    {
        var locked = false;
        string? planPath = null;
        string? problem = null;

        for (var i = 0; i < args.Count; i++)
        {
            if (Is(args[i], LockedSwitch))
            {
                locked = true;
            }
            else if (Is(args[i], PlanSwitch))
            {
                // "--plan --locked" is a forgotten path, not a file named "--locked".
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    planPath = args[++i];
                }
                else
                {
                    problem = $"{PlanSwitch} needs the path of a plan file.";
                }
            }
        }

        return new StartOptions(locked, planPath, problem);
    }

    private static bool Is(string arg, string name) => arg.Equals(name, StringComparison.OrdinalIgnoreCase);
}
