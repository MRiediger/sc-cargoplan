using CargoPlan.App.Editor;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>Which ship the overlay starts with (T7.2), and the entries of the header's ship selection (T7.2b).</summary>
public class ShipChoicesTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");
    private static readonly Ship C2 = ShipLoader.Load("c2_hercules");

    private static readonly ShipCatalog Catalog = new(
        [C2, HullA], [new ShipProblem("broken", "Ship file 'broken.json' is invalid: no racks.")]);

    // --- Resolve: the start ---

    [Fact]
    public void Resolve_without_a_ship_or_plan_takes_the_Hull_A_quietly()
    {
        ShipChoices.Resolve(Catalog, null, null).Should().Be((HullA, (string?)null));
    }

    [Fact]
    public void Resolve_takes_the_ship_of_the_settings()
    {
        ShipChoices.Resolve(Catalog, "c2_hercules", null).Should().Be((C2, (string?)null));
    }

    [Fact]
    public void Resolve_with_a_missing_ship_takes_the_Hull_A_and_says_so()
    {
        ShipChoices.Resolve(Catalog, "nope", null)
            .Should().Be((HullA, "Ship 'nope' not loaded: not found – using MISC Hull A."));
    }

    [Fact]
    public void Resolve_with_a_broken_ship_takes_the_Hull_A_and_names_the_reason()
    {
        ShipChoices.Resolve(Catalog, "broken", null)
            .Should().Be((HullA, "Ship 'broken' not loaded: Ship file 'broken.json' is invalid: no racks. – using MISC Hull A."));
    }

    [Fact]
    public void Resolve_without_a_ship_in_the_settings_takes_the_plans_ship()
    {
        // Not saved: settings.json gets "ship" only from a choice in the overlay header (D59, T7.2b).
        ShipChoices.Resolve(Catalog, null, "c2_hercules").Should().Be((C2, "Ship set to CRUS C2 Hercules from your plan."));
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("broken")]
    [InlineData("hull_a")]
    public void Resolve_without_a_ship_in_the_settings_and_a_plan_for_no_other_loaded_ship_takes_the_Hull_A_quietly(string planShipId)
    {
        // The plan is planned anew for the Hull A; ShowPlan says so (D59).
        ShipChoices.Resolve(Catalog, null, planShipId).Should().Be((HullA, (string?)null));
    }

    [Fact]
    public void Resolve_with_a_ship_in_the_settings_ignores_the_plans_ship()
    {
        ShipChoices.Resolve(Catalog, "hull_a", "c2_hercules").Should().Be((HullA, (string?)null));
    }

    // --- Of: the editor's selection ---

    [Fact]
    public void Of_groups_by_manufacturer_and_shows_the_name_without_its_code()
    {
        var choices = ShipChoices.Of(Catalog);

        choices.Take(2).Select(c => (c.Group, c.Text, c.Ship)).Should().Equal(("CRUS", "C2 Hercules", C2), ("MISC", "Hull A", HullA));
    }

    [Fact]
    public void Of_lists_ships_that_didnt_load_last_and_not_selectable()
    {
        var broken = ShipChoices.Of(Catalog).Last();

        broken.Should().Be(new ShipChoice(
            "broken", ShipChoices.NotLoadedGroup, "broken – Ship file 'broken.json' is invalid: no racks.", null, "Ship file 'broken.json' is invalid: no racks."));
        broken.IsSelectable.Should().BeFalse();
    }

    [Fact]
    public void Of_puts_a_ship_without_manufacturer_under_Other_with_its_full_name()
    {
        var plain = HullA with { Id = "plain", Name = "Plain Hauler", Manufacturer = null };

        var choices = ShipChoices.Of(new ShipCatalog([plain, C2, HullA], []));

        choices.Select(c => (c.Group, c.Text)).Should().Equal(("CRUS", "C2 Hercules"), ("MISC", "Hull A"), (ShipChoices.OtherGroup, "Plain Hauler"));
    }

    [Fact]
    public void Of_sorts_within_a_group_by_shown_name()
    {
        var shipped = ShipChoices.Of(ShipLoader.LoadAll()).Where(c => c.Group == "CRUS").Select(c => c.Text);

        shipped.Should().Equal("A2 Hercules", "C1 Spirit", "C2 Hercules", "Intrepid", "M2 Hercules", "Mercury Star Runner");
    }

    // --- Recent (T7.2c) ---

    private static readonly Ship HullC = ShipLoader.Load("hull_c");
    private static readonly Ship Freelancer = ShipLoader.Load("freelancer");
    private static readonly Ship Starfarer = ShipLoader.Load("starfarer");

    private static readonly ShipCatalog Five = new([C2, HullA, HullC, Freelancer, Starfarer], [new ShipProblem("broken", "no racks")]);

    [Fact]
    public void Of_puts_the_recent_ships_on_top_with_their_full_names_and_keeps_them_in_their_groups()
    {
        var choices = ShipChoices.Of(Five, ["hull_c", "c2_hercules"], "hull_a");

        choices.Take(2).Select(c => (c.Group, c.Text, c.Ship)).Should().Equal(
            (ShipChoices.RecentGroup, "MISC Hull C", HullC), (ShipChoices.RecentGroup, "CRUS C2 Hercules", C2));
        choices.Skip(2).Should().Equal(ShipChoices.Of(Five));
    }

    [Fact]
    public void Of_leaves_the_current_ship_out_of_Recent_and_shows_three()
    {
        var choices = ShipChoices.Of(Five, ["hull_a", "hull_c", "c2_hercules", "freelancer", "starfarer"], "hull_a");

        choices.Where(c => c.Group == ShipChoices.RecentGroup).Select(c => c.Id)
            .Should().Equal("hull_c", "c2_hercules", "freelancer");
    }

    [Fact]
    public void Of_skips_recent_ids_that_dont_load()
    {
        var choices = ShipChoices.Of(Five, ["nope", "broken", "freelancer"], "hull_a");

        choices.Where(c => c.Group == ShipChoices.RecentGroup).Select(c => c.Id).Should().Equal("freelancer");
    }

    [Fact]
    public void Of_without_recent_ships_has_no_Recent_group()
    {
        ShipChoices.Of(Five, ["hull_a"], "hull_a").Should().Equal(ShipChoices.Of(Five));
    }

    [Fact]
    public void Remember_puts_the_new_ship_first_and_the_one_left_behind_second()
    {
        ShipChoices.Remember([], "hull_a", "c2_hercules").Should().Equal("c2_hercules", "hull_a");
    }

    [Fact]
    public void Remember_moves_a_ship_already_listed_to_the_front_without_a_duplicate()
    {
        ShipChoices.Remember(["c2_hercules", "hull_a", "hull_c"], "c2_hercules", "hull_c")
            .Should().Equal("hull_c", "c2_hercules", "hull_a");
    }

    [Fact]
    public void Remember_keeps_the_current_ship_and_three_more()
    {
        ShipChoices.Remember(["freelancer", "hull_a", "hull_c", "c2_hercules"], "freelancer", "starfarer")
            .Should().Equal("starfarer", "freelancer", "hull_a", "hull_c");
    }

    // --- Search (T7.2c) ---

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("hull")]
    [InlineData("HULL A")]
    [InlineData("misc")]
    [InlineData("misc hull")]
    [InlineData(" hull a ")]
    public void Matches_finds_a_ship_by_name_or_manufacturer_ignoring_case(string query)
    {
        ShipChoices.Matches(ShipChoices.Of(Catalog).Single(c => c.Id == "hull_a"), query).Should().BeTrue();
    }

    [Theory]
    [InlineData("hercules")]
    [InlineData("rec")]
    [InlineData("other")]
    public void Matches_ignores_the_group_names(string query)
    {
        var recentHullA = ShipChoices.Of(Five, ["hull_a"], "c2_hercules").First();

        ShipChoices.Matches(recentHullA, query).Should().BeFalse();
    }

    [Fact]
    public void Matches_finds_a_ship_that_didnt_load_by_its_id()
    {
        ShipChoices.Matches(ShipChoices.Of(Catalog).Last(), "brok").Should().BeTrue();
    }

    [Fact]
    public void SingleMatch_is_the_one_ship_left_also_when_it_is_listed_twice()
    {
        var shown = ShipChoices.Of(Five, ["hull_c"], "hull_a").Where(c => ShipChoices.Matches(c, "hull c")).ToList();

        shown.Should().HaveCount(2);
        ShipChoices.SingleMatch(shown)!.Id.Should().Be("hull_c");
    }

    [Fact]
    public void SingleMatch_is_null_for_two_ships_or_none()
    {
        var all = ShipChoices.Of(Five);

        ShipChoices.SingleMatch(all.Where(c => ShipChoices.Matches(c, "hull"))).Should().BeNull();
        ShipChoices.SingleMatch(all.Where(c => ShipChoices.Matches(c, "xyz"))).Should().BeNull();
    }

    [Fact]
    public void SingleMatch_ignores_ships_that_didnt_load()
    {
        var shown = ShipChoices.Of(Five).Where(c => c.Id is "broken" or "freelancer");

        ShipChoices.SingleMatch(shown)!.Id.Should().Be("freelancer");
    }
}
