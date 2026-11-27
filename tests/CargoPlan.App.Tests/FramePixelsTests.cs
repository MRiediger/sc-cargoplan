using CargoPlan.App.Vr;

namespace CargoPlan.App.Tests;

// T14.2, D131: what the capture thread does to every picture, on synthetic pictures. PrintWindow gives BGRA premultiplied
// on black with alpha 255 (S14.0 G2); SetOverlayRaw reads RGBA (S14.0 G1).
public class FramePixelsTests
{
    // The default window background #A0141414: 0x14 at alpha 0xA0 premultiplied is (20 · 160 + 127) / 255 = 13.
    private const uint Background = 0xA0141414;
    private const byte Premultiplied = 13;

    [Fact]
    public void The_background_within_one_per_channel_gets_its_alpha_back()
    {
        // Successor of "The_test_image_has_a_half_transparent_band…" (T14.1): the alpha now comes from the key.
        byte[] bgra =
        [
            Premultiplied, Premultiplied, Premultiplied, 255,
            Premultiplied + 1, Premultiplied - 1, Premultiplied + 1, 255,
            Premultiplied - 1, Premultiplied, Premultiplied + 1, 255,
        ];

        FramePixels.ApplyKey(bgra, Background);

        Alphas(bgra).Should().Equal(0xA0, 0xA0, 0xA0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Two_off_in_any_channel_stays_opaque(int channel)
    {
        // Anti-aliasing next to the background (Auflage 2 S14.0): ±2 is content.
        byte[] bgra = [Premultiplied, Premultiplied, Premultiplied, 255];
        bgra[channel] += 2;

        FramePixels.ApplyKey(bgra, Background);

        bgra[3].Should().Be(255);
    }

    [Fact]
    public void Content_keeps_its_colour_and_stays_opaque()
    {
        byte[] bgra = [0x10, 0xC0, 0xF0, 255];

        FramePixels.ApplyKey(bgra, Background);

        bgra.Should().Equal(0x10, 0xC0, 0xF0, 255);
    }

    [Fact]
    public void The_key_keeps_the_premultiplied_colour()
    {
        // As in G5 (S14.0): only the alpha changes.
        byte[] bgra = [Premultiplied, Premultiplied, Premultiplied, 255];

        FramePixels.ApplyKey(bgra, Background);

        bgra.Should().Equal(Premultiplied, Premultiplied, Premultiplied, 0xA0);
    }

    [Fact]
    public void The_copy_for_SteamVR_swaps_red_and_blue()
    {
        // Successor of "The_test_image_is_rgba_with_four_different_corners" (T14.1): red, green, blue, white in BGRA.
        byte[] bgra = [0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 0x80, 255, 255, 255, 255];
        var rgba = new byte[bgra.Length];

        FramePixels.CopyAsRgba(bgra, rgba);

        rgba.Should().Equal(255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 0x80, 255, 255, 255, 255);
        bgra[2].Should().Be(255, "the capture keeps its own bytes for the next comparison");
    }

    [Fact]
    public void The_corner_mask_takes_the_pixels_outside_the_rounding()
    {
        // E1 of the approval: CornerRadius 4 on a see-through window comes back black from PrintWindow. A pixel counts as
        // outside when its centre lies outside the quarter circle: (0,0), (1,0) and (0,1) per corner at radius 4.
        const int Width = 20;
        const int Height = 10;

        var mask = FramePixels.CornerMask(Width, Height, 4);

        mask.Should().BeEquivalentTo(
        [
            At(0, 0), At(1, 0), At(0, 1),
            At(19, 0), At(18, 0), At(19, 1),
            At(0, 9), At(1, 9), At(0, 8),
            At(19, 9), At(18, 9), At(19, 8),
        ]);

        static int At(int x, int y) => (y * Width) + x;
    }

    [Fact]
    public void No_rounding_gives_no_mask()
    {
        FramePixels.CornerMask(20, 10, 0).Should().BeEmpty();
    }

    [Fact]
    public void A_masked_pixel_turns_clear()
    {
        byte[] bgra = [1, 2, 3, 255, 4, 5, 6, 255];

        FramePixels.ClearCorners(bgra, [1]);

        bgra.Should().Equal(1, 2, 3, 255, 0, 0, 0, 0);
    }

    private static byte[] Alphas(byte[] bgra) => [.. bgra.Where((_, i) => i % 4 == 3)];
}
