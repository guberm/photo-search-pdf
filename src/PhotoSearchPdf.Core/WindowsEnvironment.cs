namespace PhotoSearchPdf.Core;

public static class WindowsEnvironment
{
    public static void EnsureWindir()
    {
        if (!OperatingSystem.IsWindows()) return;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("windir"))) return;

        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (!string.IsNullOrWhiteSpace(systemRoot))
        {
            Environment.SetEnvironmentVariable("windir", systemRoot);
        }
    }
}
