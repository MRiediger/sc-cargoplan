namespace CargoPlan.Ocr.Tests;

/// <summary>Loads the models once for all engine tests (≈ 0.2 s), as the app will once per process.</summary>
public sealed class EngineFixture : IDisposable
{
    public OcrEngine Engine { get; } = OcrEngine.Load();

    public void Dispose() => Engine.Dispose();
}

/// <summary>
/// One engine for every class that reads, and those classes one after the other: a read uses 8 recognizer threads plus
/// ONNX Runtime's own, and two classes reading in parallel doubled each test's time (≈ 2 s instead of ≈ 0.6 s).
/// Source: https://xunit.net/docs/shared-context#collection-fixture
/// </summary>
[CollectionDefinition(Name)]
public sealed class EngineCollection : ICollectionFixture<EngineFixture>
{
    public const string Name = "OCR engine";
}

/// <summary>
/// Loading from a folder without models (T10.1b, K1): outside the engine collection on purpose, it touches no shared
/// state, so it may run in parallel with the classes that read.
/// </summary>
public sealed class OcrEngineLoadTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("cargoplan-ocr-").FullName;

    [Fact]
    public void A_folder_without_models_names_the_first_missing_file()
    {
        var load = () => OcrEngine.Load(_folder);

        load.Should().Throw<FileNotFoundException>()
            .Which.FileName.Should().Be(Path.Combine(_folder, "ch_PP-OCRv5_mobile_det.onnx"));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}

// The text is drawn with SkiaSharp's default typeface, so the asserts look for key words, case-insensitive, not for the
// exact sentence (K4). If this turns flaky, it becomes Slow; the assert isn't loosened.
[Collection(EngineCollection.Name)]
public sealed class OcrEngineTests(EngineFixture fixture)
{
    private static readonly (string, float, float)[] Contract =
        [("PRIMARY OBJECTIVES", 20, 20), ("Deliver 0/8 SCU to Riker Memorial Spaceport", 20, 80)];

    [Fact]
    public void It_reads_a_colour_picture()
    {
        using var picture = TextImages.Draw(900, 140, Contract);

        var text = Joined(fixture.Engine.Read(picture));

        text.Should().ContainEquivalentOf("PRIMARY OBJECTIVES").And.ContainEquivalentOf("SCU").And.ContainEquivalentOf("Riker");
    }

    // The proof that RapidOcrNet takes the Gray8 bitmap of up2-gray as it is.
    [Fact]
    public void It_reads_the_up2gray_picture()
    {
        using var picture = TextImages.Draw(900, 140, Contract);
        using var gray = Preprocessing.Up2Gray(picture);

        var text = Joined(fixture.Engine.Read(gray));

        text.Should().ContainEquivalentOf("PRIMARY OBJECTIVES").And.ContainEquivalentOf("SCU").And.ContainEquivalentOf("Riker");
    }

    [Fact]
    public void Each_block_has_a_box_around_its_text()
    {
        using var picture = TextImages.Draw(900, 140, Contract);

        var heading = fixture.Engine.Read(picture).Single(b => b.Text.Contains("OBJECTIVES", StringComparison.OrdinalIgnoreCase));

        heading.Left.Should().BeInRange(0, 40);
        heading.Top.Should().BeInRange(0, 40);
        heading.Bottom.Should().BeInRange(40, 80);
        heading.Right.Should().BeGreaterThan(heading.Left);
    }

    [Fact]
    public void An_empty_picture_has_no_blocks()
    {
        using var picture = TextImages.Draw(400, 100);

        fixture.Engine.Read(picture).Should().BeEmpty();
    }

    private static string Joined(IReadOnlyList<TextBox> blocks) => string.Join('\n', blocks.Select(b => b.Text));
}
