using PhotoSearchPdf.Core;

namespace PhotoSearchPdf.Tests;

public sealed class CodexCliTests
{
    [Fact]
    public void FindInvocation_PrefersNativeExecutable()
    {
        var path = Path.Combine("C:\\tools", "codex.exe");

        var result = CodexCliLocator.FindInvocation(["C:\\tools"], candidate => candidate == path);

        Assert.NotNull(result);
        Assert.Equal(path, result.FileName);
        Assert.Empty(result.PrefixArguments);
    }

    [Fact]
    public void FindInvocation_UsesEarlierCommandShimBeforeLaterWindowsAppsExecutable()
    {
        var npm = "C:\\npm";
        var windowsApps = "C:\\Program Files\\WindowsApps\\OpenAI.Codex";
        var command = Path.Combine(npm, "codex.cmd");
        var inaccessibleExecutable = Path.Combine(windowsApps, "codex.exe");

        var result = CodexCliLocator.FindInvocation(
            [npm, windowsApps],
            candidate => candidate == command || candidate == inaccessibleExecutable);

        Assert.NotNull(result);
        Assert.Equal("cmd.exe", result.FileName);
        Assert.Contains(command, result.PrefixArguments[^1]);
    }

    [Fact]
    public void BuildQuestionArguments_IsEphemeralReadOnlyAndContainsNoUserText()
    {
        var args = CodexQuestionService.BuildQuestionArguments();
        var joined = string.Join(' ', args);

        Assert.Contains("--ephemeral", args);
        Assert.Contains("read-only", args);
        Assert.Contains("--ignore-user-config", args);
        Assert.Contains("--ignore-rules", args);
        Assert.Contains("--skip-git-repo-check", args);
        Assert.Contains("plugins", args);
        Assert.Contains("apps", args);
        Assert.Contains("hooks", args);
        Assert.Equal("-", args[^1]);
        Assert.DoesNotContain("question", joined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPrompt_RequiresGroundedAnswerAndPageCitations()
    {
        var prompt = CodexQuestionService.BuildPrompt(
            "Когда заканчивается договор?",
            "=== Page 7 | scan.jpg ===\n31 декабря");

        Assert.Contains("Когда заканчивается договор?", prompt);
        Assert.Contains("[стр. 7]", prompt);
        Assert.Contains("только", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("31 декабря", prompt);
    }

    [Fact]
    public void SafeWorkingDirectory_IsIsolatedFromTheSelectedDocument()
    {
        var path = CodexQuestionService.GetSafeWorkingDirectory("C:\\Users\\me\\AppData\\Local");

        Assert.Equal(Path.Combine("C:\\Users\\me\\AppData\\Local", "PhotoSearchPdf", "llm-workspace"), path);
    }
}
