using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CargoPlan.App.Settings;

namespace CargoPlan.App;

/// <summary>
/// The colour picker's square (T4.2c): saturation from left (grey) to right (full colour), brightness from bottom (black)
/// to top, in the colour of <see cref="Hue"/>. Click or drag picks. The mapping is pure (<see cref="At"/>), the control
/// only paints and captures the mouse.
/// </summary>
public sealed class SaturationValuePad : FrameworkElement
{
    // FrameworkPropertyMetadataOptions: two-way by default, so the picker writes back like a slider; AffectsRender redraws.
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.frameworkpropertymetadataoptions
    public static readonly DependencyProperty HueProperty = DependencyProperty.Register(
        nameof(Hue), typeof(double), typeof(SaturationValuePad),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SaturationProperty = DependencyProperty.Register(
        nameof(Saturation), typeof(double), typeof(SaturationValuePad),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty BrightnessProperty = DependencyProperty.Register(
        nameof(Brightness), typeof(double), typeof(SaturationValuePad),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>Degrees around the colour wheel, red at 0 and 360: the hue slider's background, built from <see cref="Hsv"/>.</summary>
    public static Brush HueSpectrum { get; } = CreateHueSpectrum();

    private const double MarkerRadius = 5;

    // The pad's own definition: full colour fades to white on the left and to black at the bottom (HSV), not a theme.
    // Each gradient fades within one hue (Transparent is transparent white): a fade from transparent white to black
    // would pass through grey.
    private static readonly Brush ToWhite = Frozen(new LinearGradientBrush(Colors.White, Colors.Transparent, 0));
    private static readonly Brush ToBlack = Frozen(new LinearGradientBrush(Colors.Black with { A = 0 }, Colors.Black, 90));

    // A white ring inside a black one stays visible on any colour of the pad.
    private static readonly Pen MarkerInner = Frozen(new Pen(Brushes.White, 2));
    private static readonly Pen MarkerOuter = Frozen(new Pen(Brushes.Black, 1));

    public double Hue
    {
        get => (double)GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public double Saturation
    {
        get => (double)GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    public double Brightness
    {
        get => (double)GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    /// <summary>Saturation and brightness at <paramref name="point"/>; a drag beyond the pad stops at its edge.</summary>
    public static (double Saturation, double Brightness) At(Point point, Size size) =>
        (Math.Clamp(point.X / size.Width, 0, 1), Math.Clamp(1 - point.Y / size.Height, 0, 1));

    /// <summary>Where the marker of <paramref name="saturation"/> and <paramref name="brightness"/> sits.</summary>
    public static Point PositionOf(double saturation, double brightness, Size size) =>
        new(saturation * size.Width, (1 - brightness) * size.Height);

    protected override void OnRender(DrawingContext drawingContext)
    {
        Rect area = new(RenderSize);
        var hue = new Hsv(Hue, 1, 1).ToArgb(byte.MaxValue);
        drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(hue.R, hue.G, hue.B)), null, area);
        drawingContext.DrawRectangle(ToWhite, null, area);
        drawingContext.DrawRectangle(ToBlack, null, area);

        var marker = PositionOf(Saturation, Brightness, RenderSize);
        drawingContext.DrawEllipse(null, MarkerOuter, marker, MarkerRadius + 1, MarkerRadius + 1);
        drawingContext.DrawEllipse(null, MarkerInner, marker, MarkerRadius, MarkerRadius);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        CaptureMouse();
        PickAt(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsMouseCaptured)
        {
            PickAt(e.GetPosition(this));
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
    }

    /// <summary>
    /// SetCurrentValue instead of SetValue: it keeps the binding and writes through it to the view model.
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.dependencyobject.setcurrentvalue
    /// </summary>
    private void PickAt(Point point)
    {
        var (saturation, brightness) = At(point, RenderSize);
        SetCurrentValue(SaturationProperty, saturation);
        SetCurrentValue(BrightnessProperty, brightness);
    }

    private static LinearGradientBrush CreateHueSpectrum()
    {
        const int Steps = 6;
        GradientStopCollection stops = [];
        for (var i = 0; i <= Steps; i++)
        {
            var colour = new Hsv(360.0 * i / Steps, 1, 1).ToArgb(byte.MaxValue);
            stops.Add(new GradientStop(Color.FromRgb(colour.R, colour.G, colour.B), (double)i / Steps));
        }

        return Frozen(new LinearGradientBrush(stops, 0));
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
