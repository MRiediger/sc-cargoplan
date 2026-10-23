using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

public class ShipOutlineLoaderTests
{
    // Slugs struck from outlines/ship-outlines.json because their trace was wrong (D55); those ships show the bow wedge.
    private static readonly string[] StruckSlugs = [];

    private static string TestFile(string name) => Path.Combine(AppContext.BaseDirectory, "TestOutlines", name);

    private static Ship WithSlug(string? slug) =>
        new("test", "Test", 16, [new Rack("rack-0", 0, 16, new Dimensions(4, 2, 2), null, new ShipPoint(0, 0, 0))], FleetyardsSlug: slug);

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

        foreach (var ship in ShipLoader.LoadAll().Ships.Where(s => !StruckSlugs.Contains(s.FleetyardsSlug)))
        {
            outlines.For(ship).Should().NotBeNull(because: $"{ship.Id} ({ship.FleetyardsSlug}) needs an outline or an entry in {nameof(StruckSlugs)}");
        }
    }

    [Fact]
    public void Hull_a_outline_points_to_the_bow_and_is_in_metres()
    {
        var outline = ShipOutlineLoader.Load().For(ShipLoader.Load("hull_a"))!;

        // 30 m long as in the export; the traced silhouette is 12 m wide (the export's beam says 12.5).
        (outline.Points.Max(p => p.X) - outline.Points.Min(p => p.X)).Should().BeApproximately(30, 0.1);
        (outline.Points.Max(p => p.Y) - outline.Points.Min(p => p.Y)).Should().BeApproximately(12, 0.1);
    }

    // --- Matching ---

    [Fact]
    public void For_matches_by_fleetyards_slug()
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
