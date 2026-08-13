using System.Drawing;
using System.Drawing.Imaging;
using PhotoSearchPdf.Core;
using UglyToad.PdfPig;

namespace PhotoSearchPdf.Tests;

public sealed class PhotoPdfConverterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"photo-search-pdf-{Guid.NewGuid():N}");

    public PhotoPdfConverterTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public async Task ConvertAsync_TurnsFolderIntoSearchablePdfAndSidecars()
    {
        CreateTextImage("page2.png", "Second searchable page");
        CreateTextImage("page1.png", "First searchable page");
        var output = Path.Combine(_folder, "result.pdf");
        var progress = new List<ConversionProgress>();
        using var converter = new PhotoPdfConverter(Path.Combine(AppContext.BaseDirectory, "tessdata"));

        var result = await converter.ConvertAsync(
            new ConversionOptions(_folder, output, "eng", false),
            new Progress<ConversionProgress>(item => progress.Add(item)),
            CancellationToken.None);

        Assert.Equal(2, result.PageCount);
        Assert.True(File.Exists(result.PdfPath));
        Assert.True(File.Exists(result.Sidecars.Markdown));
        using var pdf = PdfDocument.Open(result.PdfPath);
        Assert.Contains("First searchable page", pdf.GetPage(1).Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Second searchable page", pdf.GetPage(2).Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertAsync_AutomaticallyCorrectsRotatedPhotosBeforeOcr()
    {
        var imagePath = Path.Combine(_folder, "rotated.png");
        using (var bitmap = new Bitmap(1800, 2400))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("Arial", 48, FontStyle.Bold))
        {
            graphics.Clear(Color.White);
            for (var line = 0; line < 18; line++)
            {
                graphics.DrawString($"Annual price adjustment and consumer price index {line + 1}", font, Brushes.Black, 50, 60 + line * 120);
            }

            bitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
            bitmap.Save(imagePath, ImageFormat.Png);
        }

        var output = Path.Combine(_folder, "rotated-result.pdf");
        using var converter = new PhotoPdfConverter(Path.Combine(AppContext.BaseDirectory, "tessdata"));

        await converter.ConvertAsync(
            new ConversionOptions(_folder, output, "eng", false),
            null,
            CancellationToken.None);

        using var pdf = PdfDocument.Open(output);
        var page = pdf.GetPage(1);
        Assert.True(page.Width < page.Height);
        Assert.Contains("Annual price adjustment", page.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("consumer price index", page.Text, StringComparison.OrdinalIgnoreCase);
    }

    private void CreateTextImage(string name, string text)
    {
        using var bitmap = new Bitmap(1500, 300);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font("Arial", 72, FontStyle.Bold);
        graphics.Clear(Color.White);
        graphics.DrawString(text, font, Brushes.Black, 30, 80);
        bitmap.Save(Path.Combine(_folder, name), ImageFormat.Png);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
