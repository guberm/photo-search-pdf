using PhotoSearchPdf.Core;

namespace PhotoSearchPdf.Tests;

public sealed class OutputPathTests
{
    [Fact]
    public void ResolvePdfPath_UsesFolderNameAndAvoidsOverwrite()
    {
        var folder = Path.Combine(Path.GetTempPath(), "Summer Photos");
        var existing = Path.Combine(folder, "Summer Photos-searchable.pdf");

        var result = OutputPaths.ResolvePdfPath(folder, path => path == existing, existing);

        Assert.Equal(Path.Combine(folder, "Summer Photos-searchable-2.pdf"), result);
    }

    [Fact]
    public void Sidecars_UseSameBaseNameAsPdf()
    {
        var pdf = Path.Combine("C:\\docs", "archive.pdf");

        var sidecars = OutputPaths.GetSidecars(pdf);

        Assert.Equal(Path.Combine("C:\\docs", "archive.md"), sidecars.Markdown);
        Assert.Equal(Path.Combine("C:\\docs", "archive.txt"), sidecars.Text);
        Assert.Equal(Path.Combine("C:\\docs", "archive.ocr.json"), sidecars.Json);
    }
}
