using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Ship files of this test project (TestShips/*.json), copied next to the test assembly like ships/ next to the exe
/// (D56): broken files for the loader tests and synthetic ships such as spike_uneven.
/// </summary>
internal static class TestShips
{
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "TestShips");

    public static Ship Load(string shipId) => ShipLoader.Load(Folder, shipId);

    public static string PathOf(string shipId) => Path.Combine(Folder, shipId + ".json");
}

/// <summary>
/// Shipped ships as they were before the game-file racks (FrozenShips/*.json, D92): mechanics tests run on fixed cells,
/// which a data import must not move. The data tests check ships/ itself. The App tests link the same files.
/// </summary>
internal static class FrozenShips
{
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "FrozenShips");

    public static Ship Load(string shipId) => ShipLoader.Load(Folder, shipId);

    public static string PathOf(string shipId) => Path.Combine(Folder, shipId + ".json");
}
