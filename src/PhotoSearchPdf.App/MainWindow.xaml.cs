using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PhotoSearchPdf.Core;

namespace PhotoSearchPdf.App;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cancellation;
    private CancellationTokenSource? _questionCancellation;
    private string? _completedPdf;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"PhotoSearch PDF v{typeof(MainWindow).Assembly.GetName().Version!.ToString(3)}";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var folder = args.FirstOrDefault(Directory.Exists);
        if (folder is not null) InputFolderTextBox.Text = Path.GetFullPath(folder);

        var document = args.FirstOrDefault(path => File.Exists(path) &&
            (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith(".ocr.json", StringComparison.OrdinalIgnoreCase)));
        if (document is not null)
        {
            QuestionDocumentTextBox.Text = Path.GetFullPath(document);
            MainTabs.SelectedIndex = 1;
        }

        var languageIndex = Array.FindIndex(args, arg => arg.Equals("--lang", StringComparison.OrdinalIgnoreCase));
        if (languageIndex >= 0 && languageIndex + 1 < args.Length)
        {
            foreach (var item in LanguageComboBox.Items.OfType<ComboBoxItem>())
            {
                if (Equals(item.Tag, args[languageIndex + 1])) LanguageComboBox.SelectedItem = item;
            }
        }

        await RefreshCodexStatusAsync();
    }

    private void BrowseInput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a photo folder" };
        if (Directory.Exists(InputFolderTextBox.Text)) dialog.InitialDirectory = InputFolderTextBox.Text;
        if (dialog.ShowDialog(this) == true) InputFolderTextBox.Text = dialog.FolderName;
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save the searchable PDF",
            Filter = "PDF document (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = Path.GetFileName(OutputPdfTextBox.Text)
        };
        var directory = Path.GetDirectoryName(OutputPdfTextBox.Text);
        if (Directory.Exists(directory)) dialog.InitialDirectory = directory;
        if (dialog.ShowDialog(this) == true) OutputPdfTextBox.Text = dialog.FileName;
    }

    private void InputFolderTextBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshFolderSummary();

    private void RecursiveCheckBox_Changed(object sender, RoutedEventArgs e) => RefreshFolderSummary();

    private void RefreshFolderSummary()
    {
        var folder = InputFolderTextBox?.Text.Trim().Trim('"') ?? string.Empty;
        if (!Directory.Exists(folder))
        {
            if (ImageCountTextBlock is not null) ImageCountTextBlock.Text = "Folder not found";
            return;
        }

        try
        {
            var count = ImageDiscovery.FindImages(folder, RecursiveCheckBox?.IsChecked == true).Count;
            ImageCountTextBlock.Text = $"Images found: {count}";
            OutputPdfTextBox.Text = OutputPaths.ResolvePdfPath(folder);
        }
        catch (Exception error)
        {
            ImageCountTextBlock.Text = error.Message;
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var folder = InputFolderTextBox.Text.Trim().Trim('"');
        var output = OutputPdfTextBox.Text.Trim().Trim('"');
        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, "Choose an existing photo folder.", "PhotoSearch PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(output)) output = OutputPaths.ResolvePdfPath(folder);
        if (!output.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) output += ".pdf";
        if (File.Exists(output) && MessageBox.Show(this,
                "This PDF already exists. Replace it?",
                "Confirm replacement",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        var language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "rus+eng";

        _cancellation = new CancellationTokenSource();
        SetRunning(true);
        LogTextBox.Clear();
        AppendLog($"Input: {folder}");
        AppendLog($"Output: {output}");
        AppendLog($"OCR: {language}");

        try
        {
            using var converter = new PhotoPdfConverter(Path.Combine(AppContext.BaseDirectory, "tessdata"));
            var result = await converter.ConvertAsync(
                new ConversionOptions(folder, output, language, RecursiveCheckBox.IsChecked == true),
                new Progress<ConversionProgress>(UpdateProgress),
                _cancellation.Token);

            _completedPdf = result.PdfPath;
            OpenButton.IsEnabled = true;
            QuestionDocumentTextBox.Text = result.PdfPath;
            ProgressBar.Value = ProgressBar.Maximum;
            StatusTextBlock.Text = $"Done: {result.PageCount} pages";
            AppendLog($"PDF: {result.PdfPath}");
            AppendLog($"Markdown: {result.Sidecars.Markdown}");
            AppendLog("Searchable OCR PDF and sidecar files created.");
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Operation canceled";
            AppendLog("Canceled by the user.");
        }
        catch (Exception error)
        {
            StatusTextBlock.Text = "Error";
            AppendLog(error.ToString());
            MessageBox.Show(this, error.Message, "Could not create PDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            SetRunning(false);
        }
    }

    private void UpdateProgress(ConversionProgress progress)
    {
        ProgressBar.Maximum = Math.Max(1, progress.Total);
        ProgressBar.Value = progress.Stage == "Done" ? progress.Total : progress.Completed;
        StatusTextBlock.Text = string.IsNullOrEmpty(progress.CurrentFile)
            ? progress.Stage
            : $"{progress.Stage}: {progress.CurrentFile} ({progress.Completed + 1}/{progress.Total})";
        if (!string.IsNullOrEmpty(progress.CurrentFile)) AppendLog($"[{progress.Completed + 1}/{progress.Total}] {progress.CurrentFile}");
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cancellation?.Cancel();

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var completedPdf = _completedPdf;
        if (completedPdf is not null && File.Exists(completedPdf))
        {
            Process.Start(new ProcessStartInfo(completedPdf) { UseShellExecute = true });
        }
    }

    private void BrowseQuestionDocument_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a searchable PDF or OCR JSON file",
            Filter = "PhotoSearch PDF (*.pdf;*.ocr.json)|*.pdf;*.ocr.json|PDF (*.pdf)|*.pdf|OCR JSON (*.ocr.json)|*.ocr.json",
            CheckFileExists = true
        };
        var currentDirectory = Path.GetDirectoryName(QuestionDocumentTextBox.Text.Trim().Trim('"'));
        if (Directory.Exists(currentDirectory)) dialog.InitialDirectory = currentDirectory;
        if (dialog.ShowDialog(this) == true) QuestionDocumentTextBox.Text = dialog.FileName;
    }

    private async void RefreshCodex_Click(object sender, RoutedEventArgs e) => await RefreshCodexStatusAsync();

    private async Task RefreshCodexStatusAsync()
    {
        RefreshCodexButton.IsEnabled = false;
        CodexStatusTextBlock.Text = "Checking Codex CLI…";
        try
        {
            var service = CreateCodexService();
            if (service is null)
            {
                var wingetAvailable = CodexCliInstaller.FindWinget() is not null;
                CodexStatusTextBlock.Text = wingetAvailable
                    ? "Codex is not installed — the app can install it automatically"
                    : "Codex is not installed; Windows Package Manager was not found";
                LoginCodexButton.Content = wingetAvailable ? "Install and connect" : "Open setup guide";
                LoginCodexButton.IsEnabled = true;
                DisconnectCodexButton.Visibility = Visibility.Collapsed;
                return;
            }

            var status = await service.GetLoginStatusAsync();
            CodexStatusTextBlock.Text = status.Message;
            LoginCodexButton.Content = status.SignedInWithChatGpt ? "Connected" : "Sign in with ChatGPT";
            LoginCodexButton.IsEnabled = !status.SignedInWithChatGpt;
            DisconnectCodexButton.Visibility = status.SignedInWithChatGpt
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (Exception error)
        {
            CodexStatusTextBlock.Text = $"Could not check sign-in: {error.Message}";
        }
        finally
        {
            RefreshCodexButton.IsEnabled = true;
        }
    }

    private async void LoginCodex_Click(object sender, RoutedEventArgs e)
    {
        _questionCancellation = new CancellationTokenSource();
        SetQuestionRunning(true);
        try
        {
            var service = await EnsureCodexReadyAsync(_questionCancellation.Token);
            if (service is not null)
            {
                CodexStatusTextBlock.Text = "Connected using ChatGPT subscription";
                LoginCodexButton.Content = "Connected";
                DisconnectCodexButton.Visibility = Visibility.Visible;
            }
        }
        catch (OperationCanceledException)
        {
            CodexStatusTextBlock.Text = "Connection canceled";
        }
        catch (Exception error)
        {
            CodexStatusTextBlock.Text = "Could not connect OpenAI";
            MessageBox.Show(this, error.Message, "OpenAI connection", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _questionCancellation?.Dispose();
            _questionCancellation = null;
            SetQuestionRunning(false);
            await RefreshCodexStatusAsync();
        }
    }

    private async void DisconnectCodex_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(this,
            "Disconnecting signs Codex out of ChatGPT for this Windows account and affects other Codex apps. Continue?",
            "Disconnect ChatGPT",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;

        var service = CreateCodexService();
        if (service is null)
        {
            CodexStatusTextBlock.Text = "Codex CLI was not found";
            return;
        }

        _questionCancellation = new CancellationTokenSource();
        SetQuestionRunning(true);
        CodexStatusTextBlock.Text = "Disconnecting ChatGPT…";
        try
        {
            await service.LogoutAsync(_questionCancellation.Token);
            CodexStatusTextBlock.Text = "Disconnected from ChatGPT";
        }
        catch (OperationCanceledException)
        {
            CodexStatusTextBlock.Text = "Disconnect canceled";
        }
        catch (Exception error)
        {
            CodexStatusTextBlock.Text = "Could not disconnect ChatGPT";
            MessageBox.Show(this, error.Message, "Disconnect ChatGPT", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _questionCancellation?.Dispose();
            _questionCancellation = null;
            SetQuestionRunning(false);
            await RefreshCodexStatusAsync();
        }
    }

    private async void Ask_Click(object sender, RoutedEventArgs e)
    {
        var documentPath = QuestionDocumentTextBox.Text.Trim().Trim('"');
        var question = QuestionTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            MessageBox.Show(this, "Enter a question about the document.", "PhotoSearch PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _questionCancellation = new CancellationTokenSource();
        SetQuestionRunning(true);
        AnswerTextBox.Clear();
        QuestionStatusTextBlock.Text = "Preparing the OpenAI connection…";
        try
        {
            var service = await EnsureCodexReadyAsync(_questionCancellation.Token);
            if (service is null)
            {
                QuestionStatusTextBlock.Text = "OpenAI setup was not completed";
                return;
            }

            QuestionStatusTextBlock.Text = "Selecting relevant document pages…";
            var context = await Task.Run(
                () => DocumentContextBuilder.Build(documentPath, question),
                _questionCancellation.Token);
            ContextInfoTextBlock.Text = context.IsTruncated
                ? $"Pages selected for context: {context.SelectedPages.Count} of {context.TotalPages}"
                : $"All pages included in context: {context.TotalPages}";

            QuestionStatusTextBlock.Text = "OpenAI is analyzing the document text…";
            var answer = await service.AskAsync(question, context, _questionCancellation.Token);
            AnswerTextBox.Text = answer;
            QuestionStatusTextBlock.Text = "Answer ready";
        }
        catch (OperationCanceledException)
        {
            QuestionStatusTextBlock.Text = "Question canceled";
        }
        catch (Exception error)
        {
            QuestionStatusTextBlock.Text = "Could not get an answer";
            MessageBox.Show(this, error.Message, "Document question", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _questionCancellation?.Dispose();
            _questionCancellation = null;
            SetQuestionRunning(false);
        }
    }

    private void CancelQuestion_Click(object sender, RoutedEventArgs e) => _questionCancellation?.Cancel();

    private static CodexQuestionService? CreateCodexService()
    {
        var invocation = CodexCliLocator.FindInvocation();
        return invocation is null ? null : new CodexQuestionService(invocation);
    }

    private async Task<CodexQuestionService?> EnsureCodexReadyAsync(CancellationToken cancellationToken)
    {
        var service = CreateCodexService();
        if (service is null)
        {
            var winget = CodexCliInstaller.FindWinget();
            if (winget is null)
            {
                var openDocs = MessageBox.Show(this,
                    "Windows Package Manager was not found, so automatic installation is unavailable. Open the official OpenAI setup guide?",
                    "Codex setup",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (openDocs == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(CodexCliInstaller.HelpUrl) { UseShellExecute = true });
                }
                return null;
            }

            var confirmation = MessageBox.Show(this,
                "Document questions require the official Codex CLI from OpenAI. Install it automatically through Windows Package Manager?\n\nNo API key is required.",
                "Install OpenAI Codex",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes) return null;

            CodexStatusTextBlock.Text = "Installing the official OpenAI Codex CLI…";
            QuestionStatusTextBlock.Text = "Installing the ChatGPT connection component…";
            var install = await new CodexCliInstaller(winget).InstallAsync(cancellationToken);
            if (!install.Succeeded) throw new InvalidOperationException(install.Message);

            service = CreateCodexService();
            if (service is null)
            {
                throw new InvalidOperationException(
                    "Codex was installed, but Windows has not refreshed its path yet. Restart PhotoSearch PDF and select Connect OpenAI.");
            }
        }

        CodexStatusTextBlock.Text = "Checking ChatGPT subscription sign-in…";
        var status = await service.GetLoginStatusAsync(cancellationToken);
        if (status.SignedInWithChatGpt) return service;

        CodexStatusTextBlock.Text = "Complete sign-in in the browser window…";
        QuestionStatusTextBlock.Text = "Waiting for ChatGPT sign-in…";
        await service.LoginWithChatGptAsync(cancellationToken);
        status = await service.GetLoginStatusAsync(cancellationToken);
        if (!status.SignedInWithChatGpt)
        {
            throw new InvalidOperationException(
                "ChatGPT subscription sign-in was not confirmed. If Codex is configured with an API key, run `codex logout`, then try connecting again.");
        }
        return service;
    }

    private void SetQuestionRunning(bool running)
    {
        AskButton.IsEnabled = !running;
        CancelQuestionButton.IsEnabled = running;
        QuestionDocumentTextBox.IsEnabled = !running;
        QuestionTextBox.IsEnabled = !running;
        LoginCodexButton.IsEnabled = !running && !Equals(LoginCodexButton.Content, "Connected");
        DisconnectCodexButton.IsEnabled = !running;
        RefreshCodexButton.IsEnabled = !running;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var folder = paths?.FirstOrDefault(Directory.Exists);
        if (folder is not null) InputFolderTextBox.Text = folder;
    }

    private void SetRunning(bool running)
    {
        StartButton.IsEnabled = !running;
        CancelButton.IsEnabled = running;
        InputFolderTextBox.IsEnabled = !running;
        OutputPdfTextBox.IsEnabled = !running;
        LanguageComboBox.IsEnabled = !running;
        RecursiveCheckBox.IsEnabled = !running;
    }

    private void AppendLog(string line)
    {
        LogTextBox.AppendText(line + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }
}
