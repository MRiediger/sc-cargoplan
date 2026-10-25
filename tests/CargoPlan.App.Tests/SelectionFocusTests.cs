using CargoPlan.App.ShipView;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>What a crate selection shows (T7.4d, D68, B6): the info line for one crate, its missions framed, the rest dimmed.</summary>
public class SelectionFocusTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    /// <summary>Martin's case: two missions to Riker, the same commodity and crate size, from Lorville and from Everus Harbor.</summary>
    private static SavedPlan TwoRikers()
    {
        Mission[] missions =
        [
            new(1, [new("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Eight, Pickup: ["Lorville"])])]),
            new(2, [new("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Eight, Pickup: ["Everus Harbor"])])]),
            new(3, [new("Orison", 1, [new MissionItem("Stims", 7, CrateSize.Four, Pickup: ["A", "B", "C"]), new MissionItem("Gold", 2, CrateSize.Two)])]),
        ];
        return LoadPlanner.Plan(HullA, missions, ["Riker", "Orison"]);
    }

    private static Crate CrateOf(SavedPlan saved, int mission, string commodity) =>
        saved.Plan.Crates.First(c => c.Mission == mission && c.Commodity == commodity);

    // --- Info line ---

    [Fact]
    public void One_crate_shows_its_pickup_commodity_and_destination()
    {
        var saved = TwoRikers();

        SelectionFocus.InfoLine(saved, new HashSet<Crate> { CrateOf(saved, 1, "Titanium") }).Should().Be("from Lorville · Titanium · to Riker");
        SelectionFocus.InfoLine(saved, new HashSet<Crate> { CrateOf(saved, 2, "Titanium") }).Should().Be("from Everus Harbor · Titanium · to Riker");
    }

    [Fact]
    public void Several_pickups_are_joined_with_a_slash()
    {
        var saved = TwoRikers();

        SelectionFocus.InfoLine(saved, new HashSet<Crate> { CrateOf(saved, 3, "Stims") }).Should().Be("from A / B / C · Stims · to Orison");
    }

    [Fact]
    public void Without_pickup_the_line_says_from_where_is_not_known()
    {
        // B6 (D69): "from ?" like the legend's "From ?", so the line always reads the same way.
        var saved = TwoRikers();

        SelectionFocus.InfoLine(saved, new HashSet<Crate> { CrateOf(saved, 3, "Gold") }).Should().Be("from ? · Gold · to Orison");
    }

    [Fact]
    public void No_or_two_crates_show_no_line()
    {
        var saved = TwoRikers();

        SelectionFocus.InfoLine(saved, new HashSet<Crate>()).Should().BeNull();
        SelectionFocus.InfoLine(saved, new HashSet<Crate> { CrateOf(saved, 1, "Titanium"), CrateOf(saved, 2, "Titanium") }).Should().BeNull();
        SelectionFocus.InfoLine(null, new HashSet<Crate> { CrateOf(saved, 1, "Titanium") }).Should().BeNull();
    }

    // --- Mission frame and dimming ---

    [Fact]
    public void The_selection_frames_its_mission_and_dims_the_others()
    {
        var saved = TwoRikers();
        var missions = SelectionFocus.Missions(new HashSet<Crate> { CrateOf(saved, 1, "Titanium") });

        saved.Plan.Crates.Where(c => SelectionFocus.Frames(c, missions)).Should().OnlyContain(c => c.Mission == 1).And.NotBeEmpty();
        saved.Plan.Crates.Where(c => SelectionFocus.Dims(c, missions)).Should().OnlyContain(c => c.Mission != 1).And.NotBeEmpty();
    }

    [Fact]
    public void Split_crates_stay_framed_with_their_mission()
    {
        var saved = TwoRikers();
        var split = LoadPlanner.SplitCrate(saved, HullA, CrateOf(saved, 1, "Titanium"));
        var part = split.Plan.Crates.First(c => c.Mission == 1);

        var missions = SelectionFocus.Missions(new HashSet<Crate> { part });

        split.Plan.Crates.Where(c => c.Mission == 1).Should().HaveCount(2).And.OnlyContain(c => SelectionFocus.Frames(c, missions));
    }

    [Fact]
    public void Two_missions_selected_frame_both()
    {
        var saved = TwoRikers();

        var missions = SelectionFocus.Missions(new HashSet<Crate> { CrateOf(saved, 1, "Titanium"), CrateOf(saved, 3, "Gold") });

        missions.Should().BeEquivalentTo([1, 3]);
        saved.Plan.Crates.Where(c => SelectionFocus.Dims(c, missions)).Should().OnlyContain(c => c.Mission == 2);
    }

    [Fact]
    public void Without_selection_nothing_is_framed_or_dimmed_and_free_cells_never_are()
    {
        var saved = TwoRikers();
        var none = SelectionFocus.Missions(new HashSet<Crate>());
        var some = SelectionFocus.Missions(new HashSet<Crate> { CrateOf(saved, 1, "Titanium") });

        saved.Plan.Crates.Should().OnlyContain(c => !SelectionFocus.Frames(c, none) && !SelectionFocus.Dims(c, none));
        SelectionFocus.Dims(null, some).Should().BeFalse();
        SelectionFocus.Frames(null, some).Should().BeFalse();
    }
}
