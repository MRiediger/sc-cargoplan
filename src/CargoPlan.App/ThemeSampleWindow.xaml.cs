using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CargoPlan.App.Settings;
using CargoPlan.App.Theme;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App;

/// <summary>
/// Developer window for the tool window theme (T4.7a, D36), Debug builds only: every control the settings window and the
/// editor use, in Fluent light, dark or following Windows, with the teal accent. It reports whether the overlay stayed
/// untouched, so a window theme can't turn into an application one unnoticed.
/// </summary>
public partial class ThemeSampleWindow : Window
{
    private static ToolWindowTheme _theme;

    private readonly Window _overlay;
    private readonly byte[] _overlayAtStart;

    public ThemeSampleWindow(Window overlay)
    {
        // Before InitializeComponent, so the own styles in the XAML find Fluent's with BasedOn (D36).
        ToolWindowThemer.Apply(this, _theme);
        InitializeComponent();
        _overlay = overlay;
        _overlayAtStart = Snapshot(overlay);

        ModeBox.ItemsSource = Enum.GetValues<ToolWindowTheme>();
        ModeBox.SelectedItem = _theme;
        PositionBox.ItemsSource = LegendPosition.Names;
        Grid.ItemsSource = SampleRows();
    }

    public ColourField Colour { get; } = new("#FF2A9DB0", minAlpha: 0, () => { });

    /// <summary>This window only: settings and editor follow the setting in the settings window (T4.7b).</summary>
    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        _theme = (ToolWindowTheme)ModeBox.SelectedItem;
        ToolWindowThemer.Apply(this, _theme);

        // After the next render: the report compares what the overlay shows now with what it showed at start.
        Dispatcher.InvokeAsync(UpdateReport, DispatcherPriority.ContextIdle);
    }

    /// <summary>The overlay's pixels and the merged dictionaries against the start; Fluent for the app would add one.</summary>
    private void UpdateReport()
    {
        var now = Snapshot(_overlay);
        var differing = 0;
        if (now.Length == _overlayAtStart.Length)
        {
            for (var i = 0; i < now.Length; i += 4)
            {
                if (now[i] != _overlayAtStart[i] || now[i + 1] != _overlayAtStart[i + 1]
                    || now[i + 2] != _overlayAtStart[i + 2] || now[i + 3] != _overlayAtStart[i + 3])
                {
                    differing++;
                }
            }
        }
        else
        {
            differing = -1;
        }

        Report.Text =
            $"Overlay pixels differing from start: {(differing < 0 ? "size changed" : differing)}\n" +
            $"Application merged dictionaries: {Application.Current.Resources.MergedDictionaries.Count}\n" +
            $"Overlay merged dictionaries: {_overlay.Resources.MergedDictionaries.Count}\n" +
            $"This window merged dictionaries: {Resources.MergedDictionaries.Count}";
    }

    private static byte[] Snapshot(Window window)
    {
        if (window.Content is not Visual root || window.ActualWidth < 1 || window.ActualHeight < 1)
        {
            return [];
        }

        RenderTargetBitmap bitmap = new((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return pixels;
    }

    private static ObservableCollection<SampleRow> SampleRows() =>
    [
        .. Enumerable.Range(1, 30).Select(i => new SampleRow
        {
            Stop = (i + 2) / 3,
            Destination = i % 3 == 0 ? "Riker Memorial Spaceport" : "Seraphim Station",
            Commodity = i % 2 == 0 ? "Titanium" : "Agricium",
            Scu = 8 * i,
            MaxCrate = 16,
            Crates = $"{i}×8",
            HasError = i == 4,
        }),
    ];

    /// <summary>A row of the sample grid; settable, because the grid edits it and adds new ones.</summary>
    public sealed class SampleRow
    {
        public int Stop { get; set; }

        public string Destination { get; set; } = "";

        public string Commodity { get; set; } = "";

        public int Scu { get; set; }

        public int MaxCrate { get; set; } = 16;

        public string Crates { get; set; } = "";

        public bool HasError { get; set; }
    }
}
