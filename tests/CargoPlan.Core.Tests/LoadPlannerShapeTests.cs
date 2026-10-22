using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The shape-aware fallback (D54): when the SCU partition doesn't fit a rack's grid, every crate goes largest first into
/// the first rack that takes it. spike_uneven has two racks of 5×5×3: each holds one 32 SCU crate, by SCU it would be two.
/// </summary>
public class LoadPlannerShapeTests
{
    private static readonly Ship Uneven = TestShips.Load("spike_uneven");
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private const string FallbackNote = "Racks not in route order – crates placed where they fit";

    private static Stop StopOf(string destination, int order, string commodity, int scu, CrateSize size) =>
        new(destination, order, [new CargoItem(commodity, scu, size)]);

    /// <summary>
    /// Area18 70 × 1 before Port Tressler 2 × 32. By SCU Port Tressler's 32s share rack 2; by shape each rack takes one,
    /// and Area18's 1s fill the rest of both racks: two stops in both racks, which no cut of the route gives.
    /// </summary>
    private static readonly Stop[] OnesThen32s =
        [StopOf("Area18", 1, "Quartz", 70, CrateSize.One), StopOf("Port Tressler", 2, "Titanium", 64, CrateSize.ThirtyTwo)];

    private static IReadOnlyList<Stop> Run4 => MissionFixtures.Load("spike_uneven_run4");

    private static int Scu(LoadPlan plan) => plan.Crates.Sum(c => (int)c.CrateSize);

    /// <summary>Findings that make a plan unloadable; a crate on cargo unloaded first is only marked (D24 addendum).</summary>
    private static IEnumerable<string> Blocking(LoadPlan plan, Ship ship) =>
        LoadPlanner.Validate(plan, ship).Select(w => w.Message).Where(m => !m.EndsWith("which is unloaded first"));

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Plan_lays_run_4_with_one_32_per_rack(FillOrder fillOrder)
    {
        var plan = LoadPlanner.Plan(Uneven, Run4, fillOrder);

        Scu(plan).Should().Be(104);
        Uneven.Racks.Should().OnlyContain(r => plan.CratesForRack(r.Id).Count(c => c.CrateSize == CrateSize.ThirtyTwo) == 1);
        Blocking(plan, Uneven).Should().BeEmpty();
    }

