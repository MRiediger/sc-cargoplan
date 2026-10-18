using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

/// <summary>Works on a fresh temp folder per test; xUnit creates a new instance for every test.</summary>
public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private void WriteFile(string content)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, content);
    }

    [Fact]
    public void Save_then_Load_returns_the_same_settings()
    {
        var settings = AppSettings.Default with { Overlay = new WindowBounds(100.5, -20, 520, 200) };

        SettingsStore.Save(SettingsPath, settings);
        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(settings);
        problem.Should().BeNull();
    }

    [Fact]
    public void Save_then_Load_keeps_the_hotkeys()
    {
        var settings = AppSettings.Default with
        {
            Overlay = new WindowBounds(1, 2, 300, 400),
            Hotkeys = new HotkeySettings(ToggleLock: "Ctrl+Alt+L", ToggleOverlay: "Ctrl+Alt+H"),
        };

        SettingsStore.Save(SettingsPath, settings);
        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(settings);
        problem.Should().BeNull();
        File.ReadAllText(SettingsPath).Should().Contain("\"hotkeys\"").And.Contain("\"toggleLock\": \"Ctrl+Alt+L\"");
    }

    [Fact]
    public void File_without_hotkeys_gives_no_hotkeys()
    {
        // Written by T3.4–T3.6: still readable, no schemaVersion bump for an optional field (D13).
        WriteFile("""{ "schemaVersion": 1, "overlay": { "left": 1, "top": 2, "width": 300, "height": 400 } }""");

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Hotkeys.Should().BeNull();
        problem.Should().BeNull();
    }

    [Fact]
    public void Hotkeys_with_one_action_leave_the_other_unset()
    {
        WriteFile("""{ "schemaVersion": 1, "hotkeys": { "toggleOverlay": "Ctrl+Alt+H" } }""");

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Hotkeys.Should().Be(new HotkeySettings(ToggleLock: null, ToggleOverlay: "Ctrl+Alt+H"));
        problem.Should().BeNull();
    }

    [Fact]
    public void Save_creates_the_missing_folder()
    {
        SettingsStore.Save(SettingsPath, AppSettings.Default);

        File.Exists(SettingsPath).Should().BeTrue();
    }

    [Fact]
    public void Save_leaves_no_temp_file_behind()
    {
        SettingsStore.Save(SettingsPath, AppSettings.Default);
        SettingsStore.Save(SettingsPath, AppSettings.Default with { Overlay = new WindowBounds(1, 2, 300, 400) });

        Directory.GetFiles(_folder).Should().ContainSingle().Which.Should().Be(SettingsPath);
    }

    [Fact]
    public void Save_writes_camel_case_with_schema_version()
    {
        SettingsStore.Save(SettingsPath, AppSettings.Default with { Overlay = new WindowBounds(1, 2, 300, 400) });

        var json = File.ReadAllText(SettingsPath);
        json.Should().Contain("\"schemaVersion\": 1").And.Contain("\"overlay\"").And.Contain("\"left\": 1");
    }

    [Fact]
    public void Missing_file_gives_defaults_without_a_problem()
    {
        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(AppSettings.Default);
        problem.Should().BeNull();
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("null")]
    [InlineData("""{ "overlay": null }""")]                                     // no schemaVersion
    [InlineData("""{ "schemaVersion": 2, "overlay": null }""")]                 // newer app wrote it
    [InlineData("""{ "schemaVersion": 1, "overlay": null, "opacity": 0.5 }""")] // unknown property
    [InlineData("""{ "schemaVersion": 1, "overlay": { "left": 1, "top": 2 } }""")] // missing width/height
    [InlineData("""{ "schemaVersion": 1, "hotkeys": { "toggleLok": "Ctrl+Alt+L" } }""")] // typo in a hotkey name
    public void Broken_file_gives_defaults_with_a_problem_and_stays_untouched(string content)
    {
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(AppSettings.Default);
        problem.Should().NotBeNullOrWhiteSpace();
        File.ReadAllText(SettingsPath).Should().Be(content);
    }

    [Fact]
    public void File_without_overlay_gives_no_bounds()
    {
        WriteFile("""{ "schemaVersion": 1 }""");

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Overlay.Should().BeNull();
        problem.Should().BeNull();
    }

    [Fact]
    public void DefaultPath_is_in_local_app_data()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        SettingsStore.DefaultPath.Should().Be(Path.Combine(localAppData, "SC-CargoPlan", "settings.json"));
    }
}
