using CargoPlan.App.Editor;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;

namespace CargoPlan.App.Tests;

public class MissionRowsTests
{
    private static MissionRow Row(string destination, string commodity, string scu, int maxCrateSize, string crates = "") =>
        new() { Destination = destination, Commodity = commodity, Scu = scu, MaxCrateSize = maxCrateSize, Crates = crates };

    // --- Grouping ---

    [Fact]
    public void Stops_follow_the_first_appearance_of_each_destination()
    {
        MissionRow[] rows =
        [
            Row("Riker", "Titanium", "8", 4),
            Row("Shubin", "Quartz", "4", 2),
            Row("Orison", "Agricium", "8", 8),
            Row("Shubin", "Gold", "2", 1),
        ];

        var result = MissionRows.ToStops(rows, CrateCap.None);

        result.StopNumbers.Should().Equal(1, 2, 3, 2);
        result.Stops.Select(s => (s.Destination, s.Order)).Should().Equal(("Riker", 1), ("Shubin", 2), ("Orison", 3));
    }

    [Fact]
    public void Items_of_a_stop_keep_row_order()
    {
        MissionRow[] rows =
        [
            Row("Shubin", "Quartz", "4", 2),
            Row("Riker", "Titanium", "8", 4),
            Row("Shubin", "Gold", "2", 1),
        ];

        var shubin = MissionRows.ToStops(rows, CrateCap.None).Stops[0];

        shubin.Items.Should().BeEquivalentTo(
            [new MissionItem("Quartz", 4, CrateSize.Two), new MissionItem("Gold", 2, CrateSize.One)], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Names_of_one_place_are_one_stop_with_the_longer_name()
    {
        // B1: "Thundering Express" and "Thundering Express Station" are one cargo lift (DestinationNames).
        MissionRow[] rows = [Row("Thundering Express", "Pressurized Ice", "3", 2), Row("Thundering Express Station", "Stims", "4", 4)];

        var result = MissionRows.ToStops(rows, CrateCap.None);

        result.StopNumbers.Should().Equal(1, 1);
        result.Stops.Should().ContainSingle().Which.Destination.Should().Be("Thundering Express Station");
        MissionRows.ToMissions(rows, []).Missions.Should().ContainSingle()
            .Which.Stops.Should().ContainSingle().Which.Destination.Should().Be("Thundering Express Station");
    }

    [Fact]
    public void Destinations_group_case_insensitive_and_trimmed_and_keep_the_first_spelling()
    {
        // "Riker" and " riker " are the same cargo lift, as in the mission file and the planner.
        MissionRow[] rows = [Row("Riker", "Titanium", "8", 4), Row(" riker ", " Gold ", "2", 1)];

        var stop = MissionRows.ToStops(rows, CrateCap.None).Stops.Should().ContainSingle().Which;

        stop.Destination.Should().Be("Riker");
        stop.Items[1].Commodity.Should().Be("Gold");
    }

    [Fact]
    public void Blank_rows_are_skipped_without_stop_number_or_error()
    {
        MissionRow[] rows = [Row("", " ", "", 4), Row("Riker", "Titanium", "8", 4), new MissionRow()];

        var result = MissionRows.ToStops(rows, CrateCap.None);

        result.StopNumbers.Should().Equal(null, 1, null);
        result.Errors.Should().AllSatisfy(e => e.Should().BeNull());
        result.Stops.Should().ContainSingle();
        result.Problem.Should().BeNull();
    }

    [Fact]
    public void A_row_with_only_crates_typed_is_not_blank()
    {
        MissionRows.ToStops([Row("", "", "", 4, "2+2")], CrateCap.None).Errors.Should().Equal(MissionRows.DestinationEmptyText);
    }

    [Fact]
    public void Only_blank_rows_give_no_stops_and_ask_for_a_mission()
    {
        var result = MissionRows.ToStops([new MissionRow()], CrateCap.None);

        result.Stops.Should().BeEmpty();
        result.IsValid.Should().BeFalse();
        result.Problem.Should().Be(MissionRows.NoMissionsText);
    }

    // --- Crate mix (D23) ---

    [Fact]
    public void A_new_row_starts_with_max_crate_size_4_and_no_override()
    {
        MissionRow row = new();

        row.MaxCrateSize.Should().Be(MissionRows.DefaultMaxCrateSize).And.Be(4);
        row.Crates.Should().BeEmpty();
        row.IsCrateOverride.Should().BeFalse();
    }

    [Fact]
    public void Without_override_the_item_derives_its_crates_and_the_row_shows_them()
    {
        var result = MissionRows.ToStops([Row("Green Glade", "Stims", "6", 4)], CrateCap.None);

        var item = result.Stops[0].Items.Should().ContainSingle().Which;
        item.Crates.Should().BeNull();
        item.MaxCrateSize.Should().Be(CrateSize.Four);
        result.CrateTexts.Should().Equal("4+2");
    }

    [Theory]
    [InlineData("2+2+2")]
    [InlineData(" 2 + 2+2 ")]
    public void An_override_becomes_the_items_crates_and_shows_normalised(string crates)
    {
        var result = MissionRows.ToStops([Row("Green Glade", "Stims", "6", 4, crates)], CrateCap.None);

        result.Stops[0].Items[0].Crates.Should().Equal(CrateSize.Two, CrateSize.Two, CrateSize.Two);
        result.CrateTexts.Should().Equal("2+2+2");
    }

    [Fact]
    public void An_override_equal_to_the_derived_mix_stays_an_override()
    {
        // Typed is typed: the row stays marked as by hand, and the file keeps the list (D23).
        var row = Row("Green Glade", "Stims", "6", 4, "4+2");

        MissionRows.ToStops([row], CrateCap.None).Stops[0].Items[0].Crates.Should().Equal(CrateSize.Four, CrateSize.Two);
        row.IsCrateOverride.Should().BeTrue();
    }

    [Fact]
    public void Crates_text_is_empty_while_the_scu_is_invalid()
    {
        MissionRows.ToStops([Row("Green Glade", "Stims", "abc", 4)], CrateCap.None).CrateTexts.Should().Equal("");
    }

    [Fact]
    public void An_invalid_override_shows_as_typed()
    {
        MissionRows.ToStops([Row("Green Glade", "Stims", "6", 4, "2+2+3")], CrateCap.None).CrateTexts.Should().Equal("2+2+3");
    }

    // --- Validation ---

    [Theory]
    [InlineData("", "Titanium", "8", 4, "", MissionRows.DestinationEmptyText)]
    [InlineData("Riker", " ", "8", 4, "", MissionRows.CommodityEmptyText)]
    [InlineData("Riker", "Titanium", "", 4, "", MissionRows.ScuInvalidText)]
    [InlineData("Riker", "Titanium", "0", 4, "", MissionRows.ScuInvalidText)]
    [InlineData("Riker", "Titanium", "-4", 4, "", MissionRows.ScuInvalidText)]
    [InlineData("Riker", "Titanium", "abc", 4, "", MissionRows.ScuInvalidText)]
    [InlineData("Riker", "Titanium", "4.5", 4, "", MissionRows.ScuInvalidText)]
    [InlineData("Riker", "Titanium", "8", 3, "", MissionRows.MaxCrateSizeInvalidText)]
    [InlineData("Riker", "Titanium", "6", 4, "2+2+3", "3 SCU is not a crate size.")]
    [InlineData("Riker", "Titanium", "6", 4, "2+2", "The crates add up to 4 SCU, not 6.")]
    [InlineData("Riker", "Titanium", "8", 4, "8", "8 SCU crates are larger than the max crate size of 4 SCU.")]
    [InlineData("Riker", "Titanium", "6", 4, "2++4", MissionRows.CratesInvalidText)]
    [InlineData("Riker", "Titanium", "6", 4, "2,2,2", MissionRows.CratesInvalidText)]
    public void Each_rule_has_its_own_message(string destination, string commodity, string scu, int max, string crates, string expected)
    {
        var result = MissionRows.ToStops([Row(destination, commodity, scu, max, crates)], CrateCap.None);

        result.Errors.Should().Equal(expected);
        result.Stops.Should().BeEmpty();
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Any_scu_fits_any_max_crate_size_without_override()
    {
        // Before D23, 17 SCU in 4 SCU crates was an error; now it is 4+4+4+4+1.
        MissionRows.ToStops([Row("Riker", "Titanium", "17", 4)], CrateCap.None).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Problem_names_the_first_invalid_row_one_based()
    {
        MissionRow[] rows = [Row("Riker", "Titanium", "8", 4), Row("Shubin", "Quartz", "6", 4, "2+2"), Row("Orison", "", "8", 8)];

        MissionRows.ToStops(rows, CrateCap.None).Problem.Should().Be("Row 2: The crates add up to 4 SCU, not 6.");
    }

    [Fact]
    public void One_invalid_row_empties_the_stops_but_keeps_all_stop_numbers()
    {
        // The stop column still shows what the arrows do while a row is being fixed.
        MissionRow[] rows = [Row("Riker", "Titanium", "8", 4), Row("Shubin", "Quartz", "abc", 4)];

        var result = MissionRows.ToStops(rows, CrateCap.None);

        result.Stops.Should().BeEmpty();
        result.StopNumbers.Should().Equal(1, 2);
    }

    [Fact]
    public void A_row_without_destination_has_no_stop_number()
    {
        MissionRows.ToStops([Row("", "Titanium", "8", 4)], CrateCap.None).StopNumbers.Should().Equal([null]);
    }

    [Fact]
    public void Scu_accepts_surrounding_spaces()
    {
        MissionRows.ToStops([Row("Riker", "Titanium", " 8 ", 4)], CrateCap.None).Stops[0].TotalScu.Should().Be(8);
    }

    [Fact]
    public void Rules_match_MissionItem_for_every_amount_max_and_some_overrides()
    {
        // The SCU rule lives in MissionItem and, with a message for the status line, here (D19); the crate rule is
        // CrateMix.Problem for both (D23). This pins them together.
        int[][] overrides = [[], [4, 2], [2, 2, 2], [8], [3, 3], [1, 1, 1, 1, 1, 1], [16, 4], [32]];
        foreach (var max in Enum.GetValues<CrateSize>())
        {
            for (var scu = 1; scu <= 64; scu++)
            {
                foreach (var crates in overrides)
                {
                    var text = string.Join("+", crates);
                    var acceptedHere = MissionRows.ToStops([Row("Riker", "Titanium", $"{scu}", (int)max, text)], CrateCap.None).IsValid;
                    IReadOnlyList<CrateSize>? list = crates.Length == 0 ? null : [.. crates.Select(c => (CrateSize)c)];
                    var acceptedByCore = Record.Exception(() => new MissionItem("Titanium", scu, max, list)) is null;

                    acceptedHere.Should().Be(acceptedByCore, $"{scu} SCU, max {(int)max}, crates '{text}'");
                }
            }
        }
    }

    [Fact]
    public void MaxCrateSizes_lists_every_crate_size_in_ascending_order()
    {
        MissionRows.MaxCrateSizes.Should().Equal(1, 2, 4, 8, 16, 24, 32);
    }

    // --- Stops to rows ---

    [Fact]
    public void FromStops_gives_one_row_per_item_in_route_order()
    {
        var missions = TestPlans.SplitAndShared().Stops;

        var rows = MissionRows.FromStops(missions);

        rows.Should().HaveCount(missions.Sum(s => s.Items.Count));
        rows[0].Destination.Should().Be(missions[0].Destination);
        rows[0].Scu.Should().Be($"{missions[0].Items[0].Scu}");
        rows[0].MaxCrateSize.Should().Be((int)missions[0].Items[0].MaxCrateSize);
    }

    [Fact]
    public void FromStops_of_a_v1_file_gives_explicit_crates_marked_as_override()
    {
        // Riker 16 Titanium in 4 SCU crates (schema 1): exactly those crates, not a derivation (D23).
        var rows = MissionRows.FromStops(TestPlans.SplitAndShared().Stops);

        rows[0].Crates.Should().Be("4+4+4+4");
        rows[0].IsCrateOverride.Should().BeTrue();
    }

    [Fact]
    public void FromStops_of_a_derived_item_leaves_crates_empty()
    {
        MissionStop[] stops = [new("Green Glade", 1, [new MissionItem("Stims", 6, CrateSize.Four)])];

        var row = MissionRows.FromStops(stops).Should().ContainSingle().Which;

        row.Crates.Should().BeEmpty();
        row.IsCrateOverride.Should().BeFalse();
    }

    [Fact]
    public void FromStops_then_ToStops_gives_the_same_stops()
    {
        MissionStop[] derived = [new("Green Glade", 1, [new MissionItem("Stims", 6, CrateSize.Four)])];

        foreach (var missions in new[] { TestPlans.SplitAndShared().Stops, derived })
        {
            var stops = MissionRows.ToStops(MissionRows.FromStops(missions), CrateCap.None).Stops;

            stops.Should().BeEquivalentTo(missions, o => o.WithStrictOrdering());
        }
    }

    // --- Missions (T5.4) ---

    private static Mission Captured(int id, long contractId, params MissionStop[] stops) =>
        new(id, [.. stops.Select((s, i) => s with { Order = i + 1 })], new ContractSource([contractId], CrateSizeAssumed: true));

    private static MissionStop Stop(string destination, string commodity, int scu = 4) => new(destination, 1, [new MissionItem(commodity, scu, CrateSize.Four)]);

    [Fact]
    public void ToMissions_makes_the_typed_rows_of_one_destination_one_mission()
    {
        MissionRow[] rows = [Row("Riker", "Titanium", "8", 4), Row("Shubin", "Quartz", "4", 2), Row("riker", "Gold", "2", 1)];

        var (missions, route) = MissionRows.ToMissions(rows, []);

        route.Should().Equal("Riker", "Shubin");
        missions.Select(m => (m.Id, m.Stops.Single().Destination, m.Stops.Single().Items.Count)).Should().Equal((1, "Riker", 2), (2, "Shubin", 1));
        missions.Should().OnlyContain(m => m.Contract == null);
    }

    [Fact]
    public void ToMissions_keeps_a_capture_with_its_id_and_ids_and_numbers_typed_ones_above()
    {
        Mission[] known = [Captured(4, 36, Stop("Riker", "Stims"), Stop("Shubin", "Stims"))];
        var rows = MissionRows.FromMissions(known, ["Riker", "Shubin"]).Append(Row("Riker", "Gold", "2", 1)).ToArray();

        var (missions, _) = MissionRows.ToMissions(rows, known);

        missions.Select(m => (m.Id, MissionText.Source(m.Contract))).Should().Equal((4, "#36"), (5, ""));
        missions[0].Stops.Select(s => s.Destination).Should().Equal("Riker", "Shubin");
        missions[0].Contract!.CrateSizeAssumed.Should().BeTrue();
    }

    [Fact]
    public void FromMissions_gives_rows_in_route_order_with_mission_and_source()
    {
        Mission[] known = [Captured(1, 36, Stop("Shubin", "Stims")), new Mission(2, [Stop("Riker", "Titanium")])];

        var rows = MissionRows.FromMissions(known, ["Riker", "Shubin"]);

        rows.Select(r => (r.Destination, r.MissionId, r.Source, r.MaxCrateSizeAssumed)).Should().Equal(
            ("Riker", 2, "", false), ("Shubin", 1, "#36", true));
    }

    [Fact]
    public void Changing_the_max_crate_size_ends_the_assumption()
    {
        var row = MissionRows.FromMissions([Captured(1, 36, Stop("Shubin", "Stims"))], ["Shubin"]).Single();

        row.MaxCrateSize = 2;

        row.MaxCrateSizeAssumed.Should().BeFalse();
    }

    // --- Pickup (D68) ---

    [Fact]
    public void A_typed_pickup_becomes_the_items_list_of_places()
    {
        var row = Row("Everus Harbor", "Stims", "7", 4);
        row.Pickup = " Lorville / Area18 /lorville ";

        MissionRows.ToStops([row], CrateCap.None).Stops[0].Items[0].Pickup.Should().Equal("Lorville", "Area18");
    }

    [Fact]
    public void An_empty_pickup_is_none()
    {
        var row = Row("Riker", "Titanium", "4", 4);
        row.Pickup = " / ";

        MissionRows.ToStops([row], CrateCap.None).Stops[0].Items[0].Pickup.Should().BeNull();
    }

    [Fact]
    public void A_row_with_only_a_pickup_is_blank()
    {
        // A new row takes the pickup of the row above, as it takes the max crate size: that alone is no mission yet.
        MissionRow row = new() { Pickup = "Lorville" };

        row.IsBlank.Should().BeTrue();
    }

    [Fact]
    public void Two_rows_of_one_kind_from_two_pickups_are_two_items_of_one_mission()
    {
        var lorville = Row("Riker", "Titanium", "8", 8);
        lorville.Pickup = "Lorville";
        var everus = Row("Riker", "Titanium", "8", 8);
        everus.Pickup = "Everus Harbor";

        var (missions, _) = MissionRows.ToMissions([lorville, everus], []);

        missions.Single().Items.Select(i => i.Item.Pickup![0]).Should().Equal("Lorville", "Everus Harbor");
    }

    [Fact]
    public void FromStops_shows_the_pickup_joined_with_a_slash_and_a_capture_without_one_empty()
    {
        MissionStop everus = new("Everus Harbor", 1, [new MissionItem("Stims", 7, CrateSize.Four, Pickup: ["A", "B", "C"])]);

        MissionRows.FromStops([everus]).Single().Pickup.Should().Be("A / B / C");
        MissionRows.FromMissions([Captured(1, 36, Stop("Shubin", "Stims"))], ["Shubin"]).Single().Pickup.Should().BeEmpty();
    }

    [Fact]
    public void The_pickup_is_input()
    {
        MissionRow.IsInput(nameof(MissionRow.Pickup)).Should().BeTrue();
    }
}
