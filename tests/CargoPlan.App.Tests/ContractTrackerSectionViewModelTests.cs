using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;
using CargoPlan.Import.ContractTracker;

namespace CargoPlan.App.Tests;

/// <summary>The ContractTracker section of the settings window (T5.5, D44); the file system is a set of existing paths.</summary>
public sealed class ContractTrackerSectionViewModelTests
{
    private const string Manual = @"D:\Games\CT\contracts.db";

    private readonly HashSet<string> _existing = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ContractTrackerSettings> _changes = [];

    private ContractTrackerSectionViewModel Create(ContractTrackerSettings? settings = null) =>
        new(settings ?? ContractTrackerSettings.Default, _existing.Contains, _changes.Add);

    [Fact]
    public void Detected_database_shows_its_path_as_found_without_the_link()
    {
        _existing.Add(ContractTrackerReader.DefaultPath);

        var viewModel = Create();

        viewModel.PathText.Should().Be(ContractTrackerReader.DefaultPath);
        viewModel.IsDetected.Should().BeTrue();
        viewModel.IsFound.Should().BeTrue();
        viewModel.StatusText.Should().Be(ContractTrackerSectionViewModel.DetectedText);
        viewModel.AutoSync.Should().BeTrue();
        _changes.Should().BeEmpty("opening the window changes nothing");
    }

    [Fact]
    public void Missing_database_shows_the_hint_and_the_link()
    {
        var viewModel = Create();

        viewModel.IsFound.Should().BeFalse();
        viewModel.StatusText.Should().Be("ContractTracker not found – set path or install");
        ContractTrackerSectionViewModel.NexusUrl.Should().StartWith("https://www.nexusmods.com/");
    }

    [Fact]
    public void A_picked_path_applies_and_is_checked_again()
    {
        _existing.Add(Manual);
        var viewModel = Create(new ContractTrackerSettings(null, AutoSync: false));
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.SetPath(Manual);

        _changes.Should().Equal(new ContractTrackerSettings(Manual, AutoSync: false));
        viewModel.PathText.Should().Be(Manual);
        viewModel.IsDetected.Should().BeFalse();
        viewModel.StatusText.Should().Be(ContractTrackerSectionViewModel.FoundText);
        changed.Should().Contain([nameof(viewModel.PathText), nameof(viewModel.IsFound), nameof(viewModel.StatusText)]);
    }

    [Fact]
    public void A_manual_path_without_a_file_shows_the_hint()
    {
        var viewModel = Create(new ContractTrackerSettings(Manual));

        viewModel.IsFound.Should().BeFalse();
        viewModel.StatusText.Should().Be(ContractTrackerSectionViewModel.NotFoundText);
    }

    [Fact]
    public void Detect_automatically_goes_back_to_ContractTrackers_own_place()
    {
        var viewModel = Create(new ContractTrackerSettings(Manual));

        viewModel.DetectAutomaticallyCommand.CanExecute(null).Should().BeTrue();
        viewModel.DetectAutomaticallyCommand.Execute(null);

        _changes.Should().Equal(ContractTrackerSettings.Default);
        viewModel.PathText.Should().Be(ContractTrackerReader.DefaultPath);
        viewModel.DetectAutomaticallyCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Picking_the_same_path_again_changes_nothing()
    {
        var viewModel = Create(new ContractTrackerSettings(Manual));

        viewModel.SetPath(Manual);

        _changes.Should().BeEmpty();
    }

    [Fact]
    public void Switching_auto_sync_applies_and_keeps_the_path()
    {
        var viewModel = Create(new ContractTrackerSettings(Manual));

        viewModel.AutoSync = false;

        _changes.Should().Equal(new ContractTrackerSettings(Manual, AutoSync: false));
    }
}
