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

        foreach (var ship in ShipLoader.LoadAll().Ships.Where(s => !StruckSlugs.Contains(s.FleetyardsSlug) && !NotTracedYet.Contains(s.FleetyardsSlug)))
        {
            outlines.For(ship).Should().NotBeNull(because: $"{ship.Id} ({ship.FleetyardsSlug}) needs an outline or an entry in {nameof(StruckSlugs)}");
        }
    }

    // An entry here must still be missing: once traced, it goes from the list.
    [Fact]
    public void A_ship_not_traced_yet_has_no_outline()
    {
        var outlines = ShipOutlineLoader.Load();

        foreach (var ship in ShipLoader.LoadAll().Ships.Where(s => NotTracedYet.Contains(s.FleetyardsSlug)))
        {
            outlines.For(ship).Should().BeNull(because: $"{ship.FleetyardsSlug} has an outline now, take it from {nameof(NotTracedYet)}");
        }
    }

    [Fact]
    public void Hull_a_outline_points_to_the_bow_and_is_in_metres()
    {
        var outline = ShipOutlineLoader.Load().For(ShipLoader.Load("hull_a"))!;

        // 23.42 m long as in the index of 26 Oct 2026 (30 before, K5.2b); the traced silhouette is 9.38 m wide (the index's
        // beam says 9.35), 12 m scaled by the same 0.781.
        (outline.Points.Max(p => p.X) - outline.Points.Min(p => p.X)).Should().BeApproximately(23.42, 0.1);
        (outline.Points.Max(p => p.Y) - outline.Points.Min(p => p.Y)).Should().BeApproximately(9.38, 0.1);
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
