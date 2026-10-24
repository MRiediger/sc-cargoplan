using CargoPlan.App.ViewModels;

namespace CargoPlan.App.Tests;

/// <summary>The picker part of <see cref="ColourField"/> (T4.2c); hex and alpha are covered in <see cref="SettingsViewModelTests"/>.</summary>
public sealed class ColourFieldTests
{
    private int _changes;

    private ColourField Create(string colour = "#A0FF0000") => new(colour, minAlpha: 0, () => _changes++);

    [Fact]
    public void A_field_without_alpha_is_always_opaque()
    {
        // The hologram's colours (D63): WPF 3D sorts no transparent triangles, its opacity has a slider of its own.
        var field = new ColourField("#809CB0C0", minAlpha: 0, () => _changes++, hasAlpha: false);

        field.HasAlpha.Should().BeFalse();
        field.Alpha.Should().Be(255);
        field.Value.Should().Be("#FF9CB0C0");

        field.Hex = "#112233";

        field.Value.Should().Be("#FF112233");
    }

    [Fact]
    public void Opening_shows_the_colour_in_the_picker()
    {
        var field = Create("#FF00FF00");

        field.Hue.Should().Be(120);
        field.Saturation.Should().Be(1);
        field.Brightness.Should().Be(1);
        field.HueColour.Should().Be("#FF00FF00");
    }

    [Fact]
    public void Picking_writes_the_hex_and_keeps_the_alpha()
    {
        var field = Create();
        _changes = 0;

        field.Brightness = 0.5;

        field.Hex.Should().Be("#800000");
        field.Value.Should().Be("#A0800000");
        _changes.Should().Be(1);
    }

    [Fact]
    public void Picking_the_hue_changes_the_colour()
    {
        var field = Create();
        _changes = 0;

        field.Hue = 240;

        field.Value.Should().Be("#A00000FF");
        field.HueColour.Should().Be("#FF0000FF");
        _changes.Should().Be(1);
    }

    [Fact]
    public void Typing_a_hex_moves_the_picker()
    {
        var field = Create();

        field.Hex = "#0000FF";

        field.Hue.Should().Be(240);
        field.Saturation.Should().Be(1);
        field.Brightness.Should().Be(1);
    }

    [Fact]
    public void Grey_keeps_the_hue_so_the_hue_slider_doesnt_jump()
    {
        var field = Create();
        field.Hue = 200;

        field.Saturation = 0;

        field.Hue.Should().Be(200);
        field.Hex.Should().Be("#FFFFFF");
    }

    [Fact]
    public void Black_keeps_hue_and_saturation()
    {
        var field = Create();
        field.Hue = 200;
        field.Saturation = 0.5;

        field.Brightness = 0;

        field.Hue.Should().Be(200);
        field.Saturation.Should().Be(0.5);
        field.Hex.Should().Be("#000000");
    }

    [Fact]
    public void Hue_on_grey_changes_no_colour()
    {
        var field = Create("#FF808080");
        _changes = 0;

        field.Hue = 90;

        field.Value.Should().Be("#FF808080");
        _changes.Should().Be(0);
    }

    [Fact]
    public void Picking_heals_an_invalid_hex()
    {
        var field = Create();
        field.Hex = "nope";
        _changes = 0;

        field.Saturation = 0.5;

        field.IsValid.Should().BeTrue();
        field.Hex.Should().Be("#FF8080");
        _changes.Should().Be(1);
    }

    [Fact]
    public void Invalid_hex_leaves_the_picker_where_it_was()
    {
        var field = Create();

        field.Hex = "#00";

        field.Hue.Should().Be(0);
        field.Saturation.Should().Be(1);
        field.Brightness.Should().Be(1);
    }

    [Fact]
    public void Load_moves_the_picker()
    {
        var field = Create();

        field.Load("#FF0000FF");

        field.Hue.Should().Be(240);
    }
}
