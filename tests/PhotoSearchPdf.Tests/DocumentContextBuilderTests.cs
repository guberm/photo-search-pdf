using System.Text.Json;
using PhotoSearchPdf.Core;

namespace PhotoSearchPdf.Tests;

public sealed class DocumentContextBuilderTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"photo-search-pdf-context-{Guid.NewGuid():N}");

    public DocumentContextBuilderTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void Build_FromPdf_UsesAdjacentOcrJsonAndKeepsPageMarkers()
    {
        var pdf = Path.Combine(_folder, "archive.pdf");
        File.WriteAllText(pdf, string.Empty);
        WriteManifest(Path.Combine(_folder, "archive.ocr.json"),
        [
            (1, "001.jpg", "The agreement starts here."),
            (2, "002.jpg", "Payment is due in thirty days.")
        ]);

        var result = DocumentContextBuilder.Build(pdf, "When is payment due?", 10_000);

        Assert.Equal(2, result.TotalPages);
        Assert.Equal([1, 2], result.SelectedPages);
        Assert.False(result.IsTruncated);
        Assert.Contains("=== Page 2 | 002.jpg ===", result.Text);
        Assert.Contains("Payment is due in thirty days.", result.Text);
    }

    [Fact]
    public void Build_WhenDocumentExceedsBudget_SelectsRelevantPages()
    {
        var manifest = Path.Combine(_folder, "large.ocr.json");
        WriteManifest(manifest,
        [
            (1, "001.jpg", new string('a', 500)),
            (2, "002.jpg", "The cancellation deadline is 15 September. " + new string('b', 250)),
            (3, "003.jpg", new string('c', 500))
        ]);

        var result = DocumentContextBuilder.Build(manifest, "What is the cancellation deadline?", 420);

        Assert.True(result.IsTruncated);
        Assert.Contains(2, result.SelectedPages);
        Assert.Contains("15 September", result.Text);
        Assert.DoesNotContain("=== Page 1", result.Text);
    }

    [Fact]
    public void Build_WithoutSidecarForPdf_ExplainsRequiredFile()
    {
        var pdf = Path.Combine(_folder, "missing.pdf");
        File.WriteAllText(pdf, string.Empty);

        var error = Assert.Throws<FileNotFoundException>(() =>
            DocumentContextBuilder.Build(pdf, "Question", 1_000));

        Assert.Contains(".ocr.json", error.Message);
    }

    private static void WriteManifest(string path, IReadOnlyList<(int Page, string Source, string Text)> pages)
    {
        var payload = new
        {
            schemaVersion = 1,
            pdfFile = Path.ChangeExtension(Path.GetFileName(path), ".pdf"),
            ocrLanguage = "rus+eng",
            pages = pages.Select(page => new
            {
                pageNumber = page.Page,
                sourceFile = page.Source,
                pixelWidth = 100,
                pixelHeight = 100,
                lines = new[] { new { text = page.Text, box = new { x = 0, y = 0, width = 1, height = 1 } } },
                text = page.Text
            })
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
