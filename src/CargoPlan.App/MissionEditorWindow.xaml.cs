using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using CargoPlan.App.ViewModels;
using Microsoft.Win32;

namespace CargoPlan.App;

/// <summary>
/// The mission editor (D19): rows in, plan out. File dialogs and the clipboard live here, the rest in
/// <see cref="MissionEditorViewModel"/>. Owned by the overlay: closes with it and stays above it.
/// </summary>
public partial class MissionEditorWindow : Window
{
    private const string MissionFileFilter = "Mission files (*.json)|*.json|All files (*.*)|*.*";

    private readonly MissionEditorViewModel _viewModel;
    private readonly OverlayViewModel _overlay;

    /// <param name="owner">Must already be shown: Owner rejects a window that was never shown.</param>
    public MissionEditorWindow(MissionEditorViewModel viewModel, OverlayViewModel overlay, Window owner)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _overlay = overlay;
        DataContext = viewModel;
        Owner = owner;
        overlay.PropertyChanged += OnOverlayChanged;
        viewModel.Planned += OnPlanned;
    }

    protected override void OnClosed(EventArgs e)
    {
        _overlay.PropertyChanged -= OnOverlayChanged;
        _viewModel.Planned -= OnPlanned;
        _viewModel.Detach();
        base.OnClosed(e);
    }

    /// <summary>
    /// Hidden with the overlay (hotkey), and while Star Citizen has the mouse: as an owned window of the topmost overlay
    /// the editor is topmost too and would catch the tractor-beam click (D11, D19). Shown again without activation,
    /// because ShowActivated=False applies to every Show(), so the game keeps the focus.
    /// Sources: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos (owned windows of a
    /// topmost window are topmost), dotnet/wpf Window.nCmdForShow (SW_SHOWNA on every show without ShowActivated).
    /// </summary>
    private void OnOverlayChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OverlayViewModel.IsOverlayHidden) or nameof(OverlayViewModel.IsAutoLocked))
        {
            OverlayWindow.ApplyHidden(this, _overlay.IsOverlayHidden || _overlay.IsAutoLocked);
        }
    }

    /// <summary>The plan is in the overlay, the next step is loading in the game (D19).</summary>
    private void OnPlanned(object? sender, EventArgs e) => Close();

    // Source: https://learn.microsoft.com/dotnet/api/system.windows.controls.datagrid.commitedit
    private void OnAnyButtonClick(object sender, RoutedEventArgs e) => Grid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

    // Microsoft.Win32 dialogs ship with WPF, no package needed.
    // Source: https://learn.microsoft.com/dotnet/desktop/wpf/windows/how-to-open-common-system-dialog-box
    private void OnImport(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Title = "Import missions", Filter = MissionFileFilter };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.Import(dialog.FileName);
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new()
        {
            Title = "Export missions",
            Filter = MissionFileFilter,
            DefaultExt = ".json",
            FileName = "missions.json",
        };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.Export(dialog.FileName);
        }
    }

    /// <summary>
    /// The clipboard is shared with every program; while another one holds it open, OleSetClipboard is retried 10 times
    /// every 100 ms and then fails with a COMException (CLIPBRD_E_CANT_OPEN), which derives from ExternalException.
    /// Not in the API docs, but a boundary (A6): shown, not swallowed.
    /// Source: dotnet/winforms, System.Private.Windows.Core/.../Ole/ClipboardCore.cs (OleRetryCount, OleRetryDelay)
    /// </summary>
    private void OnCopyText(object sender, RoutedEventArgs e)
    {
        if (_viewModel.PlanAsText() is not { } text)
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch (ExternalException ex)
        {
            _viewModel.ShowProblem($"Copy failed ({ex.Message}).");
        }
    }
}
