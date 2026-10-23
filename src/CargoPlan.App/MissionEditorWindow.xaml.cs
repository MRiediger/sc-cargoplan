using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CargoPlan.App.Editor;
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
        viewModel.PropertyChanged += OnViewModelChanged;
        ShowCurrentShip();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Planned -= OnPlanned;
        _viewModel.Detach();
        base.OnClosed(e);
    }

    /// <summary>The plan is in the overlay, the next step is loading in the game (D19).</summary>
    private void OnPlanned(object? sender, EventArgs e) => Close();

    // --- Ship selection (T7.2) ---
    // A closed ComboBox selects at once on arrow keys (SelectNext/SelectPrev) and, with keyboard focus, on the mouse
    // wheel (a class handler that also runs for handled events); an open one only highlights until Enter or a click.
    // So keys, typing and the wheel open the list first, and every selection change is a choice: committed at once.
    // Source: dotnet/wpf, PresentationFramework/System/Windows/Controls/ComboBox.cs (KeyDownHandler, OnMouseWheel, FocusItem)

    private bool _showingShip;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MissionEditorViewModel.CurrentShip))
        {
            ShowCurrentShip();
        }
    }

    /// <summary>The overlay's ship in the box, without counting as a choice.</summary>
    private void ShowCurrentShip()
    {
        _showingShip = true;
        try
        {
            ShipBox.SelectedItem = _viewModel.CurrentShip;
        }
        finally
        {
            _showingShip = false;
        }
    }

    private void OnShipBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!ShipBox.IsDropDownOpen && e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
        {
            ShipBox.IsDropDownOpen = true;
            e.Handled = true;
        }
    }

    // Not handled: the text goes on to the text search, which only highlights in the open list.
    private void OnShipBoxPreviewTextInput(object sender, TextCompositionEventArgs e) => ShipBox.IsDropDownOpen = true;

    private void OnShipBoxPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!ShipBox.IsDropDownOpen && ShipBox.IsKeyboardFocusWithin)
        {
            ShipBox.IsDropDownOpen = true;
        }
    }

    private void OnShipSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_showingShip || ShipBox.SelectedItem is not ShipChoice choice || choice.Id == _viewModel.CurrentShip.Id)
        {
            return;
        }

        // After the list has closed (a click selects before it closes), so the dialog doesn't sit under the open popup.
        Dispatcher.BeginInvoke(() => ChangeShip(choice), DispatcherPriority.Background);
    }

    /// <summary>Asks first when there is a plan (D59); declined or not loadable, the box shows the overlay's ship again.</summary>
    private void ChangeShip(ShipChoice choice)
    {
        if (!choice.IsSelectable
            || (_viewModel.ShipChangeQuestion(choice) is { } question
                && MessageBox.Show(this, question, "Change ship", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes))
        {
            ShowCurrentShip();
            return;
        }

        _viewModel.ChangeShip(choice);
    }

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
