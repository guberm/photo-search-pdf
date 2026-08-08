using PhotoSearchPdf.Core;

namespace PhotoSearchPdf.Tests;

public sealed class CodexCliInstallerTests
{
    [Fact]
    public void FindWinget_UsesWindowsAppsExecutable()
    {
        var directory = "C:\\Users\\me\\AppData\\Local\\Microsoft\\WindowsApps";
        var executable = Path.Combine(directory, "winget.exe");

        var result = CodexCliInstaller.FindWinget([directory], candidate => candidate == executable);

        Assert.Equal(executable, result);
    }

    [Fact]
    public void InstallArguments_PinOfficialPackageAndRunNonInteractively()
    {
        var args = CodexCliInstaller.BuildInstallArguments();

        Assert.Contains("OpenAI.Codex", args);
        Assert.Contains("--exact", args);
        Assert.Contains("--source", args);
        Assert.Contains("winget", args);
        Assert.Contains("--accept-package-agreements", args);
        Assert.Contains("--accept-source-agreements", args);
        Assert.Contains("--silent", args);
        Assert.Contains("--disable-interactivity", args);
    }
}
