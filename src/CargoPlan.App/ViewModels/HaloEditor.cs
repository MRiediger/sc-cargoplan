using CargoPlan.App.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CargoPlan.App.ViewModels;

/// <summary>A section's halo in the settings window (D28): switch, colour, size. Off keeps colour and size, as in the file.</summary>
public sealed partial class HaloEditor : ObservableObject
{
    private readonly Action _changed;

    /// <param name="label">What the halo surrounds; for the rack numbers it is a sharp outline, not a glow (D31).</param>
    /// <param name="changed">Called after every change; the owner ignores the calls while it loads values.</param>
    public HaloEditor(HaloSettings halo, string label, Action changed)
    {
        Label = label;
        _changed = changed;
        Colour = new ColourField(halo.Color, minAlpha: 0, changed);
        Load(halo);
    }

    /// <summary>The switch's text in the window.</summary>
    public string Label { get; }

    public ColourField Colour { get; }

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    /// <summary>
    /// Between <see cref="AppearanceRules.MinHaloSize"/> and <see cref="AppearanceRules.MaxHaloSize"/>, the slider's limits.
    /// Blur radius for text, stroke width for the rack numbers.
    /// </summary>
    [ObservableProperty]
    public partial double Size { get; set; }

    public HaloSettings Value => new(Enabled, Colour.Value, Size);

    public void Load(HaloSettings halo)
    {
        Enabled = halo.Enabled;
        Size = halo.Size;
        Colour.Load(halo.Color);
    }

    partial void OnEnabledChanged(bool value) => _changed();

    partial void OnSizeChanged(double value) => _changed();
}
