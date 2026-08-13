using TesseractOCR.Pix;
using TesseractImageFormat = TesseractOCR.Enums.ImageFormat;

namespace PhotoSearchPdf.Core;

public sealed record ConversionOptions(
    string InputFolder,
    string OutputPdfPath,
    string OcrLanguage,
    bool Recursive);

public sealed record ConversionProgress(int Completed, int Total, string CurrentFile, string Stage);

public sealed record ConversionResult(string PdfPath, SidecarPaths Sidecars, int PageCount);

public sealed class PhotoPdfConverter : IDisposable
{
    private readonly TesseractOcrService _ocr;

    public PhotoPdfConverter(string tessdataPath) => _ocr = new TesseractOcrService(tessdataPath);

    public async Task<ConversionResult> ConvertAsync(
        ConversionOptions options,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var images = ImageDiscovery.FindImages(options.InputFolder, options.Recursive);
        if (images.Count == 0)
        {
            throw new InvalidOperationException("No supported images were found in the selected folder.");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "PhotoSearchPdf", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var pages = await Task.Run(() => ProcessImages(images, tempRoot, options.OcrLanguage, progress, cancellationToken),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ConversionProgress(images.Count, images.Count, string.Empty, "Building PDF"));
            await Task.Run(() => SearchablePdfWriter.Write(
                options.OutputPdfPath,
                pages.Select(page => new PdfPageInput(page.NormalizedImage, page.Ocr)).ToArray(),
                new DirectoryInfo(options.InputFolder).Name), cancellationToken);

            progress?.Report(new ConversionProgress(images.Count, images.Count, string.Empty, "Writing LLM sidecars"));
            var sidecars = await SidecarWriter.WriteAsync(
                options.OutputPdfPath,
                pages.Select(page => page.Ocr).ToArray(),
                options.OcrLanguage,
                cancellationToken);

            progress?.Report(new ConversionProgress(images.Count, images.Count, string.Empty, "Done"));
            return new ConversionResult(options.OutputPdfPath, sidecars, images.Count);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private IReadOnlyList<ProcessedPage> ProcessImages(
        IReadOnlyList<string> images,
        string tempRoot,
        string language,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var pages = new List<ProcessedPage>(images.Count);
        for (var index = 0; index < images.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = images[index];
            progress?.Report(new ConversionProgress(index, images.Count, Path.GetFileName(source), "OCR"));
            var normalized = Path.Combine(tempRoot, $"page-{index + 1:D6}.png");
            using (var image = Image.LoadFromFile(source))
            {
                image.Save(normalized, TesseractImageFormat.Png);
            }

            _ocr.CorrectOrientation(normalized, language, cancellationToken);
            var ocr = _ocr.Recognize(normalized, index + 1, language, cancellationToken) with { SourceFile = source };
            pages.Add(new ProcessedPage(normalized, ocr));
        }

        return pages;
    }

    public void Dispose() => _ocr.Dispose();

    private sealed record ProcessedPage(string NormalizedImage, OcrPage Ocr);
}
