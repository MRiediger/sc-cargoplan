using CargoPlan.App.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// The settings window's Updates section (T11.2, U8, U9): the start's check on or off, the skipped version with Clear, and
/// Check now. Applied at once through the settings session (D30); its own DataContext like the hotkeys'. Follows the
/// session while the window is open, so a Skip on the overlay shows here too.
/// </summary>
public sealed partial class UpdateSettingsViewModel : ObservableObject
{
    public const string CheckingText = "Checking…";

    private readonly Action<bool> _changeCheckOnStartup;

    private readonly Action<string?> _changeSkippedVersion;

    private readonly Func<Task<string>> _checkNow;

    private readonly Action<EventHandler<UpdateSettings>> _unfollow;

    // True while values come from the session: they are what it holds already, nothing to write back.
    private bool _following;

    /// <param name="checkNow">Runs Check now and returns its status line; an offer goes to the overlay's update line.</param>
    /// <param name="follow">Subscribes to the session's <see cref="SettingsSession.UpdatesChanged"/>.</param>
    /// <param name="unfollow">Unsubscribes, from <see cref="Detach"/> when the window closes.</param>
    public UpdateSettingsViewModel(
        UpdateSettings current,
        Action<bool> changeCheckOnStartup,
        Action<string?> changeSkippedVersion,
        Func<Task<string>> checkNow,
        Action<EventHandler<UpdateSettings>> follow,
        Action<EventHandler<UpdateSettings>> unfollow)
    {
        (_changeCheckOnStartup, _changeSkippedVersion, _checkNow, _unfollow) = (changeCheckOnStartup, changeSkippedVersion, checkNow, unfollow);
        Show(current);
        follow(OnSessionChanged);
    }

    /// <summary>Off = no request at the start (D109); Check now still works.</summary>
    [ObservableProperty]
    public partial bool CheckOnStartup { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSkippedVersion), nameof(SkippedText))]
    [NotifyCanExecuteChangedFor(nameof(ClearSkippedCommand))]
    public partial string? SkippedVersion { get; private set; }

    public bool HasSkippedVersion => SkippedVersion is not null;

    public string SkippedText => $"Skipped version: v{SkippedVersion}";

    /// <summary>Check now's last result; empty until it runs.</summary>
    [ObservableProperty]
    public partial string? Status { get; private set; }

    /// <summary>The window closed: no more following the session.</summary>
    public void Detach() => _unfollow(OnSessionChanged);

    partial void OnCheckOnStartupChanged(bool value)
    {
        if (!_following)
        {
            _changeCheckOnStartup(value);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSkippedVersion))]
    private void ClearSkipped()
    {
        _changeSkippedVersion(null);
        SkippedVersion = null;
    }

    /// <summary>One at a time: the command stays disabled while it runs (AsyncRelayCommand's default).</summary>
    [RelayCommand]
    private async Task CheckNow()
    {
        Status = CheckingText;
        Status = await _checkNow();
    }

    private void OnSessionChanged(object? sender, UpdateSettings settings) => Show(settings);

    private void Show(UpdateSettings settings)
    {
        _following = true;
        CheckOnStartup = settings.CheckOnStartup;
        SkippedVersion = settings.SkippedVersion;
        _following = false;
    }
}
