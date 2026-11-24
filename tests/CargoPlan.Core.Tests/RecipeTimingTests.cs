using System.Diagnostics;
using System.Globalization;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using Xunit.Abstractions;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The recipe on the pinned release's Everus board (T13.3b): what it takes for a Hull B and a Hull C, and how long it
/// takes, as a table. By hand only: a measurement and a look, not a check of the mechanics (those are <see cref="RecipeTests"/>).
/// </summary>
[Trait("Category", "Timing")]
public class RecipeTimingTests(ITestOutputHelper output)
{
    private static readonly Lazy<HaulingData> Data = new(() =>
        ((HaulingLoad.Loaded)HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder))).Data);

    [Theory]
    [InlineData("hull_b", HaulRank.Senior)]
    [InlineData("hull_b", HaulRank.Master)]
    [InlineData("hull_c", HaulRank.Master)]
    [InlineData("hull_a", HaulRank.Master)]
    public void Everus_recipe_with_its_time(string shipId, HaulRank rank)
    {
        var ship = FrozenShips.Load(shipId);
        var board = Board.For(Data.Value.Templates, StarSystem.Stanton, "Stanton1", rank, null);

        var first = Stopwatch.StartNew();
        var recipe = Recipe.For(board, ship, ObservedEdges.Empty);
        var firstMs = first.Elapsed.TotalMilliseconds;
        List<double> runs = [];
        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            Recipe.For(board, ship, ObservedEdges.Empty);
            runs.Add(watch.Elapsed.TotalMilliseconds);
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{ship.Name}, {rank}, board {board.Templates.Count}: first {firstMs:F1} ms, median {runs.Order().ElementAt(2):F1} ms"));
        foreach (var role in recipe.Outbound.Concat(recipe.Returns))
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  {role.Kind,-6} {role.Template.Title,-48} {role.Template.Reward,9:N0} aUEC {role.WorstScu,4} SCU {role.Crates,3} crates → {string.Join(", ", role.Destinations.Select(PlaceNames.Short))}{(role.AcceptAt is { } at ? $" (accept at {PlaceNames.Short(at)})" : "")}"));
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  Σ {recipe.RewardSum:N0} out · +{recipe.ReturnSum:N0} back = {recipe.RewardSum + recipe.ReturnSum:N0} aUEC · {recipe.WorstScu}/{ship.TotalScu} SCU · {recipe.Crates} crates{(recipe.Note is { } note ? " · " + note : "")}"));
        recipe.Outbound.Should().HaveCountLessThanOrEqualTo(Recipe.MaxContracts);
    }
}
