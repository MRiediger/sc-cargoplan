using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App.Tests;

/// <summary>The settings window's Updates section (T11.2, U8, U9): the switch, the skipped version and Check now.</summary>
public class UpdateSettingsViewModelTests
{
    private readonly List<bool> _checkOnStartup = [];

    private readonly List<string?> _skipped = [];

    private EventHandler<UpdateSettings>? _follow;

    private string _checkResult = "Up to date";

    private UpdateSettingsViewModel Create(UpdateSettings settings) =>
        new(
            settings,
            _checkOnStartup.Add,
            _skipped.Add,
            () => Task.FromResult(_checkResult),
            handler => _follow += handler,
            handler => _follow -= handler);

    [Fact]
    public void It_shows_the_settings_without_writing()
    {
        var updates = Create(new UpdateSettings(CheckOnStartup: false, SkippedVersion: "0.9.1"));

        updates.CheckOnStartup.Should().BeFalse();
        updates.HasSkippedVersion.Should().BeTrue();
        updates.SkippedText.Should().Be("Skipped version: v0.9.1");
        _checkOnStartup.Should().BeEmpty();
        _skipped.Should().BeEmpty();
    }

    [Fact]
    public void Without_a_skipped_version_its_line_is_hidden()
    {
        Create(UpdateSettings.Default).HasSkippedVersion.Should().BeFalse();
    }

    [Fact]
    public void The_switch_writes_through_the_session()
    {
        var updates = Create(UpdateSettings.Default);

        updates.CheckOnStartup = false;

        _checkOnStartup.Should().Equal(false);
    }

    [Fact]
    public void Clear_sets_the_skipped_version_to_null()
    {
        var updates = Create(new UpdateSettings(SkippedVersion: "0.9.1"));

        updates.ClearSkippedCommand.Execute(null);

        _skipped.Should().Equal([null]);
        updates.HasSkippedVersion.Should().BeFalse();
    }

    [Fact]
    public async Task Check_now_shows_its_result()
    {
        _checkResult = "Dev build – updates disabled";
        var updates = Create(UpdateSettings.Default);

        await updates.CheckNowCommand.ExecuteAsync(null);

        updates.Status.Should().Be("Dev build – updates disabled");
    }

    [Fact]
    public void A_skip_from_the_overlay_shows_in_the_open_window_without_writing_again()
    {
        var updates = Create(UpdateSettings.Default);

        _follow!.Invoke(null, new UpdateSettings(SkippedVersion: "0.9.1"));

        updates.SkippedText.Should().Be("Skipped version: v0.9.1");
        _skipped.Should().BeEmpty();
        _checkOnStartup.Should().BeEmpty();
    }

    [Fact]
    public void Detach_stops_following_the_session()
    {
        var updates = Create(UpdateSettings.Default);
        _follow.Should().NotBeNull();

        updates.Detach();

        _follow.Should().BeNull();
    }
}