    [Fact]
    public void Plan_of_run_4_gives_the_derived_hints_and_the_fallback_note()
    {
        var plan = LoadPlanner.Plan(Uneven, Run4);

        plan.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Rack 1 shared by Dest1, Dest2 – identify crates by count"),
            new PlanWarning("rack-0", "Dest1 spans racks 1+2"),
            new PlanWarning("rack-1", "Dest1 spans racks 1+2"),
            new PlanWarning("rack-0", FallbackNote),
            new PlanWarning("rack-1", FallbackNote));
    }

    [Fact]
    public void Plan_lets_two_stops_share_both_racks()
    {
        var plan = LoadPlanner.Plan(Uneven, OnesThen32s);

        Scu(plan).Should().Be(134);
        Blocking(plan, Uneven).Should().BeEmpty();
        plan.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Rack 1 shared by Area18, Port Tressler – identify crates by count"),
            new PlanWarning("rack-0", "Area18 spans racks 1+2"),
            new PlanWarning("rack-0", "Port Tressler spans racks 1+2"),
            new PlanWarning("rack-1", "Rack 2 shared by Area18, Port Tressler – identify crates by count"),
            new PlanWarning("rack-1", "Area18 spans racks 1+2"),
            new PlanWarning("rack-1", "Port Tressler spans racks 1+2"),
            new PlanWarning("rack-0", FallbackNote),
            new PlanWarning("rack-1", FallbackNote));
    }

    [Fact]
    public void Place_lays_the_largest_crate_first_whatever_the_input_order()
    {
        // The route order puts Area18's 1s before the 32; laid in that order, 43 1s would leave no two free layers for it.
        (CrateSize Size, string Destination)[] crates = [.. Enumerable.Repeat((CrateSize.One, "Area18"), 43), (CrateSize.ThirtyTwo, "Port Tressler")];

        var positions = CrateLayout.Place(Uneven.Racks[0], [], crates, ["Area18", "Port Tressler"], FillOrder.BowFirst);

        positions.Should().NotBeNull();
        CrateLayout.Problems(Uneven.Racks[0], [.. crates.Zip(positions!).Select(p =>
            new Crate(p.First.Destination, "Gold", p.First.Size, "rack-0", p.Second))], []).Should().BeEmpty();
    }

    [Fact]
    public void The_fallback_note_stays_through_ticks_moves_and_the_plan_file()
    {
        var plan = LoadPlanner.Plan(Uneven, Run4);
        // A 1 of Dest2 from the top of rack 1 to a free cell on the plate of rack 2.
        var top = plan.Crates.Where(c => c.Destination == "Dest2").MaxBy(c => c.Position.Stack)!;
        HashSet<GridPosition> taken = [.. plan.CratesForRack("rack-1").SelectMany(c => c.Cells)];
        var free = Enumerable.Range(0, 5).SelectMany(l => Enumerable.Range(0, 5).Select(w => new GridPosition(l, w, 0))).First(p => !taken.Contains(p));

        var ticked = LoadPlanner.SetDelivered(plan, Uneven, "Dest1", true);
        var moved = LoadPlanner.MoveCrate(plan, Uneven, top, "rack-1", free);
        var read = ReadBack(SavedPlan.FromStops(MissionFixtures.LoadMissions("spike_uneven_run4"), plan));

        foreach (var warnings in (IReadOnlyList<PlanWarning>[])[ticked.Warnings, moved.Warnings, read.Plan.Warnings, LoadPlanner.HintsOnBoard(ticked, Uneven)])
        {
            warnings.Should().Contain([new PlanWarning("rack-0", FallbackNote), new PlanWarning("rack-1", FallbackNote)]);
        }
    }

    [Fact]
    public void A_new_plan_without_the_fallback_has_no_note()
    {
        // After run 4, Dest2 alone: the SCU partition fits, the note doesn't come along.
        LoadPlanner.Plan(Uneven, Run4);

        var plan = LoadPlanner.Plan(Uneven, [.. Run4.Where(s => s.Destination == "Dest2")]);

        plan.Warnings.Should().NotContain(w => w.Message == FallbackNote);
    }

    [Fact]
    public void Plan_rejects_run_3_naming_what_doesnt_fit()
    {
        var act = () => LoadPlanner.Plan(Uneven, MissionFixtures.Load("spike_uneven_run3"));

        act.Should().Throw<PlanException>().Which.Message.Should().Be(
            "Dest2 doesn't fit: no room for 2 × 32 SCU crates of Titanium – 22 SCU free, but no gap of that shape. Leave a stop out or take a ship with bigger racks.");
    }

    [Fact]
    public void Plan_names_the_stop_whose_32_finds_no_rack()
    {
        Stop[] route =
        [
            StopOf("Area18", 1, "Gold", 32, CrateSize.ThirtyTwo),
            StopOf("Port Tressler", 2, "Iron", 32, CrateSize.ThirtyTwo),
            StopOf("Seraphim", 3, "Titanium", 32, CrateSize.ThirtyTwo),
        ];

        var act = () => LoadPlanner.Plan(Uneven, route);

        act.Should().Throw<PlanException>().Which.Message.Should().Be(
            "Seraphim doesn't fit: no room for 1 × 32 SCU crate of Titanium – 54 SCU free, but no gap of that shape. Leave a stop out or take a ship with bigger racks.");
    }

    [Fact]
    public void Hull_A_load_case_is_laid_whole_without_the_fallback()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.HullAEightStopsMissions.ToStops());

        Scu(plan).Should().Be(58);
        plan.Warnings.Should().NotContain(w => w.Message == FallbackNote);
    }

    private static SavedPlan ReadBack(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        using var input = new MemoryStream(stream.ToArray());
        return PlanFile.Read(input, "plan.json", Uneven);
    }
}
