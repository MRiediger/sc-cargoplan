using System.Text.Json.Nodes;
using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

/// <summary>
/// settings.json 2 → 3 (T10.4a, F1, F2): the ContractTracker keys and the framing from before the perspective view go,
/// nothing else. Each old form loads without a problem into what the code before the migration read from it, minus the
/// removed keys; the expected files were written by that code (Fixtures/Settings/Expected).
/// </summary>
public sealed class SettingsMigrationTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("cargoplan-settings-migration-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static string Resource(string name)
    {
        using var stream = typeof(SettingsMigrationTests).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"No resource {name}.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private string Write(string content, string name = "settings.json")
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, content);
        return path;
    }

    private JsonNode? SavedAgain(AppSettings settings)
    {
        var path = Path.Combine(_folder, "saved.json");
        SettingsStore.Save(path, settings);
        return JsonNode.Parse(File.ReadAllText(path));
    }

    // v0.1.0 to v0.3.1 documented the same file (git show <tag>:docs/settings-format.md); "full" sets every key v0.3.1
    // knew, "t10.3_scan" is main's form with scanContracts, between v0.3.1 and the migration.
    [Theory]
    [InlineData("v0.1-v0.3.1_defaults")]
    [InlineData("v0.3.1_full")]
    [InlineData("t10.3_scan")]
    public void An_old_form_loads_without_a_problem_into_what_was_read_before(string name)
    {
        var loaded = SettingsStore.Load(Write(Resource($"settings/{name}.json")));

        loaded.Problem.Should().BeNull();
        JsonNode.DeepEquals(SavedAgain(loaded.Settings), JsonNode.Parse(Resource($"settings/expected/{name}.json"))).Should().BeTrue(
            $"the migrated {name} saves as Expected/{name}.json:{Environment.NewLine}{SavedAgain(loaded.Settings)}");
    }

    [Fact]
    public void A_set_sync_hotkey_is_named_once_it_is_removed()
    {
        var loaded = SettingsStore.Load(Write(Resource("settings/v0.3.1_full.json")));

        loaded.Removed.Should().Be(SettingsStore.SyncHotkeyRemovedText("Ctrl+Alt+O"));
        SettingsStore.SyncHotkeyRemovedText("Ctrl+Alt+O").Should().Be(
            "settings.json: hotkey Ctrl+Alt+O for the ContractTracker sync was removed; contracts come in with the scan hotkey.");
    }

    [Theory]
    [InlineData("v0.1-v0.3.1_defaults")]
    [InlineData("t10.3_scan")]
    public void Without_a_set_sync_hotkey_nothing_is_named(string name)
    {
        SettingsStore.Load(Write(Resource($"settings/{name}.json"))).Removed.Should().BeNull();
    }

    [Fact]
    public void A_schema_1_file_migrates_too()
    {
        var loaded = SettingsStore.Load(Write("""
            { "schemaVersion": 1, "hotkeys": { "toggleLock": "Ctrl+Alt+L", "syncContractTracker": "Ctrl+Alt+S" },
              "contractTracker": { "autoSync": false } }
            """));

        loaded.Problem.Should().BeNull();
        loaded.Settings.Should().Be(AppSettings.Default with { Hotkeys = new HotkeySettings(ToggleLock: "Ctrl+Alt+L") });
        loaded.Removed.Should().Be(SettingsStore.SyncHotkeyRemovedText("Ctrl+Alt+S"));
    }

    [Fact]
    public void Only_a_camera_framed_before_the_perspective_view_goes()
    {
        // Any of the three keys set: the code before read the whole entry as nothing (D83). All three null: only the keys go.
        var loaded = SettingsStore.Load(Write("""
            { "schemaVersion": 2, "shipView": { "cameras": {
              "hull_c": { "yaw": 70, "pitch": 35.26, "panY": -2.25 },
              "raft": { "yaw": 78, "pitch": 62.5, "pxPerCell": null, "panX": null, "panY": null },
              "hull_b": { "yaw": 26.5, "pitch": 8.25, "distance": 50.5, "target": { "x": 1, "y": 0, "z": 0 } } } } }
            """));

        loaded.Problem.Should().BeNull();
        loaded.Settings.ShipView.Cameras.Should().BeEquivalentTo(new Dictionary<string, ShipCamera>
        {
            ["raft"] = new(78, 62.5),
            ["hull_b"] = new(26.5, 8.25, Distance: 50.5, Target: new CameraTarget(1, 0, 0)),
        });
    }

    [Theory]
    // Disallow lives (F1): the migration takes only what it knows; a typo still costs the file, as before.
    [InlineData("""{ "schemaVersion": 2, "contractTracker": { "autoSync": false }, "hotkey": { } }""")]
    [InlineData("""{ "schemaVersion": 2, "hotkeys": { "syncContractTracker": null, "toggleLok": "Ctrl+Alt+L" } }""")]
    [InlineData("""{ "schemaVersion": 2, "shipView": { "cameras": { "raft": { "yaw": 1, "pitch": 2, "zoom": 3 } } } }""")]
    // Schema 3 has no ContractTracker: a key of it there is a key nobody knows.
    [InlineData("""{ "schemaVersion": 3, "contractTracker": { "autoSync": false } }""")]
    [InlineData("""{ "schemaVersion": 3, "hotkeys": { "syncContractTracker": "Ctrl+Alt+S" } }""")]
    [InlineData("""{ "schemaVersion": 3, "shipView": { "cameras": { "raft": { "yaw": 1, "pitch": 2, "pxPerCell": 90 } } } }""")]
    public void An_unknown_key_still_costs_the_file(string content)
    {
        var loaded = SettingsStore.Load(Write(content));

        loaded.Settings.Should().Be(AppSettings.Default);
        loaded.Problem.Should().StartWith("Settings not loaded, using defaults");
        loaded.Removed.Should().BeNull();
    }

    [Fact]
    public void Schema_3_saves_and_loads_unchanged()
    {
        var settings = AppSettings.Default with
        {
            Hotkeys = new HotkeySettings(ToggleLock: "Ctrl+Alt+L", ScanContracts: null),
            Ship = "raft",
            ShipView = new ShipViewSettings(Cameras: new Dictionary<string, ShipCamera> { ["raft"] = new(10, 20) }),
        };
        var path = Path.Combine(_folder, "settings.json");

        SettingsStore.Save(path, settings);
        var loaded = SettingsStore.Load(path);

        File.ReadAllText(path).Should().Contain("\"schemaVersion\": 3").And.NotContain("contractTracker").And.NotContain("pxPerCell");
        loaded.Problem.Should().BeNull();
        loaded.Removed.Should().BeNull();
        loaded.Settings.Should().BeEquivalentTo(settings);
    }

    [Theory]
    // Edited by hand: a key twice. The serializer before the migration let the last one win; the tree refuses it, and it
    // costs the file like a typo, never the start (D105).
    [InlineData("""{ "schemaVersion": 3, "ship": "hull_a", "ship": "raft" }""")]
    [InlineData("""{ "schemaVersion": 2, "hotkeys": { "toggleLock": "Ctrl+Alt+K", "toggleLock": "Ctrl+Alt+L" } }""")]
    public void A_key_twice_costs_the_file(string content)
    {
        var loaded = SettingsStore.Load(Write(content));

        loaded.Settings.Should().Be(AppSettings.Default);
        loaded.Problem.Should().StartWith("Settings not loaded, using defaults (A key appears twice:");
    }

    [Fact]
    public void Loading_writes_nothing()
    {
        // D31: the file stays as the player left it; writing a migrated file back is the session's (T10.4a).
        var content = Resource("settings/v0.3.1_full.json");
        var path = Write(content);

        SettingsStore.Load(path);

        File.ReadAllText(path).Should().Be(content);
    }
}
