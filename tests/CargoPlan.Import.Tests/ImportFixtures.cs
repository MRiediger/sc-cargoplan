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

    public static string RawText(string name) => ReadResource($"rawtext/{name}.txt");

    public static string Schema => ReadResource("ct_schema.sql");

    private static string ReadResource(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Fixture '{resourceName}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
