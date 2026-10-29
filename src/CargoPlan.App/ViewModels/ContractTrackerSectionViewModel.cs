using CargoPlan.App.Settings;
using CargoPlan.Core.Planning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// The ContractTracker section of the settings window (T5.5, D44): where the database is, whether captures come in by
/// themselves and what a sync does with a contract ContractTracker no longer lists (D81). Its own data context like the
/// hotkeys (D35); WPF-free, the window runs the file dialog and the link. Whether the file exists is checked when the
/// window opens and on every change, not watched.
/// </summary>
public sealed partial class ContractTrackerSectionViewModel : ObservableObject
{
    public const string DetectedText = "Found at ContractTracker's own place.";

    public const string FoundText = "Found.";

    public const string NotFoundText = "ContractTracker not found – set path or install";

    /// <summary>
    /// ContractTracker by Yuiki on Nexus Mods. Source: web search, 2026-10-21; the page answers scripted requests with 403,
    /// so it is checked in the browser.
    /// </summary>
    public const string NexusUrl = "https://www.nexusmods.com/starcitizen/mods/34";

    private readonly Func<string, bool> _exists;
    private readonly Action<ContractTrackerSettings> _change;
    private readonly bool _loading = true;
    private ContractTrackerSettings _settings;

    /// <param name="settings">As the session holds them.</param>
    /// <param name="exists">File.Exists in the app, a set in the tests.</param>
    /// <param name="change">Saves the settings and points the sync at them; not called while loading.</param>
    public ContractTrackerSectionViewModel(ContractTrackerSettings settings, Func<string, bool> exists, Action<ContractTrackerSettings> change)
    {
        _settings = settings;
        _exists = exists;
        _change = change;
        AutoSync = settings.AutoSync;
        OnContractGone = settings.GonePolicy;
        _loading = false;
    }

    /// <summary>The list's entries, the default first.</summary>
    public static IReadOnlyList<ContractGoneChoice> GoneChoices { get; } =
    [
        new(ContractGonePolicy.Keep, "Keep in plan"),
        new(ContractGonePolicy.Remove, "Remove from plan"),
    ];

    /// <summary>The file the sync reads, detected or picked.</summary>
    public string PathText => _settings.DatabasePath;

    /// <summary>No path of one's own: ContractTracker's one place (D38).</summary>
    public bool IsDetected => _settings.Path is null;

    /// <summary>false shows the hint and the Nexus link.</summary>
    public bool IsFound => _exists(_settings.DatabasePath);

    public string StatusText => !IsFound ? NotFoundText : IsDetected ? DetectedText : FoundText;

    /// <summary>Off = a capture only gives the notice "N new contracts"; the editor's button or the hotkey takes them in.</summary>
    [ObservableProperty]
    public partial bool AutoSync { get; set; }

    /// <summary>Keep: the contract stays as it is, marked gone; remove: what isn't loaded leaves when it goes (D81).</summary>
    [ObservableProperty]
    public partial ContractGonePolicy OnContractGone { get; set; }

    /// <summary>From the window's file dialog, which only hands over full paths.</summary>
    public void SetPath(string path) => Change(_settings with { Path = path });

    partial void OnAutoSyncChanged(bool value)
    {
        if (!_loading)
        {
            Change(_settings with { AutoSync = value });
        }
    }

    partial void OnOnContractGoneChanged(ContractGonePolicy value)
    {
        if (!_loading)
        {
            Change(_settings with { OnContractGone = ContractGonePolicies.ToName(value) });
        }
    }

    /// <summary>
    /// Auto-sync on and gone contracts kept, in one change (D30); the path stays, its reset is
    /// <see cref="DetectAutomaticallyCommand"/>.
    /// </summary>
    [RelayCommand]
    private void Reset()
    {
        Change(_settings with { AutoSync = ContractTrackerSettings.Default.AutoSync, OnContractGone = ContractTrackerSettings.Default.OnContractGone });

        // The settings hold both already, so these change nothing more.
        AutoSync = _settings.AutoSync;
        OnContractGone = _settings.GonePolicy;
    }

    [RelayCommand(CanExecute = nameof(CanDetectAutomatically))]
    private void DetectAutomatically() => Change(_settings with { Path = null });

    private bool CanDetectAutomatically() => !IsDetected;

    private void Change(ContractTrackerSettings settings)
    {
        if (settings == _settings)
        {
            return;
        }

        _settings = settings;
        _change(settings);
        OnPropertyChanged(nameof(PathText));
        OnPropertyChanged(nameof(IsDetected));
        OnPropertyChanged(nameof(IsFound));
        OnPropertyChanged(nameof(StatusText));
        DetectAutomaticallyCommand.NotifyCanExecuteChanged();
    }
}
