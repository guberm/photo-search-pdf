using PhotoSearchPdf.Core;
using UglyToad.PdfPig;

namespace PhotoSearchPdf.Tests;

public sealed class SearchablePdfWriterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"photo-search-pdf-{Guid.NewGuid():N}");

    public SearchablePdfWriterTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void Write_CreatesOneImagePageWithExtractableOcrText()
    {
        var image = Path.Combine(_folder, "page.png");
        var pdf = Path.Combine(_folder, "result.pdf");
        File.WriteAllBytes(image, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        var ocr = new OcrPage(1, image, 100, 100,
            [new OcrLine("Searchable needle", new OcrBox(0.1, 0.2, 0.7, 0.1))]);

        SearchablePdfWriter.Write(pdf, [new PdfPageInput(image, ocr)], "Test document");

        using var document = PdfDocument.Open(pdf);
        Assert.Equal(1, document.NumberOfPages);
        Assert.Contains("Searchable needle", document.GetPage(1).Text);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
