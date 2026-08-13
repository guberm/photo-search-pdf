using PhotoSearchPdf.Core;
using System.Text;

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
    public void FindInvocation_AcceptsWingetPortableLink()
    {
        var links = "C:\\Users\\me\\AppData\\Local\\Microsoft\\WinGet\\Links";
        var executable = Path.Combine(links, "codex.exe");

        var result = CodexCliLocator.FindInvocation([links], candidate => candidate == executable);

        Assert.NotNull(result);
        Assert.Equal(executable, result.FileName);
    }

    [Fact]
    public void FindInvocation_SkipsProtectedDesktopPackageBinary()
    {
        var resources = "C:\\Program Files\\WindowsApps\\OpenAI.Codex_1.0.0_x64__publisher\\app\\resources";
        var executable = Path.Combine(resources, "codex.exe");

        var result = CodexCliLocator.FindInvocation([resources], candidate => candidate == executable);

        Assert.Null(result);
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
    public void BuildLogoutArguments_UsesOfficialCodexLogoutCommand()
    {
        Assert.Equal(["logout"], CodexQuestionService.BuildLogoutArguments());
    }

    [Fact]
    public void BuildPrompt_RequiresGroundedAnswerAndPageCitations()
    {
        var prompt = CodexQuestionService.BuildPrompt(
            "When does the agreement end?",
            "=== Page 7 | scan.jpg ===\nDecember 31");

        Assert.Contains("When does the agreement end?", prompt);
        Assert.Contains("[page 7]", prompt);
        Assert.Contains("only", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("December 31", prompt);
    }

    [Fact]
    public void SafeWorkingDirectory_IsIsolatedFromTheSelectedDocument()
    {
        var path = CodexQuestionService.GetSafeWorkingDirectory("C:\\Users\\me\\AppData\\Local");

        Assert.Equal(Path.Combine("C:\\Users\\me\\AppData\\Local", "PhotoSearchPdf", "llm-workspace"), path);
    }

    [Fact]
    public void ParseAccountReadResponse_ReturnsChatGptEmailAndPlan()
    {
        const string response =
            "{\"id\":2,\"result\":{\"account\":{\"type\":\"chatgpt\",\"email\":\"person@example.com\",\"planType\":\"pro\"},\"requiresOpenaiAuth\":true}}";

        var account = CodexQuestionService.ParseAccountReadResponse(response);

        Assert.NotNull(account);
        Assert.Equal("person@example.com", account.Email);
        Assert.Equal("pro", account.PlanType);
        Assert.Equal("Connected as person@example.com (ChatGPT Pro)",
            CodexQuestionService.BuildConnectedMessage(account));
    }

    [Fact]
    public async Task AskAsync_WritesPromptAsUtf8RegardlessOfWindowsConsoleEncoding()
    {
        var script = Path.Combine(Path.GetTempPath(), $"photo-search-utf8-{Guid.NewGuid():N}.ps1");
        var command = Path.ChangeExtension(script, ".cmd");
        await File.WriteAllTextAsync(script, """
            $stream = [Console]::OpenStandardInput()
            $memory = [System.IO.MemoryStream]::new()
            $stream.CopyTo($memory)
            try {
                $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
                $null = $utf8.GetString($memory.ToArray())
                [Console]::Out.Write('OK')
            } catch {
                [Console]::Error.Write($_.Exception.Message)
                exit 17
            }
            """, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(command,
            $"@echo off{Environment.NewLine}powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{script}\"",
            TestContext.Current.CancellationToken);
        var originalEncoding = Console.InputEncoding;
        try
        {
            Console.InputEncoding = Encoding.Latin1;
            var service = new CodexQuestionService(new CodexCliInvocation(
                "cmd.exe",
                ["/d", "/s", "/c", command]));
            var context = new DocumentContext("Café — annual increase €", [1], 1, false, "contract.pdf");

            var answer = await service.AskAsync(
                "Review the price increase.", context, TestContext.Current.CancellationToken);

            Assert.Equal("OK", answer);
        }
        finally
        {
            Console.InputEncoding = originalEncoding;
            File.Delete(command);
            File.Delete(script);
        }
    }
}
