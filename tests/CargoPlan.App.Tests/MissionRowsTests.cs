using CargoPlan.App.Editor;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

public class MissionRowsTests
{
    private static MissionRow Row(string destination, string commodity, string scu, int crateSize) =>
        new() { Destination = destination, Commodity = commodity, Scu = scu, CrateSize = crateSize };

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

        var result = MissionRows.ToStops(rows);

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

        var shubin = MissionRows.ToStops(rows).Stops[0];

        shubin.Items.Should().Equal(new CargoItem("Quartz", 4, CrateSize.Two), new CargoItem("Gold", 2, CrateSize.One));
    }

    [Fact]
    public void Destinations_group_case_insensitive_and_trimmed_and_keep_the_first_spelling()
    {
        // "Riker" and " riker " are the same cargo lift, as in the mission file and the planner.
        MissionRow[] rows = [Row("Riker", "Titanium", "8", 4), Row(" riker ", " Gold ", "2", 1)];

        var stop = MissionRows.ToStops(rows).Stops.Should().ContainSingle().Which;

        stop.Destination.Should().Be("Riker");
        stop.Items[1].Commodity.Should().Be("Gold");
    }

    [Fact]
    public void Blank_rows_are_skipped_without_stop_number_or_error()
    {
        MissionRow[] rows = [Row("", " ", "", 4), Row("Riker", "Titanium", "8", 4), new MissionRow()];

        var result = MissionRows.ToStops(rows);

        result.StopNumbers.Should().Equal(null, 1, null);
        result.Errors.Should().AllSatisfy(e => e.Should().BeNull());
        result.Stops.Should().ContainSingle();
        result.Problem.Should().BeNull();
    }

    [Fact]
    public void Only_blank_rows_give_no_stops_and_ask_for_a_mission()
    {
        var result = MissionRows.ToStops([new MissionRow()]);

        result.Stops.Should().BeEmpty();
        result.IsValid.Should().BeFalse();
        result.Problem.Should().Be(MissionRows.NoMissionsText);
    }

    // --- Validation ---

    [Theory]
    [InlineData("", "Titanium", "8", 4, MissionRows.DestinationEmptyText)]
    [InlineData("Riker", " ", "8", 4, MissionRows.CommodityEmptyText)]
    [InlineData("Riker", "Titanium", "", 4, MissionRows.ScuInvalidText)]
    [InlineData("Riker", "Titanium", "0", 4, MissionRows.ScuInvalidText)]
    [InlineData("Riker", "Titanium", "-4", 4, MissionRows.ScuInvalidText)]
    [InlineData("Riker", "Titanium", "abc", 4, MissionRows.ScuInvalidText)]
    [InlineData("Riker", "Titanium", "4.5", 4, MissionRows.ScuInvalidText)]
    [InlineData("Riker", "Titanium", "8", 3, MissionRows.CrateSizeInvalidText)]
    [InlineData("Riker", "Titanium", "17", 4, "17 SCU can't be packed in 4 SCU crates.")]
    public void Each_rule_has_its_own_message(string destination, string commodity, string scu, int crateSize, string expected)
    {
        var result = MissionRows.ToStops([Row(destination, commodity, scu, crateSize)]);

        result.Errors.Should().Equal(expected);
        result.Stops.Should().BeEmpty();
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Problem_names_the_first_invalid_row_one_based()
    {
        MissionRow[] rows = [Row("Riker", "Titanium", "8", 4), Row("Shubin", "Quartz", "17", 4), Row("Orison", "", "8", 8)];

        MissionRows.ToStops(rows).Problem.Should().Be("Row 2: 17 SCU can't be packed in 4 SCU crates.");
    }

    [Fact]
    public void One_invalid_row_empties_the_stops_but_keeps_all_stop_numbers()
    {
        // The stop column still shows what the arrows do while a row is being fixed.
        MissionRow[] rows = [Row("Riker", "Titanium", "8", 4), Row("Shubin", "Quartz", "17", 4)];

        var result = MissionRows.ToStops(rows);

        result.Stops.Should().BeEmpty();
        result.StopNumbers.Should().Equal(1, 2);
    }

    [Fact]
    public void A_row_without_destination_has_no_stop_number()
    {
        MissionRows.ToStops([Row("", "Titanium", "8", 4)]).StopNumbers.Should().Equal([null]);
    }

    [Fact]
    public void Scu_accepts_surrounding_spaces()
    {
        MissionRows.ToStops([Row("Riker", "Titanium", " 8 ", 4)]).Stops[0].TotalScu.Should().Be(8);
    }

    [Fact]
    public void Scu_rule_matches_CargoItem_for_every_crate_size()
    {
        // The rule lives in CargoItem and, with a message for the status line, here (D19); this pins them together.
        foreach (var size in Enum.GetValues<CrateSize>())
        {
            for (var scu = 1; scu <= 64; scu++)
            {
                var acceptedHere = MissionRows.ToStops([Row("Riker", "Titanium", $"{scu}", (int)size)]).IsValid;
                var acceptedByCore = Record.Exception(() => new CargoItem("Titanium", scu, size)) is null;

                acceptedHere.Should().Be(acceptedByCore, $"{scu} SCU in {(int)size} SCU crates");
            }
        }
    }

    [Fact]
    public void CrateSizes_lists_every_crate_size_in_ascending_order()
    {
        MissionRows.CrateSizes.Should().Equal(1, 2, 4, 8, 16, 24, 32);
    }

    // --- Stops to rows ---

    [Fact]
    public void FromStops_gives_one_row_per_item_in_route_order()
    {
        var missions = TestPlans.SplitAndShared().Missions;

        var rows = MissionRows.FromStops(missions);

        rows.Should().HaveCount(missions.Sum(s => s.Items.Count));
        rows[0].Destination.Should().Be(missions[0].Destination);
        rows[0].Scu.Should().Be($"{missions[0].Items[0].Scu}");
        rows[0].CrateSize.Should().Be((int)missions[0].Items[0].CrateSize);
    }

    [Fact]
    public void FromStops_then_ToStops_gives_the_same_stops()
    {
        var missions = TestPlans.SplitAndShared().Missions;

        var stops = MissionRows.ToStops(MissionRows.FromStops(missions)).Stops;

        stops.Should().BeEquivalentTo(missions, o => o.WithStrictOrdering());
    }
}
