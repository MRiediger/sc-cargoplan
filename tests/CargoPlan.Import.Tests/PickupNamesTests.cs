using System.Text;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Import.Tests;

/// <summary>A pickup is a place's name, never the contract's text (B6, D69): read from ContractTracker and in old plans.</summary>
public class PickupNamesTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    // --- Parse ---

    [Theory]
    [InlineData("- Freight elevator at Green Glade Station")]
    [InlineData(" / . ")]
    [InlineData("DROP OFF LOCATIONS (ANY ORDER)")]
    public void A_text_without_a_place_name_is_no_pickup(string text) =>
        PickupNames.Parse(text).Should().BeNull();

    [Fact]
    public void A_dash_inside_a_name_stays() =>
        PickupNames.Parse("HUR-L1 Green Glade Station").Should().Equal("HUR-L1 Green Glade Station");

    // --- Clean, loading a plan ---

    [Fact]
    public void Loading_the_plan_of_25_october_cuts_the_pickup_to_its_name()
    {
        var cleaned = PickupNames.Clean(HurstonPickups());

        cleaned.Cleaned.Should().Equal("Everus Harbor");
        cleaned.Saved.Missions.Single(m => m.Id == 4).Items.Should().AllSatisfy(i => i.Item.Pickup.Should().Equal("Everus Harbor"));
    }

    [Fact]
    public void Cleaning_keeps_crates_ticks_and_the_other_missions()
    {
        var saved = HurstonPickups();

        var cleaned = PickupNames.Clean(saved).Saved;

        cleaned.Plan.Crates.Should().Equal(saved.Plan.Crates);
        cleaned.Watermark.Should().Be(saved.Watermark);
        cleaned.Missions.Where(m => m.Id != 4).Should().Equal(saved.Missions.Where(m => m.Id != 4));
    }

    [Fact]
    public void A_clean_plan_comes_back_as_it_is()
    {
        var once = PickupNames.Clean(HurstonPickups()).Saved;

        var twice = PickupNames.Clean(once);

        twice.Saved.Should().BeSameAs(once);
        twice.Cleaned.Should().BeEmpty();
    }

    [Fact]
    public void A_typed_pickup_is_the_players_and_stays()
    {
        // Typed in the editor: no capture behind it, the player chose the text.
        Mission typed = new(1, [new MissionStop("Riker", 1, [new MissionItem("Titanium", 4, CrateSize.Four, Pickup: ["Lorville. - by the gate"])])]);
        SavedPlan saved = new([typed], new LoadPlan(HullA.Id, [new Crate("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(0, 0, 0), Mission: 1)], []), CrateCap.Of(HullA));

        PickupNames.Clean(saved).Saved.Should().BeSameAs(saved);
    }

    private static SavedPlan HurstonPickups()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ImportFixtures.Plan(ImportFixtures.HurstonPickups)));
        return PlanFile.Read(stream, ImportFixtures.HurstonPickups, HullA);
    }
}
