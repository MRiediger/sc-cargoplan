using System.Text;
using System.Text.Json.Nodes;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Every crate names its contract item (D68), so it knows its pickup: two items of one kind are two items, not one.
/// The index counts the items of the mission, stops and items in list order (<see cref="Mission.Items"/>).
/// </summary>
public class CrateItemTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static MissionItem Item(string commodity, int scu, CrateSize max, string[]? pickup = null, CrateSize[]? crates = null) =>
        new(commodity, scu, max, crates, pickup);

    private static SavedPlan Planned(params Mission[] missions) =>
        LoadPlanner.Plan(HullA, missions, [.. missions.SelectMany(m => m.Stops).Select(s => s.Destination).Distinct()]);

    /// <summary>Stims for Everus Harbor from two pickups in one mission, 4 SCU each.</summary>
    private static Mission TwoPickups(int id = 1, CrateSize[]? crates = null) => new(id,
    [
        new("Everus Harbor", 1, [Item("Stims", 4, CrateSize.Four, ["Lorville"], crates), Item("Stims", 4, CrateSize.Four, ["Area18"], crates)]),
    ]);

    /// <summary>Every crate's item holds its destination and commodity.</summary>
    private static void ShouldNameTheirItems(SavedPlan saved)
    {
        foreach (var crate in saved.Plan.Crates)
        {
            var (stop, item) = saved.MissionOf(crate.Mission).Items[crate.Item];
            (stop.Destination, item.Commodity).Should().Be((crate.Destination, crate.Commodity));
        }
    }

    // --- Assignment ---

    [Fact]
    public void Plan_gives_each_item_its_own_crates()
    {
        var saved = Planned(TwoPickups(), new Mission(2, [new("Riker", 1, [Item("Titanium", 8, CrateSize.Eight)])]));

        saved.Plan.Crates.Where(c => c.Mission == 1).Select(c => c.Item).Order().Should().Equal(0, 1);
        saved.Plan.Crates.Single(c => c.Mission == 2).Item.Should().Be(0);
    }

    [Fact]
    public void FromStops_counts_the_items_of_a_stop()
    {
        MissionStop riker = new("Riker", 1, [Item("Titanium", 4, CrateSize.Four), Item("Quartz", 2, CrateSize.Two)]);

        var saved = SavedPlan.FromStops([riker], LoadPlanner.Plan(HullA, new[] { riker }.ToStops()), CrateCap.Of(HullA));

        saved.Plan.Crates.Single(c => c.Commodity == "Quartz").Item.Should().Be(1);
        ShouldNameTheirItems(saved);
    }

    [Fact]
    public void PlaceInto_gives_the_new_crates_their_items()
    {
        SavedPlan empty = new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));

        var saved = LoadPlanner.PlaceInto(empty, HullA, TwoPickups().Stops);

        saved.Plan.Crates.Select(c => c.Item).Order().Should().Equal(0, 1);
    }

    // --- SavedPlan checks per item ---

    [Fact]
    public void SavedPlan_refuses_a_crate_whose_item_doesnt_exist()
    {
        var saved = Planned(TwoPickups());
        Crate[] crates = [saved.Plan.Crates[0] with { Item = 2 }, .. saved.Plan.Crates.Skip(1)];

        var act = () => new SavedPlan(saved.Missions, saved.Plan with { Crates = crates }, saved.CrateCap);

        act.Should().Throw<ArgumentException>().WithMessage("*item 2*mission 1*");
    }

    [Fact]
    public void SavedPlan_refuses_a_crate_whose_item_is_another_commodity()
    {
        var saved = Planned(new Mission(1, [new("Riker", 1, [Item("Titanium", 4, CrateSize.Four), Item("Quartz", 4, CrateSize.Four)])]));
        Crate[] crates = [.. saved.Plan.Crates.Select(c => c with { Item = 1 - c.Item })];

        var act = () => new SavedPlan(saved.Missions, saved.Plan with { Crates = crates }, saved.CrateCap);

        act.Should().Throw<ArgumentException>().WithMessage("*Titanium*item 1*Quartz*");
    }

    [Fact]
    public void SavedPlan_counts_crates_per_item()
    {
        var saved = Planned(TwoPickups());
        Crate[] crates = [.. saved.Plan.Crates.Select(c => c with { Item = 0 })];

        var act = () => new SavedPlan(saved.Missions, saved.Plan with { Crates = crates }, saved.CrateCap);

        act.Should().Throw<ArgumentException>().WithMessage("*Everus Harbor, Stims*");
    }

    // --- Split and merge (D27 addendum) ---

    [Fact]
    public void Split_keeps_the_item_and_rebooks_it()
    {
        var saved = Planned(TwoPickups());
        var second = saved.Plan.Crates.Single(c => c.Item == 1);

        var split = LoadPlanner.SplitCrate(saved, HullA, second);

        split.Plan.Crates.Where(c => c.Item == 1).Select(c => c.CrateSize).Should().Equal(CrateSize.Two, CrateSize.Two);
        split.MissionOf(1).Items[1].Item.Crates.Should().Equal(CrateSize.Two, CrateSize.Two);
        split.MissionOf(1).Items[1].Item.Pickup.Should().Equal("Area18");
        split.MissionOf(1).Items[0].Item.Should().BeSameAs(saved.MissionOf(1).Items[0].Item);
    }

    [Fact]
    public void Merge_after_split_gives_the_item_back()
    {
        var saved = Planned(TwoPickups());
        var split = LoadPlanner.SplitCrate(saved, HullA, saved.Plan.Crates.Single(c => c.Item == 1));

        var merged = LoadPlanner.MergeCrates(split, HullA, [.. split.Plan.Crates.Where(c => c.Item == 1)]);

        merged.Plan.Crates.Single(c => c.Item == 1).CrateSize.Should().Be(CrateSize.Four);
        merged.MissionOf(1).Items[1].Item.Crates.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Merge_refuses_crates_of_two_items_with_or_without_pickup(bool withPickup)
    {
        // Before D68 one item holding 2 + 2 was enough, and the merged 4 could take the other item's crate.
        CrateSize[] twos = [CrateSize.Two, CrateSize.Two];
        Mission mission = new(1,
        [
            new("Everus Harbor", 1,
            [
                Item("Stims", 4, CrateSize.Four, withPickup ? ["Lorville"] : null, twos),
                Item("Stims", 4, CrateSize.Four, withPickup ? ["Area18"] : null, twos),
            ]),
        ]);
        var saved = Planned(mission);
        Crate[] pair = [saved.Plan.Crates.First(c => c.Item == 0), saved.Plan.Crates.First(c => c.Item == 1)];

        LoadPlanner.MergeProblem(saved, HullA, pair).Should().Be("The crates belong to different contract items.");
    }

    // --- Missions changed in place ---

    [Fact]
    public void RenameStop_moves_the_crates_with_their_items()
    {
        // Riker and Riker Station become one stop at Riker's place: Agricium moves from index 2 to 1, Quartz from 1 to 2.
        Mission mission = new(1,
        [
            new("Riker", 1, [Item("Titanium", 4, CrateSize.Four, ["Lorville"])]),
            new("Orison", 2, [Item("Quartz", 2, CrateSize.Two)]),
            new("Riker Station", 3, [Item("Agricium", 8, CrateSize.Eight, ["Area18"])]),
        ]);
        var saved = Planned(mission);

        var renamed = LoadPlanner.RenameStop(saved, HullA, "Riker", "Riker Station");

        renamed.MissionOf(1).Items.Select(i => i.Item.Commodity).Should().Equal("Titanium", "Agricium", "Quartz");
        renamed.Plan.Crates.Single(c => c.Commodity == "Agricium").Item.Should().Be(1);
        renamed.Plan.Crates.Single(c => c.Commodity == "Quartz").Item.Should().Be(2);
        ShouldNameTheirItems(renamed);
    }

    [Fact]
    public void Replan_keeps_the_items_of_a_mission_ticked_in_part()
    {
        // Riker ticked: the replan plans Orison alone, whose item is still the mission's second.
        Mission mission = new(1, [new("Riker", 1, [Item("Titanium", 4, CrateSize.Four)]), new("Orison", 2, [Item("Quartz", 4, CrateSize.Four)])]);
        var saved = Planned(mission);
        SavedPlan ticked = new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, "Riker", true), saved.CrateCap);

        var replanned = LoadPlanner.Replan(ticked, HullA);

        replanned.Plan.Crates.Single(c => c.Commodity == "Quartz").Item.Should().Be(1);
        replanned.Plan.Crates.Single(c => c.Commodity == "Titanium").Should().Match<Crate>(c => c.Item == 0 && c.Delivered);
    }

    // --- Plan file schema 4 ---

    private static string Written(SavedPlan saved)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static SavedPlan Read(string json) => PlanFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "plan.json", HullA);

    [Fact]
    public void Write_writes_schema_4_with_the_item_of_every_crate_and_the_pickups()
    {
        var node = JsonNode.Parse(Written(Planned(TwoPickups())))!;

        node["schemaVersion"]!.GetValue<int>().Should().Be(4);
        node["crates"]!.AsArray().Select(c => c!["item"]!.GetValue<int>()).Order().Should().Equal(0, 1);
        node["missions"]![0]!["stops"]![0]!["items"]![1]!["pickup"]!.AsArray().Select(p => p!.GetValue<string>()).Should().Equal("Area18");
    }

    [Fact]
    public void Write_then_Read_keeps_items_and_pickups()
    {
        var saved = Planned(TwoPickups());

        var loaded = Read(Written(saved));

        loaded.Plan.Crates.Should().Equal(saved.Plan.Crates);
        loaded.MissionOf(1).Items.Select(i => i.Item.Pickup![0]).Should().Equal("Lorville", "Area18");
    }

    [Fact]
    public void Read_of_schema_3_gives_each_mission_its_items_in_order()
    {
        // A schema 3 file is what this app wrote before D68: no "item", crates of one kind per mission in plan order.
        var node = JsonNode.Parse(Written(Planned(TwoPickups(), new Mission(2, [new("Riker", 1, [Item("Titanium", 4, CrateSize.Four), Item("Quartz", 2, CrateSize.Two)])]))))!;
        node["schemaVersion"] = 3;
        foreach (var crate in node["crates"]!.AsArray())
        {
            crate!.AsObject().Remove("item");
        }

        var loaded = Read(node.ToJsonString());

        loaded.Plan.Crates.Where(c => c.Mission == 1).Select(c => c.Item).Should().Equal(0, 1);
        loaded.Plan.Crates.Single(c => c.Commodity == "Quartz").Item.Should().Be(1);
        ShouldNameTheirItems(loaded);
    }

    [Fact]
    public void Read_of_schema_3_with_an_item_is_broken()
    {
        var node = JsonNode.Parse(Written(Planned(TwoPickups())))!;
        node["schemaVersion"] = 3;

        var act = () => Read(node.ToJsonString());

        act.Should().Throw<PlanFormatException>().WithMessage("*item*schema 4*");
    }

    [Fact]
    public void Read_of_schema_4_without_an_item_is_broken()
    {
        var node = JsonNode.Parse(Written(Planned(TwoPickups())))!;
        node["crates"]![0]!.AsObject().Remove("item");

        var act = () => Read(node.ToJsonString());

        act.Should().Throw<PlanFormatException>().WithMessage("*item*");
    }
}
