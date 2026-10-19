using CargoPlan.App.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CargoPlan.App.ViewModels;

/// <summary>A section's halo in the settings window (D28): switch, colour, size. Off keeps colour and size, as in the file.</summary>
public sealed partial class HaloEditor : ObservableObject
{
    private readonly Action _changed;

    /// <param name="changed">Called after every change; the owner ignores the calls while it loads values.</param>
    public HaloEditor(HaloSettings halo, Action changed)
    {
        _changed = changed;
        Colour = new ColourField(halo.Color, minAlpha: 0, changed);
        Load(halo);
    }

    public ColourField Colour { get; }

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    /// <summary>Between <see cref="AppearanceRules.MinHaloSize"/> and <see cref="AppearanceRules.MaxHaloSize"/>, the slider's limits.</summary>
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
