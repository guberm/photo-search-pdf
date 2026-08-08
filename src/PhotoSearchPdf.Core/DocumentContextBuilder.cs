using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhotoSearchPdf.Core;

public sealed record DocumentContext(
    string Text,
    IReadOnlyList<int> SelectedPages,
    int TotalPages,
    bool IsTruncated,
    string SourcePath);

public static partial class DocumentContextBuilder
{
    public const int DefaultMaxCharacters = 80_000;

    public static DocumentContext Build(
        string documentPath,
        string question,
        int maxCharacters = DefaultMaxCharacters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        if (maxCharacters < 200) throw new ArgumentOutOfRangeException(nameof(maxCharacters));

        var sourcePath = ResolveManifestPath(Path.GetFullPath(documentPath));
        var pages = ReadPages(sourcePath);
        if (pages.Count == 0) throw new InvalidDataException("OCR JSON не содержит распознанных страниц.");

        var formatted = pages.Select(FormatPage).ToArray();
        var fullLength = formatted.Sum(value => value.Length + Environment.NewLine.Length);
        if (fullLength <= maxCharacters)
        {
            return new DocumentContext(
                string.Join(Environment.NewLine, formatted),
                pages.Select(page => page.PageNumber).ToArray(),
                pages.Count,
                false,
                sourcePath);
        }

        var terms = TokenRegex().Matches(question.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(term => term.Length >= 3)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var ranked = pages
            .Select((page, index) => new RankedPage(page, index, Score(page.Text, terms)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Index)
            .ToArray();

        var selected = new List<OcrContextPage>();
        var used = 0;
        foreach (var item in ranked)
        {
            var pageText = FormatPage(item.Page);
            if (used + pageText.Length <= maxCharacters)
            {
                selected.Add(item.Page);
                used += pageText.Length + Environment.NewLine.Length;
            }
        }

        if (selected.Count == 0)
        {
            var first = ranked[0].Page;
            var header = FormatHeader(first);
            var available = Math.Max(0, maxCharacters - header.Length - 24);
            selected.Add(first with { Text = first.Text[..Math.Min(first.Text.Length, available)] + "\n[page truncated]" });
        }

        selected.Sort((left, right) => left.PageNumber.CompareTo(right.PageNumber));
        return new DocumentContext(
            string.Join(Environment.NewLine, selected.Select(FormatPage)),
            selected.Select(page => page.PageNumber).ToArray(),
            pages.Count,
            true,
            sourcePath);
    }

    private static string ResolveManifestPath(string documentPath)
    {
        if (documentPath.EndsWith(".ocr.json", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(documentPath)) return documentPath;
            throw new FileNotFoundException("OCR JSON не найден.", documentPath);
        }

        if (documentPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var manifest = OutputPaths.GetSidecars(documentPath).Json;
            if (File.Exists(manifest)) return manifest;
            throw new FileNotFoundException(
                $"Рядом с PDF не найден {Path.GetFileName(manifest)}. Выберите PDF, созданный PhotoSearch PDF, или соответствующий .ocr.json.",
                manifest);
        }

        throw new NotSupportedException("Для вопросов выберите PDF, созданный приложением, или файл .ocr.json.");
    }

    private static IReadOnlyList<OcrContextPage> ReadPages(string manifestPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!document.RootElement.TryGetProperty("pages", out var pagesElement) ||
            pagesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Некорректный OCR JSON: отсутствует массив pages.");
        }

        var pages = new List<OcrContextPage>();
        foreach (var element in pagesElement.EnumerateArray())
        {
            var pageNumber = element.GetProperty("pageNumber").GetInt32();
            var sourceFile = element.TryGetProperty("sourceFile", out var source)
                ? Path.GetFileName(source.GetString() ?? string.Empty)
                : string.Empty;
            var text = element.TryGetProperty("text", out var textElement)
                ? textElement.GetString() ?? string.Empty
                : ReadLines(element);
            pages.Add(new OcrContextPage(pageNumber, sourceFile, text));
        }
        return pages.OrderBy(page => page.PageNumber).ToArray();
    }

    private static string ReadLines(JsonElement page)
    {
        if (!page.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array) return string.Empty;
        return string.Join(Environment.NewLine, lines.EnumerateArray()
            .Select(line => line.TryGetProperty("text", out var text) ? text.GetString() : null)
            .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static int Score(string text, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0) return 0;
        var haystack = text.ToLowerInvariant();
        var score = 0;
        foreach (var term in terms)
        {
            var offset = 0;
            while ((offset = haystack.IndexOf(term, offset, StringComparison.Ordinal)) >= 0)
            {
                score++;
                offset += term.Length;
            }
        }
        return score;
    }

    private static string FormatPage(OcrContextPage page) => $"{FormatHeader(page)}{Environment.NewLine}{page.Text.Trim()}";

    private static string FormatHeader(OcrContextPage page) =>
        $"=== Page {page.PageNumber} | {page.SourceFile} ===";

    [GeneratedRegex(@"[\p{L}\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    private sealed record OcrContextPage(int PageNumber, string SourceFile, string Text);
    private sealed record RankedPage(OcrContextPage Page, int Index, int Score);
}
