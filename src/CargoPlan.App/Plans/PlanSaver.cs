using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Plans;

namespace CargoPlan.App.Plans;

/// <summary>
/// Saves the plan whenever it changes, debounced: a Star Citizen crash between two stops must not lose the ticks (D18).
/// Listens to <see cref="OverlayViewModel.Plan"/> itself, so every way of changing the plan saves it: tick, and
/// later editor and drag and drop.
/// </summary>
public sealed class PlanSaver
{
    /// <summary>Same rest as the window bounds (D13): a misclick and its correction write once.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

    private readonly Window _window;
    private readonly OverlayViewModel _viewModel;
    private readonly string _path;
    private readonly DispatcherTimer _timer = new() { Interval = Debounce };

    /// <param name="window">Its Closed event writes what is still pending.</param>
    public PlanSaver(Window window, OverlayViewModel viewModel, string path)
    {
        _window = window;
        _viewModel = viewModel;
        _path = path;
        _timer.Tick += OnTick;

        viewModel.PropertyChanged += OnViewModelChanged;
        window.Closed += OnClosed;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OverlayViewModel.Plan))
        {
            _timer.Stop();
            _timer.Start();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        Save();
    }

    /// <summary>Writes only what is still pending: closing without a change leaves the file alone.</summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _window.Closed -= OnClosed;

        if (_timer.IsEnabled)
        {
            _timer.Stop();
            Save();
        }
    }

    private void Save()
    {
        // Nothing sets the plan back to null yet; discarding a plan comes with the editor (T3.8b).
        if (_viewModel.Plan is not { } plan)
        {
            return;
        }

        try
        {
            PlanStore.Save(_path, new SavedPlan(_viewModel.Missions, plan));
            _viewModel.SetNotice(NoticeSource.Plan, null); // A load or save problem is solved once the file is written.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _viewModel.SetNotice(NoticeSource.Plan, $"Plan not saved ({ex.Message}).");
        }
    }
}
