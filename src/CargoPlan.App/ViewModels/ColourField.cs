using CargoPlan.App.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// One colour in the settings window: a hex field for the hue and a slider for the alpha, so transparency is a slider,
/// not two hex digits (D34). WPF-free like <see cref="Argb"/>; the window draws the swatch from <see cref="Value"/>.
/// </summary>
public sealed partial class ColourField : ObservableObject
{
    public const string HexHint = "#RRGGBB, e.g. #141414. Transparency has its own slider.";

    /// <summary>The alpha slider's maximum, as double: x:Static assigns without converting, and Slider.Maximum is a double.</summary>
    public const double MaxAlpha = byte.MaxValue;

    private readonly Action _changed;

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

    /// <summary>Shows <paramref name="colour"/>; the caller decides whether that is a change worth applying.</summary>
    public void Load(string colour)
    {
        var argb = Argb.Parse(colour);
        Hex = $"#{argb.R:X2}{argb.G:X2}{argb.B:X2}";
        Alpha = argb.A;
    }

    partial void OnAlphaChanged(int value) => Update();

    private void Update()
    {
        IsValid = TryParseHex(Hex, out var rgb);
        if (!IsValid)
        {
            return;
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
