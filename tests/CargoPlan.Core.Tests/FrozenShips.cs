using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Shipped ships frozen as test fixtures (FrozenShips/*.json, D92): mechanics tests run on fixed cells, which a data
/// import must not move. Most are as they were before the game-file racks; clipper, perseus and apollo_medivac were
/// frozen in session 4b. The data tests check ships/ itself. The App and Import tests link this file and the folder.
/// </summary>
internal static class FrozenShips
{
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "FrozenShips");

    public static Ship Load(string shipId) => ShipLoader.Load(Folder, shipId);

    public static ShipCatalog LoadAll() => ShipLoader.LoadAll(Folder);

    public static string PathOf(string shipId) => Path.Combine(Folder, shipId + ".json");
}

/// <summary>
/// The outlines of the frozen ships (FrozenOutlines/ship-outlines.json): pixel and placement tests keep their hull when
/// outlines/ is rebuilt (session 4b). Tests of the shipped outlines read outlines/ itself.
/// </summary>
internal static class FrozenOutlines
{
    public static string PathOfFile => Path.Combine(AppContext.BaseDirectory, "FrozenOutlines", ShipOutlineLoader.FileName);

    public static ShipOutlineCatalog Load() => ShipOutlineLoader.Load(PathOfFile);
}
