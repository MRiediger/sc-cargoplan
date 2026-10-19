using CargoPlan.App.ShipView;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

public class CrateSelectionTests
{
    private static ShipScene Scene()
    {
        var (ship, plan) = TestPlans.Load();
        return ShipViewBuilder.Build(plan, ship, LoadPlanner.Validate(plan, ship));
    }

    [Fact]
    public void Empty_selects_nothing()
    {
        CrateSelection.Empty.In(Scene()).Should().BeEmpty();
    }

    [Fact]
    public void Toggle_adds_a_crate_and_toggling_again_removes_it()
    {
        var scene = Scene();
        var (first, second) = (scene.Plan.Crates[0], scene.Plan.Crates[1]);

        var both = CrateSelection.Empty.Toggle(scene, first).Toggle(scene, second);
        var one = both.Toggle(scene, first);

        both.In(scene).Should().BeEquivalentTo([first, second]);
        one.In(scene).Should().BeEquivalentTo([second]);
    }

    [Fact]
    public void A_new_scene_has_an_empty_selection()
    {
        // Every plan change builds a new scene: a move, a tick, a new plan from the editor (D25).
        var scene = Scene();
        var selection = CrateSelection.Empty.Toggle(scene, scene.Plan.Crates[0]);

        selection.In(Scene()).Should().BeEmpty();
        selection.In(null).Should().BeEmpty();
    }

    [Fact]
    public void Toggle_in_a_new_scene_starts_from_empty()
    {
        var (old, scene) = (Scene(), Scene());
        var selection = CrateSelection.Empty.Toggle(old, old.Plan.Crates[0]);

        selection.Toggle(scene, scene.Plan.Crates[1]).In(scene).Should().BeEquivalentTo([scene.Plan.Crates[1]]);
    }
}
