using CargoPlan.App.Settings;
using CargoPlan.Import.ContractTracker;

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
            .And.Contain("\"fontSize\": null").And.Contain("\"fillAlpha\": 208").And.Contain("\"cornerRadius\": 3")
            .And.Contain("\"visible\": true").And.Contain("\"position\": \"bottom-left\"");
    }

    [Fact]
    public void Save_then_Load_keeps_the_legend()
    {
        var settings = AppSettings.Default with { Legend = new LegendSettings(Visible: false, Position: "top-right") };

        SettingsStore.Save(SettingsPath, settings);
        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(settings);
        problem.Should().BeNull();
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1 }""")]
    [InlineData("""{ "schemaVersion": 2, "appearance": { "crates": { "fillAlpha": 100 } } }""")]
    [InlineData("""{ "schemaVersion": 2, "legend": null }""")]
    public void File_without_legend_gets_the_old_layout(string content)
    {
        // Optional since T4.3, no schemaVersion bump (D31): bottom left, stop lines shown, as before.
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.Legend.Should().Be(LegendSettings.Default);
    }

    [Fact]
    public void Legend_with_visible_only_keeps_the_default_position()
    {
        WriteFile("""{ "schemaVersion": 2, "legend": { "visible": false } }""");

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.Legend.Should().Be(new LegendSettings(Visible: false, Position: "bottom-left"));
    }

    [Fact]
    public void Invalid_legend_position_takes_the_default_with_a_problem_and_the_rest_applies()
    {
        // The notice must show with the stop lines hidden: it stands where the legend would be (T4.3).
        const string content = """
            { "schemaVersion": 2, "overlay": { "left": 1, "top": 2, "width": 300, "height": 400 },
              "legend": { "visible": false, "position": "up" }, "appearance": { "crates": { "fillAlpha": 300 } } }
            """;
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Legend.Should().Be(new LegendSettings(Visible: false, Position: "bottom-left"));
        loaded.Overlay.Should().Be(new WindowBounds(1, 2, 300, 400));
        problem.Should().Contain("settings.json: legend.position \"up\"").And.Contain("crates.fillAlpha");
        File.ReadAllText(SettingsPath).Should().Be(content);
    }

    // ContractTracker (T5.5, D44)

    [Fact]
    public void Save_then_Load_keeps_the_ContractTracker_settings_and_hotkey()
    {
        var settings = AppSettings.Default with
        {
            ContractTracker = new ContractTrackerSettings(@"D:\Games\CT\contracts.db", AutoSync: false),
            Hotkeys = new HotkeySettings(SyncContractTracker: "Ctrl+Alt+S", Replan: "Ctrl+Alt+R"),
        };

        SettingsStore.Save(SettingsPath, settings);
        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(settings);
        problem.Should().BeNull();
        File.ReadAllText(SettingsPath).Should().Contain("\"contractTracker\"").And.Contain("\"autoSync\": false")
            .And.Contain("\"syncContractTracker\": \"Ctrl+Alt+S\"").And.Contain("\"replan\": \"Ctrl+Alt+R\"");
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2 }""")]
    [InlineData("""{ "schemaVersion": 2, "contractTracker": null }""")]
    [InlineData("""{ "schemaVersion": 2, "contractTracker": { } }""")]
    public void File_without_ContractTracker_settings_detects_it_and_syncs(string content)
    {
        // Optional, no schemaVersion bump (D31, D44): what T5.4 did without a setting.
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.ContractTracker.Should().Be(new ContractTrackerSettings(null, AutoSync: true));
        loaded.ContractTracker.DatabasePath.Should().Be(ContractTrackerReader.DefaultPath);
    }

    [Theory]
    [InlineData("contracts.db")]
    [InlineData("")]
    [InlineData(" ")]
    public void ContractTracker_path_that_is_not_a_full_path_detects_it_with_a_problem_and_the_rest_applies(string path)
    {
        var content = $$"""
            { "schemaVersion": 2, "contractTracker": { "path": "{{path}}", "autoSync": false } }
            """;
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.ContractTracker.Should().Be(new ContractTrackerSettings(null, AutoSync: false));
        problem.Should().Be($"settings.json: contractTracker.path \"{path}\" is not a full path, detecting ContractTracker instead.");
        File.ReadAllText(SettingsPath).Should().Be(content);
    }

    [Fact]
    public void Unknown_key_in_the_ContractTracker_settings_makes_the_file_invalid()
    {
        WriteFile("""{ "schemaVersion": 2, "contractTracker": { "autosync": false } }""");

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(AppSettings.Default);
        problem.Should().StartWith("Settings not loaded");
    }

    // Ship (T7.2, D59)

    [Fact]
    public void Save_then_Load_keeps_the_ship()
    {
        var settings = AppSettings.Default with { Ship = "c2_hercules" };

        SettingsStore.Save(SettingsPath, settings);
        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(settings);
        problem.Should().BeNull();
        File.ReadAllText(SettingsPath).Should().Contain("\"ship\": \"c2_hercules\"");
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2 }""")]
    [InlineData("""{ "schemaVersion": 2, "ship": null }""")]
    public void File_without_a_ship_names_none_without_a_problem(string content)
    {
        // Optional, no schemaVersion bump (D31); the start picks the ship then (plan.json's or the Hull A).
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.Ship.Should().BeNull();
    }

    [Fact]
    public void An_unknown_ship_is_kept_as_written()
    {
        // Only the ship catalog knows which ships load; the start falls back and says so, the file keeps the choice.
        WriteFile("""{ "schemaVersion": 2, "ship": "nope" }""");

        SettingsStore.Load(SettingsPath).Settings.Ship.Should().Be("nope");
    }

    // Recent ships (T7.2c)

    [Fact]
    public void Save_then_Load_keeps_the_recent_ships_in_order()
    {
        var settings = AppSettings.Default with { Ship = "hull_c", RecentShips = ["hull_c", "c2_hercules", "hull_a"] };

        SettingsStore.Save(SettingsPath, settings);
        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        // A list compares by reference in a record, so field by field.
        loaded.Should().BeEquivalentTo(settings, o => o.WithStrictOrdering());
        problem.Should().BeNull();
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2 }""")]
    [InlineData("""{ "schemaVersion": 2, "recentShips": null }""")]
    public void File_without_recent_ships_names_none_without_a_problem(string content)
    {
        // Optional, no schemaVersion bump (D31), like "ship".
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.RecentShips.Should().BeNull();
    }

    [Fact]
    public void Empty_entries_in_recent_ships_are_dropped()
    {
        // Edited by hand: a gap names no ship. Unknown ids stay; only the catalog knows which load (like "ship").
        WriteFile("""{ "schemaVersion": 2, "recentShips": ["hull_c", null, " ", "nope"] }""");

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.RecentShips.Should().Equal("hull_c", "nope");
    }

    // Tool window theme (T4.7b, D36)

    [Fact]
    public void Save_then_Load_keeps_the_tool_window_theme()
    {
        var settings = AppSettings.Default with { ToolWindowTheme = "dark" };

        SettingsStore.Save(SettingsPath, settings);
        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(settings);
        problem.Should().BeNull();
        File.ReadAllText(SettingsPath).Should().Contain("\"toolWindowTheme\": \"dark\"");
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2 }""")]
    [InlineData("""{ "schemaVersion": 2, "toolWindowTheme": null }""")]
    public void File_without_tool_window_theme_follows_Windows_without_a_problem(string content)
    {
        // Optional, no schemaVersion bump (D31); null reads like missing, as legend: null (T4.3).
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.ToolWindowTheme.Should().Be("system");
    }

    [Fact]
    public void Invalid_tool_window_theme_follows_Windows_with_a_problem_and_the_rest_applies()
    {
        const string content = """
            { "schemaVersion": 2, "toolWindowTheme": "blue", "legend": { "visible": false } }
            """;
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.ToolWindowTheme.Should().Be("system");
        loaded.Legend.Visible.Should().BeFalse();
        problem.Should().Be("settings.json: toolWindowTheme \"blue\" is not valid (system, light or dark), using system.");
        File.ReadAllText(SettingsPath).Should().Be(content);
    }

    [Fact]
    public void Save_then_Load_keeps_the_ship_outline_mode()
    {
        var settings = AppSettings.Default with { ShipOutline = "none" };

        SettingsStore.Save(SettingsPath, settings);
        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(settings);
        problem.Should().BeNull();
        File.ReadAllText(SettingsPath).Should().Contain("\"shipOutline\": \"none\"");
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2 }""")]
    [InlineData("""{ "schemaVersion": 2, "shipOutline": null }""")]
    public void File_without_ship_outline_draws_the_outline_without_a_problem(string content)
    {
        // Optional, no schemaVersion bump (D31, D62).
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().BeNull();
        loaded.ShipOutline.Should().Be("outline");
    }

    [Fact]
    public void Invalid_ship_outline_draws_the_outline_with_a_problem_and_the_rest_applies()
    {
        const string content = """
            { "schemaVersion": 2, "shipOutline": "prism", "legend": { "visible": false } }
            """;
        WriteFile(content);

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.ShipOutline.Should().Be("outline");
        loaded.Legend.Visible.Should().BeFalse();
        problem.Should().Be("settings.json: shipOutline \"prism\" is not valid (outline or none), using outline.");
        File.ReadAllText(SettingsPath).Should().Be(content);
    }

    [Fact]
    public void Tool_window_theme_of_the_wrong_JSON_type_makes_the_file_invalid()
    {
        // Structural like every wrong type (D13): defaults plus a notice, not a correction.
        WriteFile("""{ "schemaVersion": 2, "toolWindowTheme": 1 }""");

        var (loaded, problem) = SettingsStore.Load(SettingsPath);

        loaded.Should().Be(AppSettings.Default);
        problem.Should().StartWith("Settings not loaded");
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
    [InlineData("""{ "schemaVersion": 2, "legend": { "align": "left" } }""")]                       // unknown key in legend
    [InlineData("""{ "schemaVersion": 2, "legend": { "visible": "no" } }""")]                       // wrong JSON type
    [InlineData("""{ "schemaVersion": 2, "legend": { "position": 3 } }""")]                         // a number, not a name
    [InlineData("""{ "schemaVersion": 2, "appearance": { "legend": { "position": "top-left" } } }""")] // look, not layout: wrong level
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
    public void Broken_file_problem_says_the_next_save_keeps_a_backup()
    {
        WriteFile("{ broken");

        var (_, problem) = SettingsStore.Load(SettingsPath);

        problem.Should().Contain($"The next save overwrites {SettingsPath}, keeping the old file as settings.json.bak.");
    }

    // Backup (T6.1)

    [Fact]
    public void BackUp_copies_byte_for_byte_even_if_the_file_is_not_JSON()
    {
        // BOM, broken JSON and a byte that is no UTF-8: a copy that decodes and re-encodes would change it.
        byte[] content = [0xEF, 0xBB, 0xBF, (byte)'{', (byte)' ', 0xFF, (byte)'\r', (byte)'\n'];
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(SettingsPath, content);

        SettingsStore.BackUp(SettingsPath);

        File.ReadAllBytes(SettingsStore.BackupPath(SettingsPath)).Should().Equal(content);
        File.ReadAllBytes(SettingsPath).Should().Equal(content);
    }

    [Fact]
    public void BackUp_replaces_an_older_backup()
    {
        WriteFile("new");
        File.WriteAllText(SettingsStore.BackupPath(SettingsPath), "from an earlier run");

        SettingsStore.BackUp(SettingsPath);

        File.ReadAllText(SettingsStore.BackupPath(SettingsPath)).Should().Be("new");
    }

    [Fact]
    public void BackUp_without_a_file_writes_nothing()
    {
        SettingsStore.BackUp(SettingsPath);

        File.Exists(SettingsStore.BackupPath(SettingsPath)).Should().BeFalse();
    }

    [Fact]
    public void BackupPath_is_next_to_the_file()
    {
        SettingsStore.BackupPath(SettingsPath).Should().Be(Path.Combine(_folder, "settings.json.bak"));
    }

    [Fact]
    public void DefaultPath_is_in_local_app_data()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        SettingsStore.DefaultPath.Should().Be(Path.Combine(localAppData, "SC-CargoPlan", "settings.json"));
    }
}
