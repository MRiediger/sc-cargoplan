using System.Text;
using System.Text.Json.Nodes;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Small hauling files in the shape of docs/hauling-data-format.md (A10: mechanics never read the pinned release). The
/// world: Everus Harbor, HUR-L1 and HUR-L3 in Stanton (1.3 and 25.7 Gm from Everus), Checkmate and Ruin Station in
/// Pyro, and a gateway named alike in both systems (K3). The App tests link this file.
/// </summary>
internal static class HaulingFixtures
{
    public const string Everus = "Everus Harbor";
    public const string HurL1 = "HUR-L1 Green Glade Station";
    public const string HurL3 = "HUR-L3 Thundering Express Station";
    public const string Checkmate = "Checkmate";
    public const string Ruin = "Ruin Station";
    public const string Gateway = "Pyro Gateway";

    public static JsonObject Position(string name, string system = "stanton", double x = 0, double y = 0, params string[] contractNames)
    {
        var position = new JsonObject
        {
            ["uuid"] = $"uuid-{system}-{name}",
            ["name"] = name,
            ["system"] = system,
            ["type"] = "Manmade",
            ["parent"] = null,
            ["x"] = x,
            ["y"] = y,
            ["z"] = 0,
        };
        if (contractNames.Length > 0)
        {
            position["contractNames"] = new JsonArray([.. contractNames.Select(n => (JsonNode)n)]);
        }

        return position;
    }

    public static JsonObject[] World() =>
    [
        Position(Everus, x: 12_850_400_000),
        Position(HurL1, x: 11_550_400_000),
        Position(HurL3, x: -12_849_600_000),
        Position(Gateway, x: 20_000_000_000),
        Position(Checkmate, "pyro", -41_255_000_000, 18_730_000_000, "Checkmate at the L4 Lagrange of Pyro II"),
        Position(Ruin, "pyro", -27_600_000_000, 9_900_000_000),
        Position(Gateway, "pyro", 30_000_000_000),
    ];

    public static JsonArray Legs(params (string Commodity, int Min, int Max, int Container)[] legs) =>
        [.. legs.Select(l => (JsonNode)new JsonObject
        {
            ["commodity"] = l.Commodity,
            ["minScu"] = l.Min,
            ["maxScu"] = l.Max,
            ["containerScu"] = l.Container,
        })];

    /// <summary>A Covalex stellar template Everus → HUR-L1, HUR-L3; change any field by name.</summary>
    public static JsonObject Template(string uuid, Action<JsonObject>? change = null)
    {
        var template = new JsonObject
        {
            ["uuid"] = uuid,
            ["debugName"] = $"HaulCargo_AToB_{uuid}",
            ["contractor"] = "covalex",
            ["title"] = "Experienced Rank - Direct Medium Cargo Haul",
            ["rank"] = "experienced",
            ["size"] = "medium",
            ["direct"] = true,
            ["shape"] = "aToB",
            ["stops"] = 1,
            ["system"] = "stanton",
            ["areaKey"] = "Stanton1",
            ["missionType"] = "stellar",
            ["faction"] = null,
            ["reward"] = 160250,
            ["legs"] = Legs(("Pressurized Ice", 90, 144, 16)),
            ["pickupPool"] = new JsonArray(Everus),
            ["dropoffPool"] = new JsonArray(HurL1, HurL3),
        };
        change?.Invoke(template);
        return template;
    }

    /// <summary>A Red Wind template Checkmate → Ruin Station, without a reward.</summary>
    public static JsonObject RedWind(string uuid) => Template(uuid, t =>
    {
        t["contractor"] = "redWind";
        t["title"] = "Senior Hauler Needed for Large Shipment";
        t["rank"] = "senior";
        t["size"] = "large";
        t["direct"] = false;
        t["system"] = "pyro";
        t["areaKey"] = "Solar";
        t["faction"] = "cfp";
        t["reward"] = null;
        t["legs"] = Legs(("Carbon", 400, 600, 32));
        t["pickupPool"] = new JsonArray(Checkmate);
        t["dropoffPool"] = new JsonArray(Ruin);
    });

    public static byte[] TemplatesFile(params JsonObject[] templates) => File(templates, "templates", t =>
        t["source"] = new JsonObject { ["repository"] = "StarCitizenWiki/scunpacked-data", ["commit"] = "f182b7f", ["gameBuild"] = "4.10.3", ["fetchedAt"] = "2026-11-23T04:10:00Z" });

    public static byte[] PositionsFile(params JsonObject[] positions) => File(positions, "positions", p =>
        p["source"] = new JsonObject { ["repository"] = "StarCitizenWiki/scunpacked-data", ["file"] = "starmap_positions.json", ["commit"] = "f182b7f", ["fetchedAt"] = "2026-11-23T04:22:00Z" });

    /// <summary>Default files: two templates (Covalex, Red Wind) in the world above.</summary>
    public static (byte[] Templates, byte[] Positions) Files() => (TemplatesFile(Template("a"), RedWind("b")), PositionsFile(World()));

    private static byte[] File(JsonObject[] entries, string field, Action<JsonObject> source, int schemaVersion = 1)
    {
        var file = new JsonObject { ["schemaVersion"] = schemaVersion, ["dataVersion"] = "ships-2026.11.2" };
        source(file);
        file[field] = new JsonArray([.. entries.Select(e => (JsonNode)e.DeepClone())]);
        return Encoding.UTF8.GetBytes(file.ToJsonString());
    }
}
