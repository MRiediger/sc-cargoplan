using System.Reflection;

namespace CargoPlan.Import.Tests;

/// <summary>
/// Raw OCR texts of real ContractTracker captures (Fixtures/RawText, D38 addendum) and the fingerprint schema,
/// embedded in this test assembly. Contract ids in the comments are the ones in Martin's database.
/// </summary>
internal static class ImportFixtures
{
    /// <summary>Ids 28/29: Pressurized Ice 7 + Processed Food 9, both to a truncated "Thundering (Express)"; no size.</summary>
    public const string IceAndFood = "ice_and_food";

    /// <summary>Id 30: Silicon 10 to Melodic Fields Station, "Max size will be 1 SCU."</summary>
    public const string SiliconMaxSize = "silicon_max_size";

    /// <summary>Id 31: Waste 9 to Everus Harbor, "none of the containers are bigger than 4 SCU", pick-up list.</summary>
    public const string WasteBiggerThan = "waste_bigger_than";

    /// <summary>Ids 32/33 (CT's 2 + 1 split): Stims 4/5/3, drop-off list of 3, list fragments before DETAILS.</summary>
    public const string StimsSplit = "stims_split";

    /// <summary>Id 34: Waste, "• Stims (3 SCU)" from the contract list before DETAILS.</summary>
    public const string WasteListFragments = "waste_list_fragments";

    /// <summary>Id 35: Waste, "· Waste (9 SCU)" and "· Silicon (10 SCU)" before DETAILS, "LS" for "L5".</summary>
    public const string WasteLs = "waste_ls";

    /// <summary>Id 36 (CT found 1 destination): "Dellver", "O0/5", "O/3", "LS"; drop-off list of 3.</summary>
    public const string StimsOcrErrors = "stims_ocr_errors";

    /// <summary>
    /// Ids 37/38, from the Accepted tab: "Contract Deadline", ends in TRACK/ABANDON instead of ACCEPT OFFER; "to Thunde"
    /// breaks mid-word, "to High Course" is cut off; Stims 3/4/4, drop-off list of 3.
    /// </summary>
    public const string StimsAcceptedTab = "stims_accepted_tab";

    /// <summary>
    /// Id 47: Waste 9 to "Everus Harbor aboy" (line ends in a torn "above"), "bigger than 1 SCU", reward 50,250 at the
    /// start; prose "taken to a freight elevator at Everus Harbor above Hurston", pick-up list.
    /// </summary>
    public const string WasteTornAboy = "waste_torn_aboy";

    /// <summary>
    /// Id 48, another contract to the same place: Waste 9 to "Everus Harbor abov", size phrase lost in the OCR,
    /// reward 72,500; pick-up list of 4 interleaved with "Collect" lines.
    /// </summary>
    public const string WasteTornAbov = "waste_torn_abov";

    /// <summary>
    /// Silicon 7 to "High Course Station" (line cut, no completion in the text), "Max size willb 1 SCU", no reward line;
    /// CargoPlan's own overlay ("MISC Hull A", "No plan. Open t", "Open editor") read into the capture.
    /// </summary>
    public const string SiliconOverlayText = "silicon_overlay_text";

    /// <summary>
    /// Ids 9–12 (B1, 24.10.2026): Ice 6 to "Melodic Fieids" (l read as i), Food 5 to "Faithful Dream", Ice 3 to
    /// "Thundering Express", Food 5 to "Melodic Fields", every line cut before "Station"; drop-off list of 3, one entry
    /// read as "Freight alevator at Thundering Express Station".
    /// </summary>
    public const string HurstonAnyOrder = "hurston_any_order";

    /// <summary>
    /// Martin's plan.json after the route of ids 9–12 (B1), every crate ticked: stops "Melodic Fieids", "Faithful Dream
    /// Station", "Thundering Express", "Melodic Fields Station" (mission 1) and Everus Harbor (17, 18); watermark 18.
    /// </summary>
    public const string HurstonPlan = "hurston_2026-10-23";

    public static string RawText(string name) => ReadResource($"rawtext/{name}.txt");

    public static string Plan(string name) => ReadResource($"plans/{name}.json");

    public static string Schema => ReadResource("ct_schema.sql");

    private static string ReadResource(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Fixture '{resourceName}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
