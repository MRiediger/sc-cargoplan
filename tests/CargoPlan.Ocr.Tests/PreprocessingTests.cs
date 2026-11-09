using SkiaSharp;

namespace CargoPlan.Ocr.Tests;

public sealed class PreprocessingTests
{
    [Fact]
    public void Up2gray_doubles_the_size_and_keeps_one_channel()
    {
        using var source = Filled(100, 50, SKColors.White);

        using var result = Preprocessing.Up2Gray(source);

        result.Width.Should().Be(200);
        result.Height.Should().Be(100);
        result.ColorType.Should().Be(SKColorType.Gray8);
        result.BytesPerPixel.Should().Be(1);
    }

    [Fact]
    public void White_stays_white_and_black_stays_black()
    {
        using var source = Filled(4, 2, SKColors.White);
        source.SetPixel(0, 0, SKColors.Black);
        source.SetPixel(1, 0, SKColors.Black);
        source.SetPixel(0, 1, SKColors.Black);
        source.SetPixel(1, 1, SKColors.Black);

        using var result = Preprocessing.Up2Gray(source);

        Gray(result, 0, 0).Should().Be(0);
        Gray(result, 7, 3).Should().Be(255);
    }

    // Luma weights green most and blue least; red lies between (Rec. 601: 76, Rec. 709: 54). Which of the two Skia uses
    // isn't the point, only that colour becomes brightness and not one channel.
    [Fact]
    public void Colour_becomes_its_brightness()
    {
        using var red = Filled(2, 2, SKColors.Red);
        using var green = Filled(2, 2, new SKColor(0, 255, 0));
        using var blue = Filled(2, 2, SKColors.Blue);

        using var redGray = Preprocessing.Up2Gray(red);
        using var greenGray = Preprocessing.Up2Gray(green);
        using var blueGray = Preprocessing.Up2Gray(blue);

        Gray(redGray, 1, 1).Should().BeInRange(40, 90);
        Gray(greenGray, 1, 1).Should().BeGreaterThan(Gray(redGray, 1, 1));
        Gray(blueGray, 1, 1).Should().BeLessThan(Gray(redGray, 1, 1));
    }

    private static SKBitmap Filled(int width, int height, SKColor colour)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        bitmap.Erase(colour);
        return bitmap;
    }

    private static byte Gray(SKBitmap bitmap, int x, int y) => bitmap.GetPixelSpan()[y * bitmap.RowBytes + x];
}
