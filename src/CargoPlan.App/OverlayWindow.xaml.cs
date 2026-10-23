using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using CargoPlan.App.Editor;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.App.Theme;
using CargoPlan.App.ViewModels;
using CargoPlan.App.Win32;

namespace CargoPlan.App;

/// <summary>The overlay: shows the ship view and legend, applies the lock mode (click-through) to the native window.</summary>
public partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _viewModel;
    private readonly SettingsSession _settings;
    private readonly Func<HotkeysViewModel> _createHotkeys;
    private readonly Func<ContractTrackerSectionViewModel> _createContractTracker;
    private readonly Func<Task> _syncFromContractTracker;
    private MissionEditorWindow? _editor;
    private SettingsWindow? _settingsWindow;

    /// <param name="createHotkeys">Made per settings window, from the hotkeys as they are then.</param>
    /// <param name="createContractTracker">Made per settings window, from the ContractTracker settings as they are then (D44).</param>
    /// <param name="syncFromContractTracker">The editor's "Sync from ContractTracker" (T5.4).</param>
    public OverlayWindow(
        OverlayViewModel viewModel,
        SettingsSession settings,
        Func<HotkeysViewModel> createHotkeys,
        Func<ContractTrackerSectionViewModel> createContractTracker,
        Func<Task> syncFromContractTracker)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        _createHotkeys = createHotkeys;
        _createContractTracker = createContractTracker;
        _syncFromContractTracker = syncFromContractTracker;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ShowCurrentShip();
    }

    // The HWND exists from SourceInitialized on; before that there is no native window to style.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Ticking a stop off at the terminal must not take the foreground from Star Citizen (D11, addendum T3.6).
        NativeMethods.PreventActivation(new WindowInteropHelper(this).Handle, ShowInTaskbar);
        ApplyLock(_viewModel.IsLocked);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OverlayViewModel.IsLocked))
        {
            ApplyLock(_viewModel.IsLocked);
        }
        else if (e.PropertyName == nameof(OverlayViewModel.IsOverlayHidden))
        {
            ApplyHidden(this, _viewModel.IsOverlayHidden);
        }
        else if (e.PropertyName == nameof(OverlayViewModel.CurrentShipChoice))
        {
            ShowCurrentShip();
        }
    }

    // --- Ship selection (T7.2b) ---
    // The box is not focusable, so keys don't reach it and the wheel doesn't select in the closed box: a selection
    // changes only by a click in the open list or Enter in its search (T7.2c), and every selection change is a choice
    // (as in the editor, T7.2), except while the overlay shows its ship or the search filters (_showingShip).

    private bool _showingShip;

    /// <summary>The overlay's ship in the box, without counting as a choice.</summary>
    private void ShowCurrentShip()
    {
        _showingShip = true;
        try
        {
            ShipBox.SelectedItem = _viewModel.CurrentShipChoice;
        }
        finally
        {
            _showingShip = false;
        }
    }

    private void OnShipSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_showingShip || ShipBox.SelectedItem is not ShipChoice choice || choice.Id == _viewModel.Ship.Id)
        {
            return;
        }

        // After the list has closed (a click selects before it closes), so the dialog doesn't sit under the open popup.
        Dispatcher.BeginInvoke(() => ChangeShip(choice), DispatcherPriority.Background);
    }

    // --- Ship search (T7.2c, D60) ---
    // The one exception to "the overlay never takes the focus" (D11): keys must reach the field. Spike over Star Citizen:
    // the field has the focus after the opening click, one click into the game gives it back.

    private TextBox ShipSearch => (TextBox)ShipBox.Template.FindName("ShipSearch", ShipBox);

    /// <summary>The opening click on the overlay gives this process the right to take the foreground (as the pencil's editor).</summary>
    private void OnShipListOpened(object? sender, EventArgs e)
    {
        Activate();
        var search = ShipSearch;
        if (search.Tag is null)
        {
            // Wired once, at the first opening: the template exists by then.
            search.Tag = true;
            search.TextChanged += OnShipSearchChanged;
            search.PreviewKeyDown += OnShipSearchKeyDown;
            // Visible only while the list is open (template trigger): focus it as soon as it shows.
            search.IsVisibleChanged += (_, _) =>
            {
                if (search.IsVisible)
                {
                    Keyboard.Focus(search);
                }
            };
        }
    }

    /// <summary>Closing empties the search, so the list opens unfiltered next time, and shows the overlay's ship again.</summary>
    private void OnShipListClosed(object? sender, EventArgs e)
    {
        ShipSearch.Text = "";
        ShowCurrentShip();
    }

    /// <summary>
    /// Filters on every key (<see cref="ShipChoices.Matches"/>). A filter that hides the selected entry clears the
    /// selection, which isn't a choice; the cleared search selects the overlay's ship again.
    /// </summary>
    private void OnShipSearchChanged(object sender, TextChangedEventArgs e)
    {
        var query = ShipSearch.Text;
        _showingShip = true;
        try
        {
            ShipBox.Items.Filter = string.IsNullOrWhiteSpace(query) ? null : o => o is ShipChoice c && ShipChoices.Matches(c, query);
        }
        finally
        {
            _showingShip = false;
        }

        if (ShipBox.Items.Filter is null)
        {
            ShowCurrentShip();
        }
    }

    /// <summary>
    /// Esc empties the search, then closes the list; Enter chooses the one ship left, as a click would. Tab and the arrow
    /// and page keys stay in the field: they would move the focus out of it, and the box closes when it loses the focus
    /// (app run T7.2c); the wheel scrolls the list. Alt+F4 would close the overlay, which is the app (D34).
    /// </summary>
    private void OnShipSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape when ShipSearch.Text.Length > 0:
                ShipSearch.Text = "";
                break;
            case Key.Escape:
                ShipBox.IsDropDownOpen = false;
                break;
            case Key.Enter:
                if (ShipChoices.SingleMatch(ShipBox.Items.OfType<ShipChoice>()) is { } match)
                {
                    ShipBox.SelectedItem = match;
                    ShipBox.IsDropDownOpen = false;
                }

                break;
            case Key.Tab or Key.Up or Key.Down or Key.PageUp or Key.PageDown:
            case Key.System when e.SystemKey == Key.F4:
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Asks first when there is a plan (D59); declined or not loadable, the box shows the overlay's ship again. The dialog
    /// takes the foreground; a click into the game gives it back (spike T7.2b over Star Citizen).
    /// </summary>
    private void ChangeShip(ShipChoice choice)
    {
        if (choice.Ship is not { } ship
            || (_viewModel.ShipChangeQuestion(ship) is { } question
                && MessageBox.Show(this, question, "Change ship", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes))
        {
            ShowCurrentShip();
            return;
        }

        _viewModel.ChangeShip(ship);
    }

    /// <summary>
    /// Hide keeps the window and its HWND (hotkeys stay registered), Show brings it back where it was; ShowActivated=False
    /// and WS_EX_NOACTIVATE keep the game in front. Code instead of a Visibility binding: Show() and Hide() set Visibility
    /// themselves, and the lock window needs the same call (D17).
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.window.hide
    /// </summary>
    internal static void ApplyHidden(Window window, bool hidden)
    {
        if (hidden)
        {
            window.Hide();
        }
        else
        {
            window.Show();
        }
    }

    /// <summary>
    /// Editor and settings window follow <see cref="OverlayViewModel.AreToolWindowsHidden"/> (D19, D34). Shown again
    /// without activation, because ShowActivated=False applies to every Show(), so the game keeps the focus.
    /// Sources: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos (owned windows of a
    /// topmost window are topmost), dotnet/wpf Window.nCmdForShow (SW_SHOWNA on every show without ShowActivated).
    /// </summary>
    internal static void HideWithOverlay(Window toolWindow, OverlayViewModel viewModel)
    {
        PropertyChangedEventHandler onChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(OverlayViewModel.AreToolWindowsHidden))
            {
                ApplyHidden(toolWindow, viewModel.AreToolWindowsHidden);
            }
        };
        viewModel.PropertyChanged += onChanged;
        toolWindow.Closed += (_, _) => viewModel.PropertyChanged -= onChanged;
    }

    /// <summary>Locked = click-through, see <see cref="NativeMethods.SetClickThrough"/>.</summary>
    private void ApplyLock(bool locked)
    {
        // Before the HWND check: the view must ignore wheel and drag even before the window shows.
        ShipView.IsNavigationEnabled = !locked;

        // The ship list is a window of its own and not click-through: left open over the game it would catch clicks.
        // The padlock and the game taking the mouse close it anyway, the lock hotkey doesn't (spike T7.2b: 0 of 5).
        if (locked)
        {
            ShipBox.IsDropDownOpen = false;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0)
        {
            return; // Not shown yet; OnSourceInitialized applies the current state.
        }

        NativeMethods.SetClickThrough(hwnd, locked);
    }

    /// <summary>
    /// Pencil and "Open editor": one editor at a time, a second click brings it to the front. Activate() because the
    /// editor has ShowActivated=False (D19); the click on the overlay gives this process the right to take the foreground.
    /// </summary>
    private void OnOpenEditor(object sender, RoutedEventArgs e)
    {
        if (_editor is null)
        {
            _editor = new MissionEditorWindow(new MissionEditorViewModel(_viewModel, _syncFromContractTracker), _viewModel, _settings.ToolWindowTheme, this);
            _editor.Closed += (_, _) => _editor = null;
            _editor.Show();
        }

        _editor.Activate();
    }

    /// <summary>
    /// Gear: one settings window at a time, a second click brings it to the front, like the editor. Opens next to the
    /// overlay; the position is not saved (D34).
    /// </summary>
    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is null)
        {
            // SystemFonts lives in WPF, so the window reads it and the view model stays WPF-free (D31).
            var current = _settings.Current;
            _settingsWindow = new SettingsWindow(
                new SettingsViewModel(
                    current.Appearance, current.Legend, SystemFonts.MessageFontSize, _settings.ChangeLook,
                    _settings.ToolWindowTheme, ChangeToolWindowTheme),
                _createHotkeys(),
                _createContractTracker(),
                _viewModel,
                _settings.ToolWindowTheme,
                this);

            Rect screen = new(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
            var position = SettingsWindowPlacement.NextTo(
                new Rect(Left, Top, ActualWidth, ActualHeight), new Size(_settingsWindow.Width, _settingsWindow.Height), screen);
            _settingsWindow.Left = position.X;
            _settingsWindow.Top = position.Y;

            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        _settingsWindow.Activate();
    }

    /// <summary>
    /// Exit: Close(), the same way as closing from the taskbar, not Application.Shutdown(). The savers write what is
    /// pending in their Closed handlers (bounds, settings, plan); owned windows close with the overlay, and the last
    /// window closing ends the app (ShutdownMode.OnLastWindowClose, the default). No dialog: nothing is lost (D34, T5.1).
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.application.shutdownmode
    /// </summary>
    private void OnExit(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// The overlay owns the tool windows, so it switches the open ones (D36); ones opened later read the mode from the
    /// session. A third tool window must be added here.
    /// </summary>
    private void ChangeToolWindowTheme(ToolWindowTheme theme)
    {
        _settings.ChangeToolWindowTheme(theme);
        foreach (var window in new Window?[] { _settingsWindow, _editor }.OfType<Window>())
        {
            ToolWindowThemer.Apply(window, theme);
        }
    }

    /// <summary>
    /// A right press on a stop line starts its drag into the ship view, which moves the whole stop (D25). The view runs
    /// the drag; a delivered stop is off the ship and stays. Left on the line stays the tick (D16).
    /// </summary>
    private void OnLegendRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LegendEntry { Delivered: false } entry })
        {
            ShipView.StartStopDrag(entry.Destination, e);
        }
    }

    // Borderless window: drag on header, hint lines and border. The ship view (orbit) and the legend's stop lines
    // handle their own left clicks, so this never runs there (D15, D16). Clicks in the open ship list (a group header)
    // bubble up to here too: the popup's logical parent is the box.
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ShipBox.IsDropDownOpen)
        {
            DragMove();
        }
    }
}
