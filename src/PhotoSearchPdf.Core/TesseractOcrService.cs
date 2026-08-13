using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Pix;

namespace PhotoSearchPdf.Core;

public sealed class TesseractOcrService : IDisposable
{
    private readonly string _dataPath;
    private readonly Dictionary<string, Engine> _engines = new(StringComparer.OrdinalIgnoreCase);

    public TesseractOcrService(string dataPath)
    {
        _dataPath = Path.GetFullPath(dataPath);
        if (!Directory.Exists(_dataPath))
        {
            throw new DirectoryNotFoundException($"Tesseract language data folder does not exist: {_dataPath}");
        }
    }

    public OcrPage Recognize(
        string imagePath,
        int pageNumber,
        string language,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = Image.LoadFromFile(imagePath);
        using var page = GetEngine(language).Process(image);
        var lines = new List<OcrLine>();

        foreach (var block in page.Layout)
        {
            foreach (var paragraph in block.Paragraphs)
            {
                foreach (var textLine in paragraph.TextLines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var text = textLine.Text?.Trim();
                    var bounds = textLine.BoundingBox;
                    if (string.IsNullOrWhiteSpace(text) || bounds is null) continue;

                    var box = bounds.Value;
                    lines.Add(new OcrLine(text, new OcrBox(
                        Clamp01(box.X1 / (double)image.Width),
                        Clamp01(box.Y1 / (double)image.Height),
                        Clamp01(box.Width / (double)image.Width),
                        Clamp01(box.Height / (double)image.Height))));
                }
            }
        }

        return new OcrPage(pageNumber, imagePath, image.Width, image.Height, lines);
    }

    public void CorrectOrientation(string imagePath, string language, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = Image.LoadFromFile(imagePath);
        var turns = FindOrientationWithOsd(image);
        if (turns < 0) turns = FindBestOcrRotation(image, language, cancellationToken);
        if (turns == 0) return;

        var corrected = image.Clone();
        try
        {
            for (; turns > 0; turns--)
            {
                using var previous = corrected;
                corrected = previous.Rotate90(RotationDirection.Clockwise);
            }

            corrected.Save(imagePath, ImageFormat.Png);
        }
        finally
        {
            corrected.Dispose();
        }
    }

    private int FindOrientationWithOsd(Image image)
    {
        try
        {
            using var page = GetEngine("osd").Process(image, PageSegMode.OsdOnly);
            page.DetectOrientation(out var orientation, out var confidence);
            return confidence >= 10 ? (360 - orientation) % 360 / 90 : -1;
        }
        catch (TesseractOCR.Exceptions.TesseractException)
        {
            return -1;
        }
    }

    private int FindBestOcrRotation(Image image, string language, CancellationToken cancellationToken)
    {
        using var clockwise90 = image.Rotate90(RotationDirection.Clockwise);
        using var clockwise180 = clockwise90.Rotate90(RotationDirection.Clockwise);
        using var clockwise270 = clockwise180.Rotate90(RotationDirection.Clockwise);
        var candidates = new[] { image, clockwise90, clockwise180, clockwise270 };
        var bestTurns = 0;
        var bestScore = double.MinValue;

        for (var turns = 0; turns < candidates.Length; turns++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var page = GetEngine(language).Process(candidates[turns]);
            var characters = page.Text.Count(char.IsLetterOrDigit);
            var score = page.MeanConfidence * Math.Log(1 + characters);
            if (score <= bestScore) continue;
            bestScore = score;
            bestTurns = turns;
        }

        return bestTurns;
    }

    public IReadOnlyList<string> GetInstalledLanguages() => Directory
        .EnumerateFiles(_dataPath, "*.traineddata", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileNameWithoutExtension)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Where(name => !string.Equals(name, "osd", StringComparison.OrdinalIgnoreCase))
        .Cast<string>()
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private Engine GetEngine(string language)
    {
        if (_engines.TryGetValue(language, out var engine)) return engine;

        var codes = language.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var code in codes)
        {
            var trainedData = Path.Combine(_dataPath, $"{code}.traineddata");
            if (!File.Exists(trainedData))
            {
                throw new FileNotFoundException($"OCR language data is missing for '{code}'.", trainedData);
            }
        }

        engine = new Engine(_dataPath, language,
            language.Equals("osd", StringComparison.OrdinalIgnoreCase) ? EngineMode.Default : EngineMode.LstmOnly);
        _engines.Add(language, engine);
        return engine;
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    public void Dispose()
    {
        foreach (var engine in _engines.Values) engine.Dispose();
        _engines.Clear();
    }
}
