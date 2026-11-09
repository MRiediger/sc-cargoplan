using RapidOcrNet;
using SkiaSharp;

namespace CargoPlan.Ocr;

/// <summary>
/// RapidOcrNet with the PP-OCRv5 latin models (D99). Loading takes ≈ 0.2–0.4 s (S10.0), so a process holds one engine.
/// One read at a time: a second caller waits.
/// </summary>
public sealed class OcrEngine : IDisposable
{
    /// <summary>Where the models lie next to the exe (copied from the RapidOcrNet package, see the csproj).</summary>
    public static readonly string ModelFolder = Path.Combine(AppContext.BaseDirectory, "models", "v5");

    private const string DetectorFile = "ch_PP-OCRv5_mobile_det.onnx";
    private const string ClassifierFile = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx";
    private const string RecognizerFile = "latin_PP-OCRv5_rec_mobile_infer.onnx";
    private const string KeysFile = "ppocrv5_latin_dict.txt";

    // S10.0: parallelism 8 brought the region's offline p95 from 1652 to 1332 ms; the default is 1 (serial). The rest is
    // the Default preset, which the spike used for the region (1024 px long side is enough there, unlike the full frame).
    // Source: https://github.com/BobLd/RapidOcrNet/blob/master/RapidOcrNet/RapidOcrOptions.cs
    private static readonly RapidOcrOptions Options = RapidOcrOptions.Default with { RecMaxDegreeOfParallelism = 8 };

    private readonly RapidOcr _ocr = new();
    private readonly Lock _gate = new();

    private OcrEngine(string folder)
    {
        // Explicit paths: the parameterless InitModels() resolves "models/v5/…" against the working directory, which for
        // the Cli is wherever the user stands. Source: https://github.com/BobLd/RapidOcrNet/blob/master/RapidOcrNet/RapidOcr.cs
        _ocr.InitModels(
            Path.Combine(folder, DetectorFile),
            Path.Combine(folder, ClassifierFile),
            Path.Combine(folder, RecognizerFile),
            Path.Combine(folder, KeysFile));
    }

    /// <summary>Loads the models from <see cref="ModelFolder"/>; a missing file is named (a broken install).</summary>
    public static OcrEngine Load()
    {
        foreach (var file in (string[])[DetectorFile, ClassifierFile, RecognizerFile, KeysFile])
        {
            var path = Path.Combine(ModelFolder, file);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"The OCR model {file} is missing next to the exe (models/v5).", path);
            }
        }

        return new OcrEngine(ModelFolder);
    }

    /// <summary>Reads a small drawn line once, so the first real read doesn't pay for the runtime's first run.</summary>
    public void Warmup()
    {
        using var picture = new SKBitmap(new SKImageInfo(320, 48, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(picture);
        canvas.Clear(SKColors.White);
        using var font = new SKFont(SKTypeface.Default, 24);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawText("Deliver 0/8 SCU", 8, 32, font, paint);
        Read(picture);
    }

    /// <summary>The blocks in <paramref name="picture"/> (Bgra8888, Rgba8888 or Gray8), in the order the OCR found them.</summary>
    public IReadOnlyList<TextBox> Read(SKBitmap picture)
    {
        OcrResult result;
        lock (_gate)
        {
            // BoxPoints: four corners in source-picture pixels. Source: https://github.com/BobLd/RapidOcrNet (README,
            // "Drawing the boxes back onto the image"), OcrResult.cs.
            result = _ocr.Detect(picture, Options);
        }

        return [.. result.TextBlocks.Select(block => new TextBox(
            block.Text,
            block.BoxPoints.Min(p => p.X),
            block.BoxPoints.Min(p => p.Y),
            block.BoxPoints.Max(p => p.X),
            block.BoxPoints.Max(p => p.Y)))];
    }

    public void Dispose() => _ocr.Dispose();
}
