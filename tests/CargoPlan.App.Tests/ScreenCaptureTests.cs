using CargoPlan.App.Win32;
using CargoPlan.Ocr;
using SkiaSharp;

namespace CargoPlan.App.Tests;

/// <summary>
/// The GDI object count is the whole process's: tests that show WPF windows in parallel would move it. This collection
/// runs alone, after the parallel ones.
/// Source: https://xunit.net/docs/running-tests-in-parallel ("Opting a test collection out of parallelism")
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GdiCountCollection
{
    public const string Name = "GDI object count";
}

// No window needed: the leak check counts handles, not pixels. Without a desktop (CI, a locked session) BitBlt fails;
// that is an outcome, and the handles must be freed all the same (H3). Content is checked by hand (OverlayCaptureTests).
[Collection(GdiCountCollection.Name)]
public class ScreenCaptureTests
{
    private static readonly PixelRect Area = new(0, 0, 320, 200);

    [Fact]
    public void A_hundred_captures_leave_the_gdi_object_count_as_it_was()
    {
        // One first: GDI may create its per-process objects on the first call.
        Capture();
        var before = NativeMethods.GdiObjectCount();

        for (var i = 0; i < 100; i++)
        {
            Capture();
        }

        NativeMethods.GdiObjectCount().Should().Be(before);
    }

    [Fact]
    public void A_capture_is_a_bgra_bitmap_of_the_region_or_a_failure_with_its_error()
    {
        using var captured = ScreenCapture.Region(Area, out var error);

        if (captured is null)
        {
            error.Should().NotBe(0, "a failed BitBlt names its error");
            return;
        }

        error.Should().Be(0);
        captured.Bitmap.Width.Should().Be(Area.Width);
        captured.Bitmap.Height.Should().Be(Area.Height);
        captured.Bitmap.ColorType.Should().Be(SKColorType.Bgra8888);
    }

    [Fact]
    public void An_empty_region_is_a_caller_error() =>
        ((Action)(() => ScreenCapture.Region(Area with { Width = 0 }, out _))).Should().Throw<ArgumentOutOfRangeException>();

    private static void Capture()
    {
        using var captured = ScreenCapture.Region(Area, out _);
    }
}
