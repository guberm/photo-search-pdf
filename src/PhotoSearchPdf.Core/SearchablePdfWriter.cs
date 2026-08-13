using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace PhotoSearchPdf.Core;

public sealed record PdfPageInput(string ImagePath, OcrPage Ocr);

public static class SearchablePdfWriter
{
    private const double A4Width = 595.28;
    private const double A4Height = 841.89;

    public static void Write(string outputPath, IReadOnlyList<PdfPageInput> pages, string title)
    {
        if (pages.Count == 0) throw new ArgumentException("At least one page is required.", nameof(pages));

        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        using var document = new PdfDocument();
        document.Info.Title = title;
        document.Info.Creator = "PhotoSearch PDF";
        document.Info.Subject = "Searchable OCR document";

        foreach (var input in pages)
        {
            AddPage(document, input);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        document.Save(outputPath);
    }

    private static void AddPage(PdfDocument document, PdfPageInput input)
    {
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(A4Width);
        page.Height = XUnit.FromPoint(A4Height);

        using var graphics = XGraphics.FromPdfPage(page);
        using var image = XImage.FromFile(input.ImagePath);
        var imageBounds = DrawContainedImage(graphics, image, page.Width.Point, page.Height.Point);

        var hiddenBrush = new XSolidBrush(XColor.FromArgb(1, 0, 0, 0));
        foreach (var line in input.Ocr.Lines.Where(line => !string.IsNullOrWhiteSpace(line.Text)))
        {
            var box = line.Box;
            var fontSize = Math.Clamp(box.Height * imageBounds.Height * 0.8, 4, 72);
            var font = new XFont("Arial", fontSize, XFontStyleEx.Regular,
                new XPdfFontOptions(PdfFontEncoding.Unicode));
            var x = imageBounds.X + Math.Clamp(box.X, 0, 1) * imageBounds.Width;
            var y = imageBounds.Y + Math.Clamp(box.Y + box.Height, 0, 1) * imageBounds.Height;
            graphics.DrawString(line.Text, font, hiddenBrush, new XPoint(x, y));
        }
    }

    private static XRect DrawContainedImage(XGraphics graphics, XImage image, double pageWidth, double pageHeight)
    {
        var scale = Math.Min(pageWidth / image.PixelWidth, pageHeight / image.PixelHeight);
        var width = image.PixelWidth * scale;
        var height = image.PixelHeight * scale;
        var bounds = new XRect((pageWidth - width) / 2, (pageHeight - height) / 2, width, height);
        graphics.DrawImage(image, bounds);
        return bounds;
    }
}
