using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The crates picked by right click (D25): view state, not part of the plan and never saved. Bound to the scene it was
/// made in: every plan change builds a new scene (a move, a tick, a new plan), and the selection of an old scene is
/// empty, so no crate of an old plan reaches Core. Immutable, so the view replaces it.
/// </summary>
public sealed class CrateSelection
{
    private readonly ShipScene? _scene;
    private readonly HashSet<Crate> _crates;

    private CrateSelection(ShipScene? scene, HashSet<Crate> crates) => (_scene, _crates) = (scene, crates);

    public static CrateSelection Empty { get; } = new(null, []);

    /// <summary>The selected crates if <paramref name="scene"/> is the scene they were picked in; empty otherwise.</summary>
    public IReadOnlySet<Crate> In(ShipScene? scene) => scene is not null && ReferenceEquals(scene, _scene) ? _crates : Empty._crates;

    /// <summary>Adds the crate, or removes it if it is selected; a selection of an older scene starts from empty.</summary>
    public CrateSelection Toggle(ShipScene scene, Crate crate)
    {
        HashSet<Crate> crates = [.. In(scene)];
        if (!crates.Remove(crate))
        {
            crates.Add(crate);
        }

        return new CrateSelection(scene, crates);
    }
}
