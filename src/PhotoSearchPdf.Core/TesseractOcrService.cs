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

    public IReadOnlyList<string> GetInstalledLanguages() => Directory
        .EnumerateFiles(_dataPath, "*.traineddata", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileNameWithoutExtension)
        .Where(name => !string.IsNullOrWhiteSpace(name))
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

        engine = new Engine(_dataPath, language, EngineMode.LstmOnly);
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
