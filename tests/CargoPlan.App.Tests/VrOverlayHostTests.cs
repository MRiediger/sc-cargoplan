using CargoPlan.App.Vr;

namespace CargoPlan.App.Tests;

public class VrOverlayHostTests
{
    [Fact]
    public void The_test_image_is_rgba_with_four_different_corners()
    {
        // SetOverlayRaw reads RGBA (S14.0 G1: BGRA showed red and blue swapped). Each corner a colour of its own, so a
        // mirrored, flipped or swapped picture shows in the headset.
        var (rgba, width, height) = VrOverlayHost.TestImage();

        (width, height).Should().Be((512, 256));
        rgba.Should().HaveCount(width * height * 4);
        Pixel(rgba, width, 0, 0).Should().Equal(255, 0, 0, 255);
        Pixel(rgba, width, width - 1, 0).Should().Equal(0, 255, 0, 255);
        Pixel(rgba, width, 0, height - 1).Should().Equal(0, 0, 255, 255);
        Pixel(rgba, width, width - 1, height - 1).Should().Equal(255, 255, 255, 255);
    }

    [Fact]
    public void The_test_image_has_a_half_transparent_band_and_is_clear_elsewhere()
    {
        var (rgba, width, height) = VrOverlayHost.TestImage();

        Pixel(rgba, width, width / 4, height / 2)[3].Should().Be(0x80);
        Pixel(rgba, width, width / 2, 8).Should().Equal(0, 0, 0, 0);
    }

    private static byte[] Pixel(byte[] rgba, int width, int x, int y) => rgba.AsSpan(((y * width) + x) * 4, 4).ToArray();
}
