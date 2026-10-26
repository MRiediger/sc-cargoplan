using CargoPlan.App.Editor;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

public class MissionTextTests
{
    private static MissionStop Stop(string destination, int order) => new(destination, order, [new MissionItem("Stims", 4, CrateSize.Four)]);

    [Theory]
    [InlineData(new long[] { 36 }, "#36")]
    [InlineData(new long[] { 38, 37 }, "#37–38")]
    [InlineData(new long[] { 36, 40 }, "#36, 40")]
    [InlineData(new long[] { 47, 48, 49 }, "#47–49")]
    [InlineData(new long[] { 49, 47, 48 }, "#47–49")]
    [InlineData(new long[] { 36, 37, 40 }, "#36–37, 40")]
    public void Source_names_the_ids_as_ContractTracker_shows_them(long[] ids, string expected)
    {
        MissionText.Source(new ContractSource(ids)).Should().Be(expected);
    }

    [Fact]
    public void Source_of_a_typed_mission_is_empty()
    {
        MissionText.Source(null).Should().BeEmpty();
    }

    [Fact]
    public void Name_is_the_first_stop_and_how_many_follow()
    {
        MissionText.Name(new Mission(1, [Stop("Area18", 2), Stop("Hurston", 1), Stop("Orison", 3)])).Should().Be("Hurston + 2");
        MissionText.Name(new Mission(1, [Stop("Riker", 1)])).Should().Be("Riker");
    }
}
