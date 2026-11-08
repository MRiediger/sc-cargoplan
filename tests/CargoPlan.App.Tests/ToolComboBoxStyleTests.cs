using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CargoPlan.App.Settings;
using CargoPlan.App.Theme;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// One combo box style for every tool window (session 4d, D36 addendum): <see cref="ToolWindowThemer"/> derives
/// ToolComboBoxStyle and ToolComboBoxItemStyle from Fluent's and puts them into each window, implicit and by key.
/// </summary>
public sealed class ToolComboBoxStyleTests
{
    // Fluent loads through pack://, which Application's static constructor sets up; see ToolWindowThemerTests.
    static ToolComboBoxStyleTests() => _ = Application.Current;

    [Theory]
    [InlineData(ToolWindowTheme.Light)]
    [InlineData(ToolWindowTheme.Dark)]
    public void Apply_puts_both_styles_into_the_window_implicit_and_by_key(ToolWindowTheme theme)
    {
        Sta.Run(() =>
        {
            Window window = new();

            ToolWindowThemer.Apply(window, theme);

            var combo = (Style)window.Resources[ToolWindowThemer.ComboBoxStyleKey];
            var item = (Style)window.Resources[ToolWindowThemer.ComboBoxItemStyleKey];
            window.Resources[typeof(ComboBox)].Should().BeSameAs(combo);
            window.Resources[typeof(ComboBoxItem)].Should().BeSameAs(item);
            combo.TargetType.Should().Be(typeof(ComboBox));
            combo.BasedOn.Should().NotBeNull("Fluent's style carries the template").And.NotBeSameAs(combo);
            item.BasedOn.Should().NotBeNull().And.NotBeSameAs(item);
            ValueOf(combo, ItemsControl.ItemContainerStyleProperty).Should().BeSameAs(item);
            ValueOf(item, Control.TemplateProperty).Should().BeOfType<ControlTemplate>();
        });
    }

    [Fact]
    public void Applying_again_for_a_mode_switch_bases_the_styles_on_Fluent_never_on_themselves()
    {
        Sta.Run(() =>
        {
            Window window = new();
            ToolWindowThemer.Apply(window, ToolWindowTheme.Light);

            ToolWindowThemer.Apply(window, ToolWindowTheme.Dark);

            var combo = (Style)window.Resources[ToolWindowThemer.ComboBoxStyleKey];
            Chain(combo).Should().OnlyHaveUniqueItems().And.HaveCountLessThan(5);
            Chain(combo).Skip(1).Should().NotContain(s => ValueOf(s, ItemsControl.ItemContainerStyleProperty) != null,
                "below ours lies Fluent's own style");
        });
    }

    [Fact]
    public void Layout_drop_downs_and_the_max_size_column_wear_the_same_style()
    {
        // The real editor; its owner must have been shown, so a 1×1 window far off screen (offscreen host, 4d).
        Sta.Run(() =>
        {
            Window owner = new() { Width = 1, Height = 1, Left = -3000, Top = -3000, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None };
            owner.Show();
            try
            {
                var overlay = new OverlayViewModel(FrozenShips.Load("hull_a"));
                var editor = new MissionEditorWindow(new MissionEditorViewModel(overlay), overlay, ToolWindowTheme.Dark, owner);
                var shared = (Style)editor.Resources[ToolWindowThemer.ComboBoxStyleKey];
                var sharedItem = (Style)editor.Resources[ToolWindowThemer.ComboBoxItemStyleKey];

                var layout = LogicalDescendants(editor).OfType<ComboBox>().ToList();
                var maxSize = ((DataGrid)editor.FindName("Grid")).Columns.OfType<DataGridComboBoxColumn>().Single();

                layout.Should().HaveCount(4);
                layout.Should().OnlyContain(c => c.Style == shared);
                layout.Should().OnlyContain(c => c.ItemContainerStyle.BasedOn == sharedItem);
                maxSize.EditingElementStyle.BasedOn.Should().BeSameAs(shared);
                editor.Close();
            }
            finally
            {
                owner.Close();
            }
        });
    }

    [Theory]
    [InlineData(ToolWindowTheme.Light)]
    [InlineData(ToolWindowTheme.Dark)]
    public void Picked_and_hovered_entries_keep_text_contrast_of_at_least_4_5_to_1(ToolWindowTheme theme)
    {
        // WCAG 2.x contrast; brushes with alpha are mixed over the drop-down's background first.
        Sta.Run(() =>
        {
            Window window = new();
            ToolWindowThemer.Apply(window, theme);
            var popup = Colour(window, "ComboBoxDropDownBackground");
            var text = Colour(window, "TextFillColorPrimaryBrush");

            foreach (var (name, background, foreground) in ToolWindowThemer.ComboBoxItemStates)
            {
                var back = Over(Colour(window, background), popup);
                var fore = Over(foreground is null ? text : Colour(window, foreground), back);
                Contrast(fore, back).Should().BeGreaterThanOrEqualTo(4.5, $"{name} in {theme}");
            }
        });
    }

    private static object? ValueOf(Style style, DependencyProperty property) =>
        style.Setters.OfType<Setter>().SingleOrDefault(s => s.Property == property)?.Value;

    private static IEnumerable<Style> Chain(Style style)
    {
        for (Style? s = style; s is not null; s = s.BasedOn)
        {
            yield return s;
        }
    }

    private static IEnumerable<object> LogicalDescendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            yield return child;
            if (child is DependencyObject element)
            {
                foreach (var below in LogicalDescendants(element))
                {
                    yield return below;
                }
            }
        }
    }

    private static Color Colour(FrameworkElement element, object key) => element.FindResource(key) switch
    {
        SolidColorBrush brush => Color.FromArgb((byte)(brush.Color.A * brush.Opacity), brush.Color.R, brush.Color.G, brush.Color.B),
        Color color => color,
        var other => throw new InvalidOperationException($"{key} is {other?.GetType().Name}, not a colour"),
    };

    private static Color Over(Color top, Color below)
    {
        var a = top.A / 255.0;
        byte Mix(byte t, byte b) => (byte)Math.Round(t * a + b * (1 - a));
        return Color.FromRgb(Mix(top.R, below.R), Mix(top.G, below.G), Mix(top.B, below.B));
    }

    // Source: https://www.w3.org/TR/WCAG21/#dfn-contrast-ratio, #dfn-relative-luminance
    private static double Contrast(Color a, Color b)
    {
        var (l1, l2) = (Luminance(a), Luminance(b));
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }
}
