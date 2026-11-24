using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using static CargoPlan.Core.Tests.TemplateFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The check before accepting (T13.3b, D123, design §4.3 step 3): a scanned template takes the place of the recipe's last
/// role of its kind, and the planner's first fit says whether the whole still lies in the ship. Only for a template the
/// scan matched; Large and Bulk sizes never fit; outside Stanton there is no recipe to fit into.
/// </summary>
public class RecipeFitTests
{
    private const string Everus = "Everus Harbor";
    private const string L1 = "HUR-L1 Green Glade Station";
    private const string L3 = "HUR-L3 Thundering Express Station";

    private static readonly Ship HullB = FrozenShips.Load("hull_b");

    private static HaulTemplate Local(HaulSize size, int maxScu, long reward, CrateSize crate = CrateSize.Sixteen, bool direct = false, string to = L1) =>
        T([], [to], reward, size: size, direct: direct, legs: [Leg("Gold", 1, maxScu, crate)]);

    private static readonly HaulTemplate Bulk = Local(HaulSize.Medium, 400, 400_000);
    private static readonly HaulTemplate SmallA = Local(HaulSize.Small, 48, 90_000, CrateSize.Four);
    private static readonly HaulTemplate SmallB = Local(HaulSize.Small, 48, 60_000, CrateSize.Four, to: L3);

    // Hull B (512): the bulk 400, then two smalls of 48 – 496 SCU.
    private static Recipe Recipe(params HaulTemplate[] more) =>
        Routing.Recipe.For(new Board(StarSystem.Stanton, "Stanton1", Everus, [Bulk, SmallA, SmallB, .. more]), HullB, ObservedEdges.Empty);

    [Fact]
    public void A_template_of_the_recipe_fits_as_its_role()
    {
        var recipe = Recipe();

        RecipeFit.Of(new MatchResult.Matched(Bulk), recipe, HullB).Should().Be(new RecipeFit.FitsAs(RecipeKind.Bulk));
        RecipeFit.Of(new MatchResult.Matched(SmallB), recipe, HullB).Should().Be(new RecipeFit.FitsAs(RecipeKind.Small));
    }

    [Fact]
    public void Another_template_takes_the_place_of_the_last_role_of_its_kind()
    {
        var recipe = Recipe();
        var otherBulk = Local(HaulSize.Medium, 416, 380_000, to: L3);
        var otherSmall = Local(HaulSize.Small, 64, 50_000, CrateSize.Four, to: L3);

        // 416 instead of 400: 416 + 48 + 48 = 512. 64 instead of the last small: 400 + 48 + 64 = 512.
        RecipeFit.Of(new MatchResult.Matched(otherBulk), recipe, HullB).Should().Be(new RecipeFit.FitsAs(RecipeKind.Bulk));
        RecipeFit.Of(new MatchResult.Matched(otherSmall), recipe, HullB).Should().Be(new RecipeFit.FitsAs(RecipeKind.Small));
    }

    [Fact]
    public void A_template_that_leaves_no_room_is_too_big_for_the_ship()
    {
        var recipe = Recipe();
        var bigBulk = Local(HaulSize.Medium, 432, 420_000);
        var direct = Local(HaulSize.Small, 24, 30_000, CrateSize.Four, direct: true);

        // 432 + 48 + 48 = 528 > 512; a direct is added to 496: 520 > 512.
        RecipeFit.Of(new MatchResult.Matched(bigBulk), recipe, HullB).Should().Be(new RecipeFit.TooBig(432, CrateSize.Sixteen, HullB.Name));
        RecipeFit.Of(new MatchResult.Matched(direct), recipe, HullB).Should().Be(new RecipeFit.TooBig(24, CrateSize.Four, HullB.Name));
    }

    [Fact]
    public void A_Large_or_Bulk_size_is_always_too_big()
    {
        var large = Local(HaulSize.Large, 128, 900_000, CrateSize.ThirtyTwo);

        RecipeFit.Of(new MatchResult.Matched(large), Recipe(), HullB).Should().Be(new RecipeFit.TooBig(128, CrateSize.ThirtyTwo, HullB.Name));
    }

    [Fact]
    public void No_hint_without_a_single_template_or_outside_Stanton()
    {
        var recipe = Recipe();
        var redWind = T(["Ruin Station"], ["Checkmate"], null, size: HaulSize.Small, contractor: HaulContractor.RedWind, system: StarSystem.Pyro, areaKey: "Solar");

        RecipeFit.Of(new MatchResult.None("no such template"), recipe, HullB).Should().BeNull();
        RecipeFit.Of(new MatchResult.Ambiguous([Bulk, SmallA]), recipe, HullB).Should().BeNull();
        RecipeFit.Of(new MatchResult.Matched(redWind), recipe, HullB).Should().BeNull();
        RecipeFit.Of(new MatchResult.Matched(Bulk), Routing.Recipe.For(new Board(StarSystem.Pyro, "Solar", null, []), HullB, ObservedEdges.Empty), HullB)
            .Should().BeNull("a board without a hub has no recipe");
    }
}
