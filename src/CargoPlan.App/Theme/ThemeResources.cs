using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;

namespace CargoPlan.App.Theme;

/// <summary>
/// Turns <see cref="AppearanceSettings"/> into the resources the views bind with DynamicResource (D31). Called once at
/// startup before the windows exist; calling it again (settings window, T4.2) changes the overlay at once, because
/// DynamicResource follows a replaced resource. The only place with theme defaults is the settings model.
/// </summary>
public static class ThemeResources
{
    /// <summary>
    /// The contract between settings and views: &lt;Section&gt;&lt;Part&gt;&lt;Kind&gt;, the section as in settings.json.
    /// XAML uses the same strings.
    /// </summary>
    public static class Keys
    {
        public const string WindowBackgroundBrush = nameof(WindowBackgroundBrush);
        public const string WindowBorderBrush = nameof(WindowBorderBrush);
        public const string WindowBorderThickness = nameof(WindowBorderThickness);
        public const string HeaderForegroundBrush = nameof(HeaderForegroundBrush);
        public const string HeaderHaloEffect = nameof(HeaderHaloEffect);
        public const string LegendForegroundBrush = nameof(LegendForegroundBrush);
        public const string LegendFontSize = nameof(LegendFontSize);
        public const string LegendHaloEffect = nameof(LegendHaloEffect);
        public const string CratesFillAlpha = nameof(CratesFillAlpha);
        public const string CratesEdgeBrush = nameof(CratesEdgeBrush);
        public const string CratesRackNumberBrush = nameof(CratesRackNumberBrush);
        public const string CratesHaloBrush = nameof(CratesHaloBrush);
        public const string CratesHaloSize = nameof(CratesHaloSize);
        public const string ButtonsBackgroundBrush = nameof(ButtonsBackgroundBrush);
        public const string ButtonsBorderBrush = nameof(ButtonsBorderBrush);
        public const string ButtonsBorderThickness = nameof(ButtonsBorderThickness);
        public const string ButtonsCornerRadius = nameof(ButtonsCornerRadius);
        public const string ButtonsHaloEffect = nameof(ButtonsHaloEffect);

        public static IReadOnlyList<string> All { get; } =
        [
            WindowBackgroundBrush, WindowBorderBrush, WindowBorderThickness,
            HeaderForegroundBrush, HeaderHaloEffect,
            LegendForegroundBrush, LegendFontSize, LegendHaloEffect,
            CratesFillAlpha, CratesEdgeBrush, CratesRackNumberBrush, CratesHaloBrush, CratesHaloSize,
            ButtonsBackgroundBrush, ButtonsBorderBrush, ButtonsBorderThickness, ButtonsCornerRadius, ButtonsHaloEffect,
        ];
    }

    /// <param name="appearance">Checked by <see cref="AppearanceRules.Normalize"/>; an invalid colour here is a bug.</param>
    public static void Apply(ResourceDictionary resources, AppearanceSettings appearance)
    {
        var window = appearance.Window;
        resources[Keys.WindowBackgroundBrush] = Brush(window.Background);
        resources[Keys.WindowBorderBrush] = Brush(window.BorderColor);
        resources[Keys.WindowBorderThickness] = new Thickness(window.BorderThickness);

        resources[Keys.HeaderForegroundBrush] = Brush(appearance.Header.Foreground);
        Set(resources, Keys.HeaderHaloEffect, Glow(appearance.Header.Halo));

        var legend = appearance.Legend;
        resources[Keys.LegendForegroundBrush] = Brush(legend.Foreground);

        // null = what TextBlocks got before settings existed: the window's font size, which WPF takes from the Windows
        // message font (11 on the development machine, not the often assumed 12).
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.systemfonts.messagefontsize
        resources[Keys.LegendFontSize] = legend.FontSize ?? SystemFonts.MessageFontSize;
        Set(resources, Keys.LegendHaloEffect, Glow(legend.Halo));

        var crates = appearance.Crates;
        resources[Keys.CratesFillAlpha] = (byte)crates.FillAlpha;

        // The hue of the crate edges is the rack edges'; only the alpha is a setting (D29: crate colours stay fixed).
        resources[Keys.CratesEdgeBrush] = Frozen(new SolidColorBrush(MissionPalette.RackEdge with { A = (byte)crates.EdgeAlpha }));
        resources[Keys.CratesRackNumberBrush] = Brush(crates.RackNumberColor);

        // The rack numbers keep their sharp outline (D15); the halo gives its colour and width.
        Set(resources, Keys.CratesHaloBrush, crates.Halo.Enabled ? Brush(crates.Halo.Color) : null);
        resources[Keys.CratesHaloSize] = crates.Halo.Size;

        var buttons = appearance.Buttons;
        resources[Keys.ButtonsBackgroundBrush] = Brush(buttons.Background);
        resources[Keys.ButtonsBorderBrush] = Brush(buttons.BorderColor);
        resources[Keys.ButtonsBorderThickness] = new Thickness(buttons.BorderThickness);
        resources[Keys.ButtonsCornerRadius] = new CornerRadius(buttons.CornerRadius);
        Set(resources, Keys.ButtonsHaloEffect, Glow(buttons.Halo));
    }

    /// <summary>
    /// null means "off" and removes the key. Assigning null through the indexer does not deliver null to a
    /// DynamicResource: the property falls back to its default (spike T4.1, Foreground went back to black). Removing says
    /// the same openly; every property bound to such a key (Effect, ShipViewControl.RackNumberHaloBrush) defaults to null.
    /// </summary>
    private static void Set(ResourceDictionary resources, string key, object? value)
    {
        if (value is null)
        {
            resources.Remove(key);
        }
        else
        {
            resources[key] = value;
        }
    }

    /// <summary>
    /// A soft halo (D28): a shadow without offset blurs evenly around the glyphs. Frozen, so one instance serves every
    /// element of its section (spike T4.1: a shared frozen effect works on several elements).
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.media.effects.dropshadoweffect
    /// </summary>
    private static DropShadowEffect? Glow(HaloSettings halo)
    {
        if (!halo.Enabled)
        {
            return null;
        }

        var colour = Argb.Parse(halo.Color);
        return Frozen(new DropShadowEffect
        {
            ShadowDepth = 0,
            BlurRadius = halo.Size,
            Color = Color.FromRgb(colour.R, colour.G, colour.B),
            Opacity = colour.A / (double)byte.MaxValue,
        });
    }

    private static SolidColorBrush Brush(string text)
    {
        var colour = Argb.Parse(text);
        return Frozen(new SolidColorBrush(Color.FromArgb(colour.A, colour.R, colour.G, colour.B)));
    }

    // Frozen resources are read-only and shared without copies. Source: https://learn.microsoft.com/dotnet/desktop/wpf/advanced/freezable-objects-overview
    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
