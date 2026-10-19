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
        json.Should().Contain("\"schemaVersion\": 2").And.Contain("\"overlay\"").And.Contain("\"left\": 1");
    }

    [Fact]
    public void Save_writes_the_whole_appearance()
    {
        SettingsStore.Save(SettingsPath, AppSettings.Default);

        // Every key in the file, so a player editing it by hand sees what there is (D31).
        File.ReadAllText(SettingsPath).Should()
            .Contain("\"appearance\"").And.Contain("\"background\": \"#A0141414\"")
            .And.Contain("\"fontSize\": null").And.Contain("\"fillAlpha\": 208").And.Contain("\"cornerRadius\": 3");
    }

    [Fact]
    public void Save_then_Load_keeps_the_appearance()
    {
        var settings = AppSettings.Default with
        {
            Appearance = new AppearanceSettings(
                new WindowAppearance("#E0300000", "#FF00FF00", 2),
                new HeaderAppearance("#FFFFFF00", new HaloSettings(true, "#FF000000", 4)),
                new LegendAppearance("#FF00FFFF", 16, new HaloSettings(true, "#80000000", 6)),
                new CrateAppearance(120, 64, "#FFFF0000", 40, HaloSettings.Off),
                new ButtonAppearance("#40FFFFFF", "#FFFFFFFF", 1, 6, new HaloSettings(true, "#FF101010", 2))),
        };

        SettingsStore.Save(SettingsPath, settings);
        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(settings);
        problem.Should().BeNull();
    }

    [Fact]
    public void Schema_1_file_gets_appearance_defaults_and_stays_untouched()
    {
        // Written by T3.4–T3.15: schema 1 is a subset of 2, so loading converts nothing and writes nothing (D31).
        const string content = """
            { "schemaVersion": 1, "overlay": { "left": 1, "top": 2, "width": 300, "height": 400 }, "hotkeys": { "toggleLock": "Ctrl+Alt+L" } }
            """;
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.SchemaVersion.Should().Be(2);
        loaded.Overlay.Should().Be(new WindowBounds(1, 2, 300, 400));
        loaded.Hotkeys.Should().Be(new HotkeySettings(ToggleLock: "Ctrl+Alt+L"));
        loaded.Appearance.Should().Be(AppearanceSettings.Default);
        File.ReadAllText(SettingsPath).Should().Be(content);
    }

    [Fact]
    public void Schema_1_file_is_saved_as_schema_2()
    {
        WriteFile("""{ "schemaVersion": 1, "hotkeys": { "toggleLock": "Ctrl+Alt+L" } }""");

        var (loaded, _) = SettingsStore.Load(SettingsPath);
        SettingsStore.Save(SettingsPath, loaded);

        File.ReadAllText(SettingsPath).Should().Contain("\"schemaVersion\": 2").And.Contain("\"appearance\"").And.Contain("\"toggleLock\": \"Ctrl+Alt+L\"");
    }

    [Fact]
    public void Partial_appearance_keeps_the_defaults_of_the_rest()
    {
        WriteFile("""{ "schemaVersion": 2, "appearance": { "legend": { "fontSize": 14 }, "crates": { "fillAlpha": 100 } } }""");

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.Appearance.Should().Be(AppearanceSettings.Default with
        {
            Legend = LegendAppearance.Default with { FontSize = 14 },
            Crates = CrateAppearance.Default with { FillAlpha = 100 },
        });
    }

    [Fact]
    public void Schema_2_file_without_rack_number_font_size_gets_the_default()
    {
        // Optional since T4.6, no schemaVersion bump (D31): a schema 2 file of T4.1 has no rackNumberFontSize.
        WriteFile("""{ "schemaVersion": 2, "appearance": { "crates": { "fillAlpha": 100 } } }""");

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.Appearance.Crates.Should().Be(CrateAppearance.Default with { FillAlpha = 100 });
        loaded.Appearance.Crates.RackNumberFontSize.Should().Be(27);
    }

    [Fact]
    public void Null_section_or_halo_means_its_defaults()
    {
        WriteFile("""{ "schemaVersion": 2, "appearance": { "window": null, "crates": { "halo": null } } }""");

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.Appearance.Should().Be(AppearanceSettings.Default);
    }

    [Fact]
    public void Value_out_of_range_is_clamped_with_a_problem_and_the_rest_applies()
    {
        const string content = """
            { "schemaVersion": 2, "overlay": { "left": 1, "top": 2, "width": 300, "height": 400 },
              "appearance": { "legend": { "fontSize": 0 }, "crates": { "fillAlpha": 300 }, "window": { "background": "#80FF0000" } } }
            """;
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Appearance.Legend.FontSize.Should().Be(AppearanceRules.MinFontSize);
        loaded.Appearance.Crates.FillAlpha.Should().Be(255);
        loaded.Appearance.Window.Background.Should().Be("#80FF0000");
        loaded.Overlay.Should().Be(new WindowBounds(1, 2, 300, 400));
        problem.Should().Contain("legend.fontSize").And.Contain("crates.fillAlpha");
        File.ReadAllText(SettingsPath).Should().Be(content);
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
    [InlineData("""{ "schemaVersion": 3, "overlay": null }""")]                 // newer app wrote it
    [InlineData("""{ "schemaVersion": 1, "overlay": null, "opacity": 0.5 }""")] // unknown property
    [InlineData("""{ "schemaVersion": 1, "overlay": { "left": 1, "top": 2 } }""")] // missing width/height
    [InlineData("""{ "schemaVersion": 1, "hotkeys": { "toggleLok": "Ctrl+Alt+L" } }""")] // typo in a hotkey name
    [InlineData("""{ "schemaVersion": 2, "appearance": { "legend": { "fontsize": 14 } } }""")]      // typo in a key (D31: strict)
    [InlineData("""{ "schemaVersion": 2, "appearance": { "legend": { "fontSize": "big" } } }""")]   // wrong JSON type
    [InlineData("""{ "schemaVersion": 2, "appearance": { "crates": { "fillAlpha": 3.5 } } }""")]    // not a whole number
    [InlineData("""{ "schemaVersion": 2, "appearance": { "window": { "background": null } } }""")]  // null for a colour
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
