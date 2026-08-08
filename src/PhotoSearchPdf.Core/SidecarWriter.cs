using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoSearchPdf.Core;

public static class SidecarWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<SidecarPaths> WriteAsync(
        string pdfPath,
        IReadOnlyList<OcrPage> pages,
        string ocrLanguage,
        CancellationToken cancellationToken)
    {
        var paths = OutputPaths.GetSidecars(pdfPath);
        var markdown = new StringBuilder()
            .AppendLine($"# {Path.GetFileNameWithoutExtension(pdfPath)}")
            .AppendLine()
            .AppendLine($"> OCR language: `{ocrLanguage}` · Pages: {pages.Count}")
            .AppendLine();
        var text = new StringBuilder();

        foreach (var page in pages)
        {
            markdown.AppendLine($"<!-- Page {page.PageNumber}: {Path.GetFileName(page.SourceFile)} -->")
                .AppendLine()
                .AppendLine($"## Page {page.PageNumber}")
                .AppendLine()
                .AppendLine(page.Text)
                .AppendLine();
            text.AppendLine($"--- Page {page.PageNumber}: {Path.GetFileName(page.SourceFile)} ---")
                .AppendLine(page.Text)
                .AppendLine();
        }

        await File.WriteAllTextAsync(paths.Markdown, markdown.ToString(), Utf8NoBom, cancellationToken);
        await File.WriteAllTextAsync(paths.Text, text.ToString(), Utf8NoBom, cancellationToken);
        var manifest = new OcrManifest(1, Path.GetFileName(pdfPath), ocrLanguage, DateTimeOffset.UtcNow, pages);
        await File.WriteAllTextAsync(paths.Json, JsonSerializer.Serialize(manifest, JsonOptions), Utf8NoBom, cancellationToken);
        return paths;
    }

    private sealed record OcrManifest(
        int SchemaVersion,
        string PdfFile,
        string OcrLanguage,
        DateTimeOffset CreatedUtc,
        IReadOnlyList<OcrPage> Pages);
}
