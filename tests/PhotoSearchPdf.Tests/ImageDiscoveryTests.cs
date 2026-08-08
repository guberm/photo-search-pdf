using PhotoSearchPdf.Core;

namespace PhotoSearchPdf.Tests;

public sealed class ImageDiscoveryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"photo-search-pdf-{Guid.NewGuid():N}");

    public ImageDiscoveryTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void FindImages_ReturnsSupportedFilesInNaturalNameOrder()
    {
        Touch("page10.jpg");
        Touch("page2.PNG");
        Touch("page1.webp");
        Touch("notes.txt");

        var images = ImageDiscovery.FindImages(_folder, recursive: false);

        Assert.Equal(new[] { "page1.webp", "page2.PNG", "page10.jpg" }, images.Select(Path.GetFileName));
    }

    [Fact]
    public void FindImages_OnlyIncludesSubfoldersWhenRequested()
    {
        Touch("root.jpg");
        Directory.CreateDirectory(Path.Combine(_folder, "album"));
        File.WriteAllBytes(Path.Combine(_folder, "album", "inside.png"), []);

        Assert.Single(ImageDiscovery.FindImages(_folder, recursive: false));
        Assert.Equal(2, ImageDiscovery.FindImages(_folder, recursive: true).Count);
    }

    [Fact]
    public void FindImages_RejectsMissingFolder()
    {
        var missing = Path.Combine(_folder, "missing");

        var error = Assert.Throws<DirectoryNotFoundException>(() => ImageDiscovery.FindImages(missing, false));

        Assert.Contains(missing, error.Message);
    }

    private void Touch(string name) => File.WriteAllBytes(Path.Combine(_folder, name), []);

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
