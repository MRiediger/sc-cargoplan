using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Discard plan (K2.3, Martin's variant a): the plan empties; nothing remembers what was discarded. The fill order stays,
/// it belongs to the player's way of loading (T4.5).
/// </summary>
public class DiscardTests
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static MissionStop Stop(string destination) => new(destination, 1, [new MissionItem("Stims", 4, CrateSize.Four)]);

    /// <summary>Scans 1 to 3 placed into a stern-first plan, Riker ticked.</summary>
    private static SavedPlan Placed()
    {
        SavedPlan saved = new([], new LoadPlan(HullA.Id, [], [], FillOrder.SternFirst), CrateCap.Of(HullA));
        foreach (var (destination, no) in new[] { ("Riker", 1), ("Shubin", 2), ("Orison", 3) })
        {
            saved = LoadPlanner.PlaceInto(saved, HullA, [Stop(destination)], new ContractSource(new ScanIdentity(no, $"KEY {no}", false)));
        }

        return new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, "Riker", true), saved.CrateCap);
    }

    [Fact]
    public void Discard_empties_the_plan_and_keeps_ship_and_fill_order()
    {
        var discarded = LoadPlanner.Discard(Placed(), HullA);

        discarded.Missions.Should().BeEmpty();
        discarded.Plan.Crates.Should().BeEmpty("the ticks go with the plan");
        discarded.Plan.Warnings.Should().BeEmpty();
        discarded.Plan.ShipId.Should().Be(HullA.Id);
        discarded.Plan.FillOrder.Should().Be(FillOrder.SternFirst);
        discarded.CrateCap.Should().Be(CrateCap.Of(HullA));
    }
}
