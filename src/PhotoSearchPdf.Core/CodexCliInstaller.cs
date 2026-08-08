using System.Diagnostics;
using System.Text;

namespace PhotoSearchPdf.Core;

public sealed record CodexInstallResult(bool Succeeded, string Message);

public sealed class CodexCliInstaller
{
    public const string PackageId = "OpenAI.Codex";
    public const string HelpUrl = "https://learn.chatgpt.com/docs/codex/cli";

    private readonly string _wingetExecutable;

    public CodexCliInstaller(string wingetExecutable) => _wingetExecutable = wingetExecutable;

    public static string? FindWinget()
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData)) paths.Add(Path.Combine(localAppData, "Microsoft", "WindowsApps"));
        return FindWinget(paths.Distinct(StringComparer.OrdinalIgnoreCase), File.Exists);
    }

    public static string? FindWinget(IEnumerable<string> directories, Func<string, bool> fileExists)
    {
        foreach (var directory in directories)
        {
            var candidate = Path.Combine(directory, "winget.exe");
            if (fileExists(candidate)) return candidate;
        }
        return null;
    }

    public static IReadOnlyList<string> BuildInstallArguments() =>
    [
        "install",
        "--id", PackageId,
        "--exact",
        "--source", "winget",
        "--accept-package-agreements",
        "--accept-source-agreements",
        "--silent",
        "--disable-interactivity"
    ];

    public async Task<CodexInstallResult> InstallAsync(CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _wingetExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in BuildInstallArguments()) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start()) return new CodexInstallResult(false, "Не удалось запустить Windows Package Manager.");
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();
            if (process.ExitCode == 0)
            {
                return new CodexInstallResult(true, "Codex CLI установлен.");
            }

            var detail = string.IsNullOrWhiteSpace(error) ? output : error;
            if (detail.Length > 1_000) detail = detail[^1_000..];
            return new CodexInstallResult(false,
                $"Windows Package Manager завершился с ошибкой ({process.ExitCode}). {detail}".Trim());
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
