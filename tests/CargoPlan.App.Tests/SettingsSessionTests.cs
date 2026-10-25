using System.Windows;
using System.Windows.Media;
using CargoPlan.App.Hotkeys;
using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Ships;
using Keys = CargoPlan.App.Theme.ThemeResources.Keys;

namespace CargoPlan.App.Tests;

/// <summary>Works on a fresh temp folder per test, like <see cref="SettingsStoreTests"/>. The debounce timer never ticks here: no dispatcher loop runs, so <see cref="SettingsSession.Flush"/> writes.</summary>
public sealed class SettingsSessionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));
    private readonly ResourceDictionary _resources = new();
    private readonly OverlayViewModel _viewModel = new(ShipLoader.Load("hull_a"));

    private string SettingsPath => Path.Combine(_folder, "settings.json");

    private static readonly WindowBounds Bounds = new(10, 20, 400, 500);

    private static readonly AppearanceSettings Thick = AppearanceSettings.Default with
    {
        Window = WindowAppearance.Default with { BorderThickness = 4 },
    };

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private SettingsSession Create(AppSettings? settings = null) =>
        new(_resources, _viewModel, SettingsPath, settings ?? AppSettings.Default);

    private AppSettings Saved() => SettingsStore.Load(SettingsPath).Settings;

    [Fact]
    public void Start_applies_the_look_and_the_legend_layout_without_writing()
    {
        Create(AppSettings.Default with { Appearance = Thick, Legend = new LegendSettings(false, "top-right") });

        _resources[Keys.WindowBorderThickness].Should().Be(new Thickness(4));
        _viewModel.IsLegendVisible.Should().BeFalse();
        _viewModel.LegendPosition.Should().Be(LegendPosition.Parse("top-right"));
        File.Exists(SettingsPath).Should().BeFalse();
    }

    [Fact]
    public void ChangeLook_applies_at_once_and_writes_on_flush()
    {
        var session = Create();

        session.ChangeLook(Thick, new LegendSettings(false, "top-center"));

        _resources[Keys.WindowBorderThickness].Should().Be(new Thickness(4));
        _viewModel.IsLegendVisible.Should().BeFalse();
        _viewModel.LegendPosition.Should().Be(LegendPosition.Parse("top-center"));
        File.Exists(SettingsPath).Should().BeFalse();

        session.Flush();

        Saved().Appearance.Should().Be(Thick);
        Saved().Legend.Should().Be(new LegendSettings(false, "top-center"));
    }

    [Fact]
    public void The_stop_palette_goes_to_the_view_model_at_start_and_on_change()
    {
        // K4.2: not a theme resource but a view model property, so the scene and with it the selection stay (D25).
        _viewModel.StopPalette.Should().BeSameAs(ShipView.StopPalettes.OkabeIto);
        var session = Create(AppSettings.Default with
        {
            Appearance = AppearanceSettings.Default with { Crates = CrateAppearance.Default with { StopPalette = "tolMuted" } },
        });
        _viewModel.StopPalette.Should().BeSameAs(ShipView.StopPalettes.TolMuted);
        var scene = _viewModel.Scene;

        session.ChangeLook(AppearanceSettings.Default with { Crates = CrateAppearance.Default with { StopPalette = "tolBright" } }, LegendSettings.Default);

        _viewModel.StopPalette.Should().BeSameAs(ShipView.StopPalettes.TolBright);
        _viewModel.Scene.Should().BeSameAs(scene);
    }

    [Fact]
    public void An_imported_look_changes_the_look_and_nothing_else()
    {
        // K4.3 gate: hotkeys, ContractTracker, bounds, cameras, ship and the download switch stay; the window is wired to
        // the session as the overlay wires it.
        var before = AppSettings.Default with
        {
            Overlay = Bounds,
            Hotkeys = new HotkeySettings(ToggleLock: "Ctrl+Alt+L", Undo: "Ctrl+Alt+Z"),
            ContractTracker = new ContractTrackerSettings(@"C:\Data\contracts.db", AutoSync: false),
            Ship = "c2_hercules",
            RecentShips = ["c2_hercules", "hull_a"],
            DownloadShipHolograms = false,
            ShipView = new ShipViewSettings(100, new Dictionary<string, ShipCamera> { ["c2_hercules"] = new(30, 20, 50, 1, 2) }),
        };
        var session = Create(before);
        SettingsViewModel window = new(
            before.Appearance, before.Legend, 11, session.ChangeLook,
            session.ToolWindowTheme, session.ChangeToolWindowTheme,
            session.ShipOutline, session.ChangeShipOutline,
            session.DownloadShipHolograms, session.ChangeDownloadShipHolograms,
            session.HoloOpacity, session.ChangeHoloOpacity,
            before.HoloHullColor, before.HoloEdgeColor, session.ChangeHoloColors,
            session.MaxZoom, session.ChangeMaxZoom,
            notice => _viewModel.SetNotice(NoticeSource.Look, notice));
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "haul.cargoplan-look.json");
        using (var file = File.Create(path))
        {
            LookFile.Write(file, LookFileTests.Custom);
        }

        window.ImportLook(path);
        session.Flush();

        var saved = Saved();
        Look.Of(saved).Should().Be(LookFileTests.Custom);
        saved.Should().Be(before with
        {
            Appearance = saved.Appearance,
            Legend = saved.Legend,
            ToolWindowTheme = saved.ToolWindowTheme,
            ShipOutline = saved.ShipOutline,
            HoloOpacity = saved.HoloOpacity,
            HoloHullColor = saved.HoloHullColor,
            HoloEdgeColor = saved.HoloEdgeColor,
            // Lists and dictionaries compare by reference in a record: checked one by one below.
            ShipView = saved.ShipView,
            RecentShips = saved.RecentShips,
        });
        saved.ShipView.MaxZoomPxPerCell.Should().Be(LookFileTests.Custom.MaxZoomPxPerCell);
        saved.ShipView.Cameras.Should().BeEquivalentTo(before.ShipView.Cameras);
        saved.RecentShips.Should().Equal("c2_hercules", "hull_a");
        _viewModel.Notices.Should().Contain("Look imported: haul.cargoplan-look.json");
    }

    [Fact]
    public void ChangeBounds_writes_at_once_with_a_pending_look()
    {
        var session = Create();
        session.ChangeLook(Thick, LegendSettings.Default);

        session.ChangeBounds(Bounds);

        Saved().Should().Be(AppSettings.Default with { Overlay = Bounds, Appearance = Thick });
    }

    [Fact]
    public void ChangeLook_keeps_bounds_set_meanwhile()
    {
        var session = Create();
        session.ChangeBounds(Bounds);

        session.ChangeLook(Thick, LegendSettings.Default);
        session.Flush();

        Saved().Overlay.Should().Be(Bounds);
        Saved().Appearance.Should().Be(Thick);
    }

    [Fact]
    public void ChangeLook_keeps_the_hotkeys()
    {
        var hotkeys = new HotkeySettings("Ctrl+Alt+L", "Ctrl+Alt+H");
        var session = Create(AppSettings.Default with { Hotkeys = hotkeys });

        session.ChangeLook(Thick, LegendSettings.Default);
        session.Flush();

        Saved().Hotkeys.Should().Be(hotkeys);
    }

    [Fact]
    public void Unchanged_bounds_write_nothing()
    {
        var session = Create(AppSettings.Default with { Overlay = Bounds });

        session.ChangeBounds(Bounds);

        File.Exists(SettingsPath).Should().BeFalse();
    }

    [Fact]
    public void Flush_without_a_change_writes_nothing()
    {
        Create().Flush();

        File.Exists(SettingsPath).Should().BeFalse();
    }

    [Fact]
    public void A_save_clears_a_load_problem()
    {
        var session = Create();
        _viewModel.SetNotice(NoticeSource.Settings, "Settings not loaded");

        session.ChangeBounds(Bounds);

        _viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public void A_failed_save_shows_a_notice_and_the_next_save_clears_it()
    {
        Directory.CreateDirectory(SettingsPath); // A folder where the file should be: the move fails.
        var session = Create();

        session.ChangeBounds(Bounds);

        _viewModel.Notices.Should().ContainSingle().Which.Should().StartWith("Settings not saved");

        Directory.Delete(SettingsPath);
        session.ChangeBounds(Bounds with { Left = 11 });

        _viewModel.Notices.Should().BeEmpty();
    }

    // Backup (T6.1)

    private string BackupPath => SettingsStore.BackupPath(SettingsPath);

    [Fact]
    public void The_first_save_backs_up_the_file_as_it_was_and_later_saves_leave_the_backup()
    {
        const string broken = """{ "schemaVersion": 2, "overlay": """;
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, broken);
        var original = File.ReadAllBytes(SettingsPath);
        var session = Create();

        session.ChangeBounds(Bounds);
        session.ChangeBounds(Bounds with { Left = 11 });

        File.ReadAllBytes(BackupPath).Should().Equal(original);
        Saved().Overlay.Should().Be(Bounds with { Left = 11 });
    }

    [Fact]
    public void Without_a_file_no_backup_is_written()
    {
        var session = Create();

        session.ChangeBounds(Bounds);
        session.ChangeBounds(Bounds with { Left = 11 });

        File.Exists(BackupPath).Should().BeFalse();
        _viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public void A_failed_backup_leaves_the_file_alone_and_the_next_save_retries()
    {
        const string handEdited = """{ "schemaVersion": 2 }""";
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, handEdited);
        var session = Create();

        using (File.Open(BackupPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None)) // locked: the copy fails
        {
            session.ChangeBounds(Bounds);

            _viewModel.Notices.Should().ContainSingle().Which.Should().StartWith("Settings not saved");
            File.ReadAllText(SettingsPath).Should().Be(handEdited);
        }

        session.ChangeBounds(Bounds with { Left = 11 });

        File.ReadAllText(BackupPath).Should().Be(handEdited);
        Saved().Overlay.Should().Be(Bounds with { Left = 11 });
        _viewModel.Notices.Should().BeEmpty();
    }

    /// <summary>Preview and save run the same check as loading: what the overlay shows now is what it shows after a restart.</summary>
    [Fact]
    public void ChangeLook_corrects_like_loading_and_the_notice_outlasts_the_save()
    {
        var session = Create();

        session.ChangeLook(AppearanceSettings.Default with { Window = WindowAppearance.Default with { BorderThickness = 99 } }, LegendSettings.Default);

        _resources[Keys.WindowBorderThickness].Should().Be(new Thickness(AppearanceRules.MaxBorderThickness));
        _viewModel.Notices.Should().ContainSingle().Which.Should().Contain("window.borderThickness 99");

        session.Flush();

        Saved().Appearance.Window.BorderThickness.Should().Be(AppearanceRules.MaxBorderThickness);
        _viewModel.Notices.Should().ContainSingle().Which.Should().Contain("window.borderThickness 99");
    }

    [Fact]
    public void A_valid_change_after_a_correction_clears_its_notice_on_save()
    {
        var session = Create();
        session.ChangeLook(AppearanceSettings.Default with { Window = WindowAppearance.Default with { BorderThickness = 99 } }, LegendSettings.Default);

        session.ChangeLook(Thick, LegendSettings.Default);
        session.Flush();

        _viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public void An_unknown_legend_position_is_corrected_too()
    {
        var session = Create();

        session.ChangeLook(AppearanceSettings.Default, new LegendSettings(true, "up"));

        _viewModel.LegendPosition.Should().Be(LegendPosition.Default);
        _viewModel.Notices.Should().ContainSingle().Which.Should().Contain("legend.position \"up\"");
    }

    // Hotkeys (T4.4)

    private static Hotkey Key(string text)
    {
        HotkeyParser.TryParse(text, out var hotkey, out _).Should().BeTrue();
        return hotkey!;
    }

    [Fact]
    public void ChangeHotkey_writes_the_canonical_text_on_flush_and_keeps_the_rest()
    {
        var session = Create(AppSettings.Default with { Overlay = Bounds, Appearance = Thick });

        session.ChangeHotkey(HotkeyAction.ToggleLock, Key("alt+ctrl+l"));
        File.Exists(SettingsPath).Should().BeFalse();
        session.Flush();

        Saved().Hotkeys.Should().Be(new HotkeySettings("Ctrl+Alt+L", null));
        Saved().Overlay.Should().Be(Bounds);
        Saved().Appearance.Should().Be(Thick);
    }

    [Fact]
    public void ChangeHotkey_keeps_the_other_action()
    {
        var session = Create(AppSettings.Default with { Hotkeys = new HotkeySettings("Ctrl+Alt+L", "Ctrl+Alt+H") });

        session.ChangeHotkey(HotkeyAction.ToggleOverlay, Key("Ctrl+Alt+O"));

        session.Current.Hotkeys.Should().Be(new HotkeySettings("Ctrl+Alt+L", "Ctrl+Alt+O"));
    }

    /// <summary>A cleared hotkey is written as null, so the player sees the key exists; missing and null read the same.</summary>
    [Fact]
    public void Clearing_writes_null_for_the_action()
    {
        var session = Create(AppSettings.Default with { Hotkeys = new HotkeySettings("Ctrl+Alt+L", "Ctrl+Alt+H") });

        session.ChangeHotkey(HotkeyAction.ToggleLock, null);
        session.Flush();

        File.ReadAllText(SettingsPath).Should().Contain("\"toggleLock\": null").And.Contain("\"toggleOverlay\": \"Ctrl+Alt+H\"");
        Saved().Hotkeys.Should().Be(new HotkeySettings(null, "Ctrl+Alt+H"));
    }

    [Fact]
    public void Clearing_without_hotkeys_still_writes_every_key()
    {
        var session = Create();

        session.ChangeHotkey(HotkeyAction.ToggleLock, null);
        session.Flush();

        File.ReadAllText(SettingsPath).Should().Contain("\"toggleLock\": null").And.Contain("\"toggleOverlay\": null")
            .And.Contain("\"syncContractTracker\": null").And.Contain("\"replan\": null").And.Contain("\"discardPlan\": null")
            .And.Contain("\"undo\": null").And.Contain("\"redo\": null").And.Contain("\"resetView\": null");
    }

    [Fact]
    public void ChangeHotkey_for_the_reset_view_keeps_the_other_actions()
    {
        var session = Create(AppSettings.Default with { Hotkeys = new HotkeySettings("Ctrl+Alt+L", Undo: "Ctrl+Alt+Z") });

        session.ChangeHotkey(HotkeyAction.ResetView, Key("Ctrl+Alt+V"));
        session.Flush();

        Saved().Hotkeys.Should().Be(new HotkeySettings("Ctrl+Alt+L", Undo: "Ctrl+Alt+Z", ResetView: "Ctrl+Alt+V"));
    }

    [Fact]
    public void ChangeHotkey_for_the_sync_keeps_the_other_actions()
    {
        var session = Create(AppSettings.Default with { Hotkeys = new HotkeySettings("Ctrl+Alt+L", "Ctrl+Alt+H") });

        session.ChangeHotkey(HotkeyAction.SyncContractTracker, Key("Ctrl+Alt+S"));
        session.Flush();

        Saved().Hotkeys.Should().Be(new HotkeySettings("Ctrl+Alt+L", "Ctrl+Alt+H", "Ctrl+Alt+S"));
    }

    [Fact]
    public void ChangeHotkey_for_the_replan_keeps_the_other_actions()
    {
        var session = Create(AppSettings.Default with { Hotkeys = new HotkeySettings("Ctrl+Alt+L", "Ctrl+Alt+H", "Ctrl+Alt+S") });

        session.ChangeHotkey(HotkeyAction.Replan, Key("Ctrl+Alt+R"));
        session.Flush();

        Saved().Hotkeys.Should().Be(new HotkeySettings("Ctrl+Alt+L", "Ctrl+Alt+H", "Ctrl+Alt+S", "Ctrl+Alt+R"));
    }

    [Fact]
    public void ChangeHotkey_for_the_discard_keeps_the_other_actions()
    {
        var session = Create(AppSettings.Default with { Hotkeys = new HotkeySettings("Ctrl+Alt+L", "Ctrl+Alt+H", "Ctrl+Alt+S", "Ctrl+Alt+R") });

        session.ChangeHotkey(HotkeyAction.DiscardPlan, Key("Ctrl+Alt+D"));
        session.Flush();

        Saved().Hotkeys.Should().Be(new HotkeySettings("Ctrl+Alt+L", "Ctrl+Alt+H", "Ctrl+Alt+S", "Ctrl+Alt+R", "Ctrl+Alt+D"));
    }

    [Fact]
    public void ChangeHotkey_for_undo_and_redo_keeps_the_other_actions()
    {
        var session = Create(AppSettings.Default with { Hotkeys = new HotkeySettings("Ctrl+Alt+L") });

        session.ChangeHotkey(HotkeyAction.Undo, Key("Ctrl+Alt+Z"));
        session.ChangeHotkey(HotkeyAction.Redo, Key("Ctrl+Alt+Y"));
        session.Flush();

        Saved().Hotkeys.Should().Be(new HotkeySettings("Ctrl+Alt+L", Undo: "Ctrl+Alt+Z", Redo: "Ctrl+Alt+Y"));
    }

    // ContractTracker (T5.5, D44)

    [Fact]
    public void ChangeContractTracker_writes_on_flush_and_keeps_the_rest()
    {
        var hotkeys = new HotkeySettings("Ctrl+Alt+L", null);
        var session = Create(AppSettings.Default with { Overlay = Bounds, Appearance = Thick, Hotkeys = hotkeys });
        var changed = new ContractTrackerSettings(@"D:\CT\contracts.db", AutoSync: false);

        session.ChangeContractTracker(changed);

        session.Current.ContractTracker.Should().Be(changed);
        File.Exists(SettingsPath).Should().BeFalse();

        session.Flush();

        Saved().Should().Be(AppSettings.Default with { Overlay = Bounds, Appearance = Thick, Hotkeys = hotkeys, ContractTracker = changed });
    }

    // Ship (T7.2)

    [Fact]
    public void ChangeShip_writes_on_flush_and_keeps_the_rest()
    {
        var session = Create(AppSettings.Default with { Overlay = Bounds, Appearance = Thick });

        session.ChangeShip("c2_hercules", ["c2_hercules", "hull_a"]);

        session.Current.Ship.Should().Be("c2_hercules");
        File.Exists(SettingsPath).Should().BeFalse();

        session.Flush();

        // The recent ships with it (T7.2c); a list compares by reference in a record, so field by field.
        Saved().Should().BeEquivalentTo(
            AppSettings.Default with { Overlay = Bounds, Appearance = Thick, Ship = "c2_hercules", RecentShips = ["c2_hercules", "hull_a"] },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void ChangeShip_to_the_ship_already_set_writes_nothing()
    {
        var session = Create(AppSettings.Default with { Ship = "c2_hercules" });

        session.ChangeShip("c2_hercules", ["c2_hercules"]);
        session.Flush();

        File.Exists(SettingsPath).Should().BeFalse();
    }

    [Fact]
    public void ChangeLook_keeps_the_ContractTracker_settings()
    {
        var changed = new ContractTrackerSettings(null, AutoSync: false);
        var session = Create(AppSettings.Default with { ContractTracker = changed });

        session.ChangeLook(Thick, LegendSettings.Default);
        session.Flush();

        Saved().ContractTracker.Should().Be(changed);
    }

    // Tool window theme (T4.7b)

    [Fact]
    public void ChangeToolWindowTheme_writes_on_flush_and_keeps_the_rest()
    {
        var hotkeys = new HotkeySettings("Ctrl+Alt+L", null);
        var session = Create(AppSettings.Default with { Overlay = Bounds, Appearance = Thick, Hotkeys = hotkeys });

        session.ChangeToolWindowTheme(ToolWindowTheme.Dark);

        session.ToolWindowTheme.Should().Be(ToolWindowTheme.Dark);
        File.Exists(SettingsPath).Should().BeFalse();

        session.Flush();

        Saved().Should().Be(AppSettings.Default with { Overlay = Bounds, Appearance = Thick, Hotkeys = hotkeys, ToolWindowTheme = "dark" });
    }

    [Fact]
    public void The_start_applies_the_ship_outline_mode_to_the_overlay()
    {
        Create(AppSettings.Default with { ShipOutline = "none" });

        _viewModel.OutlineMode.Should().Be(ShipOutlineMode.None);
    }

    [Fact]
    public void ChangeShipOutline_applies_at_once_writes_on_flush_and_keeps_the_rest()
    {
        var session = Create(AppSettings.Default with { Overlay = Bounds, Appearance = Thick, ToolWindowTheme = "dark" });

        session.ChangeShipOutline(ShipOutlineMode.None);

        _viewModel.OutlineMode.Should().Be(ShipOutlineMode.None);
        session.ShipOutline.Should().Be(ShipOutlineMode.None);
        File.Exists(SettingsPath).Should().BeFalse();

        session.Flush();

        Saved().Should().Be(AppSettings.Default with { Overlay = Bounds, Appearance = Thick, ToolWindowTheme = "dark", ShipOutline = "none" });
    }

    [Fact]
    public void ChangeDownloadShipHolograms_writes_on_flush_and_tells_the_ship_view()
    {
        var session = Create(AppSettings.Default with { ShipOutline = "holo-shaded" });
        var told = 0;
        session.ShipViewChanged += (_, _) => told++;

        session.ChangeDownloadShipHolograms(false);
        session.ChangeDownloadShipHolograms(false);

        told.Should().Be(1);
        session.DownloadShipHolograms.Should().BeFalse();
        session.Flush();
        Saved().Should().Be(AppSettings.Default with { ShipOutline = "holo-shaded", DownloadShipHolograms = false });
    }

    [Fact]
    public void The_start_and_ChangeHoloColors_apply_the_hologram_colours_and_save_on_flush()
    {
        var session = Create(AppSettings.Default with { HoloEdgeColor = "#FFFFCC00" });
        _viewModel.HoloEdgeColor.Should().Be("#FFFFCC00");
        _viewModel.HoloHullColor.Should().Be("#FF9CB0C0");

        session.ChangeHoloColors("#FF336699", "#FF00FF00");

        _viewModel.HoloHullColor.Should().Be("#FF336699");
        _viewModel.HoloEdgeColor.Should().Be("#FF00FF00");
        File.Exists(SettingsPath).Should().BeFalse();
        session.Flush();
        Saved().Should().Be(AppSettings.Default with { HoloHullColor = "#FF336699", HoloEdgeColor = "#FF00FF00" });
    }

    [Fact]
    public void The_start_applies_the_hologram_opacity_to_the_overlay()
    {
        Create(AppSettings.Default with { HoloOpacity = 0.7 });

        _viewModel.HoloOpacity.Should().Be(0.7);
    }

    [Fact]
    public void ChangeHoloOpacity_applies_at_once_and_writes_on_flush()
    {
        var session = Create(AppSettings.Default with { ShipOutline = "holo-flat" });

        session.ChangeHoloOpacity(0.25);

        _viewModel.HoloOpacity.Should().Be(0.25);
        File.Exists(SettingsPath).Should().BeFalse();
        session.Flush();
        Saved().Should().Be(AppSettings.Default with { ShipOutline = "holo-flat", HoloOpacity = 0.25 });
    }

    [Fact]
    public void The_start_applies_the_max_zoom_to_the_overlay()
    {
        Create(AppSettings.Default with { ShipView = new ShipViewSettings(MaxZoomPxPerCell: 90) });

        _viewModel.MaxZoomPxPerCell.Should().Be(90);
    }

    [Fact]
    public void ChangeMaxZoom_applies_at_once_and_writes_on_flush()
    {
        var session = Create();

        session.ChangeMaxZoom(200);

        _viewModel.MaxZoomPxPerCell.Should().Be(200);
        File.Exists(SettingsPath).Should().BeFalse();
        session.Flush();
        Saved().Should().Be(AppSettings.Default with { ShipView = new ShipViewSettings(MaxZoomPxPerCell: 200) });
    }

    [Fact]
    public void ChangeCameras_writes_on_flush_and_keeps_the_max_zoom()
    {
        // Debounced like a look change (D13, T3.4): an orbit drag writes once, after 500 ms of rest.
        var session = Create(AppSettings.Default with { ShipView = new ShipViewSettings(MaxZoomPxPerCell: 90) });
        Dictionary<string, ShipCamera> cameras = new() { ["hull_a"] = new ShipCamera(120, 20) };

        session.ChangeCameras(cameras);

        File.Exists(SettingsPath).Should().BeFalse();
        session.Flush();
        Saved().ShipView.MaxZoomPxPerCell.Should().Be(90);
        Saved().ShipView.Cameras.Should().BeEquivalentTo(cameras);
    }

    [Fact]
    public void ChangeMaxZoom_keeps_the_cameras()
    {
        // The Ship view section's reset sets the zoom, never the cameras.
        Dictionary<string, ShipCamera> cameras = new() { ["hull_a"] = new ShipCamera(120, 20) };
        var session = Create(AppSettings.Default with { ShipView = new ShipViewSettings(Cameras: cameras) });

        session.ChangeMaxZoom(200);
        session.Flush();

        Saved().ShipView.Cameras.Should().BeEquivalentTo(cameras);
    }

    [Fact]
    public void ChangeShipOutline_tells_the_ship_view()
    {
        var session = Create(AppSettings.Default);
        var told = 0;
        session.ShipViewChanged += (_, _) => told++;

        session.ChangeShipOutline(ShipOutlineMode.HoloFlat);

        told.Should().Be(1);
        _viewModel.OutlineMode.Should().Be(ShipOutlineMode.HoloFlat);
    }

    [Fact]
    public void ChangeLook_keeps_the_tool_window_theme()
    {
        var session = Create(AppSettings.Default with { ToolWindowTheme = "light" });

        session.ChangeLook(Thick, LegendSettings.Default);
        session.Flush();

        Saved().ToolWindowTheme.Should().Be("light");
        session.ToolWindowTheme.Should().Be(ToolWindowTheme.Light);
    }

    [Fact]
    public void Colours_reach_the_resources()
    {
        var session = Create();

        session.ChangeLook(AppearanceSettings.Default with { Window = WindowAppearance.Default with { Background = "#40FF0000" } }, LegendSettings.Default);

        ((SolidColorBrush)_resources[Keys.WindowBackgroundBrush]).Color.Should().Be(Color.FromArgb(0x40, 0xFF, 0, 0));
    }
}
