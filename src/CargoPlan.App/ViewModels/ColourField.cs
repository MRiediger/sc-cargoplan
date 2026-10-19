using CargoPlan.App.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// One colour in the settings window: a hex field and a picker for the hue, a slider for the alpha, so transparency is
/// a slider, not two hex digits (D34). WPF-free like <see cref="Argb"/>; the window draws the swatch from
/// <see cref="Value"/>. The picker writes <see cref="Hex"/>, so typing and picking take one way to the overlay.
/// </summary>
public sealed partial class ColourField : ObservableObject
{
    public const string HexHint = "#RRGGBB, e.g. #141414. Transparency has its own slider.";

    /// <summary>The alpha slider's maximum, as double: x:Static assigns without converting, and Slider.Maximum is a double.</summary>
    public const double MaxAlpha = byte.MaxValue;

    private readonly Action _changed;

    // The picker writes the hex, the hex moves the picker: each guard stops the way back, so neither undoes the other.
    private bool _picking;
    private bool _syncing;

    /// <param name="colour">"#AARRGGBB", checked by <see cref="AppearanceRules"/>.</param>
    /// <param name="minAlpha">The slider's minimum: <see cref="AppearanceRules.MinClickableAlpha"/> for a background that takes clicks.</param>
    /// <param name="changed">Called after every change that gives a valid colour, never for an invalid hex.</param>
    public ColourField(string colour, byte minAlpha, Action changed)
    {
        MinAlpha = minAlpha;
        _changed = changed;
        Load(colour);
    }

    public byte MinAlpha { get; }

    /// <summary>As typed: "#RRGGBB" or "RRGGBB", any case. Eight digits are invalid, or field and slider would disagree.</summary>
    public string Hex
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                Update();
            }
        }
    } = "";

    /// <summary>0–255, bound to the slider; int like the crates' alphas. A binding converts the slider's double with System.Convert.</summary>
    // Source: dotnet/wpf, PresentationFramework/MS/Internal/Data/DefaultValueConverter.cs (SystemConvertConverter)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlphaPercent))]
    public partial int Alpha { get; set; }

    public string AlphaPercent => $"{Math.Round(Alpha * 100.0 / byte.MaxValue)} %";

    /// <summary>false marks the field; the overlay keeps <see cref="Value"/>, the last valid colour.</summary>
    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    /// <summary>"#AARRGGBB", the last valid colour: what the overlay shows and the swatch draws.</summary>
    public string Value
    {
        get;
        private set => SetProperty(ref field, value);
    } = "";

    /// <summary>The picker's hue in degrees, 0–360 (T4.2c). Kept on grey, where the colour itself has none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HueColour))]
    public partial double Hue { get; set; }

    /// <summary>The picker's saturation, 0–1. Kept on black, where the colour itself has none.</summary>
    [ObservableProperty]
    public partial double Saturation { get; set; }

    /// <summary>The picker's value (HSV), 0–1.</summary>
    [ObservableProperty]
    public partial double Brightness { get; set; }

    /// <summary>The hue at full saturation and brightness: the colour behind the picker's saturation/brightness pad.</summary>
    public string HueColour => new Hsv(Hue, 1, 1).ToArgb(byte.MaxValue).ToString();

    /// <summary>Shows <paramref name="colour"/>; the caller decides whether that is a change worth applying.</summary>
    public void Load(string colour)
    {
        var argb = Argb.Parse(colour);
        Hex = $"#{argb.R:X2}{argb.G:X2}{argb.B:X2}";
        Alpha = argb.A;
    }

    partial void OnAlphaChanged(int value) => Update();

    partial void OnHueChanged(double value) => Pick();

    partial void OnSaturationChanged(double value) => Pick();

    partial void OnBrightnessChanged(double value) => Pick();

    /// <summary>A picker change: written as hex, which heals an invalid field and applies like typing.</summary>
    private void Pick()
    {
        if (_syncing)
        {
            return;
        }

        var rgb = new Hsv(Hue, Saturation, Brightness).ToArgb(byte.MaxValue);
        _picking = true;
        Hex = $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}";
        _picking = false;
    }

    /// <summary>A typed or loaded colour moves the picker; a hue or saturation the colour doesn't have stays (grey, black).</summary>
    private void SyncPicker(Argb rgb)
    {
        var hsv = Hsv.FromRgb(rgb);
        _syncing = true;
        if (hsv.Saturation > 0)
        {
            Hue = hsv.Hue;
        }

        if (hsv.Value > 0)
        {
            Saturation = hsv.Saturation;
        }

        Brightness = hsv.Value;
        _syncing = false;
    }

    private void Update()
    {
        IsValid = TryParseHex(Hex, out var rgb);
        if (!IsValid)
        {
            return;
        }

        if (!_picking)
        {
            SyncPicker(rgb);
        }

        Value = (rgb with { A = (byte)Alpha }).ToString();
        _changed();
    }

    // Same digits as settings.json (Argb.TryParse), the '#' optional because people type colours without it.
    private static bool TryParseHex(string text, out Argb rgb)
    {
        rgb = default;
        var digits = text.StartsWith('#') ? text[1..] : text;
        return digits.Length == 6 && Argb.TryParse('#' + digits, out rgb);
    }
}
