using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Discard plan (K2.3, Martin's variant a): the plan empties and the sync's baseline goes back to 0, so the next sync
/// takes every ContractTracker contract in again. The fill order stays, it belongs to the player's way of loading (T4.5).
/// </summary>
public class DiscardTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static MissionStop Stop(string destination) => new(destination, 1, [new MissionItem("Stims", 4, CrateSize.Four)]);

    private static readonly ContractCapture[] Captures =
    [
        new([Stop("Riker")], new ContractSource([36])),
        new([Stop("Shubin")], new ContractSource([37])),
        new([Stop("Orison")], new ContractSource([38])),
    ];

    private static readonly HashSet<long> Present = [36, 37, 38];

    /// <summary>The three captures synced into a stern-first plan, watermark 38, Riker ticked.</summary>
    private static SavedPlan Synced()
    {
        SavedPlan empty = new([], new LoadPlan(HullA.Id, [], [], FillOrder.SternFirst), CrateCap.Of(HullA));
        var synced = ContractSync.Apply(empty, HullA, Captures, Present);
        return new(synced.Missions, LoadPlanner.SetDelivered(synced.Plan, HullA, "Riker", true), synced.CrateCap, synced.Watermark);
    }

    [Fact]
    public void Discard_empties_the_plan_and_its_baseline_and_keeps_ship_and_fill_order()
    {
        var saved = Synced();
        saved.Watermark.Should().Be(38);

        var discarded = LoadPlanner.Discard(saved, HullA);

        discarded.Missions.Should().BeEmpty();
        discarded.Plan.Crates.Should().BeEmpty("the ticks go with the plan");
        discarded.Plan.Warnings.Should().BeEmpty();
        discarded.Watermark.Should().Be(0);
        discarded.Plan.ShipId.Should().Be(HullA.Id);
        discarded.Plan.FillOrder.Should().Be(FillOrder.SternFirst);
        discarded.CrateCap.Should().Be(CrateCap.Of(HullA));
    }

    [Fact]
    public void The_next_sync_after_a_discard_takes_every_contract_in_again()
    {
        var saved = Synced();

        var again = ContractSync.Apply(LoadPlanner.Discard(saved, HullA), HullA, Captures, Present);

        again.Missions.Select(m => m.Contract!.ContractIds.Single()).Should().Equal(36, 37, 38);
        again.Plan.Crates.Should().OnlyContain(c => !c.Delivered);
        again.Watermark.Should().Be(38);
        again.Plan.FillOrder.Should().Be(FillOrder.SternFirst);
    }

    [Fact]
    public void Without_a_discard_the_same_sync_takes_nothing()
    {
        var saved = Synced();

        ContractSync.Apply(saved, HullA, Captures, Present).Should().BeSameAs(saved);
    }
}
