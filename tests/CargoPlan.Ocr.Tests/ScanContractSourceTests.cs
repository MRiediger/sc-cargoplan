using SkiaSharp;

namespace CargoPlan.Ocr.Tests;

// A 960×540 window, region [322, 78, 585, 375]: a quarter of 1080p's pixels keeps a read under the default run's ≈ 1.3 s
// (a full 1080p frame took ≈ 1.5 s per test). Text at 14 px is 28 px after up2-gray. Asserts look for key words in
// order, not exact sentences: the text is drawn with SkiaSharp's default typeface (K4).
[Collection(EngineCollection.Name)]
public sealed class ScanContractSourceTests(EngineFixture fixture)
{
    private const int Width = 960;
    private const int Height = 540;
    private const float TextSize = 14;

    [Fact]
    public void A_contract_in_the_region_comes_out_in_column_order()
    {
        using var frame = TextImages.Draw(Width, Height, TextSize,
            ("FPS 144", 10, 10),
            ("PRIMARY OBJECTIVES", 625, 100),
            ("DETAILS", 340, 100),
            ("Deliver 0/8 SCU to Riker", 625, 130),
            ("Covalex needs a hauler.", 340, 130),
            ("Collect from Everus Harbor.", 340, 160));

        var result = new ScanContractSource(fixture.Engine).Read(frame);

        result.Outcome.Should().Be(ScanOutcome.Read);
        var lines = result.RawText.Split('\n');
        int[] order = [.. new[] { "DETAILS", "hauler", "Everus", "PRIMARY OBJECTIVES", "Riker" }.Select(word => IndexOf(lines, word))];
        order.Should().NotContain(-1).And.BeInAscendingOrder();
        result.RawText.Should().NotContainEquivalentOf("FPS", "the FPS counter lies outside the region");
    }

    [Fact]
    public void Without_a_contract_the_frame_says_so()
    {
        using var frame = TextImages.Draw(Width, Height, TextSize, ("Quantum travel ready", 350, 150));

        var result = new ScanContractSource(fixture.Engine).Read(frame);

        result.Outcome.Should().Be(ScanOutcome.NoContractInFrame);
        result.RawText.Should().BeEmpty();
        result.Timings.Ocr.Should().BePositive();
        result.Timings.Total.Should().Be(result.Timings.Region + result.Timings.Preprocess + result.Timings.Ocr + result.Timings.Lines);
    }

    // The App captures only the region (T10.1b): its own bitmap, not a subset of a frame, read without a second cut.
    [Fact]
    public void A_region_captured_on_its_own_reads_like_the_frame_it_was_cut_from()
    {
        using var frame = TextImages.Draw(Width, Height, TextSize,
            ("PRIMARY OBJECTIVES", 625, 100),
            ("DETAILS", 340, 100),
            ("Deliver 0/8 SCU to Riker", 625, 130));
        var region = ScanRegion.Of(Width, Height);
        using var captured = new SKBitmap(new SKImageInfo(region.Width, region.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        frame.ExtractSubset(captured, SKRectI.Create(region.X, region.Y, region.Width, region.Height)).Should().BeTrue();
        using var own = captured.Copy();
        var source = new ScanContractSource(fixture.Engine);

        var fromRegion = source.ReadRegion(own);

        fromRegion.Outcome.Should().Be(ScanOutcome.Read);
        fromRegion.RawText.Should().Be(source.Read(frame).RawText);
        fromRegion.Timings.Region.Should().Be(TimeSpan.Zero, "nothing was cut");
    }

    private static int IndexOf(string[] lines, string word) =>
        Array.FindIndex(lines, line => line.Contains(word, StringComparison.OrdinalIgnoreCase));
}
