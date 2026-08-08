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
        var imageRatio = Math.Max(1, input.Ocr.PixelWidth) / (double)Math.Max(1, input.Ocr.PixelHeight);
        var page = document.AddPage();
        if (imageRatio >= 1)
        {
            page.Width = XUnit.FromPoint(A4Height);
            page.Height = XUnit.FromPoint(A4Width);
        }
        else
        {
            page.Width = XUnit.FromPoint(A4Width);
            page.Height = XUnit.FromPoint(A4Height);
        }

        using var graphics = XGraphics.FromPdfPage(page);
        using var image = XImage.FromFile(input.ImagePath);
        DrawContainedImage(graphics, image, page.Width.Point, page.Height.Point);

        var hiddenBrush = new XSolidBrush(XColor.FromArgb(1, 0, 0, 0));
        foreach (var line in input.Ocr.Lines.Where(line => !string.IsNullOrWhiteSpace(line.Text)))
        {
            var box = line.Box;
            var fontSize = Math.Clamp(box.Height * page.Height.Point * 0.8, 4, 72);
            var font = new XFont("Arial", fontSize, XFontStyleEx.Regular,
                new XPdfFontOptions(PdfFontEncoding.Unicode));
            var x = Math.Clamp(box.X, 0, 1) * page.Width.Point;
            var y = Math.Clamp(box.Y + box.Height, 0, 1) * page.Height.Point;
            graphics.DrawString(line.Text, font, hiddenBrush, new XPoint(x, y));
        }
    }

    private static void DrawContainedImage(XGraphics graphics, XImage image, double pageWidth, double pageHeight)
    {
        var scale = Math.Min(pageWidth / image.PixelWidth, pageHeight / image.PixelHeight);
        var width = image.PixelWidth * scale;
        var height = image.PixelHeight * scale;
        graphics.DrawImage(image, (pageWidth - width) / 2, (pageHeight - height) / 2, width, height);
    }
}
