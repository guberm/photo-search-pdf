using System.Text.Json;
using PhotoSearchPdf.Core;

namespace PhotoSearchPdf.Tests;

public sealed class SidecarWriterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"photo-search-pdf-{Guid.NewGuid():N}");

    public SidecarWriterTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public async Task WriteAsync_CreatesLlmReadyMarkdownTextAndJson()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pdf = Path.Combine(_folder, "book.pdf");
        var pages = new[]
        {
            new OcrPage(1, "001.jpg", 1200, 1600, [new OcrLine("First page", new OcrBox(0.1, 0.1, 0.4, 0.05))]),
            new OcrPage(2, "002.jpg", 1200, 1600, [new OcrLine("Second page", new OcrBox(0.1, 0.2, 0.5, 0.05))])
        };

        var files = await SidecarWriter.WriteAsync(pdf, pages, "en-US", cancellationToken);

        Assert.Contains("<!-- Page 1: 001.jpg -->", await File.ReadAllTextAsync(files.Markdown, cancellationToken));
        Assert.Contains("First page", await File.ReadAllTextAsync(files.Text, cancellationToken));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(files.Json, cancellationToken));
        Assert.Equal("en-US", json.RootElement.GetProperty("ocrLanguage").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("pages").GetArrayLength());
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
