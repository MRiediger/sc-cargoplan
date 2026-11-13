using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Plans;

namespace CargoPlan.App.Plans;

/// <summary>
/// Saves the plan whenever it changes, debounced: a Star Citizen crash between two stops must not lose the ticks (D18).
/// Listens to <see cref="OverlayViewModel.Saved"/> itself, so every way of changing the plan saves it: tick, editor,
/// drag and drop, and a scan, also when it only adds a mission that didn't fit (T5.4). The one writer of plan.json, so it
/// is also the one place that keeps an unreadable file aside before overwriting it (B13, D106), and the one that decides
/// what the Plan notice line shows after a save: a load or save problem goes, a hint stays for the run (B14).
/// </summary>
public sealed class PlanSaver
{
    /// <summary>Same rest as the window bounds (D13): a misclick and its correction write once.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

    private readonly Window _window;
    private readonly OverlayViewModel _viewModel;
    private readonly string _path;
    private readonly DispatcherTimer _timer = new() { Interval = Debounce };

    // The file at _path couldn't be read at the start: the first save moves it aside before writing, once (B13, D106).
    private bool _keepUnreadable;

    // The hint of the Plan line for the rest of the run, like an old hologram cache that stays (NoticeSource.HoloUpdate):
    // the start's converted captures (D107), the kept file (D106), or both. Every successful save shows it again.
    private string? _notice;

    /// <param name="window">Its Closed event writes what is still pending.</param>
    /// <param name="keepUnreadable"><see cref="PlanLoad.Unreadable"/> of the start's load of <paramref name="path"/>.</param>
    /// <param name="notice"><see cref="PlanLoad.Notice"/> of the start's load: a hint, no problem, so a save keeps it.</param>
    public PlanSaver(Window window, OverlayViewModel viewModel, string path, bool keepUnreadable = false, string? notice = null)
    {
        _keepUnreadable = keepUnreadable;
        _notice = notice;
        _window = window;
        _viewModel = viewModel;
        _path = path;
        _timer.Tick += OnTick;

        viewModel.PropertyChanged += OnViewModelChanged;
        window.Closed += OnClosed;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OverlayViewModel.Saved))
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

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _window.Closed -= OnClosed;
        Flush();
    }

    /// <summary>Writes only what is still pending: closing without a change leaves the file alone. Called by tests too.</summary>
    internal void Flush()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            Save();
        }
    }

    private void Save()
    {
        // Nothing sets the plan back to null: Discard plan leaves an empty one with its fill order (K2.3), saved like any.
        if (_viewModel.Saved is not { } saved)
        {
            return;
        }

        if (_keepUnreadable && !KeepUnreadable())
        {
            return;
        }

        try
        {
            PlanStore.Save(_path, saved, _viewModel.LastScanNo);
            // A load or save problem is solved once the file is written; a hint stays (B14).
            _viewModel.SetNotice(NoticeSource.Plan, _notice);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var problem = $"Plan not saved ({ex.Message}).";
            AppLog.Write("Plan", $"{problem} [{ex.GetType().Name}]");
            _viewModel.SetNotice(NoticeSource.Plan, problem);
        }
    }

    /// <summary>
    /// Moves the unreadable file aside (B13, D106). If that fails, nothing is written: better an unsaved change than an
    /// overwritten plan. The next save tries again; missing permissions fail every time, and the notice says why.
    /// </summary>
    /// <returns>true if the file is out of the way (kept, or gone by itself), so the save may write.</returns>
    private bool KeepUnreadable()
    {
        var name = Path.GetFileName(_path);
        try
        {
            if (PlanStore.KeepUnreadable(_path, DateTime.Now) is { } keptAs)
            {
                // With --plan the start file's captures can come over an unreadable plan.json: both hints, one line.
                var kept = $"Previous {name} couldn't be read; kept as {keptAs}.";
                _notice = _notice is null ? kept : $"{kept} · {_notice}";
                AppLog.Write("Plan", $"{_path} couldn't be read; kept as {keptAs}.");
            }

            _keepUnreadable = false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The whole message: "Access to the path … is denied." must stay readable (B13).
            var problem = $"Plan not saved: {name} couldn't be moved aside ({ex.Message}).";
            AppLog.Write("Plan", $"{problem} [{ex.GetType().Name}]");
            _viewModel.SetNotice(NoticeSource.Plan, problem);
            return false;
        }
    }
}
