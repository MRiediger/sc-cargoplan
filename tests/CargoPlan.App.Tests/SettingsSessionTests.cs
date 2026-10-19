using System.Windows;
using System.Windows.Media;
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

    [Fact]
    public void Colours_reach_the_resources()
    {
        var session = Create();

        session.ChangeLook(AppearanceSettings.Default with { Window = WindowAppearance.Default with { Background = "#40FF0000" } }, LegendSettings.Default);

        ((SolidColorBrush)_resources[Keys.WindowBackgroundBrush]).Color.Should().Be(Color.FromArgb(0x40, 0xFF, 0, 0));
    }
}
