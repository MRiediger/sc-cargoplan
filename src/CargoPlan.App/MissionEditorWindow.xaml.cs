using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using CargoPlan.App.Settings;
using CargoPlan.App.Theme;
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

    /// <param name="theme">The tool windows' mode (D36); the overlay switches it later through <see cref="ToolWindowThemer"/>.</param>
    /// <param name="owner">Must already be shown: Owner rejects a window that was never shown.</param>
    public MissionEditorWindow(MissionEditorViewModel viewModel, OverlayViewModel overlay, ToolWindowTheme theme, Window owner)
    {
        // Before InitializeComponent, so the own styles in the XAML find Fluent's with BasedOn (D36).
        ToolWindowThemer.Apply(this, theme);
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Owner = owner;
        OverlayWindow.HideWithOverlay(this, overlay);
        viewModel.Planned += OnPlanned;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Planned -= OnPlanned;
        _viewModel.Detach();
        base.OnClosed(e);
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
