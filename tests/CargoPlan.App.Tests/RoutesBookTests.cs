using System.IO;
using CargoPlan.App.Routing;
using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using CargoPlan.Import.Contracts;

namespace CargoPlan.App.Tests;

/// <summary>
/// routes.json schema 3 through its one writer (T13.6, D125): the route and the runs beside the observations, one lock,
/// one atomic write, an unreadable file kept aside first (D106).
/// </summary>
public sealed class RoutesBookTests : IDisposable
{
    private static readonly DateTime Monday = new(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    private string RoutesPath => Path.Combine(_folder, "routes.json");

    public RoutesBookTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void A_route_set_is_on_disk_beside_the_observations_and_a_restart_reads_it()
    {
        var book = RoutesBook.Load(RoutesPath, Positions.Empty);
        book.Observe(Offer());

        book.SetRoute(SomeRoute());

        var restarted = RoutesBook.Load(RoutesPath, Positions.Empty);
        restarted.Route.Should().BeEquivalentTo(SomeRoute(), options => options.WithStrictOrdering());
        restarted.Observations.Entries.Should().ContainSingle();
        File.ReadAllText(RoutesPath).Should().Contain("\"schemaVersion\": 3");
    }

    [Fact]
    public void An_observation_after_the_route_keeps_it_and_ending_the_route_keeps_the_observations()
    {
        var book = RoutesBook.Load(RoutesPath, Positions.Empty);
        book.SetRoute(SomeRoute());

        book.Observe(Offer());
        RoutesBook.Load(RoutesPath, Positions.Empty).Route.Should().NotBeNull("one writer writes both");

        book.SetRoute(null);
        var restarted = RoutesBook.Load(RoutesPath, Positions.Empty);
        restarted.Route.Should().BeNull();
        restarted.Observations.Entries.Should().ContainSingle();
        File.ReadAllText(RoutesPath).Should().NotContain("\"route\"");
    }

    [Fact]
    public void Runs_are_appended_and_the_oldest_goes_beyond_fifty()
    {
        var book = RoutesBook.Load(RoutesPath, Positions.Empty);

        for (var i = 0; i < RouteRun.MaxKept + 2; i++)
        {
            book.AddRun(SomeRun(i));
        }

        var runs = RoutesBook.Load(RoutesPath, Positions.Empty).Runs;
        runs.Should().HaveCount(RouteRun.MaxKept);
        (runs[0].Name, runs[^1].Name).Should().Be(("run 2", $"run {RouteRun.MaxKept + 1}"));
    }

    [Fact]
    public void An_unreadable_file_is_kept_aside_by_the_first_route_never_overwritten()
    {
        const string Broken = "{ \"schemaVersion\": 3, \"route\": ";
        File.WriteAllText(RoutesPath, Broken);
        var book = RoutesBook.Load(RoutesPath, Positions.Empty);
        book.Route.Should().BeNull();

        book.SetRoute(SomeRoute());

        File.ReadAllText(Directory.GetFiles(_folder, "routes.json.unreadable-*").Should().ContainSingle().Subject).Should().Be(Broken);
        RoutesBook.Load(RoutesPath, Positions.Empty).Route.Should().NotBeNull();
    }

    private static Route SomeRoute() => new(
        "Everus ring", Monday, "Everus Harbor", LoopKind.LocalRing, StarSystem.Stanton, ["HUR-L1 Green Glade Station"],
        [new Placeholder(1, RecipeKind.Bulk, "t-bulk", "Senior Rank - Medium Cargo Haul", 325_250, [new HaulLeg("Waste", 41, 72, CrateSize.Sixteen)],
            ["HUR-L1 Green Glade Station"], FilledBy: "KEY")],
        []);

    private static RouteRun SomeRun(int i) => new(
        $"run {i}", Monday, null, Monday.AddHours(1), "Everus Harbor", LoopKind.LocalRing, StarSystem.Stanton, 6, 50, 4, 877_000, 10, 10, 0);

    private static ScannedContract Offer() => ApplyScanTests.Scanned(false, "50,250", "Deliver 0/8 SCU of Stims to Riker.") with
    {
        Title = new ContractTitle(HaulContractor.Covalex, HaulRank.Junior, HaulSize.Small, false, null),
    };
}
