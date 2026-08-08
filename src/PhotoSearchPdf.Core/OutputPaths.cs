namespace PhotoSearchPdf.Core;

public static class OutputPaths
{
    public static string ResolvePdfPath(string folder, Func<string, bool>? exists = null, string? preferredPath = null)
    {
        exists ??= File.Exists;
        var basePath = preferredPath ?? Path.Combine(folder, $"{new DirectoryInfo(folder).Name}-searchable.pdf");
        if (!exists(basePath)) return basePath;

        var directory = Path.GetDirectoryName(basePath) ?? folder;
        var baseName = Path.GetFileNameWithoutExtension(basePath);
        for (var suffix = 2; ; suffix++)
        {
            var candidate = Path.Combine(directory, $"{baseName}-{suffix}.pdf");
            if (!exists(candidate)) return candidate;
        }
    }

    public static SidecarPaths GetSidecars(string pdfPath)
    {
        var directory = Path.GetDirectoryName(pdfPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(pdfPath);
        return new SidecarPaths(
            Path.Combine(directory, $"{baseName}.md"),
            Path.Combine(directory, $"{baseName}.txt"),
            Path.Combine(directory, $"{baseName}.ocr.json"));
    }
}
