using CargoPlan.Import.Contracts;

namespace CargoPlan.Import.Tests;

/// <summary>
/// The content key (D101) of every raw text fixture, recorded on main before T13.1 taught the parser the title. A parser
/// change that moves a key breaks every plan that holds the contract, so a new fixture gets its line here too.
/// </summary>
public class ContentKeyGoldenTests
{
    private static readonly Dictionary<string, string> Golden = new(StringComparer.Ordinal)
    {
        ["carbon_or_cut"] = "RAT'SNEST, STARIIGHTSERVICE | RUIN:CARBON | 8 | ?",
        ["fuel_accepted"] = "EVERUSHARBOR | FAITHFUIDREAM:HYDROGENFUEI, FAITHFUIDREAM:QUANTUMFUEI, GREENGIADE:HYDROGENFUEI, GREENGIADE:SHIPAMMUNITION | 16 | 191500",
        ["fuel_offer"] = "EVERUSHARBOR | FAITHFUIDREAM:HYDROGENFUEI, FAITHFUIDREAM:QUANTUMFUEI, GREENGIADE:HYDROGENFUEI, GREENGIADE:SHIPAMMUNITION | 16 | 191500",
        ["hurston_any_order"] = "EVERUSHARBOR | FAITHFUIDREAM:PROCESSEDFOOD, MEIODICFIEIDS:PRESSURIZEDICE, MEIODICFIEIDS:PROCESSEDFOOD, THUNDERINGEXPRESS:PRESSURIZEDICE | ? | ?",
        ["ice_and_food"] = "EVERUSHARBOR | THUNDERINGEXPRESS:PRESSURIZEDICE, THUNDERINGEXPRESS:PROCESSEDFOOD | ? | ?",
        ["iron_couldnt_handle"] = "EVERUSHARBOR | FAITHFUIDREAM:IRON(ORE) | 8 | 50500",
        ["potassium_or_cut"] = "DUDIEY&DAUGHTERS, GASIIGHT, STARIIGHTSERVICE | RUIN:POTASSIUM | 8 | ?",
        ["silicon_at_most"] = "HUR-I2FAITHFUIDREAM | EVERUSHARBOR:SIIICON | 8 | 50500",
        ["silicon_max_size"] = "HUR-I2FAITHFUIDREAM | MEIODICFIEIDS:SIIICON | 1 | ?",
        ["silicon_overlay_text"] = "HUR-IIGREENGIADE | HIGHCOURSE:SIIICON | 1 | ?",
        ["silicon_typing_in"] = " |  | 1 | 50250",
        ["stims_accepted_tab"] = "EVERUSH, EVERUSHARBOR | GREENGIADE:STIMS, HIGHCOURSE:STIMS, THUNDERINGEXPRESS:STIMS | ? | ?",
        ["stims_ocr_errors"] = "EVERUSHARBOR | FAITHFUIDREAM:STIMS, HIGHCOURSE:STIMS, THUNDERINGEXPRESS:STIMS | ? | ?",
        ["stims_split"] = "EVERUSHARBOR | FAITHFUIDREAM:STIMS, HIGHCOURSE:STIMS, THUNDERINGEXPRESS:STIMS | ? | ?",
        ["waste_bigger_than"] = "HUR-I2FAITHFUIDREAM, HUR-I3THUNDERINGEXPRESS, HUR-I5HIGHCOURSE, HUR-IIGREENGIADE | EVERUSHARBOR:WASTE | 4 | ?",
        ["waste_collect_wrapped"] = "HUR-I2FAITHFUIDREAM, HUR-I3THUNDERINGEXPRESS, HUR-I4MEIODICFIEIDS, HUR-IIGREENGIADE | EVERUSHARBOR:WASTE | 4 | 72500",
        ["waste_list_fragments"] = "HUR-I2FAITHFUIDREAM, HUR-I3THUNDERINGEXPRESS, HUR-I5HIGHCOURSE, HUR-IIGREENGIADE | EVERUSHARBOR:WASTE | 4 | ?",
        ["waste_ls"] = "HUR-I2FAITHFUIDREAM, HUR-I3THUNDERINGEXPRESS, HUR-I5HIGHCOURSE, HUR-IIGREENGIADE | EVERUSHARBOR:WASTE | 4 | ?",
        ["waste_or_smaller"] = "RUIN | ENDGAME:WASTE, ORBITUARY:WASTE | 8 | ?",
        ["waste_torn_abov"] = "HUR-I3THUNDG, HUR-I4MEIODICFIEI, HUR-I5HIGHCOURSEST, HUR-IIGREENGIE | EVERUSHARBOR:WASTE | ? | 72500",
        ["waste_torn_aboy"] = "HUR-I4MEIODICFIEIDS, HUR-IIGREENGIAD | EVERUSHARBOR:WASTE | 1 | 50250",
    };

    public static TheoryData<string> AllRawTextFixtures() => ContractTextParserTests.AllRawTextFixtures();

    [Theory]
    [MemberData(nameof(AllRawTextFixtures))]
    public void Key_of_every_fixture_is_the_one_recorded_before_the_title(string fixture)
    {
        Golden.Should().ContainKey(fixture, "a new fixture gets its key recorded here");

        ScannedContract.Read(ImportFixtures.RawText(fixture)).Key.Should().Be(Golden[fixture]);
    }

    [Fact]
    public void Every_recorded_key_belongs_to_a_fixture()
    {
        Golden.Keys.Should().BeEquivalentTo(AllRawTextFixtures().Cast<object[]>().Select(row => (string)row[0]));
    }
}
