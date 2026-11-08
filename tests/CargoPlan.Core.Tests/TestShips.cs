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
