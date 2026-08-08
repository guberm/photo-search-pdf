using System.Globalization;

namespace PhotoSearchPdf.Core;

public static class ImageDiscovery
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".webp"
    };

    public static IReadOnlyList<string> FindImages(string folder, bool recursive)
    {
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"Image folder does not exist: {folder}");
        }

        return Directory
            .EnumerateFiles(folder, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(path => Extensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, NaturalPathComparer.Instance)
            .ToArray();
    }

    private sealed class NaturalPathComparer : IComparer<string>
    {
        public static NaturalPathComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;

            var x = left.AsSpan();
            var y = right.AsSpan();
            var xi = 0;
            var yi = 0;

            while (xi < x.Length && yi < y.Length)
            {
                if (char.IsDigit(x[xi]) && char.IsDigit(y[yi]))
                {
                    var xStart = xi;
                    var yStart = yi;
                    while (xi < x.Length && char.IsDigit(x[xi])) xi++;
                    while (yi < y.Length && char.IsDigit(y[yi])) yi++;

                    var xNumber = x[xStart..xi].TrimStart('0');
                    var yNumber = y[yStart..yi].TrimStart('0');
                    var lengthComparison = xNumber.Length.CompareTo(yNumber.Length);
                    if (lengthComparison != 0) return lengthComparison;

                    var numberComparison = xNumber.CompareTo(yNumber, StringComparison.Ordinal);
                    if (numberComparison != 0) return numberComparison;

                    var originalLengthComparison = (xi - xStart).CompareTo(yi - yStart);
                    if (originalLengthComparison != 0) return originalLengthComparison;
                    continue;
                }

                var comparison = char.ToUpper(x[xi], CultureInfo.InvariantCulture)
                    .CompareTo(char.ToUpper(y[yi], CultureInfo.InvariantCulture));
                if (comparison != 0) return comparison;
                xi++;
                yi++;
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}
