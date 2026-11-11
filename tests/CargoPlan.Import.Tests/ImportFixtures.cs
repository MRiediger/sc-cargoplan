using System.Reflection;

namespace CargoPlan.Import.Tests;

/// <summary>
/// Raw OCR texts of real ContractTracker captures (Fixtures/RawText, D38 addendum) and Martin's plan of 23 Oct, embedded
/// in this test assembly. Contract ids in the comments are the ones in Martin's database.
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
    /// Issue #22 (Reikal, Pyro, 6.11.2026), contracts 1/2: Waste 124 to Endgame, Waste 82 to Orbituary, "bunch of 8 SCU or
    /// smaller waste containers"; ends in UNTRACK/ABANDON, lines of the prose interleaved with the objectives.
    /// </summary>
    public const string WasteOrSmaller = "waste_or_smaller";

    /// <summary>
    /// Issue #22, contract 3: Potassium 215 to Ruin Station from three pickups, "They got cargo (8 SCU or" and the line
    /// with "smaller)" lost in the OCR; another contract's header read into the capture.
    /// </summary>
    public const string PotassiumOrCut = "potassium_or_cut";

    /// <summary>Issue #22, contract 4: Carbon 200 to Ruin Station, "(8 SCU or" as in contract 3, "smaller)" lost.</summary>
    public const string CarbonOrCut = "carbon_or_cut";

    // CargoPlan's own OCR (T10.2a): excerpts of scan-probe.log (11 Nov 2026, 3440×1440 over the game), shortened by hand:
    // the prose keeps only the sentence a test needs. Reading order in columns (D99): the reward line after the prose.

    /// <summary>
    /// Offers view, reward 191,500: Hydrogen Fuel 70/62, Quantum Fuel 81, Ship Ammunition 133 to Green Glade and Faithful
    /// Dream (two lines break before "Station"), "16 SCU or smaller", a Collect line from Everus Harbor after each delivery,
    /// "O/" and "0/" mixed.
    /// </summary>
    public const string FuelOffer = "fuel_offer";

    /// <summary>
    /// The same contract after accepting: other SCU (55/136/115/64), ABANDON first, "Contract Deadline", the objective
    /// marker "L" on its own line, ends in SHARE UNTRACK. Same content key as <see cref="FuelOffer"/>.
    /// </summary>
    public const string FuelAccepted = "fuel_accepted";

    /// <summary>Accepted view: Silicon 92 to Everus Harbor, "At most the\ncontainers will be 8 SCU in size.", SHARE TRACK.</summary>
    public const string SiliconAtMost = "silicon_at_most";

    /// <summary>Offers view: Iron (Ore) 98 to Faithful Dream Station, "couldn't handle 8\nSCU containers".</summary>
    public const string IronCouldntHandle = "iron_couldnt_handle";

    /// <summary>
    /// Offers view: Waste 10 to Everus Harbor, "bigger than 4 SCU", four Collect lines after the one delivery, the third
    /// broken inside its place ("HUR-L3 Thundering Express" / "Station.").
    /// </summary>
    public const string WasteCollectWrapped = "waste_collect_wrapped";

    /// <summary>
    /// S10.0 set-1080s c06 through today's OCR (T10.2b2), captured while the game was still typing the objectives in:
    /// "Deliver 0/9 SCU of Silicon" ended up in a prose line, the objectives hold only "Collect Silicon from".
    /// </summary>
    public const string SiliconTypingIn = "silicon_typing_in";

    /// <summary>
    /// Martin's plan.json after the route of ids 9–12 (B1), every crate ticked: stops "Melodic Fieids", "Faithful Dream
    /// Station", "Thundering Express", "Melodic Fields Station" (mission 1) and Everus Harbor (17, 18); watermark 18.
    /// </summary>
    public const string HurstonPlan = "hurston_2026-10-23";

    public static string RawText(string name) => ReadResource($"rawtext/{name}.txt");

    public static string Plan(string name) => ReadResource($"plans/{name}.json");

    private static string ReadResource(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException($"Fixture '{resourceName}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
