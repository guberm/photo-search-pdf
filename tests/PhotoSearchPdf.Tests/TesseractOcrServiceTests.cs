using System.Drawing;
using System.Drawing.Imaging;
using PhotoSearchPdf.Core;

namespace PhotoSearchPdf.Tests;

public sealed class TesseractOcrServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"photo-search-pdf-{Guid.NewGuid():N}");

    public TesseractOcrServiceTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void Recognize_ReturnsTextLinesWithNormalizedBounds()
    {
        var imagePath = Path.Combine(_folder, "ocr.png");
        using (var bitmap = new Bitmap(1400, 300))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("Arial", 72, FontStyle.Bold))
        {
            graphics.Clear(Color.White);
            graphics.DrawString("Searchable needle 123", font, Brushes.Black, 40, 80);
            bitmap.Save(imagePath, ImageFormat.Png);
        }

        using var service = new TesseractOcrService(Path.Combine(AppContext.BaseDirectory, "tessdata"));
        var page = service.Recognize(imagePath, 1, "eng", CancellationToken.None);

        Assert.Contains("Searchable", page.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(page.Lines, line => line.Box is { X: >= 0, Y: >= 0, Width: > 0, Height: > 0 }
            && line.Box.X + line.Box.Width <= 1.001
            && line.Box.Y + line.Box.Height <= 1.001);
    }

    [Fact]
    public void Recognize_SupportsCombinedRussianAndEnglishModel()
    {
        var imagePath = Path.Combine(_folder, "russian.png");
        using (var bitmap = new Bitmap(1400, 300))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("Arial", 72, FontStyle.Bold))
        {
            graphics.Clear(Color.White);
            graphics.DrawString("РУССКИЙ ТЕКСТ 456", font, Brushes.Black, 40, 80);
            bitmap.Save(imagePath, ImageFormat.Png);
        }

        using var service = new TesseractOcrService(Path.Combine(AppContext.BaseDirectory, "tessdata"));
        var page = service.Recognize(imagePath, 1, "rus+eng", CancellationToken.None);

        Assert.Contains("РУССКИЙ", page.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("456", page.Text, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
