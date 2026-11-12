using CargoPlan.App.Editor;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

public class MissionTextTests
{
    private static MissionStop Stop(string destination, int order) => new(destination, order, [new MissionItem("Stims", 4, CrateSize.Four)]);

    [Fact]
    public void Source_of_a_scanned_contract_is_its_scan_number()
    {
        // D102: "#3" is CargoPlan's own count, as "Scanned #3" will say (T10.3).
        MissionText.Source(new ContractSource(new ScanIdentity(3, "EVERUSHARBOR | RIKER:STIMS | 4 | 50250", false))).Should().Be("#3");
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
