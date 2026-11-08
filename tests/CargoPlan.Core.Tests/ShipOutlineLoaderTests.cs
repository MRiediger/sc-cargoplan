using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

public class ShipOutlineLoaderTests
{
    // Slugs struck from outlines/ship-outlines.json because their trace was wrong (D55); those ships show the bow wedge.
    private static readonly string[] StruckSlugs = [];

    // Ships newer than the outlines: they show the bow wedge (D62) until the pipeline traces them. Empty since K5.2b
    // traced the Salvation (index of 26 Oct 2026).
    private static readonly string[] NotTracedYet = [];

    private static string TestFile(string name) => Path.Combine(AppContext.BaseDirectory, "TestOutlines", name);

    private static Ship WithSlug(string? slug) =>
        new("test", "Test", 16, [new Rack("rack-0", 0, 16, new Dimensions(4, 2, 2), null, new ShipPoint(0, 0, 0))], ModelKey: slug);

    // --- The shipped file (outlines/ next to the test assembly) ---

    [Fact]
    public void The_shipped_file_loads_without_problems()
    {
        var catalog = ShipOutlineLoader.Load();

        catalog.Problems.Should().BeEmpty();
        catalog.Outlines.Should().NotBeEmpty();
    }

    [Fact]
    public void Every_shipped_ship_has_an_outline_unless_it_was_struck()
    {
        var outlines = ShipOutlineLoader.Load();

        // A ship without a model key has no outline to look up and shows the bow wedge (D62): the Cyclone (D92).
        foreach (var ship in ShipLoader.LoadAll().Ships.Where(s => s.ModelKey is not null && !StruckSlugs.Contains(s.ModelKey) && !NotTracedYet.Contains(s.ModelKey)))
        {
            outlines.For(ship).Should().NotBeNull(because: $"{ship.Id} ({ship.ModelKey}) needs an outline or an entry in {nameof(StruckSlugs)}");
        }
    }

    // An entry here must still be missing: once traced, it goes from the list.
    [Fact]
    public void A_ship_not_traced_yet_has_no_outline()
    {
        var outlines = ShipOutlineLoader.Load();

        foreach (var ship in ShipLoader.LoadAll().Ships.Where(s => NotTracedYet.Contains(s.ModelKey)))
        {
            outlines.For(ship).Should().BeNull(because: $"{ship.ModelKey} has an outline now, take it from {nameof(NotTracedYet)}");
        }
    }

    [Fact]
    public void Hull_a_outline_is_the_game_models_top_view_in_metres_around_its_origin()
    {
        var outline = ShipOutlineLoader.Load().For(ShipLoader.Load("hull_a"))!;

        // The Motherlode model in the spindle's cargo pose (D93): 29.41 m long (Fleetyards' flight pose: 23.42), 9.32 m
        // wide; its origin stays the model's, 11.06 m behind the bow and 18.35 m ahead of the stern.
        (outline.Points.Max(p => p.X) - outline.Points.Min(p => p.X)).Should().BeApproximately(29.41, 0.1);
        (outline.Points.Max(p => p.Y) - outline.Points.Min(p => p.Y)).Should().BeApproximately(9.32, 0.1);
        outline.Points.Max(p => p.X).Should().BeApproximately(11.06, 0.1);
        outline.Points.Min(p => p.X).Should().BeApproximately(-18.35, 0.1);
    }

    // The outline lies on the hologram's floor (D83), which needs the hull's height also where no hologram is loaded (T8.5).
    [Fact]
    public void Every_shipped_outline_has_its_height()
    {
        ShipOutlineLoader.Load().Outlines.Values.Should().AllSatisfy(o => o.HeightM.Should().BePositive(because: o.Slug));
    }

    // --- Height ---

    [Fact]
    public void The_height_is_read_in_metres()
    {
        ShipOutlineLoader.Load(TestFile("heights.json")).For(WithSlug("tall"))!.HeightM.Should().Be(6.25);
    }

    [Fact]
    public void An_outline_without_a_height_loads_without_one()
    {
        var catalog = ShipOutlineLoader.Load(TestFile("heights.json"));

        catalog.For(WithSlug("no-height"))!.HeightM.Should().BeNull();
        catalog.Problems.Should().NotContainMatch("*'no-height'*");
    }

    [Theory]
    [InlineData("zero-height")]
    [InlineData("negative-height")]
    public void A_height_that_is_not_positive_is_dropped_with_a_problem_and_the_outline_stays(string slug)
    {
        var catalog = ShipOutlineLoader.Load(TestFile("heights.json"));

        catalog.For(WithSlug(slug))!.HeightM.Should().BeNull();
        catalog.Problems.Should().ContainSingle(p => p.Contains($"'{slug}'")).Which.Should().Contain("heightM");
    }

    // --- Matching ---

    [Fact]
    public void For_matches_by_model_key()
    {
        var catalog = ShipOutlineLoader.Load(TestFile("mixed.json"));

        catalog.For(WithSlug("good-ship"))!.Points.Should().Equal(new OutlinePoint(10, 0), new OutlinePoint(-10, 5), new OutlinePoint(-10, -5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("no-such-ship")]
    [InlineData("Good-Ship")] // slugs are compared as written, like ship ids
    public void A_ship_without_a_matching_slug_has_no_outline(string? slug)
    {
        ShipOutlineLoader.Load(TestFile("mixed.json")).For(WithSlug(slug)).Should().BeNull();
    }

    // --- Broken entries cost only themselves ---

    [Fact]
    public void Broken_entries_are_skipped_with_a_problem_each_and_the_rest_loads()
    {
        var catalog = ShipOutlineLoader.Load(TestFile("mixed.json"));

        catalog.Outlines.Keys.Should().BeEquivalentTo(["good-ship", "second-good"]);
        catalog.Problems.Should().HaveCount(5);
        catalog.Problems.Should().ContainMatch("*'two-points'*at least 3 points*");
        catalog.Problems.Should().ContainMatch("*'three-numbers'*[x, y]*");
        catalog.Problems.Should().ContainMatch("*'good-ship'*more than once*");
        catalog.Problems.Should().ContainMatch("*5*slug*");
        catalog.Problems.Should().ContainMatch("*'flat'*no area*");
    }

    [Fact]
    public void A_slug_given_twice_keeps_its_first_outline()
    {
        ShipOutlineLoader.Load(TestFile("mixed.json")).For(WithSlug("good-ship"))!.Points[0].Should().Be(new OutlinePoint(10, 0));
    }

    // --- A broken file never stops the start ---

    [Theory]
    [InlineData("broken.json")]
    [InlineData("wrong_schema.json")]
    [InlineData("ships_not_a_list.json")]
    [InlineData("missing.json")]
    public void A_file_that_does_not_load_gives_no_outlines_and_one_problem(string name)
    {
        var catalog = ShipOutlineLoader.Load(TestFile(name));

        catalog.Outlines.Should().BeEmpty();
        catalog.Problems.Should().ContainSingle().Which.Should().Contain(name);
    }

    [Fact]
    public void Wrong_schema_version_names_the_expected_one()
    {
        ShipOutlineLoader.Load(TestFile("wrong_schema.json")).Problems.Single().Should().Contain("expected 1");
    }
}
