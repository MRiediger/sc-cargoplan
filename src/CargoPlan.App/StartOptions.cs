namespace CargoPlan.App;

/// <summary>Start switches of the overlay. Unknown arguments are ignored, so nothing keeps the overlay from starting.</summary>
/// <param name="Locked">Start with the manual lock on (click-through); the lock window unlocks it.</param>
/// <param name="PlanPath">Load this plan file instead of the saved plan; it becomes the saved plan on the next save.</param>
/// <param name="Problem">A switch that couldn't be used, for the notice line; null = all fine.</param>
/// <param name="ThemeSample">Also open the tool window theme sample (T4.7a); only a Debug build has that window.</param>
/// <param name="MeasureFrames">
/// Measure the ship view's frame time for this ship id, or "empty" for the bare window, instead of starting the overlay
/// (Phase 8, D83); a developer's tool, it reads and writes neither settings.json nor plan.json.
/// </param>
public sealed record StartOptions(bool Locked, string? PlanPath, string? Problem, bool ThemeSample = false, string? MeasureFrames = null)
{
    public const string LockedSwitch = "--locked";

    public const string PlanSwitch = "--plan";

    public const string ThemeSampleSwitch = "--theme-sample";

    public const string MeasureFramesSwitch = "--measure-frames";

    public static StartOptions Parse(IReadOnlyList<string> args)
    {
        var locked = false;
        var themeSample = false;
        string? planPath = null;
        string? measureFrames = null;
        string? problem = null;

        for (var i = 0; i < args.Count; i++)
        {
            if (Is(args[i], LockedSwitch))
            {
                locked = true;
            }
            else if (Is(args[i], ThemeSampleSwitch))
            {
                themeSample = true;
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
            else if (Is(args[i], MeasureFramesSwitch))
            {
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    measureFrames = args[++i];
                }
                else
                {
                    problem = $"{MeasureFramesSwitch} needs a ship id or \"empty\".";
                }
            }
        }

        return new StartOptions(locked, planPath, problem, themeSample, measureFrames);
    }

    private static bool Is(string arg, string name) => arg.Equals(name, StringComparison.OrdinalIgnoreCase);
}
