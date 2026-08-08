namespace PhotoSearchPdf.Core;

public sealed record OcrBox(double X, double Y, double Width, double Height);

public sealed record OcrLine(string Text, OcrBox Box);

public sealed record OcrPage(
    int PageNumber,
    string SourceFile,
    int PixelWidth,
    int PixelHeight,
    IReadOnlyList<OcrLine> Lines)
{
    public string Text => string.Join(Environment.NewLine, Lines.Select(line => line.Text));
}

public sealed record SidecarPaths(string Markdown, string Text, string Json);
